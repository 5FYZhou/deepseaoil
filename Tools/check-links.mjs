// 扫 .md 里的相对链接与图片引用，逐个 Test-Path。
// 用途：文档重排后确认没有断链（含 Docs/表格数据配置/image/ 的截图引用）。
// 用法: node Tools/check-links.mjs <仓库根>
// 退出码 0 = 无断链。
import fs from 'node:fs';
import path from 'node:path';

const root = process.argv[2] || process.cwd();
const SKIP = ['node_modules', '.git', 'Library', 'Temp', 'obj', 'bin', 'Logs'];

function walk(dir, acc) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (SKIP.includes(e.name)) continue;
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, acc);
    else if (e.name.toLowerCase().endsWith('.md')) acc.push(p);
  }
  return acc;
}

let total = 0;
const bad = [];
for (const f of walk(root, [])) {
  const rel = path.relative(root, f).replace(/\\/g, '/');
  const text = fs.readFileSync(f, 'utf8').replace(/\r\n/g, '\n');
  for (const m of text.matchAll(/!?\[[^\]]*\]\(([^)]+)\)/g)) {
    let target = m[1].trim();
    if (/^(https?:|mailto:|#)/i.test(target)) continue;
    target = target.split('#')[0].trim();
    if (!target) continue;
    if (/[<>]/.test(target)) continue;      // 占位符
    total++;
    const decoded = decodeURIComponent(target).replace(/\//g, path.sep);
    const full = path.isAbsolute(decoded) ? decoded : path.resolve(path.dirname(f), decoded);
    if (!fs.existsSync(full)) bad.push(`${rel}  ->  ${target}`);
  }
}

console.log(`相对链接/图片 ${total} 个，断链 ${bad.length} 个`);
bad.forEach(b => console.log('  BROKEN  ' + b));
process.exit(bad.length ? 1 : 0);
