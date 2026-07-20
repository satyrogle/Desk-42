"""Finalize Desk 42 concept sources onto a real indexed pixel grid."""

from __future__ import annotations

import argparse
from collections import deque
from pathlib import Path

from PIL import Image


PALETTE_HEX = (
    "081513", "101f1b", "173f32", "24503f", "356650", "4d7963",
    "f1e8ce", "d8c58b", "bfa26f", "8fa9a6", "77736a", "4f5652",
    "ce713a", "a44e2d", "6a402b", "503021", "b73b32", "332c30",
    "18191b", "292a2e", "45464b", "20d6c7", "168f88", "4aa7ff",
    "d447a7", "b68849", "e2a552", "7d8e3c", "a9a95b", "c5c98b",
    "d6a070", "f0c692",
)


def build_palette() -> Image.Image:
    values: list[int] = []
    for colour in PALETTE_HEX:
        values.extend(int(colour[offset : offset + 2], 16) for offset in (0, 2, 4))
    values.extend([0] * (768 - len(values)))
    palette = Image.new("P", (1, 1))
    palette.putpalette(values)
    return palette


def square_crop(image: Image.Image) -> Image.Image:
    side = min(image.size)
    left = (image.width - side) // 2
    top = (image.height - side) // 2
    return image.crop((left, top, left + side, top + side))


def transparent_cell(cell: Image.Image, primary_only: bool = False) -> Image.Image:
    """Flood-fill the shared dark-green backdrop and remove distant debris."""
    indexed = cell if cell.mode == "P" else cell.quantize(palette=build_palette(), dither=Image.Dither.NONE)
    width, height = indexed.size
    pixels = indexed.load()
    background_indices = set(range(6))
    queue: deque[tuple[int, int]] = deque()
    background: set[tuple[int, int]] = set()

    for x in range(width):
        queue.append((x, 0))
        if not primary_only:
            queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))

    while queue:
        point = queue.popleft()
        if point in background:
            continue
        x, y = point
        if pixels[x, y] not in background_indices:
            continue
        background.add(point)
        if x > 0:
            queue.append((x - 1, y))
        if x + 1 < width:
            queue.append((x + 1, y))
        if y > 0:
            queue.append((x, y - 1))
        if y + 1 < height:
            queue.append((x, y + 1))

    rgba = cell.convert("RGBA")
    rgba_pixels = rgba.load()
    for x, y in background:
        rgba_pixels[x, y] = (0, 0, 0, 0)

    if primary_only:
        # Generated claimant references repeat a ceiling fixture in this corner.
        # It is environment debris, not part of the portrait sprite.
        for y in range(min(20, height)):
            for x in range(min(42, width)):
                rgba_pixels[x, y] = (0, 0, 0, 0)

    # Keep the primary subject plus nearby detached details such as gel droplets.
    opaque = {(x, y) for y in range(height) for x in range(width) if rgba_pixels[x, y][3]}
    components: list[set[tuple[int, int]]] = []
    while opaque:
        seed = opaque.pop()
        component = {seed}
        pending = [seed]
        while pending:
            x, y = pending.pop()
            for neighbour in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if neighbour in opaque:
                    opaque.remove(neighbour)
                    component.add(neighbour)
                    pending.append(neighbour)
        components.append(component)

    if components:
        primary = max(components, key=len)
        min_x = min(x for x, _ in primary) - 10
        max_x = max(x for x, _ in primary) + 10
        min_y = min(y for _, y in primary) - 10
        max_y = max(y for _, y in primary) + 10
        for component in components:
            if component is primary:
                continue
            near_primary = (not primary_only) and any(
                min_x <= x <= max_x and min_y <= y <= max_y for x, y in component
            )
            if not near_primary:
                for x, y in component:
                    rgba_pixels[x, y] = (0, 0, 0, 0)

    return rgba


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--grid", type=int)
    parser.add_argument("--columns", type=int)
    parser.add_argument("--rows", type=int)
    parser.add_argument("--cell", default=128, type=int)
    parser.add_argument("--sheet", required=True, type=Path)
    parser.add_argument("--preview", required=True, type=Path)
    parser.add_argument("--cells-dir", required=True, type=Path)
    parser.add_argument("--names", required=True, nargs="+")
    parser.add_argument("--preview-scale", default=4, type=int)
    parser.add_argument("--transparent-cells", action="store_true")
    parser.add_argument("--primary-component-only", action="store_true")
    args = parser.parse_args()

    columns = args.columns or args.grid
    rows = args.rows or args.grid
    if not columns or not rows:
        raise SystemExit("Provide --grid or both --columns and --rows")

    expected = columns * rows
    if len(args.names) != expected:
        raise SystemExit(f"Expected {expected} names for a {columns}x{rows} grid")

    source = Image.open(args.input).convert("RGB")
    native_width = columns * args.cell
    native_height = rows * args.cell
    reduced = Image.new("RGB", (native_width, native_height))

    for index in range(expected):
        column = index % columns
        row = index // columns
        left = round(column * source.width / columns)
        top = round(row * source.height / rows)
        right = round((column + 1) * source.width / columns)
        bottom = round((row + 1) * source.height / rows)
        panel = square_crop(source.crop((left, top, right, bottom)))
        panel = panel.resize((args.cell, args.cell), Image.Resampling.BOX)
        reduced.paste(panel, (column * args.cell, row * args.cell))

    indexed = reduced.quantize(palette=build_palette(), dither=Image.Dither.NONE)

    args.sheet.parent.mkdir(parents=True, exist_ok=True)
    args.preview.parent.mkdir(parents=True, exist_ok=True)
    args.cells_dir.mkdir(parents=True, exist_ok=True)
    indexed.save(args.sheet, optimize=False)

    preview_width = native_width * args.preview_scale
    preview_height = native_height * args.preview_scale
    indexed.resize((preview_width, preview_height), Image.Resampling.NEAREST).save(
        args.preview, optimize=False
    )

    for index, name in enumerate(args.names):
        column = index % columns
        row = index // columns
        box = (
            column * args.cell,
            row * args.cell,
            (column + 1) * args.cell,
            (row + 1) * args.cell,
        )
        cell = indexed.crop(box)
        if args.transparent_cells:
            cell = transparent_cell(cell, primary_only=args.primary_component_only)
        cell.save(args.cells_dir / f"{name}.png", optimize=False)

    used = indexed.getcolors(maxcolors=native_width * native_height) or []
    print(f"sheet={args.sheet} size={indexed.width}x{indexed.height} colours={len(used)}")
    print(f"preview={args.preview} size={preview_width}x{preview_height}")
    print(f"cells={expected} cell_size={args.cell}x{args.cell}")


if __name__ == "__main__":
    main()
