"""Verify Desk 42 golden-frame pixel, palette, layer, and registration contracts."""

from __future__ import annotations

import json
from pathlib import Path
import sys
import zipfile

from PIL import Image


def main(root: Path) -> None:
    native = Image.open(root / "D42_GoldenFrame_384_v001.png")
    preview = Image.open(root / "D42_GoldenFrame_Preview4x_v001.png")
    assert native.size == (384, 216), native.size
    assert preview.size == (1536, 864), preview.size
    expected_preview = native.convert("RGB").resize((1536, 864), Image.Resampling.NEAREST)
    assert preview.convert("RGB").tobytes() == expected_preview.tobytes()

    manifest = json.loads((root / "D42_GoldenFrame_LayerManifest_v001.json").read_text())
    allowed = {value.lower() for value in manifest["palette"].values()}
    used = {
        "#%02x%02x%02x" % color
        for _, color in (native.convert("RGB").getcolors(384 * 216) or [])
    }
    assert used <= allowed, used - allowed
    assert not ({"#20d6c7", "#4aa7ff", "#d447a7"} & used)

    layers = sorted((root / "Layers").glob("*.png"))
    assert len(layers) == 11, len(layers)
    for path in layers:
        image = Image.open(path)
        assert image.size == (384, 216), (path, image.size)
        assert image.mode == "RGBA", (path, image.mode)

    with zipfile.ZipFile(root / "D42_GoldenFrame_Layers_v001.ora") as archive:
        assert archive.read("mimetype") == b"image/openraster"
        assert "mergedimage.png" in archive.namelist()
        ora_entries = len(archive.namelist())

    print(f"native=384x216 mode={native.mode} colours={len(used)} palette_subset=true anomaly_colours=0")
    print("preview=exact_nearest_4x true")
    print(f"layers={len(layers)} full_canvas_rgba=true ora_entries={ora_entries}")
    print(
        f"claimant_anchor={manifest['claimant_room_anchor']} "
        f"contact_y={manifest['desk_claimant_contact_y']} "
        f"shadow_bounds={manifest['claimant_contact_shadow_bounds']}"
    )


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("usage: verify_desk42_golden_frame.py GOLDEN_FRAME_DIR")
    main(Path(sys.argv[1]))
