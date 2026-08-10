# Office Slice M4 Visual Bible

## Target

`WARM OFFICE / INK BREAK` is a readable 2.5D workplace diorama. Calm play uses cream paper, warm plaster, moss furniture and teal machines. Pressure introduces amber. Breaks replace warmth with hard ink, red interruptions, cyan temporal seams and violet impossible-space accents. The result should read in motion at 1280x720 before it rewards close inspection.

## Semantic palette

| Name | Hex | Use |
|---|---|---|
| Cream paper | `#E8D9B5` | folders, paper, player-facing panels |
| Warm plaster | `#C7BFA7` | office shell and neutral fixtures |
| Moss furniture | `#66705B` | desks, chairs, calm structure |
| Machine teal | `#2F6B67` | automation, active equipment, Warden accent |
| Coffee wood | `#6C4E3D` | floor trim, counter fronts, props |
| Calm mint | `#B8D6B0` | recovery and safe-action feedback |
| Warning amber | `#D8892B` | rush, warning, actionable pressure |
| Break red | `#B53B38` | fault, copy escalation, urgent interruption |
| Ink | `#15151A` | outlines, break-state shadow, type |
| Ghost cyan | `#49C6C8` | Ghost Clock and time slips |
| Impossible violet | `#7B4A88` | Missing Room and Promotion Cascade |

## Camera, scale, and pixel discipline

- Fixed orthographic camera, three-quarter view, complete office visible.
- Environment sprites are authored on a 32-pixel grid and imported point-filtered without mipmaps.
- Character anchors share a bottom-centre contact point. Portraits use a consistent eye line.
- Outlines are 2-4 source pixels and never carry hidden gameplay information.
- All authored labels remain Unity-rendered text; generated raster art contains no words.

## Department signatures

| Zone | Shape language | Colour cue | Landmark |
|---|---|---|---|
| Front Desk | Long counter, open fan | cream + coffee | brass bell and six-folder rail |
| Waiting Area | soft semicircle | moss + mint | paired calm chairs and plant |
| Paper Room | stacked rectangles | cream + moss | tall document shelves |
| Money Room | gridded geometry | teal + coffee | ledger vault and green lamp |
| Weird Room | broken diagonals | violet + ink | copier, sorter and impossible door |

## Character signatures

- Warden: compact teal coat, cream sleeves, square satchel.
- Runner: forward lean, mint scarf, long courier bag.
- Talker: broad violet cardigan, round speech-pin silhouette.
- Nia Bell: amber bob and bell-shaped coat hem.
- Owen Pike: tall moss profile and pointed shoulder line.
- Mara Vale: strong red-violet jacket and angular hair; Promotion allegiance adds an ink sash.
- Iris Cole: short cyan coat and asymmetric satchel.
- Tomas Reed: narrow coffee silhouette; Ghost state adds cyan clock echoes.
- June Hart: warm cream cardigan and rounded heart-like collar.

## State progression

| State | Lighting | Motion | Marks |
|---|---|---|---|
| Calm | warm cream | restrained two-frame breathing | clean soft contact shadows |
| Rush | amber edge | faster but tick-quantised | queue chevrons and paper wake |
| Break | ink/red key | stepped impact and machine shake | tears, copy bursts, interruption bars |
| Recovery | mint/cyan return | pulse slows with completed targets | checked recovery marks and residue |
| Result | warm neutral | settled | paper ledger frame and tomorrow card |

## Accessibility seams

Break language uses shape, pattern, scale, and motion in addition to hue. Reduced flash suppresses high-frequency alternation without changing state or timing. The development HUD is opt-in and absent from production captures.
