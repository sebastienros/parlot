"""End-to-end host tests. Build the tool and sample first; run from the repository root."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
DOTNET = shutil.which('dotnet')
TOOL = Path(os.environ.get('PARLOT_EXPLORER_TOOL', str(ROOT / 'tools/Parlot.Explorer/bin/Debug/net10.0/Parlot.Explorer.dll')))
SAMPLE = ROOT / 'tools/Parlot.Explorer.Sample/bin/Debug/net10.0/Parlot.Explorer.Sample.dll'
command = [DOTNET, str(TOOL)] if TOOL.suffix == '.dll' else [str(TOOL)]
process = subprocess.Popen([*command, '--no-browser'], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, cwd=ROOT, env={**os.environ, 'Logging__LogLevel__Default': 'Warning'})
try:
    for line in process.stdout:
        if line.startswith('Parlot Explorer: '):
            url = urllib.parse.urlsplit(line.split(': ', 1)[1].strip())
            break
    else:
        raise AssertionError('Server failed to start')
    base = f'{url.scheme}://{url.netloc}'
    token = url.fragment
    def request(route, payload=None, headers=None):
        data = json.dumps(payload).encode() if payload is not None else None
        req = urllib.request.Request(base + '/api/' + route, data=data,
            headers={'Content-Type': 'application/json', 'X-Parlot-Token': token, **(headers or {})})
        with urllib.request.urlopen(req, timeout=30) as response:
            return json.load(response)
    def rejected(route, payload, headers, status):
        try:
            request(route, payload, headers)
        except urllib.error.HTTPError as error:
            assert error.code == status, (error.code, error.read())
            return
        raise AssertionError('Expected rejection')
    rejected('session', None, {'X-Parlot-Token': ''}, 401)
    rejected('session', None, {'Origin': 'https://unrelated.example'}, 403)
    rejected('session', None, {'Host': 'unrelated.example'}, 403)
    catalog = request('catalog', {'path': str(SAMPLE)})
    assert {'Choice', 'Assignment', 'Optional', 'Recursive', 'Throws', 'Json'} <= {entry['name'].rsplit('.', 1)[-1] for entry in catalog}, catalog
    def capture(name, source, path=SAMPLE):
        entries = catalog if path == SAMPLE else request('catalog', {'path': str(path)})
        parser = next(entry for entry in entries if entry['name'].endswith('.' + name))
        return request('capture', {'path': str(path), 'parser': parser['id'], 'input': source})
    choice = capture('Choice', 'ac')
    assert choice['success'] and choice['value'] == 'c', choice
    assert 0 <= choice['parseElapsedMs'] <= choice['elapsedMs'], choice
    assert any(event['kind'] == 'reset' and event['offset'] == 1 and event['target'] == 0 for event in choice['events'])
    assert not capture('Choice', 'ax')['success']
    assert capture('Optional', 'a')['success']
    assert capture('Recursive', '((a))')['success']
    throwing = capture('Throws', 'a')
    assert 'Sample callback exception' in throwing['error']
    assert throwing['events'][-1]['kind'] == 'exception'
    assert 0 <= throwing['parseElapsedMs'] <= throwing['elapsedMs'], throwing
    assignment = capture('Assignment', 'answer = 42;')
    assert assignment['success'] and assignment['value']['Value'] == 42, assignment
    assert assignment['value']['Greeting'] == 'Bonjour', assignment
    assert not list(SAMPLE.parent.glob('Parlot.dll')), 'Sample must be standalone'
    print('PASS private discovery, backtracking, optional, recursion, exception, dependency + French satellite, object result, authentication')

    with tempfile.TemporaryDirectory(prefix='parlot-explorer-test-') as temp:
        folder = Path(temp)
        generator = ROOT / 'src/Parlot.SourceGenerator/bin/Debug/netstandard2.0/Parlot.SourceGenerator.dll'
        runtime = ROOT / 'src/Parlot/bin/Debug/netstandard2.0/Parlot.dll'
        (folder / 'Reload.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>
<TargetFramework>net10.0</TargetFramework><LangVersion>12</LangVersion><Nullable>enable</Nullable>
</PropertyGroup><ItemGroup><Compile Remove="*.parlot.cs"/><AdditionalFiles Include="*.parlot.cs"/>
<Analyzer Include="{generator}"/><Analyzer Include="{runtime}"/></ItemGroup></Project>''')
        (folder / 'Grammar.cs').write_text('''public static partial class Grammar {
private static partial bool Parse(string text, out char value);
public static char Block(char value) { System.Threading.Thread.Sleep(20000); return value; }
}''')
        grammar = folder / 'Grammar.parlot.cs'
        def build(character, block=False):
            grammar.write_text('''using Parlot.Fluent; using Parlot.SourceGenerator; using static Parlot.Fluent.Parsers;
public static partial class Grammar {
[GenerateParser(nameof(Parse), Diagnostics = true)]
public static Parser<char> Build() => Literals.Char('%s')%s;
}''' % (character, '.Then(static value => Block(value))' if block else ''))
            subprocess.run([DOTNET, 'build', str(folder / 'Reload.csproj'), '--disable-build-servers', '-v:q'], cwd=ROOT, check=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        binary = folder / 'bin/Debug/net10.0/Reload.dll'
        build('a')
        first = capture('Parse', 'a', binary)
        rev1 = request('revision', {'path': str(binary)})
        build('b')
        second = capture('Parse', 'b', binary)
        assert second['success'] and second['value'] == 'b'
        assert first['assemblyVersion'] != second['assemblyVersion']
        assert request('revision', {'path': str(binary)}) != rev1
        assert not capture('Parse', 'a', binary)['success']
        print('PASS recompilation with unchanged assembly identity loads new code and changes revision')
        build('b', block=True)
        started = time.monotonic()
        try:
            capture('Parse', 'b', binary)
        except urllib.error.HTTPError as error:
            assert error.code == 400 and 'exceeded 10 seconds' in error.read().decode()
        else:
            raise AssertionError('Worker was not stopped')
        assert time.monotonic() - started < 15
        assert capture('Choice', 'ac')['success'], 'Host must remain usable after timeout'
        print('PASS hard timeout and host recovery')

        # A protocol fixture controls event counts and window changes exactly, independent of graph optimizations.
        grammar.unlink()
        (folder / 'Grammar.cs').write_text("""using System;
