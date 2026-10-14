import json,subprocess,threading,shutil
from pathlib import Path
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
root=Path(__file__).resolve().parents[1];out=root/'tests/output/page-engine';out.mkdir(parents=True,exist_ok=True)
config=json.loads((root/'bin/tools/config.json').read_text(encoding='utf-8-sig'))
source=out/'fixture.mp4'
subprocess.run([config['ffmpeg'],'-y','-loglevel','error','-f','lavfi','-i','testsrc=size=160x90:rate=15','-f','lavfi','-i','sine=frequency=440','-t','2','-c:v','libx264','-pix_fmt','yuv420p','-c:a','aac',str(source)],check=True,timeout=30)
subprocess.run([config['ffmpeg'],'-y','-loglevel','error','-i',str(source),'-c','copy','-hls_time','1','-hls_list_size','0','-hls_segment_filename',str(out/'segment-%03d.ts'),str(out/'stream.m3u8')],check=True,timeout=30)
seen=[]
class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args):pass
    def do_GET(self):
        cookie=self.headers.get('Cookie','');seen.append((self.path,cookie))
        if 'session=fixture' not in cookie:self.send_response(403);self.end_headers();return
        if self.path=='/watch':data=b'<html><title>Owned fixture</title><video controls src="/asset.mp4"></video></html>';mime='text/html'
        elif self.path=='/asset.mp4':data=source.read_bytes();mime='video/mp4'
        elif self.path=='/stream.m3u8':data=(out/'stream.m3u8').read_bytes();mime='application/vnd.apple.mpegurl'
        elif self.path.startswith('/segment-') and self.path.endswith('.ts'):data=(out/Path(self.path).name).read_bytes();mime='video/mp2t'
        else:self.send_response(404);self.end_headers();return
        self.send_response(200);self.send_header('Content-Type',mime);self.send_header('Content-Length',str(len(data)));self.end_headers();self.wfile.write(data)
server=ThreadingHTTPServer(('127.0.0.1',0),Handler);threading.Thread(target=server.serve_forever,daemon=True).start()
exe=out/'PageHarness.exe';compiler=r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
subprocess.run([compiler,'/nologo','/target:exe',f'/out:{exe}','/r:System.Net.Http.dll','/r:System.Web.Extensions.dll','/r:System.Core.dll','/r:System.Security.dll',str(root/'desktop/Core.cs'),str(root/'desktop/VideoPage.cs'),str(root/'tests/PageHarness.cs')],check=True)
(out/'tools').mkdir(exist_ok=True);shutil.copyfile(root/'bin/tools/config.json',out/'tools/config.json')
request=out/'request.json';request.write_text(json.dumps({'op':'download','kind':'page','url':f'http://127.0.0.1:{server.server_port}/watch','cookies':[{'domain':'127.0.0.1','path':'/','name':'session','value':'fixture','secure':False,'httpOnly':True,'hostOnly':True,'expirationDate':0}]}))
dest=out/'complete.mp4'
if dest.exists():dest.unlink()
try:
    result=subprocess.run([str(exe),str(request),str(dest)],timeout=50)
    state=json.loads(Path(str(request)+'.result.json').read_text());assert result.returncode==0,state
    assert dest.read_bytes()==source.read_bytes(),'Downloaded bytes differed'
    assert any(path=='/watch' and 'session=fixture' in cookie for path,cookie in seen),'Page session missing'
    assert any(path=='/asset.mp4' and 'session=fixture' in cookie for path,cookie in seen),'Media session missing'
    assert not list((out/'data').rglob('session-*.txt')),'Temporary cookies were not deleted'
    assert 'Cookies' not in Path(str(request)+'.result.json').read_text(),'Cookies leaked into persisted history'
    print('PASS generic page extraction, signed-in session forwarding, MP4 integrity, temporary-cookie cleanup, and history privacy')
    hls_request=out/'hls-request.json';hls=json.loads(request.read_text());hls['url']=f'http://127.0.0.1:{server.server_port}/stream.m3u8';hls_request.write_text(json.dumps(hls));hls_dest=out/'hls-complete.mp4'
    if hls_dest.exists():hls_dest.unlink()
    result=subprocess.run([str(exe),str(hls_request),str(hls_dest)],timeout=50);state=json.loads(Path(str(hls_request)+'.result.json').read_text());assert result.returncode==0,state
    probe=subprocess.run([str(Path(config['ffmpeg']).with_name('ffprobe.exe')),'-v','error','-show_entries','stream=codec_type','-of','json',str(hls_dest)],capture_output=True,text=True,timeout=20);info=json.loads(probe.stdout);assert {'video','audio'}<={x['codec_type'] for x in info['streams']},info
    assert not list((out/'data').rglob('session-*.txt')),'HLS session was not cleaned up'
    print('PASS authenticated HLS playlist/segment downloads and MP4 assembly with video and audio')
finally:server.shutdown()
