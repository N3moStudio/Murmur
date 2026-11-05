using Murmur.Core;
var failures = new List<string>(); var count = 0;
void Test(string name, Action action) { count++; try { action(); Console.WriteLine("PASS " + name); } catch (Exception ex) { failures.Add(name + ": " + ex.Message); Console.WriteLine("FAIL " + name + ": " + ex.Message); } }
void Equal<T>(T actual, T expected) { if (!Equals(actual, expected)) throw new Exception($"Expected {expected}, got {actual}"); }
LibraryEntry E(string trigger, string value, bool shared = false) => new(Guid.NewGuid().ToString(), "snippet", trigger, value, shared);
Test("decoded audio cache reuses only identical samples and engine", () => {
    var cache = new DecodedAudioCache(); float[] audio = [.1f, -.2f, .3f];
    var key = DecodedAudioCache.Key("engine-v1", audio); cache.Add(key, "original");
    Equal(cache.TryGet(DecodedAudioCache.Key("engine-v1", audio.ToArray()), out var value), true); Equal(value, "original");
    Equal(cache.TryGet(DecodedAudioCache.Key("engine-v2", audio), out _), false);
    audio[2] = .30000004f; Equal(cache.TryGet(DecodedAudioCache.Key("engine-v1", audio), out _), false);
    Equal(cache.TryGet(DecodedAudioCache.Key("engine-v1", audio.AsSpan(0, 2)), out _), false);
});
Test("decoded cache evicts oldest without losing updated entry", () => {
    var cache = new DecodedAudioCache(2); cache.Add("a", "a"); cache.Add("b", "b"); cache.Add("b", "new b"); cache.Add("c", "c");
    Equal(cache.TryGet("a", out _), false); Equal(cache.TryGet("b", out var b), true); Equal(b, "new b");
    cache.Clear(); Equal(cache.TryGet("b", out _), false); Equal(cache.TryGet("c", out _), false);
});
Test("cleanup rejects invented greetings and repeated thanks", () => {
    Equal(TextRules.PreservesProtected("Please send the report.", "Hi Sarah, please send the report. Thank you.", []), false);
    Equal(TextRules.PreservesProtected("Send the report. Thanks, Alex.", "Thank you. Send the report. Thanks, Alex.", []), false);
    Equal(TextRules.PreservesProtected("Thanks, Alex.", "Thank you, Alex.", []), true);
});
Test("cleanup preserves negation including contractions", () => {
    Equal(TextRules.PreservesProtected("Do not cancel it.", "Cancel it.", []), false);
    Equal(TextRules.PreservesProtected("Don't cancel it.", "Do not cancel it.", []), true);
    Equal(TextRules.PreservesProtected("We can start.", "We cannot start.", []), false);
});
Test("longest snippet wins without recursive expansion", () => Equal(TextRules.Expand("my email address, my email", [E("my email", "a@b.com"), E("my email address", "my email")]), "my email, a@b.com"));
Test("matches whole words and punctuation", () => Equal(TextRules.Expand("emailing email, EMAIL!", [E("email", "a@b.com")]), "emailing a@b.com, a@b.com!"));
Test("preserves literal dollars and Unicode replacement", () => Equal(TextRules.Expand("my price", [E("my price", "$50\n你好")]), "$50\n你好"));
Test("personal entries win over shared", () => Equal(TextRules.Expand("name", [E("name", "shared", true), E("name", "personal")]), "personal"));
Test("protects URL exact spelling", () => Equal(TextRules.PreservesProtected("Visit https://example.com/A?b=2", "Visit https://example.com/a?b=2", []), false));
Test("protects numbers and email", () => Equal(TextRules.PreservesProtected("Send 37 to A@b.com", "Send 73 to A@b.com", []), false));
Test("protects vocabulary names", () => Equal(TextRules.PreservesProtected("Ask Naith today", "Ask Nathan today", ["Naith"]), false));
Test("allows filler cleanup", () => Equal(TextRules.PreservesProtected("um send 37 to A@b.com", "Send 37 to A@b.com.", []), true));
Test("rejects empty trigger", () => Equal(TextRules.Validate(E("  ", "a")).Length > 0, true));
Test("rejects huge snippet", () => Equal(TextRules.Validate(E("test", new string('x', 4001))).Length > 0, true));
Test("accepts multiline snippet", () => Equal(TextRules.Validate(E("signature", "Best,\nNaith")), ""));
Test("ARM64 selection uses native machine", () => Equal(TextRules.Architecture(0xaa64), "arm64"));
Test("x64 selection", () => Equal(TextRules.Architecture(0x8664), "x64"));
Test("unsupported architecture rejected", () => { try { TextRules.Architecture(0x14c); throw new Exception("accepted x86"); } catch (PlatformNotSupportedException) { } });
Test("recording transition rejects repeat starts", () => { var m = new RecordingMachine(); Equal(m.Start(), true); Equal(m.Start(), false); Equal(m.Stop(), true); Equal(m.Start(), false); m.Finish(true); Equal(m.State, RecordingState.Completed); Equal(m.Start(), true); });
Test("cancel returns idle from processing", () => { var m = new RecordingMachine(); m.Start(); m.Stop(); m.Cancel(); Equal(m.State, RecordingState.Idle); });
Test("late completion cannot revive canceled work", () => { var m = new RecordingMachine(); m.Start(); m.Stop(); m.Cancel(); m.Finish(true); Equal(m.State, RecordingState.Idle); });
Test("protected numbers cannot become larger substrings", () => Equal(TextRules.PreservesProtected("Send 37", "Send 137", []), false));
Test("protected token repetition cannot disappear", () => Equal(TextRules.PreservesProtected("37 plus 37", "37", []), false));
Test("explicit correction may replace the nearest numeric fact", () => Equal(TextRules.PreservesProtected("Send 37 invoices. Actually, make that 38 invoices.", "Send 38 invoices.", []), true));
Test("numeric correction cannot remove another fact", () => Equal(TextRules.PreservesProtected("Meet at 3:30. Send 37 invoices. Actually make that 38 invoices.", "Send 38 invoices.", []), false));
Test("correction retains its replacement number", () => Equal(TextRules.PreservesProtected("Send 37 invoices. Actually make that 38 invoices.", "Send 37 invoices.", []), false));
Test("cleanup cannot add unsupported numeric facts", () => Equal(TextRules.PreservesProtected("Send 37 invoices.", "Send 37 invoices at 4:30.", []), false));
Test("ordinal date formatting preserves the number", () => Equal(TextRules.PreservesProtected("Meet November 4th at 3:30.", "Meet November 4 at 3:30.", []), true));
Test("time normalization requires a time context", () => { Equal(TextRules.NormalizeSpokenTimes("Meet at 3 30."), "Meet at 3:30."); Equal(TextRules.NormalizeSpokenTimes("Send 3 30 invoices."), "Send 3 30 invoices."); });
Test("time normalization rejects invalid minute values", () => Equal(TextRules.NormalizeSpokenTimes("Meet at 3 75."), "Meet at 3 75."));
Test("common short replies skip unnecessary generation", () => { Equal(TextRules.IsCompleteShortReply("No, not yet."), true); Equal(TextRules.IsCompleteShortReply("Um, no, not yet."), false); Equal(TextRules.IsCompleteShortReply("Actually make that 38."), false); });
Test("email layout separates spoken greeting and sign-off", () => Equal(TextRules.EmailParagraphs("Hi Sarah, we can meet at 3:30. Please send the 38 invoices, thanks, Nathan."), "Hi Sarah,\n\nWe can meet at 3:30. Please send the 38 invoices.\n\nThanks,\nNathan"));
Test("email layout does not invent greeting or sign-off", () => Equal(TextRules.EmailParagraphs("Please do not cancel the meeting."), "Please do not cancel the meeting."));
Test("native orb matches original library vectors", () => {
    using var vectors = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText("tests/fixtures/thinking-orbs-20.json"));
    var expected = vectors.RootElement.GetProperty("dots"); var actual = ThinkingOrb.Frame(.6);
    Equal(actual.Length, expected.GetArrayLength());
    for (int i = 0; i < actual.Length; i++) {
        double[] values = [actual[i].X,actual[i].Y,actual[i].Z,actual[i].R,actual[i].White,actual[i].A];
        string[] names = ["x","y","z","r","white","a"];
        for(int k=0;k<names.Length;k++) if(Math.Abs(values[k]-expected[i].GetProperty(names[k]).GetDouble())>1e-9) throw new Exception($"Orb point {i} {names[k]} differs");
    }
});
Test("double Ctrl requires two short complete taps", () => { var d=new ControlDoubleTap(); Equal(d.Feed(17,true,0),false); Equal(d.Feed(17,false,80),false); Equal(d.Feed(17,true,180),false); Equal(d.Feed(17,false,240),true); Equal(d.Feed(17,true,300),false); Equal(d.Feed(17,false,370),false); });
Test("Ctrl+C and Ctrl+V never start recording", () => { var d=new ControlDoubleTap(); d.Feed(17,true,0); d.Feed(67,true,40); Equal(d.Feed(17,false,100),false); d.Feed(17,true,180); d.Feed(86,true,200); Equal(d.Feed(17,false,240),false); });
Test("long and widely separated Ctrl taps do not start", () => { var d=new ControlDoubleTap(); d.Feed(17,true,0); d.Feed(17,false,300); d.Feed(17,true,350); Equal(d.Feed(17,false,400),false); d.Feed(17,true,1000); Equal(d.Feed(17,false,1060),false); });
Test("idle completed failed and cancelled have no overlay", () => { foreach(var s in new[]{"idle","completed","failed","cancelled"}) Equal(InteractionRules.ShowOverlay(s),false); Equal(InteractionRules.ShowOverlay("recording"),true); Equal(InteractionRules.ShowOverlay("processing"),true); });
Test("Gmail and native Outlook are email context", () => { Equal(InteractionRules.Category("chrome","Inbox - Gmail","Message Body"),"email"); Equal(InteractionRules.Category("olk","New message",""),"email"); });
Test("chat and document context remain distinct", () => { Equal(InteractionRules.Category("msedge","Workspace - Slack","Message"),"work"); Equal(InteractionRules.Category("Discord","Friends","Message"),"personal"); Equal(InteractionRules.Category("chrome","Project - Google Docs","Document"),"document"); Equal(InteractionRules.Category("WindowsTerminal","PowerShell",""),"other"); });
Test("one short Ctrl tap stops active recording", () => { var d=new ControlDoubleTap(); d.Feed(17,true,100,singleTapToStop:true); Equal(d.Feed(17,false,180,singleTapToStop:true),true); });
Test("Ctrl shortcuts do not stop recording", () => { var d=new ControlDoubleTap(); d.Feed(17,true,100,singleTapToStop:true); d.Feed(67,true,130,singleTapToStop:true); Equal(d.Feed(17,false,180,singleTapToStop:true),false); });
Test("ported context rules match upstream Windows cases", () => { using var fixture=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText("tests/fixtures/context-categories.json"));foreach(var c in fixture.RootElement.EnumerateArray()) Equal(AppContextClassifier.Classify(c.GetProperty("process").GetString()!,c.GetProperty("title").GetString()!),c.GetProperty("expected").GetString()); });
Test("recreated editor requires matching identity, text and caret", () => {
 Equal(InteractionRules.SameRecreatedField("editor", "editor", "draft", "draft", 5, 5), true);
 Equal(InteractionRules.SameRecreatedField("editor", "other", "draft", "draft", 5, 5), false);
 Equal(InteractionRules.SameRecreatedField("", "", "draft", "draft", 5, 5), false);
 Equal(InteractionRules.SameRecreatedField("editor", "editor", null, null, 5, 5), false);
 Equal(InteractionRules.SameRecreatedField("editor", "editor", "draft", "changed", 5, 5), false);
 Equal(InteractionRules.SameRecreatedField("editor", "editor", "draft", "draft", 5, 1), false);
 Equal(InteractionRules.SameRecreatedField("editor", "editor", "draft", "draft", null, null), false);
});
Test("double Caps Lock starts and a single tap stops", () => {
 var d=new ControlDoubleTap(0x14); d.Feed(0x14,true,0); Equal(d.Feed(0x14,false,60),false);
 d.Feed(0x14,true,120); Equal(d.Feed(0x14,false,180),true);
 d.Feed(0x14,true,500,singleTapToStop:true); Equal(d.Feed(0x14,false,560,singleTapToStop:true),true);
});
Test("Caps gesture ignores Ctrl, long holds, and modified Caps", () => {
 var d=new ControlDoubleTap(0x14); d.Feed(17,true,0); Equal(d.Feed(17,false,60),false);
 d.Feed(0x14,true,100); Equal(d.Feed(0x14,false,500),false);
 d.Feed(0x14,true,600,anotherKeyHeld:true); Equal(d.Feed(0x14,false,650),false);
 d.Feed(0x14,true,700); d.Feed(65,true,720); Equal(d.Feed(0x14,false,760),false);
 Equal(InteractionRules.ShowOverlay("starting"),true);
});
string TestFolder() { var path = Path.GetFullPath(Path.Combine("artifacts", "test-state", Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(path); return path; }
Test("shared deletion propagates and is not resurrected", () => {
 var root=TestFolder(); using var a=new Store(Path.Combine(root,"a"));using var b=new Store(Path.Combine(root,"b"));string folder=Path.Combine(root,"shared");var entry=E("team word","Alpha",true);
 a.Save("snippet",entry.Id,entry);new SharedLibrary(a).Sync(folder);new SharedLibrary(b).Sync(folder);Equal(b.Get<LibraryEntry>("snippet",entry.Id)?.Value,"Alpha");
 a.Delete("snippet",entry.Id);new SharedLibrary(a).Sync(folder);new SharedLibrary(b).Sync(folder);new SharedLibrary(a).Sync(folder);Equal(b.Get<LibraryEntry>("snippet",entry.Id),null);Equal(a.Get<LibraryEntry>("snippet",entry.Id),null);
});
Test("shared meetings exclude personal notes and audio", () => {
 var root=TestFolder();using var a=new Store(Path.Combine(root,"a"));using var b=new Store(Path.Combine(root,"b"));string shared=Path.Combine(root,"shared");var folder=new Folder(Guid.NewGuid().ToString("N"),"Team",Shared:true);var meeting=new Meeting(Guid.NewGuid().ToString("N"),"Review",DateTimeOffset.UtcNow,Notes:"PRIVATE-NOTE",Summary:"Decision A",Transcript:"Public words",Audio:["PRIVATE-AUDIO"],FolderId:folder.Id);
 a.Save("folder",folder.Id,folder);a.Save("meeting",meeting.Id,meeting);new SharedLibrary(a).Sync(shared);var exchange=File.ReadAllText(Path.Combine(shared,"murmur-library-v2.json"));Equal(exchange.Contains("PRIVATE-"),false);new SharedLibrary(b).Sync(shared);Equal(b.Get<Meeting>("meeting",meeting.Id)?.Notes,"");
 b.Save("meeting",meeting.Id,b.Get<Meeting>("meeting",meeting.Id)! with {Summary="Decision B"});new SharedLibrary(b).Sync(shared);
 a.Save("meeting",meeting.Id,a.Get<Meeting>("meeting",meeting.Id)! with {Notes="PRIVATE-UPDATED"});new SharedLibrary(a).Sync(shared);Equal(a.Get<Meeting>("meeting",meeting.Id)?.Summary,"Decision B");Equal(a.Get<Meeting>("meeting",meeting.Id)?.Notes,"PRIVATE-UPDATED");Equal(a.Get<Meeting>("meeting",meeting.Id)?.Audio?[0],"PRIVATE-AUDIO");
});
Test("unsharing keeps the private local copy", () => {
 var root=TestFolder();using var a=new Store(Path.Combine(root,"a"));using var b=new Store(Path.Combine(root,"b"));string shared=Path.Combine(root,"shared");var entry=E("team word","Alpha",true);a.Save("snippet",entry.Id,entry);new SharedLibrary(a).Sync(shared);new SharedLibrary(b).Sync(shared);a.Save("snippet",entry.Id,entry with {Shared=false});new SharedLibrary(a).Sync(shared);new SharedLibrary(b).Sync(shared);Equal(a.Get<LibraryEntry>("snippet",entry.Id)?.Shared,false);Equal(b.Get<LibraryEntry>("snippet",entry.Id),null);
});
Test("backup remains readable and transaction import is atomic", () => {
 var root=TestFolder();using var a=new Store(Path.Combine(root,"a"));var entry=E("name","Alpha");a.Save("snippet",entry.Id,entry);
 IEnumerable<(string Kind,string Id,string Json)> BrokenBatch(){yield return ("snippet",Guid.NewGuid().ToString(),Json.Write(E("second","Beta")));throw new InvalidDataException("Fixture interruption");}
 try{a.SaveBatch(BrokenBatch());throw new Exception("Batch unexpectedly completed");}catch(InvalidDataException){}
 Equal(a.List<LibraryEntry>("snippet").Count,1);string backup=Path.Combine(root,"backup.db");a.Backup(backup);using var database=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+backup);database.Open();using var query=database.CreateCommand();query.CommandText="SELECT COUNT(*) FROM documents WHERE deleted=0";Equal(Convert.ToInt32(query.ExecuteScalar()),1);
});
Test("signed updates reject wrong architecture and tampering", () => {
 using var rsa=System.Security.Cryptography.RSA.Create(2048);string key=rsa.ExportSubjectPublicKeyInfoPem();var release=new Release("0.2.0",new(){["arm64"]=new("https://example.com/arm64.exe",new string('a',64),123)});byte[] payload=System.Text.Encoding.UTF8.GetBytes(Json.Write(release));var envelope=new SignedRelease(Convert.ToBase64String(payload),Convert.ToBase64String(rsa.SignData(payload,System.Security.Cryptography.HashAlgorithmName.SHA256,System.Security.Cryptography.RSASignaturePadding.Pss)));Equal(UpdateManifest.Verify(Json.Write(envelope),key,"0.1.0","arm64").Version,"0.2.0");
 try{UpdateManifest.Verify(Json.Write(envelope),key,"0.1.0","x64");throw new Exception("accepted wrong architecture");}catch(InvalidDataException){}
 payload[5]^=1;try{UpdateManifest.Verify(Json.Write(envelope with {Payload=Convert.ToBase64String(payload)}),key,"0.1.0","arm64");throw new Exception("accepted tamper");}catch(InvalidDataException){}
});
Test("meeting answer rejects invented room numbers",()=>Equal(GroundedAnswer.Validate("{\"quotes\":[{\"source\":1,\"quote\":\"The meeting was held in room 101.\"}]}",["The budget is $250."]),GroundedAnswer.Missing));
Test("meeting answer retains verified quote and citation",()=>Equal(GroundedAnswer.Validate("{\"quotes\":[{\"source\":1,\"quote\":\"The budget is $250.\"}]}",["Hello. The budget is $250. Ship Friday."]),"[1] “The budget is $250.”"));
Test("meeting questions exclude generic retrieval terms",()=>Equal(string.Join(' ',GroundedAnswer.Terms("What room number was the meeting held in?")),"room number held"));
Test("meeting answer fails closed on wrong source and invalid JSON",()=>{Equal(GroundedAnswer.Validate("invented answer",["Source words"]),GroundedAnswer.Missing);Equal(GroundedAnswer.Validate("{\"quotes\":[{\"source\":2,\"quote\":\"Source words\"}]}",["Source words"]),GroundedAnswer.Missing);});
Test("dictation may choose an editor after starting without one",()=>{
 var empty=new Target(1,"browser","page",null,"",false);
 var editor=new Target(1,"browser","compose","Hello","",true,Caret:5);
 Equal(DictationTarget.AtCompletion(empty,editor),editor);
 Equal(DictationTarget.AtCompletion(null,editor),editor);
});
Test("returning to an unchanged dictation field follows the new caret",()=>{
 var editor=new Target(1,"browser","compose","Hello there","",true,Caret:11);
 var returned=editor with {Caret=5};Equal(DictationTarget.AtCompletion(editor,returned),returned);
});
Test("dictation return preserves edits and different-field guards",()=>{
 var editor=new Target(1,"browser","compose","Hello","",true,Caret:5);
 foreach(var changed in new[]{editor with {Value="Changed"},editor with {Window=2},editor with {Field="search"},editor with {Editable=false}})
  Equal(DictationTarget.AtCompletion(editor,changed),editor);
});
Test("recreated dictation editor can move caret only with identical known text and identity",()=>{
 var editor=new Target(1,"browser","old","Hello","",true,Caret:5,StableField:"compose-id-and-bounds");
 var returned=editor with {Field="new",Caret=2};Equal(DictationTarget.AtCompletion(editor,returned),returned);
 Equal(DictationTarget.AtCompletion(editor with {Value=null},returned),editor with {Value=null});
 Equal(DictationTarget.AtCompletion(editor,returned with {StableField="other"}),editor);
});
Test("late dictation destination excludes password/read-only and Murmur",()=>{
 var empty=new Target(1,"browser","page",null,"",false);
 Equal(DictationTarget.AtCompletion(empty,new Target(2,"browser","password",null,"",false)),empty);
 Equal(DictationTarget.AtCompletion(empty,new Target(2,"Murmur","scratch","","",true)),empty);
});
Test("long cleanup sections retain every character including the final sentence",()=>{
 string input=string.Join(" ",Enumerable.Range(1,150).Select(i=>$"Keep item {i} and its details. Make no changes to the deadline."))+" End marker retained.";
 var sections=CleanupSections.Split(input);Equal(string.Concat(sections),input);Equal(sections.Count>1,true);Equal(sections.All(s=>s.Length<=900),true);Equal(sections[^1].EndsWith("End marker retained."),true);
});
Test("completed cleanup sections stay stable when more speech is appended",()=>{
 string prefix=string.Join(" ",Enumerable.Range(1,40).Select(i=>$"This is sentence {i} with its original details."));
 var before=CleanupSections.Split(prefix);var after=CleanupSections.Split(prefix+" More words follow at the end of the recording.");
 Equal(string.Join("|",before.Take(before.Count-1)),string.Join("|",after.Take(before.Count-1)));
});
Test("cleanup splitting does not cut an unbroken URL or lose empty text",()=>{
 string url="https://example.com/"+new string('x',1100);var sections=CleanupSections.Split("Read "+url+" then finish.");
 Equal(sections.Any(s=>s.Contains(url)),true);Equal(string.Concat(sections),"Read "+url+" then finish.");Equal(CleanupSections.Split("").Count,0);
});
Test("cleanup keeps nearby numeric self-correction in one section",()=>{
 string input=string.Join(" ",Enumerable.Repeat("Keep the notes.",51))+" Send 37 invoices. Actually make that 38 invoices. "+string.Join(" ",Enumerable.Repeat("Finish the remaining work.",30));
 var sections=CleanupSections.Split(input);Equal(string.Concat(sections),input);Equal(sections.Any(s=>s.Contains("37 invoices")&&s.Contains("38 invoices")),true);
});
Console.WriteLine($"{count-failures.Count}/{count} passed"); Environment.ExitCode = failures.Count == 0 ? 0 : 1;
