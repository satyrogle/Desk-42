"""Deterministic Office Slice M4 pixel-asset normaliser."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image

VERSION = "1.0.0"
PALETTE = {
    (0xE8, 0xD9, 0xB5), (0xC7, 0xBF, 0xA7), (0x66, 0x70, 0x5B),
    (0x2F, 0x6B, 0x67), (0x6C, 0x4E, 0x3D), (0xB8, 0xD6, 0xB0),
    (0xD8, 0x89, 0x2B), (0xB5, 0x3B, 0x38), (0x15, 0x15, 0x1A),
    (0x49, 0xC6, 0xC8), (0x7B, 0x4A, 0x88), (255, 255, 255), (0, 0, 0),
}


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def normalise(source: Path, output: Path, width: int, height: int,
              require_alpha: bool, strict_palette: bool, manifest: Path | None):
    image = Image.open(source).convert("RGBA")
    if require_alpha and not any(pixel[3] < 255 for pixel in image.getdata()):
        raise ValueError("alpha constraint failed: no transparent pixel")
    marker = None
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            if pixels[x, y][:3] == (255, 0, 255):
                marker = (x, y)
                pixels[x, y] = (0, 0, 0, 0)
    if strict_palette:
        unexpected = {p[:3] for p in image.getdata() if p[3] and p[:3] not in PALETTE}
        if unexpected:
            raise ValueError(f"palette constraint failed: {len(unexpected)} unexpected colours")
    image = image.resize((width, height), Image.Resampling.NEAREST)
    output.parent.mkdir(parents=True, exist_ok=True)
    image.save(output, optimize=False, compress_level=9)
    result = {
        "normaliser_version": VERSION,
        "source": source.as_posix(),
        "output": output.as_posix(),
        "width": width,
        "height": height,
        "anchor_marker": marker,
        "sha256": digest(output),
    }
    if manifest:
        manifest.parent.mkdir(parents=True, exist_ok=True)
        data = json.loads(manifest.read_text(encoding="utf-8")) if manifest.exists() else {"assets": []}
        data.setdefault("assets", [])
        data["assets"] = [item for item in data["assets"] if item.get("output") != output.as_posix()]
        data["assets"].append(result)
        data["assets"].sort(key=lambda item: item["output"])
        manifest.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, sort_keys=True))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--width", type=int, required=True)
    parser.add_argument("--height", type=int, required=True)
    parser.add_argument("--require-alpha", action="store_true")
    parser.add_argument("--strict-palette", action="store_true")
    parser.add_argument("--manifest", type=Path)
    args = parser.parse_args()
    normalise(args.source, args.output, args.width, args.height,
              args.require_alpha, args.strict_palette, args.manifest)


if __name__ == "__main__":
    main()
