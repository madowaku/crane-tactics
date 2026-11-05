"""Record source and exported byte identity; this is not game-dev package validation."""
import hashlib, json, pathlib
root=pathlib.Path(__file__).resolve().parents[1]
paths=[root/'ArtSource/create_toys.py', root/'ArtSource/toys.blend', *sorted((root/'Assets/_CraneTactics/Art').glob('*.fbx'))]
result={'origin':'Original procedural Blender study meshes for CRANE TACTICS','generator':'Blender 5.2.1 LTS','units':'metres','format':'FBX','reference':'Owner Knight reference retained locally, excluded from git','verification_boundary':'File SHA-256 and byte count only; Unity scale/axis/runtime evidence is recorded separately','files':[{'path':p.relative_to(root).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in paths]}
(root/'ArtSource/export-manifest.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
