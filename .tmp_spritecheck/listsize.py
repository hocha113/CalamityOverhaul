from PIL import Image
import glob, os
lines = []
base = os.path.dirname(os.path.abspath(__file__))
for p in glob.glob(os.path.join(base, '*.png')):
    try:
        lines.append(f"{os.path.basename(p)} {Image.open(p).size}")
    except Exception as e:
        lines.append(f"{os.path.basename(p)} ERR {e}")
with open(os.path.join(base, 'sizes.txt'), 'w') as f:
    f.write('\n'.join(lines))
print('done', len(lines))
