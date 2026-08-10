"""Build and render the locked Office Slice M4 Blender guide scene.

Run with:
  blender --background --python tools/art/office_slice_m4/render_guides.py
The script replaces only the M4 guide scene and generated guide renders.
"""
from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[3]
BLENDER_DIR = ROOT / "ArtLab" / "OfficeSliceM4" / "Blender"
GUIDE_DIR = ROOT / "ArtLab" / "OfficeSliceM4" / "References" / "BlenderGuides"
BLEND_PATH = BLENDER_DIR / "office_slice_m4_master.blend"

PALETTE = {
    "cream": (0xE8 / 255, 0xD9 / 255, 0xB5 / 255, 1),
    "plaster": (0xC7 / 255, 0xBF / 255, 0xA7 / 255, 1),
    "moss": (0x66 / 255, 0x70 / 255, 0x5B / 255, 1),
    "teal": (0x2F / 255, 0x6B / 255, 0x67 / 255, 1),
    "coffee": (0x6C / 255, 0x4E / 255, 0x3D / 255, 1),
    "amber": (0xD8 / 255, 0x89 / 255, 0x2B / 255, 1),
    "red": (0xB5 / 255, 0x3B / 255, 0x38 / 255, 1),
    "ink": (0x15 / 255, 0x15 / 255, 0x1A / 255, 1),
    "cyan": (0x49 / 255, 0xC6 / 255, 0xC8 / 255, 1),
    "violet": (0x7B / 255, 0x4A / 255, 0x88 / 255, 1),
}


def material(name: str, colour: tuple[float, float, float, float]):
    item = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    item.diffuse_color = colour
    item.use_nodes = True
    bsdf = item.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = colour
    bsdf.inputs["Roughness"].default_value = 0.9
    return item


def cube(name: str, location, scale, colour: str, collection):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(material("M4_" + colour, PALETTE[colour]))
    for prior in list(obj.users_collection):
        prior.objects.unlink(obj)
    collection.objects.link(obj)
    return obj


def build_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)
    root = bpy.context.scene.collection
    default = bpy.data.collections.get("Collection")
    default.name = "M4_GUIDE_ROOT"

    departments = {}
    for name in ["FRONT_DESK", "WAITING_AREA", "PAPER_ROOM", "MONEY_ROOM", "WEIRD_ROOM", "CAST"]:
        col = bpy.data.collections.new(name)
        root.children.link(col)
        departments[name] = col

    cube("Office_Floor", (0, 0, -0.3), (14.5, 9.5, 0.25), "plaster", default)
    specs = [
        ("FRONT_DESK", (-9, 5, 0), (4.2, 2.5, 0.15), "cream"),
        ("WAITING_AREA", (-1, -3.5, 0), (3.4, 2.5, 0.15), "moss"),
        ("PAPER_ROOM", (0, 5, 0), (3.3, 2.5, 0.15), "cream"),
        ("MONEY_ROOM", (8.5, 5, 0), (2.8, 2.5, 0.15), "teal"),
        ("WEIRD_ROOM", (8.5, -3.5, 0), (2.8, 2.5, 0.15), "violet"),
    ]
    for name, loc, scale, colour in specs:
        cube(name + "_PLATE", loc, scale, colour, departments[name])

    cube("Front_Counter", (-9, 5.6, 0.65), (3.2, 0.35, 0.7), "coffee", departments["FRONT_DESK"])
    cube("Paper_Shelves", (0, 6.3, 0.9), (2.6, 0.3, 0.9), "moss", departments["PAPER_ROOM"])
    cube("Money_Vault", (8.7, 6.1, 0.9), (1.7, 0.45, 0.9), "coffee", departments["MONEY_ROOM"])
    cube("Auto_Sorter", (10.2, -3.4, 0.8), (0.75, 0.75, 0.8), "teal", departments["WEIRD_ROOM"])
    cube("Copy_Echo", (6.1, -3.4, 0.8), (0.75, 0.75, 0.8), "red", departments["WEIRD_ROOM"])
    for index in range(6):
        cube(f"Folder_{index + 1}", (-11.3 + index * 0.85, 5.0, 0.65), (0.32, 0.45, 0.08), "cream", departments["FRONT_DESK"])

    cast = [
        ("Warden", (-6, 1.2, 0.8), "teal"),
        ("Nia_Bell", (-9, 7, 0.8), "amber"),
        ("Runner", (-1, 0, 0.8), "moss"),
        ("Talker", (2, -1, 0.8), "violet"),
    ]
    for name, loc, colour in cast:
        cube(name, loc, (0.38, 0.38, 0.8), colour, departments["CAST"])

    bpy.ops.object.light_add(type="AREA", location=(-6, -8, 16))
    light = bpy.context.object
    light.name = "M4_Key_Light"
    light.data.energy = 1700
    light.data.shape = "DISK"
    light.data.size = 12

    bpy.ops.object.camera_add(location=(18, -23, 26))
    camera = bpy.context.object
    camera.name = "M4_Locked_Orthographic_Camera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 32
    direction = mathutils_vector((0, 1.0, 1.8)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = PALETTE["ink"][:3]
    scene.render.filepath = str(GUIDE_DIR / "office_m4_flat_colour.png")
    return scene


def mathutils_vector(values):
    from mathutils import Vector
    return Vector(values)


def main():
    BLENDER_DIR.mkdir(parents=True, exist_ok=True)
    GUIDE_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = build_scene()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.render.render(write_still=True)
    guide = GUIDE_DIR / "office_m4_flat_colour.png"
    digest = hashlib.sha256(guide.read_bytes()).hexdigest()
    (GUIDE_DIR / "guide-manifest.json").write_text(json.dumps({
        "schema": "desk42.office-slice-m4.blender-guides.v1",
        "blend": BLEND_PATH.relative_to(ROOT).as_posix(),
        "render": guide.relative_to(ROOT).as_posix(),
        "sha256": digest,
        "resolution": [1600, 900],
        "camera": "M4_Locked_Orthographic_Camera",
        "engine": scene.render.engine,
    }, indent=2) + "\n", encoding="utf-8")
    print("OFFICE_SLICE_M4_GUIDE_OK", digest)


if __name__ == "__main__":
    main()
