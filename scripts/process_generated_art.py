"""Turn Codex-generated originals (ArtSource/Generated) into small Unity runtime textures.

python scripts/process_generated_art.py
Domes are cut out with a circular alpha mask found from their non-black bounding box. No pixels are invented.
Originals are kept as WebP (quality 90, full resolution) to keep the repository light; outputs stay PNG.
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource/Generated'
RES = ROOT / 'Assets/_CraneTactics/Resources'


def cut_dome(path):
    image = Image.open(path).convert('RGB')
    box = image.convert('L').point(lambda v: 255 if v > 28 else 0).getbbox()
    cx, cy = (box[0] + box[2]) / 2, (box[1] + box[3]) / 2
    half = max(box[2] - box[0], box[3] - box[1]) / 2
    square = image.crop((int(cx - half), int(cy - half), int(cx + half), int(cy + half))).resize((512, 512), Image.LANCZOS)
    mask = Image.new('L', (512, 512), 0)
    ImageDraw.Draw(mask).ellipse((5, 5, 506, 506), fill=255)
    square.putalpha(mask.filter(ImageFilter.GaussianBlur(2)))
    return square


def main():
    ui = RES / 'UI'
    ui.mkdir(parents=True, exist_ok=True)
    for color in ('cyan', 'amber', 'red', 'mint'):
        dome = cut_dome(SOURCE / f'ui/button-{color}.webp')
        dome.resize((256, 256), Image.LANCZOS).save(ui / f'button-{color}.png')
        dome.resize((44, 44), Image.LANCZOS).save(ui / f'pill-{color}.png')
    plate = Image.open(SOURCE / 'ui/console-plate.webp').convert('RGB')
    w, h = plate.size
    band = h // 6
    plate.crop((0, h // 2 - band, w, h // 2 + band)).resize((1024, 128), Image.LANCZOS).save(ui / 'console-plate.png')
    terrain = RES / 'Terrain'
    terrain.mkdir(parents=True, exist_ok=True)
    for path in sorted((SOURCE / 'tiles').glob('*.webp')):
        Image.open(path).convert('RGB').resize((256, 256), Image.LANCZOS).save(terrain / (path.stem + '.png'))
    marquee = RES / 'Marquee'
    marquee.mkdir(parents=True, exist_ok=True)
    for path in sorted((SOURCE / 'marquee').glob('*.webp')):
        image = Image.open(path).convert('RGB')
        image.thumbnail((720, 720), Image.LANCZOS)
        image.save(marquee / (path.stem + '.png'))
    print('processed', len(list(ui.glob('*.png'))), 'ui', len(list(terrain.glob('*.png'))), 'terrain', len(list(marquee.glob('*.png'))), 'marquee')


if __name__ == '__main__':
    main()
