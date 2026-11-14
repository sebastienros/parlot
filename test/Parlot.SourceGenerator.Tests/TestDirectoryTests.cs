#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

// The bodies either throw before they return a task or return one which fails: both are a test which failed.
// A directory which doesn't exist is one the delete fails on, on every platform.
public class TestDirectoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Delete_That_Fails_Does_Not_Hide_The_Failure_Of_The_Test(bool yields)
    {
        var directory = Missing();
        var failure = new InvalidOperationException("The test failed.");

        var reported = await Record.ExceptionAsync(() => TestDirectory.RunAsync(directory, Body(yields, () => throw failure)));

        Assert.Same(failure, reported);
        // What the delete threw is in the output of the test, which is reported with a test that failed
        var output = TestContext.Current.TestOutputHelper!.Output;
        Assert.Contains(directory, output);
        Assert.Contains(nameof(DirectoryNotFoundException), output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Delete_That_Fails_Fails_A_Test_That_Passed(bool yields)
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => TestDirectory.RunAsync(Missing(), Body(yields, static () => { })));

        Assert.Empty(TestContext.Current.TestOutputHelper!.Output);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task The_Directory_And_Its_Content_Are_Deleted_After_The_Test(bool yields, bool fails)
    {
        var directory = Directory.CreateDirectory(Missing()).FullName;
        var failure = new InvalidOperationException("The test failed.");

        var reported = await Record.ExceptionAsync(() => TestDirectory.RunAsync(directory, Body(yields, () =>
        {
            // Throws when the directory was deleted before the test ended
            File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(directory, "nested")).FullName, "file"), "");
            Assert.True(Directory.Exists(directory));
            if (fails)
            {
                throw failure;
            }
        })));

        Assert.Same(fails ? failure : null, reported);
        Assert.False(Directory.Exists(directory));
        Assert.Empty(TestContext.Current.TestOutputHelper!.Output);
    }

    private static string Missing()
        => Path.Combine(Path.GetTempPath(), "Parlot.TestDirectory.Tests", Guid.NewGuid().ToString("N"));

    private static Func<Task> Body(bool yields, Action test)
        => yields
            ? async () =>
            {
                await Task.Yield();
                test();
            }
            : () =>
            {
                test();
                return Task.CompletedTask;
            };
}
