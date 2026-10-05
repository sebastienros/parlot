import { test } from 'node:test';
import assert from 'node:assert/strict';
import { framesAt, visibleFrames, callers } from './replay.js';
const events = [
  {kind:'enter',id:1,parent:0,offset:0}, {kind:'enter',id:2,parent:1,offset:0},
  {kind:'reset',id:2,offset:1,target:0}, {kind:'failure',id:2,offset:0},
  {kind:'enter',id:3,parent:1,offset:0}, {kind:'success',id:3,offset:0}, {kind:'success',id:1,offset:0}
];
test('rewind retains observed reach but restores cursor; zero-width success is visible', () => {
  const frames = framesAt(events,6);
  assert.equal(frames.get(2).max,1); assert.equal(frames.get(2).end,0);
  assert.equal(frames.get(1).max,1); assert.equal(frames.get(3).status,'success');
  assert.deepEqual(callers(frames,2).map(x=>x.id),[2,1]);
  assert.equal(visibleFrames(frames,new Set([1])).length,1);
});
test('backward replay does not leak future outcomes or children', () => {
  const frames = framesAt(events,1);
  assert.equal(frames.size,2); assert.equal(frames.get(2).max,0); assert.equal(frames.get(1).status,'running');
});

test('active ancestor bars follow the playhead and current cursor', () => {
  const frames = framesAt([{kind:'enter',id:1,parent:0,offset:0}, {kind:'enter',id:2,parent:1,offset:0}, {kind:'success',id:2,offset:2}], 2);
  assert.equal(frames.get(1).last,2); assert.equal(frames.get(1).end,2); assert.equal(frames.get(1).status,'running');
  assert.equal(frames.get(1).exit,undefined); assert.equal(frames.get(2).exit,2);
});

test('indexed seeks match a sequential replay, including backward scrubs', async () => {
  const { indexTrace } = await import('./replay.js');
  const index = indexTrace(events);
  function reference(step) {
    const frames = new Map();
    for (let i = 0; i <= step; i++) {
      const event = events[i];
      if (event.kind === 'enter') frames.set(event.id, { max: event.offset, parent: event.parent, status: 'running', enter: i });
      const frame = frames.get(event.id);
      frame.max = Math.max(frame.max, event.offset);
      frame.end = event.kind === 'reset' ? event.target : event.offset;
      frame.last = i;
      if (!['enter', 'reset'].includes(event.kind)) { frame.status = event.kind; frame.exit = i; }
      for (let parent = frames.get(frame.parent); parent; parent = frames.get(parent.parent)) {
        parent.max = Math.max(parent.max, event.offset); parent.end = frame.end; parent.last = i;
      }
    }
    return frames;
  }
  for (const step of [6, 0, 4, 2, 5, 1, 3]) {
    const actual = index.at(step), expected = reference(step);
    assert.equal(actual.size, expected.size);
    for (const [id, frame] of expected) for (const property of ['max', 'end', 'last', 'status', 'exit', 'enter'])
      assert.equal(actual.get(id)[property], frame[property], `${step}/${id}/${property}`);
    for (const frame of actual.values()) assert.ok(frame.children.every(child => child.enter <= step));
  }
});
test('truncated calls are incomplete only at the end of the recording', async () => {
  const { indexTrace } = await import('./replay.js');
  const index = indexTrace(events.slice(0, 3), true);
  assert.equal(index.at(2).get(1).status, 'incomplete');
  assert.equal(index.at(1).get(1).status, 'running');
  assert.equal(index.at(2).get(2).end, 0);
});
test('deep traces flatten without recursion and search reaches collapsed descendants', async () => {
  const { indexTrace, rowWindow } = await import('./replay.js');
  const deep = Array.from({length:10000}, (_, i) => ({kind:'enter',id:i+1,parent:i,rule:`rule ${i}`,offset:i}));
  const frames = indexTrace(deep,true).at(9999);
  assert.equal(visibleFrames(frames,new Set()).length,10000);
  assert.equal(visibleFrames(frames,new Set([1]),'rule 9999')[0].id,10000);
  const window = rowWindow(10000,9999*36);
  assert.equal(window.end,10000); assert.ok(window.end-window.start <= 32);
  assert.deepEqual(rowWindow(0,10000),{start:0,end:0});
});

test('noise exclusions keep descendants, even below a collapsed excluded wrapper', async () => {
  const { indexTrace, ruleMatcher } = await import('./replay.js');
  const trace = indexTrace([
    {kind:'enter',id:1,parent:0,rule:'Root',offset:0},
    {kind:'enter',id:2,parent:1,rule:'Then_1',offset:0},
    {kind:'enter',id:3,parent:2,rule:'Number',offset:0},
    {kind:'success',id:3,offset:2}, {kind:'success',id:2,offset:2},
    {kind:'enter',id:4,parent:1,rule:'SkipWS_3',offset:2},
    {kind:'success',id:4,offset:3}, {kind:'success',id:1,offset:3}
  ]);
  const frames = trace.at(7), collapsed = new Set([2]);
  assert.deepEqual(visibleFrames(frames,collapsed,'','Then_*, skipws*').map(row=>row.id),[1,3]);
  assert.deepEqual(visibleFrames(frames,collapsed,'','Then_*, skipws*').map(row=>row.depth),[0,1]);
  assert.equal(frames.get(3).depth,2); // Display indentation changes without rewriting the call graph.
  assert.deepEqual(visibleFrames(frames,collapsed,'number','Then_*').map(row=>row.id),[3]);
  assert.deepEqual(callers(frames,3).filter(ruleMatcher('','Then_*')).map(row=>row.id),[3,1]);
  assert.equal(visibleFrames(frames,collapsed,'','*').length,0);
  assert.equal(trace.at(7).size,4); // Filtering never changes the recording or replay state.
  assert.equal(trace.at(1).size,2);
});
test('exclusion patterns handle wildcards, whitespace, literal punctuation, and blank input', async () => {
  const { ruleMatcher } = await import('./replay.js');
  const allows = (name, pattern) => ruleMatcher('',pattern)({rule:name});
  assert.equal(allows('MySkipWSHelper','skipws'),false);
  assert.equal(allows('MySkipWSHelper','SkipWS*'),true); // Glob matches the whole name.
  assert.equal(allows('Then_12',' Then_??, SkipWS* '),false);
  assert.equal(allows('Then_1','Then_??'),true);
  assert.equal(allows('a[b].c','a[b].*'),false); // Punctuation is not regex syntax.
  assert.equal(allows('Then_1','skipws*\nthen_*'),false);
  assert.equal(allows('anything',' , , '),true);
  assert.equal(allows('aaab','a*b'),false);
  assert.equal(allows('aaac','a*b'),true);
  assert.equal(allows('root','*root*'),false);
});
