#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// Runs a test in a directory and deletes the directory after it, without letting the delete hide why the test failed.
/// </summary>
/// <remarks>
/// A <c>finally</c> block that deletes the directory replaces the exception of the test with the one of the delete,
/// which is all that gets reported: on Windows a command which timed out and was killed can still hold the directory.
/// After a test which passed nothing is caught: a directory that can't be deleted then means that something outlived the
/// commands the test ran, and that fails the test.
/// </remarks>
internal static class TestDirectory
{
    // The only form takes a Func<Task>: an async lambda given to an Action would be async void, and the directory deleted
    // while the test still runs. A test which awaits nothing returns Task.CompletedTask.
    public static async Task RunAsync(string directory, Func<Task> body)
    {
        try
        {
            await body();
        }
        catch
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception exception)
            {
                // The test failed already: its exception is the one to report, this one only says what was left behind
                TestContext.Current.TestOutputHelper?.WriteLine(
                    $"The test failed, and '{directory}' could not be deleted after it: {exception}");
            }

            throw;
        }

        Directory.Delete(directory, recursive: true);
    }
}
