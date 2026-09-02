# Generic Theme Architecture

## Authority and storage

The existing `SiteSetting` row with key `appearance` remains the single configuration authority. `SiteAppearanceDto`, `SiteSettingsService`, the existing appearance controller, and the frontend `ThemeProvider` are extended in place; no second provider, Theme table, or migration is introduced. Only Root may upload assets or update the global appearance. ProblemSetter and Answerer access is rejected by backend authorization and service-level role checks.

The current site visual is the Default Theme. Both custom nodes are optional and disabled by default. When a node is disabled, its asset is missing, or the browser cannot load its same-origin asset, the renderer omits its custom layer/modifier and continues to use existing CSS tokens and component styles. It does not synthesize approximate defaults.

## Background

`background` supports a controlled asset reference plus optional position X/Y (0–100), size (`cover`, `contain`, `auto`), repeat (`no-repeat`, `repeat`, `repeat-x`, `repeat-y`), attachment (`scroll`, `fixed`), overlay `#RRGGBB` and opacity (0–1), blur (0–20 px), and brightness (50–150%). The image and overlay are fixed, non-interactive layers behind application content. Filters apply to the image layer only; mobile forces attachment to `scroll`.

No remote URL or arbitrary CSS value is accepted. The configuration stores only a server-generated asset identifier and `/theme-assets/{assetId}` URL.

## Panel skin

`panelSkin` supports optional background, existing-header, and border textures, background opacity, texture opacity, radius override, and shadow strength. Opt-in generic modifier classes cover primary Problem, Challenge, Team, Leaderboard/Season, Help, Account, Security Audit, modal/table, and login surfaces. A header texture applies only where a header region already exists; it does not add DOM. Disabled or missing values do not emit overrides.

## Icon and decoration slots

`icons` and `decorations` extend the same Appearance JSON. Icon keys are a centralized controlled registry for Problem, Challenge, Leaderboard, Team, Submission, Help, Profile, Chat, Git, Season, and Reward. A shared `ThemeIcon` renders a managed asset only when the assignment is enabled and the image loaded successfully; otherwise the existing component, text, SVG, or symbol remains unchanged. The fixed slot box and `object-fit: contain` prevent source dimensions from changing layout, and custom images are decorative (`aria-hidden`) so the existing link or button accessible name remains authoritative.

Decoration keys are the generic semantic surfaces PageHeader, CardHeader, PanelCorner, and EmptyState. Controlled opacity, scale, offset, alignment, and corner values become scoped ThemeProvider variables and opt-in modifiers. Decorations are non-interactive, never replace empty-state text, and are omitted entirely when disabled, unassigned, or missing on disk.

Root editing continues to use one local Appearance draft for Background, Panel, Icons, and Decorations. Upload and assignment are separate: one asset may be selected by multiple slots. The lightweight asset library lists thumbnail, type, size, and logical `UsedBy` references without exposing filesystem paths.

Artist-facing asset display names are optional metadata stored inside the existing bounded `theme-library` SiteSetting JSON. The name is normalized from a client basename, limited to 128 characters, and may be changed by Root. It never participates in an asset ID, URL, physical filename, filesystem lookup, or preset reference. Existing assets without metadata continue to use a short generated-ID label.

## Asset lifecycle and persistence

PNG, JPEG, and WebP files up to 5 MiB pass the shared `ThemeImage` secure upload policy, including filename, MIME, signature, and size checks. SVG is rejected. Files are written atomically under configured `Storage:ThemeAssetsRoot` and served at `/theme-assets`; absolute disk paths are never returned.

Replacement uploads the new asset before appearance save. V1 retains detached assets as cleanup candidates rather than risking deletion before a successful configuration update. Explicit deletion is Root-only, root-confined, generated-name-only, and blocked while the asset is referenced by Background, Panel, Icon, or Decoration configuration.

For production configure:

```text
Storage__ThemeAssetsRoot=/var/lib/onlinejudge/theme-assets
```

The directory is persistent runtime data owned for API read/write access. Back it up and preserve it across `current` release switches. It must not reside in a release directory, `frontend/dist`, or the database JSON.

## Audit, security, and failure behavior

Appearance updates reuse `SiteAppearance.Updated`. Metadata records only whether Background/Panel enablement changed and which logical asset slots changed; it excludes bytes, paths, URLs, and full configuration. Existing CSP remains sufficient (`img-src 'self' data: blob:`), and no directive is widened.

Upload or save failure leaves the active appearance unchanged. Missing/deleted asset loading fails closed to the Default Theme without blocking page rendering or retry loops.

## Future extension

The visual editor is a Root-only management workbench over this same contract; its preview, selection, comparison, and undo history are local session state. Reusable presets are stored separately under the `theme-library` SiteSetting key and never become a runtime provider or active-theme pointer. See `visual-theme-editor.md` and `theme-library-and-pack-v1.md`.

There is no mascot, Live2D, Spine, mouse-follow character, or mascot asset-slot requirement. Any later extension must reuse the same Root authority, secure asset reference, persistence, audit, and exact-default fallback contracts; custom CSS, JavaScript, and HTML remain outside the Theme contract.
