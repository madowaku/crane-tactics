"""Keep capture timing when milestone PNG requests replace a numbered frame."""
import pathlib,sys
folder=pathlib.Path(sys.argv[1]).resolve()
frames=sorted((folder/'frames').glob('*.png'))
lines=['ffconcat version 1.0']
for index,frame in enumerate(frames):
    following=int(frames[index+1].stem) if index+1<len(frames) else int(frame.stem)+1
    lines.extend([f"file 'frames/{frame.name}'",f'duration {(following-int(frame.stem))/20:.6f}'])
lines.append(f"file 'frames/{frames[-1].name}'")
(folder/'frames.ffconcat').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(f'{len(frames)} captured frames; {(int(frames[-1].stem)-int(frames[0].stem)+1)/20:.2f}s timeline')
