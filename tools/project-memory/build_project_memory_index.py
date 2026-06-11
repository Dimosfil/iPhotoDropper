#!/usr/bin/env python3
"""Build and query a local SQLite index from git tracked project files."""

from __future__ import annotations

import argparse
import hashlib
import os
import sqlite3
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path


DEFAULT_DB = Path("tools/project-memory/project_memory.sqlite")
MAX_TEXT_BYTES = 512 * 1024
TEXT_EXTENSIONS = {
    ".bat",
    ".cmd",
    ".css",
    ".csv",
    ".html",
    ".ini",
    ".js",
    ".json",
    ".md",
    ".mjs",
    ".ps1",
    ".py",
    ".sql",
    ".toml",
    ".ts",
    ".tsx",
    ".txt",
    ".xml",
    ".yaml",
    ".yml",
}
SKIP_PREFIXES = (
    ".git/",
    "node_modules/",
    "dist/",
    "build/",
)
SKIP_SUFFIXES = (
    ".png",
    ".jpg",
    ".jpeg",
    ".gif",
    ".webp",
    ".ico",
    ".pdf",
    ".zip",
    ".7z",
    ".gz",
    ".sqlite",
    ".db",
)


def utc_now() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


def run_git(args: list[str], root: Path) -> bytes:
    try:
        return subprocess.check_output(["git", *args], cwd=root)
    except FileNotFoundError:
        raise SystemExit("git is required to build the project memory index.")
    except subprocess.CalledProcessError as exc:
        message = exc.output.decode("utf-8", errors="replace").strip()
        raise SystemExit(message or f"git {' '.join(args)} failed.")


def repo_root() -> Path:
    output = run_git(["rev-parse", "--show-toplevel"], Path.cwd())
    return Path(output.decode("utf-8", errors="replace").strip()).resolve()


def tracked_paths(root: Path) -> list[str]:
    output = run_git(["ls-files", "-z"], root)
    paths = [p for p in output.decode("utf-8", errors="replace").split("\0") if p]
    return sorted(paths)


def should_index(path: str) -> bool:
    normalized = path.replace("\\", "/")
    lower = normalized.lower()
    if any(lower.startswith(prefix) for prefix in SKIP_PREFIXES):
        return False
    if any(lower.endswith(suffix) for suffix in SKIP_SUFFIXES):
        return False
    return Path(lower).suffix in TEXT_EXTENSIONS


def read_text(path: Path) -> tuple[str | None, int, str]:
    data = path.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if len(data) > MAX_TEXT_BYTES:
        return None, len(data), digest
    if b"\x00" in data:
        return None, len(data), digest
    try:
        return data.decode("utf-8"), len(data), digest
    except UnicodeDecodeError:
        try:
            return data.decode("utf-8-sig"), len(data), digest
        except UnicodeDecodeError:
            return data.decode("cp1251", errors="replace"), len(data), digest


def connect(db_path: Path) -> sqlite3.Connection:
    db_path.parent.mkdir(parents=True, exist_ok=True)
    con = sqlite3.connect(db_path)
    con.row_factory = sqlite3.Row
    return con


def has_fts5(con: sqlite3.Connection) -> bool:
    try:
        con.execute("CREATE VIRTUAL TABLE temp._fts5_check USING fts5(value)")
        con.execute("DROP TABLE temp._fts5_check")
        return True
    except sqlite3.DatabaseError:
        return False


def create_fts_table(con: sqlite3.Connection) -> None:
    con.execute(
        """
        CREATE VIRTUAL TABLE IF NOT EXISTS files_fts
        USING fts5(path, content, content='files', content_rowid='rowid')
        """
    )


def ensure_schema(con: sqlite3.Connection) -> bool:
    fts = has_fts5(con)
    con.executescript(
        """
        CREATE TABLE IF NOT EXISTS meta (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS files (
            path TEXT PRIMARY KEY,
            extension TEXT NOT NULL,
            size_bytes INTEGER NOT NULL,
            sha256 TEXT NOT NULL,
            line_count INTEGER NOT NULL,
            indexed_at TEXT NOT NULL,
            content TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS notes (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            created_at TEXT NOT NULL,
            topic TEXT NOT NULL,
            title TEXT NOT NULL,
            body TEXT NOT NULL,
            evidence_paths TEXT NOT NULL
        );
        """
    )
    if fts:
        create_fts_table(con)
    return fts


