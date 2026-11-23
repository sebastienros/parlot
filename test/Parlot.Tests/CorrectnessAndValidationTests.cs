#nullable enable

using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class CorrectnessAndValidationTests
{
    [Fact]
    public void Warning_Wrapping_Deferred_Locates_Warning_After_WhiteSpace()
    {
        var deferred = Deferred<TextSpan>();
        var warning = deferred.Warning("deprecated");

        // Assign parser AFTER Warning is constructed
        deferred.Parser = Terms.Identifier();

        var context = new ParseContext(new Scanner("   foo")) { CollectDiagnostics = true };
        Assert.True(warning.TryParse(context, out var value, out _));
        Assert.Equal("foo", value.ToString());

        var diagnostic = Assert.Single(context.Diagnostics);
        Assert.Equal("deprecated", diagnostic.Message);
        Assert.Equal(ParseDiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(3, diagnostic.Position.Offset);
        Assert.True(((ISeekable)warning).SkipWhitespace);
    }

    [Fact]
    public void Separated_NonSeekable_In_OneOf_Still_Matches_Empty_And_Leading_Separator()
    {
        var optionalList = Separated(Terms.Char(','), Terms.Text("foo"), min: 0);
        Assert.False(((ISeekable)optionalList).CanSeek);

        // If ExpectedPrefixes leaked ["foo"] when !CanSeek, OneOf prefix dispatch would skip optionalList on "bar"
        var choiceOptional = OneOf(
            optionalList.AndSkip(Terms.Text("bar")).Then(static list => list.Count),
            Terms.Text("baz").Then(static _ => -1));
        Assert.Equal(0, choiceOptional.Parse("bar"));
        Assert.Equal(2, choiceOptional.Parse("foo,foo bar"));

        var leadingSeparator = Separated(Terms.Char(','), Terms.Text("foo"), allowLeadingSeparator: true);
        Assert.False(((ISeekable)leadingSeparator).CanSeek);

        var choiceLeading = OneOf(
            leadingSeparator.Then(static list => list.Count),
            Terms.Text("baz").Then(static _ => -1));
        Assert.Equal(2, choiceLeading.Parse(",foo,foo"));
    }

    [Fact]
    public void Cursor_NegativeOffset_Throws_ArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cursor("abc", new TextPosition(-1, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cursor("abc", new TextPosition(4, 1, 5)));
    }

    [Fact]
    public void Null_Arguments_Are_Validated()
    {
        var a = Literals.Char('a');

        Assert.Throws<ArgumentNullException>(() => new Capture<char>(null!));
        Assert.Throws<ArgumentNullException>(() => new TextBefore<char>(null!));

        // Sequence arities 3..7
        Assert.Throws<ArgumentNullException>(() => new Sequence<char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new Sequence<char, char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new Sequence<char, char, char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new Sequence<char, char, char, char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new Sequence<char, char, char, char, char, char, char>(null!, a));

        // SequenceAndSkip arities 3..8
        Assert.Throws<ArgumentNullException>(() => new SequenceAndSkip<char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new SequenceAndSkip<char, char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new SequenceAndSkip<char, char, char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new SequenceAndSkip<char, char, char, char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new SequenceAndSkip<char, char, char, char, char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new SequenceAndSkip<char, char, char, char, char, char, char, char>(null!, a));

        // SequenceSkipAnd arities 3..8
        Assert.Throws<ArgumentNullException>(() => new SequenceSkipAnd<char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new SequenceSkipAnd<char, char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new SequenceSkipAnd<char, char, char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new SequenceSkipAnd<char, char, char, char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new SequenceSkipAnd<char, char, char, char, char, char, char>(null!, a));
        Assert.Throws<ArgumentNullException>(() => new SequenceSkipAnd<char, char, char, char, char, char, char, char>(null!, a));

        // Unary & UnaryWithContext null elements
        Assert.Throws<ArgumentException>(() => new Unary<char, char>(a, [(null!, static x => x)]));
        Assert.Throws<ArgumentException>(() => new Unary<char, char>(a, [(a, null!)]));
        Assert.Throws<ArgumentException>(() => new UnaryWithContext<char, char>(a, [(null!, static (_, x) => x)]));
        Assert.Throws<ArgumentException>(() => new UnaryWithContext<char, char>(a, [(a, null!)]));

        // RightAssociative & RightAssociativeWithContext null elements
        Assert.Throws<ArgumentException>(() => new RightAssociative<char, char>(a, [(null!, static (x, _) => x)]));
        Assert.Throws<ArgumentException>(() => new RightAssociative<char, char>(a, [(a, null!)]));
        Assert.Throws<ArgumentException>(() => new RightAssociativeWithContext<char, char>(a, [(null!, static (_, x, _) => x)]));
        Assert.Throws<ArgumentException>(() => new RightAssociativeWithContext<char, char>(a, [(a, null!)]));

        // Lookup null element
        Assert.Throws<ArgumentException>(() => a.Lookup((ISeekable)null!));
        Assert.Throws<ArgumentException>(() => a.Lookup((ISeekable)a, null!));

        // Parse / TryParse / IsMatch null context
        Assert.Throws<ArgumentNullException>(() => a.Parse((ParseContext)null!));
        Assert.Throws<ArgumentNullException>(() => a.TryParse((ParseContext)null!, out _, out _));
        Assert.Throws<ArgumentNullException>(() => a.IsMatch((ParseContext)null!));
    }
}
