using System.Diagnostics;
using System.Text.Json;
using System.IO;
using Murmur.Core;
using Murmur.Services;
var results = new List<object>(); int failures = 0;
void Check(string name, bool pass) { results.Add(new {name, pass}); if (!pass) failures++; }
string root = Path.GetFullPath("artifacts/service-fixtures/" + Guid.NewGuid().ToString("N"));
using var store = new Store(root);
Secrets.Save(root, "fixture", "PRIVATE-TEST-SECRET");
Check("DPAPI round trip", Secrets.Read(root, "fixture") == "PRIVATE-TEST-SECRET");
Check("DPAPI file is encrypted", !System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root,"fixture.secret"))).Contains("PRIVATE-TEST-SECRET"));
Secrets.Delete(root,"fixture"); Check("secret deletion", Secrets.Read(root,"fixture") is null);
using var google = JsonDocument.Parse("""{"items":[{"id":"a","summary":"Review","start":{"dateTime":"2026-11-04T10:00:00-08:00"},"end":{"dateTime":"2026-11-04T11:00:00-08:00"},"hangoutLink":"https://meet.google.com/example"},{"id":"cancel","status":"cancelled"},{"id":"all","start":{"date":"2026-11-04"},"end":{"date":"2026-11-05"}}]}""");
var events=CalendarService.ParseEvents("google",google.RootElement); Check("Google filters cancelled/all-day and parses timezone", events.Count==1 && events[0].Start.UtcDateTime.Hour==18 && events[0].JoinUrl.StartsWith("https://"));
using var outlook=JsonDocument.Parse("""{"value":[{"id":"b","subject":"Standup","start":{"dateTime":"2026-11-04T18:00:00","timeZone":"UTC"},"end":{"dateTime":"2026-11-04T19:00:00","timeZone":"UTC"},"onlineMeeting":{"joinUrl":"javascript:bad"}}]}""");
events=CalendarService.ParseEvents("microsoft",outlook.RootElement);Check("Outlook UTC and unsafe join URL",events.Count==1 && events[0].Start.Offset==TimeSpan.Zero && events[0].JoinUrl=="");
var upcoming=new CalendarEvent("fixture","google","Review",DateTimeOffset.UtcNow.AddMinutes(2),DateTimeOffset.UtcNow.AddMinutes(30));store.Save("calendar-event",upcoming.Id,upcoming);
using var calendar=new CalendarService(store);Check("reminders once per event",calendar.DueReminders().Count==1 && calendar.DueReminders().Count==0);
var meeting=new Meeting(Guid.NewGuid().ToString("N"),"Fixture review",DateTimeOffset.UtcNow,Notes:"PRIVATE NOTE",Summary:"Ship Friday",Transcript:new string('a',14000),Audio:["PRIVATE AUDIO"]);store.Save("meeting",meeting.Id,meeting);
store.Settings=store.Settings with {McpEnabled=true};
var info=new ProcessStartInfo(args[0]){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
info.ArgumentList.Add("--data-dir");info.ArgumentList.Add(root);info.ArgumentList.Add("--mcp");
using(var child=Process.Start(info)!){
 string[] requests={Json.Write(new{jsonrpc="2.0",id=1,method="initialize"}),Json.Write(new{jsonrpc="2.0",id=2,method="tools/list"}),Json.Write(new{jsonrpc="2.0",id=3,method="tools/call",@params=new{name="read_meeting",arguments=new{id=meeting.Id,offset=0}}}),Json.Write(new{jsonrpc="2.0",id=4,method="tools/call",@params=new{name="read_meeting",arguments=new{id=meeting.Id,offset=12000}}}),Json.Write(new{jsonrpc="2.0",id=5,method="tools/call",@params=new{name="delete_meeting",arguments=new{id=meeting.Id}}})};
 foreach(var request in requests)await child.StandardInput.WriteLineAsync(request);child.StandardInput.Close();string output=await child.StandardOutput.ReadToEndAsync();await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
 var lines=output.Split('\n',StringSplitOptions.RemoveEmptyEntries);Check("MCP real stdio protocol",lines.Length==5 && child.ExitCode==0);Check("MCP excludes private notes/audio",!output.Contains("PRIVATE"));Check("MCP paginates",output.Contains("12000"));Check("MCP rejects writes",lines.Last().Contains("Unknown tool"));
}
await DownloadChecks.Run(store, (name, passed) => Check(name, passed));
File.WriteAllText("artifacts/services-integration.json",Json.Write(new{failures,results}));Console.WriteLine($"{results.Count-failures}/{results.Count} service checks passed");Environment.ExitCode=failures==0?0:1;
