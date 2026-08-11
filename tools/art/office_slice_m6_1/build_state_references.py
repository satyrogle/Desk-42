"""Build deterministic action boards for the M6.1 Krea 2 Gate A edits."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps


ROOT = Path(__file__).resolve().parents[3]
M4 = ROOT / "ArtLab" / "OfficeSliceM4" / "ApprovedSources"
OUTPUT = ROOT / "ArtLab" / "OfficeSliceM6_1" / "References" / "StateBoards"

SOURCES = {
    "auto_sorter_active": M4 / "Machines" / "auto-sorter_active.png",
    "copy_echo_active": M4 / "Machines" / "copy-echo_active.png",
    "copy_echo_break": M4 / "Machines" / "copy-echo_break.png",
    "supervisor_stamp_break": M4 / "Machines" / "supervisor-stamp_break.png",
    "warden_help": M4 / "Characters" / "warden_help.png",
    "runner_carry": M4 / "Characters" / "runner_carry.png",
    "runner_obey": M4 / "Characters" / "runner_obey-copier.png",
    "customer_upset": M4 / "Characters" / "mara-vale_upset.png",
    "folder_normal": M4 / "Folders" / "folder_normal.png",
    "folder_rule": M4 / "Folders" / "folder_rule-matched.png",
    "folder_original": M4 / "Folders" / "folder_original.png",
    "folder_copy": M4 / "Folders" / "folder_copy_tier_2.png",
    "machine_stop": M4 / "VFX" / "machine-stop.png",
    "copy_clear": M4 / "VFX" / "copy-clear.png",
    "cascade": M4 / "VFX" / "promotion-cascade-ink-fracture.png",
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(name: str) -> Image.Image:
    path = SOURCES[name]
    if not path.is_file():
        raise FileNotFoundError(path)
    return Image.open(path).convert("RGBA")


def paste(canvas: Image.Image, name: str, box: tuple[int, int, int, int]) -> None:
    with load(name) as source:
        fitted = ImageOps.contain(
            source,
            (box[2] - box[0], box[3] - box[1]),
            method=Image.Resampling.NEAREST,
        )
        x = box[0] + (box[2] - box[0] - fitted.width) // 2
        y = box[1] + (box[3] - box[1] - fitted.height) // 2
        canvas.alpha_composite(fitted, (x, y))


def relief_board() -> Image.Image:
    canvas = Image.new("RGBA", (1024, 1024), "#e8d9b5")
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((24, 24, 1000, 1000), radius=36, outline="#15151a", width=20)
    draw.line((110, 520, 914, 520), fill="#2f6b67", width=44)
    draw.polygon(((914, 520), (850, 476), (850, 564)), fill="#2f6b67")
    for x in (90, 220, 740, 870):
        paste(canvas, "folder_rule" if x > 600 else "folder_normal", (x, 410, x + 150, 570))
    paste(canvas, "auto_sorter_active", (260, 250, 590, 670))
    paste(canvas, "copy_echo_active", (515, 250, 820, 670))
    paste(canvas, "warden_help", (50, 610, 350, 950))
    paste(canvas, "runner_carry", (690, 610, 970, 950))
    return canvas


def break_board() -> Image.Image:
    canvas = Image.new("RGBA", (1024, 1024), "#15151a")
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((24, 24, 1000, 1000), radius=36, outline="#b53b38", width=24)
    for offset in range(-120, 1100, 170):
        draw.line((offset, 60, offset + 330, 960), fill="#7b4a88", width=22)
    paste(canvas, "cascade", (80, 60, 944, 940))
    paste(canvas, "copy_echo_break", (310, 220, 710, 690))
    paste(canvas, "supervisor_stamp_break", (610, 110, 930, 480))
    paste(canvas, "runner_obey", (70, 560, 360, 920))
    paste(canvas, "customer_upset", (690, 590, 950, 930))
    paste(canvas, "folder_original", (80, 120, 300, 350))
    for x, y in ((120, 370), (700, 420), (800, 520), (400, 720), (560, 780)):
        paste(canvas, "folder_copy", (x, y, x + 170, y + 190))
    paste(canvas, "machine_stop", (345, 70, 540, 250))
    paste(canvas, "copy_clear", (480, 70, 675, 250))
    return canvas


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    outputs = {
        "A02_automation_relief_board.png": relief_board(),
        "A03_promotion_cascade_break_board.png": break_board(),
    }
    records = []
    for filename, image in outputs.items():
        path = OUTPUT / filename
        image.save(path, format="PNG", optimize=True)
        records.append({"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path)})
    manifest = {
        "schema": "desk42.office-slice-m6.1.state-reference-boards.v1",
        "method": "deterministic Pillow composition of approved M4 project-original assets",
        "sources": [
            {"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path)}
            for path in SOURCES.values()
        ],
        "outputs": records,
    }
    (OUTPUT / "state-board-manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    print("M6_1_STATE_BOARDS_OK", *(record["sha256"] for record in records))


if __name__ == "__main__":
    main()
