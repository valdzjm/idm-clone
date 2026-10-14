"""Deterministic local HTTP integration tests of the compiled download engine."""
import hashlib, json, struct, subprocess, threading, time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'tests'/'output';OUT.mkdir(exist_ok=True)
EXE=ROOT/'bin'/'LocalDownload.exe';HOST=ROOT/'bin'/'LocalDownloadHost.exe'
if (ROOT/'bin/LocalDownload.v02.exe').exists():EXE=ROOT/'bin/LocalDownload.v02.exe';HOST=ROOT/'bin/LocalDownloadHost.v02.exe'
if (ROOT/'bin/LocalDownload.v03.exe').exists():EXE=ROOT/'bin/LocalDownload.v03.exe';HOST=ROOT/'bin/LocalDownloadHost.v03.exe'
if (ROOT/'bin/LocalDownload.v04.exe').exists():EXE=ROOT/'bin/LocalDownload.v04.exe';HOST=ROOT/'bin/LocalDownloadHost.v04.exe'
DATA=bytes(range(256))*8192
cookies=[]
class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args): pass
    def do_GET(self):
        if self.path=='/redirect':
            self.send_response(302);self.send_header('Location',f'http://localhost:{self.server.server_port}/file');self.end_headers();return
        if self.path=='/file':cookies.append((self.headers.get('Cookie'),self.headers.get('Referer')))
        html=self.path=='/html';data=b'<html>not a video</html>' if html else DATA
        start=0;code=200
        if self.headers.get('Range') and self.path!='/ignore':start=int(self.headers['Range'].split('=')[1].split('-')[0]);code=206
        self.send_response(code)
        self.send_header('Content-Type','text/html' if html else 'video/mp4')
        self.send_header('ETag','"different"' if self.path=='/changed' else '"stable"')
        if code==206:self.send_header('Content-Range',f'bytes {start+1 if self.path=="/bad-range" else start}-{len(data)-1}/{len(data)}')
        self.send_header('Content-Length',str(len(data)-start));self.end_headers()
        try:
            for i in range(start,len(data),16384):
                self.wfile.write(data[i:i+16384]);self.wfile.flush()
                if self.path=='/slow':time.sleep(.012)
        except (BrokenPipeError,ConnectionResetError,ConnectionAbortedError):pass

server=ThreadingHTTPServer(('127.0.0.1',0),Handler);threading.Thread(target=server.serve_forever,daemon=True).start()
base=f'http://127.0.0.1:{server.server_port}'
passed=[]
def check(name,condition):
    assert condition,name
    passed.append(name);print('PASS',name,flush=True)
def fetch(name,url,extra=None,cancel=None):
    dest=OUT/(name+'.mp4')
    for path in [dest,Path(str(dest)+'.part')]:
        if path.exists():path.unlink()
    job={'Id':name,'Url':base+url,'Name':dest.name,'Path':str(dest),'Total':-1}
    if extra:job.update(extra)
    path=OUT/(name+'.json');path.write_text(json.dumps(job))
    command=[str(EXE),'--fetch',str(path)]
    if cancel:command.append(str(cancel))
    process=subprocess.run(command,timeout=20)
    result=json.loads(Path(str(path)+'.result.json').read_text())
    return result,process.returncode,dest,path
try:
    result,code,dest,_=fetch('direct','/file')
    check('direct file SHA-256 matches',code==0 and hashlib.sha256(dest.read_bytes()).digest()==hashlib.sha256(DATA).digest())
    result,code,dest,_=fetch('redirect','/redirect',{'Cookie':'secret=value','Referer':base+'/watch'})
    check('cross-origin redirect strips cookies',code==0 and cookies[-1][0] is None)
    result,code,dest,path=fetch('resume','/slow',cancel=250)
    check('cancellation preserves a partial file',code==2 and 0<Path(str(dest)+'.part').stat().st_size<len(DATA))
    path.write_text(json.dumps(result));process=subprocess.run([str(EXE),'--fetch',str(path)],timeout=20)
    check('validated Range resume produces exact original bytes',process.returncode==0 and dest.read_bytes()==DATA)
    for name,url,validator,expect in [('ignored','/ignore','"stable"',0),('changed','/changed','"stable"',1),('bad','/bad-range','"stable"',1)]:
        dest=OUT/(name+'.mp4');partial=Path(str(dest)+'.part');
        if dest.exists():dest.unlink()
        partial.write_bytes(DATA[:123456]);path=OUT/(name+'.json');path.write_text(json.dumps({'Url':base+url,'Path':str(dest),'Validator':validator}))
        process=subprocess.run([str(EXE),'--fetch',str(path)],timeout=20)
        check(name+' range safety',process.returncode==expect and (dest.read_bytes()==DATA if expect==0 else not dest.exists() and partial.read_bytes()==DATA[:123456]))
    result,code,dest,_=fetch('html','/html');check('HTML login pages are rejected',code==1 and not dest.exists())
    result,code,dest,_=fetch('authenticated','/file',{'Cookie':'session=selected'});check('selected-origin cookie forwarded',code==0 and cookies[-1][0]=='session=selected')
    def native(value,arg='--test'):
        payload=json.dumps(value).encode();process=subprocess.run([str(HOST),arg],input=struct.pack('<I',len(payload))+payload,capture_output=True,timeout=10)
        length=struct.unpack('<I',process.stdout[:4])[0];assert len(process.stdout)==length+4
        return json.loads(process.stdout[4:]),process.returncode
    response,code=native({'op':'ping'});check('native messaging framing',code==0 and response['ok'])
    origin='chrome-extension://'+(ROOT/'extension-id.txt').read_text().strip()+'/'
    response,code=native({'op':'ping'},origin);check('registered origin passes',code==0 and response['ok'])
    response,code=native({'op':'ping'},'chrome-extension://'+'a'*32+'/');check('unregistered origin rejected',code==1 and not response['ok'])
    for name,url in [('file scheme','file:///C:/Windows/win.ini'),('playlist','https://example.com/video.m3u8')]:
        response,code=native({'op':'download','url':url});check(name+' rejected',code==1 and not response['ok'])
    for name,cookie in [('foreign domain',{'domain':'.facebook.com','name':'session','value':'fixture'}),('header injection',{'domain':'.example.com','name':'session','value':'fixture\nInjected=true'})]:
        response,code=native({'op':'download','kind':'page','url':'https://www.example.com/video','cookies':[cookie]});check(name+' cookie rejected',code==1 and not response['ok'])
    print('Verified',len(passed),'checks.')
    (OUT/'verification.json').write_text(json.dumps({'passed':passed,'count':len(passed)},indent=2))
finally:server.shutdown()
