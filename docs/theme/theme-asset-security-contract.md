# Theme Asset Security Contract

Theme assets are an optional site-level customization surface. The current site appearance is the Default Theme and remains the exact fallback whenever a custom asset is absent.

- Root is the only authority that may select or enable a site-wide Theme.
- ProblemSetter and Answerer roles cannot change the active site-wide Theme.
- Backgrounds, panel background/header/border textures, icon slots, and decoration slots use the shared `ISecureUploadValidator` foundation. The project has no mascot asset requirement.
- V1 image assets are limited to PNG, JPEG, and WebP with matching extension, declared MIME type, and file signature.
- SVG, custom CSS, custom JavaScript, and custom HTML are not accepted Theme asset formats.
- Custom assets are optional. Missing or rejected assets must not alter the existing background, panels, cards, navigation, icons, or appearance tokens.

The Root-only `/api/site-settings/theme-assets` endpoints upload, list, and delete managed assets. Responses contain a generated asset identifier, same-origin URL, canonical content type, size, and logical usage slots; they never expose a physical path. One asset may be reused by multiple slots. Delete accepts only a generated file identifier inside `ThemeAssetsRoot` and refuses assets referenced by Background, Panel Skin, Icon, or Decoration configuration.

Theme files live outside release directories. Production must set `Storage__ThemeAssetsRoot=/var/lib/onlinejudge/theme-assets`, provision it for API read/write access, retain it across release switches and backups, and expose it only through the same-origin `/theme-assets` static route. Do not place this directory under a versioned release or frontend build output.
