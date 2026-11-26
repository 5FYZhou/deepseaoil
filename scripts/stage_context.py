#!/usr/bin/env python3
"""Stage real project files into a deterministic FEED_DROP context bundle."""

from __future__ import annotations

import argparse
import json
import math
import os
import re
import shutil
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

TASK_DIR_PATTERN = re.compile(r"^GD-\d{4}-\d{4}$")
MANIFEST_ENTRY_RE = re.compile(
    r"^\|\s*`(?P<staged>[^`]+)`\s*\|\s*(?P<layer>L[123])\s*\|"
    r"\s*`(?P<source>[^`]+)`\s*\|\s*(?P<tokens>\d+)\s*\|$"
)
CHAT_SAFE_SUFFIXES = {
    ".c",
    ".cpp",
    ".cs",
    ".h",
    ".hpp",
    ".json",
    ".md",
    ".py",
    ".toml",
    ".txt",
    ".yaml",
    ".yml",
}
TREE_EXCLUDED_NAMES = {
    ".git",
    ".mypy_cache",
    ".pytest_cache",
    ".ruff_cache",
    ".venv",
    "__pycache__",
    "FEED_DROP",
    "Library",
    "Logs",
    "Temp",
    "logs",
    "tmp",
}


class StageError(RuntimeError):
    """A user-actionable staging failure."""


@dataclass(frozen=True, slots=True)
class AutoFile:
    source: str
    staged: str
    layer: str
    required: bool


@dataclass(frozen=True, slots=True)
class StagedFile:
    staged: str
    layer: str
    source: str
    source_path: Path
    tokens: int

    def to_json(self) -> dict[str, str | int]:
        return {
            "staged": self.staged,
            "layer": self.layer,
            "source": self.source,
            "tokens": self.tokens,
        }


AUTO_FILES = (
    AutoFile("AI_Context/PROJECT_OVERVIEW.md", "L1__PROJECT_OVERVIEW.md", "L1", True),
    AutoFile("AI_Context/CODING_STANDARDS.md", "L1__CODING_STANDARDS.md", "L1", True),
    AutoFile("AI_Context/BUILD_AND_TEST.md", "L1__BUILD_AND_TEST.md", "L1", True),
    AutoFile("AI_Context/PROJECT_BOARD.md", "L2__PROJECT_BOARD.md", "L2", False),
    AutoFile("AI_Context/ARCHITECTURE.md", "L2__ARCHITECTURE.md", "L2", True),
    AutoFile("AI_Context/MODULE_MAP.md", "L2__MODULE_MAP.md", "L2", True),
    AutoFile("AI_Context/DEPENDENCIES.md", "L2__DEPENDENCIES.md", "L2", False),
)


def _now() -> str:
    return datetime.now().astimezone().isoformat(timespec="seconds")


def _find_project_root(task_dir: Path) -> Path:
    task_dir = task_dir.resolve()
    for candidate in (task_dir, *task_dir.parents):
        if (candidate / "AI_Context").is_dir() or (candidate / ".git").exists():
            return candidate
    raise StageError("无法从任务目录向上定位项目根（须存在 AI_Context/ 或 .git）")


def _validate_task_dir(task_dir: Path, root: Path) -> Path:
    resolved = task_dir.resolve()
    tasks_root = (root / "AI_Context" / "TASKS").resolve()
    try:
        relative = resolved.relative_to(tasks_root)
    except ValueError as exc:
        raise StageError(f"任务目录必须位于 {root / 'AI_Context' / 'TASKS'} 内") from exc
    if len(relative.parts) != 1 or not TASK_DIR_PATTERN.fullmatch(resolved.name):
        raise StageError("任务目录必须直接位于 AI_Context/TASKS/ 下，并命名为 GD-YYYY-NNNN")
    return resolved


def _is_sensitive(relative: Path) -> bool:
    folded_parts = [part.casefold() for part in relative.parts]
    name = folded_parts[-1]
    if name == ".env.example":
        return False
    if name == ".env" or name.startswith(".env."):
        return True
    if relative.suffix.casefold() in {".key", ".pem"}:
        return True
    return any(
        "secret" in part.replace("secretary", "") or "credential" in part
        for part in folded_parts
    )


