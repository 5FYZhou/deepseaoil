// 本地复跑 Assets/Tests/Tools/仓库一致性Tests.cs::目录说明与磁盘同源 的解析逻辑。
// 目的：在 Unity 之前就知道那条测试会不会红。
//
// 它守的坑：目录树是「一目录一行」。把两个兄弟目录写在一行
// （如 ├── Adapters/ Input/ Debug/）会被解析成一个目录名，
// 而那个名字磁盘上不存在 → 测试变红，但肉眼看树是「对的」。
// 实测：HEAD 态解析出 63 条全部通过；合并行会让它掉到 42 条并报 2 条不存在。
//
// 用法: node Tools/check-tree.mjs <仓库根>
// 退出码 0 = 目录树与磁盘同源且路径数 ≥ 40。
import fs from 'node:fs';
import path from 'node:path';

const root = process.argv[2] || process.cwd();
const doc = path.join(root, 'Docs', '目录说明.md');
if (!fs.existsSync(doc)) { console.log('FAIL 找不到 Docs/目录说明.md'); process.exit(1); }

const md = fs.readFileSync(doc, 'utf8').replace(/\r\n/g, '\n').replace(/\r/g, '\n');

const section = /##\s*仓库目录(?<body>[\s\S]*?)(\n##\s|$)/.exec(md);
if (!section) { console.log('FAIL 文档里找不到「## 仓库目录」一节'); process.exit(1); }

const block = /```[a-zA-Z]*\n(?<tree>[\s\S]*?)```/.exec(section.groups.body);
if (!block) { console.log('FAIL 「## 仓库目录」里找不到目录树代码块'); process.exit(1); }

const TREE_CHARS = [' ', '│', '├', '└', '─'];
const stack = [];      // depth -> 累积路径
const paths = [];
let lineNo = 0;
let jumpError = null;

for (const raw of block.groups.tree.split('\n')) {
  lineNo++;
  if (raw.trim().length === 0) continue;

  let nameStart = 0;
  while (nameStart < raw.length && TREE_CHARS.includes(raw[nameStart])) nameStart++;
  if (nameStart >= raw.length) continue;

  const depth = Math.floor(nameStart / 4);
  const rest = raw.slice(nameStart);

  const gap = /  +/.exec(rest);
  const name = (gap ? rest.slice(0, gap.index) : rest).trim();
  if (name.length === 0) continue;

  if (depth > stack.length && jumpError === null) {
    jumpError = `第 ${lineNo} 行缩进跳级（depth ${depth} > 已累积 ${stack.length}）：${raw}`;
  }
  while (stack.length > depth) stack.pop();

  const parent = depth === 0 ? '' : stack[depth - 1].value;
  const here = parent + name;

  if (name.endsWith('/')) stack.push({ depth, value: here });
  paths.push(here);
}

const missing = [];
for (const p of paths) {
  const full = path.join(root, p.replace(/\/$/, '').split('/').join(path.sep));
  if (!fs.existsSync(full)) missing.push(p);
}

console.log(`解析出路径 ${paths.length} 条（测试要求 ≥ 40）`);
if (jumpError) console.log('FAIL 缩进跳级 -> ' + jumpError);
if (missing.length) {
  console.log(`FAIL 文档声明但磁盘不存在（${missing.length} 条）：`);
  for (const m of missing) console.log('  ' + m);
}
const ok = paths.length >= 40 && !jumpError && missing.length === 0;
console.log(ok ? '\nOK 目录树与磁盘同源' : '\nFAIL 目录树与磁盘脱钩');
process.exit(ok ? 0 : 1);
