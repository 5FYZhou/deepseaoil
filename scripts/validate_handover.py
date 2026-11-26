#!/usr/bin/env python3
"""validate_handover.py - v3.0 unified FEED_DROP handover validator."""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path, PurePath

TASK_DIR_PATTERN = re.compile(r"^GD-\d{4}-\d{4}$")
PLAN_FILE_PATTERN = re.compile(r"^PLAN_v(\d{2})\.md$")
EXEC_REPORT_PATTERN = re.compile(r"^EXECUTION_REPORT_A0([1-3])\.md$")
MANIFEST_ENTRY_RE = re.compile(
    r"^\|\s*`?(?P<staged>[^|`]+?)`?\s*\|\s*(?P<layer>L[123])\s*\|"
    r"\s*`?(?P<source>[^|`]+?)`?\s*\|\s*(?P<tokens>\d+)\s*\|$"
)
COMMIT_HEX_RE = re.compile(r"^[0-9a-fA-F]{7,40}$")
COMMIT_TS_RE = re.compile(r"\d{4}-\d{2}-\d{2}([T ]\d{1,2}:\d{2}(:\d{2})?)?")
PLAN_VERSION_FIELD_RE = re.compile(r"Plan\s*Version[:：]?\s*v?(\d{2})", re.IGNORECASE)
TASK_LEVEL_FIELD_RE = re.compile(r"Task\s*Level[:：]?\s*L(\d)", re.IGNORECASE)
CONCLUSION_LINE_RE = re.compile(
    r"^(?:\*\*)?Conclusion(?:\*\*)?[:：][ \t]*(.+?)[ \t]*$", re.MULTILINE
)
ACCEPTANCE_DECISION_LINE_RE = re.compile(
    r"^(?:\*\*)?(?:用户决定|Decision)(?:\*\*)?[:：][ \t]*(.+?)[ \t]*$",
    re.MULTILINE,
)
APPROVAL_DECISION_LINE_RE = re.compile(
    r"^(?:\*\*)?Decision(?:\*\*)?[:：][ \t]*(.+?)[ \t]*$", re.MULTILINE
)

TASK_BRIEF_SECTIONS = [
    "Objective",
    "Background",
    "Current Behavior",
    "Expected Behavior",
    "Non-goals",
    "已知约束",
    "相关模块与文件",
    "已观察到的错误",
    "验收方式",
    "其他说明",
]
PLAN_SECTIONS = [
    "0. Metadata",
    "1. Objective",
    "2. Non-goals",
    "3. Current State",
    "4. Constraints",
    "5. Proposed Design",
    "6. File Scope",
    "7. Execution Steps",
    "8. Engineering Rules",
    "9. Acceptance Criteria",
    "10. Test Plan",
    "11. Risks",
    "12. Rollback",
    "13. Open Questions",
    "14. Approval",
]
EXEC_REPORT_SECTIONS = [
    "1. Preflight",
    "2. Actual Changes",
    "3. Step Results",
    "4. Commands",
    "5. AC Mapping",
    "6. Tests",
    "7. Usage",
    "8. Deviations",
    "9. Remaining Issues",
    "10. Rollback",
    "11. Conclusion",
]
RECON_SECTIONS = [
    "1. Scope",
    "2. Repo Map Check",
    "3. Key Files",
    "4. Interface Facts",
    "5. Risks",
    "6. Context Manifest",
    "7. Evidence",
]

PLAN_ALLOWED_STATUSES_PLANNER = {"DRAFT", "NEED_INVESTIGATION", "IN_REVIEW", "APPROVED"}
PLAN_ALLOWED_STATUSES_WORKER = {"APPROVED"}
CONCLUSION_ENUM = {"PASS", "PASS WITH WARNINGS", "FAIL", "BLOCKED", "NOT RUN"}
ACCEPTANCE_DECISIONS = {"ACCEPT", "ACCEPT WITH FOLLOW-UP", "REJECT"}
APPROVAL_DECISIONS = {"APPROVED", "APPROVED WITH CONDITIONS"}
MODES = ("planner", "worker", "watchdog", "archive")
LEGACY_MODE_HINTS = {
    "exec" + "utor": "worker",
    "veri" + "fier": "watchdog",
}
L0_MINI_REQUIRED_MARKS = ["目标与验收", "文件范围"]

