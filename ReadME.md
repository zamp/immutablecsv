# immutablecsv Plugin Documentation

## 1. Purpose & High‑Level Overview
`immutablecsv` is a lightweight, reflection‑driven data import system for Unity that turns a set of CSV definition files (shipped in `StreamingAssets`) into a runtime, in‑memory `ImmutableDatabase` of strongly typed immutable model objects.

It is designed for:
- Rapid iteration on game balance / content using spreadsheets.
- Deterministic, read‑only data objects ("immutables") referenced anywhere in gameplay code.
- Zero manual wiring: types are auto‑discovered via assemblies + reflection.
- Cross‑referencing between immutable objects by `Name`.

## 2. Core Concepts
| Concept | Summary |
| ------- | ------- |
| `Immutable` | Base class all imported models derive from. Provides `Name`, `Title`, `Description` + validation & lifecycle hooks. |
| `ImmutableDatabase` | Central registry keyed by `Name`. Supports lookup (`Get<T>(name)`), existence tests, iteration, lifecycle passes, and validation. |
| `ImmutableImporter` | Orchestrates reading CSV files, constructing objects, deferred resolution of references, list parsing, and validation. |
| `IDeserializerProvider` / Deserializers | Pluggable chain of strategies that convert string cells to typed values. Order matters. |
| `IAssemblyProvider` | Supplies the assemblies that will be scanned for `Immutable` subclasses. Basic provider uses the assembly that contains `Immutable`. |
| Breadcrumbs | A rolling trace list used for debugging failed imports. Last 10 are surfaced in exceptions. |
| Unresolved Model Queue | Circular / forward references are handled by iteratively attempting to resolve outstanding fields until stable or a safety iteration cap is reached. |

## 3. Import Lifecycle
1. Collect CSV file paths (example: `GameManager` calls `Directory.GetFiles(Globals.ModelsPath, "*.csv")`).
2. `ImmutableImporter.Import(files)`
   - Reads each file → produces `UnresolvedModel` (object + list of field/property assignments still in string form).
   - All models are queued and iteratively resolved. Each pass tries to deserialize pending members; unresolved ones are retried until:
     - All resolved OR
     - `MaxResolveIterations` (1000) exceeded → `RanOutOfResolveStepsException` (often circular dependency or bad reference name).
3. Once fully resolved:
   - `PrePostImport()` pass on every immutable (early preprocessing across whole DB).
   - `PostImport()` pass on every immutable (can link to other immutables confidently now).
   - (Optional) `PostImport<T>(gameState)` variant if used later by gameplay systems.
4. Validation: each immutable's `Validate(Result)` is invoked; any failure throws `ImmutableValidationException` with first error.
5. Database returned. The static reference used by deserializers (`ImmutableDeserializer.ImmutableDatabase`) is cleared.

## 4. CSV File Formats
Each CSV describes one or more instances. Minimal required header: `Class` and any fields/properties you want to set. `Name` must be present as a field/property on the target class (enforced indirectly because name required for DB key and uniqueness).

### 4.1 Standard Row‑Per‑Object Format
Example (`SignatureComponentTypes.csv`):
```
Class,Name,Title,Description,Icon
SignatureComponentType,SignatureComponentTypeVisualMetal,Metallic,A metallic signature. Usually a ship or an asteroid with high metal content.,T_SignatureBox
SignatureComponentType,SignatureComponentTypeVisualMineral,Mineral,"A mineral signature. Asteroids, planets, and moons have mineral signatures.",T_SignatureBox
```
Importer flow:
- For each row, look up `Class` → find a `Type` with matching `Type.Name` among all scanned assemblies.
- Instantiate via parameterless constructor.
- For each header (other than `Class`) try to match a property (public or non‑public instance) first, then a field.
- Store as an `Unresolved` assignment until successfully deserialized.

