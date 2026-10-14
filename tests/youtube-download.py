import json,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[1];out=root/'tests/output';out.mkdir(exist_ok=True)
job=out/'youtube-job.json';dest=out/'FlipTop-test.mp4'
job.write_text(json.dumps({'Id':'99c28358da004d688e7cedbe7c1a357a','Kind':'youtube','Url':'https://www.youtube.com/watch?v=JIIBErjcyHU','Name':dest.name,'Path':str(dest),'Total':-1}))
result=subprocess.run([str(root/'bin/LocalDownload.v02.exe'),'--fetch',str(job)],timeout=240)
state=json.loads(Path(str(job)+'.result.json').read_text());print('State:',state['State'],'Bytes:',state['Bytes']);print('Error:',state.get('Error',''))
if result.returncode!=0:raise SystemExit(result.returncode)
config=json.loads((root/'bin/tools/config.json').read_text(encoding='utf-8-sig'))
ffprobe=Path(config['ffmpeg']).with_name('ffprobe.exe')
probe=subprocess.run([str(ffprobe),'-v','error','-show_entries','stream=codec_type,codec_name','-show_entries','format=duration','-of','json',str(dest)],capture_output=True,text=True,timeout=30)
info=json.loads(probe.stdout);kinds={x['codec_type'] for x in info['streams']}
assert {'video','audio'}<=kinds,info
print('Verified:',json.dumps(info));(out/'youtube-verification.json').write_text(json.dumps(info,indent=2))
