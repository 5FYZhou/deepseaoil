// 用法: node verify.mjs <repoRoot>
// 扫 repoRoot 下所有 .md 的 ```mermaid 块，用 mermaid 的真实解析 + 渲染管线逐个验证。
// jsdom 没有 SVG 布局引擎，这里给 getBBox / getComputedTextLength 打上桩，
// 让布局能跑完 —— 我们验的是「语法与图类型是否成立」，不是像素。
import fs from 'node:fs';
import path from 'node:path';
import { JSDOM } from 'jsdom';

const repoRoot = process.argv[2] || process.cwd();

function walk(dir, acc) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (['node_modules', '.git', 'Library', 'Temp', 'obj', 'bin'].includes(e.name)) continue;
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, acc);
    else if (e.name.toLowerCase().endsWith('.md')) acc.push(p);
  }
  return acc;
}

const blocks = [];
for (const f of walk(repoRoot, [])) {
  const rel = path.relative(repoRoot, f).replace(/\\/g, '/');
  const lines = fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n').split('\n');
  let inBlock = false, start = 0, buf = [];
  for (let i = 0; i < lines.length; i++) {
    const t = lines[i].trim();
    if (!inBlock && /^```mermaid\s*$/.test(t)) { inBlock = true; start = i + 1; buf = []; continue; }
    if (inBlock && /^```\s*$/.test(t)) { inBlock = false; blocks.push({ file: rel, line: start, code: buf.join('\n') }); continue; }
    if (inBlock) buf.push(lines[i]);
  }
  if (inBlock) blocks.push({ file: rel, line: start, code: buf.join('\n'), unterminated: true });
}

// ---- jsdom 环境 ----
const dom = new JSDOM('<!DOCTYPE html><html><body><div id="stage"></div></body></html>', {
  pretendToBeVisual: true, url: 'http://localhost/'
});
const { window } = dom;
for (const [k, v] of [['window', window], ['document', window.document], ['navigator', window.navigator], ['DOMPurify', undefined]]) {
  try { globalThis[k] = v; } catch { Object.defineProperty(globalThis, k, { value: v, configurable: true, writable: true }); }
}

// SVG 布局桩：按文本长度估个盒子，够 mermaid 的 bbox 计算走完
const proto = window.SVGElement.prototype;
proto.getBBox = function () {
  const t = (this.textContent || '').length;
  return { x: 0, y: 0, width: Math.max(10, t * 8), height: 16 };
};
proto.getComputedTextLength = function () { return Math.max(10, (this.textContent || '').length * 8); };
proto.getScreenCTM = function () { return { a: 1, b: 0, c: 0, d: 1, e: 0, f: 0, inverse() { return this; }, multiply() { return this; } }; };
window.SVGElement.prototype.getBBox = proto.getBBox;
window.SVGElement.prototype.getComputedTextLength = proto.getComputedTextLength;

const mermaid = (await import('mermaid')).default;
mermaid.initialize({ startOnLoad: false, securityLevel: 'loose' });

const results = [];
for (const b of blocks) {
  const tag = `${b.file}:${b.line}`;
  if (b.unterminated) { results.push({ tag, ok: false, msg: '代码围栏未闭合' }); continue; }
  let kind = (b.code.trim().split('\n')[0] || '').trim();
  try {
    await mermaid.parse(b.code);
  } catch (e) {
    results.push({ tag, kind, ok: false, msg: 'parse: ' + String(e?.message ?? e).replace(/\s+/g, ' ').slice(0, 400) });
    continue;
  }
  let info = '';
  try {
    const { svg } = await mermaid.render('g' + results.length, b.code);
    const vb = /viewBox="([^"]+)"/.exec(svg);
    const nodes = (svg.match(/class="[^"]*\bnode\b/g) || []).length;
    const edges = (svg.match(/class="[^"]*\b(edgePath|flowchart-link)\b/g) || []).length
                + (svg.match(/class="[^"]*\bmessageLine[01]\b/g) || []).length
                + (svg.match(/class="[^"]*\btransition\b/g) || []).length
                + (svg.match(/class="[^"]*\brelationshipLine\b/g) || []).length;
    info = `svg ${svg.length}B | viewBox ${vb ? vb[1] : '?'} | node ${nodes} | edge ${edges}`;
  } catch (e) {
    results.push({ tag, kind, ok: true, warn: 'render: ' + String(e?.message ?? e).replace(/\s+/g, ' ').slice(0, 200), msg: 'parse OK' });
    continue;
  }
  results.push({ tag, kind, ok: true, msg: info });
}

const bad = results.filter(r => !r.ok);
for (const r of results) {
  const mark = r.ok ? (r.warn ? 'WARN' : 'OK  ') : 'FAIL';
  console.log(`${mark}  ${r.tag}  [${r.kind}]  ${r.msg}${r.warn ? '  << ' + r.warn : ''}`);
}
console.log(`\n块数=${results.length}  通过=${results.length - bad.length}  失败=${bad.length}`);
process.exit(bad.length ? 1 : 0);
