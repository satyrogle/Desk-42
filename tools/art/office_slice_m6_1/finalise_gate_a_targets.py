"""Apply deterministic Gate A physical-storytelling cleanup to local Krea 2 frames."""
from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[3]
ARTLAB = ROOT / "ArtLab" / "OfficeSliceM6_1"
RAW = ARTLAB / "Candidates" / "TargetFrames"
FINAL = ARTLAB / "Candidates" / "PresentationTargets"
REJECTED = ARTLAB / "Candidates" / "Rejected"
EXECUTION_MANIFEST = ARTLAB / "Provenance" / "execution-manifest.json"
FINALISATION_MANIFEST = ARTLAB / "Provenance" / "finalisation-manifest.json"

SEED = 6101001
A01_LOCKED_SHA256 = "39ccd5f354e8339068399c5ca1cf1aaf6d4acbd43923000ce604b550098b0efb"
RAW_FRAMES = {
    "A01": RAW / f"A01_calm_office_seed{SEED}.png",
    "A02": RAW / f"A02_automation_relief_seed{SEED}.png",
    "A03": RAW / f"A03_promotion_cascade_break_seed{SEED}.png",
}

INK = "#15151a"
CREAM = "#e8d9b5"
WARM = "#c7bfa7"
MOSS = "#66705b"
TEAL = "#2f6b67"
COFFEE = "#6c4e3d"
MINT = "#b8d6b0"
AMBER = "#d8892b"
RED = "#b53b38"
CYAN = "#49c6c8"
VIOLET = "#7b4a88"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def composite(canvas: Image.Image, layer: Image.Image, softness: float = 0.25) -> None:
    if softness:
        layer = layer.filter(ImageFilter.GaussianBlur(softness))
    canvas.alpha_composite(layer)


def painterly_copier_cutout(colour: str, scale: float = 1.0) -> Image.Image:
    """Reuse the physically integrated A03 copier as painterly machine source."""
    with Image.open(RAW_FRAMES["A03"]) as opened:
        source = opened.convert("RGBA")
    crop_box = (945, 415, 1095, 575)
    crop = source.crop(crop_box)
    mask = Image.new("L", crop.size, 0)
    mask_draw = ImageDraw.Draw(mask)
    mask_draw.polygon(
        [(18, 44), (95, 18), (126, 27), (145, 67), (136, 126), (105, 145), (26, 133), (6, 83)],
        fill=255,
    )
    mask = mask.filter(ImageFilter.GaussianBlur(2.0))
    target = tuple(int(colour[index : index + 2], 16) for index in (1, 3, 5))
    pixels = crop.load()
    for y in range(crop.height):
        for x in range(crop.width):
            red, green, blue, alpha = pixels[x, y]
            if red > 95 and red > green * 1.18 and red > blue * 1.12:
                luminance = max(0.55, min(1.25, (red + green + blue) / 360.0))
                pixels[x, y] = (
                    min(255, int(target[0] * luminance)),
                    min(255, int(target[1] * luminance)),
                    min(255, int(target[2] * luminance)),
                    alpha,
                )
    crop.putalpha(mask)
    if scale != 1.0:
        crop = crop.resize(
            (max(1, int(crop.width * scale)), max(1, int(crop.height * scale))),
            Image.Resampling.LANCZOS,
        )
    return crop


def polygon(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], fill: str, width: int = 4) -> None:
    draw.polygon(points, fill=fill)
    draw.line([*points, points[0]], fill=INK, width=width, joint="curve")


def folder_sprite(scale: float, copied: bool = False, original: bool = False) -> Image.Image:
    width = max(28, int(58 * scale))
    height = max(22, int(42 * scale))
    image = Image.new("RGBA", (width + 14, height + 14), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    body = [(6, 11), (width - 16, 11), (width - 10, 6), (width + 4, 6), (width + 4, height), (6, height)]
    draw.polygon([(x + 3, y + 4) for x, y in body], fill=(21, 21, 26, 65))
    polygon(draw, body, CREAM, max(1, int(1.5 * scale)))
    if copied:
        draw.line((10, 15, width - 2, height - 5), fill=RED, width=max(2, int(2.5 * scale)))
        draw.rectangle((7, 9, 12, height - 3), fill=RED)
    elif original:
        seal_x, seal_y = width - 7, height - 8
        radius = max(3, int(4 * scale))
        draw.ellipse((seal_x - radius, seal_y - radius, seal_x + radius, seal_y + radius), fill=AMBER, outline=INK, width=1)
        draw.line((12, 20, width - 18, 20), fill=COFFEE, width=max(1, int(2 * scale)))
    else:
        draw.line((12, 20, width - 15, 20), fill=TEAL, width=max(2, int(2 * scale)))
    return image


def place_folder(
    canvas: Image.Image,
    centre: tuple[int, int],
    scale: float = 1.0,
    angle: float = 0.0,
    copied: bool = False,
    original: bool = False,
) -> None:
    folder = folder_sprite(scale, copied=copied, original=original)
    if angle:
        folder = folder.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)
    x = int(centre[0] - folder.width / 2)
    y = int(centre[1] - folder.height / 2)
    canvas.alpha_composite(folder, (x, y))


