// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Generates the shared string table of .NET nanoFramework from string-table.json.
// Run with: dotnet run tools/StringGenerator/StringGenerator.cs -- [options]
// See README.md in this folder.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

return StringGenerator.Run(args);

internal static class StringGenerator
{
    private const string MdpConstantsPath = "MetadataProcessor.Shared/Utility/nanoStringsConstants.cs";
    private const string ClrDataPath = "src/CLR/Core/StringTableData.cpp";
    private const string ClrTypesPath = "src/CLR/Include/nanoCLR_Types.h";

    public static int Run(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);

            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            Catalog catalog = Catalog.Load(options.CatalogPath);
            Layout layout = Layout.Build(catalog.Strings, catalog.Stride);
            layout.Verify();

            string hash = catalog.ComputeHash();

            string mdpConstantsFile = Path.GetFullPath(Path.Combine(options.MdpRoot, MdpConstantsPath));
            CheckVersion(catalog, hash, mdpConstantsFile);

            var outputs = new List<(string Path, byte[] Content)>
            {
                (mdpConstantsFile, Utf8WithBom(CSharpWriter.Write(layout, catalog.Version, hash))),
            };

            if (options.NfInterpreterRoot != null)
            {
                outputs.Add((
                    Path.GetFullPath(Path.Combine(options.NfInterpreterRoot, ClrDataPath)),
                    Encoding.ASCII.GetBytes(CppWriter.Write(layout, catalog.Version, hash))));

                string typesFile = Path.GetFullPath(Path.Combine(options.NfInterpreterRoot, ClrTypesPath));
                outputs.Add((typesFile, UpdateClrVersion(typesFile, catalog.Version)));
            }

            PrintSummary(catalog, layout, hash);

            int stale = 0;

            foreach ((string path, byte[] content) in outputs)
            {
                bool upToDate = File.Exists(path)
                    && NormalizeLineEndings(File.ReadAllBytes(path)).AsSpan().SequenceEqual(NormalizeLineEndings(content));

                if (upToDate)
                {
                    Console.WriteLine($"  up to date: {path}");
                    continue;
                }

                if (options.CheckOnly)
                {
                    Console.WriteLine($"  OUT OF DATE: {path}");
                    stale++;
                    continue;
                }

                File.WriteAllBytes(path, content);
                Console.WriteLine($"  written:    {path}");
            }

            if (options.NfInterpreterRoot == null)
            {
                Console.WriteLine();
                Console.WriteLine("nanoCLR files were not generated: pass --nf-interpreter <path> to produce them as well.");
            }

            if (stale > 0)
            {
                Console.Error.WriteLine($"{stale} file(s) out of date. Run without --check to regenerate.");
                return 1;
            }

