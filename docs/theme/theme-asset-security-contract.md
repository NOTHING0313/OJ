# Theme Asset Security Contract

Theme assets are an optional site-level customization surface. The current site appearance is the Default Theme and remains the exact fallback whenever a custom asset is absent.

- Root is the only authority that may select or enable a site-wide Theme.
- ProblemSetter and Answerer roles cannot change the active site-wide Theme.
- Backgrounds, panel background/header/border textures, icon slots, and decoration slots use the shared `ISecureUploadValidator` foundation. The project has no mascot asset requirement.
- V1 image assets are limited to PNG, JPEG, and WebP with matching extension, declared MIME type, and file signature.
- SVG, custom CSS, custom JavaScript, and custom HTML are not accepted Theme asset formats.
- Custom assets are optional. Missing or rejected assets must not alter the existing background, panels, cards, navigation, icons, or appearance tokens.

The Root-only `/api/site-settings/theme-assets` endpoints upload, list, and delete managed assets. Responses contain a generated asset identifier, same-origin URL, canonical content type, size, and logical usage slots; they never expose a physical path. One asset may be reused by multiple slots and presets. Delete accepts only a generated file identifier inside `ThemeAssetsRoot` and refuses assets referenced by the active Appearance or any stored Theme Preset.

An optional Root-managed display name is persisted as editor metadata only. Client paths are reduced to their basename, Unicode is normalized, control characters are removed, and the result is bounded to 128 characters. Duplicate display names are allowed because identity remains the generated AssetId. Rename never moves a file or changes its URL/reference; deleting a physical asset also removes its display metadata. Traversal, absolute paths, remote URLs, SVG, custom CSS/JS/HTML, and non-generated physical names remain rejected by the existing validators.

Theme files live outside release directories. Production must set `Storage__ThemeAssetsRoot=/var/lib/onlinejudge/theme-assets`, provision it for API read/write access, retain it across release switches and backups, and expose it only through the same-origin `/theme-assets` static route. Do not place this directory under a versioned release or frontend build output.
