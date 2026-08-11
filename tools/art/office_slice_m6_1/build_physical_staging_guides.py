"""Build physical A02/A03 staging guides for the local Krea 2 revision pass."""
from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

from finalise_gate_a_targets import ROOT


OUTPUT = ROOT / "ArtLab" / "OfficeSliceM6_1" / "References" / "PhysicalStaging"
MANIFEST = OUTPUT / "physical-staging-manifest.json"
REJECTED = ROOT / "ArtLab" / "OfficeSliceM6_1" / "Candidates" / "Rejected"
SOURCE_FRAMES = {
    "A02": REJECTED / "A02_owner_rejected_dc3b7aeb.png",
    "A03": REJECTED / "A03_owner_rejected_6bda52a5.png",
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    records = []
    for frame_id in ("A02", "A03"):
        raw_path = SOURCE_FRAMES[frame_id]
        output = OUTPUT / (
            "A02_automation_relief_physical_staging.png"
            if frame_id == "A02"
            else "A03_promotion_cascade_break_physical_staging.png"
        )
        shutil.copy2(raw_path, output)
        records.append(
            {
                "id": frame_id,
                "source": raw_path.relative_to(ROOT).as_posix(),
                "source_sha256": sha256(raw_path),
                "guide": output.relative_to(ROOT).as_posix(),
                "guide_sha256": sha256(output),
                "role": "retained owner-rejected physical placement pass used only as Krea staging guidance",
            }
        )
    manifest = {
        "schema": "desk42.office-slice-m6.1.physical-staging.v1",
        "script": "tools/art/office_slice_m6_1/build_physical_staging_guides.py",
        "guides": records,
    }
    MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("M6_1_PHYSICAL_STAGING_GUIDES", *(record["guide_sha256"] for record in records))


if __name__ == "__main__":
    main()