def draw_worker(
    layer: Image.Image,
    x: int,
    y: int,
    coat: str,
    facing_right: bool,
    carrying_copies: bool = False,
    upset: bool = False,
) -> None:
    draw = ImageDraw.Draw(layer)
    draw.ellipse((x - 20, y + 48, x + 23, y + 61), fill=(21, 21, 26, 70))
    draw.ellipse((x - 10, y, x + 12, y + 22), fill="#c89b76", outline=INK, width=2)
    body = [(x - 13, y + 20), (x + 14, y + 20), (x + 20, y + 49), (x - 18, y + 49)]
    polygon(draw, body, coat, 2)
    draw.line((x - 8, y + 48, x - 10, y + 59), fill=INK, width=3)
    draw.line((x + 9, y + 48, x + 11, y + 59), fill=INK, width=3)
    hand_x = x + 22 if facing_right else x - 22
    draw.line((x + (10 if facing_right else -10), y + 28, hand_x, y + 37), fill=INK, width=3)
    if carrying_copies:
        for offset in (0, 5, 10):
            folder = folder_sprite(0.48, copied=True)
            layer.alpha_composite(folder, (hand_x - 10 + offset, y + 22 - offset))
    if upset:
        draw.line((x - 8, y - 5, x - 15, y - 12), fill=RED, width=3)
        draw.line((x + 8, y - 5, x + 15, y - 12), fill=RED, width=3)


def draw_sorter(layer: Image.Image, x: int, y: int) -> None:
    draw = ImageDraw.Draw(layer)
    draw.ellipse((x - 24, y + 86, x + 145, y + 111), fill=(21, 21, 26, 65))
    polygon(draw, [(x, y + 17), (x + 102, y), (x + 132, y + 24), (x + 120, y + 91), (x + 8, y + 99)], TEAL, 5)
    polygon(draw, [(x + 102, y), (x + 132, y + 24), (x + 120, y + 91), (x + 101, y + 70)], "#245653", 4)
    polygon(draw, [(x + 18, y + 28), (x + 92, y + 18), (x + 102, y + 39), (x + 25, y + 49)], INK, 3)
    polygon(draw, [(x + 28, y + 31), (x + 85, y + 24), (x + 91, y + 35), (x + 33, y + 42)], CREAM, 2)
    polygon(draw, [(x - 28, y + 56), (x + 20, y + 47), (x + 26, y + 72), (x - 20, y + 81)], AMBER, 3)
    polygon(draw, [(x + 112, y + 57), (x + 156, y + 65), (x + 151, y + 87), (x + 108, y + 78)], MINT, 3)
    draw.ellipse((x + 93, y + 50, x + 106, y + 63), fill=MINT, outline=INK, width=2)


def draw_copy_machine(layer: Image.Image, x: int, y: int, broken: bool = False) -> None:
    draw = ImageDraw.Draw(layer)
    draw.ellipse((x - 30, y + 101, x + 176, y + 132), fill=(21, 21, 26, 78))
    body_colour = VIOLET if not broken else RED
    polygon(draw, [(x, y + 35), (x + 128, y + 14), (x + 158, y + 40), (x + 143, y + 112), (x + 10, y + 123)], body_colour, 6)
    polygon(draw, [(x + 128, y + 14), (x + 158, y + 40), (x + 143, y + 112), (x + 121, y + 91)], "#5d3869" if not broken else "#8f302f", 4)
    polygon(draw, [(x + 16, y + 20), (x + 124, y), (x + 143, y + 19), (x + 34, y + 42)], INK, 5)
    polygon(draw, [(x + 29, y + 22), (x + 116, y + 7), (x + 127, y + 17), (x + 39, y + 33)], CREAM, 2)
    polygon(draw, [(x + 22, y + 69), (x + 112, y + 57), (x + 120, y + 82), (x + 31, y + 96)], INK, 3)
    polygon(draw, [(x + 35, y + 70), (x + 104, y + 62), (x + 108, y + 76), (x + 40, y + 85)], CREAM, 2)
    indicator = RED if broken else MINT
    draw.ellipse((x + 126, y + 50, x + 140, y + 64), fill=indicator, outline=INK, width=2)


