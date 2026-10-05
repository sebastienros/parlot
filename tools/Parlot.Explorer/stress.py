"""Capture reproducible long traces. Build the explorer and sample, then run from the repository root."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = Path(os.environ.get('PARLOT_STRESS_OUTPUT', str(ROOT / 'artifacts/explorer-stress')))
OUTPUT.mkdir(parents=True, exist_ok=True)
SAMPLE = ROOT / 'tools/Parlot.Explorer.Sample/bin/Debug/net10.0/Parlot.Explorer.Sample.dll'
TOOL = ROOT / 'tools/Parlot.Explorer/bin/Debug/net10.0/Parlot.Explorer.dll'


def node_count(value):
    if isinstance(value, dict):
        return 1 + sum(node_count(child) for child in value.values())
    if isinstance(value, list):
        return 1 + sum(node_count(child) for child in value)
    return 1


def document(rows):
    return json.dumps({'version': 1, 'rows': [
        {'id': i, 'name': f'row {i}: \\"quoted\\"', 'enabled': i % 2 == 0, 'missing': None,
         'values': [i, -i, 1.25e-5], 'metadata': {'tags': ['parser', 'trace'], 'active': True}}
        for i in range(rows)]}, separators=(',', ':'))


cases = []
for rows in [1, 10, 50, 200, 700]:
    source = document(rows)
    cases.append((f'rows-{rows}', source, True))
# Failure near EOF after substantial work; late marker makes the tail easy to find in the UI.
source = document(50)
cases.append(('late-failure', source[:-1] + ',"late marker":}', False))
source = document(700)
cases.append(('late-failure-capped', source[:-1] + ',"late marker":}', False))
cases.append(('nested-80', '[' * 80 + '{"leaf":[1,"end",null]}' + ']' * 80, True))
cases.append(('exact-input-limit', '"' + 'x' * 127998 + '"', True))

process = subprocess.Popen([shutil.which('dotnet'), str(TOOL), '--no-browser'], cwd=ROOT,
    stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
try:
    for line in process.stdout:
        if line.startswith('Parlot Explorer: '):
            url = urllib.parse.urlsplit(line.split(': ', 1)[1].strip())
            break
    else:
        raise AssertionError('Server failed to start')
    base = f'{url.scheme}://{url.netloc}'
    def request(route, payload):
        data = json.dumps(payload).encode()
        req = urllib.request.Request(base + '/api/' + route, data=data,
            headers={'Content-Type': 'application/json', 'X-Parlot-Token': url.fragment})
        with urllib.request.urlopen(req, timeout=30) as response:
            raw = response.read()
            return json.loads(raw), len(raw)
    catalog, _ = request('catalog', {'path': str(SAMPLE)})
    parser = next(item['id'] for item in catalog if item['name'].endswith('.Json'))
    results = []
    for name, source, expected in cases:
        (OUTPUT / f'{name}.input.json').write_text(source)
        started = time.perf_counter()
        capture, size = request('capture', {'path': str(SAMPLE), 'parser': parser, 'input': source})
        duration = (time.perf_counter() - started) * 1000
        assert capture['success'] == expected and capture['error'] is None, (name, capture['error'])
        if expected:
            assert capture['value']['NodeCount'] == node_count(json.loads(source)), (name, capture['value'], node_count(json.loads(source)))
        assert len(capture['events']) <= 20000
        stack, peak = [], 0
        for event in capture['events']:
            if event['kind'] == 'enter':
                assert event['parent'] == (stack[-1] if stack else 0), name
                stack.append(event['id'])
                peak = max(peak, len(stack))
            elif event['kind'] != 'reset':
                assert stack.pop() == event['id'], name
        if not capture['truncated']:
            assert not stack, name
            assert capture['events'][-1]['kind'] == ('success' if expected else 'failure')
        capture['input'] = source
        (OUTPUT / f'{name}.capture.json').write_text(json.dumps(capture, separators=(',', ':')))
        result = {'case': name, 'inputUnits': len(source.encode('utf-16-le')) // 2, 'events': len(capture['events']),
                  'calls': sum(event['kind'] == 'enter' for event in capture['events']), 'depth': peak,
                  'truncated': capture['truncated'], 'success': capture['success'], 'captureMs': round(capture['elapsedMs'], 2),
                  'requestMs': round(duration, 2), 'responseBytes': size, 'buffers': len(capture['buffers']), 'openFrames': len(stack)}
        results.append(result)
        print(json.dumps(result), flush=True)
    try:
        request('capture', {'path': str(SAMPLE), 'parser': parser, 'input': 'x' * 128001})
    except urllib.error.HTTPError as error:
        assert error.code == 400 and '128,000' in error.read().decode()
    else:
        raise AssertionError('Oversized input should be rejected')
    (OUTPUT / 'captures.json').write_text(json.dumps(results, indent=2))
    print('PASS grammar results, balanced traces, truncated prefixes, and input size boundary', flush=True)
finally:
    process.terminate()
    try:
        process.wait(timeout=5)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait()
