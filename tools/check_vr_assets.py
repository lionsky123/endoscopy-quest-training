"""Read-only checks for the C09 VR scene and resource publication. Does not launch Unity."""
from pathlib import Path
import hashlib
import json
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / 'app'
ROOM = APP / 'Assets/EndoscopyTheme/Resources/EndoscopyRoom'


def blocks(path):
    return re.split(r'(?=^--- !u!)', path.read_text(encoding='utf-8'), flags=re.M)


def main():
    scene = blocks(APP/'Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity')
    for name in ('MRUK', "'[BuildingBlock] Passthrough'"):
        found = [b for b in scene if f'  m_Name: {name}\n' in b]
        assert len(found) == 1 and '  m_IsActive: 0\n' in found[0], name
    for b in scene:
        if b.startswith('--- !u!20 '):
            assert 'm_ClearFlags: 2' in b and 'a: 1}' in b, 'Opaque camera'
        assert 'isInsightPassthroughEnabled: 1' not in b
    gateway = [b for b in scene if b.startswith('--- !u!1001 ') and 'guid: f885cab0732406d408f6dd3342a116d2' in b]
    assert len(gateway) == 1 and 'propertyPath: m_IsActive\n      value: 0' in gateway[0], 'MR mode gateway disabled'
    prefab = blocks(APP/'Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab')
    for name in ('MetaSpatialDataPermissionGate','MetaPhysicalAnchorLocator','QrRecognitionSourceAdapter'):
        found = [b for b in prefab if b.startswith('--- !u!114 ') and re.search(r'\.'+name+r'\n', b)]
        assert len(found) == 1 and 'm_Enabled: 0' in found[0], name
    options=(APP/'Assets/BotanicalGardenQR/Content/Authoring/RuntimeEnvironmentOptions.asset').read_text()
    assert '_virtualRoomEnabled: 1' in options and '_arrivalRadius: 0.35' in options
    project=(APP/'Assets/Oculus/OculusProjectConfig.asset').read_text()
    for flag in ('anchorSupport','sceneSupport','_insightPassthroughSupport','_systemLoadingScreenBackground'):
        assert f'  {flag}: 0\n' in project, flag
    settings=(APP/'ProjectSettings/EditorBuildSettings.asset').read_text()
    assert settings.count('  - enabled: 1\n') == 1
    assert '- enabled: 0\n    path: Assets/BotanicalGardenQR/Scenes/Admin/' in settings
    profile=(APP/'Assets/Settings/Build Profiles/Meta Quest.asset').read_text()
    assert 'm_BuildTarget: 13' in profile and '- m_enabled: 0\n    m_path: Assets/BotanicalGardenQR/Scenes/Admin/' in profile
    manifest=ET.parse(APP/'Assets/Plugins/Android/AndroidManifest.xml').getroot()
    android='{http://schemas.android.com/apk/res/android}'
    banned={'com.oculus.permission.USE_SCENE','com.oculus.permission.USE_ANCHOR_API','com.oculus.permission.PLANE_TRACKING','com.oculus.feature.PASSTHROUGH'}
    assert not any(item.attrib.get(android+'name') in banned for item in manifest)
    assert manifest.attrib['package'] == 'com.endoscopy.inspection'
    assert 'm_DisableAudio: 1' in (APP/'ProjectSettings/AudioManager.asset').read_text()
    audio_rooms = [b for b in scene if 'guid: dc3115f71c5841078600752259d33fd1' in b]
    assert len(audio_rooms) == 1 and 'm_Enabled: 0' in audio_rooms[0], 'Disable native acoustic updates in the silent scene'
    definition=json.loads((APP/'Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json').read_text())
    assert definition['modelDigest']==hashlib.sha256((ROOM/'geometry.bytes').read_bytes()).hexdigest()
    assert definition['sourceDigest']==hashlib.sha256((APP/'Tools/Authoring/visitor-map.json').read_bytes()).hexdigest()
    assert definition['fairyJoinRadius'] == 1.5 and definition['departureRadius'] == .55
    manifest=json.loads((ROOM/'manifest.json').read_text(encoding='utf-8'))
    for material in manifest['materials']:
        if material['texture']: assert (ROOM/(material['texture']+'.png')).is_file()
    for asset in ROOM.rglob('*'):
        if asset.is_file() and asset.suffix != '.meta': assert Path(str(asset)+'.meta').exists(), asset
    # Reused archived texture GUIDs must not collide with any active project asset.
    guids={}
    room_guids=set()
    for meta in (APP/'Assets').rglob('*.meta'):
        m=re.search(r'^guid: ([a-f0-9]{32})$',meta.read_text(encoding='utf-8-sig'),re.M)
        if not m: continue
        guid=m.group(1);guids.setdefault(guid,[]).append(meta)
        if ROOM in meta.parents: room_guids.add(guid)
    assert all(len(guids[g]) == 1 for g in room_guids), 'Room asset GUID collision'
    print('PASS: opaque VR cameras, MR services/gateway disabled, no MR permissions, Android profile, single Visitor scene, mute, room hashes/textures/metas/GUIDs.')


if __name__ == '__main__': main()
