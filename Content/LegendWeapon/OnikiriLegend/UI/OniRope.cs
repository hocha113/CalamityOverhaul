using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// UI 空间的轻量 Verlet 绳：8~12 质点、每帧数次约束迭代。<br/>
    /// 挂绳自然垂成悬链线、随锚点移动甩摆、被风扰动——刚性直线和会晃的绳子之间就是贴图感与手工感的差距。<br/>
    /// 单端钉(挂坠/流苏)或双端钉(两点间松弛悬绳)皆可
    /// </summary>
    internal sealed class OniRope
    {
        private readonly Vector2[] pos;
        private readonly Vector2[] old;
        private readonly float segLen;
        private bool warmed;

        public OniRope(int points, float totalLen) {
            points = Math.Max(points, 3);
            pos = new Vector2[points];
            old = new Vector2[points];
            segLen = totalLen / (points - 1);
        }

        public Vector2 this[int i] => pos[i];
        public Vector2 End => pos[^1];

        /// <summary>末段方向(挂物朝向用),静止下垂时 ≈ π/2</summary>
        public float EndRotation => (pos[^1] - pos[^2]).SafeNormalize(Vector2.UnitY).ToRotation();

        /// <summary>沿垂直方向摆好初始落位,避免首帧从旧位置飞来</summary>
        public void WarmStart(Vector2 anchor) {
            for (int i = 0; i < pos.Length; i++) {
                pos[i] = old[i] = anchor + new Vector2(0f, segLen * i);
            }
            warmed = true;
        }

        /// <summary>
        /// 推进一帧。anchor 钉首端;tail 非空则钉末端(双端悬绳);
        /// windAmp 风扰强度(像素/帧);endWeight 末端附加重力(挂坠质量感)
        /// </summary>
        public void Update(Vector2 anchor, Vector2? tail, float time, float windAmp,
            float endWeight = 0f, float damping = 0.90f, int iterations = 3) {
            //布局跳变(开窗/改分辨率)时重摆,防止绳从屏幕另一端甩来
            if (!warmed || Vector2.DistanceSquared(pos[0], anchor) > 300f * 300f) {
                WarmStart(anchor);
            }

            int n = pos.Length;
            for (int i = 1; i < n; i++) {
                Vector2 vel = (pos[i] - old[i]) * damping;
                old[i] = pos[i];
                pos[i] += vel;
                pos[i].Y += 0.14f;
                //风:两个不同频率的谐波叠加,越靠末端摆幅越大
                float reach = i / (float)(n - 1);
                float wind = (float)Math.Sin(time * 1.6f + i * 0.85f)
                    + (float)Math.Sin(time * 0.53f + i * 0.37f) * 0.55f;
                pos[i].X += wind * windAmp * reach;
            }
            if (endWeight > 0f) {
                pos[^1].Y += endWeight;
            }

            for (int k = 0; k < iterations; k++) {
                pos[0] = anchor;
                if (tail.HasValue) {
                    pos[^1] = tail.Value;
                }
                for (int i = 0; i < n - 1; i++) {
                    Vector2 delta = pos[i + 1] - pos[i];
                    float len = delta.Length();
                    if (len < 0.0001f) {
                        continue;
                    }
                    float diff = (len - segLen) / len;
                    if (i == 0) {
                        //首端已钉:全部修正量给下一点
                        pos[i + 1] -= delta * diff;
                    }
                    else {
                        Vector2 corr = delta * (diff * 0.5f);
                        pos[i] += corr;
                        pos[i + 1] -= corr;
                    }
                }
            }
            pos[0] = anchor;
            if (tail.HasValue) {
                pos[^1] = tail.Value;
            }
        }

        /// <summary>朝末端点注入一次横向冲量(hover 轻颤/开合甩动用)</summary>
        public void Nudge(float impulseX, float impulseY = 0f) {
            if (!warmed) {
                return;
            }
            old[^1] -= new Vector2(impulseX, impulseY);
            if (pos.Length > 2) {
                old[^2] -= new Vector2(impulseX * 0.5f, impulseY * 0.5f);
            }
        }

        /// <summary>
        /// 逐段折线绘制:主体首尾渐变 + 绞纹高光(每段一粒斜向亮点,随材质走不滑动)
        /// </summary>
        public void Draw(SpriteBatch sb, Color start, Color end, float thickness, float alpha, bool twist = true) {
            if (!warmed || alpha <= 0.01f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            int n = pos.Length;
            for (int i = 0; i < n - 1; i++) {
                Vector2 a = pos[i];
                Vector2 b = pos[i + 1];
                Vector2 d = b - a;
                float len = d.Length();
                if (len < 0.01f) {
                    continue;
                }
                float rot = d.ToRotation();
                float t = i / (float)(n - 1);
                Color col = Color.Lerp(start, end, t) * alpha;
                sb.Draw(pixel, a, src, col, rot, new Vector2(0f, 0.5f), new Vector2(len + 0.7f, thickness), SpriteEffects.None, 0f);

                //绞纹:编绳的斜向亮点,位置由段序决定(材质固定,随绳形变而动)
                if (twist && thickness >= 1.2f) {
                    float side = i % 2 == 0 ? 0.32f : -0.32f;
                    Vector2 perp = new(-d.Y / len, d.X / len);
                    Vector2 hp = a + d * 0.45f + perp * (thickness * side);
                    sb.Draw(pixel, hp, src, OnikiriUITheme.Bright * (alpha * 0.20f), rot + 0.5f,
                        new Vector2(0.5f), new Vector2(len * 0.34f, MathF.Max(thickness * 0.4f, 0.8f)), SpriteEffects.None, 0f);
                }
            }
        }
    }
}