### 4.2 Key–Value Pair (Vertical) Format
If the file has exactly two headers: `Key` and `Value` it is treated specially:
- All rows pivoted into a single logical row (one object total).
- First you still need a `Class` key specifying the target immutable type.
- Remaining keys become headers with their `Value` cell content.
Use this when you want a singleton config (e.g. `Config.csv`).

### 4.3 Compound / Aggregate Headers
Headers of the form `BaseName(SubKey)` are grouped.
Example (`Signatures.csv`):
```
Class,Name,Title,Description,Components(SignatureComponentTypeVisualMetal),Components(SignatureComponentTypeVisualMineral),Components(SignatureComponentTypeVisualLight)
SignatureData,SignatureShip,Ship,,1,,
SignatureData,SignatureStar,Star,,,,1
```
Importer groups the present, non‑empty values into a synthetic single assignment for `BaseName`:
- Output value string format: `SubKey;Value,SubKey;Value,...`
- In the `SignatureShip` line this becomes: `"SignatureComponentTypeVisualMetal;1"`
- In the `SignatureStar` line: `"SignatureComponentTypeVisualLight;1"`

Your target field/property (e.g. `public readonly ??? Components`) must know how to parse this aggregated string (custom deserializer or manual `PostImport` processing) — the core plugin does not auto‑expand that further.

### 4.4 Ordered Types (`[Ordered]` + `OrderAttribute`)
For compact positional encoding of nested value objects.
- Mark a class with `[Ordered]`.
- Tag each participating field or property with `[Order(n)]` (0-based or any unique int, order sorted by the numeric value).
- In CSV, encode value as `OptionalNestedTypeName:val0;val1;[blockWith;delimiters];val2...`
  - The prefix before the first `:` lets you override which concrete type (by simple `Type.Name`) to instantiate; if omitted the target member type itself is used.
  - Values are split on `;` unless inside a bracketed block `[ ... ]` which is treated as one token (supports nested list representations inside a single ordered slot).
  - Nullables: If you have nullable ordered members, all non‑nullable ones must appear first. Once the parser runs out of tokens, any remaining nullable members are skipped. If a non‑nullable appears after a skipped nullable → error.
Example definition fragment:
```csharp
[Ordered]
public class Range : Immutable // could also be a plain support class
{
    [Order(0)] public readonly int Min;
    [Order(1)] public readonly int Max;
}
```
CSV cell example: `Range:10;25` assigns `Min=10`, `Max=25`.

### 4.5 Lists, Arrays & Weighted Lists
The importer recognizes certain collection forms:
| Type Pattern | Representation | Notes |
| ------------ | -------------- | ----- |
| `T[]` (array) | `a,b,c` or bracketed `[a,b,c]` | Brackets optional; internal logic can parse both. |
| `ImmutableList<T>` | `[a,b,c]` or simple comma list | Empty string → `ImmutableList<T>.Empty`. |
| `WeightedList<T>` | `value:weight,value:weight,...` | Cannot be empty; weight parsed as `double`. |

Internally, brackets trigger a specialized block parser (`ParseArrays`) so nested list/array or composite types encoded inside a single cell can work. If multiple top-level arrays are found in one cell (e.g. `[...] [...]`) it's treated as malformed.

### 4.6 Step Sequences (Integer Expansion)
For `List<int>`-like targets (specifically when the element type is `int` and the raw string matches a step spec), you can use generator syntax:
Form: `LIN(steps,min,max)`, `LOG(...)`, `EXP(...)`, `MUL(...)`:
- `LIN(5,0,100)` → 5 linear steps from 0 to 100.
- `LOG(5,1,1000)` → logarithmic.
- `EXP(5,2,64)` → exponential.
- `MUL(5,2,1.5)` → multiplicative progression (floored each iteration).
If this pattern matches, it overrides normal comma splitting.

### 4.7 Null / Empty Cells
- Empty string deserializes to `null` for nullable / reference types.
- Empty string for arrays / lists produces an empty collection (except `WeightedList<T>` which throws).

