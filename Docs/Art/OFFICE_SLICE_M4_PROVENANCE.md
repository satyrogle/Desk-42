# Office Slice M4 Asset Provenance

The runtime visual target is produced from deterministic local scripts, a locked Blender guide, and controlled ComfyUI workflows. Runtime artwork and production sources are deliberately separate.

- Ledger: `ArtLab/OfficeSliceM4/Provenance/asset-ledger.csv`
- Workflow manifest: `ArtLab/OfficeSliceM4/Provenance/workflow-manifest.json`
- Approved sources: `ArtLab/OfficeSliceM4/ApprovedSources/`
- Runtime assets: `Assets/_Project/Art/OfficeSliceM4/`

Each approved item records a stable ID, method, guide/workflow/model/seed where applicable, prompt hashes, source licence, normaliser version, final SHA-256, and reviewer decision. Procedurally drawn project-owned assets use `PROJECT-ORIGINAL` as the reference licence. Generated raster text is prohibited.

Rejected candidates are preserved and receive a rejection reason. The normaliser refuses invalid dimensions, missing alpha where required, or colours outside the declared palette when strict palette validation is selected.
