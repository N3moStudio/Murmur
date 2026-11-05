using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Murmur.Core;

// Stores text only. Exact PCM and engine identity must match before reuse;
// changing even one sample forces another decode. Bounded and session-scoped.
public sealed class DecodedAudioCache(int capacity = 128)
{
    private readonly Dictionary<string, string> entries = new();
    private readonly Queue<string> order = new();
    private readonly object sync = new();
    public static string Key(string engine, ReadOnlySpan<float> samples) =>
        engine + ":" + samples.Length + ":" + Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(samples)));
    public bool TryGet(string key, out string value) { lock (sync) return entries.TryGetValue(key, out value!); }
    public void Add(string key, string value)
    {
        if (capacity < 1) return;
        lock (sync)
        {
            if (!entries.ContainsKey(key))
            {
                while (entries.Count >= capacity) entries.Remove(order.Dequeue());
                order.Enqueue(key);
            }
            entries[key] = value;
        }
    }
    public void Clear() { lock (sync) { entries.Clear(); order.Clear(); } }
}
