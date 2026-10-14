import subprocess
from pathlib import Path
root=Path(__file__).resolve().parents[1]
startup=subprocess.STARTUPINFO();startup.dwFlags=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=0
result=subprocess.run([str(root/'bin/LocalDownload.exe'),'--preview',str(root/'preview.png')],startupinfo=startup,timeout=15)
print('Preview exit:',result.returncode,'PNG:',(root/'preview.png').exists())
