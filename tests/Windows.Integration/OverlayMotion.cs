using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;
using Murmur.Windows;

internal static class OverlayMotion
{
    public static void Run(string output)
    {
        using var host = new Form { Width = 1, Height = 1, Opacity = 0, ShowInTaskbar = false };
        host.Shown += async (_, _) =>
        {
            host.Hide();
            using var overlay = new RecordingOverlay();
            var checks = new Dictionary<string, bool>();
            try
            {
                var timer = (System.Windows.Forms.Timer)typeof(RecordingOverlay).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(overlay)!;
                string Frame()
                {
                    using var bitmap = new Bitmap(overlay.Width, overlay.Height);
                    overlay.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    using var bytes = new MemoryStream(); bitmap.Save(bytes, System.Drawing.Imaging.ImageFormat.Png);
                    return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
                }
                overlay.Animation = "on"; overlay.UpdateState("processing", "Thinking…"); overlay.Show();
                await Task.Delay(120); string first = Frame(); await Task.Delay(180);
                checks["processing orb changes rendered frame"] = first != Frame() && timer.Enabled;
                checks["power frame cap"] = timer.Interval == (SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline ? 67 : 33);
                overlay.Hide(); checks["hidden timer stopped"] = !timer.Enabled;
                overlay.Animation = "off"; overlay.Show(); overlay.UpdateState("processing", "Thinking…");
                first = Frame(); await Task.Delay(180);
                checks["still preference stops motion"] = first == Frame() && !timer.Enabled;
                overlay.Animation = "system"; overlay.UpdateState("processing", "Thinking…");
                bool reduced = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "MinAnimate", "1") as string == "0";
                checks["system preference follows Windows"] = timer.Enabled == !reduced;
                overlay.Animation = "on"; overlay.UpdateState("recording", "Listening");
                checks["recording has no orb timer"] = !timer.Enabled;
                File.WriteAllText(output, JsonSerializer.Serialize(new { checks, reducedMotion = reduced, failures = checks.Count(x => !x.Value) }));
                Environment.ExitCode = checks.Values.All(x => x) ? 0 : 1;
            }
            catch (Exception ex) { File.WriteAllText(output, JsonSerializer.Serialize(new { error = ex.ToString() })); Environment.ExitCode = 1; }
            finally { overlay.Hide(); host.Close(); }
        };
        Application.Run(host);
    }
}
