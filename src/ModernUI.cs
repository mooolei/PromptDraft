using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace PromptDraft {
    static class Palette {
        public static readonly Color Ink=Color.FromArgb(35,48,43), Muted=Color.FromArgb(119,130,122), Green=Color.FromArgb(44,91,72), Tint=Color.FromArgb(229,238,230), Line=Color.FromArgb(226,231,225), Canvas=Color.FromArgb(248,249,246), Side=Color.FromArgb(240,243,237);
        public static GraphicsPath Round(Rectangle r,int radius) {
            var p=new GraphicsPath(); int d=Math.Min(radius*2,Math.Min(r.Width,r.Height));
            if(d<2){p.AddRectangle(r);return p;}
            p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
        }
    }
    class SoftButton:Button {
        public bool Primary, Selected;
        bool hovered;
        public SoftButton() { FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true); }
        protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e) {
            Control ancestor=Parent;while(ancestor!=null&&ancestor.BackColor.A<255)ancestor=ancestor.Parent;
            e.Graphics.Clear(ancestor==null?Palette.Canvas:ancestor.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            Color bg=Primary?Palette.Green:Selected?Palette.Tint:hovered?Color.FromArgb(233,238,231):BackColor;
            if(Primary&&hovered)bg=Color.FromArgb(35,77,59);
            using(var path=Palette.Round(new Rectangle(0,0,Width-1,Height-1),8)) {
                using(var brush=new SolidBrush(bg)) e.Graphics.FillPath(brush,path);
                if(!Primary&&!Selected)using(var pen=new Pen(Palette.Line))e.Graphics.DrawPath(pen,path);
            }
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,!Enabled?Palette.Muted:Primary?Color.White:Selected?Palette.Green:ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5));
        }
    }
    class Surface:Panel {
        public Surface(){BackColor=Color.White;Padding=new Padding(14);DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var p=new Pen(Palette.Line))e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);}
    }
    public static class RichClipboard {
        public static string HtmlFormat(string html) {
            const string start="<!--StartFragment-->", end="<!--EndFragment-->";
            int a=html.IndexOf("<body>",StringComparison.OrdinalIgnoreCase), b=html.LastIndexOf("</body>",StringComparison.OrdinalIgnoreCase);
            if(a<0||b<a)throw new ArgumentException("HTML body missing");
            html=html.Insert(b,end).Insert(a+6,start);
            const string template="Version:1.0\r\nStartHTML:{0:0000000000}\r\nEndHTML:{1:0000000000}\r\nStartFragment:{2:0000000000}\r\nEndFragment:{3:0000000000}\r\n";
            int header=Encoding.UTF8.GetByteCount(String.Format(template,0,0,0,0));
            int sf=header+Encoding.UTF8.GetByteCount(html.Substring(0,html.IndexOf(start,StringComparison.Ordinal)+start.Length));
            int ef=header+Encoding.UTF8.GetByteCount(html.Substring(0,html.IndexOf(end,StringComparison.Ordinal)));
            return String.Format(template,header,header+Encoding.UTF8.GetByteCount(html),sf,ef)+html;
        }
        public static void Set(string html,string plain) { var data=new DataObject();data.SetData(DataFormats.UnicodeText,true,plain);data.SetData(DataFormats.Html,HtmlFormat(html));Clipboard.SetDataObject(data,true,5,80); }
    }
    public class MainForm:Form {
        readonly DraftStore store; readonly DraftState data;
        readonly ViewStateStore viewStore; readonly WindowViewState viewState;
        TextBox search,titleBox,agentBox,editor;
        ListBox list; Label status,count,empty,sectionTitle,sectionHint,bodyLabel,listCount;
        SoftButton archive,copy,copyRaw,activeTab,archiveTab,trashTab,delete,add,promptCategory,clipCategory,readView,editView,splitView,pin;
        Panel content,sourceHost,previewHost,viewTools;
        SplitContainer editorSplit;
        WebBrowser preview;
        ContextMenuStrip previewMenu;
        readonly System.Windows.Forms.Timer saveTimer=new System.Windows.Forms.Timer {Interval=400};
        readonly System.Windows.Forms.Timer copyTimer=new System.Windows.Forms.Timer {Interval=1600};
        readonly System.Windows.Forms.Timer previewTimer=new System.Windows.Forms.Timer {Interval=450};
        readonly System.Windows.Forms.Timer viewTimer=new System.Windows.Forms.Timer {Interval=1200};
        Draft current; bool binding,refreshing,dirty,showArchived,showTrash,previewReady;
        string kind="prompt",viewMode="edit",renderedSource=null,renderedId=null,renderKey="",renderedHtml="";
        bool windowReady;

        public MainForm(DraftStore storage,DraftState state) {
            store=storage;data=state;Text="Prompt 草稿与收藏";StartPosition=FormStartPosition.CenterScreen;
            viewStore=new ViewStateStore(store.DirectoryPath);viewState=viewStore.Load();
            ClientSize=new Size(1180,790);MinimumSize=new Size(940,650);AutoScaleMode=AutoScaleMode.Dpi;
            Font=new Font("Microsoft YaHei UI",10);ForeColor=Palette.Ink;BackColor=Palette.Canvas;KeyPreview=true;ShowIcon=true;ShowInTaskbar=true;
            using(var iconStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PromptDraft.App.ico")) if(iconStream!=null)using(var loadedIcon=new Icon(iconStream))Icon=(Icon)loadedIcon.Clone();
            BuildUI();
            if(viewState.HasBounds){StartPosition=FormStartPosition.Manual;Bounds=ViewStateStore.VisibleBounds(viewState,Screen.AllScreens.Select(s=>s.WorkingArea).ToArray(),MinimumSize);if(viewState.Maximized)WindowState=FormWindowState.Maximized;}
            else{Rectangle area=Screen.FromPoint(Cursor.Position).WorkingArea;var initial=new WindowViewState{X=area.Left+Math.Max(0,(area.Width-Width)/2),Y=area.Top+Math.Max(0,(area.Height-Height)/2),Width=Width,Height=Height};StartPosition=FormStartPosition.Manual;Bounds=ViewStateStore.VisibleBounds(initial,new[]{area},MinimumSize);}
            saveTimer.Tick+=delegate{saveTimer.Stop();SaveNow();};previewTimer.Tick+=delegate{previewTimer.Stop();RenderPreview();};
            viewTimer.Tick+=delegate{SaveViewState();};ResizeEnd+=delegate{SaveViewState();};Deactivate+=delegate{if(windowReady)SaveViewState();};
            Resize+=delegate{if(windowReady&&WindowState!=FormWindowState.Minimized)viewState.Maximized=WindowState==FormWindowState.Maximized;};
            copyTimer.Tick+=delegate{copyTimer.Stop();UpdateCopyButtons();};
            FormClosing+=delegate(object s,FormClosingEventArgs e){SaveViewState();if(!SaveNow()){e.Cancel=true;MessageBox.Show(this,"保存未成功，窗口将保持打开。请检查文件夹权限或磁盘空间，再按 Ctrl+S 重试。\n\n"+store.FilePath,"草稿尚未保存",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
            FormClosed+=delegate{viewTimer.Dispose();saveTimer.Dispose();previewTimer.Dispose();copyTimer.Dispose();};
            if(data.Drafts.Count==0){var d=new Draft();data.Drafts.Add(d);data.SelectedId=d.Id;dirty=true;}
            var selected=data.Drafts.FirstOrDefault(d=>d.Id==data.SelectedId)??data.Drafts[0];
            kind=selected.Kind??"prompt";showArchived=selected.Archived;showTrash=selected.Deleted;RefreshList(selected.Id);
            Shown+=delegate{windowReady=true;if(viewMode=="read")preview.Focus();else editor.Focus();RestoreDraftPosition();viewTimer.Start();SaveNow();if(store.RecoveryNotice.Length>0)MessageBox.Show(this,store.RecoveryNotice,"已恢复备份");};
        }
        SoftButton Btn(string text,bool primary=false) {return new SoftButton{Text=text,Primary=primary,Height=38,BackColor=Color.White,ForeColor=Palette.Ink,Margin=new Padding(0),Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,9.5F)};}
        Label L(string text,float size,Color color){return new Label{Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=color,Font=new Font(Font.FontFamily,size),Margin=new Padding(0)};}
        TextBox Input(string name){return new TextBox{BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,AccessibleName=name,Font=new Font(Font.FontFamily,11),ForeColor=Palette.Ink,BackColor=Color.White,MaxLength=0};}
        TableLayoutPanel Grid(int columns,int rows){var t=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=columns,RowCount=rows,Margin=new Padding(0),BackColor=Color.Transparent};if(columns==1)t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));if(rows==1)t.RowStyles.Add(new RowStyle(SizeType.Percent,100));return t;}
        Control Frame(Control control,Padding padding){var p=new Surface{Dock=DockStyle.Fill,Padding=padding,Margin=new Padding(0)};p.Controls.Add(control);return p;}
        void BuildUI() {
            var root=Grid(2,1);root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,280));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(root);
            var side=Grid(1,9);side.BackColor=Palette.Side;side.Padding=new Padding(20,24,20,16);
            foreach(int h in new[]{64,44,62,27,42,56})side.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
            side.RowStyles.Add(new RowStyle(SizeType.Percent,100));side.RowStyles.Add(new RowStyle(SizeType.Absolute,30));side.RowStyles.Add(new RowStyle(SizeType.Absolute,27));root.Controls.Add(side,0,0);
            var brand=Grid(2,1);brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            var logo=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.CenterImage};
            using(var imageStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("PromptDraft.App.png"))if(imageStream!=null)using(var loadedImage=Image.FromStream(imageStream))logo.Image=new Bitmap(loadedImage,new Size(32,32));
            brand.Controls.Add(logo,0,0);
            var brandText=Grid(1,2);brandText.RowStyles.Add(new RowStyle(SizeType.Absolute,33));brandText.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            brandText.Controls.Add(L("Prompt 草稿",16,Palette.Ink),0,0);brandText.Controls.Add(L("思路先放这里",9,Palette.Muted),0,1);brand.Controls.Add(brandText,1,0);side.Controls.Add(brand,0,0);
            var categories=Grid(2,1);categories.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));categories.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            promptCategory=Btn("Prompt 草稿");clipCategory=Btn("AI 收藏");clipCategory.Margin=new Padding(6,0,0,0);
            promptCategory.Click+=delegate{SwitchCategory("prompt");};clipCategory.Click+=delegate{SwitchCategory("clip");};categories.Controls.Add(promptCategory,0,0);categories.Controls.Add(clipCategory,1,0);side.Controls.Add(categories,0,1);
            add=Btn("＋  新建草稿",true);add.Margin=new Padding(0,14,0,6);add.Click+=delegate{NewDraft();};side.Controls.Add(add,0,2);
            side.Controls.Add(L("搜索标题、正文或 Agent",9,Palette.Muted),0,3);
            search=Input("搜索草稿与收藏");search.TextChanged+=delegate{RefreshList(current==null?null:current.Id);};side.Controls.Add(Frame(search,new Padding(12,10,12,7)),0,4);
            var filters=Grid(3,1);for(int i=0;i<3;i++)filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.33F));filters.Padding=new Padding(0,13,0,9);
            activeTab=Btn("草稿");archiveTab=Btn("已归档");trashTab=Btn("回收站");archiveTab.Margin=new Padding(4,0,4,0);
            activeTab.Click+=delegate{SetArchiveView(false);};archiveTab.Click+=delegate{SetArchiveView(true);};trashTab.Click+=delegate{showTrash=true;RefreshList(null);};
            filters.Controls.Add(activeTab,0,0);filters.Controls.Add(archiveTab,1,0);filters.Controls.Add(trashTab,2,0);side.Controls.Add(filters,0,5);
            list=new ListBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=Palette.Side,IntegralHeight=false,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=92,AccessibleName="草稿列表",Margin=new Padding(0)};
            list.DrawItem+=DrawDraft;list.SelectedIndexChanged+=delegate{if(!refreshing)SelectDraft(list.SelectedItem as Draft);};side.Controls.Add(list,0,6);
            listCount=L("",9,Palette.Muted);side.Controls.Add(listCount,0,7);side.Controls.Add(L("●  仅保存在这台电脑",9,Palette.Green),0,8);
            var right=Grid(1,3);right.Padding=new Padding(30,24,30,14);right.RowStyles.Add(new RowStyle(SizeType.Absolute,66));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));right.RowStyles.Add(new RowStyle(SizeType.Absolute,35));root.Controls.Add(right,1,0);
            var heading=Grid(1,2);heading.RowStyles.Add(new RowStyle(SizeType.Absolute,33));heading.RowStyles.Add(new RowStyle(SizeType.Percent,100));sectionTitle=L("Prompt 草稿",17,Palette.Ink);sectionHint=L("写好，再复制。",9.5F,Palette.Muted);heading.Controls.Add(sectionTitle,0,0);heading.Controls.Add(sectionHint,0,1);right.Controls.Add(heading,0,0);
            var bodyHost=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};right.Controls.Add(bodyHost,0,1);content=new Panel{Dock=DockStyle.Fill};bodyHost.Controls.Add(content);
            var layout=Grid(1,5);layout.RowStyles.Add(new RowStyle(SizeType.Absolute,63));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,53));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,49));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));content.Controls.Add(layout);
            var titleRow=Grid(2,1);titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));titleRow.Padding=new Padding(0,0,0,9);
            titleBox=Input("草稿标题");titleBox.Font=new Font(Font.FontFamily,17,FontStyle.Bold);var titleFrame=Frame(titleBox,new Padding(16,13,16,11));titleRow.Controls.Add(titleFrame,0,0);
            pin=Btn("☆  置顶");pin.Margin=new Padding(10,6,0,6);pin.Click+=delegate{TogglePin();};titleRow.Controls.Add(pin,1,0);layout.Controls.Add(titleRow,0,0);
            var agentRow=Grid(2,1);agentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,106));agentRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));agentRow.Padding=new Padding(0,2,0,9);agentRow.Controls.Add(L("Agent / 来源",9.5F,Palette.Muted),0,0);agentBox=Input("Agent 或来源名称");agentRow.Controls.Add(Frame(agentBox,new Padding(12,9,12,5)),1,0);layout.Controls.Add(agentRow,0,1);
            var editorBar=Grid(2,1);editorBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));editorBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,228));editorBar.Padding=new Padding(0,5,0,9);bodyLabel=L("正文",10,Palette.Muted);editorBar.Controls.Add(bodyLabel,0,0);
            viewTools=new Panel{Dock=DockStyle.Fill};var modes=Grid(3,1);for(int i=0;i<3;i++)modes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.33F));readView=Btn("阅读");editView=Btn("编辑");splitView=Btn("对照");editView.Margin=new Padding(4,0,4,0);readView.Click+=delegate{SetView("read");};editView.Click+=delegate{SetView("edit");};splitView.Click+=delegate{SetView("split");};modes.Controls.Add(readView,0,0);modes.Controls.Add(editView,1,0);modes.Controls.Add(splitView,2,0);viewTools.Controls.Add(modes);editorBar.Controls.Add(viewTools,1,0);layout.Controls.Add(editorBar,0,2);
            editorSplit=new SplitContainer{Dock=DockStyle.Fill,Margin=new Padding(0),BackColor=Palette.Canvas,SplitterWidth=14,Panel1MinSize=0,Panel2MinSize=0};layout.Controls.Add(editorSplit,0,3);
            sourceHost=new Panel{Dock=DockStyle.Fill};previewHost=new Panel{Dock=DockStyle.Fill};editorSplit.Panel1.Controls.Add(sourceHost);editorSplit.Panel2.Controls.Add(previewHost);
            editor=Input("草稿正文");editor.Multiline=true;editor.AcceptsReturn=true;editor.AcceptsTab=true;editor.ScrollBars=ScrollBars.Vertical;editor.WordWrap=true;editor.Font=new Font(Font.FontFamily,12);editor.HideSelection=false;sourceHost.Controls.Add(Frame(editor,new Padding(20,18,10,18)));
            preview=new WebBrowser{Dock=DockStyle.Fill,AllowWebBrowserDrop=false,IsWebBrowserContextMenuEnabled=false,ScriptErrorsSuppressed=true,WebBrowserShortcutsEnabled=true};
            preview.Navigating+=delegate(object s,WebBrowserNavigatingEventArgs e){if(e.Url.AbsoluteUri!="about:blank")e.Cancel=true;};preview.NewWindow+=delegate(object s,System.ComponentModel.CancelEventArgs e){e.Cancel=true;};preview.DocumentCompleted+=PreviewCompleted;
            previewHost.Controls.Add(Frame(preview,new Padding(1)));
            previewMenu=new ContextMenuStrip();previewMenu.Items.Add("复制所选   Ctrl+C",null,delegate{CopySelection();});previewMenu.Items.Add("全选   Ctrl+A",null,delegate{if(preview.Document!=null)preview.Document.ExecCommand("SelectAll",false,null);});previewMenu.Items.Add(new ToolStripSeparator());previewMenu.Items.Add("复制全部排版",null,delegate{CopyFormatted();});
            var actions=Grid(5,1);actions.Padding=new Padding(0,16,0,0);actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,86));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,132));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,136));
            archive=Btn("归档");delete=Btn("删除");delete.Margin=new Padding(8,0,0,0);archive.Click+=delegate{ArchiveCurrent();};delete.Click+=delegate{DeleteCurrent();};copyRaw=Btn("复制 Markdown");copyRaw.Click+=delegate{CopyBody();};copy=Btn("复制全文",true);copy.Margin=new Padding(10,0,0,0);copy.Click+=delegate{CopyPrimary();};actions.Controls.Add(archive,0,0);actions.Controls.Add(delete,1,0);actions.Controls.Add(copyRaw,3,0);actions.Controls.Add(copy,4,0);layout.Controls.Add(actions,0,4);
            var footer=Grid(2,1);footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));status=L("已保存在本地",9,Palette.Muted);count=L("0 字符",9,Palette.Muted);count.TextAlign=ContentAlignment.MiddleRight;footer.Controls.Add(status,0,0);footer.Controls.Add(count,1,0);right.Controls.Add(footer,0,2);
            empty=L("这里还没有内容\n\n新建一条，或试试其他搜索词。",13,Palette.Muted);empty.TextAlign=ContentAlignment.MiddleCenter;empty.Visible=false;bodyHost.Controls.Add(empty);
            titleBox.TextChanged+=Changed;agentBox.TextChanged+=Changed;editor.TextChanged+=Changed;
            var tips=new ToolTip();tips.SetToolTip(titleBox,"标题可留空，列表会自动使用正文首行");tips.SetToolTip(status,store.FilePath);tips.SetToolTip(copy,"Ctrl+Shift+C：草稿复制全文，收藏复制排版");tips.SetToolTip(copyRaw,"复制原始 Markdown，保留所有标记");tips.SetToolTip(add,"Ctrl+N 新建；Ctrl+F 搜索");
        }
        void SwitchCategory(string value){kind=value;showArchived=false;showTrash=false;search.Text="";RefreshList(null);}
        void SetView(string mode) {
            if(!binding)CaptureDraftView();
            bool clip=current!=null&&current.Kind=="clip";viewMode=clip?mode:"edit";
            editorSplit.Panel1Collapsed=false;editorSplit.Panel2Collapsed=false;
            if(viewMode=="read")editorSplit.Panel1Collapsed=true;
            else if(viewMode=="edit")editorSplit.Panel2Collapsed=true;
            else if(editorSplit.Width>100){double ratio=current==null?0.5:viewState.For(current.Id).SplitRatio;if(ratio<0.15||ratio>0.85)ratio=0.5;editorSplit.SplitterDistance=(int)((editorSplit.Width-editorSplit.SplitterWidth)*ratio);}
            readView.Selected=viewMode=="read";editView.Selected=viewMode=="edit";splitView.Selected=viewMode=="split";viewTools.Visible=clip;
            readView.Invalidate();editView.Invalidate();splitView.Invalidate();
            bodyLabel.Text=!clip?"正文":viewMode=="read"?"阅读预览 · 可选中复制":viewMode=="split"?"Markdown 编辑 / 实时预览":"Markdown 原文";
            if(clip)RenderPreview();
            if(current!=null)viewState.For(current.Id).Mode=viewMode;
            RestoreDraftPosition();
        }
        void DrawDraft(object sender,DrawItemEventArgs e) {
            if(e.Index<0)return;var d=(Draft)list.Items[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;
            // Owner-drawn rows share the list's drawing surface. Clear only this row.
            using(var background=new SolidBrush(Palette.Side))e.Graphics.FillRectangle(background,e.Bounds);
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var card=new Rectangle(e.Bounds.X,e.Bounds.Y+3,e.Bounds.Width-1,e.Bounds.Height-6);
            using(var path=Palette.Round(card,9))using(var brush=new SolidBrush(selected?Palette.Tint:Palette.Side))e.Graphics.FillPath(brush,path);
            if(selected)using(var p=new Pen(Palette.Green,3))e.Graphics.DrawLine(p,card.Left+2,card.Top+15,card.Left+2,card.Bottom-15);
            Rectangle r=e.Bounds;r.Inflate(-13,-10);var flags=TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix;
            string title=d.DisplayTitle;if(d.Kind=="clip")title=title.TrimStart('#',' ');
            using(var f=new Font(Font.FontFamily,10.5F,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,title,f,new Rectangle(r.X,r.Y,r.Width-(d.Pinned?22:0),23),Palette.Ink,flags);
            if(d.Pinned)TextRenderer.DrawText(e.Graphics,"★",Font,new Rectangle(r.Right-22,r.Y,22,23),Palette.Green,flags);
            string excerpt=d.Body.Replace("\r"," ").Replace("\n"," ");TextRenderer.DrawText(e.Graphics,String.IsNullOrWhiteSpace(excerpt)?"开始写下你的想法…":excerpt,Font,new Rectangle(r.X,r.Y+26,r.Width,22),Palette.Muted,flags);
            using(var f=new Font(Font.FontFamily,8.5F))TextRenderer.DrawText(e.Graphics,(String.IsNullOrWhiteSpace(d.Agent)?"未分组":d.Agent)+" · "+d.Updated.ToString("MM-dd HH:mm"),f,new Rectangle(r.X,r.Y+52,r.Width,19),Palette.Muted,flags);
        }
        void RefreshList(string selectId) {
            refreshing=true;list.BeginUpdate();list.Items.Clear();string q=search.Text.Trim();
            foreach(var d in data.Drafts.Where(d=>(d.Kind??"prompt")==kind&&(showTrash?d.Deleted:!d.Deleted&&d.Archived==showArchived)&&(q.Length==0||(d.Title+"\n"+d.Agent+"\n"+d.Body).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)).OrderByDescending(d=>d.Pinned).ThenByDescending(d=>d.Updated))list.Items.Add(d);
            list.SelectedItem=list.Items.Cast<Draft>().FirstOrDefault(d=>d.Id==selectId);if(list.SelectedIndex<0&&list.Items.Count>0)list.SelectedIndex=0;list.EndUpdate();refreshing=false;
            activeTab.Selected=!showArchived&&!showTrash;archiveTab.Selected=showArchived&&!showTrash;trashTab.Selected=showTrash;promptCategory.Selected=kind=="prompt";clipCategory.Selected=kind=="clip";
            foreach(var b in new[]{activeTab,archiveTab,trashTab,promptCategory,clipCategory})b.Invalidate();
            activeTab.Text=kind=="clip"?"收藏":"草稿";add.Text=kind=="clip"?"＋  新建收藏":"＋  新建草稿";sectionTitle.Text=kind=="clip"?"AI 收藏":"Prompt 草稿";
            sectionHint.Text=kind=="clip"?"留下值得再读的回答，随时复制使用。":"写好，再复制。每段思路都有自己的位置。";
            listCount.Text=list.Items.Count+" 条"+(showTrash?" · 回收站":showArchived?" · 已归档":"");SelectDraft(list.SelectedItem as Draft);
        }
        void SelectDraft(Draft d) {
            if(current==d&&d!=null)return;CaptureDraftView();viewStore.Save(viewState);
            current=d;binding=true;titleBox.Text=d==null?"":d.Title;agentBox.Text=d==null?"":d.Agent;editor.Text=d==null?"":d.Body;content.Visible=d!=null;empty.Visible=d==null;
            if(d!=null){archive.Text=d.Deleted?"恢复":d.Archived?"取消归档":"归档";delete.Text=d.Deleted?"彻底删除":"删除";titleBox.ReadOnly=agentBox.ReadOnly=editor.ReadOnly=d.Deleted;UpdatePin();string savedMode=viewState.For(d.Id).Mode;SetView(savedMode=="read"||savedMode=="edit"||savedMode=="split"?savedMode:d.Kind=="clip"&&d.Body.Length>0?"read":d.Kind=="clip"?"split":"edit");if(data.SelectedId!=d.Id){data.SelectedId=d.Id;dirty=true;saveTimer.Start();}}
            binding=false;UpdateCount();UpdateCopyButtons();
        }
        void UpdateCount(){count.Text=new System.Globalization.StringInfo(editor.Text).LengthInTextElements.ToString("N0")+" 字符";}
        void UpdatePin(){pin.Text=current!=null&&current.Pinned?"★  已置顶":"☆  置顶";pin.Selected=current!=null&&current.Pinned;pin.Enabled=current!=null&&!current.Deleted;pin.Invalidate();}
        void TogglePin(){if(current==null||current.Deleted)return;current.Pinned=!current.Pinned;dirty=true;UpdatePin();RefreshList(current.Id);SaveNow();}
        void UpdateCopyButtons(){bool clip=current!=null&&current.Kind=="clip";copyRaw.Visible=clip;copy.Text=clip?"复制排版":"复制全文";copyRaw.Text="复制 Markdown";copy.Enabled=editor.TextLength>0&&(!clip||previewReady);copyRaw.Enabled=editor.TextLength>0;}
        void Changed(object sender,EventArgs e) {
            if(binding||current==null)return;current.Title=titleBox.Text;current.Agent=agentBox.Text;current.Body=editor.Text;current.Updated=DateTime.Now;dirty=true;status.Text="正在保存…";status.ForeColor=Palette.Muted;saveTimer.Stop();saveTimer.Start();list.Invalidate();UpdateCount();
            if(sender==editor&&current.Kind=="clip"){previewReady=false;previewTimer.Stop();previewTimer.Start();}UpdateCopyButtons();
        }
        bool SaveNow(){if(windowReady)SaveViewState();if(!dirty)return true;try{store.Save(data);dirty=false;status.ForeColor=Palette.Muted;status.Text="●  已保存在本地  "+DateTime.Now.ToString("HH:mm:ss");return true;}catch{status.ForeColor=Color.Firebrick;status.Text="保存失败 · Ctrl+S 重试";return false;}}
        void NewDraft(){string agent=current==null?"":current.Agent;var d=new Draft{Agent=agent,Kind=kind};data.Drafts.Add(d);dirty=true;showArchived=false;showTrash=false;search.Text="";RefreshList(d.Id);SaveNow();SetView(kind=="clip"?"split":"edit");editor.Focus();}
        void SetArchiveView(bool value){showArchived=value;showTrash=false;RefreshList(current==null?null:current.Id);}
        void ArchiveCurrent(){if(current==null)return;if(current.Deleted)current.Deleted=false;else current.Archived=!current.Archived;dirty=true;RefreshList(null);SaveNow();}
        void DeleteCurrent(){if(current==null)return;if(current.Deleted){if(MessageBox.Show(this,"彻底删除这条内容？删除后无法从回收站恢复。","彻底删除",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;data.Drafts.Remove(current);viewState.Drafts.Remove(current.Id);}else current.Deleted=true;dirty=true;RefreshList(null);SaveNow();}
        void RenderPreview() {
            if(current==null||current.Kind!="clip")return;
            if(renderedSource==editor.Text&&renderedId==current.Id){var existing=preview.Document==null?null:preview.Document.GetElementById("render-key");previewReady=existing!=null&&existing.GetAttribute("data-key")==renderKey;UpdateCopyButtons();return;}
            CapturePreviewPosition();renderedSource=editor.Text;renderedId=current.Id;previewReady=false;renderKey=Guid.NewGuid().ToString("N");
            try{renderedHtml=MarkdownView.Render(editor.Text);preview.DocumentText=renderedHtml.Replace("<body>","<body><span id='render-key' data-key='"+renderKey+"' style='display:none'></span>");}
            catch{renderedSource=null;preview.DocumentText="<html><body>预览暂时无法显示，请切回编辑查看原文。</body></html>";}
            UpdateCopyButtons();
        }
        void PreviewCompleted(object sender,WebBrowserDocumentCompletedEventArgs e) {
            if(preview.Document==null||preview.Document.Body==null)return;var marker=preview.Document.GetElementById("render-key");
            if(marker==null||marker.GetAttribute("data-key")!=renderKey)return;
            previewReady=current!=null&&current.Id==renderedId&&editor.Text==renderedSource;
            preview.Document.ContextMenuShowing+=delegate(object s,HtmlElementEventArgs a){a.ReturnValue=false;previewMenu.Show(Cursor.Position);};UpdateCopyButtons();
            RestorePreviewPosition();
        }
        void CapturePreviewPosition(){
            if(current==null||renderedId!=current.Id||preview.Document==null||preview.Document.Body==null)return;
            var marker=preview.Document.GetElementById("render-key");if(marker==null||marker.GetAttribute("data-key")!=renderKey)return;
            var html=preview.Document.GetElementsByTagName("html");int top=preview.Document.Body.ScrollTop;if(html.Count>0)top=Math.Max(top,html[0].ScrollTop);viewState.For(current.Id).PreviewScrollY=Math.Max(0,top);
        }
        void CaptureDraftView(){
            if(binding||current==null||!data.Drafts.Contains(current))return;var state=viewState.For(current.Id);
            state.Mode=viewMode;
            if(!editorSplit.Panel1Collapsed&&editor.IsHandleCreated){state.Caret=editor.SelectionStart;state.SelectionLength=editor.SelectionLength;state.FirstVisibleChar=EditorPosition.FirstVisibleChar(editor);}
            if(viewMode=="split"&&editorSplit.Width>100)state.SplitRatio=(double)editorSplit.SplitterDistance/(editorSplit.Width-editorSplit.SplitterWidth);
            if(!editorSplit.Panel2Collapsed)CapturePreviewPosition();
        }
        void RestorePreviewPosition(){if(current!=null&&previewReady&&renderedId==current.Id&&preview.Document!=null)preview.Document.Window.ScrollTo(0,Math.Max(0,viewState.For(current.Id).PreviewScrollY));}
        void RestoreDraftPosition(){
            if(current==null)return;string id=current.Id;string mode=viewMode;
            Action restore=delegate{if(IsDisposed||current==null||current.Id!=id||viewMode!=mode)return;if(!editorSplit.Panel1Collapsed)EditorPosition.Restore(editor,viewState.For(id));RestorePreviewPosition();};
            restore();if(windowReady&&IsHandleCreated)BeginInvoke(restore);
        }
        bool SaveViewState(){
            CaptureDraftView();
            if(windowReady&&WindowState!=FormWindowState.Minimized){Rectangle bounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;if(bounds.Width>0&&bounds.Height>0){viewState.HasBounds=true;viewState.X=bounds.X;viewState.Y=bounds.Y;viewState.Width=bounds.Width;viewState.Height=bounds.Height;viewState.Maximized=WindowState==FormWindowState.Maximized;}}
            bool saved=viewStore.Save(viewState);if(!saved&&status!=null){status.ForeColor=Color.Firebrick;status.Text="视图位置保存失败 · Ctrl+S 重试";}return saved;
        }
        void CopySelection(){if(previewReady&&preview.Document!=null)preview.Document.ExecCommand("Copy",false,null);}
        void CopyPrimary(){if(current!=null&&current.Kind=="clip")CopyFormatted();else CopyBody();}
        void CopyFormatted() {
            if(current==null||current.Kind!="clip"||editor.TextLength==0)return;
            if(!previewReady||renderedSource!=editor.Text||renderedId!=current.Id){RenderPreview();return;}
            try{RichClipboard.Set(renderedHtml,preview.Document.Body.InnerText??"");copy.Text="已复制排版 ✓";copyTimer.Stop();copyTimer.Start();}
            catch{MessageBox.Show(this,"剪贴板暂时被其他程序占用，请再试一次。","未复制成功");}
        }
        void CopyBody(){if(current==null||editor.TextLength==0)return;try{Clipboard.SetText(editor.Text,TextDataFormat.UnicodeText);if(current.Kind=="clip")copyRaw.Text="已复制原文 ✓";else copy.Text="已复制 ✓";copyTimer.Stop();copyTimer.Start();}catch{MessageBox.Show(this,"剪贴板暂时被其他程序占用，请再试一次。","未复制成功");}}
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
            if(keyData==(Keys.Control|Keys.N)){NewDraft();return true;}if(keyData==(Keys.Control|Keys.Shift|Keys.C)){CopyPrimary();return true;}if(keyData==(Keys.Control|Keys.F)){search.Focus();search.SelectAll();return true;}if(keyData==(Keys.Control|Keys.S)){SaveNow();return true;}
            if(preview.ContainsFocus&&keyData==(Keys.Control|Keys.C)){CopySelection();return true;}if(preview.ContainsFocus&&keyData==(Keys.Control|Keys.A)){if(preview.Document!=null)preview.Document.ExecCommand("SelectAll",false,null);return true;}
            return base.ProcessCmdKey(ref msg,keyData);
        }
    }
}
