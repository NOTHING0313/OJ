# Theme Library and Theme Pack V1

## Authority and persistence

The active public site remains the existing `appearance` SiteSetting and existing ThemeProvider. The Theme Library is editor-only data serialized under a separate `theme-library` SiteSetting row; its PostgreSQL `text` value is suitable for the bounded library and no table or migration is introduced. Ordinary runtime pages never query the preset library.

Each stored preset has a server-generated GUID, trimmed 1–64 character name, optional plain-text description up to 256 characters, schema version, complete `SiteAppearance`, and created/updated timestamps. The library is limited to 30 presets. Names are case-insensitively unique. The built-in `Default Theme` is virtual, exact, and never serialized as an ordinary preset.

Mutations are serialized inside the API process so two concurrent editor operations cannot overwrite one another. The current production topology has one API writer; a future multi-writer deployment must add a database-backed concurrency token before scaling this mutable SiteSetting contract across API instances.

Creating, updating, duplicating, renaming, deleting, importing, exporting, listing, and applying presets are Root-only. Saving a Draft as a preset does not mutate `appearance`. Applying a preset delegates to the existing Appearance validation/update service; the library does not provide a second runtime pointer. Missing physical assets are disabled during apply so the exact existing fallback remains usable.

Deleting or renaming a preset changes metadata only. Duplicating copies `SiteAppearance` references, not asset bytes. Deleting a preset never deletes files. The Theme Asset library computes `Used By` from both current Appearance and all presets, and explicit asset deletion returns conflict while any reference exists.

## Pack format

`ThemePackContract.Version` is the centralized V1 authority. A portable ZIP contains exactly:

```text
manifest.json
assets/001.png
assets/002.jpg
assets/003.webp
```

The manifest format is `onlinejudge-theme`, version `1`, preset schema version `1`, metadata, and one `SiteAppearance`. Asset references are logical `assets/<server-numbered-file>` keys only. Export includes referenced assets only, deduplicates a reused physical file, and exposes no server path, database data, users, JWTs, audit records, runtime configuration, CSS, JavaScript, HTML, or SVG. Limits are 50 assets and 50 MiB compressed.

V1 may include an optional `assets` metadata array containing a logical pack path and normalized display name. This metadata never changes the server-controlled archive path. Older V1 packs without the array remain valid; imported assets receive a safe basename fallback. No pack version bump is required.

## Import security and atomicity

Import uses the SECURITY-10D upload boundary plus `ISecureArchiveExtractor`; it never calls unchecked `ExtractToDirectory`. The extractor rejects traversal, rooted or drive paths, double-decoded traversal, backslashes, symlinks, duplicate entries, unexpected directories or file types, excessive entry count, excessive expanded/per-entry bytes, and excessive compression ratio. Only `manifest.json` and PNG/JPEG/WebP files directly under `assets/` are accepted.

JSON deserialization rejects unknown fields. Format/version, required appearance nodes, controlled page/icon/decoration slots, numeric ranges, name/description, and logical references are validated. Every archived image passes the existing ThemeImage filename, MIME, magic-byte, trailer, and 5 MiB policy. Imported asset IDs are ignored; fresh generated IDs and same-origin URLs replace all logical pack references. Unreferenced archive files and missing referenced files are rejected.

All entries and images are validated before a preset is persisted. New files are tracked and removed if validation or persistence fails, so no partial preset or imported assets remain. Import never auto-applies. A name collision receives the first safe `(2)`, `(3)`, and so on suffix rather than overwriting an existing preset.

The editor first uploads the selected ZIP to the Root-only preflight endpoint. Preflight runs the same archive, manifest, reference, slot, and image validators as commit import and returns only a safe summary. It creates no preset, asset, active Appearance change, or audit event. Because packs are capped at 50 MiB, confirmation re-uploads and re-validates the file instead of creating a server-side import session or staging token. This trades one additional upload for no import-session state, TTL, cleanup job, or time-of-check/time-of-use trust.

## Audit and operational limits

Lifecycle actions are `ThemePreset.Created`, `ThemePreset.Updated`, `ThemePreset.Duplicated`, `ThemePreset.Renamed`, `ThemePreset.Deleted`, `ThemePreset.Imported`, and `Theme.Applied`. Metadata is limited to preset/source IDs, name, schema version, and asset count. Full Appearance JSON, archive bytes, URLs, filesystem paths, credentials, and secrets are excluded.

The serialized library is bounded by 30 presets and the finite Appearance contract. Theme assets remain persistent runtime files under `Storage__ThemeAssetsRoot`; ZIPs and temporary extraction data are never stored there or committed to the repository.
