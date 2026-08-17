using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Parlot.SourceGenerator;

internal static class IncludeFilesResolver
{
    private static readonly char[] _pathSeparators = ['/', '\\'];
    private static readonly char[] _invalidFileNameCharacters = Path.GetInvalidFileNameChars();

    private const int MaxIncludedFileCount = 256;
    private const long MaxIncludedFileSize = 1024 * 1024;
    private const long MaxIncludedFilesTotalSize = 8 * 1024 * 1024;
    private const int MaxGlobEntries = 10_000;

    [SuppressMessage("Build", "RS1035", Justification = "IncludeFiles explicitly opts source files into the generator's temporary compilation.")]
    public static bool TryLoad(
        SourceProductionContext context,
        IMethodSymbol methodSymbol,
        Location? attributeLocation,
        string[] patterns,
        SyntaxTree originalSyntaxTree,
        string projectDirectory,
        CSharpParseOptions parseOptions,
        IEnumerable<SyntaxTree> existingTrees,
        out List<SyntaxTree> includedTrees)
    {
        includedTrees = new List<SyntaxTree>();
        var diagnosticLocation = attributeLocation ?? methodSymbol.Locations.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(projectDirectory) || !Path.IsPathRooted(projectDirectory))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ParserSourceGenerator.ProjectRootUnavailableDescriptor,
                diagnosticLocation,
                methodSymbol.Name));
            return false;
        }

        if (!TryGetRoots(projectDirectory, originalSyntaxTree.FilePath, out var projectRoot, out var sourceDirectory))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ParserSourceGenerator.InvalidIncludeFileDescriptor,
                diagnosticLocation,
                1,
                methodSymbol.Name,
                "the project or parser source path is invalid"));
            return false;
        }

        if (!IsWithinProjectRoot(projectRoot, sourceDirectory))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ParserSourceGenerator.InvalidIncludeFileDescriptor,
                diagnosticLocation,
                1,
                methodSymbol.Name,
                "the parser source file is outside the project root"));
            return false;
        }

        var comparer = GetPathComparer();
        var existingPaths = new HashSet<string>(
            existingTrees
                .Select(static tree => tree.FilePath)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath),
            comparer);
        var includedPaths = new HashSet<string>(comparer);
        long totalSize = 0;

        for (var entryIndex = 0; entryIndex < patterns.Length; entryIndex++)
        {
            var displayIndex = entryIndex + 1;
            if (!TryNormalizePattern(
                projectRoot,
                sourceDirectory,
                patterns[entryIndex],
                out var normalizedPattern,
                out var rejectionReason))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ParserSourceGenerator.InvalidIncludeFileDescriptor,
                    diagnosticLocation,
                    displayIndex,
                    methodSymbol.Name,
                    rejectionReason));
                return false;
            }

            if (!TryFindFiles(
                context,
                diagnosticLocation,
                methodSymbol.Name,
                displayIndex,
                projectRoot,
                normalizedPattern,
                out var matchedFiles))
            {
                return false;
            }

            if (matchedFiles.Count == 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ParserSourceGenerator.IncludeFileNotFoundDescriptor,
                    diagnosticLocation,
                    displayIndex,
                    methodSymbol.Name));
                return false;
            }

            foreach (var matchedFile in matchedFiles)
            {
                if (!includedPaths.Add(matchedFile) || existingPaths.Contains(matchedFile))
                {
                    continue;
                }

                if (includedPaths.Count > MaxIncludedFileCount)
                {
                    ReportLimit(context, diagnosticLocation, displayIndex, methodSymbol.Name, $"{MaxIncludedFileCount} file");
                    return false;
                }

                try
                {
                    var fileLength = new FileInfo(matchedFile).Length;
                    if (fileLength > MaxIncludedFileSize)
                    {
                        ReportLimit(context, diagnosticLocation, displayIndex, methodSymbol.Name, $"{MaxIncludedFileSize / 1024} KiB per-file");
                        return false;
                    }

                    totalSize += fileLength;
                    if (totalSize > MaxIncludedFilesTotalSize)
                    {
                        ReportLimit(context, diagnosticLocation, displayIndex, methodSymbol.Name, $"{MaxIncludedFilesTotalSize / (1024 * 1024)} MiB total-size");
                        return false;
                    }

                    var sourceText = SourceText.From(File.ReadAllText(matchedFile), Encoding.UTF8);
                    includedTrees.Add(CSharpSyntaxTree.ParseText(sourceText, parseOptions, matchedFile));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    ReportReadFailure(context, diagnosticLocation, displayIndex, methodSymbol.Name, ex);
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryGetRoots(
        string projectDirectory,
        string sourcePath,
        out string projectRoot,
        out string sourceDirectory)
    {
        projectRoot = "";
        sourceDirectory = "";

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return false;
        }

        try
        {
            projectRoot = TrimEndingDirectorySeparators(Path.GetFullPath(projectDirectory));
            sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? "";
            return sourceDirectory.Length > 0;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryNormalizePattern(
        string projectRoot,
        string sourceDirectory,
        string pattern,
        out string normalizedPattern,
        out string rejectionReason)
    {
        normalizedPattern = "";
        rejectionReason = "";

        if (string.IsNullOrWhiteSpace(pattern))
        {
            rejectionReason = "the path is empty";
            return false;
        }

        if (Path.IsPathRooted(pattern) || pattern.IndexOf(':') >= 0)
        {
            rejectionReason = "absolute paths are not allowed";
            return false;
        }

        if (pattern.IndexOf('\0') >= 0)
        {
            rejectionReason = "the path contains an invalid character";
            return false;
        }

        var rootWithSeparator = EnsureEndingDirectorySeparator(projectRoot);
        var relativeSourceDirectory = sourceDirectory.Length == projectRoot.Length
            ? ""
            : sourceDirectory.Substring(rootWithSeparator.Length);
        var segments = relativeSourceDirectory
            .Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        foreach (var segment in pattern.Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    rejectionReason = "the path escapes the project root";
                    return false;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            if (!HasOnlyValidPatternCharacters(segment))
            {
                rejectionReason = "the path contains an invalid character";
                return false;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            rejectionReason = "the path does not identify a C# source file";
            return false;
        }

        normalizedPattern = string.Join("/", segments);
        if (!normalizedPattern.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            rejectionReason = "only patterns ending in .cs are allowed";
            return false;
        }

        return true;
    }

    private static bool HasOnlyValidPatternCharacters(string segment)
    {
        return segment.All(c => Array.IndexOf(_invalidFileNameCharacters, c) < 0 || c is '*' or '?');
    }

    [SuppressMessage("Build", "RS1035", Justification = "IncludeFiles explicitly opts source files into the generator's temporary compilation.")]
    private static bool TryFindFiles(
        SourceProductionContext context,
        Location? diagnosticLocation,
        string methodName,
        int entryIndex,
        string projectRoot,
        string normalizedPattern,
        out List<string> matchedFiles)
    {
        matchedFiles = new List<string>();

        if (!ContainsWildcard(normalizedPattern))
        {
            var fullPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                normalizedPattern.Replace('/', Path.DirectorySeparatorChar)));

            if (!File.Exists(fullPath))
            {
                return true;
            }

            if (ContainsReparsePoint(projectRoot, fullPath))
            {
                ReportSymlink(context, diagnosticLocation, entryIndex, methodName);
                return false;
            }

            matchedFiles.Add(fullPath);
            return true;
        }

        var patternSegments = normalizedPattern.Split('/');
        var literalSegmentCount = Array.FindIndex(patternSegments, ContainsWildcard);
        var searchRoot = projectRoot;
        for (var i = 0; i < literalSegmentCount; i++)
        {
            searchRoot = Path.Combine(searchRoot, patternSegments[i]);
        }

        if (!Directory.Exists(searchRoot))
        {
            return true;
        }

        if (ContainsReparsePoint(projectRoot, searchRoot))
        {
            ReportSymlink(context, diagnosticLocation, entryIndex, methodName);
            return false;
        }

        var matcher = new Regex(GlobToRegex(normalizedPattern), GetRegexOptions());
        var recursive = normalizedPattern.IndexOf("**", StringComparison.Ordinal) >= 0;
        var maximumDirectoryDepth = patternSegments.Length - 1;
        var directories = new Stack<(string Path, int Depth)>();
        directories.Push((searchRoot, literalSegmentCount));
        var visitedEntries = 0;

        try
        {
            while (directories.Count > 0)
            {
                var (directory, depth) = directories.Pop();

                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    if (++visitedEntries > MaxGlobEntries)
                    {
                        ReportLimit(context, diagnosticLocation, entryIndex, methodName, $"{MaxGlobEntries} traversed-entry");
                        return false;
                    }

                    var relativePath = file.Substring(EnsureEndingDirectorySeparator(projectRoot).Length)
                        .Replace(Path.DirectorySeparatorChar, '/')
                        .Replace(Path.AltDirectorySeparatorChar, '/');
                    if (!matcher.IsMatch(relativePath))
                    {
                        continue;
                    }

                    if (ContainsReparsePoint(projectRoot, file))
                    {
                        ReportSymlink(context, diagnosticLocation, entryIndex, methodName);
                        return false;
                    }

                    matchedFiles.Add(Path.GetFullPath(file));
                }

                if (!recursive && depth >= maximumDirectoryDepth)
                {
                    continue;
                }

                foreach (var childDirectory in Directory.EnumerateDirectories(directory))
                {
                    if (++visitedEntries > MaxGlobEntries)
                    {
                        ReportLimit(context, diagnosticLocation, entryIndex, methodName, $"{MaxGlobEntries} traversed-entry");
                        return false;
                    }

                    if ((File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) != 0)
                    {
                        ReportSymlink(context, diagnosticLocation, entryIndex, methodName);
                        return false;
                    }

                    directories.Push((childDirectory, depth + 1));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            ReportReadFailure(context, diagnosticLocation, entryIndex, methodName, ex);
            return false;
        }

        matchedFiles.Sort(GetPathComparer());
        return true;
    }

    private static bool ContainsWildcard(string value)
        => value.IndexOf('*') >= 0 || value.IndexOf('?') >= 0;

    private static string GlobToRegex(string pattern)
    {
        var builder = new StringBuilder("^");

        for (var i = 0; i < pattern.Length; i++)
        {
            var current = pattern[i];
            if (current == '*')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                {
                    i++;
                    if (i + 1 < pattern.Length && pattern[i + 1] == '/')
                    {
                        i++;
                        builder.Append("(?:.*/)?");
                    }
                    else
                    {
                        builder.Append(".*");
                    }
                }
                else
                {
                    builder.Append("[^/]*");
                }
            }
            else if (current == '?')
            {
                builder.Append("[^/]");
            }
            else
            {
                builder.Append(Regex.Escape(current.ToString()));
            }
        }

        return builder.Append('$').ToString();
    }

    [SuppressMessage("Build", "RS1035", Justification = "IncludeFiles path validation must reject symbolic-link traversal.")]
    private static bool ContainsReparsePoint(string projectRoot, string fullPath)
    {
        var rootWithSeparator = EnsureEndingDirectorySeparator(projectRoot);
        if (fullPath.Equals(projectRoot, GetPathComparison()))
        {
            return false;
        }

        if (!fullPath.StartsWith(rootWithSeparator, GetPathComparison()))
        {
            return true;
        }

        var current = projectRoot;
        var relativePath = fullPath.Substring(rootWithSeparator.Length);
        foreach (var segment in relativePath.Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithinProjectRoot(string projectRoot, string fullPath)
    {
        var normalizedPath = TrimEndingDirectorySeparators(Path.GetFullPath(fullPath));
        return normalizedPath.Equals(projectRoot, GetPathComparison())
            || normalizedPath.StartsWith(EnsureEndingDirectorySeparator(projectRoot), GetPathComparison());
    }

    private static void ReportSymlink(SourceProductionContext context, Location? location, int index, string methodName)
        => context.ReportDiagnostic(Diagnostic.Create(
            ParserSourceGenerator.IncludeFileSymlinkDescriptor,
            location,
            index,
            methodName));

    private static void ReportLimit(SourceProductionContext context, Location? location, int index, string methodName, string limit)
        => context.ReportDiagnostic(Diagnostic.Create(
            ParserSourceGenerator.IncludeFileLimitDescriptor,
            location,
            index,
            methodName,
            limit));

    private static void ReportReadFailure(SourceProductionContext context, Location? location, int index, string methodName, Exception exception)
        => context.ReportDiagnostic(Diagnostic.Create(
            ParserSourceGenerator.IncludeFileReadDescriptor,
            location,
            index,
            methodName,
            exception.GetType().Name));

    private static RegexOptions GetRegexOptions()
        => Path.DirectorySeparatorChar == '\\'
            ? RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
            : RegexOptions.CultureInvariant;

    private static string EnsureEndingDirectorySeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static string TrimEndingDirectorySeparators(string path)
    {
        var pathRoot = Path.GetPathRoot(path);
        return string.Equals(path, pathRoot, GetPathComparison())
            ? path
            : path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static StringComparison GetPathComparison()
        => Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer GetPathComparer()
        => Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
