using System;
using System.IO;
using System.Text;

namespace Parlot.SourceGenerator;

/// <summary>
/// How generated callbacks are mapped back to their <c>.parlot.cs</c> source, from the <c>ParlotLineDirectives</c>
/// MSBuild property.
/// </summary>
internal sealed class LineDirectives
{
    public static readonly LineDirectives Absolute = new(LineDirectivesMode.Absolute, "");

    private readonly LineDirectivesMode _mode;
    private readonly string _projectDirectory;

    private LineDirectives(LineDirectivesMode mode, string projectDirectory)
    {
        _mode = mode;
        _projectDirectory = projectDirectory;
    }

    /// <summary>
    /// Reads the property, <c>absolute</c> (the default), <c>relative</c> or <c>none</c>, ignoring case.
    /// Returns <c>null</c> for another value.
    /// </summary>
    public static LineDirectives? Parse(string? value, string projectDirectory)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or "absolute":
                return Absolute;
            case "relative":
                return new(LineDirectivesMode.Relative, projectDirectory);
            case "none":
                return new(LineDirectivesMode.None, projectDirectory);
            default:
                return null;
        }
    }

    /// <summary>
    /// Gets the path the directives of a callback from this file name, or <c>null</c> when they are not emitted.
    /// </summary>
    public string? GetPath(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath) || _mode == LineDirectivesMode.None)
        {
            return null;
        }

        return _mode == LineDirectivesMode.Relative ? GetRelativePath(_projectDirectory, filePath!) : filePath;
    }

    /// <summary>
    /// Gets the path of a file relative to the project directory, with '/' separators, so that it is the same in every
    /// checkout. A path on another root, or without a project directory, is reduced to its file name.
    /// </summary>
    internal static string GetRelativePath(string directory, string filePath)
    {
        if (string.IsNullOrEmpty(directory) || !Path.IsPathRooted(directory) || !Path.IsPathRooted(filePath))
        {
            return Path.GetFileName(filePath);
        }

        var from = Path.GetFullPath(directory).Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var to = Path.GetFullPath(filePath).Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        var common = 0;
        while (common < from.Length && common < to.Length - 1 && string.Equals(from[common], to[common], comparison))
        {
            common++;
        }

        // Paths on different drives share nothing
        if (common == 0 && Path.DirectorySeparatorChar == '\\')
        {
            return Path.GetFileName(filePath);
        }

        var builder = new StringBuilder();
        for (var i = common; i < from.Length; i++)
        {
            builder.Append("../");
        }

        for (var i = common; i < to.Length; i++)
        {
            builder.Append(to[i]);
            if (i < to.Length - 1)
            {
                builder.Append('/');
            }
        }

        return builder.ToString();
    }

    private enum LineDirectivesMode
    {
        Absolute,
        Relative,
        None,
    }
}
