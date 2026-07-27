"""
Desk 42 — enable/disable a MonoBehaviour in a serialized Unity scene by script GUID.

Used to disable the four experimental audio directors attached to
Shift.unity for the proof candidate. They are attached but currently
compile to no-ops; defining DESK42_FMOD would make them live during the
scored run, so they are explicitly disabled BEFORE the define exists.

Operates on the script GUID, never a class-name text search: Unity scenes
reference scripts by GUID, so a name search matches nothing and would
silently report success.

Usage:
    python tools/Set-SceneComponentEnabled.py <scene> <guid> <0|1> [--apply]

Without --apply it reports intended changes and exits non-zero if any are
pending, which makes it usable as a CI guard.
"""

import io
import re
import sys

SCRIPT_LINE = re.compile(r"^\s*m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32}),")
ENABLED_LINE = re.compile(r"^(\s*)m_Enabled:\s*([01])\s*$")


def apply(lines, guid, desired):
    """Set m_Enabled on every MonoBehaviour block referencing `guid`."""
    changed = []

    for i, line in enumerate(lines):
        m = SCRIPT_LINE.match(line)
        if not m or m.group(1) != guid:
            continue

        # m_Enabled precedes m_Script within the same MonoBehaviour block.
        for j in range(i - 1, max(-1, i - 40), -1):
            if lines[j].lstrip().startswith("--- !u!"):
                break  # left the block without finding it
            em = ENABLED_LINE.match(lines[j])
            if not em:
                continue
            if em.group(2) != desired:
                lines[j] = f"{em.group(1)}m_Enabled: {desired}\n"
                changed.append(j + 1)
            break

    return changed


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        return 2

    scene, guid, desired = sys.argv[1], sys.argv[2], sys.argv[3]
    do_apply = "--apply" in sys.argv

    with io.open(scene, encoding="utf-8", newline="") as fh:
        lines = fh.readlines()

    original = list(lines)
    changed = apply(lines, guid, desired)

    if not changed:
        print(f"OK: guid {guid[:8]} already m_Enabled: {desired} (or absent)")
        return 0

    print(f"guid {guid[:8]} -> m_Enabled: {desired} at line(s) {changed}")

    if not do_apply:
        print("Dry run. Re-run with --apply to rewrite the scene.")
        return 1

    if lines == original:
        return 0

    with io.open(scene, "w", encoding="utf-8", newline="") as fh:
        fh.writelines(lines)
    print(f"Rewrote {scene}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
