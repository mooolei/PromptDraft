using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PromptDraft {
    public static class CategoryNames {
        public static string Clean(string value){value=(value??"").Trim();return value=="未分类"?"":value;}
        public static string Display(string value){value=Clean(value);return value.Length==0?"未分类":value;}
        public static bool Same(string a,string b){return String.Equals(Clean(a),Clean(b),StringComparison.OrdinalIgnoreCase);}
        public static List<string> All(DraftState state){
            return (state.Categories??new List<string>()).Concat(state.Drafts.Select(d=>d.Category)).Select(Clean).Where(s=>s.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s=>s,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
    public class CategoryHeader {
        public string Name;
        public int Count;
        public bool Collapsed;
        public override string ToString(){return CategoryNames.Display(Name)+" · "+Count+" 条 · "+(Collapsed?"已收起":"已展开");}
    }
    public partial class MainForm {
        ComboBox categoryBox;
        SoftButton foldAll;
        readonly ToolTip sidebarTip=new ToolTip{AutoPopDelay=10000,InitialDelay=500,ReshowDelay=100};
        ContextMenuStrip groupMenu;
        CategoryHeader menuCategory;
        string selectedCategory="";
        int hoveredRow=-1;
        string GroupKey(string name){return kind+"|"+CategoryNames.Clean(name).ToUpperInvariant();}
        int HeaderHeight {get{return Math.Max(38,Font.Height*2+4);}}
        int DraftHeight {get{return Math.Max(112,(Font.Height+2)*4+27);}}

        void InitializeSidebar(TableLayoutPanel side){
            var tools=Grid(3,1);tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,76));tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,86));tools.Padding=new Padding(0,0,0,5);
            tools.Controls.Add(L("分类",9,Palette.Muted),0,0);var addCategory=Btn("＋ 分类");addCategory.Margin=new Padding(0,0,6,0);addCategory.Click+=delegate{CreateCategory();};tools.Controls.Add(addCategory,1,0);
            foldAll=Btn("收起全部");foldAll.Click+=delegate{ToggleAllGroups();};tools.Controls.Add(foldAll,2,0);side.Controls.Add(tools,0,6);
            list.MeasureItem+=delegate(object s,MeasureItemEventArgs e){e.ItemHeight=e.Index>=0&&e.Index<list.Items.Count&&list.Items[e.Index] is CategoryHeader?HeaderHeight:DraftHeight;};
            list.MouseClick+=delegate(object s,MouseEventArgs e){if(e.Button!=MouseButtons.Left)return;int index=list.IndexFromPoint(e.Location);if(index>=0&&list.Items[index] is CategoryHeader)ToggleGroup((CategoryHeader)list.Items[index]);};
            list.KeyDown+=delegate(object s,KeyEventArgs e){var header=list.SelectedItem as CategoryHeader;if(header==null)return;if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter||(e.KeyCode==Keys.Right&&header.Collapsed)||(e.KeyCode==Keys.Left&&!header.Collapsed)){ToggleGroup(header);e.Handled=true;e.SuppressKeyPress=true;}};
            list.MouseMove+=delegate(object s,MouseEventArgs e){int index=list.IndexFromPoint(e.Location);if(index==hoveredRow)return;hoveredRow=index;var draft=index<0?null:list.Items[index] as Draft;sidebarTip.SetToolTip(list,draft==null?"":draft.DisplayTitle);};
            list.MouseLeave+=delegate{hoveredRow=-1;sidebarTip.SetToolTip(list,"");};
            groupMenu=new ContextMenuStrip();groupMenu.Items.Add("重命名分类…",null,delegate{if(menuCategory==null)return;string name=AskCategory("重命名分类",menuCategory.Name);if(name!=null)RenameCategory(menuCategory.Name,name);});
            groupMenu.Items.Add("移除分类，内容归入未分类",null,delegate{if(menuCategory!=null&&MessageBox.Show(this,"移除这个分类？所有内容会保留，并移到「未分类」。","移除分类",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes)RenameCategory(menuCategory.Name,"");});
            groupMenu.Opening+=delegate(object s,CancelEventArgs e){int index=list.IndexFromPoint(list.PointToClient(Cursor.Position));menuCategory=index<0?null:list.Items[index] as CategoryHeader;e.Cancel=menuCategory==null||CategoryNames.Clean(menuCategory.Name).Length==0;};list.ContextMenuStrip=groupMenu;
        }
        void InitializeCategoryInput(TableLayoutPanel row){
            categoryBox=new ComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDown,FlatStyle=FlatStyle.Flat,Font=new Font(Font.FontFamily,10),BackColor=Color.White,ForeColor=Palette.Ink,MaxLength=40,AccessibleName="所属分类，可选择或输入新分类",IntegralHeight=false,DropDownHeight=240};
            categoryBox.Validated+=delegate{CommitCategory();};categoryBox.SelectionChangeCommitted+=delegate{CommitCategory();};
            categoryBox.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){CommitCategory();editor.Focus();e.Handled=true;e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape){categoryBox.Text=current==null?"":CategoryNames.Display(current.Category);e.Handled=true;e.SuppressKeyPress=true;}};
            row.Controls.Add(Frame(categoryBox,new Padding(8,6,8,6)),3,0);sidebarTip.SetToolTip(categoryBox,"选择已有分类，或输入新名称后按 Enter。清空表示未分类。");
        }
        void UpdateCategoryChoices(){
            if(categoryBox==null)return;bool wasBinding=binding;binding=true;string text=categoryBox.Text;categoryBox.BeginUpdate();categoryBox.Items.Clear();categoryBox.Items.Add("未分类");foreach(string name in CategoryNames.All(data))categoryBox.Items.Add(name);categoryBox.Text=text;categoryBox.SelectionLength=0;categoryBox.EndUpdate();binding=wasBinding;
        }
        string EnsureCategory(string name){
            name=CategoryNames.Clean(name);if(name.Length==0)return "";
            string existing=CategoryNames.All(data).FirstOrDefault(s=>CategoryNames.Same(s,name));if(existing!=null)return existing;
            if(data.Categories==null)data.Categories=new List<string>();data.Categories.Add(name);return name;
        }
        void CommitCategory(){
            if(binding||current==null||current.Deleted||categoryBox==null)return;
            string name=CategoryNames.Clean(categoryBox.Text);if(CategoryNames.Same(current.Category,name))return;
            name=EnsureCategory(name);current.Category=name;selectedCategory=name;current.Updated=DateTime.Now;dirty=true;viewState.CollapsedCategories.Remove(GroupKey(name));RefreshList(current.Id);categoryBox.Text=CategoryNames.Display(name);saveTimer.Stop();saveTimer.Start();
        }
        void BuildCategoryRows(List<Draft> matches,bool searching){
            hoveredRow=-1;sidebarTip.SetToolTip(list,"");
            var names=searching?matches.Select(d=>CategoryNames.Clean(d.Category)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s=>s,StringComparer.CurrentCultureIgnoreCase).ToList():CategoryNames.All(data);
            if(!searching)names.Add("");
            foreach(string name in names){
                var drafts=matches.Where(d=>CategoryNames.Same(d.Category,name)).ToList();bool collapsed=!searching&&viewState.CollapsedCategories.Contains(GroupKey(name));
                list.Items.Add(new CategoryHeader{Name=name,Count=drafts.Count,Collapsed=collapsed});if(!collapsed)foreach(var draft in drafts)list.Items.Add(draft);
            }
            foldAll.Enabled=!searching;foldAll.Text=list.Items.OfType<CategoryHeader>().Any(h=>!h.Collapsed)?"收起全部":"展开全部";
        }
        void DrawCategory(CategoryHeader header,DrawItemEventArgs e){
            using(var b=new SolidBrush(Palette.Side))e.Graphics.FillRectangle(b,e.Bounds);
            bool selected=(e.State&DrawItemState.Selected)!=0;var rect=Rectangle.Inflate(e.Bounds,-4,-3);
            if(selected)using(var b=new SolidBrush(Palette.Tint))e.Graphics.FillRectangle(b,rect);
            var flags=TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics,header.Collapsed?"▸":"▾",Font,new Rectangle(rect.Left+4,rect.Top,22,rect.Height),Palette.Green,flags);
            using(var f=new Font(Font.FontFamily,9.5F,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,CategoryNames.Display(header.Name),f,new Rectangle(rect.Left+29,rect.Top,Math.Max(1,rect.Width-78),rect.Height),Palette.Ink,flags);
            TextRenderer.DrawText(e.Graphics,header.Count.ToString(),Font,new Rectangle(rect.Right-45,rect.Top,38,rect.Height),Palette.Muted,flags|TextFormatFlags.Right);
        }
        void ToggleGroup(CategoryHeader header){
            if(search.Text.Trim().Length>0)return;
            string key=GroupKey(header.Name);if(!viewState.CollapsedCategories.Remove(key))viewState.CollapsedCategories.Add(key);
            int top=list.TopIndex;RefreshList(current==null?null:current.Id);selectedCategory=CategoryNames.Clean(header.Name);
            refreshing=true;list.SelectedItem=list.Items.OfType<CategoryHeader>().FirstOrDefault(h=>CategoryNames.Same(h.Name,header.Name));if(list.Items.Count>0)list.TopIndex=Math.Min(top,list.Items.Count-1);refreshing=false;viewStore.Save(viewState);
        }
        void ToggleAllGroups(){
            if(search.Text.Trim().Length>0)return;var groups=list.Items.OfType<CategoryHeader>().ToList();bool collapse=groups.Any(h=>!h.Collapsed);
            foreach(var group in groups){string key=GroupKey(group.Name);viewState.CollapsedCategories.Remove(key);if(collapse)viewState.CollapsedCategories.Add(key);}
            RefreshList(current==null?null:current.Id);viewStore.Save(viewState);
        }
        void CreateCategory(){
            string name=AskCategory("新建分类","");if(name==null)return;AddCategory(name);
        }
        void AddCategory(string name){
            name=EnsureCategory(name);if(name.Length==0)return;dirty=true;viewState.CollapsedCategories.Remove(GroupKey(name));RefreshList(current==null?null:current.Id);selectedCategory=name;
            refreshing=true;list.SelectedItem=list.Items.OfType<CategoryHeader>().FirstOrDefault(h=>CategoryNames.Same(h.Name,name));refreshing=false;SaveNow();
        }
        void RenameCategory(string oldName,string newName){
            oldName=CategoryNames.Clean(oldName);newName=CategoryNames.Clean(newName);if(oldName.Length==0||oldName==newName)return;
            string existing=CategoryNames.All(data).FirstOrDefault(s=>CategoryNames.Same(s,newName)&&!CategoryNames.Same(s,oldName));if(existing!=null)newName=existing;
            foreach(var draft in data.Drafts.Where(d=>CategoryNames.Same(d.Category,oldName)))draft.Category=newName;
            data.Categories=(data.Categories??new List<string>()).Where(s=>!CategoryNames.Same(s,oldName)).ToList();if(newName.Length>0&&!data.Categories.Any(s=>CategoryNames.Same(s,newName)))data.Categories.Add(newName);
            foreach(string section in new[]{"prompt","clip"}){string oldKey=section+"|"+oldName.ToUpperInvariant();string newKey=section+"|"+newName.ToUpperInvariant();if(viewState.CollapsedCategories.Remove(oldKey)&&newName.Length>0&&!viewState.CollapsedCategories.Contains(newKey))viewState.CollapsedCategories.Add(newKey);}
            if(CategoryNames.Same(selectedCategory,oldName))selectedCategory=newName;dirty=true;
            bool wasBinding=binding;binding=true;categoryBox.Text=current==null?"":CategoryNames.Display(current.Category);binding=wasBinding;RefreshList(current==null?null:current.Id);SaveNow();viewStore.Save(viewState);
        }
        string AskCategory(string title,string initial){
            using(var dialog=new Form{Text=title,Font=Font,ClientSize=new Size(390,148),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false,BackColor=Palette.Canvas}){
                var label=new Label{Text="分类名称（例如：工作、学习、项目名）",AutoSize=true,Location=new Point(18,16)};
                var input=new TextBox{Text=initial,MaxLength=40,Location=new Point(20,43),Width=348,BorderStyle=BorderStyle.FixedSingle};
                var ok=new Button{Text="确定",DialogResult=DialogResult.OK,Bounds=new Rectangle(198,98,80,32)};var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(288,98,80,32)};
                dialog.Controls.AddRange(new Control[]{label,input,ok,cancel});dialog.AcceptButton=ok;dialog.CancelButton=cancel;dialog.Shown+=delegate{input.Focus();input.SelectAll();};
                if(dialog.ShowDialog(this)!=DialogResult.OK)return null;string name=CategoryNames.Clean(input.Text);return name.Length==0?null:name;
            }
        }
    }
}