def draw_stamp(layer: Image.Image, x: int, y: int) -> None:
    draw = ImageDraw.Draw(layer)
    draw.ellipse((x - 10, y + 75, x + 105, y + 94), fill=(21, 21, 26, 80))
    polygon(draw, [(x, y + 57), (x + 77, y + 45), (x + 92, y + 63), (x + 14, y + 77)], COFFEE, 4)
    polygon(draw, [(x + 29, y + 9), (x + 54, y + 4), (x + 67, y + 48), (x + 39, y + 53)], RED, 4)
    draw.ellipse((x + 26, y - 4, x + 57, y + 20), fill=RED, outline=INK, width=4)


def draw_recovery_bin(layer: Image.Image, x: int, y: int) -> None:
    draw = ImageDraw.Draw(layer)
    draw.ellipse((x - 5, y + 45, x + 77, y + 61), fill=(21, 21, 26, 65))
    polygon(draw, [(x, y + 7), (x + 66, y), (x + 59, y + 51), (x + 9, y + 56)], MOSS, 4)
    polygon(draw, [(x - 3, y), (x + 63, y - 8), (x + 74, y + 3), (x + 5, y + 13)], INK, 3)
    place_folder(layer, (x + 30, y + 5), 0.48, -8, copied=True)


def finalise_relief(raw: Image.Image) -> Image.Image:
    canvas = raw.copy()
    physical = Image.new("RGBA", canvas.size, (0, 0, 0, 0))

    # Reuse the painterly copier integrated by Krea in A03 so A02's physical
    # machines inherit the office's line, wear, perspective and lighting.
    sorter = painterly_copier_cutout(TEAL, 0.78)
    copy_echo = painterly_copier_cutout(VIOLET, 0.92)
    physical.alpha_composite(sorter, (812, 452))
    physical.alpha_composite(copy_echo, (966, 423))

    # Folders rest on input/output surfaces and in a collected stack. There is
    # deliberately no route line, arrow, icon enclosure or floating annotation.
    for centre, angle in (((802, 523), -8), ((826, 515), -4), ((936, 521), 4)):
        place_folder(physical, centre, 0.46, angle)
    for index, centre in enumerate(((1110, 532), (1115, 526), (1120, 520))):
        place_folder(physical, centre, 0.44, -3 + index)

    # A small tidy output stack lands on the staff desk. The accepted Krea frame
    # already leaves the Warden helping elsewhere and the corridor breathing.
    for index in range(3):
        place_folder(physical, (1065 + index * 4, 394 - index * 3), 0.38, -3)

    composite(canvas, physical, 0.55)
    return canvas


def finalise_break(raw: Image.Image) -> Image.Image:
    canvas = raw.copy()
    physical = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(physical)

    # Local architectural damage grows out of the promoted copier. These are
    # fractures in the floor and wall, never arrows or a causal flowchart.
    fractures = [
        [(1008, 487), (980, 470), (958, 444), (939, 420)],
        [(1035, 492), (1080, 473), (1121, 448), (1153, 416)],
        [(1015, 520), (990, 548), (967, 580), (947, 616)],
        [(1068, 519), (1097, 548), (1128, 578)],
    ]
    for points in fractures:
        draw.line(points, fill=INK, width=5, joint="curve")
        draw.line(points, fill=RED, width=2, joint="curve")

    # Krea's physically integrated red copier remains authoritative. Add only a
    # literal dominant rubber stamp resting on its housing.
    draw_stamp(physical, 1038, 392)

    # A physical stop lever mounted beside the copier and a clearable copy bin
    # remain reachable inside the room.
    polygon(draw, [(939, 488), (950, 486), (955, 534), (943, 536)], TEAL, 2)
    draw.line((947, 493, 928, 462), fill=INK, width=5)
    draw.line((947, 493, 928, 462), fill=RED, width=3)
    draw.ellipse((921, 454, 935, 468), fill=RED, outline=INK, width=2)
    draw_recovery_bin(physical, 1083, 533)

    # Runner visibly faces and feeds the copier. An upset customer stands at the
    # threshold instead of being represented by an abstract status symbol.
    draw_worker(physical, 889, 502, MOSS, facing_right=True, carrying_copies=True)
    draw_worker(physical, 810, 457, VIOLET, facing_right=True, upset=True)

    # Duplicates physically spill out of the copier and across the Weird Room
    # threshold. The untouched original is a cream folder with an amber seal,
    # isolated in open floor space so it remains recoverable.
    copy_positions = [
        ((932, 548), -18, 0.52),
        ((974, 565), 11, 0.50),
        ((1020, 577), -8, 0.54),
        ((1065, 594), 16, 0.48),
        ((1110, 606), -13, 0.46),
        ((885, 584), 9, 0.47),
        ((845, 611), -15, 0.44),
        ((805, 582), 13, 0.42),
        ((775, 545), -7, 0.40),
    ]
    for centre, angle, scale in copy_positions:
        place_folder(physical, centre, scale, angle, copied=True)
    place_folder(physical, (787, 500), 0.60, -5, original=True)

    composite(canvas, physical, 0.6)
    return canvas