def _resolve_source(root: Path, raw: str) -> tuple[Path, str]:
    raw_path = Path(raw)
    if ".." in raw_path.parts:
        raise StageError(f"拒绝包含 '..' 的路径：{raw}")
    candidate = raw_path if raw_path.is_absolute() else root / raw_path
    try:
        resolved = candidate.resolve(strict=True)
    except FileNotFoundError as exc:
        raise StageError(f"源文件不存在：{raw}") from exc
    try:
        relative = resolved.relative_to(root)
    except ValueError as exc:
        raise StageError(f"拒绝仓库外路径：{raw}") from exc
    if _is_sensitive(relative):
        raise StageError(f"拒绝敏感文件：{relative.as_posix()}")
    if not resolved.is_file():
        raise StageError(f"源路径不是普通文件：{relative.as_posix()}")
    return resolved, relative.as_posix()


def _manual_staged_name(source: str, chat_safe: bool) -> str:
    flattened = source.replace("\\", "_").replace("/", "_")
    staged = f"L3__{flattened}"
    if chat_safe and Path(source).suffix.casefold() not in CHAT_SAFE_SUFFIXES:
        staged += ".txt"
    return staged


def _estimate_tokens_text(text: str) -> int:
    cjk = 0
    other = 0
    for char in text:
        codepoint = ord(char)
        if (
            0x3400 <= codepoint <= 0x4DBF
            or 0x4E00 <= codepoint <= 0x9FFF
            or 0xF900 <= codepoint <= 0xFAFF
            or 0x3040 <= codepoint <= 0x30FF
            or 0xAC00 <= codepoint <= 0xD7AF
        ):
            cjk += 1
        elif not char.isspace():
            other += 1
    return cjk + math.ceil(other / 4)


def _estimate_tokens(path: Path) -> int:
    return _estimate_tokens_text(path.read_text(encoding="utf-8", errors="replace"))


def _read_source_list(path_argument: str) -> list[str]:
    path = Path(path_argument).resolve()
    if not path.is_file():
        raise StageError(f"文件清单不存在：{path_argument}")
    entries: list[str] = []
    for line in path.read_text(encoding="utf-8").splitlines():
        value = line.strip()
        if value and not value.startswith("#"):
            entries.append(value)
    return entries


def _manifest_sources(manifest: Path) -> list[str]:
    text = manifest.read_text(encoding="utf-8")
    sources: list[str] = []
    for line in text.splitlines():
        match = MANIFEST_ENTRY_RE.match(line)
        if match:
            sources.append(match.group("source"))
    if not sources:
        raise StageError(f"--append 无法从现有清单解析任何文件映射：{manifest}")
    return sources


def _run_git(root: Path, *args: str) -> subprocess.CompletedProcess[str]:
    environment = os.environ.copy()
    environment["GIT_OPTIONAL_LOCKS"] = "0"
    return subprocess.run(
        ["git", "-C", str(root), *args],
        text=True,
        capture_output=True,
        check=False,
        env=environment,
    )


def _base_commit(root: Path, generated_at: str) -> tuple[str, list[str]]:
    warnings: list[str] = []
    if not (root / ".git").exists():
        return (
            f"{generated_at}（非 Git 工作区，可恢复副本时间）",
            ["非 Git 工作区，上下文基线与近期历史不可用。"],
        )
    result = _run_git(root, "rev-parse", "HEAD")
    commit = result.stdout.strip()
    if result.returncode == 0 and commit:
        return commit, warnings
    warnings.append("Git 仓库尚无可用 HEAD，已改用生成时间作为恢复基线。")
    return f"{generated_at}（Git 仓库无提交，可恢复副本时间）", warnings


