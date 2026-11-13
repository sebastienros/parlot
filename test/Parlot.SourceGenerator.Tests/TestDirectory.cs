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
    /// <summary>
    /// The key in <see cref="Exception.Data"/> of the exception a test threw, for the exception the delete threw after it.
    /// </summary>
    internal const string CleanupFailure = "Parlot.TestDirectory.CleanupFailure";

    public static void Run(string directory, Action body)
        => Run(directory, body, Delete);

    public static Task RunAsync(string directory, Func<Task> body)
        => RunAsync(directory, body, Delete);

    internal static void Run(string directory, Action body, Action<string> delete)
    {
        try
        {
            body();
        }
        catch (Exception failure)
        {
            DeleteAfterFailure(directory, delete, failure);
            throw;
        }

        delete(directory);
    }

    internal static async Task RunAsync(string directory, Func<Task> body, Action<string> delete)
    {
        try
        {
            await body();
        }
        catch (Exception failure)
        {
            DeleteAfterFailure(directory, delete, failure);
            throw;
        }

        delete(directory);
    }

    private static void Delete(string directory)
        => Directory.Delete(directory, recursive: true);

    private static void DeleteAfterFailure(string directory, Action<string> delete, Exception failure)
    {
        try
        {
            delete(directory);
        }
        catch (Exception exception)
        {
            // The test failed already: its exception is the one to report, this one only says what was left behind
            failure.Data[CleanupFailure] = exception;
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"The test failed, and '{directory}' could not be deleted after it: {exception}");
        }
    }
}
