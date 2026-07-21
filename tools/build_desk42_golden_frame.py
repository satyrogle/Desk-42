"""Author the Desk 42 golden frame directly on the 384x216 production grid.

This is deterministic pixel art, not a diffusion output. Every runtime-facing
layer is a registered full-canvas PNG and the merged image uses the locked
healthy-office palette only.
"""

from __future__ import annotations

import json
from pathlib import Path
import sys
import zipfile
from xml.etree.ElementTree import Element, SubElement, tostring

from PIL import Image, ImageDraw, ImageFont


W, H = 384, 216

PALETTE = {
    "ink": "#081513",
    "deep": "#101F1B",
    "green": "#173F32",
    "green_mid": "#24503F",
    "green_light": "#356650",
    "green_high": "#4D7963",
    "cream": "#F1E8CE",
    "paper_shadow": "#D8C58B",
    "paper_mid": "#BFA26F",
    "blue_grey": "#8FA9A6",
    "warm_grey": "#77736A",
    "metal_shadow": "#4F5652",
    "task_orange": "#CE713A",
    "rust": "#A44E2D",
    "wood": "#6A402B",
    "wood_shadow": "#503021",
    "approval_red": "#B73B32",
    "soot": "#332C30",
    "char": "#18191B",
    "suit": "#292A2E",
    "suit_light": "#45464B",
    "brass": "#B68849",
    "amber": "#E2A552",
    "moth": "#D6A070",
    "moth_light": "#F0C692",
}


def rgba(name: str, alpha: int = 255) -> tuple[int, int, int, int]:
    value = PALETTE[name].lstrip("#")
    return tuple(int(value[i : i + 2], 16) for i in (0, 2, 4)) + (alpha,)


def layer() -> Image.Image:
    return Image.new("RGBA", (W, H), (0, 0, 0, 0))


def px_rect(draw: ImageDraw.ImageDraw, box, fill, outline=None, width=1):
    draw.rectangle(box, fill=rgba(fill) if isinstance(fill, str) else fill,
                   outline=rgba(outline) if isinstance(outline, str) else outline,
                   width=width)


def px_poly(draw: ImageDraw.ImageDraw, points, fill):
    draw.polygon(points, fill=rgba(fill) if isinstance(fill, str) else fill)


def draw_room_shell() -> Image.Image:
    im = layer()
    d = ImageDraw.Draw(im)

    px_rect(d, (0, 0, 383, 71), "green")
    px_rect(d, (0, 0, 383, 15), "deep")
    px_rect(d, (0, 14, 383, 17), "ink")
    px_rect(d, (0, 17, 383, 18), "green_high")

    # The floor is an orthographic parallelogram grid: every diagonal is
    # parallel. Nothing converges toward a vanishing point.
    px_rect(d, (0, 72, 383, 215), "green_light")
    px_rect(d, (0, 69, 383, 72), "deep")
    d.line((0, 69, 383, 69), fill=rgba("green_high"), width=1)
    for y in (93, 119, 145, 171, 197):
        d.line((0, y, 383, y), fill=rgba("green_mid"), width=2)
        d.line((0, y + 2, 383, y + 2), fill=rgba("green_high"), width=1)
    # Parallel 3/4-ortho tile divisions.
    for bottom_x in range(-80, 465, 48):
        d.line((bottom_x, 215, bottom_x + 48, 72), fill=rgba("green_mid"), width=1)

    # Sparse maintenance wear in controlled clusters.
    for x, y in ((13, 54), (151, 43), (275, 59), (300, 26), (112, 205),
                 (18, 183), (362, 165), (272, 188), (82, 101)):
        px_rect(d, (x, y, x + 2, y + 1), "green_mid")
        d.point((x + 3, y), fill=rgba("green_high"))
    return im