def _git_recent_context(root: Path, limit: int) -> tuple[str, list[str]]:
    if limit == 0:
        return "", []
    warnings: list[str] = []
    if not (root / ".git").exists():
        return "非 Git 工作区：已跳过近期 Git 上下文。\n", warnings
    head = _run_git(root, "rev-parse", "--verify", "HEAD")
    if head.returncode != 0:
        return "Git 仓库尚无提交：已跳过近期 Git 上下文。\n", warnings
    log = _run_git(
        root,
        "log",
        f"-n{limit}",
        "--stat",
        "--decorate",
        "--date=iso-strict",
        "--format=commit %H%nAuthorDate: %ad%nSubject: %s",
    )
    if log.returncode != 0:
        warnings.append(f"读取近期 Git 历史失败：{log.stderr.strip()}")
        return "近期 Git 上下文读取失败，详见清单警告。\n", warnings
    status = _run_git(root, "status", "--short")
    parts = [log.stdout.rstrip(), ""]
    parts.append("工作区状态（git status --short）：")
    parts.append(status.stdout.rstrip() if status.returncode == 0 and status.stdout.strip() else "(clean)")
    return "\n".join(parts).rstrip() + "\n", warnings


def _project_tree(root: Path) -> tuple[list[str], bool]:
    entries: list[str] = []
    truncated = False
    for current, directories, files in os.walk(root):
        current_path = Path(current)
        relative = current_path.relative_to(root)
        depth = len(relative.parts)
        directories[:] = sorted(
            name
            for name in directories
            if name not in TREE_EXCLUDED_NAMES
            and not name.startswith(".")
            and not _is_sensitive(Path(name))
        )
        if depth >= 3:
            directories.clear()
        for directory in directories:
            entries.append(f"{(relative / directory).as_posix()}/")
            if len(entries) >= 200:
                return sorted(entries), True
        if depth < 3:
            for file_name in sorted(files):
                if (
                    file_name in TREE_EXCLUDED_NAMES
                    or file_name == "00_CONTEXT_MANIFEST.md"
                    or _is_sensitive(Path(file_name))
                ):
                    continue
                entries.append((relative / file_name).as_posix())
                if len(entries) >= 200:
                    return sorted(entries), True
    return sorted(entries), truncated


def _manifest_text(
    *,
    task_id: str,
    root: Path,
    generated_at: str,
    base_commit: str,
    chat_safe: bool,
    mode: str,
    files: list[StagedFile],
    totals: dict[str, int],
    budget_limit: int,
    warnings: list[str],
) -> str:
    tree, truncated = _project_tree(root)
    l1_status = (
        "位于 4,096–8,000 隐式缓存甜点区"
        if 4096 <= totals["L1"] <= 8000
        else "不在 4,096–8,000 隐式缓存甜点区"
    )
    lines = [
        f"# 00 CONTEXT MANIFEST — {task_id}",
        "",
        f"- Task ID: {task_id}",
        f"- Base Commit: {base_commit}",
        f"- Generated At: {generated_at}",
        f"- Generation Mode: {mode}",
        f"- Chat Safe: {'ON' if chat_safe else 'OFF'}",
        f"- Budget Limit: {budget_limit}",
        "",
        "## Token Budget",
        "",
        "| 层级 | 估算 Token | 预算/状态 |",
        "| --- | ---: | --- |",
        f"| L1 | {totals['L1']} | {l1_status} |",
        f"| L2 | {totals['L2']} | 模块层建议 ≤ 30,000 |",
        f"| L3 | {totals['L3']} | 代码层建议 ≤ 100,000 |",
        f"| Total | {totals['Total']} | {'超过' if totals['Total'] > budget_limit else '未超过'} {budget_limit} |",
        "",
        "> Token 为启发式估算：每个 CJK 字符计 1，其余非空白字符合计 ÷ 4 后向上取整。",
        "",
        "## File Mapping",
        "",
        "| 暂存名 | 层级 | 原路径 | 估算 Token |",
        "| --- | --- | --- | ---: |",
    ]
    for item in files:
        lines.append(
            f"| `{item.staged}` | {item.layer} | `{item.source}` | {item.tokens} |"
        )
    lines.extend(
        [
            "",
            "## Project Tree Summary",
            "",
            "```text",
            *tree,
            "```",
            "",
            "> 目录摘要最多深度 3、最多 200 项；达到上限时尾部内容被截断。"
            if truncated
            else "> 目录摘要已按深度 3、最多 200 项过滤生成。",
            "",
            "## Explicit Exclusions",
            "",
            "- `.git/`、`.venv/`、各类缓存、`Library/`、`Temp/`、`Logs/`、`logs/`、`tmp/`",
            "- 所有 `FEED_DROP/` 物理投喂箱及本 Manifest 自身",
            "- 未通过 `--files` / `--from-list` 选择的业务文件",
            "- `.env`、`.env.*`（`.env.example` 除外）、`*.key`、`*.pem` 与名称含 secret/credential 的文件",
        ]
    )
    if warnings:
        lines.extend(["", "## Warnings", ""])
        lines.extend(f"- {warning}" for warning in warnings)
    return "\n".join(lines).rstrip() + "\n"


