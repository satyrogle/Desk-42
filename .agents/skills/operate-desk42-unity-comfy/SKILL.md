---
name: operate-desk42-unity-comfy
description: Operate the branch-local Unity and ComfyUI MCP toolchain for Desk 42. Use when generating, revising, importing, configuring, or wiring game art; editing Unity scenes, prefabs, components, materials, cameras, or UI; verifying visual changes; or diagnosing the local Unity MCP or ComfyUI connection.
---

# Operate Desk 42 Unity + ComfyUI

Use ComfyUI for asset generation and iteration, then use Unity MCP for import settings, references, scene/prefab wiring, tests, and visual verification. Keep generation scratch outside the repository and add only reviewed assets.

## Start and verify

1. Run `scripts/Ensure-Desk42Mcp.ps1` before an MCP-heavy task. Use `-CheckOnly -Json` when the applications should already be running.
2. If the script starts either application, wait for both health checks to pass. Restart the Codex task if the MCP namespaces were absent at task startup; project MCP configuration is loaded from `.codex/config.toml` only for trusted repositories.
3. Confirm both MCP namespaces are available. Inspect their current tool/resource schemas instead of assuming old Claude tool signatures still match.
4. Read [references/comfyui-assets.md](references/comfyui-assets.md) for generation/import work. Read [references/unity-editor.md](references/unity-editor.md) for editor, scene, prefab, component, or verification work. Read both for end-to-end asset tasks.

Do not expose ports 8080 or 8188 beyond loopback. Do not launch another Unity instance when this project is already open.

## Choose the path

- For an asset-only request, generate into ComfyUI's external output folder, inspect every candidate, and stop before repository import unless the user also asked to integrate it.
- For editor-only wiring, inspect the live Unity state first and change only the requested scene, prefab, component, material, or importer.
- For an end-to-end request, follow the gated pipeline below.
- For C# systems code, edit repository files directly. Use Unity MCP to observe compilation, inspect the console, run tests, and wire serialized references.

## Run the gated asset pipeline

1. **Discover.** Inspect the destination prefab/scene/ScriptableObject, existing filenames, sprite scale, references, render pipeline, and visible composition. Never invent Unity object names or asset paths.
2. **Specify.** Define the asset category, target on-screen size, background/alpha, viewing angle, tier set, naming, and acceptance criteria before spending a generation.
3. **Generate.** Prefer installed local models. Use reference-guided ControlNet or IP-Adapter when a prop must retain a recognizable silhouette. Keep references licensed for reuse and record provenance.
4. **Inspect.** View candidate images through the Comfy MCP image tool. Reject non-diegetic HUD styling, bad alpha, illegible forms, incoherent tier progression, or obvious artifacts. Iterate in scratch space.
5. **Curate.** Copy only the chosen PNG into the correct `Assets/_Project/Art/` folder. Preserve an existing filename only when replacement is deliberate and its references have been inspected.
6. **Import.** Let Unity import the file, then configure the TextureImporter through supported editor tooling. Do not hand-edit `.meta` files when Unity can own the serialization.
7. **Wire.** Update the target prefab, scene object, component, material, UI element, or ScriptableObject through Unity MCP. Save the owning asset or scene explicitly.
8. **Verify.** Wait for Unity readiness, check compiler/console errors, run proportionate tests, capture a focused screenshot plus context view, and compare against the acceptance criteria.
9. **Report.** State the generated/imported files, Unity objects changed, verification evidence, remaining manual checks, and whether scratch output remains outside the repo.

Do not silently continue past a failed gate. Fix it or report the precise blocker.

## Unity mutation discipline

- Read editor state before a multi-step mutation. Wait while Unity compiles, reloads the domain, enters play mode, or reports blocking reasons.
- Batch independent discovery or mutations when the Unity server supports it. Keep dependent steps ordered and fail fast.
- After editing C#, wait until compilation completes and then read console errors before attaching the component or changing serialized data.
- Use optimistic file checks or current hashes when a Unity script-editing tool provides them; re-read after a stale-file response.
- Use screenshots for visual claims. A successful tool response is not proof that the result looks correct.
- Avoid destructive scene replacement, asset deletion, package changes, and broad hierarchy rewrites unless explicitly requested.

## Comfy mutation discipline

- Check installed models and node availability before building a workflow.
- Prefer fixed seeds across a tier set, changing controlled distortion inputs rather than every variable at once.
- Do not download models or custom-node packs without explicit authorization; those are large, persistent machine changes.
- Do not use paid/API nodes without explicit authorization. Prefer local GPU execution.
- Keep ComfyUI input/output scratch external to Git. Preserve workflow/provenance metadata for curated assets.
- Generate original work. Do not request imitation of a living artist or a specific copyrighted production style.

## Recover connections

- If Unity MCP is missing, verify the correct editor is open, leave Play Mode, wait for compilation/domain reload, and open **Window > MCP for Unity** to start or reconfigure the HTTP server. Then restart the Codex task.
- If Comfy tools exist but cannot reach the backend, run the ensure script, verify `http://127.0.0.1:8188/system_stats`, and inspect Comfy logs through MCP.
- If the Comfy stdio namespace is missing, verify Node 22+ and `npx`, then restart the Codex task so `.codex/config.toml` is reloaded.
- If either service listens on a non-loopback address, stop and report it instead of proceeding.