def draw_room_anchors() -> Image.Image:
    im = layer()
    d = ImageDraw.Draw(im)

    # Closed back-left door.
    px_rect(d, (18, 23, 66, 72), "ink")
    px_rect(d, (21, 25, 63, 71), "wood_shadow")
    px_rect(d, (24, 27, 60, 69), "wood")
    d.line((25, 28, 25, 68), fill=rgba("rust"), width=2)
    px_rect(d, (29, 31, 55, 45), "wood_shadow")
    px_rect(d, (31, 33, 53, 43), "wood")
    px_rect(d, (29, 50, 55, 64), "wood_shadow")
    px_rect(d, (31, 52, 53, 62), "wood")
    px_rect(d, (55, 47, 58, 50), "paper_mid")

    # Noticeboard with three blank, squared forms.
    px_rect(d, (77, 25, 132, 55), "ink")
    px_rect(d, (80, 28, 129, 52), "wood_shadow")
    px_rect(d, (82, 29, 127, 51), "rust")
    for box in ((85, 32, 96, 44), (100, 31, 113, 43), (116, 35, 124, 47)):
        x0, y0, x1, y1 = box
        px_rect(d, (x0 + 1, y0 + 1, x1 + 1, y1 + 1), "wood_shadow")
        px_rect(d, box, "paper_shadow")
        d.point((x0 + 2, y0 + 2), fill=rgba("approval_red"))
        d.line((x0 + 2, y0 + 6, x1 - 2, y0 + 6), fill=rgba("warm_grey"))
        if y1 - y0 > 10:
            d.line((x0 + 2, y0 + 9, x1 - 3, y0 + 9), fill=rgba("warm_grey"))

    # Honest mid-tick clock, no numerals.
    cx, cy = 316, 31
    rim = [(312, 21), (320, 21), (325, 25), (326, 34), (322, 40),
           (314, 42), (307, 38), (305, 30), (308, 24)]
    face = [(313, 24), (319, 24), (323, 27), (323, 34), (319, 38),
            (313, 38), (309, 34), (309, 28)]
    px_poly(d, rim, "ink")
    px_poly(d, face, "paper_shadow")
    for x, y in ((316, 25), (322, 31), (316, 37), (310, 31)):
        d.point((x, y), fill=rgba("metal_shadow"))
    d.line((316, 31, 320, 27), fill=rgba("ink"), width=1)
    d.line((316, 31, 313, 34), fill=rgba("ink"), width=1)
    px_rect(d, (316, 31, 317, 32), "approval_red")

    # Filing cabinet is narrow and subordinate at far right.
    px_rect(d, (329, 57, 373, 137), "ink")
    px_rect(d, (332, 60, 370, 134), "metal_shadow")
    px_poly(d, [(332, 60), (363, 60), (370, 67), (339, 67)], "green_high")
    for top in (69, 90, 111):
        px_rect(d, (336, top, 366, top + 18), "green_mid")
        d.line((338, top + 1, 364, top + 1), fill=rgba("green_high"))
        px_rect(d, (347, top + 5, 356, top + 8), "brass")
        px_rect(d, (344, top + 10, 359, top + 12), "ink")
        px_rect(d, (347, top + 10, 356, top + 11), "warm_grey")
    px_rect(d, (337, 131, 366, 134), "deep")

    # Fluorescent authority fixture at the seam.
    px_rect(d, (164, 5, 220, 13), "ink")
    px_rect(d, (167, 6, 217, 11), "metal_shadow")
    px_rect(d, (170, 6, 214, 8), "cream")
    d.line((172, 10, 212, 10), fill=rgba("paper_mid"))
    return im


def draw_desk_rear() -> Image.Image:
    im = layer()
    d = ImageDraw.Draw(im)

    # One continuous broad desk. Parallel top/bottom edges and a shallow lip
    # preserve the 3/4 orthographic read without a U-shaped silhouette.
    px_poly(d, [(28, 108), (348, 108), (356, 159), (36, 159)], "ink")
    px_poly(d, [(32, 111), (345, 111), (351, 156), (38, 156)], "wood")
    d.line((34, 112, 344, 112), fill=rgba("task_orange"), width=2)
    d.line((39, 154, 349, 154), fill=rgba("wood_shadow"), width=2)

    # Sparse laminate grain follows the desk, never random confetti.
    for x0, y, length in ((49, 121, 20), (89, 147, 17), (154, 126, 16),
                          (193, 151, 24), (246, 119, 14), (301, 145, 22),
                          (114, 136, 10), (273, 134, 12)):
        d.line((x0, y, x0 + length, y), fill=rgba("rust"))
        d.point((x0 + length + 2, y), fill=rgba("wood_shadow"))
    return im


