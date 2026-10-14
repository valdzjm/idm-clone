import json,subprocess
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
root=Path(__file__).resolve().parents[1];out=root/'tests/output';config=json.loads((root/'bin/tools/config.json').read_text(encoding='utf-8-sig'))
samples=[x for x in json.loads((out/'social-probes.json').read_text()) if x.get('exit')==0]
def fetch(item):
    name=item['name'];dest=out/(name+'.mp4');job=out/(name+'-job.json')
    if dest.exists():dest.unlink()
    job.write_text(json.dumps({'Kind':'page','Url':item['url'],'Name':dest.name,'Path':str(dest),'Total':-1}))
    process=subprocess.run([str(root/'bin/LocalDownload.v03.exe'),'--fetch',str(job)],timeout=180)
    state=json.loads(Path(str(job)+'.result.json').read_text());assert process.returncode==0,state
    probe=subprocess.run([str(Path(config['ffmpeg']).with_name('ffprobe.exe')),'-v','error','-show_entries','stream=codec_type,codec_name','-show_entries','format=duration','-of','json',str(dest)],capture_output=True,text=True,timeout=20)
    info=json.loads(probe.stdout);assert {'video','audio'}<={s['codec_type'] for s in info['streams']},info
    result={'name':name,'state':state['State'],'bytes':dest.stat().st_size,'probe':info};print(json.dumps(result),flush=True);return result
with ThreadPoolExecutor(max_workers=2) as pool:results=list(pool.map(fetch,samples))
(out/'social-download-verification.json').write_text(json.dumps(results,indent=2))
print('PASS',len(results),'supplied public social videos downloaded with video and audio')