REQUIRED_FEED_FILES = {
    "L1__PROJECT_OVERVIEW.md",
    "L1__CODING_STANDARDS.md",
    "L1__BUILD_AND_TEST.md",
    "L2__ARCHITECTURE.md",
    "L2__MODULE_MAP.md",
}
APPROVAL_FIELD_KEYS = ["Approved Scope", "Base Commit", "Approver", "Approval Time"]


@dataclass
class CheckResult:
    item: str
    status: str
    detail: str = ""

    @property
    def failed(self) -> bool:
        return self.status == "FAIL"


@dataclass
class Report:
    task_dir: str
    mode: str
    l0: bool = False
    checks: list[CheckResult] = field(default_factory=list)

    def add(self, item: str, status: str, detail: str = "") -> None:
        self.checks.append(CheckResult(item=item, status=status, detail=detail))

    @property
    def fails(self) -> list[CheckResult]:
        return [check for check in self.checks if check.failed]

    @property
    def warns(self) -> list[CheckResult]:
        return [check for check in self.checks if check.status == "WARN"]

    @property
    def ok(self) -> bool:
        return not self.fails

    def render(self) -> str:
        tag = " / L0 快速通道" if self.l0 else ""
        lines = [f"交接包校验：{self.task_dir}（模式 {self.mode}{tag}）", "-" * 64]
        for check in self.checks:
            mark = {"PASS": "[PASS]", "FAIL": "[FAIL]", "WARN": "[WARN]"}[check.status]
            line = f"{mark} {check.item}"
            if check.detail:
                line += f" - {check.detail}"
            lines.append(line)
        lines.extend(
            [
                "-" * 64,
                f"结果：PASS {len(self.checks) - len(self.fails) - len(self.warns)}"
                f" / FAIL {len(self.fails)} / WARN {len(self.warns)}",
                "结论：通过" if self.ok else "结论：未通过（先修复 FAIL 再交接）",
            ]
        )
        return "\n".join(lines)

    def to_json(self) -> str:
        return json.dumps(
            {
                "task_dir": self.task_dir,
                "mode": self.mode,
                "l0": self.l0,
                "ok": self.ok,
                "checks": [
                    {"item": check.item, "status": check.status, "detail": check.detail}
                    for check in self.checks
                ],
            },
            ensure_ascii=False,
            indent=2,
        )


