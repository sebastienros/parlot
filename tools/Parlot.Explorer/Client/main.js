import { h, render } from 'preact';
import { useEffect, useMemo, useRef, useState } from 'preact/hooks';
import * as monaco from 'monaco-editor/editor/editor.api.js';
import EditorWorker from 'monaco-editor/editor/editor.worker.js?worker';
import { indexTrace, visibleFrames, ruleMatcher, callers, rowWindow, rowHeight, headerHeight } from './replay';
import './style.css';
self.MonacoEnvironment = { getWorker: () => new EditorWorker() };
const token = location.hash.slice(1) || sessionStorage.getItem('parlot-token');
sessionStorage.setItem('parlot-token', token || '');
history.replaceState(null, '', location.pathname);
async function api(route, body, signal) {
  const response = await fetch('/api/' + route, { method: body ? 'POST' : 'GET', signal,
    headers: { 'X-Parlot-Token': token || '', ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined });
  if (response.status === 401) throw new Error('Session expired. Open the URL printed by parlot-explorer.');
  const data = await response.json();
  if (!response.ok) throw new Error(data.error || `Request failed (${response.status})`);
  return data;
}
const button = (label, onClick, props = {}) => h('button', { onClick, ...props }, label);
const badge = (text, className = '') => h('span', { class: 'badge ' + className }, text);
function ObjectTree({ value, name = 'result', depth = 0 }) {
  if (value !== null && typeof value === 'object') return h('details', { open: depth < 2 },
    h('summary', {}, h('b', {}, name), badge(Array.isArray(value) ? `${value.length} items` : `${Object.keys(value).length} properties`)),
    h('div', { class: 'object-children' }, Object.entries(value).map(([key, child]) => h(ObjectTree, { key, name: key, value: child, depth: depth + 1 }))));
  return h('div', { class: 'object-leaf' }, h('b', {}, name + ': '), h('code', {}, JSON.stringify(value)));
}
function App() {
  const [path, setPath] = useState(''), [loadedPath, setLoadedPath] = useState('');
  const [catalog, setCatalog] = useState([]), [parser, setParser] = useState('');
  const [input, setInput] = useState('ac'), [configuration, setConfiguration] = useState('{}');
  const [capture, setCapture] = useState(null), [step, setStep] = useState(0), [selected, setSelected] = useState(0);
  const [error, setError] = useState(''), [busy, setBusy] = useState(false), [live, setLive] = useState(true), [watch, setWatch] = useState(true);
  const [playing, setPlaying] = useState(false), [mode, setMode] = useState('Execution'), [resultMode, setResultMode] = useState('Tree');
  const [collapsed, setCollapsed] = useState(new Set()), [filter, setFilter] = useState(''), [exclude, setExclude] = useState(''), [browse, setBrowse] = useState(null);
  const [directory, setDirectory] = useState(''), [revision, setRevision] = useState('');
  const editorNode = useRef(), editor = useRef(), decorations = useRef(), requestId = useRef(0), controller = useRef();
  const [scrollTop, setScrollTop] = useState(0);
  const table = useRef();
  const events = useMemo(() => capture?.events || [], [capture]), event = events[step];
  const trace = useMemo(() => indexTrace(events, capture?.truncated), [events, capture?.truncated]);
  const frames = useMemo(() => trace.at(step), [trace, step]);
  const rows = useMemo(() => visibleFrames(frames, collapsed, filter, exclude), [frames, collapsed, filter, exclude]);
  const focus = frames.get(selected) || frames.get(event?.id);
  const displayRows = mode === 'Callers' ? callers(frames, focus?.id).filter(ruleMatcher(filter, exclude)).map((frame, depth) => ({ ...frame, depth })) : rows;
  const window = rowWindow(displayRows.length, scrollTop);
  useEffect(() => {
    const position = displayRows.findIndex(frame => frame.id === event?.id);
    if (position < 0 || !table.current) return;
    const top = position * rowHeight, bottom = top + rowHeight;
    if (top < table.current.scrollTop) table.current.scrollTop = top;
    else if (bottom > table.current.scrollTop + table.current.clientHeight - headerHeight)
      table.current.scrollTop = bottom - table.current.clientHeight + headerHeight;
    setScrollTop(table.current.scrollTop);
  }, [step, events, mode, filter, exclude, collapsed]);
  const stale = capture && (capture.input !== input || capture.parser !== parser || capture.path !== loadedPath || capture.configuration !== configuration);
  useEffect(() => {
    editor.current = monaco.editor.create(editorNode.current, { value: input, language: 'plaintext', theme: 'vs-dark', automaticLayout: true,
      minimap: { enabled: false }, fontSize: 15, fontFamily: 'SFMono-Regular, Consolas, monospace', padding: { top: 18 }, scrollBeyondLastLine: false,
      wordWrap: 'on', lineNumbersMinChars: 3, ariaLabel: 'Parser source input' });
    decorations.current = editor.current.createDecorationsCollection();
    const subscription = editor.current.onDidChangeModelContent(() => setInput(editor.current.getValue()));
    api('session').then(session => { setDirectory(session.directory); if (session.initialPath) { setPath(session.initialPath); load(session.initialPath); } }).catch(e => setError(e.message));
    return () => { subscription.dispose(); editor.current.dispose(); controller.current?.abort(); };
  }, []);
  useEffect(() => {
    if (!editor.current) return;
    if (!event || stale) { decorations.current.clear(); return; }
    const position = editor.current.getModel().getPositionAt(event.kind === 'reset' ? event.target : event.offset);
    decorations.current.set([{ range: new monaco.Range(position.lineNumber, position.column, position.lineNumber, position.column + 1), options: { inlineClassName: 'cursor-highlight', beforeContentClassName: 'cursor-mark' } }]);
    editor.current.revealPositionInCenterIfOutsideViewport(position);
  }, [event, stale]);
  async function load(newPath = path) {
    const id = ++requestId.current;
    controller.current?.abort(); controller.current = new AbortController();
    setBusy(true); setError(''); setPlaying(false);
    try {
      const list = await api('catalog', { path: newPath }, controller.current.signal);
      const nextRevision = await api('revision', { path: newPath }, controller.current.signal);
      if (id !== requestId.current) return;
      setCatalog(list); setLoadedPath(newPath); setPath(newPath); setRevision(nextRevision.revision);
      setParser(previous => list.some(item => item.id === previous) ? previous : list[0]?.id || '');
      if (!list.length) setError('No diagnostic parsers found. Rebuild with [GenerateParser(..., Diagnostics = true)].');
    } catch (e) { if (id === requestId.current && e.name !== 'AbortError') setError(e.message); }
    finally { if (id === requestId.current) setBusy(false); }
  }
  async function run() {
    if (!loadedPath || !parser) return;
    const id = ++requestId.current;
    controller.current?.abort(); controller.current = new AbortController();
    setBusy(true); setError(''); setPlaying(false);
    try {
      const data = await api('capture', { path: loadedPath, parser, input, configuration: JSON.parse(configuration) }, controller.current.signal);
      if (id !== requestId.current) return;
      setCapture({ ...data, input, path: loadedPath, parser, configuration });
      setStep(Math.max(0, data.events.length - 1)); setSelected(0); setCollapsed(new Set());
    } catch (e) { if (id === requestId.current && e.name !== 'AbortError') setError(e.message); }
    finally { if (id === requestId.current) setBusy(false); }
  }
  useEffect(() => {
    // Invalidate in-flight work as soon as input or parser changes, before the debounce expires.
    ++requestId.current; controller.current?.abort(); setBusy(false); setPlaying(false);
    if (!live) return;
    const timer = setTimeout(run, 500); return () => clearTimeout(timer);
  }, [input, parser, loadedPath, configuration, live, revision]);
  useEffect(() => {
    if (!watch || !loadedPath || busy) return;
    const timer = setInterval(async () => {
      try { const next = await api('revision', { path: loadedPath }); if (next.revision !== revision) await load(loadedPath); }
      catch (e) { setError('Waiting for a complete build: ' + e.message); }
    }, 2000);
    return () => clearInterval(timer);
  }, [watch, loadedPath, revision, busy]);
  useEffect(() => {
    if (!playing) return;
    const timer = setInterval(() => setStep(current => { if (current >= events.length - 1) { setPlaying(false); return current; } return current + 1; }), 180);
    return () => clearInterval(timer);
  }, [playing, events]);
  function seek(value) { setPlaying(false); setSelected(0); setStep(Math.min(events.length - 1, Math.max(0, value))); }
  async function files(location = directory) { try { setBrowse(await api('files?path=' + encodeURIComponent(location))); } catch (e) { setError(e.message); } }
  function exportCapture() {
    const url = URL.createObjectURL(new Blob([JSON.stringify(capture, null, 2)], { type: 'application/json' }));
    const link = document.createElement('a'); link.href = url; link.download = 'parlot-capture.json'; link.click(); URL.revokeObjectURL(url);
  }
  const selectedParser = catalog.find(item => item.id === parser);
  const buffer = event && capture.buffers[event.buffer];
  const relative = buffer ? Math.max(0, Math.min(buffer.text.length, (event.kind === 'reset' ? event.target : event.offset) - buffer.start)) : 0;
  return h('div', { class: 'shell' },
    h('header', {}, h('div', { class: 'brand' }, h('span', { class: 'logo' }, 'P'), h('div', {}, h('h1', {}, 'Parlot ', h('span', {}, 'Explorer')), h('p', {}, 'Understand every parse.'))),
      h('div', { class: 'header-meta' }, badge('LOCAL SESSION', 'local'), badge('Generated diagnostics'))),
    h('section', { class: 'assembly panel' }, h('div', { class: 'eyebrow' }, '01 / PARSER ASSEMBLY'),
      h('div', { class: 'assembly-row' }, h('input', { 'aria-label': 'Assembly path', placeholder: '/path/to/Your.Parsers.dll', value: path, onInput: e => setPath(e.target.value) }),
        button('Browse…', () => files()), button('Load assembly', () => load(), { disabled: !path || busy }),
        h('label', { class: 'check' }, h('input', { type: 'checkbox', checked: watch, onChange: e => setWatch(e.target.checked) }), 'Watch builds')),
      h('div', { class: 'parser-row' }, h('label', {}, 'Entry point'), h('select', { 'aria-label': 'Parser entry point', value: parser, onChange: e => setParser(e.target.value) },
        !catalog.length && h('option', { value: '' }, 'Load an assembly to discover parsers'), catalog.map(item => h('option', { value: item.id }, item.name))),
        selectedParser && badge(selectedParser.inputKind + ' → ' + selectedParser.resultType)),
      h('p', { class: 'hint' }, 'Assemblies and inputs stay on this computer. Load trusted code; parsers run with your local permissions.')),
    error && h('div', { class: 'notice error', role: 'alert' }, error),
    h('main', {},
      h('div', { class: 'left-column' }, h('section', { class: 'panel input-panel' },
        h('div', { class: 'panel-heading' }, h('div', {}, h('span', { class: 'eyebrow' }, '02 / SOURCE INPUT'), h('h2', {}, 'Try your grammar')),
          h('label', { class: 'check' }, h('input', { type: 'checkbox', checked: live, onChange: e => setLive(e.target.checked) }), 'Live')),
        h('div', { class: 'editor', ref: editorNode }),
        h('div', { class: 'input-footer' }, h('span', {}, `${input.length} UTF-16 units`), button(busy ? 'Capturing…' : 'Run parser ↗', run, { class: 'primary', disabled: busy || !parser })),
        !!selectedParser?.configuration.length && h('details', { class: 'config', open: true }, h('summary', {}, 'Configuration'), h('p', {}, selectedParser.configuration.join(' · ')),
          h('textarea', { 'aria-label': 'Configuration JSON', value: configuration, onInput: e => setConfiguration(e.target.value), spellcheck: false }))),
      h('section', { class: 'panel result-panel' }, h('div', { class: 'panel-heading' }, h('h2', {}, 'Result'), h('div', { class: 'tabs' }, ['Tree', 'JSON'].map(value => button(value, () => setResultMode(value), { class: resultMode === value ? 'active' : '' })))),
        capture ? h('div', { class: 'result-body' }, h('div', { class: 'result-status' }, badge(capture.error ? 'Exception' : capture.success ? 'Matched' : 'No match', capture.error ? 'exception' : capture.success ? 'success' : 'failure'), h('span', {}, `${capture.elapsedMs.toFixed(2)} ms · diagnostic capture`)),
          capture.error ? h('pre', { class: 'exception-text' }, capture.error) : resultMode === 'JSON' ? h('pre', {}, JSON.stringify(capture.value, null, 2)) : h(ObjectTree, { value: capture.value }))
          : h('p', { class: 'empty' }, 'The returned object will appear here.'))),
      h('section', { class: 'panel trace-panel' }, h('div', { class: 'panel-heading' }, h('div', {}, h('span', { class: 'eyebrow' }, '03 / TRACE REPLAY'), h('h2', {}, 'Follow the parser')),
        h('div', { class: 'header-meta' }, stale && badge('Input changed', 'failure'), capture && button('Export JSON', exportCapture))),
        h('div', { class: 'toolbar' }, button('⏮', () => seek(0), { title: 'First event', disabled: !events.length }), button('←', () => seek(step - 1), { title: 'Previous event', disabled: !events.length || step === 0 }),
          button(playing ? 'Pause' : '▶ Play', () => { if (step >= events.length - 1) setStep(0); setSelected(0); setPlaying(!playing); }, { disabled: !events.length }),
          button('→', () => seek(step + 1), { title: 'Next event', disabled: !events.length || step >= events.length - 1 }), button('⏭', () => seek(events.length - 1), { title: 'Last event', disabled: !events.length }),
          h('input', { type: 'range', 'aria-label': 'Replay event', min: 0, max: Math.max(0, events.length - 1), value: step, onInput: e => seek(+e.target.value) }), h('code', {}, events.length ? `${step + 1} / ${events.length}` : '0 / 0')),
        capture?.truncated && h('div', { class: 'notice' }, 'Showing the first 20,000 events (or the buffer limit). Parsing continued; open calls are incomplete in this recording.'),
        h('div', { class: 'trace-options' }, h('div', { class: 'tabs' }, ['Execution', 'Input', 'Callers'].map(value => button(value, () => setMode(value), { class: mode === value ? 'active' : '' }))),
          (filter || exclude) && button('Clear filters', () => { setFilter(''); setExclude(''); })),
        h('div', { class: 'rule-filters' },
          h('label', {}, 'Include names', h('input', { 'aria-label': 'Filter rules', placeholder: 'All rule names', value: filter, onInput: e => setFilter(e.target.value) })),
          h('label', {}, 'Exclude patterns', h('input', { 'aria-label': 'Exclude rules', placeholder: 'SkipWS*, Then_*, Sequence_*', value: exclude, onInput: e => setExclude(e.target.value) }))),
        h('p', { class: 'filter-help hint' }, 'Exclusions: text contains, or whole-name patterns with * (any text) and ? (one character). Separate with commas; case-insensitive.'),
        h('p', { class: 'trace-count hint' }, `${displayRows.length.toLocaleString()} shown / ${frames.size.toLocaleString()} calls · ${events.length.toLocaleString()} events`,
          capture && h('span', { class: 'parse-time', title: 'Parser invocation including diagnostics and JIT; excludes result inspection and worker startup.' }, ` · Parse ${capture.parseElapsedMs.toFixed(2)} ms`),
          capture?.truncated && ' · partial recording'),
        h('div', { class: 'trace-table', ref: table, onScroll: e => setScrollTop(e.currentTarget.scrollTop), tabIndex: 0, 'aria-label': 'Parser calls' }, h('div', { class: 'trace-table-head' }, h('span', {}, mode === 'Callers' ? 'SELECTED RULE → CALLERS' : 'RULE / OUTCOME'), h('span', {}, mode === 'Input' ? 'OBSERVED INPUT REACH' : 'EXECUTION ORDER')),
          !events.length ? h('div', { class: 'empty' }, h('h3', {}, 'A clear view into your grammar'), h('p', {}, 'Load a diagnostic assembly and run a parser to explore its calls, backtracking, and results.')) :
          [!displayRows.length && h('div', { class: 'empty', key: 'no-matches' }, 'No calls match these filters at this event.'),
          h('div', { key: 'top-space', style: { height: `${window.start * rowHeight}px` }, 'aria-hidden': true }),
          ...displayRows.slice(window.start, window.end).map(frame => {
            const max = mode === 'Input' ? Math.max(1, capture.input.length) : Math.max(1, events.length - 1);
            const start = mode === 'Input' ? frame.start : frame.enter, end = mode === 'Input' ? frame.max : frame.last;
            return h('div', { class: 'trace-row ' + (frame.id === focus?.id ? 'selected' : ''), key: frame.id, 'data-call-id': frame.id },
              h('div', { class: 'rule', style: { paddingLeft: `${12 + Math.min(frame.depth, 15) * 16}px` } },
                button(frame.children.length ? (collapsed.has(frame.id) ? '▸' : '▾') : '·', () => { const next = new Set(collapsed); next.has(frame.id) ? next.delete(frame.id) : next.add(frame.id); setCollapsed(next); }, { class: 'disclosure', 'aria-label': `Toggle ${frame.rule}` }),
                button(frame.rule, () => { seek(frame.exit ?? frame.enter); }, { class: 'rule-name', title: frame.rule }), h('span', { class: 'status-dot ' + frame.status, title: frame.status })),
              button(h('span', { class: 'bar ' + frame.status, style: { marginLeft: `${100 * start / max}%`, width: `${Math.max(.8, 100 * (end - start) / max)}%` } },
                h('span', {}, mode === 'Input' ? `${frame.start} → ${frame.end}` : frame.status)), () => seek(frame.enter), { class: 'lane', title: `Jump to entry of ${frame.rule}` }));
          }), h('div', { key: 'bottom-space', style: { height: `${(displayRows.length - window.end) * rowHeight}px` }, 'aria-hidden': true })]),
        h('div', { class: 'inspector' }, h('div', { class: 'inspector-top' }, h('b', {}, focus?.rule || 'Event inspector'), event && badge(event.kind, event.kind), event && h('code', {}, `cursor ${event.offset}${event.kind === 'reset' ? ' → ' + event.target : ''}`)),
          focus && h('div', { class: 'metrics' }, h('span', {}, `Entry ${focus.start}`), h('span', {}, `${['running', 'incomplete'].includes(focus.status) ? 'Cursor' : 'Return'} ${focus.end}`), h('span', {}, `Observed reach ${focus.max}`), button('Jump to entry', () => seek(focus.enter)), button('Jump to latest event', () => seek(focus.last))),
          buffer && h('div', {}, h('p', { class: 'hint' }, `Buffer starts at ${buffer.start} · ${buffer.text.length} units · HitEnd ${event.hitEnd}`),
            h('pre', { class: 'buffer' }, buffer.text.slice(Math.max(0, relative - 80), relative), h('mark', {}, buffer.text[relative] || 'EOF'), buffer.text.slice(relative + 1, relative + 100))),
          h('p', { class: 'hint' }, 'Calls show generated execution. Optimized or skipped branches are not invented. Observed reach is not an exact mismatch position.')))),
    h('footer', {}, h('span', {}, 'Parlot Explorer · local .NET worker + offline web UI'), capture && h('code', {}, 'Build ' + capture.assemblyVersion.slice(0, 8))),
    browse && h('div', { class: 'modal-backdrop' }, h('section', { class: 'modal panel', role: 'dialog', 'aria-label': 'Choose parser assembly' },
      h('div', { class: 'panel-heading' }, h('h2', {}, 'Choose parser assembly'), button('Close', () => setBrowse(null))),
      h('div', { class: 'browse-path' }, h('input', { 'aria-label': 'Browse directory', value: browse.directory, onInput: e => setBrowse({ ...browse, directory: e.target.value }) }), button('Go', () => files(browse.directory))),
      h('div', { class: 'file-list' }, browse.parent && button('↑ Parent directory', () => files(browse.parent)),
        browse.entries.map(item => button((item.directory ? '▸ ' : '◇ ') + item.name, () => { if (item.directory) files(item.path); else { setBrowse(null); setPath(item.path); load(item.path); } }))))));
}
render(h(App), document.getElementById('app'));
