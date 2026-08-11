"""Validate M6.1 Gate A workflows, local models, frames and contact sheet."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[3]
ARTLAB = ROOT / "ArtLab" / "OfficeSliceM6_1"
MODEL_ROOT = Path.home() / "ComfyUI-Shared" / "models"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_json(path: Path) -> dict[str, object]:
    return json.loads(path.read_text(encoding="utf-8"))


def check_image(path: Path, expected_size: tuple[int, int], expected_sha: str) -> None:
    if not path.is_file():
        raise FileNotFoundError(path)
    with Image.open(path) as image:
        if image.size != expected_size:
            raise AssertionError(f"Unexpected dimensions for {path}: {image.size}")
    actual_sha = sha256(path)
    if actual_sha != expected_sha:
        raise AssertionError(f"SHA-256 mismatch for {path}: {actual_sha}")


def main() -> None:
    spec = load_json(ARTLAB / "gate-a-targets.json")
    models = load_json(ARTLAB / "Provenance" / "model-manifest.json")
    execution = load_json(ARTLAB / "Provenance" / "execution-manifest.json")
    finalisation = load_json(ARTLAB / "Provenance" / "finalisation-manifest.json")
    contact = load_json(ARTLAB / "Reviews" / "GateA" / "desk42_m6_1_gate_a_contact_sheet.json")

    model_paths = {
        "diffusion_model": MODEL_ROOT / "diffusion_models" / "krea2_turbo_int8_convrot.safetensors",
        "text_encoder": MODEL_ROOT / "text_encoders" / "qwen3vl_4b_fp8_scaled.safetensors",
        "vae": MODEL_ROOT / "vae" / "qwen_image_vae.safetensors",
        "style_reference_lora": MODEL_ROOT / "loras" / "krea2_style_reference.safetensors",
    }
    for model in models["models"]:
        path = model_paths[model["role"]]
        if path.stat().st_size != model["bytes"]:
            raise AssertionError(f"Byte-size mismatch for {path}")
        if sha256(path) != model["sha256"]:
            raise AssertionError(f"SHA-256 mismatch for {path}")

    expected_size = tuple(spec["resolution"])
    execution_frames = {frame["id"]: frame for frame in execution["frames"]}
    final_frames = {frame["id"]: frame for frame in finalisation["outputs"]}
    contact_frames = {frame["id"]: frame for frame in contact["frames"]}
    if set(execution_frames) != {"A01", "A02", "A03"}:
        raise AssertionError("Execution manifest must contain exactly A01, A02 and A03")

    for target in spec["targets"]:
        frame_id = target["id"]
        execution_frame = execution_frames[frame_id]
        final_frame = final_frames[frame_id]
        contact_frame = contact_frames[frame_id]
        if execution_frame["seed"] != spec["shared_seed"]:
            raise AssertionError(f"Seed mismatch for {frame_id}")
        raw_path = ROOT / execution_frame["candidate"]
        check_image(raw_path, expected_size, execution_frame["candidate_sha256"])
        target_path = ROOT / final_frame["presentation_target"]
        check_image(target_path, expected_size, final_frame["presentation_target_sha256"])
        if contact_frame["presentation_target_sha256"] != final_frame["presentation_target_sha256"]:
            raise AssertionError(f"Contact sheet frame mismatch for {frame_id}")

        workflow_path = ROOT / execution_frame["workflow"]
        workflow = load_json(workflow_path)
        if workflow["1"]["inputs"]["unet_name"] != "krea2_turbo_int8_convrot.safetensors":
            raise AssertionError(f"Wrong Krea 2 UNET in {workflow_path}")
        if workflow["2"]["inputs"]["type"] != "krea2":
            raise AssertionError(f"Wrong CLIP mode in {workflow_path}")
        if workflow["12"]["inputs"]["noise_seed"] != spec["shared_seed"]:
            raise AssertionError(f"Workflow seed mismatch in {workflow_path}")
        if workflow["14"]["inputs"]["steps"] != 8:
            raise AssertionError(f"Workflow step mismatch in {workflow_path}")

    contact_path = ROOT / contact["contact_sheet"]
    check_image(contact_path, (1920, 480), contact["sha256"])
    if contact["status"] != "awaiting-owner-approval":
        raise AssertionError("Gate A must remain awaiting owner approval")

    print(
        "M6_1_GATE_A_VALIDATION_OK",
        "models=4",
        "frames=3",
        f"contact_sheet_sha256={contact['sha256']}",
    )


if __name__ == "__main__":
    main()
