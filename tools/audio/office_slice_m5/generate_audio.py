"""Generate the deterministic Office Slice M5 project-original audio library."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import platform
import struct
import wave
from dataclasses import asdict, dataclass
from pathlib import Path


SAMPLE_RATE = 48_000
WORKFLOW_VERSION = "1.0.0"
STAGE_ORDER = {"A": 0, "B": 1, "C": 2, "D": 3}


@dataclass(frozen=True)
class AssetSpec:
    stage: str
    asset_id: str
    category: str
    kind: str
    duration: float
    frequency: float
    stereo: bool = False


@dataclass(frozen=True)
class CueSpec:
    stage: str
    cue_id: str
    asset_id: str
    bus: str
    loop: bool = False
    pan: float = 0.0
    base_volume: float = 0.5


ASSETS = [
    AssetSpec("A", "ambience.calm", "Ambience", "ambience", 4.0, 50, True),
    AssetSpec("A", "ambience.rush", "Ambience", "ambience", 4.0, 64, True),
    AssetSpec("A", "ambience.break", "Ambience", "ambience", 4.0, 43, True),
    AssetSpec("A", "ambience.recovery", "Ambience", "ambience", 4.0, 56, True),
    AssetSpec("A", "ambience.result", "Ambience", "ambience", 4.0, 47, True),
    AssetSpec("A", "music.work", "Music", "music", 5.0, 196, True),
    AssetSpec("A", "music.pressure", "Music", "music", 5.0, 247, True),
    AssetSpec("A", "music.break", "Music", "music", 5.0, 147, True),

    AssetSpec("B", "warden.step", "Player", "shot", 0.12, 118),
    AssetSpec("B", "folder.take", "Player", "paper", 0.22, 520),
    AssetSpec("B", "folder.send", "Player", "paper", 0.28, 380),
    AssetSpec("B", "action.interact", "Player", "shot", 0.15, 680),
    AssetSpec("B", "action.start", "Player", "mechanical", 0.18, 230),
    AssetSpec("B", "help.complete", "Player", "confirm", 0.30, 620),
    AssetSpec("B", "calm.complete", "Player", "confirm", 0.42, 530),
    AssetSpec("B", "fix.complete", "Player", "mechanical", 0.36, 310),
    AssetSpec("B", "choice.confirm", "Player", "confirm", 0.24, 740),
    AssetSpec("B", "decision.stamp", "Player", "stamp", 0.30, 92),
    AssetSpec("B", "action.invalid", "Player", "reject", 0.23, 185),
    AssetSpec("B", "paper.open", "ManualWork", "paper", 0.30, 460),
    AssetSpec("B", "money.open", "ManualWork", "mechanical", 0.32, 275),
    AssetSpec("B", "manual.selection", "ManualWork", "shot", 0.12, 420),
    AssetSpec("B", "trace.movement", "ManualWork", "mechanical", 0.14, 335),
    AssetSpec("B", "manual.correct", "ManualWork", "confirm", 0.32, 760),
    AssetSpec("B", "manual.incorrect", "ManualWork", "reject", 0.34, 205),

    AssetSpec("C", "customer.worried", "Customer", "shot", 0.30, 330),
    AssetSpec("C", "customer.upset", "Customer", "reject", 0.38, 255),
    AssetSpec("C", "customer.strange", "Customer", "shot", 0.48, 415),
    AssetSpec("C", "customer.calm-response", "Customer", "confirm", 0.35, 560),
    AssetSpec("C", "customer.recovery-response", "Customer", "confirm", 0.46, 680),
]

for machine_index, machine in enumerate([
    "front-desk-counter", "paper-check", "money-trace", "auto-sorter",
    "copy-echo", "ghost-clock", "supervisor-stamp",
]):
    ASSETS.append(AssetSpec(
        "C", f"machine.{machine}.idle", "Machine", "machine-loop", 2.0,
        72 + machine_index * 29))
    ASSETS.append(AssetSpec(
        "C", f"machine.{machine}.active", "Machine", "machine-loop", 2.0,
        96 + machine_index * 37))

ASSETS.extend([
    AssetSpec("C", "machine.shared.warning", "Machine", "warning", 0.42, 360),
    AssetSpec("C", "machine.shared.break", "Machine", "reject", 0.58, 112),
    AssetSpec("C", "machine.shared.recovered", "Machine", "confirm", 0.42, 610),
    AssetSpec("C", "automation.enabled", "Automation", "confirm", 0.28, 690),
    AssetSpec("C", "automation.disabled", "Automation", "reject", 0.24, 260),
    AssetSpec("C", "automation.match", "Automation", "confirm", 0.26, 820),
    AssetSpec("C", "automation.reject", "Automation", "reject", 0.27, 225),
    AssetSpec("C", "automation.copied-accepted", "Automation", "confirm", 0.34, 775),

    AssetSpec("D", "event.copy-echo-trigger", "MajorEvent", "warning", 0.72, 155),
    AssetSpec("D", "event.copy-spawn", "MajorEvent", "paper", 0.26, 625),
    AssetSpec("D", "event.copy-clear", "MajorEvent", "paper", 0.24, 450),
    AssetSpec("D", "event.ghost-clock", "MajorEvent", "clock", 0.74, 910),
    AssetSpec("D", "event.missing-room", "MajorEvent", "warning", 0.82, 205),
    AssetSpec("D", "event.promotion-trigger", "MajorEvent", "warning", 0.92, 130),
    AssetSpec("D", "event.copier-promoted", "MajorEvent", "confirm", 0.58, 490),
    AssetSpec("D", "event.runner-allegiance", "MajorEvent", "mechanical", 0.48, 345),
    AssetSpec("D", "event.supervisor-removed", "MajorEvent", "stamp", 0.38, 105),
    AssetSpec("D", "event.recovery-complete", "MajorEvent", "confirm", 0.84, 640),
    AssetSpec("D", "event.shift-close", "MajorEvent", "bell", 0.72, 392),
    AssetSpec("D", "event.final-result", "MajorEvent", "bell", 1.10, 523),
    AssetSpec("D", "event.next-day-tease", "MajorEvent", "clock", 0.64, 784),
])


CUES = [
    CueSpec("A", "ambience.calm", "ambience.calm", "Ambience", True, 0, 0.23),
    CueSpec("A", "ambience.rush", "ambience.rush", "Ambience", True, 0, 0.24),
    CueSpec("A", "ambience.break", "ambience.break", "Ambience", True, 0, 0.27),
    CueSpec("A", "ambience.recovery", "ambience.recovery", "Ambience", True, 0, 0.22),
    CueSpec("A", "ambience.result", "ambience.result", "Ambience", True, 0, 0.20),
    CueSpec("A", "music.work", "music.work", "Music", True, 0, 0.18),
    CueSpec("A", "music.pressure", "music.pressure", "Music", True, 0, 0.16),
    CueSpec("A", "music.break", "music.break", "Music", True, 0, 0.19),

    CueSpec("B", "warden.step.a", "warden.step", "SFX", False, -0.08, 0.24),
    CueSpec("B", "warden.step.b", "warden.step", "SFX", False, 0.08, 0.20),
    CueSpec("B", "folder.take", "folder.take", "SFX", False, 0, 0.55),
    CueSpec("B", "folder.drop", "folder.send", "SFX", False, 0, 0.54),
    CueSpec("B", "folder.send", "folder.send", "SFX", False, 0.15, 0.56),
    CueSpec("B", "action.interact", "action.interact", "SFX", False, 0, 0.42),
    CueSpec("B", "help.start", "action.start", "SFX", False, 0, 0.40),
    CueSpec("B", "help.complete", "help.complete", "SFX", False, 0, 0.54),
    CueSpec("B", "calm.start", "action.start", "SFX", False, 0, 0.34),
    CueSpec("B", "calm.complete", "calm.complete", "SFX", False, 0, 0.52),
    CueSpec("B", "fix.start", "action.start", "SFX", False, 0.35, 0.44),
    CueSpec("B", "fix.complete", "fix.complete", "SFX", False, 0.35, 0.58),
    CueSpec("B", "choice.confirm", "choice.confirm", "SFX", False, 0, 0.52),
    CueSpec("B", "decision.stamp", "decision.stamp", "SFX", False, -0.55, 0.62),
    CueSpec("B", "action.invalid", "action.invalid", "SFX", False, 0, 0.48),
    CueSpec("B", "paper.open", "paper.open", "SFX", False, -0.2, 0.48),
    CueSpec("B", "paper.selection", "manual.selection", "SFX", False, -0.2, 0.38),
    CueSpec("B", "paper.correct", "manual.correct", "SFX", False, -0.2, 0.61),
    CueSpec("B", "paper.incorrect", "manual.incorrect", "SFX", False, -0.2, 0.55),
    CueSpec("B", "money.open", "money.open", "SFX", False, 0.25, 0.50),
    CueSpec("B", "money.trace", "trace.movement", "SFX", False, 0.25, 0.36),
    CueSpec("B", "money.correct", "manual.correct", "SFX", False, 0.25, 0.64),
    CueSpec("B", "money.incorrect", "manual.incorrect", "SFX", False, 0.25, 0.57),

    CueSpec("C", "customer.worried", "customer.worried", "SFX", False, -0.35, 0.48),
    CueSpec("C", "customer.upset", "customer.upset", "SFX", False, -0.35, 0.62),
    CueSpec("C", "customer.strange", "customer.strange", "SFX", False, 0.30, 0.58),
    CueSpec("C", "customer.calm-response", "customer.calm-response", "SFX", False, -0.25, 0.46),
    CueSpec("C", "customer.recovery-response", "customer.recovery-response", "SFX", False, -0.25, 0.50),
]

MACHINES = [
    ("front-desk-counter", -0.70), ("paper-check", -0.25),
    ("money-trace", 0.25), ("auto-sorter", 0.62),
    ("copy-echo", 0.55), ("ghost-clock", 0.08),
    ("supervisor-stamp", 0.68),
]
for machine, pan in MACHINES:
    CUES.extend([
        CueSpec("C", f"machine.{machine}.idle", f"machine.{machine}.idle", "Ambience", True, pan, 0.10),
        CueSpec("C", f"machine.{machine}.active", f"machine.{machine}.active", "Ambience", True, pan, 0.15),
        CueSpec("C", f"machine.{machine}.warning", "machine.shared.warning", "SFX", False, pan, 0.55),
        CueSpec("C", f"machine.{machine}.break", "machine.shared.break", "SFX", False, pan, 0.68),
        CueSpec("C", f"machine.{machine}.recovered", "machine.shared.recovered", "SFX", False, pan, 0.58),
    ])

CUES.extend([
    CueSpec("C", "automation.enabled", "automation.enabled", "SFX", False, 0.62, 0.55),
    CueSpec("C", "automation.disabled", "automation.disabled", "SFX", False, 0.62, 0.48),
    CueSpec("C", "automation.match", "automation.match", "SFX", False, 0.55, 0.56),
    CueSpec("C", "automation.reject", "automation.reject", "SFX", False, 0.55, 0.50),
    CueSpec("C", "automation.copied-accepted", "automation.copied-accepted", "SFX", False, 0.58, 0.58),
    CueSpec("C", "automation.second-rule-match", "automation.match", "SFX", False, 0.25, 0.54),

    CueSpec("D", "event.copy-echo-trigger", "event.copy-echo-trigger", "SFX", False, 0.55, 0.74),
    CueSpec("D", "event.copy-spawn", "event.copy-spawn", "SFX", False, 0.50, 0.54),
    CueSpec("D", "event.copy-clear", "event.copy-clear", "SFX", False, 0.35, 0.50),
    CueSpec("D", "event.copier-stop", "fix.complete", "SFX", False, 0.55, 0.64),
    CueSpec("D", "event.ghost-clock", "event.ghost-clock", "SFX", False, 0.10, 0.68),
    CueSpec("D", "event.missing-room", "event.missing-room", "SFX", False, 0.70, 0.68),
    CueSpec("D", "event.promotion-trigger", "event.promotion-trigger", "SFX", False, 0.58, 0.78),
    CueSpec("D", "event.copier-promoted", "event.copier-promoted", "SFX", False, 0.58, 0.64),
    CueSpec("D", "event.runner-allegiance", "event.runner-allegiance", "SFX", False, -0.10, 0.62),
    CueSpec("D", "event.supervisor-removed", "event.supervisor-removed", "SFX", False, 0.68, 0.62),
    CueSpec("D", "event.recovery-complete", "event.recovery-complete", "SFX", False, 0, 0.72),
    CueSpec("D", "event.shift-close", "event.shift-close", "SFX", False, 0, 0.62),
    CueSpec("D", "event.upgrade-chosen", "choice.confirm", "SFX", False, 0, 0.62),
    CueSpec("D", "event.final-result", "event.final-result", "SFX", False, 0, 0.68),
    CueSpec("D", "event.next-day-tease", "event.next-day-tease", "SFX", False, 0, 0.46),
])


def stable_frequency(frequency: float, duration: float) -> float:
    return max(1.0, round(frequency * duration) / duration)


def deterministic_noise(index: int, seed: int) -> float:
    value = (index * 1_103_515_245 + seed * 12_345 + 0x9E3779B9) & 0x7FFFFFFF
    return (value / 1_073_741_823.5) - 1.0


def envelope(index: int, count: int, attack: float = 0.01, release: float = 0.10) -> float:
    progress = index / max(1, count - 1)
    attack_gain = min(1.0, progress / max(0.0001, attack))
    release_gain = min(1.0, (1.0 - progress) / max(0.0001, release))
    return max(0.0, min(attack_gain, release_gain))


def synthesize(spec: AssetSpec, seed: int) -> list[tuple[float, float]]:
    count = max(1, round(spec.duration * SAMPLE_RATE))
    frequency = stable_frequency(spec.frequency, spec.duration)
    frames: list[tuple[float, float]] = []
    for index in range(count):
        time = index / SAMPLE_RATE
        progress = index / count
        base = 0.0
        if spec.kind == "ambience":
            base = (
                math.sin(math.tau * frequency * time) * 0.18
                + math.sin(math.tau * frequency * 2 * time) * 0.07
                + math.sin(math.tau * 0.5 * time) * 0.05
                + deterministic_noise(index, seed) * 0.035
            )
            gain = envelope(index, count, 0.03, 0.03)
        elif spec.kind == "music":
            beat = int(progress * 8) % 8
            ratios = (1.0, 1.25, 1.5, 1.25, 1.0, 1.5, 1.25, 1.75)
            local = (progress * 8) % 1.0
            note = stable_frequency(frequency * ratios[beat], spec.duration)
            note_env = math.exp(-local * 4.0) * min(1.0, local * 20.0)
            base = (
                math.sin(math.tau * note * time) * 0.20
                + math.sin(math.tau * note * 0.5 * time) * 0.10
            ) * note_env
            gain = envelope(index, count, 0.01, 0.02)
        elif spec.kind == "machine-loop":
            pulse = 1.0 if (progress * 8) % 1.0 < 0.16 else 0.18
            base = (
                math.sin(math.tau * frequency * time) * 0.22 * pulse
                + math.sin(math.tau * frequency * 3 * time) * 0.045
                + deterministic_noise(index, seed) * 0.02
            )
            gain = envelope(index, count, 0.02, 0.02)
        else:
            gain = envelope(index, count, 0.025, 0.22)
            tone = math.sin(math.tau * frequency * time)
            harmonic = math.sin(math.tau * frequency * 1.5 * time)
            noise = deterministic_noise(index, seed)
            if spec.kind == "paper":
                base = noise * 0.46 + tone * 0.12
            elif spec.kind == "mechanical":
                base = tone * 0.32 + harmonic * 0.18 + noise * 0.12
            elif spec.kind == "confirm":
                second = frequency * (1.25 if progress > 0.48 else 1.0)
                base = math.sin(math.tau * second * time) * 0.46 + harmonic * 0.10
            elif spec.kind == "reject":
                second = frequency * (0.72 if progress > 0.45 else 1.0)
                base = math.sin(math.tau * second * time) * 0.38 + noise * 0.16
            elif spec.kind == "stamp":
                base = tone * 0.18 + noise * (0.56 if progress < 0.18 else 0.08)
            elif spec.kind == "warning":
                alternate = frequency * (1.42 if int(progress * 6) % 2 else 1.0)
                base = math.sin(math.tau * alternate * time) * 0.42 + noise * 0.08
            elif spec.kind == "clock":
                tick = 1.0 if (progress * 12) % 1.0 < 0.10 else 0.0
                base = tick * (tone * 0.38 + noise * 0.20) + harmonic * 0.06
            elif spec.kind == "bell":
                base = tone * 0.36 + math.sin(math.tau * frequency * 2.01 * time) * 0.22
            else:
                base = tone * 0.40 + noise * 0.06
        left = base * gain
        right = left
        if spec.stereo:
            right = (base * 0.88 + math.sin(math.tau * (frequency + 0.5) * time) * 0.04) * gain
        frames.append((left, right))

    peak = max(0.0001, max(max(abs(left), abs(right)) for left, right in frames))
    scale = min(1.0, 0.72 / peak)
    return [(left * scale, right * scale) for left, right in frames]


def write_wav(path: Path, frames: list[tuple[float, float]], stereo: bool) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    channels = 2 if stereo else 1
    payload = bytearray()
    for left, right in frames:
        payload.extend(struct.pack("<h", max(-32767, min(32767, round(left * 32767)))))
        if stereo:
            payload.extend(struct.pack("<h", max(-32767, min(32767, round(right * 32767)))))
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(channels)
        wav.setsampwidth(2)
        wav.setframerate(SAMPLE_RATE)
        wav.writeframes(payload)


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def filename_for(asset_id: str) -> str:
    return asset_id.replace(".", "_") + ".wav"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--through", choices=tuple(STAGE_ORDER), default="D")
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[3])
    args = parser.parse_args()
    root = args.root.resolve()
    through = STAGE_ORDER[args.through]
    selected_assets = [spec for spec in ASSETS if STAGE_ORDER[spec.stage] <= through]
    selected_ids = {spec.asset_id for spec in selected_assets}
    selected_cues = [cue for cue in CUES if cue.asset_id in selected_ids and STAGE_ORDER[cue.stage] <= through]

    lab = root / "AudioLab" / "OfficeSliceM5"
    masters = lab / "ApprovedMasters"
    provenance = lab / "Provenance"
    runtime = root / "Assets" / "_Project" / "Audio" / "OfficeSliceM5" / "Resources" / "OfficeSliceM5"
    manifest_path = runtime / "audio-manifest.json"
    provenance.mkdir(parents=True, exist_ok=True)

    manifest_assets = []
    ledger_rows = []
    for index, spec in enumerate(selected_assets):
        seed = 425_000 + index
        filename = filename_for(spec.asset_id)
        runtime_path = runtime / spec.category / filename
        master_path = masters / spec.category / filename
        frames = synthesize(spec, seed)
        write_wav(master_path, frames, spec.stereo)
        write_wav(runtime_path, frames, spec.stereo)
        digest = sha256(runtime_path)
        relative_runtime = runtime_path.relative_to(root).as_posix()
        relative_master = master_path.relative_to(root).as_posix()
        manifest_assets.append({
            "asset_id": spec.asset_id,
            "resource_path": f"OfficeSliceM5/{spec.category}/{filename[:-4]}",
            "runtime_filename": relative_runtime,
            "category": spec.category,
            "channels": 2 if spec.stereo else 1,
            "sample_rate": SAMPLE_RATE,
            "bit_depth": 16,
            "duration_seconds": spec.duration,
            "loop": spec.kind in {"ambience", "music", "machine-loop"},
            "final_sha256": digest,
        })
        ledger_rows.append({
            "asset_id": spec.asset_id,
            "runtime_path": relative_runtime,
            "category": spec.category,
            "author_source": "OpenAI Codex project-original synthesis",
            "generation_method": f"deterministic {spec.kind} synthesis",
            "tool_version": f"Python {platform.python_version()} / workflow {WORKFLOW_VERSION}",
            "prompt_workflow": "tools/audio/office_slice_m5/generate_audio.py",
            "seed": seed,
            "source_license": "PROJECT-ORIGINAL",
            "source_url_reference": relative_master,
            "edit_normalisation_steps": "48 kHz; 16-bit PCM; deterministic edge fade; peak <= 0.72 FS",
            "review_status": "approved",
            "final_sha256": digest,
        })

    manifest = {
        "schema": "desk42.office-slice-m5.audio-manifest.v1",
        "workflow_version": WORKFLOW_VERSION,
        "through_gate": args.through,
        "assets": manifest_assets,
        "cues": [asdict(cue) for cue in selected_cues],
    }
    manifest_text = json.dumps(manifest, indent=2, sort_keys=True) + "\n"
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(manifest_text, encoding="utf-8")
    (provenance / "runtime-audio-manifest.json").write_text(manifest_text, encoding="utf-8")

    ledger_path = provenance / "audio-ledger.csv"
    with ledger_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(ledger_rows[0]))
        writer.writeheader()
        writer.writerows(ledger_rows)

    workflow = {
        "schema": "desk42.office-slice-m5.synthesis-workflow.v1",
        "workflow_version": WORKFLOW_VERSION,
        "python_version": platform.python_version(),
        "sample_rate": SAMPLE_RATE,
        "bit_depth": 16,
        "authoring_method": "project-original deterministic synthesis",
        "ai_assisted_voice": False,
        "artist_style_prompting": False,
        "source_script_sha256": sha256(Path(__file__)),
        "through_gate": args.through,
        "runtime_asset_count": len(selected_assets),
        "runtime_cue_count": len(selected_cues),
    }
    (provenance / "workflow-manifest.json").write_text(
        json.dumps(workflow, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(f"generated_assets={len(selected_assets)}")
    print(f"generated_cues={len(selected_cues)}")
    print(f"runtime_manifest={manifest_path}")
    print(f"ledger={ledger_path}")


if __name__ == "__main__":
    main()
