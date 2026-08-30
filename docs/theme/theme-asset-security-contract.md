# Theme Asset Security Contract

Theme assets are an optional site-level customization surface. The current site appearance is the Default Theme and remains the exact fallback whenever a custom asset is absent.

- Root is the only authority that may select or enable a site-wide Theme.
- ProblemSetter and Answerer roles cannot change the active site-wide Theme.
- Backgrounds and panel background/header/border textures use the shared `ISecureUploadValidator` foundation. Future decoration, icon, and mascot slots must use the same validation boundary when introduced.
- V1 image assets are limited to PNG, JPEG, and WebP with matching extension, declared MIME type, and file signature.
- SVG, custom CSS, custom JavaScript, and custom HTML are not accepted Theme asset formats.
- Custom assets are optional. Missing or rejected assets must not alter the existing background, panels, cards, navigation, icons, or appearance tokens.

THEME-10E exposes Root-only upload and delete endpoints at `/api/site-settings/theme-assets`. Responses contain a generated asset identifier, same-origin URL, canonical content type, and size; they never expose a physical path. Delete accepts only a generated file identifier inside `ThemeAssetsRoot` and refuses assets referenced by the active appearance.

Theme files live outside release directories. Production must set `Storage__ThemeAssetsRoot=/var/lib/onlinejudge/theme-assets`, provision it for API read/write access, retain it across release switches and backups, and expose it only through the same-origin `/theme-assets` static route. Do not place this directory under a versioned release or frontend build output.
