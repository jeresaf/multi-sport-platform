# ADR-0017 — Image processing with SkiaSharp

**Status:** Accepted (owner, 2026-11-18)
**Date:** 2026-11-18
**Deciders:** Owner (human), Reviewer (Claude), Builder (Claude Code)

## Context

ADR-0014 makes Platform the owner of media and says images get resized variants "using a permissively-licensed
library (ADR-0013)". S06 stores the first images: person photos (S06 D16). A photo must be re-encoded into
`thumbnail` (160 px), `medium` (640 px) and `large` (1600 px, the longest side), dropping EXIF data such as GPS
positions, and the original is not kept. .NET has no image codec of its own that runs on Linux. ImageSharp is not
allowed (commercial licence for this use; CLAUDE.md non-negotiable 11). Every new dependency with native code needs
an ADR (ADR-0013). The owner chose SkiaSharp when answering Q35 (2026-11-16).

## Decision

- Platform decodes, orients, resizes and encodes images with **SkiaSharp** (MIT) and its Linux native assets
  **`SkiaSharp.NativeAssets.Linux.NoDependencies`** (MIT), which bundle their own font and image libraries, so the
  container needs no system packages. The Windows and macOS native assets come with `SkiaSharp` itself (developer
  machines). Versions are pinned in `backend/Directory.Packages.props`, both packages on the same version.
- Only the Platform module references SkiaSharp, and only its media infrastructure uses it
  (`Infrastructure/Media`). An architecture test keeps every other assembly off it.
- Photos: the EXIF orientation is applied, then the image is scaled (never up) so its longest side is at most 160,
  640 and 1600 px, and each variant is encoded as **WebP** (lossy, quality 80). Re-encoding writes pixels only, so no
  metadata (EXIF, GPS, XMP, ICC comments) reaches a variant. The uploaded original is not stored.
- Images are checked before decoding: the type comes from the file's first bytes (JPEG, PNG, WebP), the file size
  from the purpose's limit, and the dimensions from the header (at most 40 megapixels), so a small file can't
  expand into a huge bitmap. A file that doesn't decode is refused (`MEDIA_IMAGE_UNREADABLE`).
- Documents (consent evidence: PDF, JPEG, PNG) are stored as uploaded; SkiaSharp doesn't touch them.

## Alternatives considered

- **Magick.NET (Apache-2.0)** — very broad format support, but large native binaries (ImageMagick), a long history
  of decoder vulnerabilities, and more than we need for three resizes.
- **ImageSharp** — pure managed and convenient, but its licence requires a commercial licence for this use
  (non-negotiable 11).
- **An external image service (Azure, Cloudflare Images)** — moves processing out of the app, but adds infrastructure
  and a provider dependency before M1 (ADR-0013); ADR-0014's review trigger covers it later.

## Consequences

- Positive: MIT licence; no system packages in the container; WebP variants are small; EXIF is dropped by
  construction, not by a scrubbing step that could miss a field.
- Negative / costs: a native dependency (about 10 MB per platform in the build output); native crashes can't be
  caught as exceptions, so inputs are checked (type, size, dimensions) before Skia decodes them; Skia releases must
  be followed for security fixes.
- Follow-up work: keep SkiaSharp's version current with the other dependencies; revisit with ADR-0014's trigger (a
  CDN or image service at scale).

## Rules affected

None changed. AR-33 (cloud-neutral code) is unaffected: SkiaSharp is not a cloud SDK. The architecture test that
keeps SkiaSharp inside Platform is a module rule like AR-02's.

## Review trigger

A security advisory in Skia's decoders that isn't fixed promptly; formats we must accept that Skia can't decode
(HEIC from phones); or image volume that makes in-process resizing a bottleneck.
