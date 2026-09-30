// ---------------------------------------------------------------------------
// Mermaid 渲染校验器
//
// 为什么需要它：Mermaid 是文档里唯一会「静默出事」的东西——画错了不报错，
// 只是渲染出一块红框，而写的人在自己的预览里未必看得见。报错又是英文
// parse error，人工定位困难。这个脚本把仓库下所有 ```mermaid 块抽出来，
// 走一遍 mermaid 的真实解析 + 渲染管线。
//
// 已实测能抓到的错（Docs/分层设计/表现层.md 的启动装配序图即因此报红）：
//   Note over GR: if (!X.IsReady) X.Init();<br/>…   → 裸 ; 与 () 被当成语法
//   报错形如：Parse error ... Expecting '()', 'SOLID_OPEN_ARROW', ...
// 正确写法：节点标签一律加双引号，Note 文字加引号，长条下沉到图旁的表格。
//
// 依赖（jsdom 没有 SVG 布局引擎，脚本给 getBBox / getComputedTextLength 打了桩，
// 让布局能跑完——验的是「语法与图类型是否成立」，不是像素）：
//
//   npm install --prefix $env:TEMP\mermaid-deps mermaid jsdom
//   # 在仓库根建一次 junction，之后直接跑：
//   cmd /c mklink /J node_modules $env:TEMP\mermaid-deps\node_modules
//   node Tools\verify-mermaid.mjs .
//
// 输出形如：OK Docs/框架蓝图.md:26 [flowchart LR] svg 12345B | node 12 | edge 14
// 末尾汇总「块数= / 通过= / 失败=」。退出码 0 = 全部通过。
// 已知噪声：WARN ... CSSStyleSheet is not defined 是 jsdom 缺 SVG 样式表 API 的
// 渲染告警，解析已通过；节点数 > 15 也会 WARN（提示该拆图）。
// ---------------------------------------------------------------------------
import fs from 'node:fs';
import path from 'node:path';
import { JSDOM } from 'jsdom';

const repoRoot = process.argv[2] || process.cwd();
const SKIP_DIRS = ['node_modules', '.git', 'Library', 'Temp', 'obj', 'bin', 'Logs'];

function walk(dir, acc) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (SKIP_DIRS.includes(e.name)) continue;
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, acc);
    else if (e.name.toLowerCase().endsWith('.md')) acc.push(p);
  }
  return acc;
}

// ---- 抽 ```mermaid 块 ----
const blocks = [];
for (const f of walk(repoRoot, [])) {
  const rel = path.relative(repoRoot, f).replace(/\\/g, '/');
  const lines = fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n').split('\n');
  let inBlock = false, start = 0, buf = [];
  for (let i = 0; i < lines.length; i++) {
    const t = lines[i].trim();
    if (!inBlock && /^```mermaid\s*$/.test(t)) { inBlock = true; start = i + 1; buf = []; continue; }
    if (inBlock && /^```\s*$/.test(t)) {
      inBlock = false;
      blocks.push({ file: rel, line: start, code: buf.join('\n') });
      continue;
    }
    if (inBlock) buf.push(lines[i]);
  }
  if (inBlock) blocks.push({ file: rel, line: start, code: buf.join('\n'), unterminated: true });
}

// ---- jsdom 环境 ----
const dom = new JSDOM('<!DOCTYPE html><html><body><div id="stage"></div></body></html>', {
  pretendToBeVisual: true, url: 'http://localhost/'
});
const { window } = dom;
for (const [k, v] of [['window', window], ['document', window.document], ['navigator', window.navigator]]) {
  try { globalThis[k] = v; }
  catch { Object.defineProperty(globalThis, k, { value: v, configurable: true, writable: true }); }
}

const svgProto = window.SVGElement.prototype;
const stub = box => function () { return box((this.textContent || '').length); };
svgProto.getBBox = stub(n => ({ x: 0, y: 0, width: Math.max(10, n * 8), height: 16 }));
svgProto.getComputedTextLength = stub(n => Math.max(10, n * 8));
svgProto.getScreenCTM = function () {
  return { a: 1, b: 0, c: 0, d: 1, e: 0, f: 0, inverse() { return this; }, multiply() { return this; } };
};

const mermaid = (await import('mermaid')).default;
mermaid.initialize({ startOnLoad: false, securityLevel: 'loose' });

// ---- 逐个验 ----
const results = [];
for (const b of blocks) {
  const tag = `${b.file}:${b.line}`;
  if (b.unterminated) { results.push({ tag, ok: false, msg: '代码围栏未闭合' }); continue; }

  const kind = (b.code.trim().split('\n')[0] || '').trim();
  try {
    await mermaid.parse(b.code);
  } catch (e) {
    results.push({ tag, kind, ok: false, msg: 'parse: ' + String(e?.message ?? e).replace(/\s+/g, ' ').slice(0, 400) });
    continue;
  }

  try {
    const { svg } = await mermaid.render('g' + results.length, b.code);
    const vb = /viewBox="([^"]+)"/.exec(svg);
    const nodes = (svg.match(/class="[^"]*\bnode\b/g) || []).length;
    const edges = (svg.match(/class="[^"]*\b(edgePath|flowchart-link)\b/g) || []).length
                + (svg.match(/class="[^"]*\bmessageLine[01]\b/g) || []).length
                + (svg.match(/class="[^"]*\btransition\b/g) || []).length
                + (svg.match(/class="[^"]*\brelationshipLine\b/g) || []).length;
    results.push({
      tag, kind, ok: true, warn: nodes > 15,
      msg: `svg ${svg.length}B | viewBox ${vb ? vb[1] : '?'} | node ${nodes} | edge ${edges}` + (nodes > 15 ? '  << 节点数 > 15，按规范该拆图' : '')
    });
  } catch (e) {
    results.push({ tag, kind, ok: true, warn: true, msg: `parse OK | render: ${String(e?.message ?? e).replace(/\s+/g, ' ').slice(0, 160)}` });
  }
}

let fail = 0, warn = 0;
for (const r of results) {
  const mark = r.ok ? (r.warn ? 'WARN' : 'OK  ') : 'FAIL';
  if (!r.ok) fail++;
  else if (r.warn) warn++;
  console.log(`${mark}  ${r.tag}  [${r.kind || '?'}]  ${r.msg}`);
}
console.log(`\n块数=${results.length}  通过=${results.length - fail}  失败=${fail}  告警=${warn}`);
process.exit(fail ? 1 : 0);
