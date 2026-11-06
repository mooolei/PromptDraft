using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

[assembly: System.Reflection.AssemblyTitle("Prompt 草稿与收藏")]
[assembly: System.Reflection.AssemblyProduct("Prompt 草稿与收藏")]
[assembly: System.Reflection.AssemblyVersion("1.2.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.2.1.0")]

namespace PromptDraft {
    public class Draft {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Agent { get; set; }
        public string Body { get; set; }
        public bool Archived { get; set; }
        public bool Deleted { get; set; }
        public bool Pinned { get; set; }
        public string Kind { get; set; }
        public DateTime Updated { get; set; }
        public Draft() { Id = Guid.NewGuid().ToString("N"); Title = ""; Agent = ""; Body = ""; Kind = "prompt"; Updated = DateTime.Now; }
        public string DisplayTitle {
            get {
                if (!String.IsNullOrWhiteSpace(Title)) return Title;
                string first = (Body ?? "").Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return String.IsNullOrWhiteSpace(first) ? "未命名草稿" : first.Trim();
            }
        }
        public override string ToString() { return DisplayTitle; }
    }

    public static class MarkdownView {
        static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().UseTaskLists().UseEmphasisExtras().UseAutoLinks().DisableHtml().Build();
        public static string Render(string source) {
            var document = Markdown.Parse(source ?? "", Pipeline);
            // The preview is completely offline. Links are readable; images are shown as descriptions.
            foreach (var link in ((MarkdownObject)document).Descendants<LinkInline>()) {
                if (link.IsImage) { link.IsImage = false; }
                link.Url = "#";
                link.GetDynamicUrl = null;
            }
            string html = Markdown.ToHtml(document, Pipeline);
            return "<!doctype html><html><head><meta http-equiv='X-UA-Compatible' content='IE=edge'><meta charset='utf-8'><style>" +
                "body{font-family:'Microsoft YaHei UI','Segoe UI',sans-serif;color:#242b2a;background:white;font-size:15px;line-height:1.8;margin:24px;word-wrap:break-word;}" +
                "h1,h2,h3,h4{line-height:1.4;margin:1.2em 0 .6em;}h1{font-size:25px;}h2{font-size:21px;border-bottom:1px solid #e4e8e4;padding-bottom:8px;}h3{font-size:18px;}" +
                "p{margin:.7em 0;}pre{background:#f3f5f3;border:1px solid #e1e5e1;padding:14px;white-space:pre-wrap;word-wrap:break-word;}code{font-family:Consolas,'Microsoft YaHei UI',monospace;background:#f1f3f1;font-size:13px;}" +
                "blockquote{border-left:3px solid #9ab3a2;color:#637168;padding-left:15px;margin-left:0;}table{border-collapse:collapse;width:100%;margin:16px 0;}th,td{border:1px solid #dce2dc;padding:8px 11px;text-align:left;}th{background:#eff3ef;}" +
                "a{color:#2c5b48;text-decoration:none;}hr{border:0;border-top:1px solid #dce2dc;margin:24px 0;}li{margin:4px 0;}input{vertical-align:middle;}" +
                "</style></head><body>" + (String.IsNullOrWhiteSpace(source) ? "<p style='color:#8a928c'>把值得保留的 AI 回答粘贴到「Markdown 原文」，这里会自动排版。</p>" : html) + "</body></html>";
        }
    }
    public class DraftState {
        public int Version { get; set; }
        public string SelectedId { get; set; }
        public List<Draft> Drafts { get; set; }
        public DraftState() { Version = 1; Drafts = new List<Draft>(); }
    }
    public class DraftStore {
        public readonly string DirectoryPath;
        public string FilePath { get { return Path.Combine(DirectoryPath, "drafts.json"); } }
        public string RecoveryNotice = "";
        JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 100 }; }
        public DraftStore(string path) { DirectoryPath = Path.GetFullPath(path); }
        DraftState Read(string path) {
            var s = Json().Deserialize<DraftState>(File.ReadAllText(path, Encoding.UTF8));
            if (s == null || s.Version != 1 || s.Drafts == null || s.Drafts.Any(d => d == null || String.IsNullOrEmpty(d.Id)) || s.Drafts.Select(d => d.Id).Distinct().Count() != s.Drafts.Count) throw new InvalidDataException("草稿数据格式无法识别。");
            foreach (var d in s.Drafts) { d.Title = d.Title ?? ""; d.Agent = d.Agent ?? ""; d.Body = d.Body ?? ""; d.Updated = d.Updated.ToLocalTime(); }
            return s;
        }
        public DraftState Load() {
            Directory.CreateDirectory(DirectoryPath);
            if (!File.Exists(FilePath) && !File.Exists(FilePath + ".bak")) return new DraftState();
            try { return Read(FilePath); }
            catch (Exception ex) {
                if (File.Exists(FilePath + ".bak")) {
                    DraftState backup;
                    try { backup = Read(FilePath + ".bak"); }
                    catch { throw new IOException("草稿和备份均无法读取，请保留 data 文件夹以便恢复。", ex); }
                    if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".unreadable-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"));
                    RecoveryNotice = "上次的草稿文件无法读取，已载入上一份备份。原文件已保留在 data 文件夹。";
                    File.Copy(FilePath + ".bak", FilePath, true);
                    return backup;
                }
                throw new IOException("无法读取草稿。原文件未修改，请保留 data 文件夹。", ex);
            }
        }
        public void Save(DraftState state) {
            Directory.CreateDirectory(DirectoryPath);
            string temp = FilePath + ".tmp";
            byte[] bytes = new UTF8Encoding(false).GetBytes(Json().Serialize(state));
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
            else File.Move(temp, FilePath);
        }
    }

    static class Program {
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static void Main(string[] args) {
            SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string path = args.Length == 2 && args[0] == "--data-dir" ? args[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            string key; using (var hash = SHA256.Create()) key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()))).Replace("-", "");
            bool created;
            using(var mutex = new Mutex(true, "Local\\PromptDraft-" + key, out created)) {
                if (!created) { MessageBox.Show("Prompt 草稿已经打开，请在任务栏切换到它。", "Prompt 草稿"); return; }
                try { var store = new DraftStore(path); Application.Run(new MainForm(store, store.Load())); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Prompt 草稿无法启动", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
    }
}
