"""Place Gate A action motifs into the exact accepted A01 office coordinates."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps


ROOT = Path(__file__).resolve().parents[3]
M4 = ROOT / "ArtLab" / "OfficeSliceM4" / "ApprovedSources"
A01 = ROOT / "ArtLab" / "OfficeSliceM6_1" / "Candidates" / "TargetFrames" / "A01_calm_office_seed6101001.png"
OUTPUT = ROOT / "ArtLab" / "OfficeSliceM6_1" / "References" / "SpatialEdits"

SOURCES = {
    "auto_sorter_active": M4 / "Machines" / "auto-sorter_active.png",
    "copy_echo_active": M4 / "Machines" / "copy-echo_active.png",
    "copy_echo_break": M4 / "Machines" / "copy-echo_break.png",
    "supervisor_stamp_break": M4 / "Machines" / "supervisor-stamp_break.png",
    "runner_obey": M4 / "Characters" / "runner_obey-copier.png",
    "folder_normal": M4 / "Folders" / "folder_normal.png",
    "folder_rule": M4 / "Folders" / "folder_rule-matched.png",
    "folder_original": M4 / "Folders" / "folder_original.png",
    "folder_copy": M4 / "Folders" / "folder_copy_tier_2.png",
    "machine_stop": M4 / "VFX" / "machine-stop.png",
    "copy_clear": M4 / "VFX" / "copy-clear.png",
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def paste(canvas: Image.Image, name: str, box: tuple[int, int, int, int]) -> None:
    path = SOURCES[name]
    with Image.open(path) as source:
        source = source.convert("RGBA")
        fitted = ImageOps.contain(
            source,
            (box[2] - box[0], box[3] - box[1]),
            method=Image.Resampling.NEAREST,
        )
        x = box[0] + (box[2] - box[0] - fitted.width) // 2
        y = box[1] + (box[3] - box[1] - fitted.height) // 2
        canvas.alpha_composite(fitted, (x, y))


def arrow(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], colour: str, width: int) -> None:
    draw.line(points, fill=colour, width=width, joint="curve")
    x, y = points[-1]
    draw.polygon(((x, y), (x - 54, y - 38), (x - 54, y + 38)), fill=colour)


def relief_edit(base: Image.Image) -> Image.Image:
    canvas = base.copy()
    overlay = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    arrow(draw, [(540, 560), (700, 545), (805, 495), (940, 500), (1110, 540)], "#2f6b67", 26)
    draw.rounded_rectangle((790, 350, 1160, 630), radius=22, outline="#49c6c8", width=16)
    canvas.alpha_composite(overlay)
    paste(canvas, "auto_sorter_active", (790, 370, 955, 580))
    paste(canvas, "copy_echo_active", (960, 370, 1125, 580))
    for x, y, name in (
        (560, 510, "folder_normal"),
        (690, 500, "folder_normal"),
        (805, 450, "folder_rule"),
        (930, 455, "folder_rule"),
        (1080, 500, "folder_rule"),
    ):
        paste(canvas, name, (x, y, x + 70, y + 74))
    return canvas


def break_edit(base: Image.Image) -> Image.Image:
    canvas = base.copy()
    overlay = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    draw.rounded_rectangle((775, 300, 1210, 690), radius=28, fill=(21, 21, 26, 125), outline="#b53b38", width=24)
    fracture_paths = [
        [(1030, 465), (930, 420), (835, 470), (735, 420)],
        [(1020, 500), (920, 545), (815, 525), (710, 585)],
        [(1060, 520), (990, 610), (880, 635)],
    ]
    for points in fracture_paths:
        draw.line(points, fill="#15151a", width=48, joint="curve")
        draw.line(points, fill="#b53b38", width=26, joint="curve")
    canvas.alpha_composite(overlay)
    paste(canvas, "copy_echo_break", (850, 360, 1070, 620))
    paste(canvas, "supervisor_stamp_break", (1010, 320, 1185, 535))
    paste(canvas, "runner_obey", (755, 465, 910, 665))
    paste(canvas, "machine_stop", (790, 310, 900, 420))
    paste(canvas, "copy_clear", (900, 300, 1010, 410))
    paste(canvas, "folder_original", (655, 405, 755, 510))
    for x, y in ((760, 420), (820, 520), (930, 600), (1060, 560), (1110, 430), (720, 560)):
        paste(canvas, "folder_copy", (x, y, x + 85, y + 92))
    return canvas


def main() -> None:
    if not A01.is_file():
        raise FileNotFoundError(A01)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    with Image.open(A01) as opened:
        base = opened.convert("RGBA")
    outputs = {
        "A02_automation_relief_spatial_edit.png": relief_edit(base),
        "A03_promotion_cascade_break_spatial_edit.png": break_edit(base),
    }
    records = []
    for filename, image in outputs.items():
        path = OUTPUT / filename
        image.save(path, format="PNG", optimize=True)
        records.append({"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path)})
    manifest = {
        "schema": "desk42.office-slice-m6.1.spatial-edit-guides.v1",
        "method": "deterministic Pillow placement of approved M4 motifs over accepted A01 internal candidate",
        "base": {"path": A01.relative_to(ROOT).as_posix(), "sha256": sha256(A01)},
        "sources": [
            {"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path)}
            for path in SOURCES.values()
        ],
        "outputs": records,
    }
    (OUTPUT / "spatial-edit-manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )
    print("M6_1_SPATIAL_EDITS_OK", *(record["sha256"] for record in records))


if __name__ == "__main__":
    main()