## 5. Deserialization Pipeline
The importer tries deserializers in the order defined by `BasicDeserializerProvider`:
1. `ValueTypesDeserializer` – primitives, nullable primitives, numeric types with units, enums (supports symbolic operators like ">=" → `GreaterOrEqual`).
2. `ImmutableDeserializer` – resolves references to previously imported immutables by `Name` (deferred resolution ensures eventual success if ordering allows).
3. `OrderedTypeDeserializer` – `[Ordered]` classes with `[Order]` members and their positional encoding.
4. `AssetRefDeserializer` – (legacy / obsolete pattern) turns a string name into an `AssetRef<T>` wrapper.
5. `TimeSpanDeserializer` – parses composite duration strings like `5h30m` or `250ms` (orderless token accumulation).
6. `TypeRefDeserializer` – wraps a named runtime `Type` in a `TypeRef` value.

If none claim a type → `No deserializer found for type {TypeName}`.

## 6. Reference & Resolution Mechanics
Because an immutable may reference another one defined later (or in another CSV):
- Each string field/property is stored as an `Unresolved` record initially.
- Passes repeatedly attempt to `TryDeserialize`. Successful ones are removed.
- Name uniqueness is enforced early (`CheckNameConflicts`) by inspecting the unresolved `Name` field values. If two objects across any types share a `Name`, import aborts.
- Maximum 1000 iterations guard prevents infinite loops (typical cause: typos in reference names or impossible circular dependency that requires runtime context to break).
- On failure, breadcrumbs show last 10 actions (e.g., file being processed, failed deserialization attempts from `ImmutableDeserializer`).

## 7. Lifecycle Hooks in `Immutable`
Override these in derived types when needed:
| Method | Timing | Purpose |
| ------ | ------ | ------- |
| `PrePostImport` | After all objects constructed & inserted but before `PostImport` | Early normalization / caching before deeper cross-object logic runs. |
| `PostImport(ImmutableDatabase db)` | After all `PrePostImport` passes | Resolve cached references, build indices, compute derived fields. |
| `PostImport<T>(ImmutableDatabase db, T gameState)` | Later game bootstrap stage | Optionally tie immutable data to runtime state. |
| `Validate(Result r)` | After `PostImport` | Return false and add error to abort import with a descriptive exception. |

## 8. Example: Using Imported Data
```csharp
// Somewhere in initialization (GameManager excerpt paraphrased)
var files = Directory.GetFiles(Globals.ModelsPath, "*.csv");
var db = ImmutableImporter.Import(files);

// Retrieve specific immutable
var signature = db.Get<SignatureData>("SignatureStar");

// Iterate all trait data
foreach (var trait in db.GetAll<ActorTraitData>())
{
    // use trait.Category, trait.ActorEffects, etc.
}
```

## 9. Example: Defining a New Immutable Type
```csharp
public class WeaponData : Immutable
{
    public readonly int Damage;                // simple int
    public readonly float Cooldown;            // float (supports units if UnitUtility extended)
    public readonly ImmutableList<ActorTraitData> Tags = ImmutableList<ActorTraitData>.Empty; // list of references
    public readonly WeightedList<SignatureData> SignatureBias = new(); // weighted list
}
```
CSV (e.g., `WeaponData.csv`):
```
Class,Name,Damage,Cooldown,Tags,SignatureBias
WeaponData,WeaponLaser1,10,1.5,[TraitFast,TraitPrecise],SignatureStar:0.7,SignatureShip:0.3
```
Notes:
- The tags list uses bracket form to avoid ambiguity if future parsing logic requires it (both `[TraitFast,TraitPrecise]` and `TraitFast,TraitPrecise` are accepted for `ImmutableList`).
- Weighted list cell merges entries with commas; each entry `value:weight`.

