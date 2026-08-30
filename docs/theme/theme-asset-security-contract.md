# Theme Asset Security Contract

Theme assets are an optional site-level customization surface. The current site appearance is the Default Theme and remains the exact fallback whenever a custom asset is absent.

- Root is the only authority that may select or enable a site-wide Theme.
- ProblemSetter and Answerer roles cannot change the active site-wide Theme.
- Backgrounds, panel and border textures, header and corner decorations, navigation and feature icons, empty-state illustrations, and mascots must use the shared `ISecureUploadValidator` foundation.
- V1 image assets are limited to PNG, JPEG, and WebP with matching extension, declared MIME type, and file signature.
- SVG, custom CSS, custom JavaScript, and custom HTML are not accepted Theme asset formats.
- Custom assets are optional. Missing or rejected assets must not alter the existing background, panels, cards, navigation, icons, or appearance tokens.

SECURITY-10D introduces no Theme upload API, database schema, selector, preview, placeholder, or visual change.
