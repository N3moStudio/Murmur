using System.Text.Json;
using Murmur.Core;

namespace Murmur.Services;

public static class McpServer
{
    public static async Task Run(Store store)
    {
        using var input = new StreamReader(Console.OpenStandardInput()); using var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        string? line;
        while ((line = await input.ReadLineAsync()) is not null)
        {
            object? id = null;
            try
            {
                if (line.Length > 1_000_000) throw new InvalidDataException("Request too large.");
                using var doc = JsonDocument.Parse(line); var root = doc.RootElement;
                if (!root.TryGetProperty("id", out var idElement)) continue; id = idElement.Clone();
                if (!store.Settings.McpEnabled) throw new InvalidOperationException("Enable local MCP in Murmur settings first.");
                string method = root.GetProperty("method").GetString()!;
                object result = method switch
                {
                    "initialize" => new { protocolVersion = "2024-11-05", capabilities = new { tools = new { } }, serverInfo = new { name = Brand.Name, version = Brand.Version } },
                    "ping" => new { },
                    "tools/list" => new { tools = new object[] {
                        new { name = "search_meetings", description = "Search saved meeting titles, summaries and transcripts. Excludes dictation and private notes.", inputSchema = new { type = "object", properties = new { query = new { type = "string" } }, required = new[] { "query" } } },
                        new { name = "read_meeting", description = "Read one meeting transcript and summary, paginated. No private notes or audio.", inputSchema = new { type = "object", properties = new { id = new { type = "string" }, offset = new { type = "integer", minimum = 0 } }, required = new[] { "id" } } }
                    } },
                    "tools/call" => Call(store, root.GetProperty("params")),
                    _ => throw new InvalidOperationException("Unsupported read-only method.")
                };
                await output.WriteLineAsync(Json.Write(new { jsonrpc = "2.0", id, result }));
            }
            catch (Exception ex) { await output.WriteLineAsync(Json.Write(new { jsonrpc = "2.0", id, error = new { code = -32602, message = ex.Message } })); }
        }
    }
    public static object Call(Store store, JsonElement args)
    {
        string name = args.GetProperty("name").GetString()!; var a = args.GetProperty("arguments"); object payload;
        if (name == "search_meetings")
        {
            var query = a.GetProperty("query").GetString() ?? "";
            payload = store.List<Meeting>("meeting", 1000).Where(m => (m.Title + m.Summary + m.Transcript).Contains(query, StringComparison.OrdinalIgnoreCase)).Take(50).Select(m => new { m.Id, m.Title, m.Created });
        }
        else if (name == "read_meeting")
        {
            var m = store.Get<Meeting>("meeting", a.GetProperty("id").GetString()!) ?? throw new KeyNotFoundException("Meeting not found.");
            int offset = a.TryGetProperty("offset", out var o) ? Math.Max(0, o.GetInt32()) : 0; offset = Math.Min(offset, m.Transcript.Length); int length = Math.Min(12000, m.Transcript.Length - offset);
            payload = new { m.Id, m.Title, m.Created, summary = offset == 0 ? m.Summary : "", transcript = m.Transcript.Substring(offset, length), nextOffset = offset + length < m.Transcript.Length ? (int?)(offset + length) : null };
        }
        else throw new InvalidOperationException("Unknown tool. This server cannot modify data.");
        return new { content = new[] { new { type = "text", text = Json.Write(payload) } } };
    }
}
