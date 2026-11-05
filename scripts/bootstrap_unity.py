"""Extract the installed official URP template without its generated cache."""
import pathlib, sys, tarfile
root = pathlib.Path(__file__).resolve().parents[1]
with tarfile.open(sys.argv[1]) as archive:
    for member in archive.getmembers():
        prefix = 'package/ProjectData~/'
        if not member.isfile() or not member.name.startswith(prefix):
            continue
        relative = pathlib.PurePosixPath(member.name[len(prefix):])
        if relative.parts[0] not in ('Assets', 'Packages', 'ProjectSettings'):
            continue
        target = root.joinpath(*relative.parts)
        if not target.resolve().is_relative_to(root) or target.exists():
            raise RuntimeError(f'Refusing overwrite/escape: {relative}')
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(archive.extractfile(member).read())
print('Official URP template extracted; Library cache omitted.')
