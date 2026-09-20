"""Prepare downloaded teaching media; no Unity launch and no application build.

Inputs are the originals in artifacts/course-media (URLs and licences in the course source manifest).
Requires trimesh, numpy and imageio-ffmpeg. Geometry is converted, never drawn or generated.
"""
from pathlib import Path
import hashlib
import json
import subprocess
import uuid
import trimesh
import imageio_ffmpeg

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artifacts/course-media"
DEST = ROOT / "app/Assets/EndoscopyTheme/Resources/ClinicalCourse"
DEST.mkdir(parents=True, exist_ok=True)
video = SOURCE / "instruments-source.mp4"
assert hashlib.md5(video.read_bytes()).hexdigest() == "1b36f78bb6920bb3faa78091433a6fc6", "Incomplete/unexpected source video"
subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-loglevel", "error", "-i", str(video),
                "-an", "-vf", "scale=960:540", "-r", "30", "-c:v", "libx264", "-profile:v", "baseline",
                "-pix_fmt", "yuv420p", "-crf", "24", "-movflags", "+faststart", str(DEST / "instrument-observation.mp4")], check=True)
scene = trimesh.load(SOURCE / "pill-bottle.glb")
mesh = scene.to_mesh()
mesh.apply_translation(-mesh.bounds.mean(axis=0))
mesh.apply_scale(1 / mesh.extents[1])
(DEST / "inspection-bottle.obj").write_text(trimesh.exchange.obj.export_obj(mesh, include_texture=False, include_color=False), encoding="utf8")
print(json.dumps({"bottle_triangles": len(mesh.faces), "video_bytes": (DEST / "instrument-observation.mp4").stat().st_size,
                  "source_model_sha256": hashlib.sha256((SOURCE / "pill-bottle.glb").read_bytes()).hexdigest()}, indent=2))
for path in [DEST, *DEST.iterdir()]:
    if path.suffix == ".meta":
        continue
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        meta.write_text("fileFormatVersion: 2\nguid: " + uuid.uuid4().hex + ("\nfolderAsset: yes\nDefaultImporter:\n  userData: \n" if path.is_dir() else "\n"), encoding="utf8")
