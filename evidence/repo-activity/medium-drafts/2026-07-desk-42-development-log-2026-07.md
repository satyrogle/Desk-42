---
title: Desk 42 development log: 2026-07
approved: false
publish: false
publish_status: public
published_url: 
work_covered_start: 2026-07-03
work_covered_end: 2026-07-28
tags: [Game Development, Unity, Indie Games]
---

# Desk 42 development log: 2026-07

Publication note: publish this with the actual Medium publication date. The dates below describe the work covered by the git history.

## Work Covered

- Period: 2026-07-03 to 2026-07-28
- Source: local git history for Desk 42
- Recorded commits: 101
- Unique changed files: 795
- Lines added/deleted: 92943/5704

## Summary

This development period moved Desk 42 forward through concrete, verifiable repository activity. The work concentrated around Assets, Tests, Scripts/UI, Prefabs, Scripts/Core, with changes recorded in commit history rather than reconstructed from memory. That matters because the project is not just a design idea; it has an auditable trail of implementation, fixes, systems work, and production scaffolding.

For this period, git records 101 commits touching 795 unique files, with 92943 added lines and 5704 deleted lines. Those numbers are not a perfect measure of creative value, but they are useful evidence of sustained engineering and design activity.

## Main Work Areas

- Assets: 478 file-change rows
- Tests: 220 file-change rows
- Scripts/UI: 106 file-change rows
- Prefabs: 58 file-change rows
- Scripts/Core: 54 file-change rows
- ArtLab: 49 file-change rows
- Scripts/Editor: 47 file-change rows
- Scripts/Narrative: 46 file-change rows

## What Changed

The commit trail shows a mix of system construction, integration work, polish, and repair. The practical pattern is familiar in game development: features arrive, tests or compiler feedback reveal pressure points, and the project becomes sturdier through each pass.

The most useful way to read this month is not as a single feature drop, but as a sequence of technical decisions. Each commit left a timestamped record of what changed and why, from project structure and Unity configuration through gameplay systems, UI layers, editor tooling, and bug fixes.

## Evidence Trail

These are the recorded commits for the period. They are included so the article can be checked against the repository rather than treated as a loose retrospective.

