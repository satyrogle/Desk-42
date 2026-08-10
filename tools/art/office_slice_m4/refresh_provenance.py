"""Refresh deterministic M4 workflow hashes and non-runtime provenance rows."""
from __future__ import annotations

import csv
import hashlib
import json
from datetime import date
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
ARTLAB = ROOT / "ArtLab" / "OfficeSliceM4"
LEDGER = ARTLAB / "Provenance" / "asset-ledger.csv"
WORKFLOW_MANIFEST = ARTLAB / "Provenance" / "workflow-manifest.json"
WORKFLOW_DIR = ARTLAB / "ComfyUI"

COLUMNS = [
    "asset_id", "runtime_filename", "category", "authoring_method", "blender_source",
    "comfy_workflow", "model_checkpoint", "prompt_sha256", "negative_prompt_sha256", "seed",
    "control_guide", "source_reference", "reference_licence", "generation_date",
    "normaliser_version", "final_sha256", "reviewer_decision", "rejection_reason",
]


def sha_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha(path: Path) -> str:
    return sha_bytes(path.read_bytes())


def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def read_rows() -> list[dict]:
    with LEDGER.open(newline="", encoding="utf-8") as handle:
        return list(csv.DictReader(handle))


def upsert(rows: list[dict], row: dict):
    rows[:] = [item for item in rows if item["asset_id"] != row["asset_id"]]
    rows.append(row)


def common(asset_id: str, filename: str, category: str, method: str,
           digest: str, decision: str, reason: str = "") -> dict:
    row = {key: "" for key in COLUMNS}
    row.update({
        "asset_id": asset_id,
        "runtime_filename": filename,
        "category": category,
        "authoring_method": method,
        "source_reference": filename,
        "reference_licence": "PROJECT-ORIGINAL",
        "generation_date": date.today().isoformat(),
        "normaliser_version": "1.0.0",
        "final_sha256": digest,
        "reviewer_decision": decision,
        "rejection_reason": reason,
    })
    return row


def main():
    rows = read_rows()
    workflow_records = []
    for path in sorted(WORKFLOW_DIR.glob("*.json")):
        workflow = json.loads(path.read_text(encoding="utf-8"))
        positive = workflow["2"]["inputs"]["text"]
        negative = workflow["3"]["inputs"]["text"]
        workflow_records.append({
            "path": rel(path),
            "sha256": sha(path),
            "checkpoint": workflow["1"]["inputs"]["ckpt_name"],
            "controlnet": workflow["6"]["inputs"]["control_net_name"],
            "seed": workflow["9"]["inputs"]["seed"],
            "positive_prompt_sha256": sha_bytes(positive.encode()),
            "negative_prompt_sha256": sha_bytes(negative.encode()),
            "status": "executed-success" if path.name == "office_environment_controlnet.json" else "schema-validated",
        })

    target = ARTLAB / "ApprovedSources" / "TargetFrames" / "shift1_opening_target.png"
    upsert(rows, common("target.shift1.opening", rel(target), "TargetFrame",
                        "python-pixel-original", sha(target), "approved"))

    guide_source = ARTLAB / "References" / "BlenderGuides" / "office_m4_flat_colour.png"
    guide_approved = ARTLAB / "ApprovedSources" / "Guides" / "office_m4_flat_colour.png"
    guide_approved.parent.mkdir(parents=True, exist_ok=True)
    Image.open(guide_source).convert("RGBA").save(guide_approved, optimize=False, compress_level=9)
    guide = common("guide.office.flat-colour", rel(guide_approved), "Guide",
                   "blender-workbench-guide", sha(guide_approved), "approved")
    guide["blender_source"] = "ArtLab/OfficeSliceM4/Blender/office_slice_m4_master.blend"
    upsert(rows, guide)

    candidate = ARTLAB / "Candidates" / "TargetFrames" / "office_environment_comfy_420401.png"
    if candidate.exists():
        env_workflow = next(item for item in workflow_records
                            if item["path"].endswith("office_environment_controlnet.json"))
        rejected = common(
            "candidate.environment.comfy.420401", rel(candidate), "TargetFrameCandidate",
            "comfyui-controlnet", sha(candidate), "rejected",
            "Structural read drifted from office to outdoor park benches; department semantics were lost.")
        rejected.update({
            "comfy_workflow": env_workflow["path"],
            "model_checkpoint": env_workflow["checkpoint"],
            "prompt_sha256": env_workflow["positive_prompt_sha256"],
            "negative_prompt_sha256": env_workflow["negative_prompt_sha256"],
            "seed": str(env_workflow["seed"]),
            "control_guide": "ArtLab/OfficeSliceM4/References/BlenderGuides/office_m4_flat_colour.png",
        })
        upsert(rows, rejected)

    with LEDGER.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=COLUMNS, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(sorted(rows, key=lambda row: row["asset_id"]))

    WORKFLOW_MANIFEST.write_text(json.dumps({
        "schema": "desk42.office-slice-m4.workflow-manifest.v1",
        "comfyui_version": "0.24.1",
        "blender_version": "5.1.2",
        "normaliser": "tools/art/office_slice_m4/normalise_assets.py",
        "normaliser_version": "1.0.0",
        "workflows": workflow_records,
        "executed_prompt_id": "4a3903a3-53c4-4277-92fa-c061376727a4",
        "blender_rgba_pixel_sha256": "913e18ff71ec67eed7c88825ea9f2a1328f04bd9347d8283308af5689630f904",
    }, indent=2) + "\n", encoding="utf-8")
    print("OFFICE_SLICE_M4_PROVENANCE_OK", len(rows), len(workflow_records), sha(WORKFLOW_MANIFEST))


if __name__ == "__main__":
    main()
