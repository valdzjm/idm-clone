import json,subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[1]
config=json.loads((root/'bin/tools/config.json').read_text(encoding='utf-8-sig'))
result=subprocess.run([config['ytDlp'],'--ignore-config','--no-playlist','--simulate','--socket-timeout','15','--retries','1','--js-runtimes','node:'+config['node'],'--ffmpeg-location',config['ffmpeg'],'--format','bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]','--print','%(title)s | duration=%(duration)s | format=%(format_id)s','--','https://www.youtube.com/watch?v=JIIBErjcyHU'],text=True,encoding='utf-8',errors='replace',timeout=120)
print('Exit:',result.returncode)
