#nullable enable
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class CovarianceTests
{
    // Test classes for demonstrating covariance
    class Animal { public string Name { get; set; } = ""; }
    class Dog : Animal { public string Breed { get; set; } = ""; }
    class Cat : Animal { public string Color { get; set; } = ""; }

    [Fact]
    public void IParserShouldSupportCovariance()
    {
        // Create parsers that return specific types
        var dogParser = Terms.Text("dog").Then(_ => new Dog { Name = "Buddy", Breed = "Golden Retriever" });
        var catParser = Terms.Text("cat").Then(_ => new Cat { Name = "Whiskers", Color = "Orange" });

        // Before: Would need .Then<Animal>(x => x) to convert each parser
        // After: Can use IParser<out T> directly with covariance

        // This should work due to covariance - Dog and Cat can be used where Animal is expected
        IParser<Animal> animalDogParser = dogParser;
        IParser<Animal> animalCatParser = catParser;

        // Verify the parsers work
        var context1 = new ParseContext(new Scanner("dog"));
        var success1 = animalDogParser.Parse(context1, out int start1, out int end1, out object? value1);
        Assert.True(success1);
        Assert.IsType<Dog>(value1);
        Assert.Equal("Buddy", ((Dog)value1!).Name);

        var context2 = new ParseContext(new Scanner("cat"));
        var success2 = animalCatParser.Parse(context2, out int start2, out int end2, out object? value2);
        Assert.True(success2);
        Assert.IsType<Cat>(value2);
        Assert.Equal("Whiskers", ((Cat)value2!).Name);
    }

    [Fact]
    public void OneOfShouldAcceptCovariantParsers()
    {
        // Create parsers for derived types
        var dogParser = Terms.Text("dog").Then(_ => new Dog { Name = "Buddy", Breed = "Golden Retriever" });
        var catParser = Terms.Text("cat").Then(_ => new Cat { Name = "Whiskers", Color = "Orange" });

        // Before: Would need dogParser.Then<Animal>(x => x).Or(catParser.Then<Animal>(x => x))
        // After: Can use OneOf with IParser<T> directly
        var animalParser = OneOf<Animal>(dogParser, catParser);

        var result1 = animalParser.Parse("dog");
        Assert.NotNull(result1);
        Assert.IsType<Dog>(result1);
        Assert.Equal("Buddy", result1.Name);
        Assert.Equal("Golden Retriever", ((Dog)result1).Breed);

        var result2 = animalParser.Parse("cat");
        Assert.NotNull(result2);
        Assert.IsType<Cat>(result2);
        Assert.Equal("Whiskers", result2.Name);
        Assert.Equal("Orange", ((Cat)result2).Color);
    }

    [Fact]
    public void OrShouldAcceptCovariantParsers()
    {
        // Create parsers for derived types
        var dogParser = Terms.Text("dog").Then(_ => new Dog { Name = "Buddy", Breed = "Golden Retriever" });
        var catParser = Terms.Text("cat").Then(_ => new Cat { Name = "Whiskers", Color = "Orange" });

        // Use Or with covariant types
        var animalParser = dogParser.Or<Dog, Cat, Animal>(catParser);

        var result1 = animalParser.Parse("dog");
        Assert.NotNull(result1);
        Assert.IsType<Dog>(result1);

        var result2 = animalParser.Parse("cat");
        Assert.NotNull(result2);
        Assert.IsType<Cat>(result2);
    }

    [Fact]
    public void CovariantParsersShouldWorkWithCompilation()
    {
        // Test that covariance works with compiled parsers
        var dogParser = Terms.Text("dog").Then(_ => new Dog { Name = "Buddy", Breed = "Golden Retriever" });
        var catParser = Terms.Text("cat").Then(_ => new Cat { Name = "Whiskers", Color = "Orange" });

        var animalParser = OneOf<Animal>(dogParser, catParser).Compile();

        var result1 = animalParser.Parse("dog");
        Assert.NotNull(result1);
        Assert.IsType<Dog>(result1);
        Assert.Equal("Buddy", result1.Name);

        var result2 = animalParser.Parse("cat");
        Assert.NotNull(result2);
        Assert.IsType<Cat>(result2);
        Assert.Equal("Whiskers", result2.Name);
    }

    [Fact]
    public void NullableCovariance()
    {
        // Test covariance with nullable reference types
        var stringParser = Terms.Text("hello").Then(_ => "world");
        
        // string is derived from object, should work with covariance
        IParser<object> objectParser = stringParser;
        
        var context = new ParseContext(new Scanner("hello"));
        var success = objectParser.Parse(context, out int start, out int end, out object? value);
        
        Assert.True(success);
        Assert.Equal("world", value);
    }
}
