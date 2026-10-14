using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using LocalDownload;

class NativeHost {
    [DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(int processId);
    static byte[] Read(Stream stream, int size) {
        byte[] bytes = new byte[size]; int read = 0;
        while (read < size) { int n = stream.Read(bytes,read,size-read); if (n == 0) throw new EndOfStreamException("Truncated native message."); read += n; }
        return bytes;
    }
    static void Reply(object value) {
        byte[] bytes = Encoding.UTF8.GetBytes(Core.Json().Serialize(value));
        var stream = Console.OpenStandardOutput(); byte[] length = BitConverter.GetBytes(bytes.Length);
        stream.Write(length,0,4); stream.Write(bytes,0,bytes.Length); stream.Flush();
    }
    static int Main(string[] args) {
        try {
            // In production Chrome supplies the originating extension URL.
            if (args.Length == 0 || (!args[0].StartsWith("chrome-extension://") && args[0] != "--test")) throw new Exception("Expected a Chrome extension origin.");
            if (args[0] != "--test") {
                string manifestPath = Path.Combine(Core.Root,"host.json");
                var manifest = Core.Json().Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(manifestPath));
                var origins = (System.Collections.ArrayList)manifest["allowed_origins"];
                if (!origins.Contains(args[0])) throw new Exception("Extension origin is not registered.");
            }
            var input = Console.OpenStandardInput(); int size = BitConverter.ToInt32(Read(input,4),0);
            if (size < 2 || size > 1024 * 1024) throw new Exception("Invalid native message size.");
            Request request = Core.Json().Deserialize<Request>(Encoding.UTF8.GetString(Read(input,size)));
            if (request.op == "ping") { Reply(new {ok=true,version="0.4",support="direct-files,page-downloads,site-session,story-media"}); return 0; }
            Core.Validate(request);
            string versionedDesktop=Path.Combine(Core.Root,"LocalDownload.v04.exe");
            if(File.Exists(versionedDesktop))foreach(string oldName in new[]{"LocalDownload","LocalDownload.v02","LocalDownload.v03"})foreach(var old in Process.GetProcessesByName(oldName)){
                try{if(String.Equals(old.MainModule.FileName,Path.Combine(Core.Root,oldName+".exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Close the old Local Download Manager and open LocalDownload.v04.exe before downloading.");}catch(System.ComponentModel.Win32Exception){}finally{old.Dispose();}
            }
            string desktopPath=File.Exists(versionedDesktop)?versionedDesktop:Path.Combine(Core.Root,"LocalDownload.exe");
            foreach(var running in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(desktopPath)))try{if(String.Equals(running.MainModule.FileName,desktopPath,StringComparison.OrdinalIgnoreCase))AllowSetForegroundWindow(running.Id);}catch(System.ComponentModel.Win32Exception){}finally{running.Dispose();}
            Directory.CreateDirectory(Core.Inbox);
            string temp = Path.Combine(Core.Inbox,Guid.NewGuid().ToString("N") + ".tmp");
            byte[] packet=Encoding.UTF8.GetBytes(Core.Json().Serialize(request));
            File.WriteAllBytes(temp,System.Security.Cryptography.ProtectedData.Protect(packet,null,System.Security.Cryptography.DataProtectionScope.CurrentUser));
            File.Move(temp,Path.ChangeExtension(temp,"request"));
            Mutex mutex;
            if (!Mutex.TryOpenExisting(Core.MutexName,out mutex)) {using(var started=Process.Start(new ProcessStartInfo(desktopPath) {UseShellExecute=true}))AllowSetForegroundWindow(started.Id);}
            else mutex.Dispose();
            Reply(new {ok=true,message="Sent to desktop. Confirm the save location there."}); return 0;
        } catch (Exception error) { Reply(new {ok=false,error=error.Message}); return 1; }
    }
}
