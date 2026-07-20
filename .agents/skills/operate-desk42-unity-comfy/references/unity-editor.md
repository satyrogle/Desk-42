# Unity MCP editor workflow

## Local setup

- Project: `C:\Users\jacob\Desk 42`
- Unity: `2022.3.62f3`
- MCP endpoint: `http://127.0.0.1:8080/mcp`
- Package: `com.coplaydev.unity-mcp`, declared in `Packages/manifest.json`
- Unity menu: **Window > MCP for Unity**

The Unity package owns the HTTP bridge. Do not launch `mcp-for-unity` manually because the package supplies the project instance token and run-state files. Use `../scripts/Ensure-Desk42Mcp.ps1` to open the project when needed, then let the package start its bridge.

## Readiness loop

Before editor mutations:

1. Read the editor-state resource exposed by Unity MCP.
2. Confirm this is the `Desk 42` project and select the correct instance if several editors are present.
3. Wait until compilation and domain reload are complete and the server says tools are ready.
4. Inspect the active scene, relevant hierarchy, components, prefab source, and asset references.
5. Perform the smallest scoped mutation.
6. Save the owning scene/prefab/asset.
7. Check console errors and warnings introduced by the change.
8. Capture a focused screenshot and a context screenshot for visual work.

Treat tool names and parameter shapes as runtime-discovered. Typical capabilities include editor state/resources, hierarchy and GameObject search, component management, scene/prefab/material/asset operations, console reading, screenshots, batching, and Unity Test Framework jobs.

Use the ComfyUI namespace for local asset generation. Do not use Unity MCP generation tools that may call external or paid services unless the user explicitly selects and authorizes them.

## Code and compilation

- Edit repository C# with normal file tooling unless an editor-aware Unity tool is necessary.
- After every C# write, wait for Unity compilation. Do not attach or serialize a newly added component before compilation succeeds.
- Read console errors with stack traces. Fix compile errors before continuing with editor wiring.
- Reconnect after a domain reload if the server drops briefly.
- Run focused EditMode tests first, then broader tests when the changed system warrants them.

## Generated image import

Use the live TextureImporter/Unity APIs rather than editing `.meta` YAML directly. Apply settings according to use:

| Use | Texture type | Mesh | Wrap | Filter | Notes |
|---|---|---|---|---|---|
| Desk prop/portrait | Sprite (2D and UI) | Tight | Clamp | Bilinear unless crisp | Enable alpha transparency |
| Pixel/crisp icon | Sprite (2D and UI) | Full Rect or Tight | Clamp | Point | Avoid lossy compression |
| Nine-sliced diegetic panel | Sprite (2D and UI) | Full Rect | Clamp | Bilinear | Set explicit borders |
| Material texture | Default | N/A | Match shader | Bilinear | Confirm color space and alpha use |

Match the Pixels Per Unit used by neighboring assets in the same category. Inspect before choosing; the project has no universal PPU declared yet.

After reimport, inspect the imported sprite/material and verify it in the actual target view. A correct importer is not sufficient if the asset is scaled, cropped, sorted, tinted, or anchored incorrectly.

## Wiring checklist

1. Find the exact target by scene path, prefab path, component type, or asset GUID.
2. Determine whether the change belongs to a prefab asset, prefab instance override, scene object, material, UI document, or ScriptableObject.
3. Update the owning object, not a temporary runtime clone.
4. Set object references using stable Unity references rather than names in gameplay code.
5. Preserve existing sibling order, anchors, sorting layers, material slots, and serialized defaults unless the request changes them.
6. Save and re-read the object to confirm serialization.
7. Enter Play Mode only when necessary, and return to Edit Mode afterward.

## Recovery

- **Port 8080 closed:** open this project in Unity, wait for packages/compilation, then use **Window > MCP for Unity** to start/configure the server.
- **Wrong project/instance:** list Unity instances and select the `Desk 42` instance before mutation.
- **Busy/blocked:** wait for the retry interval; inspect compiler and domain-reload state.
- **Tools disappear after Play Mode:** leave Play Mode, wait for domain reload, and reconnect/restart the Codex task if needed.
- **Stale file:** re-read the current file/hash and reapply the narrow edit.
- **Silent visual failure:** inspect console, component state, material/import settings, camera/canvas, then screenshot again.
