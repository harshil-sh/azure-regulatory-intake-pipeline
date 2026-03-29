#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import logging
import shlex
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


DEFAULT_VERIFY_PROMPT = (
    "Read docs/TASKS.md, docs/architecture.md, docs/adr-local-infra.md, and "
    "docs/coding-standards.md first.\n"
    "Run tests and verification only for the most recently completed task.\n"
    "Do not modify code unless something is broken.\n"
    "Summarize exactly what passed, what failed, and whether the task is fully verified."
)

STATE_DIR_NAME = ".codex-runner"
STATE_FILE_NAME = "state.json"
LOGS_DIR_NAME = "logs"


@dataclass
class Task:
    task_id: int
    phase: str
    title: str
    status: str
    acceptance_criteria: list[str]
    notes: list[str]
    prompt_hint: str = ""


class RunnerError(Exception):
    pass


class CodexPortfolioRunner:
    def __init__(
        self,
        repo_root: Path,
        tasks_file: Path,
        max_tasks: int,
        verify_prompt: str,
        codex_cmd: str,
        extra_codex_args: list[str] | None = None,
        require_clean_git: bool = True,
        auto_commit: bool = True,
        auto_push: bool = False,
        dry_run: bool = False,
        docs_tasks_path: str = "docs/TASKS.md",
        docs_architecture_path: str = "docs/architecture.md",
        docs_adr_path: str = "docs/adr-local-infra.md",
        docs_standards_path: str = "docs/coding-standards.md",
        ignore_paths: list[str] = [".codex-runner", "tools/tasks.json"],
    ) -> None:
        self.repo_root = repo_root.resolve()
        self.tasks_file = tasks_file.resolve()
        self.max_tasks = max_tasks
        self.verify_prompt = verify_prompt
        self.codex_cmd = codex_cmd
        self.extra_codex_args = extra_codex_args or []
        self.require_clean_git = require_clean_git
        self.auto_commit = auto_commit
        self.auto_push = auto_push
        self.dry_run = dry_run

        self.docs_tasks_path = docs_tasks_path
        self.docs_architecture_path = docs_architecture_path
        self.docs_adr_path = docs_adr_path
        self.docs_standards_path = docs_standards_path
        self.ignore_paths = ignore_paths or [".codex-runner"]

        self.state_dir = self.repo_root / STATE_DIR_NAME
        self.logs_dir = self.state_dir / LOGS_DIR_NAME
        self.state_file = self.state_dir / STATE_FILE_NAME

        self.state_dir.mkdir(parents=True, exist_ok=True)
        self.logs_dir.mkdir(parents=True, exist_ok=True)

        self.logger = self._build_logger()

    def _build_logger(self) -> logging.Logger:
        logger = logging.getLogger("codex_portfolio_runner")
        logger.setLevel(logging.INFO)
        logger.handlers.clear()

        formatter = logging.Formatter(
            fmt="%(asctime)s | %(levelname)s | %(message)s",
            datefmt="%Y-%m-%d %H:%M:%S",
        )

        console = logging.StreamHandler(sys.stdout)
        console.setFormatter(formatter)
        logger.addHandler(console)

        file_handler = logging.FileHandler(
            self.logs_dir / f"runner-{datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')}.log",
            encoding="utf-8",
        )
        file_handler.setFormatter(formatter)
        logger.addHandler(file_handler)

        return logger

    def run(self) -> int:
        self.logger.info("Repo root: %s", self.repo_root)
        self.logger.info("Tasks file: %s", self.tasks_file)
        self.logger.info("Auto-commit: %s | Auto-push: %s", self.auto_commit, self.auto_push)

        self._validate_environment()

        tasks = self._load_tasks()
        state = self._load_state()

        pending = [
            t for t in tasks
            if t.task_id not in state["completed_task_ids"]
            and t.status != "completed"
        ]

        if not pending:
            self.logger.info("No pending tasks found.")
            return 0

        tasks_to_run = pending[: self.max_tasks]
        self.logger.info(
            "Will run up to %d task(s): %s",
            len(tasks_to_run),
            ", ".join(f"{t.task_id}:{t.title}" for t in tasks_to_run),
        )

        for task in tasks_to_run:
            self.logger.info("=" * 100)
            self.logger.info("Starting Task %s - %s", task.task_id, task.title)

            state["current_task_id"] = task.task_id
            state["current_task_title"] = task.title
            state["current_stage"] = "task"
            state["updated_at_utc"] = self._utc_now()
            self._save_state(state)

            task_prompt = self._build_task_prompt(task)
            task_result = self._run_codex_step(
                step_type="task",
                task=task,
                prompt=task_prompt,
            )
            if task_result["returncode"] != 0:
                self._record_failure(
                    state=state,
                    task=task,
                    stage="task",
                    exit_code=task_result["returncode"],
                    stdout_log=task_result["stdout_log"],
                    stderr_log=task_result["stderr_log"],
                )
                return task_result["returncode"]

            self.logger.info("Task %s completed. Starting verification.", task.task_id)

            state["current_stage"] = "verify"
            state["updated_at_utc"] = self._utc_now()
            self._save_state(state)

            verify_result = self._run_codex_step(
                step_type="verify",
                task=task,
                prompt=self.verify_prompt,
            )
            if verify_result["returncode"] != 0:
                self._record_failure(
                    state=state,
                    task=task,
                    stage="verify",
                    exit_code=verify_result["returncode"],
                    stdout_log=verify_result["stdout_log"],
                    stderr_log=verify_result["stderr_log"],
                )
                return verify_result["returncode"]

            commit_sha = None
            if self.auto_commit:
                self.logger.info("Verification passed. Preparing git commit for Task %s.", task.task_id)
                commit_sha = self._commit_task_changes(task)

                if self.auto_push:
                    self.logger.info("Pushing commit for Task %s.", task.task_id)
                    self._push_current_branch()

            self._mark_task_complete_in_json(task.task_id)

            state["completed_task_ids"].append(task.task_id)
            state["history"].append({
                "task_id": task.task_id,
                "task_title": task.title,
                "completed_at_utc": self._utc_now(),
                "task_stdout_log": task_result["stdout_log"],
                "task_stderr_log": task_result["stderr_log"],
                "verify_stdout_log": verify_result["stdout_log"],
                "verify_stderr_log": verify_result["stderr_log"],
                "commit_sha": commit_sha,
            })
            state["current_task_id"] = None
            state["current_task_title"] = None
            state["current_stage"] = None
            state["last_failure"] = None
            state["updated_at_utc"] = self._utc_now()
            self._save_state(state)

            time.sleep(1)

        self.logger.info("Run finished successfully.")
        return 0

    def _validate_environment(self) -> None:
        if not self.tasks_file.exists():
            raise RunnerError(f"Tasks file not found: {self.tasks_file}")

        if not (self.repo_root / ".git").exists():
            raise RunnerError(f"Repo root is not a git repo: {self.repo_root}")

        required_docs = [
            self.docs_tasks_path,
            self.docs_architecture_path,
            self.docs_adr_path,
            self.docs_standards_path,
        ]
        missing = [p for p in required_docs if not (self.repo_root / p).exists()]
        if missing:
            raise RunnerError(
                "Missing required docs files:\n- " + "\n- ".join(missing)
            )

        if self.require_clean_git and not self._git_is_clean():
            raise RunnerError("Git working tree is not clean. Commit/stash changes first.")

        self._check_codex_available()
        self._check_git_available()

    def _check_codex_available(self) -> None:
        cmd = shlex.split(self.codex_cmd) + ["--help"]
        result = subprocess.run(
            cmd,
            cwd=self.repo_root,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        if result.returncode != 0:
            raise RunnerError(
                f"Unable to run Codex command '{self.codex_cmd}'. "
                f"Exit code: {result.returncode}\n"
                f"stderr: {result.stderr.strip()}"
            )

    def _check_git_available(self) -> None:
        result = subprocess.run(
            ["git", "--version"],
            cwd=self.repo_root,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        if result.returncode != 0:
            raise RunnerError("git is not available in this environment.")

    def _git_is_clean(self) -> bool:
        result = subprocess.run(
            ["git", "status", "--porcelain"],
            cwd=self.repo_root,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        if result.returncode != 0:
            raise RunnerError(f"git status failed: {result.stderr.strip()}")
        return result.stdout.strip() == ""

    def _load_tasks(self) -> list[Task]:
        try:
            raw = json.loads(self.tasks_file.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            raise RunnerError(f"Invalid JSON in tasks file: {self.tasks_file}") from exc

        if not isinstance(raw, list):
            raise RunnerError("Tasks JSON must be an array of task objects.")

        tasks: list[Task] = []
        for item in raw:
            try:
                tasks.append(
                    Task(
                        task_id=int(item["task_id"]),
                        phase=str(item.get("phase", "")),
                        title=str(item["title"]),
                        status=str(item.get("status", "not_started")),
                        acceptance_criteria=list(item.get("acceptance_criteria", [])),
                        notes=list(item.get("notes", [])),
                        prompt_hint=str(item.get("prompt_hint", "")),
                    )
                )
            except (KeyError, TypeError, ValueError) as exc:
                raise RunnerError(f"Invalid task entry: {item!r}") from exc

        tasks.sort(key=lambda x: x.task_id)
        return tasks

    def _load_state(self) -> dict[str, Any]:
        if not self.state_file.exists():
            state = {
                "completed_task_ids": [],
                "current_task_id": None,
                "current_task_title": None,
                "current_stage": None,
                "last_failure": None,
                "history": [],
                "updated_at_utc": self._utc_now(),
            }
            self._save_state(state)
            return state

        try:
            return json.loads(self.state_file.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            raise RunnerError(f"Invalid state file JSON: {self.state_file}") from exc

    def _save_state(self, state: dict[str, Any]) -> None:
        temp = self.state_file.with_suffix(".tmp")
        temp.write_text(json.dumps(state, indent=2), encoding="utf-8")
        temp.replace(self.state_file)

    def _build_task_prompt(self, task: Task) -> str:
        if task.prompt_hint.strip():
            return (
                f"{task.prompt_hint.strip()}\n\n"
                f"Read {self.docs_tasks_path}, {self.docs_architecture_path}, "
                f"{self.docs_adr_path}, and {self.docs_standards_path} first.\n"
                f"Work only on Task {task.task_id}: {task.title}\n"
                "Do not start later tasks.\n\n"
                "Constraints:\n"
                "- Keep the implementation Azure-oriented and local-first.\n"
                "- Use Azurite-compatible configuration.\n"
                "- Keep Functions thin and logic in services.\n"
                "- Do not introduce paid Azure dependencies.\n\n"
                "Final output must include:\n"
                "1. Plan\n"
                "2. Changes made\n"
                "3. Files updated\n"
                "4. Verification performed\n"
                "5. Whether the task is fully complete\n"
            )

        acceptance = "\n".join(f"- {x}" for x in task.acceptance_criteria) or "- Follow docs/TASKS.md"
        notes = "\n".join(f"- {x}" for x in task.notes) or "- None"

        return (
            f"Open {self.docs_tasks_path} and execute ONLY Task {task.task_id}.\n\n"
            f"Read {self.docs_architecture_path}, {self.docs_adr_path}, and "
            f"{self.docs_standards_path} first.\n\n"
            f"Task title:\n{task.title}\n\n"
            f"Phase:\n{task.phase}\n\n"
            "Acceptance criteria:\n"
            f"{acceptance}\n\n"
            "Notes:\n"
            f"{notes}\n\n"
            "Constraints:\n"
            "- Do not start any later task.\n"
            "- Keep changes minimal and production-minded.\n"
            "- Keep the implementation Azure-oriented and local-first.\n"
            "- Use Azurite-compatible configuration.\n"
            "- Keep Functions thin and logic in services.\n"
            "- Run relevant verification for this task.\n\n"
            "Final output:\n"
            "1. Plan\n"
            "2. Changes made\n"
            "3. Files updated\n"
            "4. Verification performed\n"
            "5. Whether the task is fully complete\n"
        )

    def _run_codex_step(
        self,
        step_type: str,
        task: Task,
        prompt: str,
    ) -> dict[str, Any]:
        timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        safe_name = self._slugify(task.title)

        stdout_log = self.logs_dir / f"{task.task_id:03d}-{safe_name}-{step_type}-{timestamp}.stdout.log"
        stderr_log = self.logs_dir / f"{task.task_id:03d}-{safe_name}-{step_type}-{timestamp}.stderr.log"

        cmd = shlex.split(self.codex_cmd) + ["exec"] + self.extra_codex_args + [prompt]

        self.logger.info("Running Codex step: %s", step_type)
        self.logger.info("Command: %s", " ".join(shlex.quote(c) for c in cmd[:-1]) + " '<prompt omitted>'")

        if self.dry_run:
            stdout_log.write_text("[DRY RUN] No stdout.\n", encoding="utf-8")
            stderr_log.write_text("[DRY RUN] No stderr.\n", encoding="utf-8")
            self.logger.info("[DRY RUN] Skipping Codex execution.")
            return {
                "returncode": 0,
                "stdout_log": str(stdout_log),
                "stderr_log": str(stderr_log),
            }

        with stdout_log.open("w", encoding="utf-8") as out, stderr_log.open("w", encoding="utf-8") as err:
            result = subprocess.run(
                cmd,
                cwd=self.repo_root,
                stdout=out,
                stderr=err,
                text=True,
                encoding="utf-8",
            )

        self.logger.info(
            "Codex step finished with exit code %s. stdout=%s stderr=%s",
            result.returncode,
            stdout_log,
            stderr_log,
        )
        return {
            "returncode": result.returncode,
            "stdout_log": str(stdout_log),
            "stderr_log": str(stderr_log),
        }

    def _commit_task_changes(self, task: Task) -> str | None:
        if self.dry_run:
            self.logger.info("[DRY RUN] Skipping git commit.")
            return "dry-run-no-commit"

        changed_files = self._git_changed_files_filtered()
        if not changed_files:
            self.logger.info("No git changes detected after Task %s. No commit created.", task.task_id)
            return None

        self._run_cmd(["git", "add", "--"] + changed_files, "git add failed")

        staged_files = self._git_staged_files_filtered()
        if not staged_files:
            self.logger.info("No staged changes after git add. No commit created.")
            return None

        commit_message = f"feat(task-{task.task_id}): {task.title}"
        self._run_cmd(["git", "commit", "-m", commit_message], "git commit failed")

        sha = self._run_cmd(["git", "rev-parse", "HEAD"], "git rev-parse failed", capture_output=True).strip()
        self.logger.info("Created commit %s for Task %s.", sha, task.task_id)
        return sha

    def _push_current_branch(self) -> None:
        if self.dry_run:
            self.logger.info("[DRY RUN] Skipping git push.")
            return
        self._run_cmd(["git", "push"], "git push failed")

    def _mark_task_complete_in_json(self, task_id: int) -> None:
        try:
            raw = json.loads(self.tasks_file.read_text(encoding="utf-8"))
        except json.JSONDecodeError as exc:
            raise RunnerError(f"Failed to re-read tasks JSON: {self.tasks_file}") from exc

        found = False
        for item in raw:
            if int(item.get("task_id")) == task_id:
                item["status"] = "completed"
                found = True
                break

        if not found:
            raise RunnerError(f"Task {task_id} not found in tasks JSON.")

        if self.dry_run:
            self.logger.info("[DRY RUN] Would mark Task %s as completed in %s", task_id, self.tasks_file)
            return

        temp = self.tasks_file.with_suffix(".tmp")
        temp.write_text(json.dumps(raw, indent=2), encoding="utf-8")
        temp.replace(self.tasks_file)
        self.logger.info("Marked Task %s as completed in %s", task_id, self.tasks_file)

    def _record_failure(
        self,
        state: dict[str, Any],
        task: Task,
        stage: str,
        exit_code: int,
        stdout_log: str,
        stderr_log: str,
    ) -> None:
        self.logger.error("Task %s failed during %s with exit code %s", task.task_id, stage, exit_code)
        state["last_failure"] = {
            "task_id": task.task_id,
            "task_title": task.title,
            "stage": stage,
            "exit_code": exit_code,
            "timestamp_utc": self._utc_now(),
            "stdout_log": stdout_log,
            "stderr_log": stderr_log,
        }
        state["updated_at_utc"] = self._utc_now()
        self._save_state(state)

    def _git_changed_files_filtered(self) -> list[str]:
        output = self._run_cmd(
            ["git", "status", "--porcelain"],
            "git status failed",
            capture_output=True,
        )
        files: list[str] = []

        for line in output.splitlines():
            if not line.strip():
                continue
            path = line[3:].strip()
            if path.startswith('"') and path.endswith('"'):
                path = path[1:-1]
            if self._should_ignore_path(path):
                continue
            files.append(path)

        return files

    def _git_staged_files_filtered(self) -> list[str]:
        output = self._run_cmd(
            ["git", "diff", "--cached", "--name-only"],
            "git diff --cached failed",
            capture_output=True,
        )
        files = [line.strip() for line in output.splitlines() if line.strip()]
        return [f for f in files if not self._should_ignore_path(f)]

    def _should_ignore_path(self, path: str) -> bool:
        normalized = path.replace("\\", "/").lstrip("./")
        for ignored in self.ignore_paths:
            ignored_norm = ignored.replace("\\", "/").rstrip("/")
            if normalized == ignored_norm or normalized.startswith(f"{ignored_norm}/"):
                return True
        return False

    def _run_cmd(
        self,
        cmd: list[str],
        error_message: str,
        capture_output: bool = False,
    ) -> str:
        result = subprocess.run(
            cmd,
            cwd=self.repo_root,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        if result.returncode != 0:
            raise RunnerError(
                f"{error_message}\n"
                f"Command: {' '.join(shlex.quote(c) for c in cmd)}\n"
                f"stdout: {result.stdout.strip()}\n"
                f"stderr: {result.stderr.strip()}"
            )
        return result.stdout if capture_output else ""

    @staticmethod
    def _slugify(value: str) -> str:
        cleaned = []
        for ch in value.lower():
            if ch.isalnum():
                cleaned.append(ch)
            elif ch in {" ", "-", "_", "—"}:
                cleaned.append("-")
        return "".join(cleaned).strip("-") or "task"

    @staticmethod
    def _utc_now() -> str:
        return datetime.now(timezone.utc).isoformat()


def load_verify_prompt(path_str: str) -> str:
    if not path_str:
        return DEFAULT_VERIFY_PROMPT
    path = Path(path_str)
    if not path.exists():
        raise RunnerError(f"Verify prompt file not found: {path}")
    return path.read_text(encoding="utf-8").strip()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run Codex tasks sequentially with verification and optional git commit/push."
    )
    parser.add_argument(
        "--repo-root",
        default=".",
        help="Path to git repo root. Default: current directory.",
    )
    parser.add_argument(
        "--tasks-file",
        default="tools/tasks.json",
        help="Path to machine-readable tasks JSON. Default: tools/tasks.json",
    )
    parser.add_argument(
        "--max-tasks",
        type=int,
        default=5,
        help="Maximum number of pending tasks to run. Default: 5.",
    )
    parser.add_argument(
        "--verify-prompt-file",
        default="",
        help="Optional path to a text file containing verification prompt.",
    )
    parser.add_argument(
        "--codex-cmd",
        default="codex",
        help="Codex executable. Default: codex",
    )
    parser.add_argument(
        "--codex-arg",
        action="append",
        default=[],
        help="Extra argument passed to 'codex exec'. Can be repeated.",
    )
    parser.add_argument(
        "--allow-dirty-git",
        action="store_true",
        help="Allow start even if git working tree is dirty.",
    )
    parser.add_argument(
        "--no-commit",
        action="store_true",
        help="Disable auto-commit after successful task verification.",
    )
    parser.add_argument(
        "--push",
        action="store_true",
        help="Push after each successful commit using normal git push.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Do not run Codex or git write actions.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    try:
        verify_prompt = load_verify_prompt(args.verify_prompt_file)

        runner = CodexPortfolioRunner(
            repo_root=Path(args.repo_root),
            tasks_file=Path(args.tasks_file),
            max_tasks=args.max_tasks,
            verify_prompt=verify_prompt,
            codex_cmd=args.codex_cmd,
            extra_codex_args=args.codex_arg,
            require_clean_git=not args.allow_dirty_git,
            auto_commit=not args.no_commit,
            auto_push=args.push,
            dry_run=args.dry_run,
        )
        return runner.run()

    except RunnerError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    except KeyboardInterrupt:
        print("Interrupted by user.", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())