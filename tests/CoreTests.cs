using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using PromptDraft;
class CoreTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static void Main(string[] args){
        string markdown="# 中文标题 😊\n\n**重点**\n\n| 列 A | 列 B |\n| --- | --- |\n| 本地 | 保存 |\n\n```text\n多行代码\n```";
        string html=MarkdownView.Render(markdown);
        Check(html.Contains("<h1>")&&html.Contains("<strong>")&&html.Contains("<table>")&&html.Contains("<pre>"),"Markdown formatting supports headings, emphasis, tables and code");
        string safe=MarkdownView.Render("<script>alert(1)</script>\n![image](https://example.com/test.png)\n[link](javascript:alert%281%29)");
        Check(!safe.Contains("<script>")&&!safe.Contains("<img")&&!safe.Contains("href=\"javascript:"),"preview does not emit executable HTML or remote images");
        string clip=RichClipboard.HtmlFormat(html);byte[] bytes=Encoding.UTF8.GetBytes(clip);
        int start=Int32.Parse(Regex.Match(clip,@"StartFragment:(\d+)").Groups[1].Value),end=Int32.Parse(Regex.Match(clip,@"EndFragment:(\d+)").Groups[1].Value);
        string fragment=Encoding.UTF8.GetString(bytes,start,end-start);
        Check(fragment.StartsWith("<h1>")&&fragment.Contains("中文标题 😊")&&fragment.Contains("<table>"),"HTML clipboard offsets count UTF-8 bytes");
        string dir=Path.Combine(args[0],Guid.NewGuid().ToString("N"));var store=new ViewStateStore(dir);var state=new WindowViewState{HasBounds=true,X=50,Y=60,Width=1000,Height=740,Maximized=true};
        var doc=state.For("example");doc.Caret=83;doc.SelectionLength=4;doc.FirstVisibleChar=60;doc.PreviewScrollY=500;doc.Mode="split";doc.SplitRatio=0.6;
        Check(store.Save(state),"view preferences save");var loaded=new ViewStateStore(dir).Load();
        Check(loaded.Maximized&&loaded.Width==1000&&loaded.For("example").Caret==83&&loaded.For("example").Mode=="split"&&loaded.For("example").PreviewScrollY==500,"window and per-document positions persist");
        var bounds=ViewStateStore.VisibleBounds(new WindowViewState{X=9999,Y=-9999,Width=2000,Height=2000},new[]{new Rectangle(0,0,1366,728)},new Size(940,650));
        Check(new Rectangle(0,0,1366,728).Contains(bounds),"off-screen window fits visible work area");
    }
}
