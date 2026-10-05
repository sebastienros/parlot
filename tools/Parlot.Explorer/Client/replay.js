// Index once per capture. Completed subtrees are immutable and shared across seeks.
// A range maximum over event offsets preserves observed reach across backtracking.
export function indexTrace(events, truncated = false) {
  let capacity = 1;
  while (capacity < events.length) capacity *= 2;
  const maxima = new Float64Array(capacity * 2);
  events.forEach((event, index) => { maxima[capacity + index] = event.offset; });
  for (let i = capacity - 1; i; i--) maxima[i] = Math.max(maxima[i * 2], maxima[i * 2 + 1]);
  function reach(first, last) {
    let max = 0;
    for (first += capacity, last += capacity + 1; first < last; first >>= 1, last >>= 1) {
      if (first & 1) max = Math.max(max, maxima[first++]);
      if (last & 1) max = Math.max(max, maxima[--last]);
    }
    return max;
  }
  const nodes = [], byId = new Map();
  events.forEach((event, index) => {
    if (event.kind === 'enter') {
      const parent = byId.get(event.parent);
      const frame = { ...event, start: event.offset, enter: index, depth: parent ? parent.depth + 1 : 0, children: [] };
      nodes.push(frame); byId.set(frame.id, frame); parent?.children.push(frame);
    } else if (event.kind !== 'reset') {
      const frame = byId.get(event.id);
      if (frame) Object.assign(frame, { status: event.kind, exit: index, last: index, end: event.offset, max: reach(frame.enter, index) });
    }
  });
  return {
    at(step) {
      step = Math.min(step, events.length - 1);
      const frames = new Map(), current = events[step];
      for (const node of nodes) {
        if (node.enter > step) break;
        const frame = node.exit <= step ? node : { ...node, exit: undefined, last: step,
          end: current.kind === 'reset' ? current.target : current.offset, max: reach(node.enter, step),
          status: truncated && step === events.length - 1 ? 'incomplete' : 'running', children: [] };
        frames.set(frame.id, frame);
        const parent = frames.get(frame.parent);
        if (parent && parent.exit === undefined) parent.children.push(frame);
      }
      return frames;
    }
  };
}
export function framesAt(events, step) { return indexTrace(events).at(step); }
// Glob matching avoids evaluating user-supplied regular expressions on every trace row.
function globMatches(name, pattern) {
  let text = 0, token = 0, star = -1, retry = 0;
  while (text < name.length) {
    if (pattern[token] === '?' || pattern[token] === name[text]) { text++; token++; }
    else if (pattern[token] === '*') { star = token++; retry = text; }
    else if (star >= 0) { token = star + 1; text = ++retry; }
    else return false;
  }
  while (pattern[token] === '*') token++;
  return token === pattern.length;
}
export function ruleMatcher(include = '', exclude = '') {
  include = include.trim().toLowerCase();
  const exclusions = exclude.split(/[,\n]/).map(text => text.trim().toLowerCase()).filter(Boolean)
    .map(pattern => ({ pattern, glob: /[*?]/.test(pattern) }));
  return frame => {
    const name = (frame.rule || '').toLowerCase();
    return (!include || name.includes(include)) && !exclusions.some(({ pattern, glob }) => glob ? globMatches(name, pattern) : name.includes(pattern));
  };
}
export function visibleFrames(frames, collapsed, filter = '', exclude = '') {
  const result = [], pending = [];
  for (const frame of frames.values()) if (!frames.has(frame.parent)) pending.push({ frame, depth: 0 });
  pending.reverse();
  filter = filter.trim();
  const matches = ruleMatcher(filter, exclude);
  while (pending.length) {
    const { frame, depth } = pending.pop();
    const visible = matches(frame);
    if (visible) result.push(frame.depth === depth ? frame : { ...frame, depth });
    // Search includes descendants of collapsed nodes.
    if (filter || !visible || !collapsed.has(frame.id)) for (let i = frame.children.length - 1; i >= 0; i--) pending.push({ frame: frame.children[i], depth: visible ? depth + 1 : depth });
  }
  return result;
}
export function callers(frames, id) {
  const path = [];
  for (let frame = frames.get(id); frame; frame = frames.get(frame.parent)) path.push(frame);
  return path;
}
export const rowHeight = 36, headerHeight = 33;
export function rowWindow(length, scrollTop, height = 520) {
  const start = Math.min(Math.max(0, length - 1), Math.max(0, Math.floor(scrollTop / rowHeight) - 8));
  return { start, end: Math.min(length, start + Math.ceil(height / rowHeight) + 17) };
}
