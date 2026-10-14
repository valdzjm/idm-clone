import subprocess,threading,json
from pathlib import Path
from http.server import ThreadingHTTPServer,BaseHTTPRequestHandler
root=Path(__file__).resolve().parents[1];out=root/'tests/output/ui-behavior';out.mkdir(parents=True,exist_ok=True)
data=b'OWNED_DOWNLOAD_FIXTURE'*2048;ranges=[]
class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args):pass
    def do_GET(self):
        ranges.append(self.headers.get('Range'));self.send_response(200);self.send_header('Content-Type','video/mp4');self.send_header('Content-Length',str(len(data)));self.send_header('ETag','"fixture"');self.end_headers();self.wfile.write(data)
server=ThreadingHTTPServer(('127.0.0.1',0),Handler);threading.Thread(target=server.serve_forever,daemon=True).start()
exe=out/'UiHarness.exe';compiler=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
subprocess.run([compiler,'/nologo','/target:exe','/main:UiHarness',f'/out:{exe}','/r:System.Net.Http.dll','/r:System.Web.Extensions.dll','/r:System.Core.dll','/r:System.Security.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll',*[str(root/'desktop'/name) for name in ['Core.cs','Desktop.cs','VideoPage.cs','WindowFocus.cs']],str(root/'tests/UiHarness.cs')],check=True)
for file in [out/'redownload.mp4',out/'redownload (1).mp4',out/'data/downloads.json']:
    if file.exists():file.unlink()
try:
    process=subprocess.run([str(exe),f'http://127.0.0.1:{server.server_port}/video.mp4'],capture_output=True,text=True,timeout=25)
    print(process.stdout);print(process.stderr);assert process.returncode==0
    assert ranges==[None,None],ranges
    assert (out/'redownload.mp4').read_bytes()==data and (out/'redownload (1).mp4').read_bytes()==data
    (out/'verification.json').write_text(json.dumps({'status':'passed','fresh_http_requests':len(ranges),'checks':process.stdout.splitlines()},indent=2))
finally:server.shutdown()
