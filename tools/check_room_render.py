"""Check paired room renders and the original face from both sides; reads PNGs only."""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image


def pixels(path):
    with Image.open(path) as image:
        return np.asarray(image.convert('RGB'), dtype=np.float64) / 255


def metrics(path):
    rgb = pixels(path)
    return {
        'meanDisplayLuminance': float((rgb @ np.array([.2126, .7152, .0722])).mean()),
        'clippedWhiteFraction': float((rgb >= 250 / 255).all(axis=-1).mean()),
    }


def main():
    output = Path(sys.argv[1]).resolve()
    report = {'stations': [], 'faceCoverage': {}}
    for number in range(1, 7):
        current = metrics(output / f'P0{number}-room.png')
        legacy = metrics(output / f'P0{number}-legacy-tint.png')
        assert current['meanDisplayLuminance'] < legacy['meanDisplayLuminance'] * .95, f'P0{number}: tint fix did not reduce brightness'
        assert current['clippedWhiteFraction'] <= legacy['clippedWhiteFraction'] + .0001, f'P0{number}: more whites clip after tint correction'
        report['stations'].append({'station': number, 'current': current, 'legacyTintOnly': legacy})
    for side in ('front', 'back'):
        rgb = pixels(output / f'original-face-{side}.png')
        background = (rgb[..., 0] > 245 / 255) & (rgb[..., 1] < 10 / 255) & (rgb[..., 2] > 245 / 255)
        report['faceCoverage'][side] = float((~background).mean())
        assert report['faceCoverage'][side] > .02, f'Original room face disappeared on {side}'
    assert abs(report['faceCoverage']['front'] - report['faceCoverage']['back']) < .005, 'Original face coverage differs between sides'
    (output / 'pixel-check.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print('PASS: six paired room views are less bright without increased clipped whites; original face visible from both sides.')


if __name__ == '__main__':
    main()
