using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Murmur.Core;
using Murmur.Services;

internal static class DownloadChecks
{
    public static async Task Run(Store store, Action<string, bool> check)
    {
        var reserve = new TcpListener(IPAddress.Loopback, 0); reserve.Start(); int port = ((IPEndPoint)reserve.LocalEndpoint).Port; reserve.Stop();
        string url = $"http://127.0.0.1:{port}/"; using var listener = new HttpListener(); listener.Prefixes.Add(url); listener.Start();
        byte[] data = System.Text.Encoding.UTF8.GetBytes("Verified model fixture bytes."); string hash = Convert.ToHexString(SHA256.HashData(data));
        var models = new ModelManager(store); models.Catalog.Clear(); models.Catalog.Add(new("fixture.bin", data.Length, hash, url, "test fixture"));
        string path = models.PathFor("fixture.bin"); await File.WriteAllBytesAsync(path + ".partial", data[..7]);
        bool range = false;
        var serve = Task.Run(async () => { var request = await listener.GetContextAsync(); range = request.Request.Headers["Range"] == "bytes=7-"; request.Response.StatusCode = 206; request.Response.Headers["Content-Range"] = $"bytes 7-{data.Length-1}/{data.Length}"; request.Response.ContentLength64 = data.Length - 7; await request.Response.OutputStream.WriteAsync(data.AsMemory(7)); request.Response.Close(); });
        await models.Download("fixture.bin"); await serve;
        check("model download resumes verified partial bytes", range && File.ReadAllBytes(path).SequenceEqual(data) && !File.Exists(path + ".partial"));
        serve = Task.Run(async () => { var request = await listener.GetContextAsync(); request.Response.ContentLength64 = data.Length; await request.Response.OutputStream.WriteAsync(new byte[data.Length]); request.Response.Close(); });
        bool rejected = false; try { await models.Download("fixture.bin"); } catch (InvalidDataException) { rejected = true; } await serve;
        check("bad model hash preserves installed bytes", rejected && File.ReadAllBytes(path).SequenceEqual(data) && !File.Exists(path + ".partial"));
        using var cancel = new CancellationTokenSource();
        serve = Task.Run(async () => { var request = await listener.GetContextAsync(); request.Response.ContentLength64 = data.Length; await request.Response.OutputStream.WriteAsync(data.AsMemory(0, 7)); await request.Response.OutputStream.FlushAsync(); await Task.Delay(150); cancel.Cancel(); request.Response.Abort(); });
        bool cancelled = false; try { await models.Download("fixture.bin", cancel.Token); } catch (OperationCanceledException) { cancelled = true; } await serve;
        check("cancelled model transfer preserves installed model", cancelled && File.ReadAllBytes(path).SequenceEqual(data));
        check("interrupted download remains resumable", File.Exists(path + ".partial") && new FileInfo(path + ".partial").Length < data.Length);
    }
}
