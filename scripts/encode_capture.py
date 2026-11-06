"""Encode real Unity frame captures, retaining time gaps by holding the prior frame.

Unity's async CaptureScreenshot can omit a frame when disk writes lag. The numeric
filenames preserve the development playback's 20fps simulation clock. This does
not measure rendering performance or record human input.
"""
import argparse
from pathlib import Path
import subprocess

p = argparse.ArgumentParser()
p.add_argument('capture', type=Path)
p.add_argument('output', type=Path)
a = p.parse_args()
frames = sorted((a.capture / 'frames').glob('*.png'))
if not frames:
    raise ValueError('No captured frames')
manifest = a.capture / 'ffmpeg-frames.txt'
rows = []
for i, frame in enumerate(frames):
    path = frame.resolve().as_posix().replace("'", "'\\''")
    duration = (int(frames[i + 1].stem) - int(frame.stem)) / 20 if i + 1 < len(frames) else .05
    rows += [f"file '{path}'", f'duration {duration:.3f}']
rows += [f"file '{frames[-1].resolve().as_posix()}'"]
manifest.write_text('\n'.join(rows) + '\n', encoding='utf-8')
subprocess.run(['ffmpeg', '-y', '-hide_banner', '-loglevel', 'warning', '-f', 'concat',
                '-safe', '0', '-i', str(manifest), '-r', '20', '-fps_mode', 'cfr',
                '-c:v', 'libx264', '-preset', 'fast', '-crf', '22', '-pix_fmt', 'yuv420p',
                str(a.output)], check=True)
missing = int(frames[-1].stem) - int(frames[0].stem) + 1 - len(frames)
print(f'{len(frames)} real frames; {missing} missing slots held; no generated motion.')
