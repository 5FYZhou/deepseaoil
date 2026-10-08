"""按列号删除 xlsx 里的列（保 XML 保真，不走第三方库的重写）。

用法：
    python Tools/xlsx_delcols.py <xlsx> <列号,列号,...> [set=D5:3 ...]
    python Tools/xlsx_delcols.py <xlsx> set=D5:3            （只改值，不删列）

设计要点（为什么不用 openpyxl 写回）：
  · Luban 读的是单元格文本，但表格里还有 `##type` 里写的 range 校验、注释、列宽等
    靠样式与列序表达的信息，openpyxl 整簿重写会重建 styles 与小众扩展标记；
  · 这些表里实测没有公式 / 合并单元格 / 数据校验（脚本会先自检，命中即拒绝），
    所以"删掉 <c> 再把右边的格左移改 r="..." 并保留 s= 样式号"是安全的等价变换；
  · sharedStrings.xml 一律不动：留着未引用的字符串条目是合法的 OOXML，
    但**绝不能删条目**——那会让所有 <v>N</v> 的下标整体错位。

改完必须跑 Tools/xlsx_verify_delcols.py 逐格核对：删列是位置敏感的改动，
表头看起来对不代表数据跟着走了（踩过两次：成对格被吃穿导致 XML 损坏、
格丢了 r= 导致整列错位）。测试见 Tools/test_xlsx_delcols.py。
"""

import re
import shutil
import sys
import zipfile


def split_ref(ref):
    m = re.fullmatch(r"([A-Z]+)(\d+)", ref)
    if not m:
        raise ValueError("非法单元格引用: %s" % ref)
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


def ref_of(seg):
    m = re.match(r'<c\b[^>]*?\br="([A-Z]+\d+)"', seg)
    return m.group(1) if m else None


def replace_ref(seg, new_ref):
    """只改 <c> **开始标签**里的 r=，内容（<is>/<v>）一律不碰。"""
    gt = seg.find(">")
    return re.sub(r'\br="[A-Z]+\d+"', 'r="%s"' % new_ref, seg[:gt], count=1) + seg[gt:]


def split_cells(row_xml):
    """把 <row>…</row> 切成 (row 头, [(cell_xml, ref), ...])。

    🔴 必须按标签配对切，不能用正则扫 `<c ...>`：成对格里有 <is><t> 嵌套，
    非贪婪的 `[^>]*>` 会跨过 </c> 吃到下一个格的 r=。切不到 r= 的格按 None 保留。
    """
    head = re.match(r"<row\b[^>]*>", row_xml)
    body_start = head.end() if head else 0

    tail_start = row_xml.rfind("</row>")
    if tail_start < 0:
        tail_start = len(row_xml)

    body = row_xml[body_start:tail_start]
    cells = []
    i = 0

    while i < len(body):
        start = body.find("<c ", i)

        if start < 0:
            rest = body[i:]
            if rest.strip():
                cells.append((rest, None))
            break

        gap = body[i:start]
        if gap.strip():
            cells.append((gap, None))

        gt = body.find(">", start)
        if gt < 0:
            raise ValueError("单元格没有闭合的 '>'")

        if body[gt - 1] == "/":                       # <c .../>
            end = gt + 1
        else:
            close = body.find("</c>", gt)
            if close < 0:
                raise ValueError("成对单元格找不到 </c>")
            end = close + 4

        seg = body[start:end]
        cells.append((seg, ref_of(seg)))
        i = end

    return (head.group(0) if head else ""), cells


