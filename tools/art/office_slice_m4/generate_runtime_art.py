"""Generate deterministic project-original Office Slice M4 runtime raster art.

This generator deliberately draws a small controlled palette rather than applying a
generic pixel filter. It creates reproducible candidates, approved sources, runtime
copies, a provenance ledger, and a hash manifest.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import shutil
from datetime import date
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
ARTLAB = ROOT / "ArtLab" / "OfficeSliceM4"
RUNTIME = ROOT / "Assets" / "_Project" / "Art" / "OfficeSliceM4"
RESOURCE_ROOT = RUNTIME / "Resources" / "OfficeSliceM4"
LEDGER = ARTLAB / "Provenance" / "asset-ledger.csv"
RUNTIME_MANIFEST = RUNTIME / "Config" / "runtime-asset-manifest.json"
GENERATOR_VERSION = "1.0.0"

P = {
    "cream": "#E8D9B5", "plaster": "#C7BFA7", "moss": "#66705B",
    "teal": "#2F6B67", "coffee": "#6C4E3D", "mint": "#B8D6B0",
    "amber": "#D8892B", "red": "#B53B38", "ink": "#15151A",
    "cyan": "#49C6C8", "violet": "#7B4A88", "clear": "#00000000",
}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def save_asset(asset_id: str, category: str, image: Image.Image,
               runtime_rel: str, records: list[dict], method="python-pixel-original"):
    candidate = ARTLAB / "Candidates" / runtime_rel
    approved = ARTLAB / "ApprovedSources" / runtime_rel
    runtime = RESOURCE_ROOT / runtime_rel
    for path in (candidate, approved, runtime):
        path.parent.mkdir(parents=True, exist_ok=True)
        image.save(path, optimize=False, compress_level=9)
    digest = sha(runtime)
    records[:] = [record for record in records if record["asset_id"] != asset_id]
    records.append({
        "asset_id": asset_id,
        "runtime_filename": runtime.relative_to(ROOT).as_posix(),
        "category": category,
        "authoring_method": method,
        "blender_source": "",
        "comfy_workflow": "",
        "model_checkpoint": "",
        "prompt_sha256": "",
        "negative_prompt_sha256": "",
        "seed": "420400",
        "control_guide": "",
        "source_reference": "tools/art/office_slice_m4/generate_runtime_art.py",
        "reference_licence": "PROJECT-ORIGINAL",
        "generation_date": date.today().isoformat(),
        "normaliser_version": "1.0.0",
        "final_sha256": digest,
        "reviewer_decision": "approved",
        "rejection_reason": "",
        "width": image.width,
        "height": image.height,
    })


def environment() -> Image.Image:
    im = Image.new("RGBA", (1600, 900), P["ink"])
    d = ImageDraw.Draw(im)
    d.rectangle((28, 26, 1572, 874), fill=P["coffee"], outline=P["cream"], width=8)
    d.rectangle((44, 42, 1556, 858), fill=P["plaster"])
    # Five unmistakable floor plates.
    rooms = [
        ((72, 90, 560, 410), "cream", "coffee"),
        ((80, 510, 600, 820), "moss", "mint"),
        ((600, 84, 1000, 410), "cream", "moss"),
        ((1040, 84, 1512, 410), "teal", "coffee"),
        ((1010, 500, 1510, 820), "violet", "ink"),
    ]
    for index, (box, fill, edge) in enumerate(rooms):
        d.rounded_rectangle(box, radius=20 + index * 2, fill=P[fill], outline=P[edge], width=12)
        x0, y0, x1, y1 = box
        for x in range(x0 + 30, x1 - 12, 46):
            d.line((x, y0 + 18, x, y1 - 18), fill=P[edge], width=2)
    # Front counter and six folder rail slots.
    d.polygon(((115, 175), (500, 175), (532, 250), (145, 250)), fill=P["coffee"], outline=P["ink"])
    for i in range(6):
        x = 155 + i * 56
        d.rectangle((x, 148, x + 38, 190), fill=P["cream"], outline=P["ink"], width=5)
    # Waiting area soft furniture.
    for x in (145, 280, 415):
        d.rounded_rectangle((x, 605, x + 90, 730), radius=28, fill=P["mint"], outline=P["ink"], width=7)
    d.ellipse((510, 620, 566, 728), fill=P["moss"], outline=P["ink"], width=6)
    # Paper shelves.
    for x in (640, 755, 870):
        d.rectangle((x, 128, x + 82, 335), fill=P["coffee"], outline=P["ink"], width=8)
        for y in range(165, 320, 42):
            d.line((x + 8, y, x + 74, y), fill=P["cream"], width=8)
    # Money room ledger grid and lamp.
    d.rectangle((1100, 150, 1445, 330), fill=P["coffee"], outline=P["ink"], width=8)
    for x in range(1130, 1425, 58): d.line((x, 170, x, 312), fill=P["teal"], width=5)
    d.polygon(((1260, 125), (1315, 125), (1340, 172), (1235, 172)), fill=P["mint"], outline=P["ink"])
    # Weird room diagonal tears, machine bays, impossible doorway.
    d.rectangle((1060, 575, 1195, 760), fill=P["teal"], outline=P["ink"], width=10)
    d.rectangle((1230, 575, 1370, 760), fill=P["red"], outline=P["ink"], width=10)
    d.polygon(((1410, 540), (1490, 570), (1465, 780), (1390, 750)), fill=P["ink"], outline=P["cyan"], width=8)
    for n in range(8):
        d.line((1025 + n * 58, 515, 1090 + n * 52, 800), fill=P["ink"], width=3)
    # Walkable central paper corridor.
    d.polygon(((560, 425), (1015, 425), (1090, 500), (970, 560), (615, 520)), fill=P["cream"], outline=P["coffee"], width=8)
    return im


def character(primary: str, accent: str, signature: int, mood: str = "calm") -> Image.Image:
    im = Image.new("RGBA", (96, 128), P["clear"])
    d = ImageDraw.Draw(im)
    # Contact shadow and legs share a stable bottom-centre anchor.
    d.ellipse((20, 112, 76, 124), fill=(21, 21, 26, 100))
    d.rectangle((34, 87, 46, 115), fill=P["ink"])
    d.rectangle((52, 87, 64, 115), fill=P["ink"])
    # Signature alters coat silhouette without changing anchor.
    shoulder = 18 + signature % 7
    hem = 78 - signature % 9
    d.polygon(((48, 39), (shoulder, 58), (25, hem), (34, 101), (62, 101), (72, hem), (78, 58)),
              fill=P[primary], outline=P["ink"])
    d.line((28, 67, 68, 67), fill=P[accent], width=7)
    d.ellipse((31, 12, 65, 48), fill=P["cream"], outline=P["ink"], width=5)
    # Hair/signature mark.
    if signature % 3 == 0:
        d.polygon(((28, 25), (35, 8), (66, 20), (61, 30)), fill=P[accent], outline=P["ink"])
    elif signature % 3 == 1:
        d.arc((26, 5, 70, 45), 185, 355, fill=P[accent], width=10)
    else:
        d.polygon(((27, 24), (39, 7), (51, 20), (65, 6), (69, 29)), fill=P[accent], outline=P["ink"])
    eye_y = 31
    d.rectangle((38, eye_y, 42, eye_y + 4), fill=P["ink"])
    d.rectangle((54, eye_y, 58, eye_y + 4), fill=P["ink"])
    if mood == "upset": d.line((40, 42, 56, 38), fill=P["red"], width=4)
    elif mood == "relieved": d.arc((40, 36, 57, 48), 10, 170, fill=P["mint"], width=3)
    elif mood == "concerned": d.line((42, 41, 55, 43), fill=P["amber"], width=3)
    else: d.line((42, 41, 55, 41), fill=P["coffee"], width=3)
    # Stable magenta anchor marker consumed by normaliser in source candidates only.
    im.putpixel((48, 127), (255, 0, 255, 255))
    im.putpixel((48, 127), (0, 0, 0, 0))
    return im


def folder(kind: str, accent: str) -> Image.Image:
    im = Image.new("RGBA", (80, 56), P["clear"])
    d = ImageDraw.Draw(im)
    if kind == "copy":
        d.polygon(((9, 15), (28, 15), (34, 9), (70, 9), (73, 47), (13, 51)), fill=P["plaster"], outline=P["ink"])
        d.polygon(((5, 10), (24, 10), (30, 4), (66, 4), (69, 42), (9, 46)), fill=P["cream"], outline=P["red"])
        d.line((18, 18, 55, 36), fill=P["red"], width=6)
    elif kind == "time-slip":
        d.polygon(((9, 8), (70, 8), (64, 49), (15, 49)), fill=P["cream"], outline=P["cyan"], width=5)
        d.arc((26, 16, 54, 44), 0, 359, fill=P["ink"], width=4)
        d.line((40, 30, 40, 20), fill=P["cyan"], width=3)
    elif kind == "promotion":
        d.polygon(((8, 8), (70, 8), (70, 48), (8, 48)), fill=P["cream"], outline=P["violet"], width=5)
        d.polygon(((39, 14), (45, 27), (59, 28), (48, 37), (52, 48), (39, 41), (26, 48), (30, 37), (19, 28), (33, 27)), fill=P["red"], outline=P["ink"])
    else:
        d.polygon(((7, 13), (27, 13), (34, 7), (72, 7), (72, 49), (7, 49)), fill=P["cream"], outline=P["ink"], width=5)
        d.rectangle((17, 22, 60, 34), fill=P[accent])
        d.rectangle((21, 25, 54, 28), fill=P["ink"])
    return im


def fallback() -> Image.Image:
    im = Image.new("RGBA", (64, 64), P["ink"])
    d = ImageDraw.Draw(im)
    d.rectangle((4, 4, 59, 59), outline=P["red"], width=6)
    d.line((10, 10, 54, 54), fill=P["red"], width=7)
    d.line((54, 10, 10, 54), fill=P["red"], width=7)
    return im


def prop(kind: str, primary: str, accent: str) -> Image.Image:
    im = Image.new("RGBA", (160, 160), P["clear"])
    d = ImageDraw.Draw(im)
    ink = P["ink"]
    if kind == "counter":
        d.polygon(((12, 54), (142, 54), (153, 82), (25, 82)), fill=P[primary], outline=ink)
        d.rectangle((24, 82, 142, 135), fill=P[accent], outline=ink, width=7)
        for x in (44, 78, 112): d.line((x, 88, x, 130), fill=P[primary], width=5)
    elif kind == "shelf":
        d.rectangle((25, 14, 136, 145), fill=P[accent], outline=ink, width=8)
        for y in (45, 78, 111):
            d.line((31, y, 130, y), fill=ink, width=6)
            for x in range(38, 125, 18): d.rectangle((x, y - 22, x + 10, y - 4), fill=P[primary])
    elif kind == "chair":
        d.rounded_rectangle((39, 20, 122, 103), radius=24, fill=P[primary], outline=ink, width=8)
        d.rectangle((32, 90, 129, 120), fill=P[accent], outline=ink, width=7)
        d.line((52, 119, 43, 148), fill=ink, width=8); d.line((110, 119, 119, 148), fill=ink, width=8)
    elif kind == "vault":
        d.rectangle((18, 18, 142, 142), fill=P[accent], outline=ink, width=10)
        d.ellipse((43, 43, 117, 117), fill=P[primary], outline=ink, width=8)
        d.line((80, 53, 80, 107), fill=ink, width=7); d.line((53, 80, 107, 80), fill=ink, width=7)
    elif kind == "door":
        d.polygon(((35, 22), (124, 12), (136, 147), (26, 137)), fill=P[primary], outline=P[accent], width=9)
        d.ellipse((100, 76, 111, 87), fill=P[accent])
        d.line((42, 35, 116, 126), fill=ink, width=4)
    elif kind == "tray":
        for n in range(3):
            y = 28 + n * 39
            d.polygon(((20, y), (139, y), (126, y + 31), (33, y + 31)), fill=P[primary], outline=ink)
            d.polygon(((125, y + 8), (150, y + 16), (128, y + 24)), fill=P[accent])
    elif kind == "label":
        d.polygon(((24, 45), (112, 45), (140, 80), (112, 115), (24, 115)), fill=P[primary], outline=ink)
        d.ellipse((105, 70, 122, 87), fill=P[accent])
    elif kind == "socket":
        d.ellipse((40, 40, 120, 120), outline=P[primary], width=10)
        d.polygon(((80, 27), (91, 61), (128, 61), (98, 82), (110, 121), (80, 97), (50, 121), (62, 82), (32, 61), (69, 61)), fill=P[accent])
    elif kind == "route":
        d.line((18, 126, 56, 88, 90, 105, 139, 39), fill=P[primary], width=14, joint="curve")
        d.polygon(((130, 24), (151, 27), (144, 50)), fill=P[accent])
    else:  # plant
        d.rectangle((57, 98, 104, 145), fill=P[accent], outline=ink, width=6)
        for box in ((22, 42, 80, 111), (69, 23, 128, 109), (48, 8, 103, 105)):
            d.ellipse(box, fill=P[primary], outline=ink, width=5)
    return im


def state_overlay(kind: str) -> Image.Image:
    im = Image.new("RGBA", (1600, 900), P["clear"])
    d = ImageDraw.Draw(im)
    if kind == "rush":
        for n in range(8):
            inset = 18 + n * 18
            d.line((inset, 32, inset + 90, 32), fill=P["amber"], width=8)
            d.line((1600 - inset, 868, 1510 - inset, 868), fill=P["amber"], width=8)
    elif kind == "break":
        for n in range(10):
            x = 45 + n * 165
            d.polygon(((x, 0), (x + 34, 78), (x + 6, 142), (x + 58, 218)), fill=P["ink"])
        d.rectangle((10, 10, 1590, 890), outline=P["red"], width=18)
    elif kind == "recovery":
        d.rectangle((12, 12, 1588, 888), outline=P["mint"], width=16)
        for x in range(90, 1550, 210):
            d.line((x, 838, x + 22, 862, x + 64, 812), fill=P["mint"], width=10)
    return im


def character_state(primary: str, accent: str, signature: int, state: str) -> Image.Image:
    mood = state if state in ("calm", "worried", "upset", "strange") else "calm"
    im = character(primary, accent, signature, mood)
    d = ImageDraw.Draw(im)
    if "carry" in state:
        d.polygon(((18, 65), (61, 65), (68, 93), (24, 96)), fill=P["cream"], outline=P["ink"])
        d.rectangle((29, 73, 57, 80), fill=P["red"])
    if state in ("interact", "work"):
        d.line((66, 63, 86, 48), fill=P["cream"], width=8)
        d.ellipse((80, 41, 92, 53), fill=P[accent], outline=P["ink"])
    if state in ("calm", "calm-customer"):
        d.arc((8, 42, 88, 121), 205, 335, fill=P["mint"], width=6)
    if state == "fix":
        d.line((69, 79, 88, 57), fill=P["cyan"], width=7)
        d.ellipse((78, 47, 95, 64), outline=P["ink"], width=5)
    if state == "help":
        d.polygon(((72, 48), (78, 60), (92, 61), (82, 70), (86, 84), (73, 77), (62, 84), (66, 70), (56, 61), (69, 60)), fill=P["amber"])
    if state in ("blocked", "stunned"):
        d.line((11, 54, 85, 54), fill=P["red"], width=8)
        d.line((16, 72, 80, 72), fill=P["ink"], width=7)
    if state == "obey-copier":
        d.polygon(((58, 47), (88, 38), (91, 66), (64, 73)), fill=P["red"], outline=P["ink"])
        d.line((74, 74, 93, 100), fill=P["violet"], width=7)
    if state == "return-to-warden":
        d.line((82, 38, 66, 30, 72, 48), fill=P["teal"], width=6)
    if state == "strange":
        d.line((15, 25, 3, 18), fill=P["violet"], width=5)
        d.line((78, 18, 92, 8), fill=P["cyan"], width=5)
    # Direction marker changes stance while preserving the bottom-centre anchor.
    if state.endswith("-left"): d.line((22, 88, 10, 103), fill=P[accent], width=6)
    elif state.endswith("-right"): d.line((74, 88, 88, 103), fill=P[accent], width=6)
    elif state.endswith("-up"): d.line((48, 52, 48, 31), fill=P[accent], width=6)
    elif state.endswith("-down"): d.polygon(((40, 92), (56, 92), (48, 105)), fill=P[accent])
    return im


def portrait(primary: str, accent: str, signature: int, mood: str) -> Image.Image:
    im = Image.new("RGBA", (192, 192), P["cream"])
    d = ImageDraw.Draw(im)
    d.rectangle((4, 4, 187, 187), outline=P["ink"], width=8)
    d.polygon(((28, 190), (42, 121), (96, 104), (150, 121), (166, 190)), fill=P[primary], outline=P["ink"])
    d.ellipse((46, 31, 146, 132), fill=P["plaster"], outline=P["ink"], width=8)
    if signature % 3 == 0:
        d.polygon(((39, 66), (52, 18), (96, 33), (144, 17), (151, 71)), fill=P[accent], outline=P["ink"])
    elif signature % 3 == 1:
        d.arc((35, 12, 154, 105), 185, 355, fill=P[accent], width=24)
    else:
        d.polygon(((40, 62), (61, 18), (97, 40), (128, 12), (153, 66)), fill=P[accent], outline=P["ink"])
    eye_y = 78
    d.rectangle((68, eye_y, 78, eye_y + 8), fill=P["ink"])
    d.rectangle((114, eye_y, 124, eye_y + 8), fill=P["ink"])
    if mood == "worried":
        d.arc((75, 99, 120, 132), 190, 350, fill=P["amber"], width=7)
    elif mood == "upset":
        d.line((70, 68, 83, 74), fill=P["red"], width=6); d.line((110, 74, 126, 67), fill=P["red"], width=6)
        d.line((78, 119, 116, 111), fill=P["red"], width=7)
    elif mood == "strange":
        d.ellipse((62, 70, 84, 92), outline=P["cyan"], width=6); d.ellipse((108, 70, 130, 92), outline=P["violet"], width=6)
        d.line((79, 116, 116, 116), fill=P["ink"], width=6)
    elif mood == "promotion-cascade":
        d.polygon(((95, 100), (106, 124), (133, 126), (112, 143), (120, 169), (95, 154), (70, 169), (78, 143), (57, 126), (84, 124)), fill=P["red"], outline=P["ink"])
    elif mood == "ghost-clock":
        d.arc((42, 26, 152, 137), 0, 359, fill=P["cyan"], width=8)
        d.line((98, 45, 98, 75), fill=P["cyan"], width=6); d.line((98, 75, 120, 91), fill=P["cyan"], width=6)
    else:
        d.arc((77, 96, 119, 126), 5, 175, fill=P["coffee"], width=6)
    return im


def gate_a(records: list[dict]):
    env = environment()
    save_asset("environment.office.base", "Environment", env, "Environment/office_background.png", records)
    save_asset("fallback.explicit", "Config", fallback(), "Config/explicit_fallback.png", records)
    essentials = [
        ("warden", "teal", "cream", 1), ("runner", "moss", "mint", 2),
        ("talker", "violet", "cream", 3), ("nia-bell", "amber", "coffee", 4),
    ]
    for name, primary, accent, signature in essentials:
        save_asset(f"character.{name}.idle", "Characters", character(primary, accent, signature),
                   f"Characters/{name}_idle.png", records)
    for i, accent in enumerate(("teal", "moss", "amber", "red", "cyan", "violet"), start=1):
        save_asset(f"folder.original.{i}", "Folders", folder("original", accent),
                   f"Folders/folder_original_{i}.png", records)
    target = env.copy()
    target.alpha_composite(character("teal", "cream", 1).resize((144, 192), Image.Resampling.NEAREST), (720, 430))
    target.alpha_composite(character("amber", "coffee", 4).resize((144, 192), Image.Resampling.NEAREST), (245, 215))
    target.alpha_composite(character("moss", "mint", 2).resize((144, 192), Image.Resampling.NEAREST), (560, 555))
    target.alpha_composite(character("violet", "cream", 3).resize((144, 192), Image.Resampling.NEAREST), (775, 565))
    approved_target = ARTLAB / "ApprovedSources" / "TargetFrames" / "shift1_opening_target.png"
    approved_target.parent.mkdir(parents=True, exist_ok=True)
    target.save(approved_target, optimize=False, compress_level=9)


def gate_b(records: list[dict]):
    kit = [
        ("environment.kit.counter", "counter", "coffee", "cream"),
        ("environment.kit.shelf", "shelf", "cream", "coffee"),
        ("environment.kit.chair", "chair", "mint", "moss"),
        ("environment.kit.vault", "vault", "teal", "coffee"),
        ("environment.kit.impossible-door", "door", "ink", "cyan"),
        ("environment.kit.plant", "plant", "moss", "coffee"),
        ("environment.upgrade.fast-trays", "tray", "teal", "mint"),
        ("environment.upgrade.calm-chairs", "chair", "mint", "amber"),
        ("environment.upgrade.red-labels", "label", "red", "cream"),
        ("environment.interaction.socket", "socket", "amber", "cream"),
        ("environment.route.overlay", "route", "teal", "cream"),
    ]
    for asset_id, kind, primary, accent in kit:
        filename = asset_id.replace("environment.", "").replace(".", "_") + ".png"
        save_asset(asset_id, "Environment", prop(kind, primary, accent),
                   "Environment/" + filename, records)
    for kind in ("rush", "break", "recovery"):
        save_asset(f"environment.state.{kind}", "Environment", state_overlay(kind),
                   f"Environment/state_{kind}.png", records)
    # Shift dressing is deliberately symbolic and contains no generated text.
    save_asset("environment.shift.2-dressing", "Environment", prop("plant", "cyan", "coffee"),
               "Environment/shift_2_dressing.png", records)
    save_asset("environment.shift.3-dressing", "Environment", prop("label", "violet", "red"),
               "Environment/shift_3_dressing.png", records)


def gate_c(records: list[dict]):
    cast = {
        "warden": ("teal", "red", 1),
        "runner": ("moss", "mint", 2),
        "talker": ("violet", "cream", 3),
    }
    warden_states = ["idle"] + [f"walk-{d}" for d in ("up", "down", "left", "right")] + \
        [f"carry-walk-{d}" for d in ("up", "down", "left", "right")] + \
        ["interact", "calm", "fix", "help", "stunned"]
    runner_states = ["idle"] + [f"walk-{d}" for d in ("up", "down", "left", "right")] + \
        ["carry", "work", "blocked", "obey-copier", "return-to-warden"]
    talker_states = ["idle"] + [f"walk-{d}" for d in ("up", "down", "left", "right")] + \
        ["calm-customer", "blocked", "work"]
    for role, states in (("warden", warden_states), ("runner", runner_states), ("talker", talker_states)):
        primary, accent, signature = cast[role]
        for state in states:
            save_asset(f"character.{role}.{state}", "Characters",
                       character_state(primary, accent, signature, state),
                       f"Characters/{role}_{state}.png", records)

    customers = {
        "nia-bell": ("amber", "coffee", 4),
        "owen-pike": ("moss", "cream", 5),
        "mara-vale": ("red", "violet", 6),
        "iris-cole": ("cyan", "teal", 7),
        "tomas-reed": ("coffee", "cyan", 8),
        "june-hart": ("cream", "red", 9),
    }
    for name, (primary, accent, signature) in customers.items():
        for mood in ("calm", "worried", "upset", "strange"):
            save_asset(f"character.{name}.{mood}", "Characters",
                       character_state(primary, accent, signature, mood),
                       f"Characters/{name}_{mood}.png", records)
            save_asset(f"portrait.{name}.{mood}", "Portraits",
                       portrait(primary, accent, signature, mood),
                       f"Characters/Portraits/{name}_{mood}.png", records)
    save_asset("portrait.mara-vale.promotion-cascade", "Portraits",
               portrait("red", "violet", 6, "promotion-cascade"),
               "Characters/Portraits/mara-vale_promotion-cascade.png", records)
    save_asset("portrait.tomas-reed.ghost-clock", "Portraits",
               portrait("coffee", "cyan", 8, "ghost-clock"),
               "Characters/Portraits/tomas-reed_ghost-clock.png", records)


def write_outputs(records: list[dict]):
    columns = [
        "asset_id", "runtime_filename", "category", "authoring_method", "blender_source",
        "comfy_workflow", "model_checkpoint", "prompt_sha256", "negative_prompt_sha256", "seed",
        "control_guide", "source_reference", "reference_licence", "generation_date",
        "normaliser_version", "final_sha256", "reviewer_decision", "rejection_reason",
    ]
    LEDGER.parent.mkdir(parents=True, exist_ok=True)
    with LEDGER.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=columns, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(sorted(records, key=lambda row: row["asset_id"]))
    RUNTIME_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    RUNTIME_MANIFEST.write_text(json.dumps({
        "schema": "desk42.office-slice-m4.runtime-assets.v1",
        "generator_version": GENERATOR_VERSION,
        "assets": sorted(records, key=lambda row: row["asset_id"]),
    }, indent=2) + "\n", encoding="utf-8")
    print("OFFICE_SLICE_M4_RUNTIME_ART_OK", len(records), sha(RUNTIME_MANIFEST))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--gate", choices=("A", "B", "C"), default="A")
    args = parser.parse_args()
    records: list[dict] = []
    gate_a(records)
    if args.gate in ("B", "C"): gate_b(records)
    if args.gate == "C": gate_c(records)
    write_outputs(records)


if __name__ == "__main__":
    main()