## 10. Adding Custom Parsing Logic
If you introduce a new serialized pattern or wrapper type:
1. Implement `IImmutableDeserializer`.
2. Insert it early in `BasicDeserializerProvider.Deserializers` if it should override a more general handler.
3. Ensure it is deterministic and side‑effect free (it may be invoked multiple times per unresolved entry during resolution attempts).

### Example Skeleton
```csharp
public class ColorDeserializer : IImmutableDeserializer
{
    public bool CanDeserialize(Type t) => t == typeof(Color);
    public bool Deserialize(Type t, string data, out object value)
    {
        if (ColorUtility.TryParseHtmlString(data, out var c)) { value = c; return true; }
        value = null; return false;
    }
}
```
Then register (e.g., make a custom provider or extend the existing one).

## 11. Validation Best Practices
Inside `Validate(Result r)` of an immutable:
```csharp
public override bool Validate(Result r)
{
    r.ErrorOnTrue(string.IsNullOrEmpty(Name), $"{GetType().Name} has empty Name");
    // Example: ensure weights sum ~1
    if (SignatureBias.Count == 0)
        r.ErrorOnTrue(true, $"{Name} has no signature bias entries");
    return r.IsSuccess;
}
```
Fail fast: One error aborts import; add the most actionable one.

## 13. Troubleshooting
| Symptom | Likely Cause | Fix |
| ------- | ------------ | --- |
| `No deserializer found for type` | Missing custom handler or unsupported wrapper type | Add/extend a deserializer. |
| `RanOutOfResolveStepsException` | Circular or typo'd reference name; object never resolves | Check breadcrumbs + verify `Name` strings. |
| `Malformed data` (array parsing) | Brackets mismatched or nested arrays incorrectly concatenated | Ensure each list cell has balanced `[]`. |
| Empty list when expecting values | Forgotten brackets or typo caused fallback to empty string | Inspect raw CSV cell content (trailing spaces trimmed). |
| Duplicate name conflict | Same `Name` across different CSVs | Rename one; `Name` is global key (not per-type). |
| Enum parse errors | Symbolic operator used but not mapped | Verify `ValueTypesDeserializer` operator mapping or expand enum values. |

### Breadcrumbs
Look at the thrown exception's included "Last 10 breadcrumbs:" section to see the most recent import actions (e.g., file starts, failed reference attempts).

## 15. Extending Assembly Scanning
Supply a custom `IAssemblyProvider` if immutables live across multiple assemblies:
```csharp
public class GameAssemblyProvider : IAssemblyProvider
{
    public IEnumerable<Assembly> Assemblies => new []
    {
        typeof(Immutable).Assembly,
        typeof(ActorTraitData).Assembly, // etc.
    };
}
ImmutableImporter.AssemblyProvider = new GameAssemblyProvider();
```
Set this before calling `Import`.

## 17. Safety & Immutability Model
- Fields are `readonly` but assigned via reflection (including private backing fields for auto-properties). Mutability after import depends on you not exposing setters or mutable collections. Use `ImmutableList<T>` over `List<T>` to enforce immutability.

## 19. Known Limitations / Caveats
- Global `Name` namespace: No per‑type scoping.
- No built‑in schema versioning; changing field order or meaning in `[Ordered]` classes is risky across content updates.
- `TypeRefDeserializer` only searches by simple type name in loaded (non-dynamic) assemblies; ambiguous type names can collide silently (first match wins).
- Bracketed block parsing for ordered types does not support nested bracket depth >1. This is by design to keep csv human readable.

## 20. Future Improvement Ideas
- Replace custom recursive assignability checks with standard `IsAssignableFrom` where appropriate.
- Add optional per‑type namespace or alias to allow duplicate `Name` values across different immutable categories.
- Provide built‑in parser for compound aggregate base patterns (e.g., produce dictionary automatically).
- Extend `WeightedList<T>` deserialization to support bracketed groups and inline comments.
- Add CSV schema linting pass to catch unused headers or unmatched class names early.

---
If you need further clarification or want automation helpers (e.g. generator for new immutable classes + CSV template), that can be added on request.

