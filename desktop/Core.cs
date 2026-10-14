using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LocalDownload {
public class BrowserCookie {
    public string domain {get;set;} public string path {get;set;} public string name {get;set;} public string value {get;set;}
    public bool secure {get;set;} public bool httpOnly {get;set;} public bool hostOnly {get;set;} public double expirationDate {get;set;}
}
public class Request {
    public string op { get; set; }
    public string url { get; set; }
    public string title { get; set; }
    public string referer { get; set; }
    public string cookie { get; set; }
    public string userAgent { get; set; }
    public string kind { get; set; }
    public string sourceUrl {get;set;}
    public List<BrowserCookie> cookies {get;set;}
}
public class Job {
    public string Id { get; set; }
    public string Url { get; set; }
    public string Name { get; set; }
    public string Path { get; set; }
    public string State { get; set; }
    public string Error { get; set; }
    public string Validator { get; set; }
    public string Referer { get; set; }
    public string Kind { get; set; }
    public string SourceUrl {get;set;}
    public long Bytes { get; set; }
    public long Total { get; set; }
    [ScriptIgnore] public string Cookie { get; set; }
    [ScriptIgnore] public string UserAgent { get; set; }
    [ScriptIgnore] public List<BrowserCookie> Cookies {get;set;}
    [ScriptIgnore] public double Speed { get; set; }
    [ScriptIgnore] public CancellationTokenSource Cancellation;
    [ScriptIgnore] public Task Running;
    [ScriptIgnore] public object Gate = new object();
}
public static class Core {
    public static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    public static readonly string Data = System.IO.Path.Combine(Root, "data");
    public static readonly string Inbox = System.IO.Path.Combine(Data, "inbox");
    public static readonly string MutexName = MutexId();
    static string MutexId() {
        using(var sha=System.Security.Cryptography.SHA256.Create()) return "LocalDownloadManager-" + BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Root.ToLowerInvariant()))).Replace("-","").Substring(0,24);
    }
    public static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 }; }
    public static void Validate(Request request) {
        Uri uri;
        if (request == null || request.op != "download" || !Uri.TryCreate(request.url, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") || !String.IsNullOrEmpty(uri.UserInfo))
            throw new Exception("Select an HTTP or HTTPS media URL.");
        if (request.url.Length > 16384) throw new Exception("URL is too long.");
        if(!String.IsNullOrEmpty(request.sourceUrl)){Uri source;if(request.sourceUrl.Length>16384||!Uri.TryCreate(request.sourceUrl,UriKind.Absolute,out source)||(source.Scheme!="http"&&source.Scheme!="https")||!String.IsNullOrEmpty(source.UserInfo))throw new Exception("Invalid source page URL.");}
        foreach (string header in new[] {request.cookie, request.referer, request.userAgent})
            if (header != null && (header.IndexOfAny(new[] {'\r','\n'}) >= 0 || header.Length > 65536)) throw new Exception("Invalid request header.");
        if(request.cookies!=null) {
            if(request.cookies.Count>1000)throw new Exception("Too many session cookies.");
            foreach(var cookie in request.cookies) {
                if(cookie==null||String.IsNullOrEmpty(cookie.domain)||String.IsNullOrEmpty(cookie.name))throw new Exception("Invalid session cookie.");
                foreach(string text in new[]{cookie.domain,cookie.path,cookie.name,cookie.value})if(text!=null&&(text.Length>65536||text.IndexOfAny(new[]{'\r','\n','\t','\0'})>=0))throw new Exception("Invalid session cookie.");
                string domain=cookie.domain.TrimStart('.').ToLowerInvariant();
                if(Uri.CheckHostName(domain)==UriHostNameType.Unknown || (uri.Host!=domain&&!uri.Host.EndsWith("."+domain,StringComparison.OrdinalIgnoreCase)))throw new Exception("Session cookies must belong to the selected video site.");
                if(Double.IsNaN(cookie.expirationDate)||Double.IsInfinity(cookie.expirationDate)||cookie.expirationDate<0||cookie.expirationDate>253402300799)throw new Exception("Invalid cookie expiration.");
            }
        }
        if(request.kind=="page")return;
        if (request.kind == "youtube") {
            if (uri.Scheme != "https" || uri.Host != "www.youtube.com" || uri.AbsolutePath != "/watch" || !System.Text.RegularExpressions.Regex.IsMatch(uri.Query,@"^\?v=[A-Za-z0-9_-]{11}$")) throw new Exception("Invalid YouTube video URL.");
            return;
        }
        if (request.kind == "stream") throw new Exception("Use Download YouTube video to combine the tracks.");
        if (request.kind == "manifest" || uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || uri.AbsolutePath.EndsWith(".mpd", StringComparison.OrdinalIgnoreCase))
            throw new Exception("This build supports direct video files. HLS/DASH playlists require a separate stream assembly engine.");
    }
    public static string Filename(string url) {
        string name = Uri.UnescapeDataString(new Uri(url).AbsolutePath);
        name = System.IO.Path.GetFileName(name);
        foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        if (String.IsNullOrWhiteSpace(name)) name = "video.mp4";
        if (name.Length > 120) name = name.Substring(0,120);
        return name;
    }
    public static string VideoFilename(Request request) {
        string name=String.IsNullOrWhiteSpace(request.title)?"Website video":request.title;
        foreach(char c in System.IO.Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
        name=name.Trim().TrimEnd('.');if(name.Length>100)name=name.Substring(0,100);
        if(String.IsNullOrWhiteSpace(name))name="Website video";
        return name+".mp4";
    }
    public static void PrepareRedownload(Job job) {
        if(job.Running!=null&&!job.Running.IsCompleted)throw new Exception("Stop this download before starting a fresh download.");
        if(job.Cancellation!=null){job.Cancellation.Dispose();job.Cancellation=null;}
        string original=job.Path;string folder=System.IO.Path.GetDirectoryName(original);string stem=System.IO.Path.GetFileNameWithoutExtension(original);string extension=System.IO.Path.GetExtension(original);int copy=1;
        while(File.Exists(job.Path)||File.Exists(job.Path+".part"))job.Path=System.IO.Path.Combine(folder,stem+" ("+(copy++)+")"+extension);
        job.Name=System.IO.Path.GetFileName(job.Path);job.Id=Guid.NewGuid().ToString("N");job.Validator=null;job.Bytes=0;job.Total=-1;job.Speed=0;job.Error="";job.State="Stopped";job.Running=null;
    }
    public static async Task Download(Job job) {
        var token = job.Cancellation.Token;
        try {
            job.State = "Connecting"; job.Error = "";
            long offset = File.Exists(job.Path + ".part") ? new FileInfo(job.Path + ".part").Length : 0;
            // Never concatenate different remote representations without a validator.
            if (offset > 0 && String.IsNullOrEmpty(job.Validator)) offset = 0;
            var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, AutomaticDecompression = DecompressionMethods.None };
            using (handler) using (var client = new HttpClient(handler)) {
                client.Timeout = TimeSpan.FromHours(24);
                var original = new Uri(job.Url); var uri = original;
                HttpResponseMessage response = null;
                try {
                    for (int redirect = 0; redirect <= 10; redirect++) {
                        token.ThrowIfCancellationRequested();
                        using (var message = new HttpRequestMessage(HttpMethod.Get, uri)) {
                            message.Headers.TryAddWithoutValidation("User-Agent", String.IsNullOrWhiteSpace(job.UserAgent) ? "LocalDownload/0.1" : job.UserAgent);
                            message.Headers.TryAddWithoutValidation("Accept-Encoding", "identity");
                            if (!String.IsNullOrWhiteSpace(job.Referer)) message.Headers.TryAddWithoutValidation("Referer", job.Referer);
                            // Credentials belong only to the exact origin selected in Chrome.
                            if (uri.GetLeftPart(UriPartial.Authority) == original.GetLeftPart(UriPartial.Authority) && !String.IsNullOrWhiteSpace(job.Cookie)) message.Headers.TryAddWithoutValidation("Cookie", job.Cookie);
                            if (offset > 0) {
                                message.Headers.Range = new RangeHeaderValue(offset, null);
                                message.Headers.TryAddWithoutValidation("If-Range", job.Validator);
                            }
                            response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                        }
                        int code = (int)response.StatusCode;
                        if (code >= 300 && code <= 399 && response.Headers.Location != null) {
                            if (redirect == 10) throw new Exception("Too many redirects.");
                            var next = new Uri(uri, response.Headers.Location);
                            if ((next.Scheme != "http" && next.Scheme != "https") || !String.IsNullOrEmpty(next.UserInfo) || (uri.Scheme == "https" && next.Scheme == "http")) throw new Exception("Unsafe redirect.");
                            uri = next; response.Dispose(); response = null; continue;
                        }
                        break;
                    }
                    response.EnsureSuccessStatusCode();
                    string media = response.Content.Headers.ContentType == null ? "" : response.Content.Headers.ContentType.MediaType.ToLowerInvariant();
                    if (media.Contains("mpegurl") || media.Contains("dash+xml") || media.Contains("text/html")) throw new Exception("The server returned a playlist or web page, rather than a direct media file.");
                    if (response.Content.Headers.ContentEncoding.Count > 0) throw new Exception("The server returned compressed transport data. Choose a direct media URL.");
                    bool partial = response.StatusCode == HttpStatusCode.PartialContent;
                    var range = response.Content.Headers.ContentRange;
                    string validator = response.Headers.ETag != null && !response.Headers.ETag.IsWeak ? response.Headers.ETag.ToString() :
                        (response.Content.Headers.LastModified.HasValue ? response.Content.Headers.LastModified.Value.ToString("r") : null);
                    if (partial && (range == null || range.Unit != "bytes" || range.From != offset || !range.To.HasValue || !range.Length.HasValue || range.To.Value != range.Length.Value - 1))
                        throw new Exception("Server returned an inconsistent byte range. Resume was stopped to protect the file.");
                    if (partial && offset > 0 && validator != job.Validator) throw new Exception("Remote file changed. Delete the partial file before restarting.");
                    if (!partial) offset = 0; // A server may ignore Range: restart rather than append.
                    job.Validator = validator;
                    long total = partial ? range.Length.Value : (response.Content.Headers.ContentLength ?? -1);
                    job.Total = total; job.Bytes = offset; job.State = "Downloading";
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(job.Path));
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new FileStream(job.Path + ".part", offset > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read)) {
                        var buffer = new byte[65536]; var watch = System.Diagnostics.Stopwatch.StartNew(); long sessionBytes = 0;
                        while (true) {
                            int count = await input.ReadAsync(buffer,0,buffer.Length,token).ConfigureAwait(false);
                            if (count == 0) break;
                            await output.WriteAsync(buffer,0,count,token).ConfigureAwait(false);
                            job.Bytes += count; sessionBytes += count; job.Speed = sessionBytes / Math.Max(0.001,watch.Elapsed.TotalSeconds);
                        }
                        output.Flush(true);
                    }
                    token.ThrowIfCancellationRequested();
                    if (total >= 0 && job.Bytes != total) throw new Exception("Download ended before the expected file size was received.");
                    // File.Move refuses an existing destination, preserving the user's files.
                    File.Move(job.Path + ".part", job.Path); job.State = "Complete";
                } finally { if (response != null) response.Dispose(); }
            }
        } catch (OperationCanceledException) { job.State = "Stopped"; }
        catch (Exception error) { job.Error = error.Message; job.State = "Error"; }
        finally { job.Speed = 0; }
    }
}
}