def draw_claimant_chair() -> Image.Image:
    im = layer()
    d = ImageDraw.Draw(im)
    px_rect(d, (161, 64, 223, 115), "ink")
    px_rect(d, (165, 68, 219, 112), "green_mid")
    px_rect(d, (169, 72, 215, 111), "green")
    d.line((171, 73, 213, 73), fill=rgba("green_high"))
    px_rect(d, (154, 91, 164, 113), "ink")
    px_rect(d, (220, 91, 230, 113), "ink")
    px_rect(d, (157, 93, 163, 111), "green_mid")
    px_rect(d, (221, 93, 227, 111), "green_mid")
    return im


def draw_claimant_shadow() -> Image.Image:
    im = layer()
    d = ImageDraw.Draw(im)
    # Contract bounds X=144-239, Y=102-121.
    px_poly(d, [(151, 106), (231, 106), (239, 112), (232, 119),
                (151, 119), (144, 113)], "wood_shadow")
    d.line((158, 107, 225, 107), fill=rgba("soot"))
    return im


def draw_moth_pending() -> Image.Image:
    im = layer()
    d = ImageDraw.Draw(im)

    # Folded wing mass is broad but contained inside the 128x128 claimant cell.
    px_poly(d, [(154, 57), (168, 48), (180, 59), (176, 107),
                (150, 111), (143, 96), (147, 72)], "ink")
    px_poly(d, [(230, 57), (216, 48), (204, 59), (208, 107),
                (234, 111), (241, 96), (237, 72)], "ink")
    px_poly(d, [(157, 59), (168, 53), (176, 63), (172, 104),
                (153, 106), (148, 94), (151, 71)], "moth")
    px_poly(d, [(227, 59), (216, 53), (208, 63), (212, 104),
                (231, 106), (236, 94), (233, 71)], "moth")
    for x, y in ((155, 70), (163, 88), (154, 97), (229, 70), (221, 88), (230, 97)):
        px_rect(d, (x, y, x + 5, y + 3), "wood")

    # Feathery antennae, relaxed forward.
    for points in (((190, 34), (181, 24), (165, 18), (151, 18)),
                   ((194, 34), (203, 24), (219, 18), (233, 18))):
        d.line(points, fill=rgba("ink"), width=4)
        d.line(points, fill=rgba("moth"), width=2)
    for x, y, direction in ((181, 24, -1), (176, 21, -1), (170, 19, -1),
                            (203, 24, 1), (208, 21, 1), (214, 19, 1)):
        d.line((x, y, x + 6 * direction, y - 4), fill=rgba("ink"))
        d.line((x, y + 2, x + 7 * direction, y + 1), fill=rgba("wood"))

    # Pixel-clustered head.
    head_outline = [(177, 31), (186, 27), (199, 27), (208, 32),
                    (215, 43), (216, 58), (210, 70), (201, 78),
                    (185, 78), (174, 71), (168, 61), (168, 44)]
    head = [(180, 33), (188, 30), (198, 30), (205, 34), (211, 44),
            (212, 57), (207, 67), (199, 73), (186, 73), (177, 67),
            (172, 58), (172, 45)]
    px_poly(d, head_outline, "ink")
    px_poly(d, head, "moth")
    px_poly(d, [(181, 34), (191, 30), (199, 31), (204, 36),
                (198, 39), (184, 39)], "moth_light")
    px_poly(d, [(173, 51), (179, 40), (190, 39), (193, 48),
                (190, 61), (181, 66), (175, 61)], "char")
    px_poly(d, [(211, 51), (205, 40), (194, 39), (191, 48),
                (194, 61), (203, 66), (209, 61)], "char")
    px_rect(d, (179, 44, 184, 55), "soot")
    px_rect(d, (200, 44, 205, 55), "soot")
    d.point((181, 45), fill=rgba("paper_mid"))
    d.point((202, 45), fill=rgba("paper_mid"))
    # Two restrained facet clusters keep the eyes insectoid at 1x without
    # turning the face into noisy generated microtexture.
    for x, y in ((176, 48), (184, 53), (179, 58), (200, 49), (205, 54), (202, 59)):
        px_rect(d, (x, y, x + 2, y + 1), "suit_light")
    for x, y in ((174, 40), (170, 46), (171, 64), (208, 40), (212, 46), (211, 64)):
        px_rect(d, (x, y, x + 2, y + 2), "moth_light")
    px_poly(d, [(188, 58), (196, 58), (199, 62), (192, 65), (185, 62)], "wood")
    d.line((185, 68, 190, 70, 196, 70, 201, 67), fill=rgba("ink"), width=2)
    # Sparse scale clusters.
    for x, y in ((183, 35), (199, 36), (177, 60), (205, 60), (188, 72)):
        d.point((x, y), fill=rgba("moth_light"))
        d.point((x + 1, y + 1), fill=rgba("wood"))

    # Worn sober suit and lapels.
    px_poly(d, [(169, 70), (181, 75), (192, 81), (203, 75), (215, 69),
                (229, 78), (235, 107), (215, 114), (168, 114),
                (149, 107), (155, 78)], "ink")
    px_poly(d, [(171, 73), (182, 77), (192, 84), (202, 77), (213, 72),
                (225, 80), (231, 104), (212, 110), (171, 110),
                (153, 104), (159, 80)], "suit")
    px_poly(d, [(177, 76), (191, 84), (183, 101), (169, 82)], "suit_light")
    px_poly(d, [(207, 76), (193, 84), (201, 101), (215, 82)], "suit_light")
    px_poly(d, [(184, 77), (192, 84), (200, 77), (197, 96), (187, 96)], "cream")
    px_poly(d, [(190, 82), (195, 82), (197, 88), (193, 101), (188, 88)], "green_mid")
    # Sleeves angle toward the folded hands.
    px_poly(d, [(158, 86), (171, 84), (184, 103), (178, 112),
                (161, 107), (153, 98)], "suit_light")
    px_poly(d, [(226, 86), (213, 84), (200, 103), (206, 112),
                (223, 107), (231, 98)], "suit_light")

    # Hands meet the fixed Y=112 contact line and remain unobstructed.
    px_poly(d, [(174, 100), (184, 99), (194, 107), (191, 115),
                (180, 114), (173, 108)], "ink")
    px_poly(d, [(210, 100), (200, 99), (190, 107), (193, 115),
                (204, 114), (211, 108)], "ink")
    px_poly(d, [(177, 102), (184, 102), (191, 108), (189, 112),
                (181, 111), (176, 107)], "moth")
    px_poly(d, [(207, 102), (200, 102), (193, 108), (195, 112),
                (203, 111), (208, 107)], "moth")
    d.line((183, 105, 187, 110), fill=rgba("moth_light"))
    d.line((201, 105, 197, 110), fill=rgba("moth_light"))
    return im


