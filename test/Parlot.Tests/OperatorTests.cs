using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class OperatorTests
{
    [Fact]
    public void TestPipeOperator()
    {
        var parser = Terms.Char('a') | Terms.Char('b');
        Assert.Equal('a', parser.Parse("a"));
        Assert.Equal('b', parser.Parse("b"));
    }

    [Fact]
    public void TestPlusOperator()
    {
        var parser = (Terms.Char('a') >> "a") + (+Terms.Char('b') >> "bbb");
        Assert.Equal("a", parser.Parse("a b").Item1);
        Assert.Equal("bbb", parser.Parse("abbb").Item2);
    }
}