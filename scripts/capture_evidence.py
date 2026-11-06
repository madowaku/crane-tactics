"""Package actual Unity captures for MADO gameplay-capture, without invented player ratings.

python scripts/capture_evidence.py artifacts/auto-return-capture
Optional: --mgsf-source <checkout>/src validates with the actual MADO service.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

p = argparse.ArgumentParser()
p.add_argument('capture', type=Path)
p.add_argument('--mgsf-source', type=Path)
a = p.parse_args()
root = a.capture.resolve()
rows = [json.loads(line) for line in (root / 'input-results.jsonl').read_text(encoding='utf-8-sig').splitlines()]
media = [dict(json.loads(line)) for line in (root / 'media-times.jsonl').read_text().splitlines()]
for i, item in enumerate(media):
    path = root / item['path']
    if not path.is_file() or path.stat().st_size == 0:
        raise ValueError(f'Missing real capture: {path}')
    item.update(id=f'frame-{i}', kind='frame', bytes=path.stat().st_size,
                sha256=hashlib.sha256(path.read_bytes()).hexdigest())
events = []
for i, row in enumerate(rows):
    events.append(dict(event_id=f'event-{i}', at_ms=round(row['time']*1000),
                       kind='input' if row['action'] in {'approach','axis-locked','release','mode-reset','case-reset'} else 'result',
                       label=row['action'], telemetry=row))
events += [dict(event_id=f'media-{i}', at_ms=m['at_ms'], kind='observation', media_id=m['id'], label=m['path']) for i,m in enumerate(media)]
events.sort(key=lambda row: row['at_ms'])
manifest = dict(version=1, capture_id='crane-auto-return', session_id=root.name,
                source=dict(evidence_class='recorded_gameplay_capture', engine='Unity 6000.3.9f1',
                            input_source='automated development playback', human_playtest=False,
                            commit=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),
                            dirty=bool(subprocess.check_output(['git','status','--porcelain'],text=True).strip())), media=media)
player=Path('Builds/Windows/CraneTactics.exe')
if player.is_file():
    manifest['source']['player_exe_sha256']=hashlib.sha256(player.read_bytes()).hexdigest()
(root/'capture.json').write_text(json.dumps(manifest,indent=2)+'\n')
(root/'timeline.jsonl').write_text(''.join(json.dumps(e)+'\n' for e in events))
if a.mgsf_source:
    sys.path.insert(0,str(a.mgsf_source.resolve()))
    from mgsf.gameplay_capture import run_gameplay_capture
    result=run_gameplay_capture(root,root/'mado-evidence')
    print(f'MADO gameplay-capture: {result.eval_status}')
print(f'{len(events)} timestamped events, {len(media)} SHA-256 indexed real frames; no human enjoyment claim.')
