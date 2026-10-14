"""Read-only IDM inspection. Extracts extension only into this workspace."""
import hashlib, io, json, re, struct, subprocess, zipfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(r'C:\Program Files (x86)\Internet Download Manager')
HERE = Path(__file__).resolve().parent
OUT = HERE / 'inspection'
OUT.mkdir(exist_ok=True)
REA = HERE.parent / 'local-video-editor/.rea-cache/_npx/dea9475797100b20/node_modules/rea-agents/scripts/rea.mjs'

def pe_info(path):
    data = path.read_bytes(); pe = struct.unpack_from('<I', data, 0x3c)[0]
    machine, count, _, _, _, optsize, flags = struct.unpack_from('<HHIIIHH', data, pe + 4)
    opt = pe + 24; sections = []
    for i in range(count):
        p = opt + optsize + i * 40
        vs, va, rs, raw = struct.unpack_from('<IIII', data, p + 8)
        sections.append((va, max(vs,rs), raw))
    def offset(va):
        for start,size,raw in sections:
            if start <= va < start+size: return raw+va-start
        raise ValueError('Unmapped RVA')
    directory = opt + (112 if machine == 0x8664 else 96)
    imports = []; rva = struct.unpack_from('<I',data,directory+8)[0]
    if rva:
        p=offset(rva)
        while any(data[p:p+20]):
            q=offset(struct.unpack_from('<I',data,p+12)[0]); imports.append(data[q:data.index(b'\0',q)].decode(errors='replace')); p+=20
    strings=[]
    for m in re.finditer(rb'[\x20-\x7e]{5,180}',data):
        s=m.group().decode()
        if re.search(r'MFC|Afx|CDialog|native|chrome|Download this|Http|Range|CreateWindow',s,re.I):
            strings.append({'offset':m.start(),'text':s})
    return {'file':path.name,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),'machine':hex(machine),'imports':imports,'strings':strings[:150]}

(OUT/'inventory.json').write_text(json.dumps([{'name':p.name,'bytes':p.stat().st_size} for p in ROOT.iterdir() if p.is_file()],indent=2))
(OUT/'pe-findings.json').write_text(json.dumps([pe_info(ROOT/n) for n in ['IDMan.exe','IDMMsgHost.exe','idmBroker.exe','IDMNetMon.dll']],indent=2))
crx=(ROOT/'IDMGCExt.crx').read_bytes(); version=struct.unpack_from('<I',crx,4)[0]
start=12+struct.unpack_from('<I',crx,8)[0] if version==3 else 16+sum(struct.unpack_from('<II',crx,8))
ext=OUT/'idm-chrome'; ext.mkdir(exist_ok=True)
with zipfile.ZipFile(io.BytesIO(crx[start:])) as z:
    for entry in z.infolist():
        dest=(ext/entry.filename).resolve()
        if not dest.is_relative_to(ext.resolve()): raise ValueError('Unsafe archive path')
        if not entry.is_dir(): dest.parent.mkdir(parents=True,exist_ok=True); dest.write_bytes(z.read(entry))
commands=[('rea-artifact',['inspect-artifact',str(ROOT/'IDMan.exe'),'--json']),
          ('rea-resources',['inspect-pe-resources',str(ROOT/'IDMan.exe'),'--json']),
          ('rea-native',['analyze',str(ROOT/'IDMan.exe'),'--provider','ghidra','--json']),
          ('rea-extension',['analyze-javascript-application',str(ext),'--json'])]
def run(item):
    name,args=item
    try:
        r=subprocess.run(['node',str(REA),*args],capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=55)
        (OUT/(name+'.json')).write_text(r.stdout,encoding='utf-8'); (OUT/(name+'.stderr.txt')).write_text(r.stderr,encoding='utf-8')
        return {'name':name,'args':args,'exit_code':r.returncode,'bytes':len(r.stdout)}
    except subprocess.TimeoutExpired: return {'name':name,'status':'timeout after 55 seconds'}
with ThreadPoolExecutor(max_workers=4) as pool:
    receipts=list(pool.map(run,commands))
(OUT/'receipts.json').write_text(json.dumps({'rea':'6.4.0','commands':receipts},indent=2))
print(json.dumps(receipts,indent=2))
print('PE summary:')
for item in json.loads((OUT/'pe-findings.json').read_text()): print(item['file'],item['machine'],item['imports'])
print('Extension manifest:',(ext/'manifest.json').read_text())
