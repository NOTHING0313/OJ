# THEME-USABILITY-11B Friction Log

The findings below were captured while completing the synthetic artist workflow in `THEME-USABILITY-11B.md`. “Fixed” means the change is local to the existing editor workflow and does not add a Theme capability or change a backend contract.

| ID | Severity | Workflow | Problem | Evidence | Fix | Status |
| --- | --- | --- | --- | --- | --- | --- |
| H-01 | High | Load / save preset | Dirty state compared every draft with the active site Appearance. Loading an already-saved, inactive preset therefore immediately looked unsaved and made artists repeat save/discard decisions. | Load a preset that differs from the active site, then attempt another library action. | Track an editor draft checkpoint independently from the active site snapshot; update it after load, preset save/update, Apply, and Save & Apply. | FIXED |
| H-02 | High | Leave / reset | Library transitions offered Save, Discard, and Cancel, but route navigation and whole-theme reset used less safe binary browser prompts. | Make a draft edit, then follow an internal link or select whole-theme reset. | Route load, apply, internal navigation, and reset through one explicit Save / Discard / Cancel guard. `beforeunload` remains browser-native by platform design. | FIXED |
| M-01 | Medium | Entire editor | Artist-facing copy mixed Chinese with implementation terms such as Draft, Preset, Appearance, AssetId, PositionX, and SchemaVersion. | Read the toolbar, library actions, inspector fields, upload notices, and dialogs without implementation knowledge. | Replace visible implementation vocabulary with stable visual-design terms while preserving stored values and API fields. | FIXED |
| M-02 | Medium | Inspector | Background, panel, color, icon, and decoration controls were long flat lists, making related choices difficult to scan. | Select Background or Primary Panel and locate material, composition, and effects controls. | Add native collapsible inspector groups with artist-oriented labels. | FIXED |
| M-03 | Medium | Asset reuse | The picker exposed opaque full IDs and offered no search, type filter, or unused-assets filter. | Reuse one uploaded asset after the library contains several images. | Show a short friendly asset reference, retain the full ID as a title, add search/type/unused filters, and translate Used By locations. | PARTIALLY_FIXED |
| M-04 | Medium | Theme library | The “More” disclosure could remain open after an action and intercept later clicks during a compact workflow. | Duplicate or export a theme and continue editing without manually closing the disclosure. | Close the disclosure after an action is selected. | FIXED |
| M-05 | Medium | Mobile | The editor toggle checkbox retained ordinary form-control dimensions and could widen the 375 px layout. | Open the editor at 375 px and inspect document width. | Constrain the scoped toggle input and stack library/asset controls at the existing mobile breakpoint. | FIXED |
| M-06 | Medium | Import review | The existing import endpoint validates and imports in one operation, so the UI cannot show a trusted manifest review before committing the import. | Select a theme pack and inspect the confirmation step. | The post-import result now reports name, format version, asset count, and inactive status. A true pre-import manifest review requires an additive API contract. | FOLLOW_UP |
| M-07 | Medium | Keyboard dialogs | Dialogs expose labels, autofocus, native controls, and `aria-modal`, but Tab can leave the dialog because focus is not trapped. | Open Save As at 375 px and tab past the enabled dialog controls. | Record for a shared accessible-dialog pass; adding focus ownership across every editor dialog is larger than a copy/grouping correction. | FOLLOW_UP |
| L-01 | Low | Rename / delete | Rename and destructive library confirmations still use native browser dialogs, which are visually inconsistent with the editor dialogs. | Rename or delete a user theme. | Keep the mature behavior for this stage; replacing it is polish and would enlarge the dialog-state change. | REMAINING |
| L-02 | Low | Asset identity | Original artist filenames are not persisted by the current asset response, so a human-friendly name cannot survive refresh. | Upload two visually similar files, refresh, then search by original filename. | Searchable short references reduce friction. Persisted display names require a backend/data contract and are outside 11B. | FOLLOW_UP |

## Closeout

- Blocker: 0
- High: 2 found, 2 fixed, 0 remaining
- Medium: 7 found, 4 fixed, 1 partially fixed, 2 follow-up
- Low: 2 remaining/follow-up
- Capability and persistence gaps were recorded rather than expanded into this usability stage.
