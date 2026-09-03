# Pdbx TypeSpec generic-argument representation

Companion to `Pdbx.cs` / `PdbxFileHelpers.cs`. Captures the reasoning behind
`TypeSpec.GenericArguments` so the source files can stay terse.

**Documentation policy:** source comments in this directory stay minimal —
one line flagging a non-obvious invariant at the point it matters. Deductions,
empirical findings, and "why not the other approach" belong here, with a
`see Pdbx/CLAUDE.md "Section Title"` pointer left in the code.

---

## 1. Why arguments are addressed by NanoCLR token, not CLR token

`TypeSpec.Token.CLRToken` is fabricated (`new MetadataToken(TokenType.TypeSpec,
nanoToken)`), not `item.MetadataToken`. Verified empirically against
`Mono.Cecil.dll` directly (small standalone harness, not the MDP pipeline):

| Where the generic instance appears | `TypeReference.MetadataToken.RID` |
|---|---|
| Field type only | `0` |
| Method return type only | `0` |
| Explicit interface implementation | real |
| `MemberRef` declaring type / IL operand | real |

Per ECMA-335, a real PE `TypeSpec` table row exists only when something needs
a token for that instantiation (an IL operand, or a `MemberRef`'s `Class`
column). A closed generic type used only as a field's or method's declared
type is encoded inline in the signature blob — Cecil constructs a
`GenericInstanceType` for it with no backing table row, so `MetadataToken`
is RID 0.

So every reference in this model — `GenericTypeDef`, `TypeSpecArg.TypeToken`,
`TypeSpecArg.GenericParamToken` — is a **NanoCLR** token, the one
`nanoTypeSpecificationsTable`/`nanoGenericParamTable` themselves assign. The
wire protocol (`Debugging_Resolve_Type`) already resolves these; nothing on
the CLR/firmware side had to change for this feature.

## 2. Why classes get a name fallback (`ClassName`, `GenericTypeDefName`)

This model has no `TypeRef` table. A NanoCLR TypeDef token only exists for a
class declared in the assembly currently being processed — there is no way
to address a class declared in a *different* assembly by token here. Cecil's
own `TypeReference.FullName` (never passed through `FixTypeNames`, which
rewrites primitive names to ILAsm short forms and would corrupt an ordinary
class name) is recorded alongside the token as a name-based fallback, mirroring
`Class.Name`. The consumer resolves the token first (unambiguous, cheap) and
falls back to a cross-assembly by-name lookup only when it's null.

## 3. Bare generic parameters (VAR/MVAR) as arguments

`GenParamContainer<T>.Slot`, typed `Pair<T,int>`, is common — any generic
type that uses another generic type internally, closing over its own type
parameter. `GenericInstanceType.GenericArguments` can contain a bare
`GenericParameter` (`T`) instead of a closed type. Confirmed empirically: 11
occurrences in one 3-member test class; `GenericParameter is TypeSpecification`
is `False` (siblings under `TypeReference`, not a subtype); `.Resolve()`
returns `null` rather than throwing.

Falling through to the ordinary-class branch for this case doesn't crash —
`ResolveLocalClassToken` degrades safely — but it records a meaningless,
unqualified `ClassName` ("T") with no token, losing which parameter it is and
whose type or method declares it. `TypeSpecArg.IsGenericParameter` +
`GenericParamToken`/`GenericParamIsMethodOwned`/`GenericParamPosition` address
it directly instead: the parameter's own declaration already has a real,
stable Cecil `MetadataToken` (declarations are real table rows, unlike
generic-instance arguments — see §1), resolved via the existing
`nanoGenericParamTable.TryGetParameterId`.

Verified against real Cecil data that type-owned and method-owned parameters
land in the same `GenericArguments` list with distinct tokens even at the same
`Position` (e.g. `Pair<U,T>` inside a method `Wrap<U>` on `Container<T>`: `U`
is MVAR position 0 with one token, `T` is VAR position 0 with a different one)
— `GenericParamIsMethodOwned` is what disambiguates them.

Tests: `TestNFApp/GenericParameterArgumentTypeSpecTests.cs` (fixture, both
cases) and `Core/Tables/nanoTypeSpecArgGenericParameterTests.cs` (assertions).

## 4. Consumer side: `CorDebugTypeParameter` (vs-extension.shared/CorDebug/CorDebugType.cs)

The CLR reports a generic instance as `DATATYPE_CLASS`/`DATATYPE_VALUETYPE`
with `HB_GenericInstance` set, `m_td` the open TypeDef and `m_ts` the closed
TypeSpec. `CorDebugTypeParameter.FromRuntimeValue` resolves `m_ts` to its
`Pdbx.TypeSpec` and walks `GenericArguments`, resolving each against the
*owning* assembly (the one that owns the TypeSpec, not the caller's) since
`TypeToken`/`GenericParamToken` are local-assembly tokens (§1).

Fails closed on the whole instance if *any* argument can't be resolved,
rather than rendering a name with holes — the caller (`EnumerateTypeParameters`)
then returns `E_NOTIMPL`, which is exactly the behaviour that shipped before
this feature, so an unresolvable instance still renders as the open type.

## 5. `ResolveLocalClassToken` and unresolvable foreign assemblies

`TypeReference.Resolve()` throws `Mono.Cecil.AssemblyResolutionException`
when the type's declaring assembly can't be located by the resolver (not
restored, not on the search path, ...). `BuildTypeSpecArg` calls this for
every non-primitive, non-generic-parameter argument encountered anywhere in
the assembly, so an unresolvable foreign reference must not take down the
whole pdbx generation — it is exactly the "not a local TypeDef" case
(`ResolveLocalClassToken` already returns `null` for a class it can resolve
but that isn't registered locally), so the exception is caught and treated
the same way.
