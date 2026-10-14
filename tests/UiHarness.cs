using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Reflection;using System.Threading.Tasks;using System.Windows.Forms;using LocalDownload;
class UiHarness {
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static object Invoke(DownloadWindow window,string name,params object[] args){return typeof(DownloadWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,args);}
    static Dictionary<Job,Form> Dialogs(DownloadWindow window){return (Dictionary<Job,Form>)typeof(DownloadWindow).GetField("progressWindows",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window);}
    [STAThread]static int Main(string[] args){
        Application.EnableVisualStyles();var window=new DownloadWindow();int result=0;
        window.Shown+=async(s,e)=>{try{
            var pending=new TaskCompletionSource<bool>();var job=new Job {Id=Guid.NewGuid().ToString("N"),Name="auto-close.mp4",State="Downloading",Running=pending.Task};
            Invoke(window,"ShowProgress",job,true);var progress=Dialogs(window)[job];Check(progress.Visible,"progress dialog is visible");
            window.WindowState=FormWindowState.Minimized;WindowFocus.ShowFront(progress);await Task.Delay(150);Check(window.WindowState==FormWindowState.Normal&&progress.Visible,"restores minimized owner and shows popup");
            await Task.Delay(1400);Check(!progress.TopMost,"popup does not remain always-on-top");
            job.State="Complete";await Task.Delay(450);Check(!progress.IsDisposed,"completion waits for transfer task to finish");pending.SetResult(true);await Task.Delay(600);Check(progress.IsDisposed,"successful progress window closes automatically");
            var failed=new Job {Id=Guid.NewGuid().ToString("N"),Name="error.mp4",State="Error",Error="Fixture error",Running=Task.FromResult(false)};
            Invoke(window,"ShowProgress",failed,true);var errorWindow=Dialogs(window)[failed];await Task.Delay(650);Check(!errorWindow.IsDisposed,"error dialog remains open");errorWindow.Close();
            var stopped=new Job {Id=Guid.NewGuid().ToString("N"),Name="stopped.mp4",State="Stopped",Running=Task.FromResult(false)};
            Invoke(window,"ShowProgress",stopped,true);var stoppedWindow=Dialogs(window)[stopped];await Task.Delay(650);Check(!stoppedWindow.IsDisposed,"stopped dialog remains open");stoppedWindow.Close();
            var jobs=(List<Job>)typeof(DownloadWindow).GetField("jobs",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window);
            var downloaded=new Job {Id=Guid.NewGuid().ToString("N"),Url=args[0],Name="redownload.mp4",Path=Path.Combine(Core.Root,"redownload.mp4"),State="Complete",Bytes=999,Total=999,Validator="old-validator"};
            jobs.Add(downloaded);string oldId=downloaded.Id;Invoke(window,"Redownload",downloaded);await downloaded.Running;await Task.Delay(700);
            Check(downloaded.State=="Complete"&&File.Exists(downloaded.Path),"deleted history file downloads again");Check(downloaded.Id!=oldId&&downloaded.Bytes!=999,"redownload creates a fresh transfer");Check(!Dialogs(window).ContainsKey(downloaded),"redownload progress closes after success");
            string first=downloaded.Path;byte[] original=File.ReadAllBytes(first);Invoke(window,"Redownload",downloaded);await downloaded.Running;await Task.Delay(700);
            Check(downloaded.Path!=first&&File.ReadAllBytes(first).SequenceEqual(original),"redownload preserves an existing file");
        }catch(Exception error){Console.WriteLine(error.ToString());result=1;}finally{window.Close();}};
        Application.Run(window);return result;
    }
}
