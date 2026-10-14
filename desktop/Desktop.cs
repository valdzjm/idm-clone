using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalDownload;

class DownloadWindow : Form {
    readonly List<Job> jobs = new List<Job>();
    readonly ListView list = new ListView();
    readonly TreeView tree = new TreeView();
    readonly ToolStrip toolbar = new ToolStrip();
    readonly StatusStrip status = new StatusStrip();
    readonly ToolStripStatusLabel statusText = new ToolStripStatusLabel();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly Dictionary<Job,Form> progressWindows = new Dictionary<Job,Form>();
    string category = "All Downloads";
    bool inboxBusy;
    int ticks;
    string StateFile {get {return Path.Combine(Core.Data,"downloads.json");}}
    Job Selected {get {return list.SelectedItems.Count == 0 ? null : (Job)list.SelectedItems[0].Tag;}}

    public DownloadWindow() {
        Text = "Local Download Manager 0.4"; Width=1060; Height=640; MinimumSize=new Size(860,480);
        StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",9);
        var menu=new MenuStrip();
        var tasks = new ToolStripMenuItem("Tasks");
        tasks.DropDownItems.Add("Add URL…",null,(s,e)=>AddUrl());
        tasks.DropDownItems.Add("Exit",null,(s,e)=>Close()); menu.Items.Add(tasks);
        var files = new ToolStripMenuItem("Downloads");
        files.DropDownItems.Add("Resume",null,(s,e)=>Resume());
        files.DropDownItems.Add("Redownload",null,(s,e)=>Redownload(Selected));
        files.DropDownItems.Add("Stop",null,(s,e)=>Stop(Selected));
        files.DropDownItems.Add("Stop All",null,(s,e)=>StopAll());
        files.DropDownItems.Add("Open containing folder",null,(s,e)=>OpenFolder()); menu.Items.Add(files);
        var help = new ToolStripMenuItem("Help");
        help.DropDownItems.Add("Chrome integration",null,(s,e)=>MessageBox.Show(this,"Load the extension folder in chrome://extensions (Developer mode → Load unpacked), then run register-host.ps1.\n\nThe extension adds a Download this video button to direct video elements and lists observed media in its popup.","Browser integration"));
        help.DropDownItems.Add("About",null,(s,e)=>MessageBox.Show(this,"Local Download Manager 0.3\nDirect files and social/website video downloads\n\nPage downloads use yt-dlp, FFmpeg and Node. Website support varies. Protected streams and full IDM parity are not implemented.","About"));menu.Items.Add(help);
        MainMenuStrip=menu;
        toolbar.ImageScalingSize=new Size(32,32); toolbar.GripStyle=ToolStripGripStyle.Hidden; toolbar.Padding=new Padding(8,5,8,5); toolbar.Height=68;
        AddButton("Add URL",Color.ForestGreen,"+",AddUrl);
        AddButton("Resume",Color.ForestGreen,"▶",Resume);
        AddButton("Stop",Color.Firebrick,"■",()=>Stop(Selected));
        AddButton("Stop All",Color.Firebrick,"Ⅱ",StopAll);
        toolbar.Items.Add(new ToolStripSeparator());
        AddButton("Delete",Color.DimGray,"×",Delete);
        AddButton("Delete Completed",Color.DimGray,"✓",DeleteCompleted);
        toolbar.Items.Add(new ToolStripSeparator());
        AddButton("Open Folder",Color.Goldenrod,"▰",OpenFolder);
        AddButton("Browser Integration",Color.SteelBlue,"↔",()=>help.DropDownItems[0].PerformClick());
        var split=new SplitContainer {Dock=DockStyle.Fill,SplitterDistance=185,FixedPanel=FixedPanel.Panel1};
        tree.Dock=DockStyle.Fill; tree.HideSelection=false;
        var all=tree.Nodes.Add("All Downloads");
        foreach(var name in new[]{"Compressed","Documents","Music","Programs","Video"}) all.Nodes.Add(name);
        tree.Nodes.Add("Unfinished"); tree.Nodes.Add("Finished"); all.Expand();
        tree.AfterSelect+=(s,e)=>{category=e.Node.Text;RefreshRows();}; tree.SelectedNode=all;
        split.Panel1.Controls.Add(tree);
        list.Dock=DockStyle.Fill;list.View=View.Details;list.FullRowSelect=true;list.GridLines=true;list.HideSelection=false;list.MultiSelect=false;
        string[] headers={"File Name","Size","Status","Time left","Transfer rate","Last Try / Error"};
        int[] widths={240,85,120,90,105,270};
        for(int i=0;i<headers.Length;i++)list.Columns.Add(headers[i],widths[i]);
        list.DoubleClick+=(s,e)=>{if(Selected!=null){if(Selected.State=="Complete"&&!File.Exists(Selected.Path))Redownload(Selected);else ShowProgress(Selected,Selected.State!="Complete");}};
        var context = new ContextMenuStrip();
        context.Items.Add("Redownload",null,(s,e)=>Redownload(Selected));
        context.Items.Add("Open source in Chrome",null,(s,e)=>OpenSource(Selected));
        context.Items.Add("Resume",null,(s,e)=>Resume());context.Items.Add("Stop",null,(s,e)=>Stop(Selected));
        context.Items.Add("Progress",null,(s,e)=>{if(Selected!=null)ShowProgress(Selected,Selected.State!="Complete");});
        context.Items.Add("Open Folder",null,(s,e)=>OpenFolder());context.Items.Add("Delete from list",null,(s,e)=>Delete());list.ContextMenuStrip=context;
        split.Panel2.Controls.Add(list); status.Items.Add(statusText);
        Controls.Add(split); Controls.Add(toolbar); Controls.Add(menu); Controls.Add(status);
        toolbar.Dock=DockStyle.Top; menu.Dock=DockStyle.Top;
        Directory.CreateDirectory(Core.Inbox);Directory.CreateDirectory(Path.Combine(Core.Root,"Downloads"));
        LoadState(); RefreshRows(); timer.Interval=500; timer.Tick+=(s,e)=>Tick();timer.Start();
        FormClosing+=(s,e)=>{
            if(jobs.Any(j=>j.Running!=null&&!j.Running.IsCompleted) && MessageBox.Show(this,"Stop running downloads and close? Partial files can be resumed later.","Close",MessageBoxButtons.YesNo)!=DialogResult.Yes){e.Cancel=true;return;}
            timer.Stop();StopAll();SaveState();
        };
    }
    void AddButton(string text, Color color, string glyph, Action action) {
        var image=new Bitmap(32,32);
        using(var g=Graphics.FromImage(image)) {g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var b=new SolidBrush(color))g.FillEllipse(b,1,1,30,30);using(var f=new Font("Segoe UI Symbol",17,FontStyle.Bold))g.DrawString(glyph,f,Brushes.White,new RectangleF(0,0,32,32),new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center});}
        var button=new ToolStripButton(text,image);button.TextImageRelation=TextImageRelation.ImageAboveText;button.AutoSize=true;button.Padding=new Padding(6,0,6,0);button.Click+=(s,e)=>action();toolbar.Items.Add(button);
    }
    void AddUrl() {
        using(var dialog=new Form {Text="Add URL",Width=580,Height=155,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
            var text=new TextBox {Left=15,Top=20,Width=530};
            var ok=new Button {Text="OK",Left=370,Top=60,Width=80,DialogResult=DialogResult.OK};
            var cancel=new Button {Text="Cancel",Left=465,Top=60,Width=80,DialogResult=DialogResult.Cancel};
            dialog.Controls.AddRange(new Control[]{text,ok,cancel});dialog.AcceptButton=ok;dialog.CancelButton=cancel;
            if(dialog.ShowDialog(this)==DialogResult.OK) { string url=text.Text.Trim();Uri uri;bool direct=Uri.TryCreate(url,UriKind.Absolute,out uri)&&System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath,@"\.(mp4|webm|mkv|mov|mp3|m4a|wav|zip|exe|pdf)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase); AcceptRequest(new Request {op="download",url=url,kind=direct?"file":"page"}); }
        }
    }
    void AcceptRequest(Request request) {
        try{Core.Validate(request);}catch(Exception error){MessageBox.Show(this,error.Message,"Cannot download");return;}
        using(var dialog=new Form {Text="Download File Info",Width=640,Height=245,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}) {
            dialog.Shown+=(s,e)=>WindowFocus.ShowFront(dialog);
            dialog.Controls.Add(new Label {Text="URL",Left=12,Top=20,Width=65});
            dialog.Controls.Add(new TextBox {Text=request.url,ReadOnly=true,Left=90,Top=15,Width=520});
            dialog.Controls.Add(new Label {Text="Category",Left=12,Top=57,Width=70});
            dialog.Controls.Add(new ComboBox {Left=90,Top=52,Width=180,Text="Video",Enabled=false});
            dialog.Controls.Add(new Label {Text="Save As",Left=12,Top=95,Width=65});
            var path=new TextBox {Left=90,Top=90,Width=430,Text=Path.Combine(Core.Root,"Downloads",(request.kind=="youtube"||request.kind=="page")?Core.VideoFilename(request):Core.Filename(request.url))};
            var browse=new Button {Left=535,Top=88,Width=75,Text="Browse…"};
            browse.Click+=(s,e)=>{using(var save=new SaveFileDialog {FileName=Path.GetFileName(path.Text),InitialDirectory=Path.GetDirectoryName(path.Text),Filter="All files|*.*"})if(save.ShowDialog(dialog)==DialogResult.OK)path.Text=save.FileName;};
            dialog.Controls.Add(path);dialog.Controls.Add(browse);
            var later=new Button {Left=245,Top=150,Width=110,Text="Download Later",DialogResult=DialogResult.Retry};
            var start=new Button {Left=365,Top=150,Width=125,Text="Start Download",DialogResult=DialogResult.OK};
            var cancel=new Button {Left=505,Top=150,Width=105,Text="Cancel",DialogResult=DialogResult.Cancel};
            dialog.Controls.AddRange(new Control[]{later,start,cancel});dialog.AcceptButton=start;dialog.CancelButton=cancel;
            var result=dialog.ShowDialog(this);
            if(result!=DialogResult.OK&&result!=DialogResult.Retry)return;
            string dest;
            try{dest=Path.GetFullPath(path.Text);}catch(Exception error){MessageBox.Show(this,error.Message,"Invalid save path");return;}
            if(File.Exists(dest)||File.Exists(dest+".part")||jobs.Any(j=>String.Equals(j.Path,dest,StringComparison.OrdinalIgnoreCase))){MessageBox.Show(this,"This path already exists or is assigned to a download. Choose another file name.","File exists");return;}
            var job=new Job {Id=Guid.NewGuid().ToString("N"),Url=request.url,SourceUrl=request.sourceUrl??request.url,Name=Path.GetFileName(dest),Path=dest,State="Stopped",Total=-1,Referer=request.referer,Cookie=request.cookie,UserAgent=request.userAgent,Kind=request.kind,Cookies=request.cookies};
            jobs.Add(job);SaveState();RefreshRows();
            if(result==DialogResult.OK){Start(job);ShowProgress(job);}
        }
    }
    void Start(Job job) {
        if(job.State=="Complete"||(job.Running!=null&&!job.Running.IsCompleted))return;
        job.Cancellation=new CancellationTokenSource();job.State="Connecting";
        job.Running=(job.Kind=="youtube"||job.Kind=="page")?VideoPage.Download(job):Core.Download(job); RefreshRows();
    }
    void Resume(){if(Selected!=null)Start(Selected);}
    void Redownload(Job job){
        if(job==null)return;
        try{Core.PrepareRedownload(job);Form open;if(progressWindows.TryGetValue(job,out open))open.Close();SaveState();RefreshRows();Start(job);ShowProgress(job);}
        catch(Exception error){MessageBox.Show(this,error.Message,"Cannot redownload");}
    }
    void OpenSource(Job job){
        if(job==null)return;string url=String.IsNullOrEmpty(job.SourceUrl)?job.Url:job.SourceUrl;Uri uri;
        if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https"))return;
        try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("chrome.exe","\""+uri.AbsoluteUri.Replace("\"","%22")+"\""){UseShellExecute=true});}catch{System.Diagnostics.Process.Start(uri.AbsoluteUri);}
    }
    void Stop(Job job){if(job!=null&&job.Cancellation!=null&&job.Running!=null&&!job.Running.IsCompleted)job.Cancellation.Cancel();}
    void StopAll(){foreach(var job in jobs)Stop(job);}
    void Delete(){var job=Selected;if(job==null)return;if(job.Running!=null&&!job.Running.IsCompleted){MessageBox.Show(this,"Stop this download before removing it.");return;}jobs.Remove(job);SaveState();RefreshRows();}
    void DeleteCompleted(){jobs.RemoveAll(j=>j.State=="Complete");SaveState();RefreshRows();}
    void OpenFolder(){string folder=Selected==null?Path.Combine(Core.Root,"Downloads"):Path.GetDirectoryName(Selected.Path);if(Directory.Exists(folder))System.Diagnostics.Process.Start("explorer.exe",folder);}
    static string FormatSize(long bytes){return bytes<0?"Unknown":bytes>=1048576?(bytes/1048576.0).ToString("0.00")+" MB":(bytes/1024.0).ToString("0.0")+" KB";}
    static string Remaining(Job job){return job.Speed>0&&job.Total>0?TimeSpan.FromSeconds(Math.Max(0,(job.Total-job.Bytes)/job.Speed)).ToString(@"hh\:mm\:ss"):"";}
    bool Matches(Job job){
        if(category=="All Downloads")return true;if(category=="Unfinished")return job.State!="Complete";if(category=="Finished")return job.State=="Complete";
        string ext=Path.GetExtension(job.Name).ToLowerInvariant();
        string kind=new[]{".mp4",".webm",".mkv",".mov",".avi",".m4v"}.Contains(ext)?"Video":new[]{".mp3",".m4a",".wav",".ogg"}.Contains(ext)?"Music":new[]{".zip",".7z",".rar"}.Contains(ext)?"Compressed":new[]{".exe",".msi"}.Contains(ext)?"Programs":"Documents";
        return kind==category;
    }
    void RefreshRows(){
        string selected=Selected==null?null:Selected.Id;list.BeginUpdate();list.Items.Clear();
        foreach(var job in jobs.Where(Matches)){
            var row=new ListViewItem(new[]{job.Name,FormatSize(job.Total),job.State=="Downloading"&&job.Total>0?"Downloading "+(100.0*job.Bytes/job.Total).ToString("0")+"%":job.State,Remaining(job),job.Speed>0?FormatSize((long)job.Speed)+"/s":"",job.Error??""}){Tag=job};
            list.Items.Add(row);if(job.Id==selected)row.Selected=true;
        }
        list.EndUpdate();statusText.Text=jobs.Count+" downloads • "+jobs.Count(j=>j.State=="Downloading")+" active • Double-click a download for progress";
    }
    void Tick(){
        RefreshRows();if(++ticks%4==0)SaveState();if(inboxBusy)return;
        foreach(string file in Directory.GetFiles(Core.Inbox,"*.request")) {
            inboxBusy=true;
            try {byte[] packet=System.Security.Cryptography.ProtectedData.Unprotect(File.ReadAllBytes(file),null,System.Security.Cryptography.DataProtectionScope.CurrentUser);var request=Core.Json().Deserialize<Request>(System.Text.Encoding.UTF8.GetString(packet));File.Delete(file);WindowFocus.ShowFront(this);AcceptRequest(request);}
            catch(Exception error){try{File.Delete(file);}catch{}MessageBox.Show(this,error.Message,"Browser request failed");}
            finally{inboxBusy=false;}
        }
    }
    void LoadState(){
        if(!File.Exists(StateFile))return;
        try{var saved=Core.Json().Deserialize<List<Job>>(File.ReadAllText(StateFile));foreach(var job in saved){if(job.State!="Complete")job.State="Stopped";job.Gate=new object();jobs.Add(job);}}
        catch(Exception error){MessageBox.Show(this,"Could not read saved downloads: "+error.Message,"State file error");}
    }
    void SaveState(){
        try{Directory.CreateDirectory(Core.Data);string tmp=StateFile+".tmp";File.WriteAllText(tmp,Core.Json().Serialize(jobs));if(File.Exists(StateFile))File.Replace(tmp,StateFile,null);else File.Move(tmp,StateFile);}
        catch(Exception error){statusText.Text="Could not save download list: "+error.Message;}
    }
    void ShowProgress(Job job,bool autoClose=true){
        Form existing;if(progressWindows.TryGetValue(job,out existing)&&!existing.IsDisposed){WindowFocus.ShowFront(existing);return;}
        var dialog=new Form {Text=job.Name+" — Download",Width=550,Height=330,StartPosition=FormStartPosition.CenterParent};
        progressWindows[job]=dialog;bool closeOnComplete=autoClose;
        var text=new TextBox {Left=20,Top=20,Width=490,Height=140,Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,ScrollBars=ScrollBars.Vertical,BackColor=SystemColors.Control,TabStop=false};var bar=new ProgressBar {Left=20,Top=170,Width=490,Height=22};
        var pause=new Button {Text="Stop",Left=220,Top=230,Width=90};pause.Click+=(s,e)=>Stop(job);
        var resume=new Button {Text="Resume",Left=320,Top=230,Width=90};resume.Click+=(s,e)=>{closeOnComplete=true;Start(job);};
        var close=new Button {Text="Close",Left=420,Top=230,Width=90};close.Click+=(s,e)=>dialog.Close();
        dialog.Controls.AddRange(new Control[]{text,bar,pause,resume,close});
        var update=new System.Windows.Forms.Timer {Interval=300};
        Action refresh=()=>{text.Text="File: "+job.Name+"\n\nStatus: "+job.State+"\nDownloaded: "+FormatSize(job.Bytes)+" / "+FormatSize(job.Total)+"\nTransfer rate: "+FormatSize((long)job.Speed)+"/s\nTime left: "+Remaining(job)+"\n"+(job.Error??"");bar.Value=job.Total>0?(int)Math.Min(100,100.0*job.Bytes/job.Total):0;pause.Enabled=job.Running!=null&&!job.Running.IsCompleted;resume.Enabled=job.State!="Complete"&&!pause.Enabled;};
        update.Tick+=(s,e)=>{refresh();if(closeOnComplete&&job.State=="Complete"&&(job.Running==null||job.Running.IsCompleted))dialog.Close();};
        dialog.FormClosed+=(s,e)=>{update.Dispose();progressWindows.Remove(job);};
        dialog.Shown+=(s,e)=>WindowFocus.ShowFront(dialog);refresh();update.Start();dialog.Show(this);
    }
    [STAThread] static int Main(string[] args){
        System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
        if(args.Length>=2&&args[0]=="--fetch"){
            try{var job=Core.Json().Deserialize<Job>(File.ReadAllText(args[1]));Core.Validate(new Request {op="download",url=job.Url,kind=job.Kind,cookies=job.Cookies});job.Cancellation=new CancellationTokenSource();if(args.Length>=3)job.Cancellation.CancelAfter(Int32.Parse(args[2]));((job.Kind=="youtube"||job.Kind=="page")?VideoPage.Download(job):Core.Download(job)).GetAwaiter().GetResult();File.WriteAllText(args[1]+".result.json",Core.Json().Serialize(job));return job.State=="Complete"?0:job.State=="Stopped"?2:1;}catch{return 3;}
        }
        bool created;using(var mutex=new Mutex(true,Core.MutexName,out created)){
            if(!created)return 0;Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            var window=new DownloadWindow();
            if(args.Length==2&&args[0]=="--preview"){
                window.Shown+=(s,e)=>{var shot=new System.Windows.Forms.Timer {Interval=800};shot.Tick+=(a,b)=>{shot.Stop();using(var bitmap=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new Rectangle(0,0,window.Width,window.Height));bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);}window.Close();shot.Dispose();};shot.Start();};
            }
            Application.Run(window);mutex.ReleaseMutex();return 0;
        }
    }
}