            return 0;
        }
        catch (GeneratorException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
    }

    // A catalog change renumbers the string IDs, so it must come with a new version.
    private static void CheckVersion(Catalog catalog, string hash, string mdpConstantsFile)
    {
        if (!File.Exists(mdpConstantsFile))
        {
            return;
        }

        string current = File.ReadAllText(mdpConstantsFile);
        Match version = Regex.Match(current, @"internal const ushort Version = (\d+);");
        Match catalogHash = Regex.Match(current, "internal const string CatalogHash = \"([0-9a-f]+)\";");

        if (!version.Success || !catalogHash.Success)
        {
            Console.WriteLine($"note: {mdpConstantsFile} has no version information yet (first generation).");
            return;
        }

        int previousVersion = int.Parse(version.Groups[1].Value);

        if (catalog.Version < previousVersion)
        {
            throw new GeneratorException(
                $"catalog version {catalog.Version} is lower than the version already generated ({previousVersion}).");
        }

        string previousHash = catalogHash.Groups[1].Value;
        bool unchanged = previousHash == hash || previousHash == catalog.ComputeLegacyHash();

        if (!unchanged && catalog.Version == previousVersion)
        {
            throw new GeneratorException(
                $"the strings changed but the catalog version is still {previousVersion}. "
                + "A new string table is a breaking change: increase 'version' in the catalog.");
        }
    }

    private static byte[] UpdateClrVersion(string typesFile, int version)
    {
        if (!File.Exists(typesFile))
        {
            throw new GeneratorException($"{typesFile} not found. Is --nf-interpreter the root of an nf-interpreter checkout?");
        }

        byte[] original = File.ReadAllBytes(typesFile);
        bool hasBom = original.Length >= 3 && original[0] == 0xEF && original[1] == 0xBB && original[2] == 0xBF;
        string text = new UTF8Encoding(false).GetString(original, hasBom ? 3 : 0, original.Length - (hasBom ? 3 : 0));

        var define = new Regex(@"#define c_CLR_StringTable_Version \d+");

        if (!define.IsMatch(text))
        {
            throw new GeneratorException($"'#define c_CLR_StringTable_Version' not found in {typesFile}.");
        }

        text = define.Replace(text, $"#define c_CLR_StringTable_Version {version}", 1);
        byte[] body = new UTF8Encoding(false).GetBytes(text);

        return hasBom ? [0xEF, 0xBB, 0xBF, .. body] : body;
    }

    // a checkout may have LF or CRLF line endings: only the text is compared
    private static byte[] NormalizeLineEndings(byte[] content)
    {
        var normalized = new List<byte>(content.Length);

        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
            {
                continue;
            }

            normalized.Add(content[i]);
        }

        return [.. normalized];
    }

    private static byte[] Utf8WithBom(string text) => [0xEF, 0xBB, 0xBF, .. new UTF8Encoding(false).GetBytes(text)];

    private static void PrintSummary(Catalog catalog, Layout layout, string hash)
    {
        Console.WriteLine($"String table version {catalog.Version}, catalog SHA-256 {hash}");
        Console.WriteLine(
            $"  {layout.Order.Count} strings ({layout.RootCount} stored, {layout.Order.Count - layout.RootCount} pooled suffixes), "
            + $"IDs 0x{0x10000 - layout.Order.Count:X4}-0xFFFF");
        Console.WriteLine(
            $"  nanoCLR: data {layout.Data.Length} B + lookup {layout.Lookup.Count * 2} B = {layout.Data.Length + layout.Lookup.Count * 2} B "
            + $"(checkpoint every {layout.Stride} strings)");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Generates the shared string table for the Metadata Processor and nanoCLR.");
        Console.WriteLine();
        Console.WriteLine("dotnet run tools/StringGenerator/StringGenerator.cs -- [options]");
        Console.WriteLine();
        Console.WriteLine("  --nf-interpreter <path>  nf-interpreter checkout: writes StringTableData.cpp and updates");
        Console.WriteLine("                           c_CLR_StringTable_Version in nanoCLR_Types.h");
        Console.WriteLine("  --catalog <file>         Catalog to use (default: string-table.json next to this program)");
        Console.WriteLine("  --mdp <path>             Metadata Processor checkout (default: the one holding this program)");
        Console.WriteLine("  --check                  Only verify that the generated files are up to date (exit code 1 if not)");
        Console.WriteLine("  --help                   Show this help");
    }
}

internal sealed class GeneratorException(string message) : Exception(message);

internal sealed class Options
{
    public string CatalogPath { get; private set; } = "";
    public string MdpRoot { get; private set; } = "";
    public string? NfInterpreterRoot { get; private set; }
    public bool CheckOnly { get; private set; }
    public bool ShowHelp { get; private set; }

    public static Options Parse(string[] args)
    {
        string toolDirectory = AppContext.GetData("EntryPointFileDirectoryPath") as string
            ?? Path.Combine(Directory.GetCurrentDirectory(), "tools", "StringGenerator");

        var options = new Options
        {
            CatalogPath = Path.Combine(toolDirectory, "string-table.json"),
            MdpRoot = Path.GetFullPath(Path.Combine(toolDirectory, "..", "..")),
        };

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--catalog":
                    options.CatalogPath = Value(args, ref i);
                    break;
                case "--mdp":
                    options.MdpRoot = Value(args, ref i);
                    break;
                case "--nf-interpreter":
                    options.NfInterpreterRoot = Value(args, ref i);
                    break;
                case "--check":
                    options.CheckOnly = true;
                    break;
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                default:
                    throw new GeneratorException($"unknown option '{args[i]}'. Use --help.");
            }
        }

        return options;
    }

    private static string Value(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
        {
            throw new GeneratorException($"option '{args[i]}' needs a value.");
        }

        return args[++i];
    }
}

internal sealed class Catalog
{
    public int Version { get; private init; }
    public int Stride { get; private init; }
    public IReadOnlyList<string> Strings { get; private init; } = [];

