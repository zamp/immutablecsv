# immutablecsv Plugin Documentation

## 1. Purpose & High‑Level Overview
`immutablecsv` is a lightweight, reflection‑driven data import system for Unity (will probably work outside of Unity too) that turns a set of CSV data files into a runtime, in‑memory `ImmutableDatabase` of strongly typed immutable model objects.

### Usage:
CSV:
```
Class,Name,Value
SomeData,SomeDataInCsv,1234

Class,Value,ValueProperty
SomeConfig,4321,Foobar
```

C#:
```csharp
class SomeData : Immutable
{
    public readonly int Value = 0; // Default value will be used if not present in CSV.
}
class SomeConfig : Immutable
{
    public readonly int Value = 0; // Default value will be used if not present in CSV.
    public string ValueProperty { get; } = "No data in csv"; // Both readonly values and properties are supported.
}
class CustomAssemblyProvider : IAssemblyProvider
{
    IEnumerable<Assembly> IAssemblyProvider.GetAssemblies()
    {
        // Add your own assemblies here.
        yield return typeof(SomeData).Assembly;
    }
}

// Import csv
var files = Directory.GetFiles("PATH_WITH_CSV_FILES", "*.csv");

// You can override the default provider if you have custom deserializers.
//ImmutableImporter.DeserializerProvider = new CustomDeserializerProvider();

// You should override the default provider, otherwise your own immutable types are not found.
ImmutableImporter.AssemblyProvider = new CustomAssemblyProvider();

var db = ImmutableImporter.Import(files);

// Use case 1: Retrieve specific SomeData immutable by name. Names are type specific
var someData = db.Get<SomeData>("SomeDataInCsv");
Debug.Log(someData.Value); // 1234

// Use case 2: Iterate all immutables of type
foreach (var moreData in db.GetAll<SomeData>())
{
    // Use moreData here
    Debug.Log(moreData.Value); // 1234
}

// Use case 3: csv data can be given no name. The importer will use the class name as a key.
var someConfig = db.Get<SomeConfig>();
Debug.Log(someConfig.Value); // 4321
Debug.Log(someConfig.ValueProperty); // 4321
```

## 2. Core Concepts
| Concept | Summary |
| ------- | ------- |
| `Immutable` | Base class all imported models derive from. Provides `Name`, `Title`, `Description` + validation & lifecycle hooks. |
| `ImmutableDatabase` | Central registry keyed by `Name`. Supports lookup (`Get<T>(name)`), existence tests, iteration, lifecycle passes, and validation. |
| `ImmutableImporter` | Orchestrates reading CSV files, constructing objects, deferred resolution of references, list parsing, and validation. |
| `IDeserializerProvider` / Deserializers | Pluggable chain of strategies that convert string cells to typed values. Order matters. |
| `IAssemblyProvider` | Supplies the assemblies that will be scanned for `Immutable` subclasses. Basic provider uses the assembly that contains `Immutable`. |

## 3. Import Lifecycle
1. Collect CSV file paths (example: call `var files = Directory.GetFiles("MY_CSV_PATH", "*.csv")`).
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
Example data:
```
Class,Name,Title,Description,Icon
SomeData,MyData1,My data 1,"This is the description for my data 1",SpriteNameInProject
SomeData,MyData2,My data 2,"This is the description for my data 2",SpriteNameInProject
```

Importer flow:
- For each row, look up `Class` → find a `Type` with matching `Type.Name` among all scanned assemblies.
- Instantiate via parameterless constructor.
- For each header (other than `Class`) try to match a property (public or non‑public instance) first, then a field.
- Store as an `Unresolved` assignment until successfully deserialized.

Code example:
```csharp
class SomeData : Immutable 
{ 
    public readonly AssetRef<Sprite> Icon = null; // AssetRef is a special handler that will store type and name.
}

// Somewhere in initialization (GameManager excerpt paraphrased)
var files = Directory.GetFiles(Globals.ModelsPath, "*.csv");
var db = ImmutableImporter.Import(files);

// Retrieve specific immutable
var myData = db.Get<SomeData>("MyData1");
Debug.Log(myData.Icon.AssetType.ToString()); // Sprite
Debug.Log(myData.Icon.Name.ToString()); // SpriteNameInProject
AssetDatabase.Find(myData.Icon.Name); // pseudo fetch code
```

### 4.2 Key–Value Pair (Vertical) Format
If the file has exactly two headers: `Key` and `Value` it is treated specially:
- All rows pivoted into a single logical row (one object total).
- First you still need a `Class` key specifying the target immutable type.
- Remaining keys become headers with their `Value` cell content.

```
Key,Value
Class,SomeConfig
Value,1234
SomeOtherValue,"Foo"
```

### 4.3 Compound / Aggregate Headers
Headers of the form `BaseName(SubKey)` are grouped.
```
Class,Name,GroupName(Foo),GroupName(Bar)
CompoundExample,Example1,FooIsOne,BarIsNone
CompoundExample,Example2,FooIsTwo,BarIsThree
```
Essentially what happens is that the field will be aggregated/compounded into one field:
- In the `Example1` line this becomes: `"Foo;FooIsOne,Bar;BarIsNone"`
- In the `Example2` line:  `"Foo;FooIsTwo,Bar;BarIsThree"`

