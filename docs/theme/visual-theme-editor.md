# Visual Theme Editor

## Purpose and authority

The Visual Theme Editor upgrades `/admin/site-settings` from a long configuration form to a Root-only workbench. It edits the existing `SiteAppearance` contract only: design tokens, page and global backgrounds, Panel Skin, icon slots, and decoration slots. It does not create a second Theme provider, API, table, JSON shape, or asset library.

ProblemSetter and Answerer have no navigation entry and remain rejected by the existing Root-protected route and backend Appearance/Theme Asset authorization. The workbench adds no production query or event listener to ordinary user pages because the route module is lazy loaded.

## Surface registry

`themeEditorModel.ts` owns the presentation-only `ThemeEditableSurfaces` registry. IDs are controlled semantic values such as `global.background`, `panel.primary`, `icon.problem`, and `decoration.pageHeader`. They are not CSS selectors, DOM paths, XPath expressions, or React component names, and they are never sent to or stored by the backend.

The Surface Navigator is the keyboard-accessible alternative to click selection. Search matches controlled labels and keywords. A selected surface determines the Property Inspector; it does not mutate the application component tree.

## Preview architecture

The Preview Canvas renders synthetic representative content in the same React tree. It never uses an iframe and never reads real users, teams, submissions, security logs, or unpublished help content. Nine compositions cover Login, Problem, Challenge, Team, Leaderboard, Season, Help, Account, and Security Audit.

Desktop, Tablet, and Mobile alter only the controlled canvas width. Preview samples reuse real semantic CSS variables, generic panel modifier classes, icon slot box styling, and decoration modifier classes. Select outlines, surface hit areas, and the background focal control exist only inside the editor canvas.

Preview comparison has three local sources:

- `Draft`: current editable Appearance.
- `Current Saved`: the last server-confirmed Appearance.
- `Default`: a transient `createDefaultSiteAppearance()` value.

Selecting Saved or Default never writes either value into the Draft.

## Inspector architecture

The Property Inspector renders only the controls supported by the selected surface. Colors provide both a color picker and validated `#RRGGBB` text. Numeric properties provide synchronized range and number inputs clamped to the existing backend bounds. Enum properties use selects. Background focal manipulation writes only controlled Position X/Y values from 0 to 100 and retains numeric alternatives.

Asset properties reuse the THEME-10F Theme Asset API and library. The picker displays thumbnail, detected browser resolution, content type, size, and accurate saved-plus-draft `Used By` references. Explicit drop zones upload only through the existing secure upload endpoints. SVG, remote URLs, custom CSS, JavaScript, and HTML are not accepted.

## Draft, history, and save semantics

One `SiteAppearance` value is the editor Draft. The session history reducer owns `saved`, `present`, `past`, and `future`; it is capped at 50 steps and stores asset references, never asset bytes. History is not written to the database, Appearance JSON, or local storage.

Slider, color, enum, asset, and reset changes update local Draft only. Undo and Redo operate only on that session history and do not call the API. Dirty state adds a browser-close warning and an editor-route navigation confirmation; both listeners are installed only while the lazy editor route is mounted and are removed on unmount.

`Save & Apply` is the only path that calls the existing Appearance PUT. A successful response becomes the new Saved baseline, clears history, reloads ThemeProvider, and produces the existing single `SiteAppearance.Updated` audit event. A failed or rate-limited save retains the full Draft. `Discard Changes` restores Current Saved without an API call.

`Reset Section` changes only the selected surface. `Reset Entire Theme` requires a confirmation dialog and places the exact default configuration into the Draft. Neither reset operation deletes assets; explicit deletion remains manual and protected by saved and draft references.

## Default Theme contract

The default Appearance still emits no custom background, Panel Skin, icon, or decoration layers. Closing the editor or leaving all custom nodes disabled produces the exact existing UI. Editor classes, selection outlines, history state, and preview listeners are not present on Login, Problem, Challenge, Team, Leaderboard, Season, Help, Account, or Security Audit pages.

## No mascot requirement

The roadmap has no mascot, 看板娘, mouse-follow character, Live2D, or Spine requirement. The editor defines no mascot model, UI, placeholder, documentation slot, or asset slot.
