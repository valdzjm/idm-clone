import json,subprocess
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
root=Path(__file__).resolve().parents[1];out=root/'tests/output';out.mkdir(exist_ok=True)
config=json.loads((root/'bin/tools/config.json').read_text(encoding='utf-8-sig'))
samples={
 'instagram-reel':'https://www.instagram.com/reels/DdgUlmZikAU/',
 'instagram-story':'https://www.instagram.com/stories/jdwnsllz/',
 'facebook-story':'https://www.facebook.com/stories/1986321208045496/UzpfSVNDOjE0MDU5MzU1NDE3NDk2NzQ=/?bucket_count=9',
 'facebook-reel':'https://www.facebook.com/reel/1822430045852437',
 'facebook-video':'https://www.facebook.com/61590365856864/videos/pcb.122137551111345528/1471882738092739',
 'youtube-short':'https://www.youtube.com/shorts/TlRlDNHdmvk'
}
def probe(item):
    name,url=item
    try:
        cmd=[config['ytDlp'],'--ignore-config','--no-playlist','--playlist-items','1','--simulate','--socket-timeout','10','--retries','0','--extractor-retries','0','--js-runtimes','node:'+config['node'],'--ffmpeg-location',config['ffmpeg'],'--format','bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]/bv*+ba/bv*/b','--print','%(title)s | duration=%(duration)s | format=%(format_id)s','--',url]
        result=subprocess.run(cmd,capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=50)
        record={'name':name,'url':url,'exit':result.returncode,'summary':result.stdout.strip()[-1000:],'diagnostic':result.stderr.strip()[-2200:]}
    except subprocess.TimeoutExpired:record={'name':name,'url':url,'status':'timeout after 50 seconds'}
    print(json.dumps(record,ensure_ascii=True),flush=True);return record
with ThreadPoolExecutor(max_workers=3) as pool:results=list(pool.map(probe,samples.items()))
(out/'social-probes.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
