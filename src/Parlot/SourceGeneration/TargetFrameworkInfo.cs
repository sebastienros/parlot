using System;

namespace Parlot.SourceGeneration;

public enum TargetFrameworkIdentifier
{
    Unknown = 0,
    NetFramework,
    NetStandard,
    NetCoreApp,
}

public sealed class TargetFrameworkInfo
{
    public static readonly TargetFrameworkInfo Unknown = new(TargetFrameworkIdentifier.Unknown, new Version(0, 0));

    public TargetFrameworkInfo(TargetFrameworkIdentifier identifier, Version version)
    {
        Identifier = identifier;
        Version = version ?? throw new ArgumentNullException(nameof(version));
    }

    public TargetFrameworkIdentifier Identifier { get; }

    public Version Version { get; }

    public static TargetFrameworkInfo FromMsBuildProperties(string? targetFrameworkIdentifier, string? targetFrameworkVersion)
    {
        var identifier = targetFrameworkIdentifier switch
        {
            ".NETFramework" => TargetFrameworkIdentifier.NetFramework,
            ".NETStandard" => TargetFrameworkIdentifier.NetStandard,
            ".NETCoreApp" => TargetFrameworkIdentifier.NetCoreApp,
            _ => TargetFrameworkIdentifier.Unknown,
        };

        // TargetFrameworkVersion is typically "v4.7.2", "v2.0", "v10.0", etc.
        var versionText = targetFrameworkVersion ?? "";
        if (versionText.Length > 0 && (versionText[0] == 'v' || versionText[0] == 'V'))
        {
            versionText = versionText.Substring(1);
        }

        if (!Version.TryParse(versionText, out var version))
        {
            version = new Version(0, 0);
        }

        return new TargetFrameworkInfo(identifier, version);
    }

    public override string ToString()
    {
        return Identifier == TargetFrameworkIdentifier.Unknown
            ? "<unknown>"
            : $"{Identifier} {Version}";
    }
}
