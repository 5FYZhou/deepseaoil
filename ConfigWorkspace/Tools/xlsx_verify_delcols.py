"""删列后核对：新表的每一格必须等于"原表同格左移若干列"的结果。

用法：python Tools/xlsx_verify_delcols.py <原表> <新表> <删掉的列号,列号...>

为什么单独一个脚本：删列是**位置敏感**的改动，只看表头对不齐是查不出来的
（实测踩过两次：一次成对格被吃穿导致 XML 损坏，一次 K1 丢了 r= 导致 hp 列错位成 flash_hz），
所以这里逐格比对，并把「丢了 r= 的格」和「结构残留」都当失败报出来。
"""

import re
import sys
import zipfile


def split_ref(ref):
    m = re.fullmatch(r"([A-Z]+)(\d+)", ref)
    return m.group(1), int(m.group(2))


def col_to_num(name):
    n = 0
    for ch in name:
        n = n * 26 + (ord(ch) - ord("A") + 1)
    return n


def num_to_col(n):
    out = ""
    while n > 0:
        n, r = divmod(n - 1, 26)
        out = chr(ord("A") + r) + out
    return out


def cells_by_ref(path):
    with zipfile.ZipFile(path) as z:
        sheet = z.read("xl/worksheets/sheet1.xml").decode("utf-8")

    by_ref = {}
    no_ref = []

    for row in re.findall(r"<row\b[^>]*>.*?</row>", sheet, re.S):
        for seg in re.findall(r"<c\b[^>]*?(?:/>|>.*?</c>)", row, re.S):
            m = re.match(r'<c\b[^>]*?\br="([A-Z]+\d+)"', seg)
            if not m:
                no_ref.append(seg[:60])
                continue
            by_ref[m.group(1)] = seg

    return by_ref, no_ref


def inner(seg):
    """取值部分（丢掉 r= 与 s=，只比内容）。"""
    gt = seg.find(">")
    return seg[gt + 1 :]


def main(argv):
    old_path, new_path, deleted = argv[0], argv[1], sorted(int(x) for x in argv[2].split(",") if x)

    if sys.stdout.encoding.lower() not in ("utf-8", "utf8"):
        sys.stdout.reconfigure(encoding="utf-8")

    old, old_no_ref = cells_by_ref(old_path)
    new, new_no_ref = cells_by_ref(new_path)

    failed = False

    if old_no_ref:
        print("原表里本来就有丢了 r= 的格（%d 个），核对不可靠" % len(old_no_ref))
        failed = True

    if new_no_ref:
        print("失败：新表里出现了丢了 r= 的格 %d 个 —— 左移把列信息写没了：" % len(new_no_ref))
        for s in new_no_ref[:5]:
            print("   ", s)
        failed = True

    def shift(n):
        return n - sum(1 for d in deleted if d < n)

    expected = {}

    for ref, seg in old.items():
        name, row = split_ref(ref)
        num = col_to_num(name)
        if num in deleted:
            continue
        expected["%s%d" % (num_to_col(shift(num)), row)] = inner(seg)

    for ref in sorted(set(expected) | set(new)):
        if ref not in expected:
            print("失败：新表多出单元格 %s = %s" % (ref, inner(new[ref])[:40]))
            failed = True
        elif ref not in new:
            print("失败：新表缺了单元格 %s（原值 %s）" % (ref, expected[ref][:40]))
            failed = True
        elif expected[ref] != inner(new[ref]):
            print("失败：%s 值不对\n   期望 %s\n   实际 %s" % (ref, expected[ref][:60], inner(new[ref])[:60]))
            failed = True

    if failed:
        print("核对未通过：%s vs %s" % (old_path, new_path))
        return 1

    print("核对通过：%d 格逐格一致（删列 %s）" % (len(expected), deleted))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