def _read(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return path.read_text(encoding="utf-8", errors="replace")


def _has_heading(text: str, heading: str) -> bool:
    tokens = [re.escape(token) for token in heading.split()]
    pattern = re.compile(
        r"^#{1,4}\s*" + r"\s+".join(tokens) + r"\s*$",
        re.MULTILINE | re.IGNORECASE,
    )
    return bool(pattern.search(text))


def _find_latest(directory: Path, pattern: re.Pattern[str]) -> tuple[Path | None, int]:
    found = [path for path in directory.iterdir() if path.is_file() and pattern.match(path.name)]
    if not found:
        return None, 0
    latest = max(found, key=lambda path: int(pattern.match(path.name).group(1)))  # type: ignore[union-attr]
    return latest, len(found)


def _find_all_plans(directory: Path) -> list[Path]:
    return sorted(
        (
            path
            for path in directory.iterdir()
            if path.is_file() and PLAN_FILE_PATTERN.match(path.name)
        ),
        key=lambda path: (
            int(PLAN_FILE_PATTERN.match(path.name).group(1)),  # type: ignore[union-attr]
            path.name,
        ),
    )


def _latest_detail(name: str, count: int) -> str:
    return f"已选最新（共 {count} 个版本）：{name}" if count > 1 else name


def _normalize_value(value: str) -> str:
    normalized = value.strip().strip("`").strip("*").strip()
    return re.sub(r"\s+", " ", normalized)


def _is_placeholder_value(value: str) -> bool:
    normalized = _normalize_value(value)
    if not normalized:
        return True
    if re.fullmatch(r"<[^>\n]+>", normalized):
        return True
    if normalized.casefold() in {"tbd", "todo", "待填写", "待填"}:
        return True
    return False


def _field_block_is_placeholder(block: str) -> bool:
    if _is_placeholder_value(block):
        return True
    for line in block.splitlines():
        value = re.sub(r"^\s*[-*]\s*", "", line).strip()
        if not value:
            continue
        if _is_placeholder_value(value):
            return True
        if re.fullmatch(
            r"(?:READ|CREATE|MODIFY|DELETE|RENAME)\s+<[^>]+>",
            value,
            re.IGNORECASE,
        ):
            return True
    return False


def _is_commit_legal(value: str) -> bool:
    normalized = _normalize_value(value)
    if COMMIT_HEX_RE.fullmatch(normalized):
        return True
    if "可恢复副本" in normalized:
        return True
    return bool(COMMIT_TS_RE.search(normalized))


def _field_value(text: str, key: str) -> str | None:
    match = re.search(
        rf"^[^\n]*?{re.escape(key)}[:：][ \t]*(.+?)[ \t]*$",
        text,
        re.MULTILINE,
    )
    return match.group(1) if match else None


def _approval_field_block(text: str, key: str) -> str | None:
    match = re.search(
        rf"^[^\n]*?{re.escape(key)}[:：][ \t]*([^\n]*)$",
        text,
        re.MULTILINE,
    )
    if match is None:
        return None
    lines = [match.group(1)]
    for line in text[match.end() :].splitlines():
        if not line.strip():
            continue
        if re.match(r"^[A-Za-z][A-Za-z /_]+[:：]", line):
            break
        lines.append(line)
    block = "\n".join(lines).strip()
    return block or None


def _approval_task_id_value(text: str) -> str | None:
    match = re.search(
        r"^[^\n]*?Task ID(?:\s*/\s*Approved Plan)?[:：][ \t]*(.+?)[ \t]*$",
        text,
        re.MULTILINE,
    )
    return match.group(1) if match else None


def check_task_brief(directory: Path, report: Report, l0: bool = False) -> None:
    path = directory / "TASK_BRIEF.md"
    if not path.is_file():
        report.add("1. TASK_BRIEF.md", "FAIL", "缺少用户任务简报")
        return
    text = _read(path)
    missing = [section for section in TASK_BRIEF_SECTIONS if section not in text]
    if not missing:
        report.add("1. TASK_BRIEF.md", "PASS", "10 段结构齐全")
        return
    if l0 and _check_l0_mini_brief(text):
        report.add("1. TASK_BRIEF.md", "PASS", "L0 三行 mini 任务书要素齐全")
        return
    if l0:
        report.add(
            "1. TASK_BRIEF.md",
            "FAIL",
            "须为标准 10 段，或含 L0、目标与验收、文件范围的 mini 简报",
        )
        return
    report.add("1. TASK_BRIEF.md", "FAIL", f"缺少 {len(missing)} 段：{'、'.join(missing)}")


def _check_l0_mini_brief(text: str) -> bool:
    if not re.search(r"\bL0\b", text):
        return False
    if any(mark not in text for mark in L0_MINI_REQUIRED_MARKS):
        return False
    for line in text.splitlines():
        if any(mark in line for mark in L0_MINI_REQUIRED_MARKS):
            value = re.split(r"[:：]", line, maxsplit=1)[-1].strip()
            if _is_placeholder_value(value):
                return False
    return True


def _safe_manifest_mapping(staged: str, source: str) -> str | None:
    path = PurePath(staged)
    if path.is_absolute() or path.name != staged or ".." in path.parts:
        return f"暂存名越界：{staged}"
    if source == "git:recent":
        return None
    source_path = PurePath(source)
    if source_path.is_absolute() or ".." in source_path.parts:
        return f"原路径越界：{source}"
    return None


def check_context_manifest(directory: Path, report: Report, task_id: str) -> None:
    manifest = directory / "00_CONTEXT_MANIFEST.md"
    if not manifest.is_file():
        report.add(
            "2. CONTEXT MANIFEST + FEED_DROP",
            "FAIL",
            "缺少 00_CONTEXT_MANIFEST.md",
        )
        return
    text = _read(manifest)

    manifest_task_id = _field_value(text, "Task ID")
    if manifest_task_id is None or TASK_DIR_PATTERN.fullmatch(task_id) is None:
        report.add("2b. Manifest Task ID", "FAIL", f"无法核对 Task ID：{task_id}")
    elif manifest_task_id.strip() == task_id:
        report.add("2b. Manifest Task ID", "PASS", task_id)
    else:
        report.add(
            "2b. Manifest Task ID",
            "FAIL",
            f"Manifest 为 {manifest_task_id.strip()}，目录为 {task_id}",
        )

    commit = _field_value(text, "Base Commit")
    if commit is None:
        report.add("2a. Manifest 基线", "FAIL", "缺少 Base Commit 字段")
    elif _is_placeholder_value(commit):
        report.add("2a. Manifest 基线", "FAIL", f"Base Commit 仍为占位符：{commit}")
    elif _is_commit_legal(commit):
        report.add("2a. Manifest 基线", "PASS", _normalize_value(commit))
    else:
        report.add("2a. Manifest 基线", "FAIL", f"Base Commit 非法：{commit}")

    feed = directory / "FEED_DROP"
    feed_files = (
        {path.name for path in feed.iterdir() if path.is_file()} if feed.is_dir() else set()
    )
    if not feed_files:
        report.add("2. CONTEXT MANIFEST + FEED_DROP", "FAIL", "FEED_DROP 不存在或为空")
    else:
        report.add(
            "2. CONTEXT MANIFEST + FEED_DROP",
            "PASS",
            f"Manifest 存在，FEED_DROP 含 {len(feed_files)} 个文件",
        )

    mappings: list[tuple[str, str]] = []
    for line in text.splitlines():
        match = MANIFEST_ENTRY_RE.match(line)
        if match:
            mappings.append((match.group("staged").strip(), match.group("source").strip()))
    if not mappings:
        report.add("2c. 文件映射表", "FAIL", "Manifest 未包含可解析的文件映射表")
        return

    mapping_errors = [
        error
        for staged, source in mappings
        if (error := _safe_manifest_mapping(staged, source)) is not None
    ]
    if mapping_errors:
        report.add("2c. 文件映射表", "FAIL", "；".join(mapping_errors))
    else:
        report.add("2c. 文件映射表", "PASS", f"{len(mappings)} 条映射路径合法")

    mapped_names = {staged for staged, _ in mappings}
    missing_required = sorted(REQUIRED_FEED_FILES - mapped_names)
    missing_files = sorted(mapped_names - feed_files)
    missing_required_files = sorted(REQUIRED_FEED_FILES - feed_files)
    if missing_required or missing_files or missing_required_files:
        details: list[str] = []
        if missing_required:
            details.append(f"映射表缺必需项：{', '.join(missing_required)}")
        if missing_files:
            details.append(f"映射文件实体缺失：{', '.join(missing_files)}")
        if missing_required_files:
            details.append(f"FEED_DROP 缺必需文件：{', '.join(missing_required_files)}")
        report.add("2d. 映射实体", "FAIL", "；".join(details))
    else:
        report.add("2d. 映射实体", "PASS", "所有映射与必需文件均存在")

    extras = sorted(feed_files - mapped_names)
    if extras:
        report.add("2e. 额外文件", "WARN", f"未列入清单：{', '.join(extras)}")
    else:
        report.add("2e. 额外文件", "PASS", "无未映射文件")


def check_plan(directory: Path, report: Report, mode: str, plan_ver: str | None) -> None:
    path, count = _find_latest(directory, PLAN_FILE_PATTERN)
    if path is None:
        report.add("4. PLAN_vNN.md", "FAIL", "缺少任务书草案（0.Metadata + 14 章）")
        return
    text = _read(path)
    missing = [section for section in PLAN_SECTIONS if not _has_heading(text, section)]
    if missing:
        report.add("4. PLAN_vNN.md", "FAIL", f"缺少章节：{'; '.join(missing)}")
    else:
        report.add(
            "4. PLAN_vNN.md",
            "PASS",
            _latest_detail(f"{path.name}，0.Metadata + 14 章齐全", count),
        )

    status_value = _field_value(text, "Status")
    status = _normalize_value(status_value or "").upper()
    allowed = (
        PLAN_ALLOWED_STATUSES_WORKER
        if mode in {"worker", "watchdog", "archive"}
        else PLAN_ALLOWED_STATUSES_PLANNER
    )
    if status in allowed:
        report.add("4a. PLAN 状态", "PASS", f"Status: {status}")
    else:
        report.add(
            "4a. PLAN 状态",
            "FAIL",
            f"Status: {status or '缺失'}，允许 {'/'.join(sorted(allowed))}",
        )

    task_id = directory.name
    if TASK_DIR_PATTERN.fullmatch(task_id) is None:
        report.add("4b. Task ID 一致", "WARN", f"目录名 {task_id} 不符合 GD-YYYY-NNNN")
    elif re.search(rf"\b{re.escape(task_id)}\b", text):
        report.add("4b. Task ID 一致", "PASS", task_id)
    else:
        report.add("4b. Task ID 一致", "FAIL", f"任务书未引用 {task_id}")

    field_version = PLAN_VERSION_FIELD_RE.search(text)
    if plan_ver is None:
        report.add("4c. 版本一致", "WARN", "无法从文件名解析版本号")
    elif field_version is None:
        report.add("4c. 版本一致", "FAIL", "正文缺少 Plan Version 字段")
    elif field_version.group(1) != plan_ver:
        report.add(
            "4c. 版本一致",
            "FAIL",
            f"文件名 v{plan_ver} 与正文 v{field_version.group(1)} 不一致",
        )
    else:
        report.add("4c. 版本一致", "PASS", f"文件名与正文均为 v{plan_ver}")


def check_plan_l0(directory: Path, report: Report) -> None:
    plans = _find_all_plans(directory)
    if not plans:
        return
    invalid: list[str] = []
    missing_level: list[str] = []
    for path in plans:
        text = _read(path)
        level_match = TASK_LEVEL_FIELD_RE.search(text)
        if level_match is None:
            missing_level.append(path.name)
        elif int(level_match.group(1)) != 0:
            invalid.append(f"{path.name}=L{level_match.group(1)}")
    if missing_level or invalid:
        details: list[str] = []
        if invalid:
            details.append(f"存在非 L0 计划：{', '.join(invalid)}")
        if missing_level:
            details.append(f"无法确认级别：{', '.join(missing_level)}")
        report.add("4d. L0 级别核验", "FAIL", "；".join(details))
    else:
        report.add("4d. L0 级别核验", "PASS", f"全部 {len(plans)} 个 PLAN 均为 L0")


def check_approval_record(
    directory: Path,
    report: Report,
    task_id: str,
    plan_ver: str | None,
) -> None:
    path = directory / "APPROVAL_RECORD.md"
    if not path.is_file():
        report.add("5. APPROVAL_RECORD.md", "FAIL", "缺少批准记录")
        return
    text = _read(path)
    decision_match = APPROVAL_DECISION_LINE_RE.search(text)
    if decision_match is None:
        report.add("5. APPROVAL_RECORD.md", "FAIL", "未找到整行 Decision 字段")
    else:
        raw = _normalize_value(decision_match.group(1))
        normalized = re.sub(r"\s+", " ", raw.replace("_", " ")).strip().upper()
        if "/" in raw:
            report.add(
                "5. APPROVAL_RECORD.md",
                "FAIL",
                f"Decision 含未消除选项：{raw}",
            )
        elif normalized in APPROVAL_DECISIONS:
            report.add("5. APPROVAL_RECORD.md", "PASS", f"Decision: {raw}")
        else:
            report.add(
                "5. APPROVAL_RECORD.md",
                "FAIL",
                "Decision 必须为 APPROVED / APPROVED_WITH_CONDITIONS / APPROVED WITH CONDITIONS",
            )

    missing: list[str] = []
    placeholders: list[str] = []
    invalid_commit: list[str] = []
    for key in APPROVAL_FIELD_KEYS:
        block = _approval_field_block(text, key)
        if block is None:
            missing.append(key)
            continue
        if _field_block_is_placeholder(block):
            placeholders.append(key)
        if key == "Base Commit":
            values = re.split(r"\s+/\s+", block.strip())
            commit_value = values[-1]
            if not _is_commit_legal(commit_value):
                invalid_commit.append(commit_value)
    if missing:
        report.add("5a. 批准记录字段", "FAIL", f"缺少：{', '.join(missing)}")
    elif placeholders:
        report.add("5a. 批准记录字段", "FAIL", f"仍为占位符：{', '.join(placeholders)}")
    elif invalid_commit:
        report.add("5a. 批准记录字段", "FAIL", f"Base Commit 非法：{invalid_commit[0]}")
    else:
        report.add("5a. 批准记录字段", "PASS", "Scope / Base Commit / Approver / Time 齐全")

    approval_task_id = _approval_task_id_value(text)
    id_ok = (
        approval_task_id is not None
        and re.search(rf"\b{re.escape(task_id)}\b", approval_task_id) is not None
    )
    ver_ok = plan_ver is not None and re.search(rf"\bv{plan_ver}\b", text)
    if id_ok and ver_ok:
        report.add("5b. 批准对象一致", "PASS", f"{task_id} / v{plan_ver}")
    else:
        details = []
        if not id_ok:
            details.append(f"Task ID 不是 {task_id}")
        if not ver_ok:
            details.append(f"未锁定 Plan Version v{plan_ver or 'NN'}")
        report.add("5b. 批准对象一致", "FAIL", "；".join(details))


def check_execution_report(
    directory: Path,
    report: Report,
    plan_ver: str | None,
    l0: bool = False,
) -> None:
    path, count = _find_latest(directory, EXEC_REPORT_PATTERN)
    if path is None:
        report.add("6. EXECUTION_REPORT_Axx.md", "FAIL", "缺少 A01-A03 执行报告")
        return
    text = _read(path)
    missing = [section for section in EXEC_REPORT_SECTIONS if not _has_heading(text, section)]
    if missing:
        report.add(
            "6. EXECUTION_REPORT_Axx.md",
            "FAIL",
            f"缺少章节：{'; '.join(missing)}",
        )
    else:
        report.add(
            "6. EXECUTION_REPORT_Axx.md",
            "PASS",
            _latest_detail(f"{path.name}，11 节齐全", count),
        )

    conclusion_match = CONCLUSION_LINE_RE.search(text)
    if conclusion_match is None:
        report.add("6a. 报告结论", "FAIL", "未找到整行 Conclusion")
    else:
        conclusion = _normalize_value(conclusion_match.group(1))
        if "/" in conclusion or _is_placeholder_value(conclusion):
            report.add("6a. 报告结论", "FAIL", f"结论仍有占位或选项：{conclusion}")
        elif conclusion in CONCLUSION_ENUM:
            report.add("6a. 报告结论", "PASS", f"Conclusion: {conclusion}")
        else:
            report.add(
                "6a. 报告结论",
                "FAIL",
                "Conclusion 必须为 PASS / PASS WITH WARNINGS / FAIL / BLOCKED / NOT RUN",
            )

    task_value = _field_value(text, "Task ID")
    if task_value is not None and directory.name in task_value:
        report.add("6b. Task ID 一致", "PASS", directory.name)
    else:
        report.add("6b. Task ID 一致", "FAIL", f"报告未引用目录 Task ID {directory.name}")

    if plan_ver is None and l0:
        report.add("6c. 版本引用一致", "PASS", "L0 快速通道无 PLAN，跳过版本引用")
    elif plan_ver is None:
        report.add("6c. 版本引用一致", "FAIL", "目录内无 PLAN_vNN.md")
    elif re.search(rf"\bv{plan_ver}\b", text):
        report.add("6c. 版本引用一致", "PASS", f"引用批准版本 v{plan_ver}")
    else:
        report.add("6c. 版本引用一致", "FAIL", f"未引用批准版本 v{plan_ver}")


def check_acceptance(directory: Path, report: Report) -> None:
    path = directory / "ACCEPTANCE.md"
    if not path.is_file():
        report.add("7. ACCEPTANCE.md", "FAIL", "缺少 CEO 验收记录")
        return
    text = _read(path)
    decision_match = ACCEPTANCE_DECISION_LINE_RE.search(text)
    if decision_match is None:
        report.add("7. ACCEPTANCE.md", "FAIL", "未找到整行用户决定/Decision")
    else:
        decision = _normalize_value(decision_match.group(1))
        if "/" in decision or _is_placeholder_value(decision):
            report.add("7. ACCEPTANCE.md", "FAIL", f"决定仍有占位或选项：{decision}")
        elif decision in ACCEPTANCE_DECISIONS:
            report.add("7. ACCEPTANCE.md", "PASS", f"用户决定：{decision}")
        else:
            report.add(
                "7. ACCEPTANCE.md",
                "FAIL",
                "必须为 ACCEPT / ACCEPT WITH FOLLOW-UP / REJECT",
            )
    if "AC-" in text:
        report.add("7a. 逐条验收", "PASS", "含 AC 条目映射")
    else:
        report.add("7a. 逐条验收", "WARN", "未检测到 AC-xx")


def check_recon_report(directory: Path, report: Report) -> None:
    path = directory / "RECON_REPORT.md"
    if not path.is_file():
        report.add("3. RECON_REPORT.md", "PASS", "未触发只读调查")
        return
    text = _read(path)
    missing = [section for section in RECON_SECTIONS if not _has_heading(text, section)]
    if missing:
        report.add("3. RECON_REPORT.md", "FAIL", f"缺少章节：{'; '.join(missing)}")
    else:
        report.add("3. RECON_REPORT.md", "PASS", "7 节齐全")


def validate(task_dir: Path, mode: str, l0: bool = False) -> Report:
    report = Report(task_dir=str(task_dir), mode=mode, l0=l0)
    if mode not in MODES:
        report.add("校验模式", "FAIL", f"v3 仅支持 {'/'.join(MODES)}")
        return report
    if not task_dir.is_dir():
        report.add("任务目录", "FAIL", f"{task_dir} 不存在")
        return report
    if TASK_DIR_PATTERN.fullmatch(task_dir.name):
        report.add("任务目录命名", "PASS", task_dir.name)
    else:
        report.add("任务目录命名", "FAIL", f"{task_dir.name} 不符合 GD-YYYY-NNNN")

    plan_path, _ = _find_latest(task_dir, PLAN_FILE_PATTERN)
    plan_ver = (
        PLAN_FILE_PATTERN.match(plan_path.name).group(1)  # type: ignore[union-attr]
        if plan_path is not None
        else None
    )

    if l0:
        check_task_brief(task_dir, report, l0=True)
        check_plan_l0(task_dir, report)
        approval = task_dir / "APPROVAL_RECORD.md"
        if approval.is_file():
            check_approval_record(task_dir, report, task_dir.name, plan_ver)
        if mode in {"watchdog", "archive"}:
            check_execution_report(task_dir, report, plan_ver, l0=True)
        if mode == "archive":
            check_acceptance(task_dir, report)
        return report

    check_task_brief(task_dir, report)
    check_context_manifest(task_dir, report, task_dir.name)
    check_recon_report(task_dir, report)
    check_plan(task_dir, report, mode, plan_ver)
    if mode in {"worker", "watchdog", "archive"}:
        check_approval_record(task_dir, report, task_dir.name, plan_ver)
    if mode in {"watchdog", "archive"}:
        check_execution_report(task_dir, report, plan_ver)
    if mode == "archive":
        check_acceptance(task_dir, report)
    return report


def list_required(mode: str, l0: bool = False) -> str:
    if l0:
        mode_items = {
            "planner": ["TASK_BRIEF.md（L0 mini 即可）"],
            "worker": ["TASK_BRIEF.md（L0 mini 即可）"],
            "watchdog": ["TASK_BRIEF.md", "EXECUTION_REPORT_A0x.md"],
            "archive": ["TASK_BRIEF.md", "EXECUTION_REPORT_A0x.md", "ACCEPTANCE.md"],
        }
        lines = [
            f"模式 {mode}（v3 L0）：PLAN/APPROVAL_RECORD/FEED_DROP 可省略；"
            "若存在 PLAN，任意非 L0 计划均失败。"
        ]
        lines.extend(f"  - {item}" for item in mode_items[mode])
        return "\n".join(lines)
    common = [
        "TASK_BRIEF.md",
        "00_CONTEXT_MANIFEST.md",
        "FEED_DROP/（非空且映射实体存在）",
        "PLAN_vNN.md",
    ]
    mode_items = {
        "planner": common,
        "worker": common + ["APPROVAL_RECORD.md（PLAN 必须 APPROVED）"],
        "watchdog": common
        + ["APPROVAL_RECORD.md（PLAN 必须 APPROVED）", "EXECUTION_REPORT_A0x.md"],
        "archive": common
        + [
            "APPROVAL_RECORD.md（PLAN 必须 APPROVED）",
            "EXECUTION_REPORT_A0x.md",
            "ACCEPTANCE.md",
        ],
    }
    lines = [f"模式 {mode} 必查件（目录 ^GD-\\d{{4}}-\\d{{4}}$；RECON_REPORT.md 按需）："]
    lines.extend(f"  - {item}" for item in mode_items[mode])
    return "\n".join(lines)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="agent-workflow-kit v3 交接包结构校验器")
    parser.add_argument("task_dir", nargs="?", help="任务目录，如 AI_Context/TASKS/GD-2026-0001")
    parser.add_argument(
        "--for",
        dest="mode",
        default="worker",
        metavar="MODE",
        help="planner|worker|watchdog|archive（默认 worker）",
    )
    parser.add_argument("--l0", action="store_true", help="L0 CEO 快速通道")
    parser.add_argument("--json", action="store_true", help="输出机器可读 JSON")
    parser.add_argument("--list", action="store_true", help="列出当前模式必查件后退出")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if args.mode not in MODES:
        hint = LEGACY_MODE_HINTS.get(args.mode)
        migration = f"；请改用 --for {hint}" if hint else ""
        parser.error(
            f"v3 模式仅支持 {'/'.join(MODES)}，收到 {args.mode}{migration}"
        )
    if args.list:
        print(list_required(args.mode, args.l0))
        return 0
    if not args.task_dir:
        parser.error("请提供任务目录，或使用 --list")
    report = validate(Path(args.task_dir), args.mode, args.l0)
    print(report.to_json() if args.json else report.render())
    return 0 if report.ok else 1


if __name__ == "__main__":
    sys.exit(main())
