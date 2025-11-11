using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.ADVChoiceBoxs.Styles
{
    /// <summary>
    /// 奸奇魔法风格 - 变化之神的炫彩扭曲样式
    /// </summary>
    internal class TzeentchChoiceBoxStyle : IChoiceBoxStyle
    {
        //奸奇魔法动画参数
        private float warpTimer = 0f;          //现实扭曲计时器
        private float changeFlux = 0f;         //变化涌动计时器
        private float arcanePhase = 0f;        //奥术相位
        private float colorShift = 0f;         //色彩变换
        private float chaosRipple = 0f;        //混沌涟漪

        //粒子系统
        private readonly List<WarpFlame> warpFlames = new();
        private int warpFlameSpawnTimer = 0;
        private readonly List<MysticOrb> mysticOrbs = new();
        private int orbSpawnTimer = 0;

        public void Update(Rectangle panelRect, bool active, bool closing) {
            //奸奇的时间是扭曲的，速度不一
            warpTimer += 0.062f + (float)Math.Sin(changeFlux * 0.3f) * 0.015f;
            changeFlux += 0.041f;
            arcanePhase += 0.038f + (float)Math.Cos(warpTimer * 0.5f) * 0.012f;
            colorShift += 0.052f;
            chaosRipple += 0.033f;

            if (warpTimer > MathHelper.TwoPi) warpTimer -= MathHelper.TwoPi;
            if (changeFlux > MathHelper.TwoPi) changeFlux -= MathHelper.TwoPi;
            if (arcanePhase > MathHelper.TwoPi) arcanePhase -= MathHelper.TwoPi;
            if (colorShift > MathHelper.TwoPi) colorShift -= MathHelper.TwoPi;
            if (chaosRipple > MathHelper.TwoPi) chaosRipple -= MathHelper.TwoPi;

            //扭曲火焰生成
            warpFlameSpawnTimer++;
            if (active && !closing && warpFlameSpawnTimer >= 8 && warpFlames.Count < 18) {
                warpFlameSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(panelRect.X + 20f, panelRect.Right - 20f);
                Vector2 startPos = new(xPos, panelRect.Bottom - 6f);
                warpFlames.Add(new WarpFlame(startPos));
            }

            //更新扭曲火焰
            for (int i = warpFlames.Count - 1; i >= 0; i--) {
                if (warpFlames[i].Update(panelRect)) {
                    warpFlames.RemoveAt(i);
                }
            }

            //神秘法球生成
            orbSpawnTimer++;
            if (active && !closing && orbSpawnTimer >= 40 && mysticOrbs.Count < 6) {
                orbSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(panelRect.X + 40f, panelRect.Right - 40f),
                    Main.rand.NextFloat(panelRect.Y + 30f, panelRect.Bottom - 30f)
                );
                mysticOrbs.Add(new MysticOrb(startPos));
            }

            //更新法球
            for (int i = mysticOrbs.Count - 1; i >= 0; i--) {
                if (mysticOrbs[i].Update(panelRect)) {
                    mysticOrbs.RemoveAt(i);
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //扭曲的阴影效果
            Rectangle shadow = panelRect;
            shadow.Offset(8, 10);
            float shadowWarp = (float)Math.Sin(warpTimer * 1.5f) * 3f;
            shadow.Offset((int)shadowWarp, 0);
            spriteBatch.Draw(pixel, shadow, new Rectangle(0, 0, 1, 1), new Color(20, 0, 40) * (alpha * 0.7f));

            //炫彩渐变背景
            int segments = 35;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                //奸奇标志性的蓝紫粉色调
                float colorPhase = colorShift + t * MathHelper.Pi;
                Color tzeentchBlue = new Color(20, 50, 180);
                Color tzeentchPurple = new Color(140, 40, 200);
                Color tzeentchPink = new Color(255, 80, 200);
                Color tzeentchCyan = new Color(80, 200, 255);

                //变化之神的色彩永不固定
                float shift1 = (float)Math.Sin(colorPhase) * 0.5f + 0.5f;
                float shift2 = (float)Math.Sin(colorPhase * 1.3f + changeFlux) * 0.5f + 0.5f;
                float shift3 = (float)Math.Sin(colorPhase * 0.7f + arcanePhase) * 0.5f + 0.5f;

                Color c1 = Color.Lerp(tzeentchBlue, tzeentchPurple, shift1);
                Color c2 = Color.Lerp(tzeentchPurple, tzeentchPink, shift2);
                Color c3 = Color.Lerp(tzeentchCyan, tzeentchBlue, shift3);

                Color baseColor = Color.Lerp(c1, c2, t);
                Color finalColor = Color.Lerp(baseColor, c3, (float)Math.Sin(t * MathHelper.Pi) * 0.4f);
                finalColor *= alpha * 0.88f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //变化脉冲叠加层
            float changePulse = (float)Math.Sin(changeFlux * 2.1f) * 0.5f + 0.5f;
            Color pulseOverlay = new Color(160, 80, 255) * (alpha * 0.18f * changePulse);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), pulseOverlay);

            //绘制现实扭曲波纹
            DrawWarpRipples(spriteBatch, panelRect, alpha * 0.75f);

            //内发光
            float arcanePulse = (float)Math.Sin(arcanePhase * 1.7f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-8, -8);
            Color arcaneGlow = Color.Lerp(new Color(100, 150, 255), new Color(200, 100, 255), arcanePulse);
            spriteBatch.Draw(pixel, inner, new Rectangle(0, 0, 1, 1), arcaneGlow * (alpha * 0.15f * arcanePulse));

            //绘制奸奇魔法边框
            DrawTzeentchFrame(spriteBatch, panelRect, alpha);

            //绘制粒子
            foreach (var orb in mysticOrbs) {
                orb.Draw(spriteBatch, alpha * 0.85f);
            }
            foreach (var flame in warpFlames) {
                flame.Draw(spriteBatch, alpha * 0.95f);
            }
        }

        public void DrawChoiceBackground(SpriteBatch spriteBatch, Rectangle choiceRect, bool enabled, float hoverProgress, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //魔法色彩背景
            float colorPhase = colorShift + (choiceRect.Y * 0.01f);
            Color magicBg1 = Color.Lerp(new Color(30, 20, 80), new Color(60, 40, 120), hoverProgress);
            Color magicBg2 = Color.Lerp(new Color(80, 40, 120), new Color(120, 60, 160), hoverProgress);
            Color choiceBg = enabled
                ? Color.Lerp(magicBg1, magicBg2, (float)Math.Sin(colorPhase) * 0.5f + 0.5f) * 0.4f
                : new Color(20, 15, 30) * 0.12f;

            spriteBatch.Draw(pixel, choiceRect, new Rectangle(0, 0, 1, 1), choiceBg * alpha);

            //魔法边框
            if (enabled && hoverProgress > 0.01f) {
                Color magicEdge = GetEdgeColor(alpha);
                DrawChoiceBorder(spriteBatch, choiceRect, magicEdge * (hoverProgress * 0.7f));

                //魔法流光效果
                float flowPhase = warpTimer * 2f + choiceRect.Y * 0.02f;
                float flowX = choiceRect.X + ((float)Math.Sin(flowPhase) * 0.5f + 0.5f) * choiceRect.Width;
                Color flowColor = Color.Lerp(
                    new Color(150, 100, 255),
                    new Color(255, 150, 255),
                    (float)Math.Sin(flowPhase) * 0.5f + 0.5f
                ) * (alpha * hoverProgress * 0.3f);

                spriteBatch.Draw(pixel,
                    new Rectangle((int)flowX, choiceRect.Y, 2, choiceRect.Height),
                    flowColor);
            }
            else if (!enabled) {
                DrawChoiceBorder(spriteBatch, choiceRect, new Color(60, 40, 80) * (alpha * 0.2f));
            }
        }

        public Color GetEdgeColor(float alpha) {
            //变幻的魔法色彩边框
            Color c1 = new Color(150, 100, 255);
            Color c2 = new Color(255, 150, 255);
            Color c3 = new Color(100, 200, 255);

            float pulse = (float)Math.Sin(arcanePhase * 1.5f) * 0.5f + 0.5f;
            return Color.Lerp(
                Color.Lerp(c1, c2, pulse),
                c3,
                (float)Math.Sin(colorShift) * 0.5f + 0.5f
            ) * (alpha * 0.9f);
        }

        public Color GetTextGlowColor(float alpha, float hoverProgress) {
            //魔法文字光晕
            Color glow1 = Color.Lerp(
                new Color(150, 100, 255),
                new Color(255, 150, 255),
                (float)Math.Sin(colorShift) * 0.5f + 0.5f
            );
            Color glow2 = Color.Lerp(
                new Color(100, 200, 255),
                new Color(200, 100, 255),
                (float)Math.Sin(colorShift + MathHelper.PiOver2) * 0.5f + 0.5f
            );

            return Color.Lerp(glow1, glow2, hoverProgress) * alpha;
        }

        public void DrawTitleDecoration(SpriteBatch spriteBatch, Vector2 titlePos, string title, float alpha) {
            //奸奇魔法文字光晕
            Color nameGlow1 = Color.Lerp(
                new Color(150, 100, 255),
                new Color(255, 150, 255),
                (float)Math.Sin(colorShift) * 0.5f + 0.5f
            );
            Color nameGlow2 = Color.Lerp(
                new Color(100, 200, 255),
                new Color(200, 100, 255),
                (float)Math.Sin(colorShift + MathHelper.PiOver2) * 0.5f + 0.5f
            );

            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + arcanePhase * 0.5f;
                Vector2 offset = angle.ToRotationVector2() * 2.5f;
                Color glowColor = Color.Lerp(nameGlow1, nameGlow2, i / 8f) * (alpha * 0.5f);
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, glowColor, 0.95f);
            }
        }

        public void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            //魔法渐变分隔线
            Color startColor = Color.Lerp(
                new Color(150, 100, 255),
                new Color(255, 150, 255),
                (float)Math.Sin(colorShift) * 0.5f + 0.5f
            ) * (alpha * 0.9f);

            Color endColor = Color.Lerp(
                new Color(100, 150, 255),
                new Color(200, 100, 255),
                (float)Math.Sin(colorShift + 1f) * 0.5f + 0.5f
            ) * (alpha * 0.1f);

            DrawMagicGradientLine(spriteBatch, start, end, startColor, endColor, 1.8f);
        }

        public void Reset() {
            warpTimer = 0f;
            changeFlux = 0f;
            arcanePhase = 0f;
            colorShift = 0f;
            chaosRipple = 0f;
            warpFlames.Clear();
            mysticOrbs.Clear();
            warpFlameSpawnTimer = 0;
            orbSpawnTimer = 0;
        }

        #region 工具方法
        private void DrawWarpRipples(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int rippleCount = 6;

            for (int i = 0; i < rippleCount; i++) {
                float t = i / (float)rippleCount;
                float baseY = rect.Y + 20 + t * (rect.Height - 40);

                //现实扭曲幅度
                float amplitude = 6f + (float)Math.Sin((chaosRipple + t * 1.5f) * 2.8f) * 4f;
                float thickness = 1.8f;
                float phase = warpTimer * 2.5f + t * MathHelper.TwoPi;

                int segments = 40;
                Vector2 prevPoint = Vector2.Zero;

                for (int s = 0; s <= segments; s++) {
                    float progress = s / (float)segments;
                    float localPhase = phase + progress * MathHelper.TwoPi * 2.3f;
                    float warpY = baseY + (float)Math.Sin(localPhase) * amplitude;

                    //添加次级扭曲
                    warpY += (float)Math.Cos(localPhase * 1.7f + changeFlux) * (amplitude * 0.3f);

                    Vector2 point = new(rect.X + 12 + progress * (rect.Width - 24), warpY);

                    if (s > 0) {
                        Vector2 diff = point - prevPoint;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();

                            //变幻的色彩
                            float colorPhase = colorShift + progress + t;
                            Color c1 = new Color(100, 150, 255);
                            Color c2 = new Color(200, 100, 255);
                            Color waveColor = Color.Lerp(c1, c2, (float)Math.Sin(colorPhase * MathHelper.Pi) * 0.5f + 0.5f);
                            waveColor *= alpha * 0.1f;

                            sb.Draw(pixel, prevPoint, new Rectangle(0, 0, 1, 1), waveColor, rot,
                                Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prevPoint = point;
                }
            }
        }

        private void DrawTzeentchFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //外框
            Color c1 = new Color(150, 100, 255);
            Color c2 = new Color(255, 150, 255);
            Color c3 = new Color(100, 200, 255);

            float pulse = (float)Math.Sin(arcanePhase * 1.5f) * 0.5f + 0.5f;
            Color outerEdge = Color.Lerp(
                Color.Lerp(c1, c2, pulse),
                c3,
                (float)Math.Sin(colorShift) * 0.5f + 0.5f
            ) * (alpha * 0.9f);

            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 4), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 4, rect.Width, 4), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 4, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - 4, rect.Y, 4, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-7, -7);
            Color innerGlow = Color.Lerp(c2, c1, pulse) * (alpha * 0.25f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);

            //角落魔法符号
            DrawTzeentchSymbol(sb, new Vector2(rect.X + 14, rect.Y + 14), alpha * 0.95f);
            DrawTzeentchSymbol(sb, new Vector2(rect.Right - 14, rect.Y + 14), alpha * 0.95f);
        }

        private void DrawTzeentchSymbol(SpriteBatch sb, Vector2 pos, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 7f;

            //变化之神的符号
            Color symbolColor = Color.Lerp(
                new Color(200, 150, 255),
                new Color(255, 150, 255),
                (float)Math.Sin(colorShift) * 0.5f + 0.5f
            ) * alpha;

            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.PiOver2 * i + arcanePhase;
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), symbolColor, angle,
                    new Vector2(0.5f, 0.5f), new Vector2(size * 1.4f, size * 0.35f), SpriteEffects.None, 0f);
            }

            //中心点
            sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), symbolColor * 0.8f, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size * 0.5f, size * 0.5f), SpriteEffects.None, 0f);
        }

        private static void DrawChoiceBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height),
                new Rectangle(0, 0, 1, 1), color);
        }

        private static void DrawMagicGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end,
            Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) return;

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 12f));

            for (int i = 0; i < segments; i++) {
                float t = (float)i / segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation,
                    new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
        #endregion

        #region 粒子类
        private class WarpFlame
        {
            public Vector2 Pos;
            public float Size;
            public float RiseSpeed;
            public float Drift;
            public float Life;
            public float MaxLife;
            public float Seed;
            public float Rotation;
            public float RotationSpeed;
            public float ColorPhase;

            public WarpFlame(Vector2 start) {
                Pos = start;
                Size = Main.rand.NextFloat(3f, 6.5f);
                RiseSpeed = Main.rand.NextFloat(0.3f, 0.9f);
                Drift = Main.rand.NextFloat(-0.3f, 0.3f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(80f, 150f);
                Seed = Main.rand.NextFloat(10f);
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
                RotationSpeed = Main.rand.NextFloat(-0.08f, 0.08f);
                ColorPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public bool Update(Rectangle bounds) {
                Life++;
                float t = Life / MaxLife;

                //扭曲的上升轨迹
                Pos.Y -= RiseSpeed * (0.8f + (float)Math.Sin(t * MathHelper.Pi) * 0.4f);
                Pos.X += (float)Math.Sin(Life * 0.08f + Seed) * Drift;
                Rotation += RotationSpeed;
                ColorPhase += 0.05f;

                if (Life >= MaxLife || Pos.Y < bounds.Y + 10f) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float scale = Size * (1f + (float)Math.Sin((Life + Seed * 25f) * 0.15f) * 0.2f);

                //奸奇魔法火焰颜色：蓝紫粉混合
                Color c1 = new Color(100, 150, 255);
                Color c2 = new Color(200, 100, 255);
                Color c3 = new Color(255, 150, 255);

                Color flameCore = Color.Lerp(
                    Color.Lerp(c1, c2, (float)Math.Sin(ColorPhase) * 0.5f + 0.5f),
                    c3,
                    t
                ) * (alpha * 0.9f * fade);

                Color flameGlow = Color.Lerp(c2, c1, t) * (alpha * 0.5f * fade);

                //外层光晕
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), flameGlow, 0f,
                    new Vector2(0.5f, 0.5f), scale * 2.8f, SpriteEffects.None, 0f);
                //内层核心
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), flameCore, Rotation,
                    new Vector2(0.5f, 0.5f), scale, SpriteEffects.None, 0f);
            }
        }

        private class MysticOrb
        {
            public Vector2 Pos;
            public Vector2 Velocity;
            public float Size;
            public float Life;
            public float MaxLife;
            public float Seed;
            public float Phase;

            public MysticOrb(Vector2 start) {
                Pos = start;
                Velocity = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(0.25f, 0.7f);
                Size = Main.rand.NextFloat(6f, 12f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(120f, 200f);
                Seed = Main.rand.NextFloat(10f);
                Phase = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            public bool Update(Rectangle bounds) {
                Life++;
                Phase += 0.08f;

                //法球漂浮
                Vector2 drift = new Vector2(
                    (float)Math.Sin(Phase + Seed) * 0.5f,
                    (float)Math.Cos(Phase * 1.5f + Seed * 1.4f) * 0.4f
                );
                Pos += Velocity + drift;

                //边界反弹
                if (Pos.X < bounds.X + 25f || Pos.X > bounds.Right - 25f) {
                    Velocity.X *= -0.8f;
                }
                if (Pos.Y < bounds.Y + 25f || Pos.Y > bounds.Bottom - 25f) {
                    Velocity.Y *= -0.8f;
                }

                if (Life >= MaxLife) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float pulse = (float)Math.Sin(Phase * 2f) * 0.5f + 0.5f;
                float scale = Size * (0.7f + pulse * 0.5f);

                //法球颜色
                Color orbCore = Color.Lerp(
                    new Color(180, 150, 255),
                    new Color(255, 180, 255),
                    pulse
                ) * (alpha * 0.75f * fade);

                Color orbGlow = Color.Lerp(
                    new Color(120, 180, 255),
                    new Color(200, 150, 255),
                    pulse
                ) * (alpha * 0.4f * fade);

                //外层光晕
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), orbGlow, 0f,
                    new Vector2(0.5f, 0.5f), scale * 3.5f, SpriteEffects.None, 0f);
                //内层核心
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), orbCore, 0f,
                    new Vector2(0.5f, 0.5f), scale * 1.5f, SpriteEffects.None, 0f);
                //最内层
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), Color.White * (alpha * 0.5f * fade), 0f,
                    new Vector2(0.5f, 0.5f), scale * 0.5f, SpriteEffects.None, 0f);
            }
        }
        #endregion
    }
}