def set_meta(con: sqlite3.Connection, key: str, value: str) -> None:
    con.execute(
        "INSERT INTO meta(key, value) VALUES(?, ?) "
        "ON CONFLICT(key) DO UPDATE SET value=excluded.value",
        (key, value),
    )


def rebuild(args: argparse.Namespace) -> int:
    root = repo_root()
    db_path = (root / args.db).resolve()
    con = connect(db_path)
    indexed_at = utc_now()
    with con:
        fts = ensure_schema(con)
        if fts:
            con.execute("DROP TABLE IF EXISTS files_fts")
            create_fts_table(con)
        con.execute("DELETE FROM files")

        indexed = 0
        skipped = 0
        for rel_path in tracked_paths(root):
            if not should_index(rel_path):
                skipped += 1
                continue
            full_path = root / rel_path
            if not full_path.is_file():
                skipped += 1
                continue
            text, size, digest = read_text(full_path)
            if text is None:
                skipped += 1
                continue
            line_count = text.count("\n") + (1 if text else 0)
            con.execute(
                """
                INSERT INTO files(path, extension, size_bytes, sha256, line_count, indexed_at, content)
                VALUES(?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    rel_path.replace("\\", "/"),
                    full_path.suffix.lower(),
                    size,
                    digest,
                    line_count,
                    indexed_at,
                    text,
                ),
            )
            indexed += 1

        if fts:
            con.execute("INSERT INTO files_fts(files_fts) VALUES('rebuild')")

        set_meta(con, "schema_version", "1")
        set_meta(con, "indexed_at", indexed_at)
        set_meta(con, "repo_root", str(root))
        set_meta(con, "source", "git ls-files tracked text files")
        set_meta(con, "fts5", "enabled" if fts else "disabled")

    print(f"Indexed files: {indexed}")
    print(f"Skipped files: {skipped}")
    print(f"Database: {db_path}")
    print(f"FTS5: {'enabled' if fts else 'disabled'}")
    return 0


def stats(args: argparse.Namespace) -> int:
    root = repo_root()
    db_path = (root / args.db).resolve()
    if not db_path.exists():
        print(f"No database found at {db_path}")
        return 1
    con = connect(db_path)
    ensure_schema(con)
    file_count = con.execute("SELECT COUNT(*) FROM files").fetchone()[0]
    total_bytes = con.execute("SELECT COALESCE(SUM(size_bytes), 0) FROM files").fetchone()[0]
    indexed_at = con.execute("SELECT value FROM meta WHERE key = 'indexed_at'").fetchone()
    print(f"Files: {file_count}")
    print(f"Source bytes: {total_bytes}")
    print(f"Database bytes: {db_path.stat().st_size}")
    print(f"Indexed at: {indexed_at['value'] if indexed_at else 'unknown'}")
    print(f"Database: {db_path}")
    return 0


def search(args: argparse.Namespace) -> int:
    root = repo_root()
    db_path = (root / args.db).resolve()
    if not db_path.exists():
        print(f"No database found at {db_path}")
        return 1
    con = connect(db_path)
    fts = ensure_schema(con)
    limit = max(1, min(args.limit, 50))
    if fts:
        rows = con.execute(
            """
            SELECT files.path, snippet(files_fts, 1, '[', ']', ' ... ', 12) AS excerpt
            FROM files_fts
            JOIN files ON files.rowid = files_fts.rowid
            WHERE files_fts MATCH ?
            ORDER BY bm25(files_fts)
            LIMIT ?
            """,
            (args.query, limit),
        ).fetchall()
    else:
        like = f"%{args.query}%"
        rows = con.execute(
            """
            SELECT path, substr(content, max(1, instr(lower(content), lower(?)) - 80), 240) AS excerpt
            FROM files
            WHERE lower(path) LIKE lower(?) OR lower(content) LIKE lower(?)
            ORDER BY path
            LIMIT ?
            """,
            (args.query, like, like, limit),
        ).fetchall()

    for row in rows:
        excerpt = " ".join(str(row["excerpt"]).split())
        print(f"{row['path']}: {excerpt}")
    if not rows:
        print("No matches.")
    return 0


def note(args: argparse.Namespace) -> int:
    root = repo_root()
    db_path = (root / args.db).resolve()
    con = connect(db_path)
    with con:
        ensure_schema(con)
        con.execute(
            """
            INSERT INTO notes(created_at, topic, title, body, evidence_paths)
            VALUES(?, ?, ?, ?, ?)
            """,
            (utc_now(), args.topic, args.title, args.body, "\n".join(args.evidence)),
        )
    print(f"Added note: {args.title}")
    return 0


def notes(args: argparse.Namespace) -> int:
    root = repo_root()
    db_path = (root / args.db).resolve()
    if not db_path.exists():
        print(f"No database found at {db_path}")
        return 1
    con = connect(db_path)
    ensure_schema(con)
    rows = con.execute(
        """
        SELECT id, created_at, topic, title, evidence_paths
        FROM notes
        ORDER BY id DESC
        LIMIT ?
        """,
        (max(1, min(args.limit, 50)),),
    ).fetchall()
    for row in rows:
        evidence = ", ".join(p for p in row["evidence_paths"].splitlines() if p)
        suffix = f" [{evidence}]" if evidence else ""
        print(f"{row['id']}. {row['created_at']} {row['topic']} - {row['title']}{suffix}")
    if not rows:
        print("No notes.")
    return 0


def export_notes(args: argparse.Namespace) -> int:
    root = repo_root()
    db_path = (root / args.db).resolve()
    output_path = (root / args.output).resolve()
    if not db_path.exists():
        print(f"No database found at {db_path}")
        return 1
    con = connect(db_path)
    ensure_schema(con)
    rows = con.execute(
        """
        SELECT created_at, topic, title, body, evidence_paths
        FROM notes
        ORDER BY id
        """
    ).fetchall()
    lines = [
        "# Project Memory Notes",
        "",
        "SQLite is the local generated search index. This Markdown file is the",
        "human-reviewable long-lived memory export.",
        "",
    ]
    for row in rows:
        lines.extend(
            [
                f"## {row['title']}",
                "",
                f"- Topic: {row['topic']}",
                f"- Created: {row['created_at']}",
            ]
        )
        evidence = [p for p in row["evidence_paths"].splitlines() if p]
        if evidence:
            lines.append(f"- Evidence: {', '.join(evidence)}")
        lines.extend(["", row["body"], ""])
    output_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Exported notes: {output_path}")
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--db", type=Path, default=DEFAULT_DB, help="SQLite database path.")
    sub = parser.add_subparsers(dest="command")

    sub.add_parser("rebuild", help="Rebuild the SQLite index from git tracked files.").set_defaults(func=rebuild)
    sub.add_parser("stats", help="Show index statistics.").set_defaults(func=stats)

    search_parser = sub.add_parser("search", help="Search indexed files.")
    search_parser.add_argument("query")
    search_parser.add_argument("--limit", type=int, default=10)
    search_parser.set_defaults(func=search)

    note_parser = sub.add_parser("note", help="Add a durable local note.")
    note_parser.add_argument("topic")
    note_parser.add_argument("title")
    note_parser.add_argument("body")
    note_parser.add_argument("--evidence", action="append", default=[])
    note_parser.set_defaults(func=note)

    notes_parser = sub.add_parser("notes", help="List local notes.")
    notes_parser.add_argument("--limit", type=int, default=20)
    notes_parser.set_defaults(func=notes)

    export_parser = sub.add_parser("export-notes", help="Export local notes to Markdown.")
    export_parser.add_argument("--output", type=Path, default=Path("tools/project-memory/NOTES.md"))
    export_parser.set_defaults(func=export_notes)

    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if not hasattr(args, "func"):
        parser.print_help()
        return 0
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