def draw_mug(d, x, y):
    px_rect(d, (x, y + 3, x + 15, y + 18), "ink")
    px_rect(d, (x + 2, y + 4, x + 12, y + 16), "paper_shadow")
    px_rect(d, (x + 12, y + 7, x + 18, y + 14), "ink")
    px_rect(d, (x + 13, y + 8, x + 16, y + 12), "cream")
    px_rect(d, (x + 2, y + 2, x + 12, y + 5), "cream")
    px_rect(d, (x + 4, y + 4, x + 11, y + 6), "wood_shadow")
    d.line((x + 3, y + 14, x + 10, y + 14), fill=rgba("cream"))


def draw_back_props() -> Image.Image:
    im = layer()
    d = ImageDraw.Draw(im)

    # Bakelite telephone behind the inbox.
    px_poly(d, [(55, 114), (59, 111), (74, 111), (78, 114),
                (78, 124), (54, 124), (54, 116)], "ink")
    px_rect(d, (58, 115, 74, 122), "char")
    # Heavy separated receiver ends create the Bakelite read.
    px_rect(d, (54, 107, 61, 114), "ink")
    px_rect(d, (72, 107, 79, 114), "ink")
    px_rect(d, (58, 108, 75, 112), "soot")
    px_rect(d, (61, 109, 72, 110), "warm_grey")
    px_rect(d, (62, 115, 71, 122), "warm_grey")
    px_rect(d, (64, 117, 69, 120), "ink")

    # Wire inbox and two forms.
    px_rect(d, (81, 112, 112, 132), "ink")
    d.line((84, 114, 84, 129, 109, 129, 109, 114), fill=rgba("blue_grey"), width=2)
    px_poly(d, [(87, 108), (105, 108), (109, 124), (90, 124)], "paper_shadow")
    px_poly(d, [(91, 105), (108, 105), (110, 121), (93, 121)], "cream")
    for y in (110, 114, 118):
        d.line((95, y, 106, y), fill=rgba("warm_grey"))

    # Low chunky CRT, left of centre, with direct keyboard.
    px_rect(d, (116, 112, 164, 142), "ink")
    px_rect(d, (119, 114, 161, 139), "warm_grey")
    px_rect(d, (123, 117, 157, 135), "deep")
    px_rect(d, (126, 119, 154, 132), "char")
    d.line((127, 120, 151, 120), fill=rgba("green_mid"))
    px_rect(d, (155, 136, 159, 138), "green_high")
    px_poly(d, [(120, 142), (160, 142), (165, 149), (116, 149)], "ink")
    px_poly(d, [(121, 143), (158, 143), (161, 147), (119, 147)], "metal_shadow")
    for x in range(124, 158, 5):
        d.point((x, 145), fill=rgba("blue_grey"))

    # Claims machine, right of centre.
    px_rect(d, (235, 112, 280, 142), "ink")
    px_rect(d, (238, 115, 276, 139), "metal_shadow")
    px_rect(d, (244, 110, 271, 121), "ink")
    px_rect(d, (248, 113, 268, 119), "char")
    px_rect(d, (243, 123, 272, 133), "deep")
    d.line((247, 128, 265, 128), fill=rgba("green_high"), width=2)
    for i, x in enumerate((261, 266, 271)):
        px_rect(d, (x, 134, x + 2, 136), "paper_mid" if i < 2 else "task_orange")

    # Switched-off brass gooseneck lamp.
    px_rect(d, (283, 108, 303, 113), "ink")
    px_poly(d, [(285, 106), (301, 106), (304, 111), (282, 111)], "brass")
    d.line((295, 107, 295, 99, 291, 94, 286, 98), fill=rgba("ink"), width=4)
    d.line((295, 107, 295, 99, 291, 94, 286, 98), fill=rgba("brass"), width=2)
    px_poly(d, [(280, 96), (291, 96), (295, 101), (278, 101)], "ink")
    px_poly(d, [(282, 97), (289, 97), (292, 100), (280, 100)], "amber")

    # Pen holder at the back-right.
    px_rect(d, (308, 110, 325, 134), "ink")
    px_poly(d, [(310, 115), (323, 115), (321, 132), (312, 132)], "green_mid")
    for x, top, color in ((312, 104, "char"), (316, 101, "blue_grey"),
                          (320, 103, "brass"), (323, 100, "approval_red")):
        px_rect(d, (x, top, x + 2, 119), color)
        px_rect(d, (x, top, x + 2, top + 2), "cream")
    return im