def stage(
    task_dir_arg: str,
    *,
    files: list[str],
    from_list: str | None,
    append: bool,
    chat_safe: bool,
    git_diff: int,
    budget_limit: int,
) -> tuple[dict[str, object], list[str]]:
    task_input = Path(task_dir_arg)
    root = _find_project_root(task_input)
    task_dir = _validate_task_dir(task_input, root)
    task_dir.mkdir(parents=True, exist_ok=True)
    feed_drop = task_dir / "FEED_DROP"
    manifest_path = task_dir / "00_CONTEXT_MANIFEST.md"

    selected = list(files)
    if from_list:
        selected.extend(_read_source_list(from_list))
    if append:
        if not manifest_path.is_file():
            raise StageError("--append 要求现有的 00_CONTEXT_MANIFEST.md")
        selected.extend(
            source
            for source in _manifest_sources(manifest_path)
            if source != "git:recent"
        )

    warnings: list[str] = []
    generated_at = _now()
    base_commit, base_warnings = _base_commit(root, generated_at)
    warnings.extend(base_warnings)

    staged_specs: list[tuple[str, str, Path]] = []
    seen_sources: set[str] = set()
    for auto in AUTO_FILES:
        source_path = root / auto.source
        if not source_path.exists():
            if auto.required:
                raise StageError(f"缺少必需固定层文件：{auto.source}")
            continue
        resolved, relative = _resolve_source(root, auto.source)
        if relative in seen_sources:
            continue
        seen_sources.add(relative)
        staged_specs.append((auto.staged, auto.layer, resolved))
        staged_specs[-1] = (auto.staged, auto.layer, resolved)
        if relative != auto.source:
            raise StageError(f"固定层文件路径解析异常：{auto.source}")

    manual_specs: list[tuple[str, str, Path]] = []
    for raw in sorted(set(selected)):
        resolved, relative = _resolve_source(root, raw)
        if relative in seen_sources:
            continue
        seen_sources.add(relative)
        manual_specs.append((_manual_staged_name(relative, chat_safe), "L3", resolved))

    all_specs = staged_specs + manual_specs
    seen_staged: dict[str, str] = {}
    for staged_name, _, source_path in all_specs:
        prior = seen_staged.get(staged_name)
        if prior is not None and prior != source_path.as_posix():
            raise StageError(f"扁平化暂存名冲突：{staged_name}")
        seen_staged[staged_name] = source_path.as_posix()

    if feed_drop.exists() and feed_drop.is_symlink():
        raise StageError(f"拒绝替换符号链接目录：{feed_drop}")
    temporary_feed = Path(tempfile.mkdtemp(prefix=".FEED_DROP_", dir=task_dir))
    try:
        staged_files: list[StagedFile] = []
        for staged_name, layer, source_path in all_specs:
            target = temporary_feed / staged_name
            shutil.copyfile(source_path, target)
            relative_source = source_path.relative_to(root).as_posix()
            staged_files.append(
                StagedFile(
                    staged=staged_name,
                    layer=layer,
                    source=relative_source,
                    source_path=source_path,
                    tokens=_estimate_tokens(target),
                )
            )

        recent_text, recent_warnings = _git_recent_context(root, git_diff)
        warnings.extend(recent_warnings)
        if recent_text:
            recent_path = temporary_feed / "L3__git_recent_context.txt"
            recent_path.write_text(recent_text, encoding="utf-8")
            staged_files.append(
                StagedFile(
                    staged=recent_path.name,
                    layer="L3",
                    source="git:recent",
                    source_path=recent_path,
                    tokens=_estimate_tokens(recent_path),
                )
            )

        totals = {
            "L1": sum(item.tokens for item in staged_files if item.layer == "L1"),
            "L2": sum(item.tokens for item in staged_files if item.layer == "L2"),
            "L3": sum(item.tokens for item in staged_files if item.layer == "L3"),
        }
        totals["Total"] = totals["L1"] + totals["L2"] + totals["L3"]
        if totals["Total"] > budget_limit:
            warnings.append(
                f"上下文预算警告：Total {totals['Total']} 超过限制 {budget_limit}，请拆分任务或减少 L3 文件。"
            )

        manifest = _manifest_text(
            task_id=task_dir.name,
            root=root,
            generated_at=generated_at,
            base_commit=base_commit,
            chat_safe=chat_safe,
            mode="append" if append else "rebuild",
            files=staged_files,
            totals=totals,
            budget_limit=budget_limit,
            warnings=warnings,
        )

        if feed_drop.exists():
            shutil.rmtree(feed_drop)
        temporary_feed.replace(feed_drop)
        temporary_feed = None
        manifest_path.write_text(manifest, encoding="utf-8")
    finally:
        if temporary_feed is not None and temporary_feed.exists():
            shutil.rmtree(temporary_feed)

    result: dict[str, object] = {
        "ok": True,
        "task_id": task_dir.name,
        "base_commit": base_commit,
        "manifest": manifest_path.relative_to(root).as_posix(),
        "feed_drop": feed_drop.relative_to(root).as_posix(),
        "files": [item.to_json() for item in staged_files],
        "tokens": {
            "l1": totals["L1"],
            "l2": totals["L2"],
            "l3": totals["L3"],
            "total": totals["Total"],
        },
        "warnings": warnings,
        "error": None,
    }
    return result, warnings


