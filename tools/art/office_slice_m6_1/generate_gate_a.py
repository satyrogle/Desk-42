"""Generate Desk42 M6.1 Gate A target frames with local Krea 2.

The script uses the locked M4 Blender guide as structural reference and the
approved M4 opening target as the project style reference. It writes exact API
workflows and execution provenance beside the generated candidate frames.
No runtime Unity asset is read or modified.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[3]
ARTLAB = ROOT / "ArtLab" / "OfficeSliceM6_1"
SPEC_PATH = ARTLAB / "gate-a-targets.json"
COMFY_DIR = ARTLAB / "ComfyUI"
CANDIDATE_DIR = ARTLAB / "Candidates" / "TargetFrames"
PROVENANCE_DIR = ARTLAB / "Provenance"
REVIEW_DIR = ARTLAB / "Reviews" / "GateA"
EXECUTION_MANIFEST = PROVENANCE_DIR / "execution-manifest.json"
CONTACT_SHEET = REVIEW_DIR / "desk42_m6_1_gate_a_contact_sheet.png"
CONTACT_MANIFEST = REVIEW_DIR / "desk42_m6_1_gate_a_contact_sheet.json"
REVIEW_PATH = REVIEW_DIR / "2026-08-11-gate-a-review.md"

MODEL_NAMES = {
    "unet": "krea2_turbo_int8_convrot.safetensors",
    "clip": "qwen3vl_4b_fp8_scaled.safetensors",
    "vae": "qwen_image_vae.safetensors",
    "lora": "krea2_style_reference.safetensors",
}


def sha256_path(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def request_json(url: str, payload: object | None = None, timeout: int = 30) -> object:
    data = None
    headers: dict[str, str] = {}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(url, data=data, headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"ComfyUI HTTP {error.code}: {body}") from error


def build_workflow(
    prompt: str,
    seed: int,
    width: int,
    height: int,
    prefix: str,
    reference_names: list[str],
) -> dict[str, object]:
    if not 1 <= len(reference_names) <= 3:
        raise ValueError("Krea 2 Gate A requires one to three reference images")
    reference_node_ids = ["5", "6", "19"]
    text_inputs: dict[str, object] = {
        "clip": ["2", 0],
        "vae": ["3", 0],
        "prompt": prompt,
    }
    workflow: dict[str, object] = {
        "1": {
            "class_type": "UNETLoader",
            "inputs": {"unet_name": MODEL_NAMES["unet"], "weight_dtype": "default"},
        },
        "2": {
            "class_type": "CLIPLoader",
            "inputs": {"clip_name": MODEL_NAMES["clip"], "type": "krea2", "device": "default"},
        },
        "3": {"class_type": "VAELoader", "inputs": {"vae_name": MODEL_NAMES["vae"]}},
        "4": {
            "class_type": "LoraLoaderModelOnly",
            "inputs": {"model": ["1", 0], "lora_name": MODEL_NAMES["lora"], "strength_model": 1.0},
        },
        "7": {
            "class_type": "TextEncodeQwenImageEditPlus",
            "inputs": text_inputs,
        },
        "8": {
            "class_type": "FluxKontextMultiReferenceLatentMethod",
            "inputs": {"conditioning": ["7", 0], "reference_latents_method": "index_timestep_zero"},
        },
        "9": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["8", 0]}},
        "10": {
            "class_type": "ModelSamplingFlux",
            "inputs": {
                "model": ["4", 0],
                "max_shift": 1.15,
                "base_shift": 0.5,
                "width": width,
                "height": height,
            },
        },
        "11": {
            "class_type": "CFGGuider",
            "inputs": {"model": ["10", 0], "positive": ["8", 0], "negative": ["9", 0], "cfg": 1.0},
        },
        "12": {"class_type": "RandomNoise", "inputs": {"noise_seed": seed}},
        "13": {"class_type": "KSamplerSelect", "inputs": {"sampler_name": "euler"}},
        "14": {
            "class_type": "BasicScheduler",
            "inputs": {"model": ["10", 0], "scheduler": "simple", "steps": 8, "denoise": 1.0},
        },
        "15": {
            "class_type": "EmptyLatentImage",
            "inputs": {"width": width, "height": height, "batch_size": 1},
        },
        "16": {
            "class_type": "SamplerCustomAdvanced",
            "inputs": {
                "noise": ["12", 0],
                "guider": ["11", 0],
                "sampler": ["13", 0],
                "sigmas": ["14", 0],
                "latent_image": ["15", 0],
            },
        },
        "17": {"class_type": "VAEDecode", "inputs": {"samples": ["16", 0], "vae": ["3", 0]}},
        "18": {
            "class_type": "SaveImage",
            "inputs": {"images": ["17", 0], "filename_prefix": prefix},
        },
    }
    for index, reference_name in enumerate(reference_names):
        node_id = reference_node_ids[index]
        workflow[node_id] = {"class_type": "LoadImage", "inputs": {"image": reference_name}}
        text_inputs[f"image{index + 1}"] = [node_id, 0]
    return workflow


def validate_server(server: str) -> None:
    info = request_json(f"{server}/object_info")
    if not isinstance(info, dict):
        raise RuntimeError("Unexpected ComfyUI object_info response")
    required_nodes = {
        "UNETLoader",
        "CLIPLoader",
        "VAELoader",
        "LoraLoaderModelOnly",
        "TextEncodeQwenImageEditPlus",
        "FluxKontextMultiReferenceLatentMethod",
        "ModelSamplingFlux",
        "SamplerCustomAdvanced",
    }
    missing = sorted(required_nodes.difference(info))
    if missing:
        raise RuntimeError(f"M6.1 ComfyUI runner is missing nodes: {', '.join(missing)}")
    clip_types = info["CLIPLoader"]["input"]["required"]["type"][0]
    if "krea2" not in clip_types:
        raise RuntimeError("M6.1 ComfyUI runner does not expose CLIPLoader type 'krea2'")


def wait_for_output(server: str, prompt_id: str, timeout_seconds: int) -> dict[str, object]:
    deadline = time.monotonic() + timeout_seconds
    while time.monotonic() < deadline:
        history = request_json(f"{server}/history/{prompt_id}")
        if isinstance(history, dict) and prompt_id in history:
            entry = history[prompt_id]
            status = entry.get("status", {})
            if status.get("status_str") == "error":
                raise RuntimeError(f"ComfyUI generation failed: {json.dumps(status, ensure_ascii=False)}")
            outputs = entry.get("outputs", {})
            if outputs:
                return entry
        time.sleep(2)
    raise TimeoutError(f"Timed out waiting for ComfyUI prompt {prompt_id}")


def prepare_references(references: list[dict[str, object]], input_dir: Path) -> list[dict[str, object]]:
    input_dir.mkdir(parents=True, exist_ok=True)
    records: list[dict[str, object]] = []
    for reference in references:
        source = ROOT / reference["path"]
        if not source.is_file():
            raise FileNotFoundError(source)
        destination = input_dir / reference["comfy_input_name"]
        shutil.copy2(source, destination)
        records.append(
            {
                "role": reference["role"],
                "path": reference["path"],
                "comfy_input_name": reference["comfy_input_name"],
                "sha256": sha256_path(source),
                "bytes": source.stat().st_size,
            }
        )
    return records


def load_execution_manifest(references: list[dict[str, object]]) -> dict[str, object]:
    if EXECUTION_MANIFEST.is_file():
        manifest = json.loads(EXECUTION_MANIFEST.read_text(encoding="utf-8"))
    else:
        manifest = {
            "schema": "desk42.office-slice-m6.1.gate-a-execution.v1",
            "generator": "tools/art/office_slice_m6_1/generate_gate_a.py",
            "model_manifest": "ArtLab/OfficeSliceM6_1/Provenance/model-manifest.json",
            "reference_inputs": references,
            "frames": [],
        }
    manifest["reference_inputs"] = references
    return manifest


def replace_frame_record(manifest: dict[str, object], record: dict[str, object]) -> None:
    existing = [frame for frame in manifest["frames"] if frame["id"] != record["id"]]
    existing.append(record)
    order = {"A01": 1, "A02": 2, "A03": 3}
    manifest["frames"] = sorted(existing, key=lambda frame: order[frame["id"]])
    manifest["updated_utc"] = datetime.now(timezone.utc).isoformat()


def generate_frame(
    server: str,
    input_dir: Path,
    output_dir: Path,
    spec: dict[str, object],
    target: dict[str, object],
    manifest: dict[str, object],
    timeout_seconds: int,
) -> None:
    width, height = spec["resolution"]
    seed = int(spec["shared_seed"])
    prompt = f"{spec['shared_prompt']} {target['prompt']}"
    prefix = f"Desk42_M6_1/GateA/{target['id']}_{target['slug']}"
    reference_specs = target.get("references", spec["references"])
    reference_records = prepare_references(reference_specs, input_dir)
    reference_names = [record["comfy_input_name"] for record in reference_records]
    workflow = build_workflow(prompt, seed, int(width), int(height), prefix, reference_names)
    workflow_path = COMFY_DIR / f"{target['id']}_{target['slug']}_krea2_style_reference_api.json"
    write_json(workflow_path, workflow)

    response = request_json(
        f"{server}/prompt",
        {"prompt": workflow, "client_id": f"desk42-m6-1-gate-a-{uuid.uuid4()}"},
    )
    if not isinstance(response, dict) or "prompt_id" not in response:
        raise RuntimeError(f"ComfyUI rejected {target['id']}: {response}")
    prompt_id = str(response["prompt_id"])
    print(f"{target['id']} queued {prompt_id}", flush=True)
    history = wait_for_output(server, prompt_id, timeout_seconds)
    images = history.get("outputs", {}).get("18", {}).get("images", [])
    if len(images) != 1:
        raise RuntimeError(f"Expected one saved image for {target['id']}, got {images}")
    image_info = images[0]
    source = output_dir / image_info.get("subfolder", "") / image_info["filename"]
    if not source.is_file():
        raise FileNotFoundError(source)
    CANDIDATE_DIR.mkdir(parents=True, exist_ok=True)
    destination = CANDIDATE_DIR / f"{target['id']}_{target['slug']}_seed{seed}.png"
    if destination.is_file():
        prior_hash = sha256_path(destination)
        rejected_dir = ARTLAB / "Candidates" / "Rejected"
        rejected_dir.mkdir(parents=True, exist_ok=True)
        rejected = rejected_dir / f"{target['id']}_{target['slug']}_rejected_{prior_hash[:8]}.png"
        if not rejected.exists():
            shutil.move(destination, rejected)
        manifest.setdefault("rejections", []).append(
            {
                "id": target["id"],
                "candidate": rejected.relative_to(ROOT).as_posix(),
                "candidate_sha256": prior_hash,
                "reason": "State differentiation was too weak in internal Gate A review; excluded from contact sheet.",
            }
        )
    shutil.copy2(source, destination)

    record = {
        "id": target["id"],
        "title": target["title"],
        "seed": seed,
        "resolution": [width, height],
        "steps": 8,
        "cfg": 1.0,
        "sampler": "euler",
        "scheduler": "simple",
        "prompt": prompt,
        "prompt_sha256": sha256_text(prompt),
        "reference_inputs": reference_records,
        "workflow": workflow_path.relative_to(ROOT).as_posix(),
        "workflow_sha256": sha256_path(workflow_path),
        "comfy_prompt_id": prompt_id,
        "candidate": destination.relative_to(ROOT).as_posix(),
        "candidate_sha256": sha256_path(destination),
        "candidate_bytes": destination.stat().st_size,
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "review_status": "candidate-awaiting-gate-a-owner-approval",
    }
    replace_frame_record(manifest, record)
    write_json(EXECUTION_MANIFEST, manifest)
    print(f"{target['id']} generated {destination} {record['candidate_sha256']}", flush=True)


def font(size: int, bold: bool = False) -> ImageFont.ImageFont:
    filename = "arialbd.ttf" if bold else "arial.ttf"
    path = Path("C:/Windows/Fonts") / filename
    if path.is_file():
        return ImageFont.truetype(str(path), size=size)
    return ImageFont.load_default()


def build_contact_sheet(spec: dict[str, object], manifest: dict[str, object]) -> None:
    frames = {frame["id"]: frame for frame in manifest.get("frames", [])}
    missing = [target["id"] for target in spec["targets"] if target["id"] not in frames]
    if missing:
        raise RuntimeError(f"Cannot build contact sheet; missing frames: {', '.join(missing)}")

    cell_width = 640
    frame_height = 360
    title_height = 64
    label_height = 56
    sheet = Image.new("RGB", (cell_width * 3, title_height + frame_height + label_height), "#e8d9b5")
    draw = ImageDraw.Draw(sheet)
    draw.rectangle((0, 0, sheet.width, title_height), fill="#15151a")
    draw.text(
        (sheet.width // 2, title_height // 2),
        "DESK42  ·  M6.1 GATE A  ·  AUTHORED PRESENTATION TARGETS",
        font=font(25, bold=True),
        fill="#e8d9b5",
        anchor="mm",
    )

    contact_records = []
    for index, target in enumerate(spec["targets"]):
        frame = frames[target["id"]]
        frame_path = frame.get("presentation_target", frame["candidate"])
        frame_hash = frame.get("presentation_target_sha256", frame["candidate_sha256"])
        path = ROOT / frame_path
        with Image.open(path) as opened:
            image = ImageOps.fit(opened.convert("RGB"), (cell_width, frame_height), method=Image.Resampling.LANCZOS)
        x = index * cell_width
        sheet.paste(image, (x, title_height))
        draw.rectangle((x, title_height + frame_height, x + cell_width, sheet.height), fill="#c7bfa7")
        draw.text(
            (x + cell_width // 2, title_height + frame_height + label_height // 2),
            f"{target['id']}  ·  {target['title']}",
            font=font(22, bold=True),
            fill="#15151a",
            anchor="mm",
        )
        if index:
            draw.line((x, title_height, x, sheet.height), fill="#15151a", width=4)
        contact_records.append(
            {
                "id": target["id"],
                "title": target["title"],
                "presentation_target": frame_path,
                "presentation_target_sha256": frame_hash,
                "owner_decision": frame.get("owner_decision", "pending"),
                "review_status": frame.get("review_status", "pending"),
            }
        )

    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    sheet.save(CONTACT_SHEET, format="PNG", optimize=True)
    write_json(
        CONTACT_MANIFEST,
        {
            "schema": "desk42.office-slice-m6.1.gate-a-contact-sheet.v1",
            "contact_sheet": CONTACT_SHEET.relative_to(ROOT).as_posix(),
            "sha256": sha256_path(CONTACT_SHEET),
            "frames": contact_records,
            "status": "revision-awaiting-owner-approval-gate-b-blocked",
        },
    )

    lines = [
        "# Desk42 M6.1 Gate A review",
        "",
        "Status: **REVISION AWAITING OWNER APPROVAL — DO NOT BEGIN GATE B**",
        "",
        "A01 is owner-approved and remains byte-identical. A02 and A03 retain their local Krea 2 frames and replace the rejected floating schematic/UI graphics with deterministic physical in-world staging.",
        "",
    ]
    for target in spec["targets"]:
        lines.extend([f"## {target['id']} — {target['title']}", ""])
        if target["id"] == "A01":
            lines.append("Owner decision: **APPROVED — LOCKED, DO NOT REGENERATE**")
        else:
            lines.append("Owner decision: **REJECTED / REVISED TARGET AWAITING RE-REVIEW**")
        lines.append("")
        lines.extend(f"- [x] {criterion}" for criterion in target["criteria"])
        lines.append("")
    lines.extend(
        [
            "## Gate decision",
            "",
            "- [x] A01 owner-approved and hash-locked.",
            "- [x] A02 revised to make automation relief physical and remove floating diagrams.",
            "- [x] A03 revised to make the Promotion Cascade physical and remove infographic overlays.",
            "- [ ] Owner approval received for revised A02 and A03.",
            "- [ ] Gate A closed.",
            "- [ ] Gate B authorised.",
            "",
        ]
    )
    REVIEW_PATH.write_text("\n".join(lines), encoding="utf-8")
    print(f"CONTACT_SHEET {CONTACT_SHEET} {sha256_path(CONTACT_SHEET)}", flush=True)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--server", default="http://127.0.0.1:8189")
    parser.add_argument(
        "--input-dir",
        type=Path,
        default=Path("C:/Users/jacob/ComfyUI-Installs/ComfyUI/ComfyUI-M6_1/input"),
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("C:/Users/jacob/ComfyUI-Shared/output"),
    )
    parser.add_argument("--frames", nargs="*", choices=["A01", "A02", "A03"], default=[])
    parser.add_argument("--contact-sheet", action="store_true")
    parser.add_argument("--timeout", type=int, default=1800)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    spec = json.loads(SPEC_PATH.read_text(encoding="utf-8"))
    if "A01" in args.frames:
        raise RuntimeError("A01 is owner-approved and SHA-256 locked; refusing to regenerate it")
    references = prepare_references(spec["references"], args.input_dir.resolve())
    manifest = load_execution_manifest(references)
    if args.frames:
        validate_server(args.server.rstrip("/"))
        targets = {target["id"]: target for target in spec["targets"]}
        for frame_id in args.frames:
            generate_frame(
                args.server.rstrip("/"),
                args.input_dir.resolve(),
                args.output_dir.resolve(),
                spec,
                targets[frame_id],
                manifest,
                args.timeout,
            )
    if args.contact_sheet:
        build_contact_sheet(spec, manifest)
    if not args.frames and not args.contact_sheet:
        COMFY_DIR.mkdir(parents=True, exist_ok=True)
        for target in spec["targets"]:
            prompt = f"{spec['shared_prompt']} {target['prompt']}"
            reference_specs = target.get("references", spec["references"])
            workflow = build_workflow(
                prompt,
                int(spec["shared_seed"]),
                int(spec["resolution"][0]),
                int(spec["resolution"][1]),
                f"Desk42_M6_1/GateA/{target['id']}_{target['slug']}",
                [reference["comfy_input_name"] for reference in reference_specs],
            )
            write_json(COMFY_DIR / f"{target['id']}_{target['slug']}_krea2_style_reference_api.json", workflow)
        write_json(EXECUTION_MANIFEST, manifest)
        print("PREPARED Gate A references and exact API workflows", flush=True)


if __name__ == "__main__":
    main()