def draw_processing_props() -> Image.Image:
    im = layer()
    d = ImageDraw.Draw(im)

    # Personal zone: mug and non-figurative brass token.
    draw_mug(d, 36, 127)
    px_rect(d, (59, 145, 69, 151), "ink")
    px_poly(d, [(61, 143), (66, 143), (69, 147), (66, 150), (61, 149)], "brass")
    d.point((64, 145), fill=rgba("amber"))

    # Current form: the brightest object and instant processing read.
    px_rect(d, (174, 117, 208, 151), "ink")
    px_rect(d, (176, 119, 206, 149), "cream")
    px_rect(d, (177, 120, 205, 124), "paper_shadow")
    px_rect(d, (179, 121, 183, 123), "task_orange")
    for y, end in ((128, 201), (132, 202), (136, 199), (140, 203), (144, 196)):
        d.line((180, y, end, y), fill=rgba("warm_grey"))
    # Abstract stamp and ink pad, no letters.
    px_rect(d, (211, 123, 224, 137), "ink")
    px_rect(d, (215, 120, 220, 127), "wood_shadow")
    px_rect(d, (213, 127, 222, 135), "approval_red")
    px_rect(d, (211, 141, 228, 150), "ink")
    px_rect(d, (214, 143, 225, 147), "soot")

    # Two-folder outbox, upper-right only.
    px_poly(d, [(302, 138), (337, 138), (340, 145), (305, 145)], "ink")
    px_poly(d, [(305, 136), (333, 136), (337, 142), (308, 142)], "paper_mid")
    px_poly(d, [(309, 140), (340, 140), (342, 148), (311, 148)], "cream")
    px_rect(d, (311, 138, 320, 141), "task_orange")

    # One crumpled form in the front-left discard lane.
    px_poly(d, [(72, 146), (77, 143), (82, 147), (86, 145), (89, 151),
                (86, 156), (80, 155), (76, 158), (70, 154)], "ink")
    px_poly(d, [(74, 148), (78, 146), (82, 150), (86, 148), (87, 152),
                (84, 154), (80, 153), (77, 156), (73, 153)], "paper_shadow")
    d.line((76, 149, 84, 154), fill=rgba("cream"))
    d.line((78, 155, 85, 149), fill=rgba("warm_grey"))
    return im


