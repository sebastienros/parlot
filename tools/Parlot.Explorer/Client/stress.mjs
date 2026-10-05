// Pure replay/flatten timings on real captured traces; these are not browser paint timings.
import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { performance } from 'node:perf_hooks';
import { indexTrace, visibleFrames, rowWindow } from './replay.js';
const directory = process.argv[2] || '../../../artifacts/explorer-stress';
const percentile = (values, fraction) => values.toSorted((a, b) => a-b)[Math.min(values.length-1, Math.floor(values.length*fraction))];
const results = [];
for (const file of readdirSync(directory).filter(name => name.endsWith('.capture.json'))) {
  const capture = JSON.parse(readFileSync(`${directory}/${file}`, 'utf8'));
  const events = capture.events, samples = [], sequential = [];
  const started = performance.now(), trace = indexTrace(events,capture.truncated), indexMs = performance.now()-started;
  // Warm the JIT and measure random scrubbing separately from sequential late-trace steps.
  for (let i=0;i<10;i++) visibleFrames(trace.at(events.length-1),new Set());
  for (let i=0;i<60;i++) {
    const step = Math.floor((i * 7919 % events.length));
    const begin = performance.now(); visibleFrames(trace.at(step),new Set()); samples.push(performance.now()-begin);
  }
  for (let i=Math.max(0,events.length-100);i<events.length;i++) {
    const begin = performance.now(); visibleFrames(trace.at(i),new Set()); sequential.push(performance.now()-begin);
  }
  const frames = trace.at(events.length-1), rows = visibleFrames(frames,new Set());
  const result = {case:file.replace('.capture.json',''), events:events.length, rows:rows.length,
    scrubP50Ms:+percentile(samples,.5).toFixed(2),scrubP95Ms:+percentile(samples,.95).toFixed(2),
    nextP50Ms:+percentile(sequential,.5).toFixed(2),nextP95Ms:+percentile(sequential,.95).toFixed(2),
    indexMs:+indexMs.toFixed(2), maximumRenderedRows:rowWindow(rows.length,0).end,tailHidden:false};
  console.log(JSON.stringify(result)); results.push(result);
}
writeFileSync(`${directory}/replay-indexed.json`,JSON.stringify(results,null,2));
