using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Murmur.Core;

namespace Murmur.Services;

public sealed record CalendarEvent(string Id, string Provider, string Title, DateTimeOffset Start, DateTimeOffset End, string JoinUrl = "");
internal sealed record CalendarToken(string Provider, string ClientId, string ClientSecret, string AccessToken, string RefreshToken, DateTimeOffset Expires);
public sealed class CalendarService(Store store) : IDisposable
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(40) };
    private static string Provider(string value) => value is "google" or "microsoft" ? value : throw new ArgumentException("Choose Google or Outlook.");
    private static string Encode(string value) => Uri.EscapeDataString(value);
    private static string Base64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Endpoint(string provider, bool authorize) => provider == "google"
        ? authorize ? "https://accounts.google.com/o/oauth2/v2/auth" : "https://oauth2.googleapis.com/token"
        : "https://login.microsoftonline.com/common/oauth2/v2.0/" + (authorize ? "authorize" : "token");
    private static string Scope(string provider) => provider == "google" ? "https://www.googleapis.com/auth/calendar.events.readonly" : "offline_access https://graph.microsoft.com/Calendars.Read";
    public object Connections() => new[] { "google", "microsoft" }.Select(p => new { provider = p, connected = Secrets.Read(store.Root, "calendar-" + p) is not null }).ToArray();
    public List<CalendarEvent> Upcoming() => store.List<CalendarEvent>("calendar-event", 200).Where(e => e.End > DateTimeOffset.Now && e.Start < DateTimeOffset.Now.AddDays(7)).OrderBy(e => e.Start).Take(50).ToList();
    public List<CalendarEvent> DueReminders()
    {
        if (!store.Settings.CalendarReminders) return [];
        var due = Upcoming().Where(e => e.Start > DateTimeOffset.Now.AddMinutes(-1) && e.Start <= DateTimeOffset.Now.AddMinutes(5)).ToList();
        var result = new List<CalendarEvent>();
        foreach (var item in due)
        {
            string key = item.Id + ":" + item.Start.ToUnixTimeSeconds();
            if (store.Get<bool>("calendar-reminder", key)) continue;
            store.Save("calendar-reminder", key, true); result.Add(item);
        }
        return result;
    }
    public async Task Connect(string provider, string clientId, string clientSecret, CancellationToken ct)
    {
        Provider(provider);
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Length > 500) throw new ArgumentException("Enter your registered desktop OAuth client ID. See Help → Calendar setup.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Server.ExclusiveAddressUse = true; listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            string redirect = (provider == "google" ? "http://127.0.0.1:" : "http://localhost:") + port + "/";
            string verifier = Base64(RandomNumberGenerator.GetBytes(32)), state = Base64(RandomNumberGenerator.GetBytes(32));
            string challenge = Base64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            string url = Endpoint(provider, true) + $"?client_id={Encode(clientId)}&response_type=code&redirect_uri={Encode(redirect)}&scope={Encode(Scope(provider))}&state={Encode(state)}&code_challenge={challenge}&code_challenge_method=S256";
            if (provider == "google") url += "&access_type=offline&prompt=consent";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            string code = await ReadCallback(listener, state, timeout.Token);
            var fields = new Dictionary<string, string> { ["client_id"] = clientId, ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirect, ["code_verifier"] = verifier };
            if (provider == "google" && !string.IsNullOrEmpty(clientSecret)) fields["client_secret"] = clientSecret;
            if (provider == "microsoft") fields["scope"] = Scope(provider);
            var token = await TokenRequest(provider, fields, timeout.Token);
            Secrets.Save(store.Root, "calendar-" + provider, Json.Write(new CalendarToken(provider, clientId, provider == "google" ? clientSecret : "", token.Access, token.Refresh, token.Expires)));
            await Refresh(provider, timeout.Token);
        }
        finally { listener.Stop(); }
    }
    private static async Task<string> ReadCallback(TcpListener listener, string expectedState, CancellationToken ct)
    {
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync(ct);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(5));
            var stream = client.GetStream(); byte[] bytes = new byte[8192]; int length = 0;
            while (length < bytes.Length)
            {
                int n = await stream.ReadAsync(bytes.AsMemory(length), deadline.Token); if (n == 0) break; length += n;
                if (Encoding.ASCII.GetString(bytes, 0, length).Contains("\r\n\r\n", StringComparison.Ordinal)) break;
            }
            string first = Encoding.ASCII.GetString(bytes, 0, length).Split('\n')[0]; var parts = first.Split(' ');
            var query = parts.Length >= 2 && parts[0] == "GET" ? new Uri("http://localhost" + parts[1]).Query : "";
            var fields = System.Web.HttpUtility.ParseQueryString(query);
            var actual = fields["state"] ?? "";
            bool valid = actual.Length == expectedState.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(expectedState));
            string message = valid ? "You can close this tab and return to Murmur." : "This callback was not accepted. Return to Murmur to reconnect.";
            var body = Encoding.UTF8.GetBytes(message);
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {(valid ? "200 OK" : "400 Bad Request")}\r\nContent-Type: text/plain; charset=utf-8\r\nCache-Control: no-store\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, ct); await stream.WriteAsync(body, ct);
            if (!valid) continue;
            if (fields["error"] is not null) throw new InvalidOperationException("Calendar authorization was declined or unavailable. No connection was saved.");
            return fields["code"] ?? throw new InvalidDataException("The calendar provider did not return an authorization code.");
        }
    }
    private async Task<(string Access, string Refresh, DateTimeOffset Expires)> TokenRequest(string provider, Dictionary<string, string> fields, CancellationToken ct)
    {
        using var response = await http.PostAsync(Endpoint(provider, false), new FormUrlEncodedContent(fields), ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Calendar authorization failed ({(int)response.StatusCode}). Check the desktop client registration or reconnect.");
        using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); var r = data.RootElement;
        return (r.GetProperty("access_token").GetString()!, r.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() ?? "" : "", DateTimeOffset.UtcNow.AddSeconds(r.GetProperty("expires_in").GetInt32()));
    }
    public async Task<List<CalendarEvent>> Refresh(string provider, CancellationToken ct)
    {
        Provider(provider);
        var token = Json.Read<CalendarToken>(Secrets.Read(store.Root, "calendar-" + provider) ?? throw new InvalidOperationException("Connect this calendar first."));
        if (token.Expires <= DateTimeOffset.UtcNow.AddMinutes(2))
        {
            if (token.RefreshToken.Length == 0) throw new InvalidOperationException("Reconnect this calendar to renew access.");
            var fields = new Dictionary<string, string> { ["client_id"] = token.ClientId, ["grant_type"] = "refresh_token", ["refresh_token"] = token.RefreshToken };
            if (token.ClientSecret.Length > 0) fields["client_secret"] = token.ClientSecret;
            if (provider == "microsoft") fields["scope"] = Scope(provider);
            var fresh = await TokenRequest(provider, fields, ct);
            token = token with { AccessToken = fresh.Access, RefreshToken = fresh.Refresh.Length > 0 ? fresh.Refresh : token.RefreshToken, Expires = fresh.Expires };
            Secrets.Save(store.Root, "calendar-" + provider, Json.Write(token));
        }
        string from = Encode(DateTimeOffset.UtcNow.ToString("O")), to = Encode(DateTimeOffset.UtcNow.AddDays(7).ToString("O"));
        string? url = provider == "google" ? $"https://www.googleapis.com/calendar/v3/calendars/primary/events?timeMin={from}&timeMax={to}&singleEvents=true&orderBy=startTime&maxResults=50" : $"https://graph.microsoft.com/v1.0/me/calendarView?startDateTime={from}&endDateTime={to}&$top=50&$orderby=start/dateTime&$select=id,subject,start,end,onlineMeeting,isCancelled";
        var events = new List<CalendarEvent>(); int pages = 0;
        while (url is not null && events.Count < 50 && pages++ < 5)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url); request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken); request.Headers.TryAddWithoutValidation("Prefer", "outlook.timezone=\"UTC\"");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Calendar refresh failed ({(int)response.StatusCode}). Saved meetings are unchanged; try reconnecting.");
            await response.Content.LoadIntoBufferAsync(2_000_000, ct);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            events.AddRange(ParseEvents(provider, json.RootElement));
            if (provider == "google" && json.RootElement.TryGetProperty("nextPageToken", out var page)) url = $"https://www.googleapis.com/calendar/v3/calendars/primary/events?timeMin={from}&timeMax={to}&singleEvents=true&orderBy=startTime&maxResults=50&pageToken={Encode(page.GetString()!)}";
            else if (provider == "microsoft" && json.RootElement.TryGetProperty("@odata.nextLink", out var link) && Uri.TryCreate(link.GetString(), UriKind.Absolute, out var next) && next.Scheme == "https" && next.Host == "graph.microsoft.com") url = next.AbsoluteUri;
            else url = null;
        }
        ct.ThrowIfCancellationRequested();
        foreach (var old in store.List<CalendarEvent>("calendar-event").Where(e => e.Provider == provider)) store.Delete("calendar-event", old.Id);
        store.SaveBatch(events.Take(50).Select(e => ("calendar-event", e.Id, Json.Write(e)))); return Upcoming();
    }
    public static List<CalendarEvent> ParseEvents(string provider, JsonElement root)
    {
        var list = new List<CalendarEvent>();
        if (!root.TryGetProperty(provider == "google" ? "items" : "value", out var values)) return list;
        foreach (var e in values.EnumerateArray())
        {
            if (e.TryGetProperty("status", out var status) && status.GetString() == "cancelled" || e.TryGetProperty("isCancelled", out var cancelled) && cancelled.GetBoolean()) continue;
            var start = e.GetProperty("start"); var end = e.GetProperty("end");
            if (!start.TryGetProperty("dateTime", out var s) || !end.TryGetProperty("dateTime", out var t)) continue; // All-day entries are not meeting reminders.
            if (!DateTimeOffset.TryParse(s.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when) || !DateTimeOffset.TryParse(t.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var until)) continue;
            string join = e.TryGetProperty("hangoutLink", out var google) ? google.GetString() ?? "" : e.TryGetProperty("onlineMeeting", out var ms) && ms.ValueKind == JsonValueKind.Object && ms.TryGetProperty("joinUrl", out var msUrl) ? msUrl.GetString() ?? "" : "";
            if (!Uri.TryCreate(join, UriKind.Absolute, out var uri) || uri.Scheme != "https") join = "";
            string title = e.TryGetProperty(provider == "google" ? "summary" : "subject", out var name) ? name.GetString() ?? "Untitled meeting" : "Untitled meeting";
            list.Add(new(provider + ":" + e.GetProperty("id").GetString(), provider, title, when, until, join));
        }
        return list;
    }
    public void Disconnect(string provider)
    {
        Provider(provider); Secrets.Delete(store.Root, "calendar-" + provider);
        foreach (var item in store.List<CalendarEvent>("calendar-event").Where(e => e.Provider == provider)) store.Delete("calendar-event", item.Id);
    }
    public void Dispose() => http.Dispose();
}
