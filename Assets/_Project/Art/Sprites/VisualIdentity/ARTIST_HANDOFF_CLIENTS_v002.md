# Desk 42 claimant and equipment handoff v002

## Canon

Source: `Desk42_Complete_GDD_v1.pdf`, identical to `main:gdd_text.txt`.

- The agency processes claims from aliens, eldritch entities, and interdimensional beings.
- The governing contrast is **the mundane containing the impossible**.
- Claimants treat impossible events with bored bureaucratic professionalism.
- Sober office clothing and a stable corporate portrait crop ground each species.
- Species silhouette communicates identity; behavioural poses communicate BSM state.
- The current `human_standard` claim-template pool is implementation scaffolding, not the claimant art direction.
- `gel_anomaly` is a non-human amorphous claimant wearing bureaucratic clothing. Human-readable emotion does not imply human anatomy or origin.

## Native pixel deliverables

### Core claimant species

- Native cell: `128x128` pixels.
- Master sheet: `Claimants/CoreSpecies/D42_ClaimantRoster_CoreSpecies_128_v002.png`.
- Indexed master palette usage: 27 colours.
- Individual cards: Moth Accountant, Gel Anomaly, Unregistered Alien, Void Proxy.
- The opaque deep-green field is part of the fixed corporate portrait presentation.

### Moth Accountant state keys

- Native cell: `128x128` pixels.
- Master sheet: `Claimants/States/D42_Claimant_MothAccountant_States_128_v002.png`.
- Indexed master palette usage: 26 colours.
- Reading order: Pending, Agitated, Litigious, Cooperative, Suspicious, Resigned, Paranoid, Dissociating, Smug.
- Preserve head, suit, crop, light direction, desk edge, and scale between frames.
- Animate at 6-10 fps with held keys. Tells should precede state changes.

### Office equipment

- Native cell: `64x64` pixels.
- Master sheet: `Equipment/D42_OfficeEquipment_Core_64_v002.png`.
- Indexed master palette usage: 28 colours.
- Individual equipment exports use transparent backgrounds.
- Equipment remains mundane until its state machine changes its established function.

## State tells from the GDD

| State | Primary visual tell |
| --- | --- |
| Pending | Calm hold; hands folded |
| Agitated | Aggressive posture; desk items rattle |
| Litigious | Straightens papers; presents complaint |
| Cooperative | Open posture; reveals hidden document |
| Suspicious | Squints toward the player's notes |
| Resigned | Collapsed shoulders; thousand-yard stare |
| Paranoid | Glances at door; covers mouth |
| Dissociating | Stops responding; impossible stillness |
| Smug | Leans back; feet on desk |

## Production rules

- Work on the native grid, never on the 4x preview.
- Use Point filtering and integer scaling only.
- Use hard clusters; remove generated microtexture and isolated noise during the artist cleanup pass.
- Use one native-pixel selective outline, not a uniform black sticker border.
- Share colour ramps across species, props, desk, and CorpOS.
- Reserve saturated teal/electric blue for mechanical anomaly information and magenta for rare rupture.
- Deliver layered Aseprite/PSD sources plus native PNG exports, pivots, state names, and frame timing.

The `Preview4x` images are nearest-neighbour review copies. They contain no additional resolution or detail.

## Unity replacement contract

- Runtime species IDs are `moth_accountant`, `gel_anomaly`, `unregistered_alien`, and `void_proxy`.
- `ClientVisualCatalog.asset` maps those IDs to BSM portrait frames. Legacy IDs remain as aliases for old saves.
- The Moth Accountant currently demonstrates the complete nine-state pipeline. The other species hold their Pending frame until their state sheets arrive.
- Keep production sprites at their native dimensions and filenames when replacing art. Rerun **Tools > Desk 42 > Visual Identity > Wire Runtime Assets** after adding or replacing files.
- Runtime imports are Sprite / Single, 64 pixels per unit, Point filtered, mipmaps off, clamp wrapped, and uncompressed.
- The existing 256x256 `ClientPortrait` UI slot proves catalog/state wiring, but it is hidden in the current desk-stage composition because a square corporate portrait reads as a picture placed on the desk.
- Production delivery needs a clean client-facing room plate plus transparent seated claimant layers in a shared upper-centre footprint. Re-enable/adapt the catalog-driven slot only after those cutouts replace the baked Moth Accountant placeholder.
- State changes come from `ClientStateMachine`; do not key portrait swaps by hand in the scene.
- The twelve equipment prefabs under `Prefabs/VisualIdentity/OfficeEquipment` are UI prefabs for the desk canvas. Place them only after the desk composition is locked; do not populate the room as generic decoration.
