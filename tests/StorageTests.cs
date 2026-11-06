using System;
using System.IO;
using System.Linq;
using PromptDraft;
class StorageTests {
    static void Check(bool result, string message) { if (!result) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static void Main(string[] args) { try { Run(args); } catch (Exception ex) { Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message); Environment.ExitCode = 1; } }
    static void Run(string[] args) {
        string dir = Path.Combine(args[0], Guid.NewGuid().ToString("N"));
        var store = new DraftStore(dir); var s = store.Load();
        Check(s.Drafts.Count == 0, "new store is empty");
        var first = new Draft { Title = "中文草稿", Agent = "agent A", Body = "第一行\r\n第二行 — emoji 😊\r\n  保留空格\t结束" };
        s.Drafts.Add(first); s.SelectedId = first.Id; store.Save(s);
        var read = store.Load();
        Check(read.Drafts[0].Body == first.Body && read.SelectedId == first.Id, "Unicode, whitespace, selection round-trip");
        first.Body += "\r\n新增文本"; first.Archived = true;
        s.Drafts.Add(new Draft { Agent = "agent B", Body = new string('长', 100000) }); store.Save(s);
        read = store.Load();
        Check(read.Drafts.Count == 2 && read.Drafts[0].Archived && read.Drafts[1].Body.Length == 100000, "independent drafts, archive, long text");
        Check(File.Exists(store.FilePath + ".bak"), "backup exists after atomic replacement");
        File.WriteAllText(store.FilePath, "{invalid"); read = store.Load();
        Check(read.Drafts.Count == 1 && read.Drafts[0].Body != first.Body && store.RecoveryNotice.Length > 0, "corruption restores previous valid backup");
        Check(Directory.GetFiles(dir, "*.unreadable-*").Length == 1, "corrupt original preserved");
        string badDir = dir + "-invalid"; Directory.CreateDirectory(badDir); File.WriteAllText(Path.Combine(badDir, "drafts.json"), "bad-data");
        bool refused = false; try { new DraftStore(badDir).Load(); } catch (IOException) { refused = true; }
        Check(refused && File.ReadAllText(Path.Combine(badDir, "drafts.json")) == "bad-data", "unrecoverable data never silently overwritten");
    }
}
