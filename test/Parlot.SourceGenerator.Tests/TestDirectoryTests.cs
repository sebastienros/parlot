#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class TestDirectoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Delete_That_Fails_Does_Not_Hide_The_Failure_Of_The_Test(bool asynchronous)
    {
        var failure = new InvalidOperationException("The test failed.");
        var cleanup = new IOException("The directory is being used by another process.");

        var reported = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Run(asynchronous, () => throw failure, _ => throw cleanup));

        Assert.Same(failure, reported);
        Assert.Same(cleanup, reported.Data[TestDirectory.CleanupFailure]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_Delete_That_Fails_Fails_A_Test_That_Passed(bool asynchronous)
    {
        var cleanup = new IOException("The directory is being used by another process.");

        var reported = await Assert.ThrowsAsync<IOException>(
            () => Run(asynchronous, static () => { }, _ => throw cleanup));

        Assert.Same(cleanup, reported);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task The_Directory_Is_Deleted_Once_After_The_Test(bool asynchronous, bool fails)
    {
        var failure = new InvalidOperationException("The test failed.");
        var calls = new List<string>();

        var reported = await Record.ExceptionAsync(() => Run(
            asynchronous,
            () =>
            {
                calls.Add("test");
                if (fails)
                {
                    throw failure;
                }
            },
            directory => calls.Add(directory)));

        Assert.Same(fails ? failure : null, reported);
        Assert.Null(failure.Data[TestDirectory.CleanupFailure]);
        Assert.Equal(["test", "directory"], calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task The_Directory_And_Its_Content_Are_Deleted(bool asynchronous, bool fails)
    {
        var directory = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "Parlot.TestDirectory.Tests", Guid.NewGuid().ToString("N"))).FullName;
        void Body()
        {
            File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(directory, "nested")).FullName, "file"), "");
            if (fails)
            {
                throw new InvalidOperationException("The test failed.");
            }
        }

        var reported = await Record.ExceptionAsync(async () =>
        {
            if (asynchronous)
            {
                await TestDirectory.RunAsync(directory, async () =>
                {
                    await Task.Yield();
                    Body();
                });
            }
            else
            {
                TestDirectory.Run(directory, Body);
            }
        });

        Assert.Equal(fails, reported is InvalidOperationException);
        Assert.False(Directory.Exists(directory));
    }

    private static async Task Run(bool asynchronous, Action body, Action<string> delete)
    {
        if (asynchronous)
        {
            await TestDirectory.RunAsync(
                "directory",
                async () =>
                {
                    await Task.Yield();
                    body();
                },
                delete);
        }
        else
        {
            TestDirectory.Run("directory", body, delete);
        }
    }
}
