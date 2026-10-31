"""Replaces the Parlot NuGet references of the cloned libraries with references to $(ParlotRoot)."""

import pathlib
import sys

repos = pathlib.Path(sys.argv[1])


def patch(path, replacements, append=None):
    text = path.read_text(encoding="utf-8-sig")

    for old, new in replacements:
        if new in text:
            continue
        if old not in text:
            sys.exit(f"{path}: can't find {old!r}")
        text = text.replace(old, new)

    if append and append not in text:
        text = text.replace("</Project>", append + "\n</Project>")

    path.write_text(text, encoding="utf-8")
    print(f"patched {path}")


# Fluid uses the Parlot runtime. The package's buildTransitive props, which a project reference doesn't import,
# add the static using of Parlot.Fluent.Parsers that Fluid relies on.
patch(
    repos / "fluid/Fluid/Fluid.csproj",
    [('<PackageReference Include="Parlot" />', '<ProjectReference Include="$(ParlotRoot)/src/Parlot/Parlot.csproj" />')],
    append='  <Import Project="$(ParlotRoot)/src/Parlot/buildTransitive/Parlot.props" />',
)

# NCalc generates its parser with Parlot.SourceGenerator: reference the generator project the way
# test/Parlot.Benchmarks does, with the build-time Parlot assembly the grammar factories execute against
patch(
    repos / "ncalc/src/NCalc.Parser/NCalc.Parser.csproj",
    [(
        '<PackageReference Include="Parlot.SourceGenerator" PrivateAssets="all" />',
        '<ProjectReference Include="$(ParlotRoot)/src/Parlot.SourceGenerator/Parlot.SourceGenerator.csproj" '
        'OutputItemType="Analyzer" ReferenceOutputAssembly="false" '
        'GlobalPropertiesToRemove="ArtifactsPath;OutDir;OutputPath;PublishDir" />\n'
        '      <Analyzer Include="$(ParlotRoot)/src/Parlot/bin/$(Configuration)/netstandard2.0/Parlot.dll" />',
    )],
    append=(
        '  <Import Project="$(ParlotRoot)/src/Parlot.SourceGenerator/build/Parlot.SourceGenerator.props" />\n'
        '  <Import Project="$(ParlotRoot)/src/Parlot.SourceGenerator/build/Parlot.SourceGenerator.targets" />'
    ),
)