def draw_desk_lip() -> Image.Image:
    im = layer()
    d = ImageDraw.Draw(im)
    # Shallow 4-pixel handoff lip; the slab remains continuous.
    points = [(36, 156), (171, 156), (171, 153), (213, 153),
              (213, 156), (356, 156), (356, 168), (36, 168)]
    px_poly(d, points, "ink")
    px_poly(d, [(39, 158), (173, 158), (173, 156), (211, 156),
                (211, 158), (353, 158), (353, 165), (39, 165)], "wood_shadow")
    d.line((41, 159, 171, 159), fill=rgba("rust"))
    d.line((213, 159, 351, 159), fill=rgba("rust"))

    # Solid pedestal units; the centre stays open for the player's chair.
    px_rect(d, (44, 166, 100, 207), "ink")
    px_rect(d, (48, 169, 96, 204), "wood_shadow")
    px_rect(d, (51, 171, 93, 201), "wood")
    d.line((52, 172, 52, 199), fill=rgba("rust"), width=2)
    px_rect(d, (286, 166, 342, 207), "ink")
    px_rect(d, (290, 169, 338, 204), "wood_shadow")
    for top in (171, 187):
        px_rect(d, (293, top, 335, top + 13), "wood")
        d.line((295, top + 1, 333, top + 1), fill=rgba("rust"))
        px_rect(d, (310, top + 6, 320, top + 8), "ink")
        px_rect(d, (312, top + 6, 318, top + 7), "brass")
    return im


def draw_player_chair() -> Image.Image:
    im = layer()
    d = ImageDraw.Draw(im)
    # Cropped lower-centre player chair, separate from the desk cut-out.
    px_rect(d, (164, 169, 220, 197), "ink")
    px_poly(d, [(168, 171), (216, 171), (220, 190), (214, 195),
                (170, 195), (164, 190)], "green_mid")
    px_rect(d, (174, 175, 210, 190), "green")
    d.line((176, 176, 208, 176), fill=rgba("green_high"), width=2)
    px_rect(d, (188, 193, 196, 210), "ink")
    px_rect(d, (190, 194, 194, 208), "metal_shadow")
    d.line((192, 207, 169, 215), fill=rgba("ink"), width=5)
    d.line((192, 207, 215, 215), fill=rgba("ink"), width=5)
    d.line((192, 207, 192, 216), fill=rgba("ink"), width=5)
    d.line((192, 207, 171, 214), fill=rgba("metal_shadow"), width=2)
    d.line((192, 207, 213, 214), fill=rgba("metal_shadow"), width=2)
    return im


