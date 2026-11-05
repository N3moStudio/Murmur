using System.Security.Cryptography;
using System.Text;

namespace Murmur.Core;

public sealed record ReleaseFile(string Url, string Sha256, long Bytes);
public sealed record Release(string Version, Dictionary<string, ReleaseFile> Platforms);
public sealed record SignedRelease(string Payload, string Signature);
public static class UpdateManifest
{
    public static Release Verify(string manifest, string publicKey, string currentVersion, string architecture)
    {
        var envelope = Json.Read<SignedRelease>(manifest); byte[] payload = Convert.FromBase64String(envelope.Payload);
        using var rsa = RSA.Create(); rsa.ImportFromPem(publicKey);
        if (!rsa.VerifyData(payload, Convert.FromBase64String(envelope.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) throw new InvalidDataException("The update signature is invalid.");
        var release = Json.Read<Release>(Encoding.UTF8.GetString(payload));
        if (!System.Version.TryParse(release.Version, out var version) || version <= System.Version.Parse(currentVersion)) throw new InvalidDataException("No newer update is available.");
        if (!release.Platforms.TryGetValue(architecture, out var file) || !Uri.TryCreate(file.Url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !System.Text.RegularExpressions.Regex.IsMatch(file.Sha256, "^[a-fA-F0-9]{64}$") || file.Bytes is <= 0 or > 4_000_000_000) throw new InvalidDataException("The release has no valid installer for this native architecture.");
        return release;
    }
}
