using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PromptDraft;

// Exercise native paste routes without reading or replacing the user's clipboard.
class FakeClipboardEditor:PromptTextBox {
    public string ClipboardText;
    public int Reads;
    protected override string ReadClipboardText(){Reads++;return ClipboardText;}
    public bool PasteKey(Keys keys){var msg=Message.Create(Handle,0x100,IntPtr.Zero,IntPtr.Zero);return ProcessCmdKey(ref msg,keys);}
}
class PasteTests {
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr w,IntPtr l);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static int LineCount(TextBox box){return SendMessage(box.Handle,0xBA,IntPtr.Zero,IntPtr.Zero).ToInt32();}
    static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);}
    static object Call(MainForm form,string name,params object[] args){return typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,args);}
    [STAThread] static int Main(string[] args){
        try{Run(args[0]);return 0;}catch(Exception ex){Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message);return 1;}
    }
    static void Run(string directory){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        const string markdown="# 标题 😊\n\n- 第一项\n  - 子项\n\n```python\n\tprint('你好')  \n```\n";
        string expected=markdown.Replace("\n","\r\n");
        using(var box=new FakeClipboardEditor{Multiline=true,WordWrap=false,MaxLength=0}){
            Check(PromptTextBox.NormalizeLineEndings("a\r\nb\nc\rd\n\n")=="a\r\nb\r\nc\r\nd\r\n\r\n","mixed LF, CRLF and CR preserve blank and trailing lines");
            box.Text=markdown;
            Check(box.Text==expected&&LineCount(box)==9,"saved LF Markdown displays native lines with Unicode, tabs and trailing spaces intact");
            box.Clear();box.ClearUndo();box.ClipboardText=markdown;
            Check(box.PasteKey(Keys.Control|Keys.V)&&box.Text==expected&&LineCount(box)==9,"Ctrl+V preserves Markdown lines");
            Check(box.CanUndo,"paste is undoable");box.Undo();Check(box.Text=="","one Undo removes the complete paste");
            box.Text="before SELECT after";box.Select(7,6);box.ClipboardText="甲\n乙";
            Check(box.PasteKey(Keys.Shift|Keys.Insert)&&box.Text=="before 甲\r\n乙 after","Shift+Insert replaces only the selection");
            Check(box.SelectionStart==11&&box.SelectionLength==0,"caret follows the normalized insertion");
            box.Undo();Check(box.Text=="before SELECT after","Undo restores the replaced selection");
            box.Text="";box.ClipboardText="one\r\ntwo\r\n\r\nthree";box.Paste();
            Check(box.Text==box.ClipboardText&&LineCount(box)==4,"native WM_PASTE used by the context menu does not double CRLF");
            box.SelectAll();box.ClipboardText="";box.Paste();Check(box.Text=="one\r\ntwo\r\n\r\nthree","empty/non-text clipboard keeps the selected text");
            box.ClipboardText="blocked\ntext";box.ReadOnly=true;int reads=box.Reads;box.Paste();box.PasteKey(Keys.Control|Keys.V);
            Check(box.Reads==reads&&box.Text=="one\r\ntwo\r\n\r\nthree","read-only trash rejects paste without reading the clipboard");
        }
        string html=MarkdownView.Render("第一行\n第二行\n\n另一段\n下一行");
        Check(html.Contains("第一行<br />\n第二行")&&html.Contains("另一段<br />\n下一行"),"preview preserves single line breaks and paragraph boundaries");
        string code=MarkdownView.Render("```text\na\nb\n```");
        Check(code.Contains("a\nb\n</code>")&&!code.Contains("<br"),"code blocks keep literal newlines without extra HTML breaks");
        string layout=MarkdownView.Render("- one\n- two\n\n| A | B |\n| --- | --- |\n| x | y |");
        Check(layout.Contains("<ul>")&&layout.Contains("<li>one</li>")&&layout.Contains("<table>"),"lists and tables still render structurally");
        foreach(string kind in new[]{"prompt","clip"}){
            var store=new DraftStore(Path.Combine(directory,kind+"-"+Guid.NewGuid().ToString("N")));
            var state=new DraftState();var old=new Draft{Kind=kind,Body=markdown};state.Drafts.Add(old);state.SelectedId=old.Id;store.Save(state);
            using(var form=new MainForm(store,store.Load())){
                var editor=Field<PromptTextBox>(form,"editor");editor.WordWrap=false;
                Check(editor.Text==expected&&LineCount(editor)==9,kind+": existing LF records display correctly");
                Check(store.Load().Drafts.Single().Body==markdown,kind+": opening leaves original stored content intact");
                editor.Select(editor.TextLength,0);editor.InsertPastedText("\n追加一行\n追加二行");Call(form,"SaveNow");
                string saved=expected+"\r\n追加一行\r\n追加二行";
                Check(store.Load().Drafts.Single().Body==saved,kind+": pasted line breaks survive autosave storage");
                using(var reopened=new MainForm(store,store.Load()))Check(Field<TextBox>(reopened,"editor").Text==saved,kind+": restart preserves edited Markdown exactly");
            }
        }
    }
}