def draw_lighting() -> Image.Image:
    im = layer()
    d = ImageDraw.Draw(im)
    # One hard-edged authority band over the processing form; no bloom.
    px_poly(d, [(165, 112), (219, 112), (226, 153), (158, 153)], rgba("cream", 18))
    return im


def palette_image() -> Image.Image:
    values = []
    for color in PALETTE.values():
        value = color.lstrip("#")
        values.extend(int(value[i : i + 2], 16) for i in (0, 2, 4))
    values.extend([0] * (768 - len(values)))
    result = Image.new("P", (1, 1))
    result.putpalette(values)
    return result


def save_ora(path: Path, named_layers, merged: Image.Image) -> None:
    root = Element("image", {"version": "0.0.1", "w": str(W), "h": str(H), "name": "Desk 42 Golden Frame"})
    stack = SubElement(root, "stack", {"name": "root"})
    # OpenRaster stores the topmost layer first.
    for index, (name, _) in enumerate(reversed(named_layers)):
        SubElement(stack, "layer", {
            "name": name,
            "src": f"data/layer_{len(named_layers) - 1 - index:02d}.png",
            "visibility": "visible",
            "composite-op": "svg:src-over",
        })

    thumbnail = merged.resize((256, 144), Image.Resampling.NEAREST)
    path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr("mimetype", "image/openraster", compress_type=zipfile.ZIP_STORED)
        archive.writestr("stack.xml", tostring(root, encoding="utf-8", xml_declaration=True))
        for index, (_, image) in enumerate(named_layers):
            from io import BytesIO
            buffer = BytesIO()
            image.save(buffer, format="PNG", optimize=False)
            archive.writestr(f"data/layer_{index:02d}.png", buffer.getvalue())
        from io import BytesIO
        merged_buffer = BytesIO()
        merged.save(merged_buffer, format="PNG", optimize=False)
        archive.writestr("mergedimage.png", merged_buffer.getvalue())
        thumb_buffer = BytesIO()
        thumbnail.save(thumb_buffer, format="PNG", optimize=False)
        archive.writestr("Thumbnails/thumbnail.png", thumb_buffer.getvalue())


def make_acceptance_strip(candidate_path: Path, golden: Image.Image, out: Path) -> None:
    candidate = Image.open(candidate_path).convert("RGB") if candidate_path.exists() else golden.convert("RGB")
    candidate = candidate.resize((W, H), Image.Resampling.NEAREST)
    golden_rgb = golden.convert("RGB")
    diff = Image.blend(candidate, golden_rgb, 0.5)

    scale = 2
    tile_w, tile_h = W * scale, H * scale
    gutter, label_h = 24, 34
    board = Image.new("RGB", (tile_w * 4 + gutter * 5, tile_h + label_h + gutter * 2), (8, 21, 19))
    draw = ImageDraw.Draw(board)
    try:
        font = ImageFont.truetype("arial.ttf", 18)
    except OSError:
        font = ImageFont.load_default()

    panels = [
        ("01 SOURCE / REJECTION TICKET", candidate.resize((tile_w, tile_h), Image.Resampling.NEAREST)),
        ("02 GOLDEN FRAME 1X (SHOWN 2X)", golden_rgb.resize((tile_w, tile_h), Image.Resampling.NEAREST)),
        ("03 PROCESSING ZONE 4X", golden_rgb.crop((96, 80, 288, 188)).resize((tile_w, tile_h), Image.Resampling.NEAREST)),
        ("04 50% DIFF OVERLAY", diff.resize((tile_w, tile_h), Image.Resampling.NEAREST)),
    ]
    for index, (title, panel) in enumerate(panels):
        x = gutter + index * (tile_w + gutter)
        draw.text((x, gutter), title, fill=(241, 232, 206), font=font)
        board.paste(panel, (x, gutter + label_h))
    out.parent.mkdir(parents=True, exist_ok=True)
    board.save(out, optimize=True)


