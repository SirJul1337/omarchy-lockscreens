#!/usr/bin/env python3
"""Check one community design directory. Run by the PR workflow and locally.

A community design is a lock screen and nothing else: it draws, it reads the
clock and the user name, it takes a password. Anything that reaches outside
that -- a process, a socket, the filesystem, another QML file loaded at
runtime -- is refused here, before a human ever reads the diff.

Usage: scan.py Designs/<id>
Exit 0 = clean, 1 = refused. Every line explains itself.
"""
import json
import re
import sys
from pathlib import Path

# The only imports a design needs. Quickshell.Io is deliberately absent: it is
# where Process and FileView live. QtMultimedia is absent because video
# designs are not accepted yet.
ALLOWED_MODULES = ("QtQuick", "qs.Commons")
DESIGNS_IMPORT = "../plugins/io.github.sirjul1337.lock-explorer/designs"

FORBIDDEN = [
    (r"\bProcess\b", "Process (runs commands)"),
    (r"\bXMLHttpRequest\b", "XMLHttpRequest (network)"),
    (r"\b(WebSocket|SocketServer|Socket)\b", "sockets"),
    (r"\bFileView\b", "FileView (reads the filesystem)"),
    (r"\bXmlListModel\b", "XmlListModel (network)"),
    (r"\beval\s*\(", "eval"),
    (r"Qt\s*\.\s*openUrlExternally", "Qt.openUrlExternally"),
    (r"Qt\s*\.\s*createQmlObject", "Qt.createQmlObject (runtime code)"),
    (r"Qt\s*\.\s*include", "Qt.include (runtime code)"),
    (r"\bLoader\b\s*\{[^}]*\bsource\b", "Loader with a runtime source"),
    (r"https?://", "a network URL"),
    (r"file:///", "an absolute file:// path"),
]

IMPORT_RE = re.compile(r'^\s*import\s+(?:"(?P<quoted>[^"]+)"|(?P<module>[A-Za-z0-9_.]+))', re.M)
TAGS = {"minimal", "cards", "clock", "type", "dark", "fun", "reactive"}
MAX_QML = 64 * 1024
MAX_PREVIEW = 1024 * 1024

problems: list[str] = []
notes: list[str] = []


def fail(msg: str) -> None:
    problems.append(msg)


def ok(msg: str) -> None:
    notes.append(msg)


def check_layout(d: Path) -> tuple[Path | None, Path | None]:
    """One design.json, one .qml, one preview image, nothing else."""
    entries = sorted(p for p in d.iterdir())
    dirs = [p.name for p in entries if p.is_dir()]
    if dirs:
        fail(f"subdirectories are not allowed: {', '.join(dirs)}")

    qml = [p for p in entries if p.suffix == ".qml"]
    previews = [p for p in entries if p.suffix.lower() in (".png", ".jpg", ".jpeg")]
    meta = [p for p in entries if p.name == "design.json"]
    known = set(qml) | set(previews) | set(meta)
    extra = [p.name for p in entries if p.is_file() and p not in known]
    if extra:
        fail(f"unexpected files: {', '.join(extra)}")
    if len(qml) != 1:
        fail(f"expected exactly one .qml file, found {len(qml)}")
    if len(previews) != 1:
        fail(f"expected exactly one preview image, found {len(previews)}")
    if not meta:
        fail("design.json is missing")

    return (qml[0] if len(qml) == 1 else None,
            previews[0] if len(previews) == 1 else None)


def check_meta(d: Path) -> None:
    path = d / "design.json"
    if not path.exists():
        return
    try:
        meta = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        fail(f"design.json is not valid JSON: {e}")
        return

    for field in ("name", "author", "description", "tags"):
        if field not in meta:
            fail(f"design.json is missing \"{field}\"")
    name = str(meta.get("name", ""))
    if not 2 <= len(name) <= 40:
        fail(f"name must be 2-40 characters, got {len(name)}")
    desc = str(meta.get("description", ""))
    if not 10 <= len(desc) <= 200:
        fail(f"description must be 10-200 characters, got {len(desc)}")
    tags = meta.get("tags") or []
    if not isinstance(tags, list) or not 1 <= len(tags) <= 3:
        fail("tags must be a list of 1-3 entries")
    else:
        bad = [t for t in tags if t not in TAGS]
        if bad:
            fail(f"unknown tags: {', '.join(map(str, bad))} (pick from {', '.join(sorted(TAGS))})")
    author = str(meta.get("author", ""))
    if not re.fullmatch(r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})", author):
        fail(f"author must be a GitHub username, got \"{author}\"")
    if not problems:
        ok(f"design.json: {name} by @{author}, tags {', '.join(tags)}")


def check_qml(path: Path) -> None:
    size = path.stat().st_size
    if size > MAX_QML:
        fail(f"{path.name} is {size // 1024} KB, over the 64 KB cap")
        return
    text = path.read_text(encoding="utf-8", errors="replace")

    saw_designs_import = False
    for m in IMPORT_RE.finditer(text):
        module, quoted = m.group("module"), m.group("quoted")
        if module:
            if module in ALLOWED_MODULES or any(module.startswith(a + ".") for a in ALLOWED_MODULES):
                continue
            fail(f"{path.name}: import {module} is not allowed "
                 f"(allowed: {', '.join(ALLOWED_MODULES)} and the designs directory)")
        elif quoted:
            if quoted == DESIGNS_IMPORT:
                saw_designs_import = True
                continue
            fail(f"{path.name}: directory import \"{quoted}\" is not allowed")
    if not saw_designs_import:
        fail(f"{path.name}: missing the designs import "
             f"(import \"{DESIGNS_IMPORT}\")")

    # Strip comments before hunting capability, so a comment naming Process
    # is not a failure -- the plugin's own designs talk about these in prose.
    stripped = re.sub(r"//[^\n]*", "", text)
    stripped = re.sub(r"/\*.*?\*/", "", stripped, flags=re.S)

    hits = [name for pattern, name in FORBIDDEN if re.search(pattern, stripped)]
    if hits:
        fail(f"{path.name}: uses {', '.join(hits)}")
    else:
        ok(f"{path.name}: no processes, network, filesystem or runtime code ({size / 1024:.1f} KB)")

    if not re.search(r"^\s*DesignBase\s*\{", stripped, re.M):
        fail(f"{path.name}: the root item must be DesignBase")


def check_preview(path: Path) -> None:
    size = path.stat().st_size
    if size > MAX_PREVIEW:
        fail(f"{path.name} is {size // 1024} KB, over the 1 MB cap")
    else:
        ok(f"{path.name}: {size / 1024:.0f} KB")


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    d = Path(sys.argv[1])
    if not d.is_dir():
        print(f"not a directory: {d}")
        return 2
    if not re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", d.name):
        fail(f"directory name \"{d.name}\" must be lowercase words separated by hyphens")

    qml, preview = check_layout(d)
    check_meta(d)
    if qml:
        check_qml(qml)
    if preview:
        check_preview(preview)

    print(f"--- {d.name} ---")
    for n in notes:
        print(f"  ok    {n}")
    for p in problems:
        print(f"  FAIL  {p}")
    print("  refused" if problems else "  clean")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
