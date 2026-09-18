"""Run isolated comparisons serially on one runner; never execute PR-supplied shell commands."""
import argparse
import json
import os
from pathlib import Path
import subprocess

README_FILTERS = ['*ExprBench.Parlot*', '*JsonBench.*Parlot*', '*RegexBenchmarks.Parlot*']
QUOTED_FILTERS = ['*SepQuotedStringBenchmarks*']
SPACE_FILTERS = ['*SkipWhiteSpaceBenchmarks.SkipWhiteSpace_Default*', '*SkipWhiteSpaceBenchmarks.SkipWhiteSpaceOrNewLine_Default*']
VARIANTS = {
    'main': (README_FILTERS + QUOTED_FILTERS + SPACE_FILTERS + ['*SepScanBenchmarks*'], 64),
    'combined': (README_FILTERS, 18),
    'quoted-simd': (README_FILTERS + QUOTED_FILTERS, 30),
    'whitespace': (README_FILTERS + SPACE_FILTERS, 28),
}


def command(args, cwd, log, env=None):
    with log.open('a', encoding='utf-8') as stream:
        stream.write('\n> ' + repr(args) + '\n')
        stream.flush()
        process = subprocess.Popen(args, cwd=cwd, env=env, stdout=subprocess.PIPE,
                                   stderr=subprocess.STDOUT, text=True, encoding='utf-8', errors='replace')
        for line in process.stdout:
            stream.write(line)
            stream.flush()
            print(line, end='', flush=True)
        if process.wait():
            raise RuntimeError(f'Command failed; see {log}: {args}')


def collect(folder):
    rows = {}
    host = None
    for path in sorted((folder / 'results').glob('*full-compressed.json')):
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        host = data['HostEnvironmentInfo']
        for row in data['Benchmarks']:
            if not row.get('Statistics'):
                raise RuntimeError('Missing measurements: ' + row['FullName'])
            rows[row['FullName']] = row
    return host, rows


def make_reports(output):
    _, baseline = collect(output / 'main')
    lines = ['# Windows x64 / .NET 11 comparison', '',
             'One runner, serial variants, one launch, three warmups, five 100 ms measurements. '
             'Negative time deltas are faster. Error is the 99.9% confidence-interval half-width. '
             'Treat small/overlapping differences as inconclusive on this hosted VM.', '']
    for variant in ['combined', 'quoted-simd', 'whitespace']:
        _, rows = collect(output / variant)
        if not rows:
            lines += [f'## {variant}', '', 'No completed results.', '']
            continue
        lines += [f'## {variant}', '', '| Case | Main ns ± error | Candidate ns ± error | Delta | Allocated B (main → candidate) |',
                  '|---|---:|---:|---:|---:|']
        for key, row in rows.items():
            a = baseline[key]
            x, y = a['Statistics'], row['Statistics']
            lines.append(f"| {row['Type']}.{row['Method']} {row['Parameters']} | {x['Mean']:.2f} ± {x['ConfidenceInterval']['Margin']:.2f} | "
                         f"{y['Mean']:.2f} ± {y['ConfidenceInterval']['Margin']:.2f} | {(y['Mean']/x['Mean']-1)*100:+.1f}% | "
                         f"{a['Memory']['BytesAllocatedPerOperation']} → {row['Memory']['BytesAllocatedPerOperation']} |")
        lines += ['']
    lines += ['## Scan kernels against SearchValues (same main build)', '',
              '| Length / Unicode | Kernel | SearchValues ns | Kernel ns | Delta |', '|---|---|---:|---:|---:|']
    scans = [b for b in baseline.values() if b['Type'] == 'SepScanBenchmarks']
    for row in scans:
        if row['Method'] == 'SearchValues':
            continue
        a = next(b for b in scans if b['Method'] == 'SearchValues' and b['Parameters'] == row['Parameters'])
        x, y = a['Statistics']['Mean'], row['Statistics']['Mean']
        lines.append(f"| {row['Parameters']} | {row['Method']} | {x:.2f} | {y:.2f} | {(y/x-1)*100:+.1f}% |")
    report = '\n'.join(lines) + '\n'
    (output / 'comparison.md').write_text(report, encoding='utf-8')
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as stream:
            stream.write(report)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--report-only', action='store_true')
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    if args.report_only:
        make_reports(output)
        return
    repo = Path(__file__).resolve().parents[2]
    revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=repo, text=True).strip()
    worktrees = Path(os.environ.get('RUNNER_TEMP', str(output.parent))) / ('parlot-variants-' + revision[:12])
    worktrees.mkdir(parents=True, exist_ok=True)
    env = dict(os.environ, ParlotBenchmarkTargetFramework='net11.0')
    manifest = {'mainRevision': '2961dca01eed79242d0cba36e8d67cf8976b7651', 'harnessRevision': revision,
                'sdk': '11.0.100-rc.1.26425.128', 'variants': {}}
    for variant, (filters, expected) in VARIANTS.items():
        tree = worktrees / variant
        destination = output / variant
        destination.mkdir(exist_ok=True)
        log = output / (variant + '.log')
        command(['git', 'worktree', 'add', '--detach', str(tree), revision], repo, log)
        if variant != 'main':
            patch = repo / 'scripts/windows-benchmarks' / (variant + '.patch')
            command(['git', 'apply', '--check', str(patch)], tree, log)
            command(['git', 'apply', str(patch)], tree, log)
        # Pin the SDK in each checkout, even if the runner image contains a newer preview.
        global_file = tree / 'global.json'
        global_settings = json.loads(global_file.read_text(encoding='utf-8-sig'))
        global_settings['sdk'].update(version=manifest['sdk'], rollForward='disable', allowPrerelease=True)
        global_file.write_text(json.dumps(global_settings, indent=2), encoding='utf-8')
        command(['dotnet', '--info'], tree, log, env)
        # Complete all builds and correctness checks for this variant before timing it.
        command(['dotnet', 'build', '-c', 'Release', '--disable-build-servers', '-m:1'], tree, log, env)
        for project in ['Parlot.Tests', 'Parlot.Standalone.Tests']:
            command(['dotnet', 'test', '--project', str(tree / 'test' / project / (project + '.csproj')),
                     '-c', 'Release', '-f', 'net10.0', '--no-build'], tree, log, env)
        command(['dotnet', 'run', '--project', str(tree / 'test/Parlot.Benchmarks/Parlot.Benchmarks.csproj'),
                 '-c', 'Release', '-f', 'net11.0', '--no-build', '--', '--filter', *filters,
                 '--launchCount', '1', '--warmupCount', '3', '--iterationCount', '5', '--iterationTime', '100',
                 '--exporters', 'json', '--artifacts', str(destination)], tree, log, env)
        host, rows = collect(destination)
        if len(rows) != expected:
            raise RuntimeError(f'{variant}: expected {expected} cases, got {len(rows)}')
        manifest['variants'][variant] = {'filters': filters, 'cases': len(rows), 'host': host}
        (output / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    make_reports(output)


if __name__ == '__main__':
    main()
