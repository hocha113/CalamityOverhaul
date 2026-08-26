# 生成凝胶体轮廓的测试精灵(模拟史莱姆女皇身形)供沙盒验证晶面皮肤着色器
import math, os
from PIL import Image

W, H = 122, 106
img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
px = img.load()

cx, by = W / 2, H - 4.0
for y in range(H):
    for x in range(W):
        dx = (x - cx) / (W * 0.44)
        dy = (y - (by - H * 0.42)) / (H * 0.46)
        # 底部略宽的半椭圆胶体
        squish = 1.0 + 0.35 * max(0.0, (y - H * 0.45) / (H * 0.55))
        d = (dx / squish) ** 2 + dy ** 2
        if d <= 1.0 and y <= by:
            t = 1.0 - d
            # 粉紫渐变体 + 底部沉色
            r = int(200 + 45 * t)
            g = int(95 + 60 * t)
            b = int(190 + 55 * t)
            depth = (y / H)
            r = int(r * (1.0 - depth * 0.25))
            g = int(g * (1.0 - depth * 0.25))
            b = int(b * (1.0 - depth * 0.18))
            a = 235 if d < 0.92 else int(235 * (1.0 - (d - 0.92) / 0.08))
            px[x, y] = (r, g, b, a)

# 内部"宝石"亮斑
for y in range(H):
    for x in range(W):
        gx = (x - cx) / 12.0
        gy = (y - H * 0.52) / 15.0
        if gx * gx + gy * gy <= 1.0 and px[x, y][3] > 0:
            r, g, b, a = px[x, y]
            px[x, y] = (min(255, r + 40), min(255, g + 80), min(255, b + 50), a)

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'qs_mock.png')
img.save(out)
print('saved', out, img.size)