    public static Catalog Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new GeneratorException($"catalog '{path}' not found.");
        }

        CatalogFile? file;

        try
        {
            file = JsonSerializer.Deserialize(File.ReadAllText(path), CatalogJsonContext.Default.CatalogFile);
        }
        catch (JsonException ex)
        {
            throw new GeneratorException($"catalog '{path}' is not valid JSON: {ex.Message}");
        }

        if (file?.Strings == null || file.Strings.Length == 0)
        {
            throw new GeneratorException("the catalog has no strings.");
        }

        if (file.Version is < 1 or > ushort.MaxValue)
        {
            throw new GeneratorException("'version' must be between 1 and 65535.");
        }

        if (file.Stride is < 1 or > 64 || (file.Stride & (file.Stride - 1)) != 0)
        {
            throw new GeneratorException("'stride' must be a power of two between 1 and 64.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string value in file.Strings)
        {
            if (string.IsNullOrEmpty(value) || value.Contains('\0'))
            {
                throw new GeneratorException("catalog strings must be non empty and must not contain NUL characters.");
            }

            if (!seen.Add(value))
            {
                throw new GeneratorException($"duplicate string '{value}'. The ID comparison in nanoCLR requires unique strings.");
            }
        }

        // keep well clear of the local string offsets, which must stay below the first shared ID
        if (file.Strings.Length > 0x2000)
        {
            throw new GeneratorException("too many strings (maximum 8192).");
        }

        return new Catalog { Version = file.Version, Stride = file.Stride, Strings = file.Strings };
    }

    // each string as a 32-bit little endian UTF-8 length followed by its bytes, in UTF-8 ordinal order
    public string ComputeHash()
    {
        using var data = new MemoryStream();
        Span<byte> length = stackalloc byte[4];

        foreach (string value in Strings.OrderBy(s => s, Utf8Ordinal.Instance))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            data.Write(length);
            data.Write(bytes);
        }

        return Convert.ToHexStringLower(SHA256.HashData(data.ToArray()));
    }

    // hash of the first generated files, newline terminated strings: still accepted so they validate
    public string ComputeLegacyHash()
    {
        var text = new StringBuilder();

        foreach (string value in Strings.OrderBy(s => s, Utf8Ordinal.Instance))
        {
            text.Append(value).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}

internal sealed class CatalogFile
{
    public int Version { get; set; }
    public int Stride { get; set; } = 8;
    public string[]? Strings { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(CatalogFile))]
internal sealed partial class CatalogJsonContext : JsonSerializerContext;

// Orders strings by their UTF-8 bytes, which is what the layout and the hash are defined on.
internal sealed class Utf8Ordinal : IComparer<string>
{
    public static readonly Utf8Ordinal Instance = new();

    public int Compare(string? x, string? y) =>
        Encoding.UTF8.GetBytes(x ?? "").AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(y ?? ""));
}

// Shared IDs are 0xFFFF - index. Indexes 0..RootCount-1 are the strings stored in the data blob, in blob order;
// the remaining indexes are suffixes of a stored string and point into it.
// Lookup = one offset per Stride stored strings (checkpoints), then one exact offset per pooled suffix.
internal sealed class Layout
{
    public int Stride { get; private init; }
    public int RootCount { get; private init; }
    public IReadOnlyList<string> Order { get; private init; } = [];
    public IReadOnlyList<int> Lookup { get; private init; } = [];
    public int CheckpointCount { get; private init; }
    public byte[] Data { get; private init; } = [];

    public static Layout Build(IReadOnlyList<string> strings, int stride)
    {
        List<string> ordinal = strings.OrderBy(s => s, Utf8Ordinal.Instance).ToList();
        Dictionary<string, byte[]> encoded = ordinal.ToDictionary(s => s, s => (byte[])[.. Encoding.UTF8.GetBytes(s), 0], StringComparer.Ordinal);

        List<string> roots = ordinal
            .Where(s => !ordinal.Any(other => other != s && EndsWith(encoded[other], encoded[s])))
            .ToList();
        var rootSet = new HashSet<string>(roots, StringComparer.Ordinal);
        List<string> pooled = ordinal.Where(s => !rootSet.Contains(s)).ToList();

        var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
        var data = new List<byte>();

        foreach (string root in roots)
        {
            offsets[root] = data.Count;
            data.AddRange(encoded[root]);
        }

        foreach (string suffix in pooled)
        {
            string root = roots.First(r => EndsWith(encoded[r], encoded[suffix]));
            offsets[suffix] = offsets[root] + encoded[root].Length - encoded[suffix].Length;
        }

        if (data.Count > ushort.MaxValue)
        {
            throw new GeneratorException($"string data is {data.Count} bytes; offsets are limited to 16 bits.");
        }

        var lookup = new List<int>();

        for (int i = 0; i < roots.Count; i += stride)
        {
            lookup.Add(offsets[roots[i]]);
        }

        int checkpointCount = lookup.Count;
        lookup.AddRange(pooled.Select(s => offsets[s]));

        return new Layout
        {
            Stride = stride,
            RootCount = roots.Count,
            Order = [.. roots, .. pooled],
            Lookup = lookup,
            CheckpointCount = checkpointCount,
            Data = [.. data],
        };
    }

