"""Smoke-test the actual native window and its parent lifetime pipe (requires a desktop)."""
import http.server
import pathlib
import subprocess
import sys
import threading

root = pathlib.Path(__file__).resolve().parent
rid = sys.argv[1]
executable = root / 'dist' / rid / ('Parlot Explorer.app/Contents/MacOS/parlot-explorer-desktop'
    if rid.startswith('osx-') else 'parlot-explorer-desktop.exe' if rid.startswith('win-') else 'parlot-explorer-desktop')

loaded = threading.Event()

class Page(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200)
        self.send_header('Content-Type', 'text/html')
        self.end_headers()
        self.wfile.write(b'<!doctype html><title>Parlot Explorer smoke test</title><p>Native window test</p>')
        loaded.set()

    def log_message(self, *args):
        pass

with http.server.ThreadingHTTPServer(('127.0.0.1', 0), Page) as server:
    threading.Thread(target=server.serve_forever, daemon=True).start()
    url = f'http://127.0.0.1:{server.server_port}/#' + 'a' * 64
    process = subprocess.Popen([str(executable), url], stdin=subprocess.PIPE)
    try:
        assert loaded.wait(timeout=30), 'Native webview did not request the local page'
        assert process.poll() is None, f'Native shell exited early: {process.returncode}'
        process.stdin.close()
        assert process.wait(timeout=10) == 0, 'Native shell did not exit cleanly after parent pipe closed'
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()
        server.shutdown()
print('Native shell started and exited cleanly when its parent pipe closed.')
