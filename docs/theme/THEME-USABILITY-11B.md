# THEME-USABILITY-11B Artist Workflow Validation

## Purpose

Validate the existing Root Visual Theme Editor as an artist-facing workflow. This stage does not add theme capabilities or change the active Appearance, Theme Pack, asset, permission, or runtime contracts.

## Artist persona

The simulated artist can use common design tools, select images and colors, and understand visual terms. They do not write code and do not know React, CSS, JSON, database structure, internal slot identifiers, or asset IDs.

## Synthetic smoke assets

Use locally generated geometric PNG/WebP fixtures only. The smoke set covers a page background, panel texture, panel header, panel border, five reusable navigation icons, page-header decoration, card-header decoration, panel-corner decoration, and empty-state illustration. Runtime uploads, exported packs, screenshots, and generated smoke files must remain outside the repository and be removed after validation.

## Test environment and safety

- Local development environment only; no production SSH, deploy, or RC work.
- Begin from the virtual Default Theme.
- Use the existing Root editor and its existing API contracts.
- Preview and Save Preset must not apply the site Appearance.
- Apply requires explicit confirmation. Applying Default must restore the exact default Appearance without deleting assets.
- Synthetic browser fixtures may isolate UI workflow validation, but runtime and persistence claims require existing service tests or a real local API result.

## Measured workflow

Record elapsed time, discrete user actions, errors, ambiguous selections, page switches, and terminology friction for:

1. Open the editor, load Default, and save `Artist Workflow Test`.
2. Configure global colors: page/panel/border/text/accent/navigation.
3. Upload or select a background; configure cover/focal point/overlay/blur/brightness; check Desktop, Tablet, and Mobile.
4. Configure panel background/header/border textures, opacity, radius, and shadow.
5. Reuse uploaded assets across Problem, Challenge, Leaderboard, Team, and Help icon slots.
6. Configure Page Header, Card Header, Panel Corner, and Empty State decorations.
7. inspect Login, Problem, Challenge, Team, Leaderboard, Season, Help, Account, and Security Audit previews.
8. Select Background, Primary Panel, Problem Icon, and Page Header Decoration from the canvas and from Navigator search.
9. Make at least ten draft mutations, Undo five, and Redo three; confirm the Preview, Draft, history, and asset references agree.
10. Compare Saved versus Draft and Default versus Draft without mutating Draft.
11. Save the preset, then separately Apply it to the site.
12. Duplicate it as `Artist Workflow Variant`, change Accent and Background, and verify the source is unchanged.
13. Preview Default, Theme A, and Theme B without applying, then explicitly Apply Theme A.
14. Export and import Theme A; review name, asset count, format version, result, and `NOT ACTIVE` status.
15. Exercise dirty protection for loading, applying, leaving, and resetting.
16. Exercise understandable failures for upload, missing asset, 429, save, and invalid import.
17. Validate keyboard access, labels, alternative controls, and the 375 px layout.
18. Apply Default and confirm exact visual recovery while retaining assets.

## Fidelity and regression matrix

Compare the editor preview with representative runtime rendering for Problem, Leaderboard, and Help. Confirm color, background, panel, icon, and decoration primitives agree. With Default active, inspect Login, Problem, Challenge, Team, Leaderboard, Season, Help, Account, and Security Audit for unintended changes.

## Acceptance and triage

- `Blocker`: prevents completing, saving, exporting, importing, or restoring a theme.
- `High`: creates likely data loss, accidental site Apply, inaccessible required action, or materially misleading workflow.
- `Medium`: repeated or ambiguous work with a clear low-risk editor-only fix.
- `Low`: polish or minor wording that does not block the workflow.

Blocker and High findings must be fixed in this stage. Medium findings may be fixed only when the change is local and low risk. Low findings remain recorded. Capability gaps are marked `FOLLOW_UP` and do not expand this stage.

## Recorded workflow baseline

This is a simulated artist baseline, not a performance benchmark. The fully automated fixture completed the final workflow and visual gates in 44.9 seconds; the operator-paced estimate below accounts for visually reviewing choices rather than executing scripted clicks instantly.

| Stage | Estimated artist time |
| --- | ---: |
| Colors | 1.2 min |
| Background | 2.5 min |
| Panels | 1.5 min |
| Icons | 2.0 min |
| Decorations | 1.3 min |
| Review | 2.5 min |
| Save and Apply | 1.0 min |
| **Total** | **12.0 min** |

Measured primary-path action counts were: Accent 2, Background 4, Panel Texture 2, Problem Icon 2, Page Header Decoration 2, Save Theme 2, and Apply Theme 2. The workflow also completed five Undo actions, three Redo actions, preset duplication, export, validated import, searchable asset reuse, explicit Apply, and explicit Default recovery.

## Validation outcome

- Problem, Leaderboard, and Help preview/runtime comparisons produced identical accent and panel theme primitives.
- Desktop, Tablet, and Mobile preview controls remained available. At 375 px the editor root had no horizontal overflow and Theme Library, Inspector, Save, and Discard remained reachable.
- Applying the built-in Default Theme restored the exact normalized default Appearance, retained all synthetic assets, and generated no Theme Asset requests across Login, Problem, Challenge, Team, Leaderboard, Season, Help, Account, and Security Audit.
- Keyboard smoke reached links, buttons, inputs, selects, and disclosure summaries. Save As autofocus and dialog controls worked; focus trapping remains a recorded follow-up.
- No visible `AssetId`, `SiteAppearance`, `ThemeImage`, `PositionX`, `PositionY`, `SchemaVersion`, or `DecorationSlot` terminology remained in the workflow.
