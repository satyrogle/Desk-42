# Desk42 Office Slice M6.1 ArtLab

Gate A establishes three authored presentation targets only. It does not replace runtime art and it does not begin Gate B.

The local workflow uses:

- the locked M4 Blender guide for camera and office structure;
- the approved M4 opening target for project visual language;
- the official Comfy-Org Krea 2 Turbo INT8 style-reference workflow and model set;
- one fixed seed across A01, A02 and A03, with A01 reused as the same-office base for spatially authored A02 and A03 edit guides;
- exact API workflows, prompts, hashes and ComfyUI prompt IDs recorded under `Provenance`.
- deterministic hand cleanup after Krea generation for the A02 machine flow and A03 local break readability.

Reproduce from the dedicated local ComfyUI runner on port 8189:

```powershell
C:\Users\jacob\ComfyUI-Installs\ComfyUI\comfy-env\Scripts\python.exe `
  tools\art\office_slice_m6_1\generate_gate_a.py `
  --frames A01 A02 A03 `
  --contact-sheet
```

Official model weights remain in the shared ComfyUI model store and are not committed to Git. Gate B remains blocked until the owner approves the three-frame contact sheet.