namespace Parlot.Generated {
[AttributeUsage(AttributeTargets.Method)] public sealed class ParserDiagnosticsAttribute : Attribute {
    public ParserDiagnosticsAttribute(int version) { }
}
public static class ParserDiagnostics {
    public static Action<object[]>? Sink;
    public static int Limit;
    public static void Begin(Action<object[]> sink, int limit) { Sink = sink; Limit = limit; }
    public static void End() { Sink = null; }
}
}
public sealed class SlowResult {
    public int Value { get { System.Threading.Thread.Sleep(200); return 42; } }
}
public static class Grammar {
    [Parlot.Generated.ParserDiagnostics(1)]
    private static bool Inspect(string text, out SlowResult value) { value = new SlowResult(); return true; }
    [Parlot.Generated.ParserDiagnostics(1)]
    private static bool Parse(string text, int count, bool overflow, out int value) {
        var first = overflow ? new string('a', 1000001) : text;
        var second = overflow ? new string('b', 1000001) : text;
        for (var i = 0; i < Math.Min(count, Parlot.Generated.ParserDiagnostics.Limit); i++) {
            var buffer = i == 1 ? second : first;
            Parlot.Generated.ParserDiagnostics.Sink!(new object[] {
                i % 2 == 0 ? "enter" : "success", i / 2 + 1, 0, "fixture", 0, 0, buffer, false, -1 });
        }
        value = count;
        return true;
    }
}""")
        subprocess.run([DOTNET, 'build', str(folder / 'Reload.csproj'), '--disable-build-servers', '-v:q'], cwd=ROOT, check=True)
        entry = next(item for item in request('catalog', {'path': str(binary)}) if item['name'].endswith('.Parse'))
        def boundary(count, overflow=False):
            return request('capture', {'path': str(binary), 'parser': entry['id'], 'input': 'a',
                'configuration': {'count': count, 'overflow': overflow}})
        exact = boundary(20000)
        assert len(exact['events']) == 20000 and not exact['truncated']
        extra = boundary(20001)
        assert len(extra['events']) == 20000 and extra['truncated'] and extra['value'] == 20001
        windows = boundary(4, True)
        assert len(windows['events']) == 1 and windows['truncated'], 'No events may resume after a missing buffer window'
        print('PASS exact event cap, overflow, and contiguous prefix after buffer limit')
        inspected = capture('Inspect', '', binary)
        assert inspected['success'] and inspected['value']['Value'] == 42, inspected
        assert inspected['elapsedMs'] - inspected['parseElapsedMs'] >= 150, inspected
        print('PASS parse timing excludes slow result inspection and is recorded for exceptions')
    print('All explorer integration checks passed.')
except urllib.error.HTTPError as error:
    print(error.read().decode(), flush=True)
    raise
finally:
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()