def _positive_int(value: str) -> int:
    parsed = int(value)
    if parsed <= 0:
        raise argparse.ArgumentTypeError("必须为正整数")
    return parsed


def _non_negative_int(value: str) -> int:
    parsed = int(value)
    if parsed < 0:
        raise argparse.ArgumentTypeError("必须为非负整数")
    return parsed


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="生成 AI_Context 物理源文件投喂箱")
    parser.add_argument("task_dir", help="任务目录，如 AI_Context/TASKS/GD-2026-0001")
    parser.add_argument("--files", nargs="+", default=[], help="相对项目根加入的 L3 源文件")
    parser.add_argument("--from-list", help="从文本清单批量读取源文件（相对当前目录）")
    parser.add_argument("--append", action="store_true", help="合并现有 Manifest 与 FEED_DROP")
    parser.add_argument(
        "--no-chat-safe",
        action="store_true",
        help="不将冷门扩展名改为 .txt 安全名",
    )
    parser.add_argument(
        "--git-diff",
        type=_non_negative_int,
        default=5,
        metavar="N",
        help="包含近期 N 条 Git log/stat；0 表示关闭（默认 5）",
    )
    parser.add_argument(
        "--budget-limit",
        type=_positive_int,
        default=150000,
        metavar="N",
        help="总 Token 警告阈值（默认 150000）",
    )
    parser.add_argument("--json", action="store_true", help="输出机器可读 JSON")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        result, warnings = stage(
            args.task_dir,
            files=args.files,
            from_list=args.from_list,
            append=args.append,
            chat_safe=not args.no_chat_safe,
            git_diff=args.git_diff,
            budget_limit=args.budget_limit,
        )
    except (OSError, StageError, UnicodeError, ValueError) as exc:
        if args.json:
            print(
                json.dumps(
                    {
                        "ok": False,
                        "task_id": Path(args.task_dir).name,
                        "base_commit": None,
                        "manifest": None,
                        "feed_drop": None,
                        "files": [],
                        "tokens": {"l1": 0, "l2": 0, "l3": 0, "total": 0},
                        "warnings": [],
                        "error": str(exc),
                    },
                    ensure_ascii=False,
                    indent=2,
                )
            )
        else:
            print(f"stage_context 失败：{exc}", file=sys.stderr)
        return 1

    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print(
            f"已生成 {result['feed_drop']} 与 {result['manifest']}；"
            f"Total Token 估算 {result['tokens']['total']}"
        )
        for warning in warnings:
            print(f"[WARN] {warning}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