def archive_rejected(path: Path, frame_id: str) -> dict[str, str] | None:
    if not path.is_file():
        return None
    prior_hash = sha256(path)
    REJECTED.mkdir(parents=True, exist_ok=True)
    archived = REJECTED / f"{frame_id}_owner_rejected_{prior_hash[:8]}.png"
    if not archived.exists():
        shutil.copy2(path, archived)
    return {
        "id": frame_id,
        "candidate": archived.relative_to(ROOT).as_posix(),
        "candidate_sha256": prior_hash,
        "reason": "Owner rejected floating schematic/UI graphics; retained before physical-storytelling revision.",
    }


def main() -> None:
    for path in [*RAW_FRAMES.values(), EXECUTION_MANIFEST]:
        if not path.is_file():
            raise FileNotFoundError(path)
    if sha256(RAW_FRAMES["A01"]) != A01_LOCKED_SHA256:
        raise AssertionError("Approved A01 source changed; refusing to regenerate or replace it")

    FINAL.mkdir(parents=True, exist_ok=True)
    archived_rejections = []
    for frame_id in ("A02", "A03"):
        destination = FINAL / RAW_FRAMES[frame_id].name
        archived = archive_rejected(destination, frame_id)
        if archived:
            archived_rejections.append(archived)

    outputs: dict[str, Path] = {}
    for frame_id, raw_path in RAW_FRAMES.items():
        destination = FINAL / raw_path.name
        if frame_id == "A01":
            shutil.copy2(raw_path, destination)
            if sha256(destination) != A01_LOCKED_SHA256:
                raise AssertionError("Approved A01 presentation target is not byte-identical")
        else:
            with Image.open(raw_path) as opened:
                image = opened.convert("RGBA")
            final = finalise_relief(image) if frame_id == "A02" else finalise_break(image)
            final.convert("RGB").save(destination, format="PNG", optimize=True)
        outputs[frame_id] = destination

    execution = json.loads(EXECUTION_MANIFEST.read_text(encoding="utf-8"))
    frame_lookup = {frame["id"]: frame for frame in execution["frames"]}
    execution.setdefault("rejections", [])
    known_rejection_hashes = {item.get("candidate_sha256") for item in execution["rejections"]}
    execution["rejections"].extend(
        item for item in archived_rejections if item["candidate_sha256"] not in known_rejection_hashes
    )

    final_records = []
    for frame_id, path in outputs.items():
        method = "unchanged-krea-output-owner-approved-locked" if frame_id == "A01" else (
            "krea-output-plus-deterministic-project-original-physical-automation-staging"
            if frame_id == "A02"
            else "krea-output-plus-deterministic-project-original-physical-break-staging"
        )
        record = {
            "id": frame_id,
            "raw_krea_candidate": RAW_FRAMES[frame_id].relative_to(ROOT).as_posix(),
            "raw_krea_sha256": sha256(RAW_FRAMES[frame_id]),
            "presentation_target": path.relative_to(ROOT).as_posix(),
            "presentation_target_sha256": sha256(path),
            "method": method,
        }
        final_records.append(record)
        frame = frame_lookup[frame_id]
        frame["presentation_target"] = record["presentation_target"]
        frame["presentation_target_sha256"] = record["presentation_target_sha256"]
        if frame_id == "A01":
            frame["owner_decision"] = "approved"
            frame["review_status"] = "owner-approved-gate-a-partial-do-not-regenerate"
        else:
            frame["owner_decision"] = "revision-required"
            frame["review_status"] = "revised-presentation-target-awaiting-owner-approval"
    EXECUTION_MANIFEST.write_text(json.dumps(execution, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    manifest = {
        "schema": "desk42.office-slice-m6.1.gate-a-finalisation.v2",
        "script": "tools/art/office_slice_m6_1/finalise_gate_a_targets.py",
        "locked_approved_frame": {"id": "A01", "sha256": A01_LOCKED_SHA256},
        "owner_rejections": archived_rejections,
        "outputs": final_records,
        "status": "revision-awaiting-owner-approval-gate-b-blocked",
    }
    FINALISATION_MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("M6_1_GATE_A_TARGETS_REVISED", *(record["presentation_target_sha256"] for record in final_records))


if __name__ == "__main__":
    main()
