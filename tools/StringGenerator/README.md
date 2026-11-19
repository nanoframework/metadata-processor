# String table generator

Generates the **shared string table** used by the Metadata Processor (MDP) and by nanoCLR from a single catalog, [`string-table.json`](string-table.json).

Strings in the shared table are not stored in the PEs: a PE refers to them by ID, and nanoCLR holds the strings in flash. Any other string is stored in the PE's own string table. Both sides must have the same table with the same IDs, so the two generated files always change together.

## ⚠️ A new string table is a breaking change

Adding, removing or renaming any entry renumbers the string IDs. Then:

- PEs produced by the previous MDP resolve the wrong strings on the new firmware, and the other way round.
- The only protection is the **string table version** in the PE header. nanoCLR rejects any assembly whose version differs from its own (`HeaderStatus_BadStringTableVersion`).

So every change to the strings **must** come with a new version:

1. Increase `version` in `string-table.json`. Never reuse a version number that has been published.
2. Regenerate (see below). The generator writes the version to both repositories:
   - MDP: `nanoStringsConstants.Version`, written to every PE header.
   - nanoCLR: `c_CLR_StringTable_Version` in `src/CLR/Include/nanoCLR_Types.h`. The generated `StringTableData.cpp` also has a `static_assert` on it, so a firmware build with a stale version fails.
3. Release both together. Every class library and application has to be recompiled with the new MDP, the NuGet packages republished, and devices flashed with firmware built from the new table.

The generator enforces step 1: it stores a hash of the strings in the generated C# file and refuses to run when the strings changed but the version did not.

Changing only `stride` is **not** a breaking change. The IDs stay the same, only the nanoCLR lookup changes, so no version bump is needed.

## Requirements

The .NET 10 SDK, pinned by the repository's `global.json`. The generator is a single-file C# program, so no project or build step is needed.

## Usage

From the root of this repository:

```console
dotnet run tools/StringGenerator/StringGenerator.cs -- --nf-interpreter <path to nf-interpreter checkout>
```

| Option | Description |
|---|---|
| `--nf-interpreter <path>` | nf-interpreter checkout. Writes `src/CLR/Core/StringTableData.cpp` and updates `c_CLR_StringTable_Version` in `src/CLR/Include/nanoCLR_Types.h`. Without it only the MDP file is generated. |
| `--catalog <file>` | Catalog to use. Default: `string-table.json` in this folder. |
| `--mdp <path>` | MDP checkout to write to. Default: the one this program is in. |
| `--check` | Write nothing. Exit code 1 if any generated file is out of date, for example in CI. |
| `--help` | Show the options. |

The output lists the size of the nanoCLR table and which files were written or are already up to date.

## Generated files and what to do with them

| File | Repository | What to do |
|---|---|---|
| `MetadataProcessor.Shared/Utility/nanoStringsConstants.cs` | Metadata Processor | Commit. Rebuild and run the unit tests. `nanoStringsConstantsTests` uses fixed IDs and must be updated when the order changes. |
| `src/CLR/Core/StringTableData.cpp` | nf-interpreter | Commit. Rebuild the firmware and the virtual device. |
| `src/CLR/Include/nanoCLR_Types.h` (version line only) | nf-interpreter | Commit with the data file. |

Do not edit the generated files by hand: run the generator again. Open the MDP and nf-interpreter pull requests together and cross-reference them. Run the MDP integration tests against a virtual device built from the nf-interpreter change: set `NF_MDP_NANOCLR_INSTANCE_PATH` to its `nanoFramework.nanoCLR.dll`.

## Changing the strings, step by step

Adding, removing and replacing strings all follow the same steps. Only step 2 differs.

1. **Create a branch** in both repositories: this one and nf-interpreter.
2. **Edit `strings` in `string-table.json`:**
   - **Add:** insert the new string anywhere in the list. The generator sorts the strings, so the position in the file doesn't matter. Keeping the list sorted makes reviews easier.
   - **Remove:** delete the line.
   - **Replace:** edit the line, or delete the old string and add the new one.

   Strings use JSON escaping: write `\"` for a quote and `\\` for a backslash. Each string can appear only once. Before adding a string, see [Choosing the strings](#choosing-the-strings).
3. **Bump `version`** in `string-table.json` to the next unused number. This is mandatory for any change in step 2: see the breaking change section at the top.
4. **Run the generator** from the root of this repository:

   ```console
   dotnet run tools/StringGenerator/StringGenerator.cs -- --nf-interpreter <path to nf-interpreter checkout>
   ```

   It reports the new nanoCLR table size and writes the three files. If it stops with an error, fix the catalog and run it again.
5. **Review the diffs.** The IDs of most strings move, even for a single added string, so expect the whole array in `nanoStringsConstants.cs` and the whole data in `StringTableData.cpp` to change. Check that the version is the new one in all three files.
6. **Update the MDP unit tests.** `nanoStringsConstantsTests` looks up strings at fixed IDs. Take the new index of each string from its `/* XXXX */` comment in `nanoStringsConstants.cs`; the ID is `0xFFFF - index`. Then build the solution and run the unit tests.
7. **Test against nanoCLR.** Build the virtual device from the nf-interpreter branch and run the MDP tests, so the integration tests load PEs built with the new table.
8. **Confirm nothing is stale:**

   ```console
   dotnet run tools/StringGenerator/StringGenerator.cs -- --nf-interpreter <path to nf-interpreter checkout> --check
   ```

9. **Commit and open both pull requests together**, cross-referencing each other. They must be merged and released together. The release then needs:
   - a new MDP package;
   - every class library recompiled and republished;
   - new firmware.

## Catalog format

```json
{
  "version": 2,
  "stride": 8,
  "strings": [ ",", "-", ".cctor", "..." ]
}
```

- `version`: the string table version, 1 to 65535.
- `stride`: nanoCLR lookup granularity, a power of two from 1 to 64 (see below).
- `strings`: the shared strings. They must be unique, non-empty and contain no NUL character. Their order in the file does not matter.

## How the table is laid out

- **IDs.** The strings are numbered from `0xFFFF` downwards. Local string offsets in a PE must stay below the lowest shared ID; the MDP checks this.
- **Suffix pooling.** A string that is the ending of another one (`Item` in `get_Item`) is not stored again: it points into the longer string. The stored strings get the first IDs, in blob order; the pooled ones follow.
- **Sparse lookup.** nanoCLR keeps one offset every `stride` stored strings, plus an exact offset for each pooled string. `GetString` jumps to the nearest checkpoint and skips up to `stride - 1` strings. A larger stride saves flash and costs a longer scan per lookup:

  | Stride | Lookup bytes (699 strings) | Average bytes scanned |
  |---:|---:|---:|
  | 2 | 788 | 7 |
  | 4 | 484 | 22 |
  | 8 | 332 | 50 |
  | 16 | 256 | 109 |

- **ID comparison.** During type, field and method resolution nanoCLR compares two shared IDs directly instead of their strings. This relies on the strings being unique, which the generator enforces.

The generator checks that every ID resolves to its string through this layout before writing anything.

## Choosing the strings

- **Cost.** Each entry costs its length plus one byte in the firmware (less when pooled), plus its share of the lookup.
- **Saving.** It saves its length plus one byte in every PE that uses it.
- **What to include:** strings used by several assemblies, and strings that most deployments carry: all of mscorlib, and common generic names such as ``List`1``, ``Dictionary`2``, `TKey` and `TValue`.
- **What to leave out:** strings used by a single library other than mscorlib cost more flash than they save.
- **Assembly names.** The MDP always stores assembly names in the PE itself, so listing them saves nothing.
