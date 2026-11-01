using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Parlot.SourceGenerator;

/// <summary>
/// Where the build lets <see cref="IncludeGeneratorsResolver"/> look for generator assemblies.
/// </summary>
internal sealed record IncludedGeneratorSearch(string? Analyzers, string? PackageRoot);

/// <summary>
/// Finds the assemblies of the generators named by <c>[IncludeGenerators]</c>.
/// </summary>
/// <remarks>
/// The analyzer references of the build come first: <c>build/Parlot.SourceGenerator.targets</c> passes the
/// <c>Analyzer</c> items MSBuild hands the compiler, so the version the project references is found wherever its
/// packages are restored. Analyzer references are not visible to a generator otherwise. Without them (another build
/// host, or a name the project doesn't reference) the generator is searched in the NuGet package folders: the
/// project's <c>NuGetPackageRoot</c>, then <c>NUGET_PACKAGES</c>, then <c>~/.nuget/packages</c>.
/// </remarks>
internal static class IncludeGeneratorsResolver
{
    /// <summary>The project's analyzer references, separated by '|' since editorconfig values end at ';'.</summary>
    public const string AnalyzersProperty = "build_property._ParlotAnalyzers";

    public const string PackageRootProperty = "build_property.NuGetPackageRoot";

    private static readonly char[] _pathSeparators = ['/', '\\'];

    /// <summary>
    /// Returns the assemblies of <paramref name="name"/>, an assembly name or a package id, or an empty list.
    /// </summary>
    [SuppressMessage("Build", "RS1035", Justification = "Generator assemblies are found on disk, where NuGet restored them.")]
    public static List<string> Find(string name, string? analyzers, string? packageRoot)
    {
        var references = (analyzers ?? "").Split(['|'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => path.Trim())
            .Where(static path => path.Length > 0)
            .ToArray();

        // An assembly name: PolySharp.SourceGenerators
        var found = references
            .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // A package id: <root>/polysharp/<version>/analyzers/dotnet/cs/PolySharp.SourceGenerators.dll
        if (found.Count == 0)
        {
            found = references.Where(path => IsInPackage(path, name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (found.Count > 0)
        {
            return found;
        }

        var roots = new List<string>();
        AddRoot(roots, packageRoot);
        AddRoot(roots, Environment.GetEnvironmentVariable("NUGET_PACKAGES"));
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            AddRoot(roots, Path.Combine(userProfile, ".nuget", "packages"));
        }

        foreach (var root in roots)
        {
            var packageDir = Path.Combine(root, name.ToLowerInvariant());
            if (!Directory.Exists(packageDir))
            {
                continue;
            }

            foreach (var versionDir in Directory.GetDirectories(packageDir).OrderByDescending(PackageVersion).ThenByDescending(static dir => dir, StringComparer.Ordinal))
            {
                var analyzerDir = Path.Combine(versionDir, "analyzers", "dotnet", "cs");
                if (!Directory.Exists(analyzerDir))
                {
                    continue;
                }

                var dllPath = Path.Combine(analyzerDir, name + ".dll");
                if (File.Exists(dllPath))
                {
                    return [dllPath];
                }

                var dlls = Directory.GetFiles(analyzerDir, "*.dll");
                if (dlls.Length > 0)
                {
                    Array.Sort(dlls, StringComparer.Ordinal);
                    return [.. dlls];
                }
            }
        }

        return [];
    }

    private static bool IsInPackage(string path, string packageId)
    {
        // <packageId>/<version>/analyzers/...
        var segments = path.Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i + 2 < segments.Length; i++)
        {
            if (string.Equals(segments[i], packageId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(segments[i + 2], "analyzers", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddRoot(List<string> roots, string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        root = root!.Trim().TrimEnd(_pathSeparators);
        if (root.Length > 0 && !roots.Contains(root, StringComparer.OrdinalIgnoreCase))
        {
            roots.Add(root);
        }
    }

    // 1.15.0 is newer than 1.9.0, which an ordinal sort gets wrong
    private static Version PackageVersion(string versionDir)
    {
        var name = Path.GetFileName(versionDir);
        var release = name.IndexOf('-');
        return Version.TryParse(release < 0 ? name : name.Substring(0, release), out var version) ? version : new Version(0, 0);
    }
}
