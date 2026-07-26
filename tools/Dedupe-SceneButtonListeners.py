"""
Desk 42 — remove duplicate UnityEvent persistent listeners from a scene.

Shift.unity accumulated 11 identical Approve and 11 identical Deny
persistent onClick listeners on the resolution buttons, so a single player
click invoked EncounterManager.Approve()/Deny() eleven times. Only an
in-memory bool prevented an 11x payout.

This collapses each m_Calls list to one entry per identical listener,
preserving order and leaving non-duplicate listeners untouched.

Usage:
    python tools/Dedupe-SceneButtonListeners.py <scene.unity> [--apply]

Without --apply it reports what it would change and exits non-zero if
duplicates exist, which makes it usable as a CI guard.
"""

import io
import sys

ENTRY_PREFIX = "      - m_Target:"
CALLS_MARKER = "m_Calls:"


def _is_calls_marker(line):
    # Unity scenes are CRLF; strip all trailing whitespace before comparing.
    return line.strip() == CALLS_MARKER


def dedupe(lines):
    out = []
    i = 0
    removed = []

    while i < len(lines):
        line = lines[i]
        out.append(line)

        if not _is_calls_marker(line):
            i += 1
            continue

        # Collect every listener entry in this m_Calls list.
        i += 1
        entries = []
        while i < len(lines) and lines[i].startswith(ENTRY_PREFIX):
            entry = [lines[i]]
            i += 1
            # An entry continues until the next entry or a dedent.
            while (i < len(lines)
                   and not lines[i].startswith(ENTRY_PREFIX)
                   and lines[i].startswith("        ")):
                entry.append(lines[i])
                i += 1
            entries.append(entry)

        seen = set()
        for entry in entries:
            key = "".join(entry)
            if key in seen:
                removed.append(entry)
                continue
            seen.add(key)
            out.extend(entry)

    return out, removed


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    path = sys.argv[1]
    apply_changes = "--apply" in sys.argv

    with io.open(path, encoding="utf-8", newline="") as fh:
        lines = fh.readlines()

    deduped, removed = dedupe(lines)

    if not removed:
        print(f"OK: no duplicate persistent listeners in {path}")
        return 0

    methods = {}
    for entry in removed:
        for line in entry:
            if "m_MethodName:" in line:
                name = line.split("m_MethodName:")[1].strip()
                methods[name] = methods.get(name, 0) + 1

    print(f"Duplicate listeners in {path}:")
    for name, count in sorted(methods.items()):
        print(f"  {name}: {count} duplicate(s) to remove")

    if not apply_changes:
        print("Dry run. Re-run with --apply to rewrite the scene.")
        return 1

    with io.open(path, "w", encoding="utf-8", newline="") as fh:
        fh.writelines(deduped)
    print(f"Rewrote {path}: {len(lines)} -> {len(deduped)} lines.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