Your target field/property must know how to parse this aggregated string (custom deserializer or manual `PostImport` processing).

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
public class Range // Not recommended to extend Immutable for Ordered classes.
{
    [Order(0)] public readonly int Min;
    [Order(1)] public readonly int Max;
}
```
CSV cell example: `Range:10;25` assigns `Min=10`, `Max=25`.

Name can be omitted if field is specific type.
```csharp
abstract class Condition 
{
    public abstract bool Check(int value);
}

[Ordered]
class Above : Condition 
{
    [Order(0)] public readonly int Value;
    public override bool Check(int value) => value > Value;  
}

[Ordered]
class Below : Condition 
{
    [Order(0)] public readonly int Value;
    public override bool Check(int value) => value < Value;
}

class Foo : Immutable
{
    public readonly Range MyRange; // Will not require type name.
    public readonly ImmutableList<Condition> Conditions; // Will require type name.
}
```
```
Class,Name,MyRange,Conditions
Foo,Foo,0;100,"Above:100,Below:200"
```

### 4.5 Lists, Arrays & Weighted Lists
The importer recognizes certain collection forms:

| Type Pattern | Representation | Notes                                                                                                                                                |
| ------------ | -------------- |------------------------------------------------------------------------------------------------------------------------------------------------------|
| `T[]` (array) | `a,b,c` or bracketed `[a,b,c]` | Brackets optional; internal logic can parse both. Use brackets for nested arrays. `[[1,2,3],[1,2,3],[1,2,3]]` will deserialize into list of 3 lists. |
| `ImmutableList<T>` | `[a,b,c]` or simple comma list | Empty string → `ImmutableList<T>.Empty`.                                                                                                             |
| `WeightedList<T>` | `value:weight,value:weight,...` | Cannot be empty; weight parsed as `double`.                                                                                                          |

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
The importer tries deserializers in the order defined by the given provider. Default is `BasicDeserializerProvider`:
1. `ValueTypesDeserializer` – primitives, nullable primitives, numeric types with units, enums (supports symbolic operators like ">=" → `GreaterOrEqual`).
2. `ImmutableDeserializer` – resolves references to previously imported immutables by `Name` (deferred resolution ensures eventual success if ordering allows).
3. `OrderedTypeDeserializer` – `[Ordered]` classes with `[Order]` members and their positional encoding.
4. `AssetRefDeserializer` – turns a string name into an `AssetRef<T>` wrapper.
5. `TimeSpanDeserializer` – parses composite duration strings like `5h30m` or `250ms` (orderless token accumulation).
6. `TypeRefDeserializer` – wraps a named runtime `Type` in a `TypeRef` value.

If none of the deserializers can deserialize the field an exception is thrown.

## 6. Reference & Resolution Mechanics
Because an immutable may reference another one defined later (or in another CSV):
- Each string field/property is stored as an `Unresolved` record initially.
- Passes repeatedly attempt to `TryDeserialize`. Successful ones are removed.
- Name uniqueness is enforced early (`CheckNameConflicts`) by inspecting the unresolved `Name` field values. If two objects with same type share a `Name`, import aborts.
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

```csharp
bool Validate(Result r)
{
    if (r.ErrorOnFalse(false, "This is an error."))
        return false;
    return true;
}
```

## 8. Example: Useful way to hook into data without hardcoding names

CSV:
```csv
Class,Name,ImportantValue
OtherData,SpecificName1,"This is very important"
OtherData,SpecificName2,"This is also very important"
OtherData,SpecificName3,"I lied, this is not important"
```
```csv
Key,Value
Class,Config
OtherDataReference1,SpecificName1
OtherDataReference2,SpecificName2
OtherDataReference3,SpecificName3
```

```csharp
#nullable enable

class OtherData : Immutable 
{
    public readonly string ImportantValue = "";
}

class Config : Immutable 
{
    public readonly OtherData OtherDataReference1 = null!;
    public readonly OtherData OtherDataReference1 = null!;
    public readonly OtherData OtherDataReference1 = null!;
}
```
```csharp
var files = Directory.GetFiles("SOME_PATH_WITH_CSV", "*.csv");
var db = ImmutableImporter.Import(files);
var config = db.Get<Config>(); // Will get the first instance of Config in any csv.
Console.WriteLine(config.OtherDataReference1.ImportantValue);
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
public class CustomAssemblyProvider : IAssemblyProvider
{
    public static Assembly DirectAssemblyReference;

    public IEnumerable<Assembly> Assemblies => new []
    {
        typeof(Immutable).Assembly, // Required
        typeof(ExtendFromThisType).Assembly, 
        DirectAssemblyReference // etc.
    };
}
ImmutableImporter.AssemblyProvider = new GameAssemblyProvider();
```
Set this before calling `Import`.

## 17. Safety & Immutability Model
- Fields are `readonly` but assigned via reflection (including private backing fields for auto-properties). Mutability after import depends on you not exposing setters or mutable collections. Use `ImmutableList<T>` over `List<T>` to enforce immutability.
- System.Collections.Immutable support is built in.

## 19. Known Limitations / Caveats
- `TypeRefDeserializer` only searches by simple type name in loaded (non-dynamic) assemblies; ambiguous type names can collide silently (first match wins).
- Bracketed block parsing for ordered types does not support nested bracket depth >1. This is by design to keep csv human readable.

## 20. Future Improvement Ideas
- Replace custom recursive assignability checks with standard `IsAssignableFrom` where appropriate.
- Provide built‑in parser for compound aggregate base patterns (e.g., produce dictionary automatically).
- Extend `WeightedList<T>` deserialization to support bracketed groups and inline comments.
- Add CSV schema linting pass to catch unused headers or unmatched class names early.