    public int StrideShift => System.Numerics.BitOperations.Log2((uint)Stride);

    // Resolves every index exactly as nanoCLR's GetString does.
    public void Verify()
    {
        for (int index = 0; index < Order.Count; index++)
        {
            int position;

            if (index < RootCount)
            {
                position = Lookup[index >> StrideShift];

                for (int skip = index & (Stride - 1); skip > 0; skip--)
                {
                    position = Array.IndexOf(Data, (byte)0, position) + 1;
                }
            }
            else
            {
                position = Lookup[CheckpointCount + index - RootCount];
            }

            int end = Array.IndexOf(Data, (byte)0, position);
            string resolved = Encoding.UTF8.GetString(Data, position, end - position);

            if (resolved != Order[index])
            {
                throw new GeneratorException($"layout check failed at index {index}: '{resolved}' instead of '{Order[index]}'.");
            }
        }
    }

    private static bool EndsWith(byte[] value, byte[] suffix) =>
        value.Length >= suffix.Length && value.AsSpan(value.Length - suffix.Length).SequenceEqual(suffix);
}

internal static class CSharpWriter
{
    public static string Write(Layout layout, int version, string hash)
    {
        var code = new StringBuilder();
        Line(code, "// Licensed to the .NET Foundation under one or more agreements.");
        Line(code, "// The .NET Foundation licenses this file to you under the MIT license.");
        Line(code, "");
        Line(code, "// Original work from Oleg Rakhmatulin.");
        Line(code, "");
        Line(code, "// Generated by tools/StringGenerator from string-table.json. Do not edit: see tools/StringGenerator/README.md.");
        Line(code, "");
        Line(code, "using System;");
        Line(code, "using System.Collections.Generic;");
        Line(code, "using System.Linq;");
        Line(code, "");
        Line(code, "namespace nanoFramework.Tools.MetadataProcessor");
        Line(code, "{");
        Line(code, "    /// <summary>");
        Line(code, "    /// Helper class for storing list of 'predefined' string constants which encoded in");
        Line(code, "    /// .NET nanoFramework assembly metadata using special notation (highest possible values).");
        Line(code, "    /// </summary>");
        Line(code, "    internal static class nanoStringsConstants");
        Line(code, "    {");
        Line(code, "        /// <summary>");
        Line(code, "        /// String table version written in the assembly header. nanoCLR only loads assemblies with its own version.");
        Line(code, "        /// </summary>");
        Line(code, $"        internal const ushort Version = {version};");
        Line(code, "");
        Line(code, "        /// <summary>");
        Line(code, "        /// SHA-256 of the catalog strings, used by the generator to detect a change without a version bump.");
        Line(code, "        /// </summary>");
        Line(code, $"        internal const string CatalogHash = \"{hash}\";");
        Line(code, "");
        Line(code, "        /// <summary>");
        Line(code, "        /// Gets string reference identifier for passed string from constants table.");
        Line(code, "        /// </summary>");
        Line(code, "        /// <param name=\"value\">String value for lookup.</param>");
        Line(code, "        /// <param name=\"index\">String reference identifier.</param>");
        Line(code, "        /// <returns>Returns <c>true</c> if string found in constants table, <c>false</c> otherwise.</returns>");
        Line(code, "        public static bool TryGetStringIndex(");
        Line(code, "            string value,");
        Line(code, "            out ushort index)");
        Line(code, "        {");
        Line(code, "            return _idsByStrings.TryGetValue(value, out index);");
        Line(code, "        }");
        Line(code, "");
        Line(code, "        /// <summary>");
        Line(code, "        /// Try to get a string value from the string constant table providing the index.");
        Line(code, "        /// </summary>");
        Line(code, "        /// <param name=\"index\">String index in table.</param>");
        Line(code, "        /// <returns>The string value or a null if the index doesn't exist.</returns>");
        Line(code, "        public static string TryGetString(ushort index)");
        Line(code, "        {");
        Line(code, "            // try to get string from id table");
        Line(code, "            return _idsByStrings.FirstOrDefault(s => s.Value == index).Key;");
        Line(code, "        }");
        Line(code, "");
        Line(code, "        /// <summary>");
        Line(code, "        /// Lowest identifier used by the constants table; local string offsets must stay below it.");
        Line(code, "        /// </summary>");
        Line(code, "        internal static ushort FirstId => (ushort)(0x10000 - _idsByStrings.Count);");
        Line(code, "");
        Line(code, "        private static readonly IDictionary<string, ushort> _idsByStrings = new[]");
        Line(code, "        {");

        for (int i = 0; i < layout.Order.Count; i++)
        {
            Line(code, $"             /* {i:X4} */ \"{Escape(layout.Order[i])}\",");
        }

        Line(code, "        }");
        Line(code, "            .Select((value, index) => new { value, index = (ushort)(0xFFFF - index) })");
        Line(code, "            .ToDictionary(item => item.value, item => item.index, StringComparer.Ordinal);");
        Line(code, "    }");
        Line(code, "}");

        return code.ToString();
    }