def main(output_dir: Path, candidate_path: Path) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    layers_dir = output_dir / "Layers"
    layers_dir.mkdir(parents=True, exist_ok=True)

    named_layers = [
        ("BG_WallsFloor", draw_room_shell()),
        ("BG_Anchors", draw_room_anchors()),
        ("Desk_Base_Rear", draw_desk_rear()),
        ("Desk_Props_Back", draw_back_props()),
        ("Claimant_Chair", draw_claimant_chair()),
        ("Claimant_ContactShadow", draw_claimant_shadow()),
        ("Claimant_MothAccountant_Pending", draw_moth_pending()),
        ("Desk_Foreground_Lip", draw_desk_lip()),
        ("Desk_Props_Front", draw_processing_props()),
        ("FG_PlayerChair", draw_player_chair()),
        ("Lighting", draw_lighting()),
    ]

    merged = Image.new("RGBA", (W, H), rgba("ink"))
    for index, (name, image) in enumerate(named_layers):
        merged = Image.alpha_composite(merged, image)
        image.save(layers_dir / f"{index:02d}_{name}.png", optimize=False)

    # Remove alpha blends from the review master and map exactly to the locked palette.
    indexed = merged.convert("RGB").quantize(palette=palette_image(), dither=Image.Dither.NONE)
    native_path = output_dir / "D42_GoldenFrame_384_v001.png"
    preview_path = output_dir / "D42_GoldenFrame_Preview4x_v001.png"
    ora_path = output_dir / "D42_GoldenFrame_Layers_v001.ora"
    strip_path = output_dir / "D42_GoldenFrame_AcceptanceStrip_v001.png"
    indexed.save(native_path, optimize=False)
    indexed.resize((1536, 864), Image.Resampling.NEAREST).save(preview_path, optimize=False)
    save_ora(ora_path, named_layers, indexed.convert("RGBA"))
    make_acceptance_strip(candidate_path, indexed.convert("RGB"), strip_path)

    gpl = ["GIMP Palette", "Name: Desk 42 Healthy Office v1", "Columns: 8", "#"]
    for name, color in PALETTE.items():
        value = color.lstrip("#")
        r, g, b = (int(value[i : i + 2], 16) for i in (0, 2, 4))
        gpl.append(f"{r:3d} {g:3d} {b:3d}\t{name}")
    (output_dir / "D42_HealthyOffice_v001.gpl").write_text("\n".join(gpl) + "\n", encoding="utf-8")

    manifest = {
        "canvas": [W, H],
        "composition_center": [192, 108],
        "claimant_room_anchor": [192, 112],
        "desk_claimant_contact_y": 112,
        "claimant_cell": {"size": [128, 128], "top_left": [128, 0], "pivot_local": [64, 112]},
        "claimant_contact_shadow_bounds": [144, 102, 239, 121],
        "draw_order": [name for name, _ in named_layers],
        "palette": PALETTE,
        "pixel_rules": {"filter": "point", "mipmaps": False, "compression": "none", "dither": "none"},
        "status": "rejected_visual_exploration",
        "rejection_reason": "Procedural redraw solved layout constraints but flattened the approved material richness and atmosphere.",
    }
    (output_dir / "D42_GoldenFrame_LayerManifest_v001.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
    )

    colors = indexed.getcolors(maxcolors=W * H) or []
    print(f"native={native_path} size={W}x{H} colours={len(colors)}")
    print(f"preview={preview_path} size=1536x864 nearest=true")
    print(f"layers={len(named_layers)} ora={ora_path}")
    print(f"acceptance_strip={strip_path}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("usage: build_desk42_golden_frame.py OUTPUT_DIR CANDIDATE_NATIVE.png")
    main(Path(sys.argv[1]), Path(sys.argv[2]))