- 2026-07-03 `8389a5b`: WIP: epitaxy pre-switch from main
- 2026-07-04 `be610ab`: docs(comfy): document the reference->ControlNet method + validated tier ladder
- 2026-07-04 `6e354e6`: Merge chore/claude-mcp-fmod-comfy-skill-pack into feat/cli-tooling
- 2026-07-17 `32420f0`: WIP: epitaxy pre-switch from main
- 2026-07-17 `e356e53`: Add Unity .meta files for new scripts and tests
- 2026-07-18 `b0eafa9`: feat: OfficeEnvironmentState + HazardInteractionTable — resource contention, consensus
- 2026-07-18 `acdd49b`: feat: split EncounterManager.ResolveEncounter — separation of concerns, event-driven coordination
- 2026-07-18 `2ab774e`: feat: PreFiledExemption feedback differentiation — fault detection, observer pattern
- 2026-07-18 `3d5215e`: feat: ClientView tell/dark humour wiring — pub/sub, interface segregation
- 2026-07-18 `b90d813`: feat: consensus audit instrumentation — Byzantine fault detection
- 2026-07-18 `1229bee`: Remove BOM from development log frontmatter
- 2026-07-18 `848b8ce`: feat(ui): add cocktail feedback visuals
- 2026-07-18 `adec34a`: feat: integration checklist — Codex interface alignment, color sync, audit channel
- 2026-07-18 `a479efc`: Merge remote-tracking branch 'origin/feat/cocktail-greenfield' into feat/cli-tooling
- 2026-07-18 `6df287a`: Refactor affected feature implementation
- 2026-07-19 `0df493f`: feat(gate3): scaffold Gate 3 evidence branch — Desk 42 Market and Execution Validation
- 2026-07-20 `52a1647`: Simplify application structure and remove obsolete code
- 2026-07-20 `a9e5f1b`: Update Desk 42 CLI build state
- 2026-07-20 `df2e22e`: Add Codex hook configuration
- 2026-07-20 `c18a9d1`: Recover visual identity and Unity presentation
- 2026-07-20 `f5c9a7b`: Add Unity MCP package and platform define symbols
- 2026-07-21 `c9e5d28`: Merge codex/MCP into feat/cocktail-greenfield (consolidation)
- 2026-07-21 `ad675a4`: Merge feat/cli-tooling into feat/cocktail-greenfield (consolidation)
- 2026-07-21 `a758359`: Merge feat/ff-resolution (cascade) into feat/cocktail-greenfield (consolidation)
- 2026-07-21 `989af6d`: docs(build-state): ground-truth reconciliation vs a758359 — compile clean, Shift.unity wiring verified, seeds, tests, bridge caveat
- 2026-07-21 `212a921`: playtest(001): seeded PlayMode harness + PLAYTEST_REPORT_001
- 2026-07-21 `271d11f`: fix: address Desk 42 playtest reliability findings
- 2026-07-21 `8349bf8`: merge: integrate recovered visual identity with playtest fixes
- 2026-07-22 `bb58018`: refactor: lock factual claim dispositions
- 2026-07-22 `7086c76`: feat: report applied action results
- 2026-07-22 `3310e64`: feat: persist and surface shift obligations
- 2026-07-22 `8530788`: feat: render confirmation layer in shift
- 2026-07-22 `65ede29`: feat: preview card and claim consequences
- 2026-07-22 `f0963db`: feat: show expected outcomes on card faces
- 2026-07-22 `3caf1e5`: feat: surface office and client modifiers
- 2026-07-22 `6dedc53`: feat: show authoritative client effect timer
- 2026-07-22 `685674c`: refactor: unify card slam input paths
- 2026-07-22 `6bab2d5`: feat: animate factual card impact feedback
- 2026-07-22 `ce0f35e`: feat: polish card and modifier feedback
- 2026-07-23 `bf1f0dd`: docs: lock five-shift proof plumbing
- 2026-07-23 `1324c50`: feat: prioritize card consequences at rest
- 2026-07-23 `4c0c29c`: feat: own Elias proof state across shifts
- 2026-07-23 `cbb2884`: feat: record Elias appearances exactly once
- 2026-07-23 `3701f11`: feat: apply Elias procedures atomically
- 2026-07-23 `6aba0a3`: feat: schedule authored Elias proof encounters
- 2026-07-23 `1c3d247`: feat: present ordered Elias procedure receipts
- 2026-07-23 `1db5587`: feat: apply authored Elias aftermath claims
- 2026-07-23 `e94a08b`: test: prove all Elias five-shift routes
- 2026-07-23 `da3f76b`: test: lock Elias proof validation handoff
- 2026-07-26 `bb14543`: fix: reset office environment per run, name fire-drill noise
- 2026-07-26 `a432a8b`: docs: reconcile build-state to main, add full test runner
- 2026-07-26 `4a4f919`: docs(proof): freeze Bucket 0B investigation baseline
- 2026-07-26 `8de9989`: feat(proof): build the encounter persistence spine
- 2026-07-26 `fda4f96`: artlab: checkpoint fast asset pipeline infrastructure
- 2026-07-26 `478d940`: artlab: validate semantic QA and ControlNet prop path
- 2026-07-26 `ecd0f71`: test: add adversarial bucket 1 persistence validation
- 2026-07-26 `c641e0d`: feat(proof): persist the Elias proof spine, wire its lifecycle, fix Shift buttons
- 2026-07-26 `f871c30`: artlab: prove Blender-guided structured prop pipeline
- 2026-07-26 `9e49d2b`: fix(proof): namespace EncounterIds per run instance, resolve claim before save
- 2026-07-26 `15a3252`: docs(proof): Bucket 3 pre-flight audits and spine gap analysis
- 2026-07-26 `718a08e`: feat(proof): Shift 5 branch mechanics, no-clean-out state, control claimant
- 2026-07-26 `55be8bb`: test: independently validate bucket 1 repair
- 2026-07-26 `b1d4e52`: feat(proof): schedule Mara Kest into Shift 3; lock aftermath and streak audits
- 2026-07-27 `b5039ee`: fix(proof): move bonus/memo authority into the commit transaction
- 2026-07-27 `9415c00`: feat(proof): passive verification telemetry for the manual five-shift run
- 2026-07-27 `4061a04`: test: retarget bucket 1 consequence validation
- 2026-07-27 `92369f0`: test: retarget BonusIndex tests at the current bonus authority
- 2026-07-27 `bacf2cb`: fix(proof): resolve sequential synergy by authored TagCategory
- 2026-07-27 `2b87c5a`: feat(audio): D1 package-independent audio boundary, event contract, audit
- 2026-07-27 `3d6e13f`: test: adversarially validate category synergy resolution
- 2026-07-27 `8e38f9e`: fix(proof): require authoritative category resolution for sequential synergy
- 2026-07-27 `c039572`: chore(audio): D1 pre-import safety closeout
- 2026-07-27 `67762b2`: test: independently validate bucket 1 final repair
- 2026-07-27 `1d77980`: refactor(audio): route PneumaticTube through AudioService; zero direct FMOD
- 2026-07-27 `f883831`: test: independently validate bucket 2 persistence
- 2026-07-27 `c98c377`: refactor(audio): rename ProofAudioEvent to AudioEventId
- 2026-07-27 `68d3067`: docs: audit remaining Bucket C persistence delta
- 2026-07-27 `64d5bac`: fix(campaign): decouple Mara scheduling from Elias proof; add history query
- 2026-07-27 `3b072b5`: test: independently validate Bucket C delta 1
- 2026-07-27 `8122113`: test: independently validate Bucket C delta 1
- 2026-07-27 `11ec181`: refactor(audio): split AudioEventCatalog from ProofAudioPolicy
- 2026-07-27 `248e2a9`: feat(campaign): persist approval liability keyed by source encounter
- 2026-07-27 `a4c0ad3`: test: independently validate Bucket C delta 2
- 2026-07-27 `0fef3b2`: test: independently validate Bucket C delta 2
- 2026-07-27 `c45dd5d`: test: independently validate Bucket C delta 2
- 2026-07-27 `4c4ef26`: feat(audio): activate FMOD 2.03.14 integration and implement FmodAudioBackend
- 2026-07-27 `9441561`: fix(campaign): canonicalise approval-liability reads by source encounter
- 2026-07-27 `66c2a5b`: revert(audio): drop the FmodAudioBackend guard from the CΔ2 delta
- 2026-07-27 `d34bbb4`: fix(repo): keep FMOD vendor binaries out of the public repository
- 2026-07-27 `43a9560`: fix(campaign): canonicalise approval-liability reads by source encounter
- 2026-07-27 `9277314`: build(fmod): scripted local activation for the external-import model
- 2026-07-27 `6d7bb40`: feat(campaign): explicit interrupted / carried-forward encounter lifecycle
- 2026-07-27 `91f5b19`: test: independently validate Bucket C delta 3A
- 2026-07-27 `f880307`: test: independently validate Bucket C delta 3A
- 2026-07-28 `ffda124`: docs: Bucket C delta 3B decision audit — CD3B NOT REQUIRED
- 2026-07-28 `20ba10a`: feat(audio): D1 FMOD pipeline tooling, diagnosability fixes, provenance
- 2026-07-28 `1ab478f`: test: validate aggregate Bucket C contracts
- 2026-07-28 `8fd5e51`: feat(audio): D1 technical FMOD pipeline proven end to end
- 2026-07-28 `80ab281`: docs(d1): record literal clean no-SDK State A result
- 2026-07-28 `eb7ab74`: merge: Bucket 4 candidate — closed Bucket C + completed D1
- 2026-07-28 `e584ce6`: docs: Bucket 4 pre-cohort validation record