def shift_cols(text, deleted):
    """删掉 deleted（1-based、升序）这些列，右边的格整体左移。"""
    dropped = set(deleted)

    def shift(n):
        return n - sum(1 for d in deleted if d < n)

    def repl_row(m):
        head, cells = split_cells(m.group(0))

        # <row spans="1:14">：列数变了，一并收窄
        head = re.sub(
            r'spans="(\d+):(\d+)"',
            lambda s: 'spans="%s:%d"' % (s.group(1), shift(int(s.group(2)))),
            head,
        )

        out = []

        for seg, ref in cells:
            if ref is None:
                out.append(seg)          # 没有列信息，原样留着
                continue

            name, row_no = split_ref(ref)
            num = col_to_num(name)

            # 🔴 只有「格自己的 r= 与登记一致」时才动它；对不上原样保留，
            #    否则会把别人的格改名（实测踩过）。
            if ref_of(seg) != ref:
                out.append(seg)
                continue

            if num in dropped:
                continue                 # 这一列被删了

            out.append(replace_ref(seg, "%s%d" % (num_to_col(shift(num)), row_no)))

        return head + "".join(out) + "</row>"

    text = re.sub(r"<row\b.*?</row>", repl_row, text, flags=re.S)

    # 列宽：<col min= max= .../>；被删的截断，右侧的整体左移
    def repl_col(m):
        body = m.group(0)
        lo = int(re.search(r'min="(\d+)"', body).group(1))
        hi = int(re.search(r'max="(\d+)"', body).group(1))

        kept = [c for c in range(lo, hi + 1) if c not in dropped]
        if not kept:
            return ""

        body = re.sub(r'min="\d+"', 'min="%d"' % shift(kept[0]), body)
        body = re.sub(r'max="\d+"', 'max="%d"' % shift(kept[-1]), body)

        return body

    def repl_cols_block(m):
        return m.group(1) + re.sub(r"<col [^/]*/>", repl_col, m.group(2)) + m.group(3)

    text = re.sub(r"(<cols>)(.*?)(</cols>)", repl_cols_block, text, flags=re.S)

    # dimension：按**实际还剩的**单元格重算。
    # 🔴 不能用 shift(原 max) 推：被删列在尾部时它会算出一个不存在的空列
    #    （实测：tile_state 删掉末列 icon 后留下一个空的 M 列，Excel 打开会看见多一列）。
    refs = re.findall(r'\br="([A-Z]+\d+)"', text)

    if refs:
        max_col = max(col_to_num(split_ref(r)[0]) for r in refs)
        max_row = max(split_ref(r)[1] for r in refs)

        text = re.sub(r'ref="A1:[A-Z]+\d+"', 'ref="A1:%s%d"' % (num_to_col(max_col), max_row), text, count=1)

    return text


def set_cell(text, col, row, value):
    ref = "%s%d" % (col, row)
    idx = text.find('r="%s"' % ref)

    if idx < 0:
        raise SystemExit("找不到单元格 %s" % ref)

    row_start = text.rfind("<row ", 0, idx)
    row_end = text.find("</row>", idx)

    if row_start < 0 or row_end < 0:
        raise SystemExit("单元格 %s 不在任何 <row> 里" % ref)

    block = text[row_start : row_end + 6]
    hit = None

    for seg, cell_ref in split_cells(block)[1]:
        if cell_ref == ref:
            hit = seg
            break

    if hit is None:
        raise SystemExit("找不到单元格 %s" % ref)

    style = re.search(r'\bs="(\d+)"', hit)
    new = '<c r="%s"%s><v>%s</v></c>' % (ref, (' s="%s"' % style.group(1)) if style else "", value)

    return text[:row_start] + block.replace(hit, new, 1) + text[row_end + 6 :]


def main(argv):
    src = argv[0]
    rest = argv[1:]
    deleted = []

    # 列号表可省：只想改值时直接写 set=单元格:值
    if rest and not rest[0].startswith("set="):
        deleted = sorted(int(x) for x in rest[0].split(",") if x)
        rest = rest[1:]

    sets = []
    for token in rest:
        if not token.startswith("set="):
            raise SystemExit("无法识别的参数: %s" % token)

        ref, _, value = token[4:].partition(":")
        m = re.fullmatch(r"([A-Z]+)(\d+)", ref)

        if not m or not value:
            raise SystemExit("set= 的格式应为 set=D5:3，收到: %s" % token)

        sets.append((m.group(1), int(m.group(2)), value))

    zin = zipfile.ZipFile(src)
    names = zin.namelist()
    sheet_name = "xl/worksheets/sheet1.xml"
    sheet = zin.read(sheet_name).decode("utf-8")

    if re.search(r"<f[ >]", sheet):
        raise SystemExit("表里有公式 —— 左移会改坏相对引用，请改用 Excel 手工删列")
    if re.search(r"<mergeCell|<dataValidation|<hyperlink", sheet):
        raise SystemExit("表里有合并单元格/数据校验/超链接 —— 手工删列更稳")

    out = shift_cols(sheet, deleted)

    for col, row, value in sets:
        out = set_cell(out, col, row, value)

    tmp = src + ".tmp"

    with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED) as zout:
        for n in names:
            if n == sheet_name:
                zout.writestr(n, out.encode("utf-8"))
            else:
                zout.writestr(zin.getinfo(n), zin.read(n))

    zin.close()
    shutil.move(tmp, src)
    print("deleted columns %s; set %d cells: %s" % (deleted, len(sets), src))


if __name__ == "__main__":
    main(sys.argv[1:])