    private static string Escape(string value)
    {
        var escaped = new StringBuilder();

        foreach (char c in value)
        {
            escaped.Append(c switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                // C# line terminators (U+0085, U+2028, U+2029) and other control characters can't be in a literal
                < ' ' or (> '~' and < '\u00A0') or '\u2028' or '\u2029' => $"\\u{(int)c:X4}",
                _ => c.ToString(),
            });
        }

        return escaped.ToString();
    }

    private static void Line(StringBuilder code, string text) => code.Append(text).Append("\r\n");
}

internal static class CppWriter
{
    public static string Write(Layout layout, int version, string hash)
    {
        var code = new StringBuilder();
        Line(code, "//");
        Line(code, "// Copyright (c) .NET Foundation and Contributors");
        Line(code, "// See LICENSE file in the project root for full license information.");
        Line(code, "//");
        Line(code, "// Generated by the Metadata Processor tools/StringGenerator. Do not edit.");
        Line(code, $"// String table version {version}, catalog SHA-256 {hash}");
        Line(code, "// Shared string table, suffix pooled, with a sparse lookup.");
        Line(code, "// Index = 0xFFFF - string ID. Indexes below c_CLR_StringTable_RootCount are stored in blob order and");
        Line(code, "// are reached from the checkpoint (index >> c_CLR_StringTable_StrideShift) by skipping strings;");
        Line(code, "// the remaining indexes are suffixes of a root and have an exact offset after the checkpoints.");
        Line(code, "//");
        Line(code, "#if defined(__GNUC__)");
        Line(code, "#include <stddef.h>");
        Line(code, "#include <nanoCLR_Types.h>");
        Line(code, "#endif");
        Line(code, "");
        Line(code, $"static_assert(c_CLR_StringTable_Version == {version}, \"String table data and c_CLR_StringTable_Version differ\");");
        Line(code, "");
        Line(code, $"static const size_t c_CLR_StringTable_Size = {layout.Order.Count};");
        Line(code, $"static const size_t c_CLR_StringTable_RootCount = {layout.RootCount};");
        Line(code, $"static const size_t c_CLR_StringTable_StrideShift = {layout.StrideShift};");
        Line(code, $"static const size_t c_CLR_StringTable_CheckpointCount = {layout.CheckpointCount};");
        Line(code, "");
        Line(code, "// clang-format off");
        Line(code, "");
        Line(code, "static const CLR_STRING c_CLR_StringTable_Lookup[] =");
        Line(code, "{");

        for (int i = 0; i < layout.CheckpointCount; i++)
        {
            Line(code, $"    /* checkpoint {i << layout.StrideShift:X4} */ {layout.Lookup[i]},");
        }

        for (int i = layout.CheckpointCount; i < layout.Lookup.Count; i++)
        {
            Line(code, $"    /* {layout.RootCount + i - layout.CheckpointCount:X4} */ {layout.Lookup[i]},");
        }

        Line(code, "};");
        Line(code, "");
        Line(code, "static const char c_CLR_StringTable_Data[] =");

        for (int i = 0; i < layout.RootCount; i++)
        {
            Line(code, $"    /* {i:X4} */ \"{Escape(layout.Order[i])}\\0\"");
        }

        Line(code, ";");
        Line(code, "");
        Line(code, "// clang-format on");

        return code.ToString();
    }

    // Octal escapes for anything outside printable ASCII: they never swallow the following characters.
    private static string Escape(string value)
    {
        var escaped = new StringBuilder();

        foreach (byte b in Encoding.UTF8.GetBytes(value))
        {
            escaped.Append(b switch
            {
                (byte)'\\' => "\\\\",
                (byte)'"' => "\\\"",
                >= 0x20 and <= 0x7E => ((char)b).ToString(),
                _ => "\\" + Convert.ToString(b, 8).PadLeft(3, '0'),
            });
        }

        return escaped.ToString();
    }

    private static void Line(StringBuilder code, string text) => code.Append(text).Append("\r\n");
}
