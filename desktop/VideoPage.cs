using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using LocalDownload;

public static class VideoPage {
    class Tools { public string ytDlp {get;set;} public string ffmpeg {get;set;} public string node {get;set;} }
    // Windows argv quoting; launch directly, without a command shell.
    static string Arg(string text) {
        var result=new StringBuilder("\"");int slashes=0;
        foreach(char c in text){if(c=='\\'){slashes++;continue;}if(c=='\"'){result.Append('\\',slashes*2+1);result.Append(c);}else{result.Append('\\',slashes);result.Append(c);}slashes=0;}
        result.Append('\\',slashes*2);result.Append('"');return result.ToString();
    }
    static double Number(string text) {double n;return Double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out n)?n:0;}
    public static Task Download(Job job) {return Task.Run(()=>Run(job));}
    static void Run(Job job) {
        var token=job.Cancellation.Token;
        string cookiePath=null;
        try {
            Core.Validate(new Request {op="download",url=job.Url,kind=job.Kind,cookies=job.Cookies});
            string configPath=Path.Combine(Core.Root,"tools","config.json");
            if(!File.Exists(configPath))throw new Exception("Run setup-video-tools.ps1 to install the website downloader.");
            var tools=Core.Json().Deserialize<Tools>(File.ReadAllText(configPath));
            if(!File.Exists(tools.ytDlp)||!File.Exists(tools.ffmpeg)||!File.Exists(tools.node))throw new Exception("A video tool is missing. Run setup-video-tools.ps1 again.");
            if(!Regex.IsMatch(job.Id??"",@"^[a-f0-9]{32}$"))job.Id=Guid.NewGuid().ToString("N");
            string work=Path.Combine(Core.Data,"media",job.Id);Directory.CreateDirectory(work);
            string output=Path.Combine(work,"video.mp4");
            if(File.Exists(job.Path))throw new Exception("The destination file already exists. Choose another name.");
            job.State="Resolving video";job.Error="";job.Total=-1;job.Speed=0;
            var arguments=new List<string>{"--ignore-config","--no-playlist","--playlist-items","1","--max-downloads","1","--no-overwrites","--continue","--newline","--no-color","--socket-timeout","30","--retries","2","--js-runtimes","node:"+tools.node,"--ffmpeg-location",tools.ffmpeg,"--format","bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]/bv*+ba/bv*/b","--merge-output-format","mp4","--remux-video","mp4","--progress-template","download:LOCALPROGRESS %(progress.downloaded_bytes)s %(progress.total_bytes,progress.total_bytes_estimate)s %(progress.speed)s","--output",Path.Combine(work,"video.%(ext)s")};
            if(!String.IsNullOrWhiteSpace(job.UserAgent))arguments.AddRange(new[]{"--user-agent",job.UserAgent});
            if(job.Cookies!=null&&job.Cookies.Count>0){cookiePath=Path.Combine(work,"session-"+Guid.NewGuid().ToString("N")+".txt");WriteCookies(cookiePath,job.Cookies,job.Url);arguments.AddRange(new[]{"--cookies",cookiePath});}
            arguments.Add("--");arguments.Add(job.Url);
            var start=new ProcessStartInfo(tools.ytDlp,String.Join(" ",Array.ConvertAll(arguments.ToArray(),Arg))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=work};
            var log=new StringBuilder();
            using(var process=new Process {StartInfo=start}) {
                process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(log){log.AppendLine(e.Data);if(log.Length>6000)log.Remove(0,log.Length-6000);}};
                token.ThrowIfCancellationRequested();process.Start();process.BeginErrorReadLine();
                using(token.Register(()=>{
                    try {if(!process.HasExited){using(var kill=Process.Start(new ProcessStartInfo("taskkill.exe","/PID "+process.Id+" /T /F"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){kill.WaitForExit(5000);}}}catch{}
                })) {
                    string line;
                    while((line=process.StandardOutput.ReadLine())!=null) {
                        if(line.StartsWith("LOCALPROGRESS ")){
                            var fields=line.Split(' ');if(fields.Length>=4){job.Bytes=(long)Number(fields[1]);double total=Number(fields[2]);job.Total=total>0?(long)total:-1;job.Speed=Number(fields[3]);job.State="Downloading";}
                        } else if(line.Contains("[Merger]")||line.Contains("[VideoRemuxer]"))job.State="Combining tracks";
                        else lock(log){log.AppendLine(line);if(log.Length>6000)log.Remove(0,log.Length-6000);}
                    }
                    process.WaitForExit();token.ThrowIfCancellationRequested();
                    if(process.ExitCode!=0&&process.ExitCode!=101) {string message;lock(log)message=log.ToString().Trim();throw new Exception(FriendlyError(message));}
                }
            }
            token.ThrowIfCancellationRequested();
            if(!File.Exists(output)||new FileInfo(output).Length==0)throw new Exception("No completed MP4 was produced.");
            VerifyVideo(output,tools.ffmpeg);
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(job.Path));
            File.Move(output,job.Path);job.Bytes=new FileInfo(job.Path).Length;job.Total=job.Bytes;job.State="Complete";
        }catch(OperationCanceledException){job.State="Stopped";}
        catch(Exception error){job.State="Error";job.Error=error.Message;}
        finally{job.Speed=0;if(cookiePath!=null)try{File.Delete(cookiePath);}catch{} }
    }
    public static void WriteCookies(string path,List<BrowserCookie> cookies,string url) {
        Core.Validate(new Request {op="download",kind="page",url=url,cookies=cookies});
        var acl=new FileSecurity();acl.SetAccessRuleProtection(true,false);acl.SetOwner(WindowsIdentity.GetCurrent().User);acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.FullControl,AccessControlType.Allow));
        using(var stream=new FileStream(path,FileMode.CreateNew,FileSystemRights.Write,FileShare.None,4096,FileOptions.None,acl))
        using(var writer=new StreamWriter(stream,new UTF8Encoding(false))) {
            writer.NewLine="\r\n";writer.WriteLine("# Netscape HTTP Cookie File");
            foreach(var cookie in cookies)writer.WriteLine((cookie.httpOnly?"#HttpOnly_":"")+cookie.domain+"\t"+(!cookie.hostOnly?"TRUE":"FALSE")+"\t"+(String.IsNullOrEmpty(cookie.path)?"/":cookie.path)+"\t"+(cookie.secure?"TRUE":"FALSE")+"\t"+((long)cookie.expirationDate).ToString(CultureInfo.InvariantCulture)+"\t"+cookie.name+"\t"+(cookie.value??""));
        }
    }
    static void VerifyVideo(string path,string ffmpeg) {
        string probe=Path.Combine(Path.GetDirectoryName(ffmpeg),"ffprobe.exe");
        if(!File.Exists(probe))throw new Exception("ffprobe is required to verify the finished video. Install the complete FFmpeg tools.");
        var start=new ProcessStartInfo(probe,"-v error -show_entries stream=codec_type -of json "+Arg(path)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var process=Process.Start(start)) {
            var read=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(30000)){try{process.Kill();}catch{}throw new Exception("Video verification timed out.");}
            string json=read.GetAwaiter().GetResult();errors.GetAwaiter().GetResult();
            if(process.ExitCode!=0)throw new Exception("The assembled MP4 could not be verified.");
            var info=Core.Json().Deserialize<System.Collections.Generic.Dictionary<string,object>>(json);
            var streams=info["streams"] as System.Collections.ArrayList;bool video=false;
            if(streams!=null)foreach(System.Collections.Generic.Dictionary<string,object> stream in streams)if(stream.ContainsKey("codec_type")&&(string)stream["codec_type"]=="video")video=true;
            if(!video)throw new Exception("The source produced audio only. Select the actual video/reel or a detected video link.");
        }
    }
    static string FriendlyError(string message) {
        if(String.IsNullOrWhiteSpace(message))return "The website downloader failed.";
        string lower=message.ToLowerInvariant();
        if(lower.Contains("login")||lower.Contains("log in")||lower.Contains("sign in")||lower.Contains("cookies"))return "This video needs a valid signed-in session. Sign in on its site in Chrome, enable Use this site's signed-in Chrome session, and send a new download.\n\n"+message;
        if(lower.Contains("unsupported url")||lower.Contains("unable to extract"))return "The downloader could not extract this page. Open the individual video/reel or try a detected direct media link; site support may require an update.\n\n"+message;
        return message;
    }
}
