namespace SportsPlatform.Modules.Platform.Contracts.Media;

/// <summary>
/// Endpoint metadata: the endpoint stores an uploaded file through <see cref="IMediaAssets.StoreAsync"/>, so it can
/// answer that method's refusals. Platform declares those errors (the catalogue reads module assemblies only), so the
/// uploading module can't name them; the Host documents them on every endpoint that carries this (S06-T5b), and
/// <c>MEDIA_IMAGE_TOO_SMALL</c> where <paramref name="Purpose"/> has a minimum size (S07-T4b, review #133 R1).
/// </summary>
/// <param name="Purpose">The purpose the endpoint stores the file under.</param>
public sealed record StoresMediaMetadata(MediaPurpose Purpose);
