"""Fetch a small, explicitly selected set of CC0 assets from their publishers."""
from pathlib import Path
import json, hashlib, urllib.request, zipfile

ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'05_网络资源'
DEST.mkdir(exist_ok=True)
records=[]
def fetch(url,path,md5=None):
    path.parent.mkdir(parents=True,exist_ok=True)
    if not path.exists():
        with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'EndoscopyTrainingAssetPreparation/1.0'}),timeout=90) as response:
            path.write_bytes(response.read())
    data=path.read_bytes()
    if md5 and hashlib.md5(data).hexdigest()!=md5:raise RuntimeError('Checksum mismatch: '+str(path))
    return {'file':str(path.relative_to(ROOT)),'url':url,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data)}

for asset in ['clipboard','plastic_bottle_gallon']:
    request=urllib.request.Request('https://api.polyhaven.com/files/'+asset,headers={'User-Agent':'EndoscopyTrainingAssetPreparation/1.0'})
    with urllib.request.urlopen(request,timeout=30) as response:info=json.load(response)
    folder=DEST/asset
    folder.mkdir(exist_ok=True)
    (folder/'publisher-files.json').write_text(json.dumps(info,indent=2),encoding='utf-8')
    file=info['gltf']['1k']['gltf']
    downloads=[fetch(file['url'],folder/(asset+'.gltf'),file['md5'])]
    for name,detail in file['include'].items():
        output=(folder/name).resolve()
        if not output.is_relative_to(folder.resolve()):raise ValueError('Unsafe asset path')
        downloads.append(fetch(detail['url'],output,detail['md5']))
    records.append({'asset':asset,'publisher':'Poly Haven','source':'https://polyhaven.com/a/'+asset,'license':'CC0-1.0','license_url':'https://polyhaven.com/license','downloads':downloads})
    print('Downloaded',asset,flush=True)

for asset in ['Metal032','Plastic010']:
    folder=DEST/asset;archive=folder/(asset+'_1K-JPG.zip')
    download=fetch('https://ambientcg.com/get?file='+archive.name,archive)
    with zipfile.ZipFile(archive) as package:
        for member in package.infolist():
            if not (folder/member.filename).resolve().is_relative_to(folder.resolve()):raise ValueError('Unsafe archive path')
        package.extractall(folder)
    records.append({'asset':asset,'publisher':'ambientCG','source':'https://ambientcg.com/a/'+asset,'license':'CC0-1.0','license_url':'https://docs.ambientcg.com/license/','downloads':[download]})
    print('Downloaded',asset,flush=True)
(DEST/'sources.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
