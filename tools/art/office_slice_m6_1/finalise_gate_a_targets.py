"""Apply deterministic Gate A readability cleanup to the local Krea 2 frames."""
from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[3]
ARTLAB = ROOT / "ArtLab" / "OfficeSliceM6_1"
RAW = ARTLAB / "Candidates" / "TargetFrames"
FINAL = ARTLAB / "Candidates" / "PresentationTargets"
M4 = ROOT / "ArtLab" / "OfficeSliceM4" / "ApprovedSources"
EXECUTION_MANIFEST = ARTLAB / "Provenance" / "execution-manifest.json"
FINALISATION_MANIFEST = ARTLAB / "Provenance" / "finalisation-manifest.json"

SEED = 6101001
RAW_FRAMES = {
    "A01": RAW / f"A01_calm_office_seed{SEED}.png",
    "A02": RAW / f"A02_automation_relief_seed{SEED}.png",
    "A03": RAW / f"A03_promotion_cascade_break_seed{SEED}.png",
}

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


def paste(canvas: Image.Image, name: str, box: tuple[int, int, int, int], soften: bool = False) -> None:
    path = SOURCES[name]
    with Image.open(path) as opened:
        source = opened.convert("RGBA")
        fitted = ImageOps.contain(
            source,
            (box[2] - box[0], box[3] - box[1]),
            method=Image.Resampling.NEAREST,
        )
        if soften:
            fitted = fitted.filter(ImageFilter.GaussianBlur(0.25))
        x = box[0] + (box[2] - box[0] - fitted.width) // 2
        y = box[1] + (box[3] - box[1] - fitted.height) // 2
        canvas.alpha_composite(fitted, (x, y))


def arrow_head(draw: ImageDraw.ImageDraw, x: int, y: int, colour: str) -> None:
    draw.polygon(((x, y), (x - 26, y - 18), (x - 26, y + 18)), fill=colour)


def finalise_relief(raw: Image.Image) -> Image.Image:
    canvas = raw.copy()
    overlay = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    path = [(585, 545), (710, 525), (815, 485), (930, 495), (1100, 525)]
    draw.line(path, fill="#15151a", width=22, joint="curve")
    draw.line(path, fill="#49c6c8", width=12, joint="curve")
    arrow_head(draw, 1100, 525, "#49c6c8")
    draw.ellipse((810, 545, 955, 590), fill=(21, 21, 26, 95))
    draw.ellipse((980, 545, 1125, 590), fill=(21, 21, 26, 95))
    canvas.alpha_composite(overlay)
    paste(canvas, "auto_sorter_active", (815, 405, 950, 570), soften=True)
    paste(canvas, "copy_echo_active", (985, 405, 1120, 570), soften=True)
    for x, y, name in (
        (565, 505, "folder_normal"),
        (680, 495, "folder_normal"),
        (790, 455, "folder_rule"),
        (900, 462, "folder_rule"),
        (1060, 495, "folder_rule"),
    ):
        paste(canvas, name, (x, y, x + 54, y + 58), soften=True)
    return canvas


def finalise_break(raw: Image.Image) -> Image.Image:
    canvas = raw.copy()
    overlay = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    draw.polygon(
        ((820, 345), (1165, 330), (1190, 590), (1110, 650), (980, 625), (875, 660), (805, 545)),
        fill=(21, 21, 26, 72),
    )
    fracture_paths = [
        [(1030, 475), (950, 430), (865, 470), (770, 430)],
        [(1025, 510), (930, 545), (845, 525), (750, 575)],
        [(1070, 530), (1010, 610), (910, 635)],
    ]
    for points in fracture_paths:
        draw.line(points, fill="#15151a", width=30, joint="curve")
        draw.line(points, fill="#b53b38", width=14, joint="curve")
    canvas.alpha_composite(overlay)
    paste(canvas, "copy_echo_break", (925, 410, 1060, 575), soften=True)
    paste(canvas, "supervisor_stamp_break", (1055, 365, 1170, 505), soften=True)
    paste(canvas, "runner_obey", (805, 480, 895, 610), soften=True)
    paste(canvas, "machine_stop", (835, 355, 910, 425), soften=True)
    paste(canvas, "copy_clear", (915, 350, 990, 420), soften=True)

    highlight = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    highlight_draw = ImageDraw.Draw(highlight)
    highlight_draw.ellipse((690, 400, 775, 490), fill=(216, 137, 43, 105), outline="#d8892b", width=7)
    canvas.alpha_composite(highlight)
    paste(canvas, "folder_original", (700, 410, 765, 480), soften=True)
    for x, y in ((785, 445), (845, 525), (935, 590), (1045, 565), (1100, 465), (765, 555)):
        paste(canvas, "folder_copy", (x, y, x + 52, y + 58), soften=True)
    return canvas


def main() -> None:
    for path in [*RAW_FRAMES.values(), *SOURCES.values(), EXECUTION_MANIFEST]:
        if not path.is_file():
            raise FileNotFoundError(path)
    FINAL.mkdir(parents=True, exist_ok=True)
    outputs = {}
    for frame_id, raw_path in RAW_FRAMES.items():
        destination = FINAL / raw_path.name
        if frame_id == "A01":
            shutil.copy2(raw_path, destination)
        else:
            with Image.open(raw_path) as opened:
                image = opened.convert("RGBA")
            final = finalise_relief(image) if frame_id == "A02" else finalise_break(image)
            final.convert("RGB").save(destination, format="PNG", optimize=True)
        outputs[frame_id] = destination

    execution = json.loads(EXECUTION_MANIFEST.read_text(encoding="utf-8"))
    frame_lookup = {frame["id"]: frame for frame in execution["frames"]}
    final_records = []
    for frame_id, path in outputs.items():
        record = {
            "id": frame_id,
            "raw_krea_candidate": RAW_FRAMES[frame_id].relative_to(ROOT).as_posix(),
            "raw_krea_sha256": sha256(RAW_FRAMES[frame_id]),
            "presentation_target": path.relative_to(ROOT).as_posix(),
            "presentation_target_sha256": sha256(path),
            "method": "unchanged-krea-output" if frame_id == "A01" else "krea-output-plus-deterministic-project-original-readability-cleanup",
        }
        final_records.append(record)
        frame_lookup[frame_id]["presentation_target"] = record["presentation_target"]
        frame_lookup[frame_id]["presentation_target_sha256"] = record["presentation_target_sha256"]
        frame_lookup[frame_id]["review_status"] = "presentation-target-awaiting-gate-a-owner-approval"
    EXECUTION_MANIFEST.write_text(json.dumps(execution, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    manifest = {
        "schema": "desk42.office-slice-m6.1.gate-a-finalisation.v1",
        "script": "tools/art/office_slice_m6_1/finalise_gate_a_targets.py",
        "sources": [
            {"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path)}
            for path in SOURCES.values()
        ],
        "outputs": final_records,
        "status": "awaiting-owner-approval",
    }
    FINALISATION_MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("M6_1_GATE_A_TARGETS_FINALISED", *(record["presentation_target_sha256"] for record in final_records))


if __name__ == "__main__":
    main()
