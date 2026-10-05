using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Parlot.Standalone.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void Rewind_Retains_PreReset_Position_And_Actual_Outcome()
    {
        var events = Collect(() => Assert.True(DiagnosticsGrammars.Choice("ac", out var value) && value == 'c'));
        var rewind = Assert.Single(events, static row => (string)row[0] == "reset" && (int)row[4] == 1);
        Assert.Equal(0, (int)rewind[8]);
        var failed = Assert.Single(events, row => (int)row[1] == (int)rewind[1] && (string)row[0] == "failure");
        Assert.Equal(0, (int)failed[4]);
        Assert.Equal("ac", rewind[6]);
        AssertBalanced(events);
    }

    [Fact]
    public void Exception_Unwinds_All_Entered_Frames()
    {
        var events = Collect(() => Assert.Throws<InvalidOperationException>(() => DiagnosticsGrammars.Throws("a", out _)));
        Assert.Equal("exception", events[events.Count - 1][0]);
        AssertBalanced(events);
    }

    [Fact]
    public void Optional_Failure_Can_Be_Followed_By_Success_Without_Consumption()
    {
        var events = Collect(() => Assert.True(DiagnosticsGrammars.Optional("a", out _)));
        Assert.Contains(events, static row => (string)row[0] == "failure" && (int)row[4] == 0);
        Assert.Contains(events, static row => (string)row[0] == "success" && (int)row[4] == 0);
        AssertBalanced(events);
    }

    [Fact]
    public void Packed_Recognizer_And_Configured_Callbacks_Keep_Return_Semantics()
    {
        AssertBalanced(Collect(() => Assert.True(DiagnosticsGrammars.Packed("seven", out var value) && value == "seven")));
        AssertBalanced(Collect(() => Assert.True(DiagnosticsGrammars.Configured("12", 3, out var value) && value == 36)));
        AssertBalanced(Collect(() => Assert.False(DiagnosticsGrammars.Packed("no", out _))));
    }

    [Fact]
    public void Reader_Captures_Buffer_And_Silent_Parser_Emits_No_Events()
    {
        AssertBalanced(Collect(() => { using var reader = new StringReader("ac"); Assert.True(DiagnosticsGrammars.Choice(reader, out _)); }));
        Assert.Empty(Collect(() => Assert.True(DiagnosticsGrammars.Silent("a", out _))));
        Assert.Equal(2, Collect(() => Assert.True(DiagnosticsGrammars.Choice("ac", out _)), 2).Count);
        Assert.True(DiagnosticsGrammars.Choice("ac", out _));
    }

    private static List<object[]> Collect(Action action, int limit = 1000)
    {
        var events = new List<object[]>();
        var bridge = typeof(DiagnosticsGrammars).Assembly.GetType("Parlot.Generated.ParserDiagnostics", throwOnError: true)!;
        bridge.GetMethod("Begin")!.Invoke(null, new object[] { (Action<object[]>)events.Add, limit });
        try { action(); }
        finally { bridge.GetMethod("End")!.Invoke(null, null); }
        return events;
    }

    private static void AssertBalanced(List<object[]> events)
    {
        Assert.NotEmpty(events);
        var stack = new Stack<int>();
        foreach (var row in events)
        {
            var kind = (string)row[0];
            if (kind == "enter")
            {
                Assert.Equal(stack.Count == 0 ? 0 : stack.Peek(), (int)row[2]);
                stack.Push((int)row[1]);
            }
            else if (kind != "reset") Assert.Equal(stack.Pop(), (int)row[1]);
        }
        Assert.Empty(stack);
    }
}
