using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Murmur.Core;

namespace Murmur.Services;

public sealed class UpdateService(Store store) : IDisposable
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(15) };
    private Release? release;
    private string trustedKey = "", trustedFeed = "";
    private static string Architecture => RuntimeInformation.OSArchitecture switch { System.Runtime.InteropServices.Architecture.Arm64 => "arm64", System.Runtime.InteropServices.Architecture.X64 => "x64", _ => throw new PlatformNotSupportedException("This release supports Windows ARM64 and x64.") };
    public async Task<object> Check(CancellationToken ct)
    {
        release = null; var settings = store.Settings;
        if (string.IsNullOrWhiteSpace(settings.UpdateFeed) || string.IsNullOrWhiteSpace(settings.UpdatePublicKey)) throw new InvalidOperationException("No release service is configured. Add your publisher’s HTTPS manifest and public key, then save settings. Local installers can be used for upgrades.");
        if (!Uri.TryCreate(settings.UpdateFeed, UriKind.Absolute, out var feed) || feed.Scheme != "https") throw new InvalidOperationException("The release manifest must use HTTPS.");
        using var response = await http.GetAsync(feed, HttpCompletionOption.ResponseHeadersRead, ct); response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri?.Scheme != "https") throw new InvalidDataException("The update service redirected to an insecure address.");
        await response.Content.LoadIntoBufferAsync(1_000_000, ct);
        release = UpdateManifest.Verify(await response.Content.ReadAsStringAsync(ct), settings.UpdatePublicKey, Brand.Version, Architecture);
        trustedKey = settings.UpdatePublicKey; trustedFeed = settings.UpdateFeed;
        return new { message = $"Murmur {release.Version} is available for native {Architecture}. The release signature is valid. Choose Install verified update when ready.", version = release.Version, bytes = release.Platforms[Architecture].Bytes };
    }
    public async Task Install(CancellationToken ct)
    {
        var settings = store.Settings;
        var version = release ?? throw new InvalidOperationException("Check for a verified update first.");
        if (settings.UpdateFeed != trustedFeed || settings.UpdatePublicKey != trustedKey) throw new InvalidOperationException("The update source changed. Check again before installing.");
        var file = version.Platforms[Architecture]; string folder = Path.Combine(store.Root, "updates"); Directory.CreateDirectory(folder);
        if (new DriveInfo(Path.GetPathRoot(folder)!).AvailableFreeSpace < file.Bytes * 3) throw new IOException("Not enough space to download and safely install this update.");
        string destination = Path.Combine(folder, $"Murmur-{version.Version}-{Architecture}.exe"), part = destination + ".partial";
        using (var response = await http.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri?.Scheme != "https") throw new InvalidDataException("The installer redirected to an insecure address.");
            await using var input = await response.Content.ReadAsStreamAsync(ct);
            await using var output = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);
            byte[] buffer = new byte[65536]; int n; long received = 0;
            while ((n = await input.ReadAsync(buffer, ct)) > 0) { received += n; if (received > file.Bytes) throw new InvalidDataException("The installer exceeds its signed size."); await output.WriteAsync(buffer.AsMemory(0, n), ct); }
            if (received != file.Bytes) throw new InvalidDataException("The installer download was incomplete. Try installing again.");
        }
        await using (var input = File.OpenRead(part))
            if (!Convert.ToHexString(await SHA256.HashDataAsync(input, ct)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) { input.Close(); File.Delete(part); throw new InvalidDataException("The installer integrity check failed. Nothing was installed."); }
        ct.ThrowIfCancellationRequested(); File.Move(part, destination, true);
        store.Backup(Path.Combine(folder, "before-" + version.Version + ".sqlite"));
        var process = Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true }) ?? throw new InvalidOperationException("Windows could not open the verified installer. The existing application and backup remain available.");
        process.Dispose();
    }
    public void Dispose() => http.Dispose();
}
