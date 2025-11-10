# Covariance Support Example

This example demonstrates how `IParser<out T>` enables covariance, eliminating the need for wasteful `.Then<TBase>(x => x)` conversions.

## Before (Without Covariance)

```csharp
class Animal { public string Name { get; set; } }
class Dog : Animal { public string Breed { get; set; } }
class Cat : Animal { public string Color { get; set; } }

var dogParser = Terms.Text("dog").Then(_ => new Dog { Name = "Buddy", Breed = "Golden Retriever" });
var catParser = Terms.Text("cat").Then(_ => new Cat { Name = "Whiskers", Color = "Orange" });

// Had to use .Then<Animal>(x => x) to convert each parser - creates wrapper objects
var animalParser = dogParser.Then<Animal>(x => x).Or(catParser.Then<Animal>(x => x));
```

## After (With Covariance)

```csharp
class Animal { public string Name { get; set; } }
class Dog : Animal { public string Breed { get; set; } }
class Cat : Animal { public string Color { get; set; } }

var dogParser = Terms.Text("dog").Then(_ => new Dog { Name = "Buddy", Breed = "Golden Retriever" });
var catParser = Terms.Text("cat").Then(_ => new Cat { Name = "Whiskers", Color = "Orange" });

// Can use OneOf directly with IParser<T> - no wrapper objects needed!
var animalParser = OneOf<Animal>(dogParser, catParser);
```

## Benefits

1. **No wrapper objects**: The original parser instances are reused without creating `Then` wrappers
2. **Cleaner syntax**: More readable code without explicit type conversions
3. **Better performance**: Eliminates the overhead of wrapper parser objects
4. **Type safety**: Still maintains full type safety through the covariant interface

## How It Works

- `IParser<out T>` is a covariant interface (note the `out` keyword)
- This means `IParser<Dog>` can be used where `IParser<Animal>` is expected
- The `OneOf<T>` method now accepts `params IParser<T>[]` instead of just `Parser<T>[]`
- Under the hood, parsers are adapted as needed while preserving the original parser behavior
