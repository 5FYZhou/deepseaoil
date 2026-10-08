"""删列工具的自测：不碰真表，只在内存 XML 上验证左移语义。

用法：python Tools/test_xlsx_delcols.py

为什么要有它：删列改的是**列位置**，而"表头看着对"完全不能证明数据跟着走了。
之前两次翻车都是靠逐格核对手工发现的，这里把当时的病根固化成断言：
  ① 成对格（<c ...><is><t>…</t></is></c>）不能被 `<c ...>` 正则跨 </c> 吃穿，
     也不能因此丢掉某个格的 r=（丢了就等于整列错位）；
  ② 删掉的列**只能**删那一列：余下的格按左移后的新列号排列，内容一格不少；
  ③ 尾部删列后 dimension 必须收敛，不能留一个空的尾列。
"""

import re
import sys

sys.path.insert(0, __file__.rsplit("\\", 1)[0])

import xlsx_delcols as X  # noqa: E402


def cell(col, n, text):
    return '<c r="%s%d" t="inlineStr"><is><t>%s</t></is></c>' % (col, n, text)


def row(n, texts, spans=None):
    """按顺序生成 A,B,C… 的格 —— 引用必须与位置一致，否则测的就不是我改的语义。"""
    head = '<row r="%d"%s>' % (n, ' spans="%s"' % spans if spans else "")
    return head + "".join(cell(X.num_to_col(i + 1), n, t) for i, t in enumerate(texts)) + "</row>"


def refs_of(xml, n=0):
    rows = re.findall(r"<row\b.*?</row>", xml, re.S)
    return re.findall(r'\br="([A-Z]+\d+)"', rows[n])


def text_of(xml, ref):
    m = re.search(r'<c r="%s"[^>]*><is><t>([^<]*)</t>' % ref, xml)
    return m.group(1) if m else None


def main():
    failures = []

    def check(name, cond, detail=""):
        print(("  ok   " if cond else "  FAIL ") + name + ("" if cond else "  <- " + detail))
        if not cond:
            failures.append(name)

    print("A) 删中间一列：余下格左移，内容跟着走")
    out = X.shift_cols(row(1, ["a", "b", "c", "d"]), [2])
    check("refs = A1,B1,C1", refs_of(out) == ["A1", "B1", "C1"], str(refs_of(out)))
    check("A1 = a", text_of(out, "A1") == "a")
    check("B1 = c（原 C 列左移过来）", text_of(out, "B1") == "c", str(text_of(out, "B1")))
    check("C1 = d（原 D 列左移过来）", text_of(out, "C1") == "d", str(text_of(out, "C1")))

    print("B) 删末尾两列：倒数第三列是最后保留的格，必须还在")
    out = X.shift_cols(row(1, ["a", "b", "c", "d", "e"]), [4, 5])
    check("refs = A1,B1,C1", refs_of(out) == ["A1", "B1", "C1"], str(refs_of(out)))
    check("C1 = c（原 C 列，不是 d/e）", text_of(out, "C1") == "c", str(text_of(out, "C1")))

    print("C) 成对格里的 <is><t> 不能被吃穿，且每格都保住 r=")
    out = X.shift_cols(row(1, ["x", "y", "z"]), [3])
    check("每格都有 r=（数量对得上）", out.count("<c ") == len(re.findall(r'<c r="', out)), out)
    check("B1 = y", text_of(out, "B1") == "y", str(text_of(out, "B1")))
    check("refs = A1,B1", refs_of(out) == ["A1", "B1"], str(refs_of(out)))

    print("D) 尾部删列后 dimension 收敛（不留空尾列）")
    xml = '<dimension ref="A1:E3"/>' + row(1, ["a", "b"])
    out = X.shift_cols(xml, [5])
    check("dimension = A1:B1", 'ref="A1:B1"' in out, out[:60])

    print("E) cols 列宽跟着左移")
    xml = ('<cols><col min="4" max="4" width="9" customWidth="1"/></cols>'
           + row(1, ["a", "b", "c", "d"]))
    out = X.shift_cols(xml, [2])
    check('<col min="3" max="3"', '<col min="3" max="3"' in out, out[:120])

    print("F) 行 spans 收窄")
    out = X.shift_cols(row(1, ["a", "b", "c", "d"], spans="1:4"), [2])
    check('spans="1:3"', 'spans="1:3"' in out, out[:60])

    print("G) 列号换算")
    check("K = 11", X.col_to_num("K") == 11)
    check("11 = K", X.num_to_col(11) == "K")
    check("AA = 27", X.col_to_num("AA") == 27)

    print()
    if failures:
        print("失败 %d 项：%s" % (len(failures), ", ".join(failures)))
        return 1

    print("全部通过")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
