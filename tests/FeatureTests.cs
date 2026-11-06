using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PromptDraft;
class FeatureTests {
    static void Check(bool ok, string name) { if(!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static T Field<T>(MainForm form, string name) { return (T)typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form); }
    static object Call(MainForm form, string name, params object[] args) { return typeof(MainForm).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, args); }
    [STAThread] static void Main(string[] args) {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        var store = new DraftStore(Path.Combine(args[0], Guid.NewGuid().ToString("N"))); var state = store.Load();
        using(var form = new MainForm(store, state)) {
            var body = Field<TextBox>(form, "editor"); var agent = Field<TextBox>(form, "agentBox");
            body.Text = "任务 A\r\n保留中文、缩进  和 emoji 😊"; agent.Text = "agent A";
            string firstId = Field<Draft>(form, "current").Id; string firstBody = body.Text;
            Call(form, "NewDraft"); Check(agent.Text == "agent A" && body.Text == "", "new draft inherits agent without mixing content");
            body.Text = "任务 B"; Call(form, "RefreshList", firstId);
            Check(body.Text == firstBody, "switching restores original draft exactly");
            Call(form, "CopyBody"); Check(Clipboard.GetText() == firstBody, "copy returns exact multiline Unicode body");
            Call(form, "DeleteCurrent"); Check(state.Drafts.Single(d => d.Id == firstId).Deleted, "delete moves draft to trash");
            Field<Button>(form, "trashTab").PerformClick();
            // Hidden control buttons do not invoke in all WinForms versions; select the trash state directly for the restore flow.
            typeof(MainForm).GetField("showTrash", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, true);
            Call(form, "RefreshList", firstId); Check(body.ReadOnly, "trash is read-only");
            Call(form, "ArchiveCurrent"); Check(!state.Drafts.Single(d => d.Id == firstId).Deleted, "restore preserves draft");
            Call(form, "SwitchCategory", "clip"); Call(form, "NewDraft");
            body.Text = "# 收藏标题\r\n\r\n**重点** 与 `code`\r\n\r\n| 项目 | 状态 |\r\n| --- | --- |\r\n| 本地 | 完成 |\r\n\r\n```python\r\nprint('你好')\r\n```\r\n\r\n> 引用\r\n\r\n- [x] 已完成\r\n- 普通列表";
            string clipBody = body.Text; string clipId = Field<Draft>(form,"current").Id;
            Check(Field<SplitContainer>(form,"editorSplit").Panel1Collapsed == false, "collection exposes Markdown editor and preview");
            Call(form, "SaveNow");
            var reloaded = store.Load(); Check(reloaded.Drafts.Single(d => d.Id == clipId).Body == clipBody && reloaded.Drafts.Single(d => d.Id == clipId).Kind == "clip", "collection persists separately with exact Markdown source");
            string html = MarkdownView.Render(clipBody);
            Check(html.Contains("<h1>") && html.Contains("<strong>") && html.Contains("<table>") && html.Contains("<pre>") && html.Contains("<blockquote>") && html.Contains("checkbox"), "headings, emphasis, tables, fenced code, quotes, tasks render");
            string hostile = MarkdownView.Render("<script>alert(1)</script>\n![remote](https://example.com/pixel)\n[x](javascript:alert%281%29)");
            Check(!hostile.Contains("<script>") && !hostile.Contains("<img") && !hostile.Contains("href=\"javascript:"), "preview disables executable HTML and remote resources");
            using(var reopened = new MainForm(store, reloaded)) Check(Field<Draft>(reopened,"current").Id == clipId, "restart restores collection and selection");
            Call(form,"ArchiveCurrent");
            // Re-select the archived collection to verify startup restoration of archived records.
            Call(form,"SetArchiveView",true); Call(form,"SaveNow");
            using(var reopened = new MainForm(store,store.Load())) Check(Field<Draft>(reopened,"current").Id == clipId, "restart restores archived collection");
            File.WriteAllText(Path.Combine(args[0],"preview-test.html"), html);
        }
    }
}
