#!/usr/bin/env python3
"""Write index.json: what this repo holds, for the site to sync from.

The split is that the repo owns the files a person wrote and a person
reviewed, and the database owns everything that changes afterwards -- who
linked their account, how many likes, how many installs, whether it is still
listed. This file is the handover between the two.

It is deliberately the *only* thing the site has to read. One fetch, one
ETag, and the site can work out for itself what is new, what changed and what
has gone: an id it has never seen is an insert, a file whose sha256 moved is a
new version, and an id that is no longer here has been withdrawn. Nothing has
to be pushed anywhere and a missed sync fixes itself on the next one.

Run: python scripts/build-index.py
"""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> int:
    root = Path(__file__).resolve().parent.parent
    designs_dir = root / "Designs"
    out = root / "index.json"

    designs = []
    for d in sorted(p for p in designs_dir.iterdir() if p.is_dir()):
        meta_path = d / "design.json"
        if not meta_path.exists():
            continue
        meta = json.loads(meta_path.read_text(encoding="utf-8"))

        qml = next((p for p in sorted(d.iterdir()) if p.suffix == ".qml"), None)
        preview = next(
            (p for p in sorted(d.iterdir())
             if p.suffix.lower() in (".png", ".jpg", ".jpeg")), None)
        if qml is None:
            continue

        entry = {
            "id": d.name,
            "name": meta.get("name", d.name),
            "author": meta.get("author", ""),
            "description": meta.get("description", ""),
            "tags": meta.get("tags", []),
            "qml": {
                "path": f"Designs/{d.name}/{qml.name}",
                "sha256": sha256(qml),
                "size": qml.stat().st_size,
            },
        }
        if preview is not None:
            entry["preview"] = {
                "path": f"Designs/{d.name}/{preview.name}",
                "sha256": sha256(preview),
                "size": preview.stat().st_size,
            }
        designs.append(entry)

    index = {
        "schemaVersion": 1,
        "generated": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "designs": designs,
    }
    # newline="" so this writes LF on Windows too. index.json is committed
    # and fetched byte for byte, and the files it describes are hashed, so a
    # generator that emitted CRLF here would produce a different file locally
    # than in CI.
    with out.open("w", encoding="utf-8", newline="") as f:
        f.write(json.dumps(index, indent=2) + "\n")
    print(f"{len(designs)} designs -> {out}")
    for d in designs:
        print(f"  {d['id']:<14} {d['qml']['sha256'][:12]}  {d['qml']['size']} B")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