## Reflection

The strongest lesson from this slice of work is that a game project becomes real through repeated contact with constraints: Unity project structure, compile errors, scene wiring, user interface behavior, asset organization, and the small decisions that keep future work possible. The visible feature list is only one layer; the repository history also captures the less glamorous engineering that lets the project survive its own growth.

## Verification

The numbers in this post come from git log and git show --numstat for the Desk 42 repository. The work dates are development dates from git history, not claimed Medium publication dates.

<!-- Review gate: set approved: true and publish: true in the front matter only after you have reviewed this article. -->

<!-- Raw notes retained below for editing. -->

## Raw Development Notes

## Development Notes

- 2026-07-03 `8389a5b`: WIP: epitaxy pre-switch from main
- 2026-07-04 `be610ab`: docs(comfy): document the reference->ControlNet method + validated tier ladder
- 2026-07-04 `6e354e6`: Merge chore/claude-mcp-fmod-comfy-skill-pack into feat/cli-tooling
- 2026-07-17 `32420f0`: WIP: epitaxy pre-switch from main
- 2026-07-17 `e356e53`: Add Unity .meta files for new scripts and tests
- 2026-07-18 `b0eafa9`: feat: OfficeEnvironmentState + HazardInteractionTable — resource contention, consensus
- 2026-07-18 `acdd49b`: feat: split EncounterManager.ResolveEncounter — separation of concerns, event-driven coordination
- 2026-07-18 `2ab774e`: feat: PreFiledExemption feedback differentiation — fault detection, observer pattern
- 2026-07-18 `3d5215e`: feat: ClientView tell/dark humour wiring — pub/sub, interface segregation
- 2026-07-18 `b90d813`: feat: consensus audit instrumentation — Byzantine fault detection
- 2026-07-18 `1229bee`: Remove BOM from development log frontmatter
- 2026-07-18 `848b8ce`: feat(ui): add cocktail feedback visuals
- 2026-07-18 `adec34a`: feat: integration checklist — Codex interface alignment, color sync, audit channel
- 2026-07-18 `a479efc`: Merge remote-tracking branch 'origin/feat/cocktail-greenfield' into feat/cli-tooling
- 2026-07-18 `6df287a`: Refactor affected feature implementation
- 2026-07-19 `0df493f`: feat(gate3): scaffold Gate 3 evidence branch — Desk 42 Market and Execution Validation
- 2026-07-20 `52a1647`: Simplify application structure and remove obsolete code
- 2026-07-20 `a9e5f1b`: Update Desk 42 CLI build state
- 2026-07-20 `df2e22e`: Add Codex hook configuration
- 2026-07-20 `c18a9d1`: Recover visual identity and Unity presentation
- 2026-07-20 `f5c9a7b`: Add Unity MCP package and platform define symbols
- 2026-07-21 `c9e5d28`: Merge codex/MCP into feat/cocktail-greenfield (consolidation)
- 2026-07-21 `ad675a4`: Merge feat/cli-tooling into feat/cocktail-greenfield (consolidation)
- 2026-07-21 `a758359`: Merge feat/ff-resolution (cascade) into feat/cocktail-greenfield (consolidation)
- 2026-07-21 `989af6d`: docs(build-state): ground-truth reconciliation vs a758359 — compile clean, Shift.unity wiring verified, seeds, tests, bridge caveat
- 2026-07-21 `212a921`: playtest(001): seeded PlayMode harness + PLAYTEST_REPORT_001
- 2026-07-21 `271d11f`: fix: address Desk 42 playtest reliability findings
- 2026-07-21 `8349bf8`: merge: integrate recovered visual identity with playtest fixes
- 2026-07-22 `bb58018`: refactor: lock factual claim dispositions
- 2026-07-22 `7086c76`: feat: report applied action results
- 2026-07-22 `3310e64`: feat: persist and surface shift obligations
- 2026-07-22 `8530788`: feat: render confirmation layer in shift
- 2026-07-22 `65ede29`: feat: preview card and claim consequences
- 2026-07-22 `f0963db`: feat: show expected outcomes on card faces
- 2026-07-22 `3caf1e5`: feat: surface office and client modifiers
- 2026-07-22 `6dedc53`: feat: show authoritative client effect timer
- 2026-07-22 `685674c`: refactor: unify card slam input paths
- 2026-07-22 `6bab2d5`: feat: animate factual card impact feedback
- 2026-07-22 `ce0f35e`: feat: polish card and modifier feedback
- 2026-07-23 `bf1f0dd`: docs: lock five-shift proof plumbing
- 2026-07-23 `1324c50`: feat: prioritize card consequences at rest
- 2026-07-23 `4c0c29c`: feat: own Elias proof state across shifts
- 2026-07-23 `cbb2884`: feat: record Elias appearances exactly once
- 2026-07-23 `3701f11`: feat: apply Elias procedures atomically
- 2026-07-23 `6aba0a3`: feat: schedule authored Elias proof encounters
- 2026-07-23 `1c3d247`: feat: present ordered Elias procedure receipts
- 2026-07-23 `1db5587`: feat: apply authored Elias aftermath claims
- 2026-07-23 `e94a08b`: test: prove all Elias five-shift routes
- 2026-07-23 `da3f76b`: test: lock Elias proof validation handoff
- 2026-07-26 `bb14543`: fix: reset office environment per run, name fire-drill noise
- 2026-07-26 `a432a8b`: docs: reconcile build-state to main, add full test runner
- 2026-07-26 `4a4f919`: docs(proof): freeze Bucket 0B investigation baseline
- 2026-07-26 `8de9989`: feat(proof): build the encounter persistence spine
- 2026-07-26 `fda4f96`: artlab: checkpoint fast asset pipeline infrastructure
- 2026-07-26 `478d940`: artlab: validate semantic QA and ControlNet prop path
- 2026-07-26 `ecd0f71`: test: add adversarial bucket 1 persistence validation
- 2026-07-26 `c641e0d`: feat(proof): persist the Elias proof spine, wire its lifecycle, fix Shift buttons
- 2026-07-26 `f871c30`: artlab: prove Blender-guided structured prop pipeline
- 2026-07-26 `9e49d2b`: fix(proof): namespace EncounterIds per run instance, resolve claim before save
- 2026-07-26 `15a3252`: docs(proof): Bucket 3 pre-flight audits and spine gap analysis
- 2026-07-26 `718a08e`: feat(proof): Shift 5 branch mechanics, no-clean-out state, control claimant
- 2026-07-26 `55be8bb`: test: independently validate bucket 1 repair
- 2026-07-26 `b1d4e52`: feat(proof): schedule Mara Kest into Shift 3; lock aftermath and streak audits
- 2026-07-27 `b5039ee`: fix(proof): move bonus/memo authority into the commit transaction
- 2026-07-27 `9415c00`: feat(proof): passive verification telemetry for the manual five-shift run
- 2026-07-27 `4061a04`: test: retarget bucket 1 consequence validation
- 2026-07-27 `92369f0`: test: retarget BonusIndex tests at the current bonus authority
- 2026-07-27 `bacf2cb`: fix(proof): resolve sequential synergy by authored TagCategory
- 2026-07-27 `2b87c5a`: feat(audio): D1 package-independent audio boundary, event contract, audit
- 2026-07-27 `3d6e13f`: test: adversarially validate category synergy resolution
- 2026-07-27 `8e38f9e`: fix(proof): require authoritative category resolution for sequential synergy
- 2026-07-27 `c039572`: chore(audio): D1 pre-import safety closeout
- 2026-07-27 `67762b2`: test: independently validate bucket 1 final repair
- 2026-07-27 `1d77980`: refactor(audio): route PneumaticTube through AudioService; zero direct FMOD
- 2026-07-27 `f883831`: test: independently validate bucket 2 persistence
- 2026-07-27 `c98c377`: refactor(audio): rename ProofAudioEvent to AudioEventId
- 2026-07-27 `68d3067`: docs: audit remaining Bucket C persistence delta
- 2026-07-27 `64d5bac`: fix(campaign): decouple Mara scheduling from Elias proof; add history query
- 2026-07-27 `3b072b5`: test: independently validate Bucket C delta 1
- 2026-07-27 `8122113`: test: independently validate Bucket C delta 1
- 2026-07-27 `11ec181`: refactor(audio): split AudioEventCatalog from ProofAudioPolicy
- 2026-07-27 `248e2a9`: feat(campaign): persist approval liability keyed by source encounter
- 2026-07-27 `a4c0ad3`: test: independently validate Bucket C delta 2
- 2026-07-27 `0fef3b2`: test: independently validate Bucket C delta 2
- 2026-07-27 `c45dd5d`: test: independently validate Bucket C delta 2
- 2026-07-27 `4c4ef26`: feat(audio): activate FMOD 2.03.14 integration and implement FmodAudioBackend
- 2026-07-27 `9441561`: fix(campaign): canonicalise approval-liability reads by source encounter
- 2026-07-27 `66c2a5b`: revert(audio): drop the FmodAudioBackend guard from the CΔ2 delta
- 2026-07-27 `d34bbb4`: fix(repo): keep FMOD vendor binaries out of the public repository
- 2026-07-27 `43a9560`: fix(campaign): canonicalise approval-liability reads by source encounter
- 2026-07-27 `9277314`: build(fmod): scripted local activation for the external-import model
- 2026-07-27 `6d7bb40`: feat(campaign): explicit interrupted / carried-forward encounter lifecycle
- 2026-07-27 `91f5b19`: test: independently validate Bucket C delta 3A
- 2026-07-27 `f880307`: test: independently validate Bucket C delta 3A
- 2026-07-28 `ffda124`: docs: Bucket C delta 3B decision audit — CD3B NOT REQUIRED
- 2026-07-28 `20ba10a`: feat(audio): D1 FMOD pipeline tooling, diagnosability fixes, provenance
- 2026-07-28 `1ab478f`: test: validate aggregate Bucket C contracts
- 2026-07-28 `8fd5e51`: feat(audio): D1 technical FMOD pipeline proven end to end
- 2026-07-28 `80ab281`: docs(d1): record literal clean no-SDK State A result
- 2026-07-28 `eb7ab74`: merge: Bucket 4 candidate — closed Bucket C + completed D1
- 2026-07-28 `e584ce6`: docs: Bucket 4 pre-cohort validation record

## Article Angle

Turn the commit list above into a narrative about the design and engineering decisions made during this period. Keep the dates as work-period evidence, not as claimed publication dates.
