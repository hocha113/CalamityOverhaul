using CalamityOverhaul.Content.ADV.UIEffect;
using MagicStorage.Common.Systems.RecurrentRecipes;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Common.QuestTrackerStyles
{
    /// <summary>
    /// 嘉登科技风格
    /// </summary>
    internal class DraedonTrackerStyle : BaseTrackerStyle
    {
        //科技动画参数
        private float scanLineTimer = 0f;
        private float hologramFlicker = 0f;
        private float circuitPulseTimer = 0f;
        private float dataStreamTimer = 0f;
        private float hexGridPhase = 0f;

        //粒子系统
        private readonly List<DraedonDataPRT> dataParticles = [];
        private readonly List<CircuitNodePRT> circuitNodes = [];
        private int dataParticleSpawnTimer = 0;
        private int circuitNodeSpawnTimer = 0;
        private const float TechSideMargin = 28f;

        public override void Update(Rectangle panelRect, bool active) {
            base.Update(panelRect, active);

            //科技动画计时器
            scanLineTimer += 0.048f;
            hologramFlicker += 0.12f;
            circuitPulseTimer += 0.025f;
            dataStreamTimer += 0.055f;
            hexGridPhase += 0.015f;

            if (scanLineTimer > MathHelper.TwoPi) scanLineTimer -= MathHelper.TwoPi;
            if (hologramFlicker > MathHelper.TwoPi) hologramFlicker -= MathHelper.TwoPi;
            if (circuitPulseTimer > MathHelper.TwoPi) circuitPulseTimer -= MathHelper.TwoPi;
            if (dataStreamTimer > MathHelper.TwoPi) dataStreamTimer -= MathHelper.TwoPi;
            if (hexGridPhase > MathHelper.TwoPi) hexGridPhase -= MathHelper.TwoPi;
        }

        public override void UpdateParticles(Vector2 basePos, float panelFade) {
            if (Main.gameMenu) {
                return;
            }
            var panelRect = basePos.GetRectangle(220, 120);
            var panelSize = new Vector2(220, 120);

            //数据粒子刷新
            dataParticleSpawnTimer++;
            if (dataParticleSpawnTimer >= 18 && dataParticles.Count < 15) {
                dataParticleSpawnTimer = 0;
                Vector2 p = basePos + new Vector2(Main.rand.NextFloat(TechSideMargin, panelSize.X - TechSideMargin), Main.rand.NextFloat(40f, panelSize.Y - 40f));
                dataParticles.Add(new DraedonDataPRT(p));
            }
            for (int i = dataParticles.Count - 1; i >= 0; i--) {
                if (dataParticles[i].Update(basePos, panelSize)) {
                    dataParticles.RemoveAt(i);
                }
            }

            //电路节点刷新
            circuitNodeSpawnTimer++;
            if (circuitNodeSpawnTimer >= 25 && circuitNodes.Count < 8) {
                circuitNodeSpawnTimer = 0;
                float scaleW = Main.UIScale;
                float left = basePos.X + TechSideMargin * scaleW;
                float right = basePos.X + panelSize.X - TechSideMargin * scaleW;
                Vector2 start = new(Main.rand.NextFloat(left, right), basePos.Y + Main.rand.NextFloat(40f, panelSize.Y - 40f));
                circuitNodes.Add(new CircuitNodePRT(start));
            }
            for (int i = circuitNodes.Count - 1; i >= 0; i--) {
                if (circuitNodes[i].Update(basePos, panelSize)) {
                    circuitNodes.RemoveAt(i);
                }
            }
        }

        public override void DrawPanel(SpriteBatch spriteBatch, Rectangle panelRect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(4, 4);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.6f));

            //主背景渐变(科技蓝色调)
            int segs = 25;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = (int)(panelRect.Y + t * panelRect.Height);
                int y2 = (int)(panelRect.Y + t2 * panelRect.Height);
                Rectangle r = new Rectangle(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color techDark = new Color(8, 15, 28);
                Color techMid = new Color(15, 30, 50);
                Color techEdge = new Color(30, 60, 90);

                float pulse = (float)Math.Sin(pulseTimer * 0.8f + t * 2.5f) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(techDark, techMid, pulse);
                Color c = Color.Lerp(blendBase, techEdge, t * 0.4f);
                c *= alpha * 0.9f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            //全息扫描效果
            float scanPulse = (float)Math.Sin(pulseTimer * 1.5f) * 0.5f + 0.5f;
            Color scanOverlay = new Color(20, 50, 80) * (alpha * 0.2f * scanPulse);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), scanOverlay);

            //绘制粒子
            foreach (var node in circuitNodes) {
                var origX = node.Pos.X;
                node.Pos.X *= (panelRect.Width / 220f);
                node.Draw(spriteBatch, alpha * 0.85f);
                node.Pos.X = origX;
            }
            foreach (var particle in dataParticles) {
                var origX = particle.Pos.X;
                particle.Pos.X *= (panelRect.Width / 220f);
                particle.Draw(spriteBatch, alpha * 0.75f);
                particle.Pos.X = origX;
            }
        }

        public override void DrawFrame(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float borderGlow) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color techEdge = Color.Lerp(new Color(60, 180, 255), new Color(100, 220, 255), borderGlow) * (alpha * 0.9f);

            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 3), new Rectangle(0, 0, 1, 1), techEdge);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - 3, panelRect.Width, 3), new Rectangle(0, 0, 1, 1), techEdge * 0.8f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, 3, panelRect.Height), new Rectangle(0, 0, 1, 1), techEdge * 0.95f);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - 3, panelRect.Y, 3, panelRect.Height), new Rectangle(0, 0, 1, 1), techEdge * 0.95f);

            Rectangle inner = panelRect;
            inner.Inflate(-6, -6);
            Color innerC = new Color(100, 220, 255) * (alpha * 0.18f * borderGlow);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.9f);
            spriteBatch.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.9f);

            DrawCornerNode(spriteBatch, new Vector2(panelRect.X + 8, panelRect.Y + 8), alpha * 0.95f, borderGlow);
            DrawCornerNode(spriteBatch, new Vector2(panelRect.Right - 8, panelRect.Y + 8), alpha * 0.95f, borderGlow);
            DrawCornerNode(spriteBatch, new Vector2(panelRect.X + 8, panelRect.Bottom - 8), alpha * 0.7f, borderGlow);
            DrawCornerNode(spriteBatch, new Vector2(panelRect.Right - 8, panelRect.Bottom - 8), alpha * 0.7f, borderGlow);
        }

        private static void DrawCornerNode(SpriteBatch sb, Vector2 pos, float a, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;
            float size = 5f + (float)Math.Sin(pulse * MathHelper.TwoPi) * 1f;
            Color c = new Color(100, 220, 255) * (a * (0.8f + pulse * 0.2f));

            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.9f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.2f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.5f, 0f, new Vector2(0.5f, 0.5f), new Vector2(size * 0.4f, size * 0.4f), SpriteEffects.None, 0f);
        }

        public override void DrawDivider(SpriteBatch spriteBatch, Vector2 start, Vector2 end, float alpha) {
            DrawGradientLine(spriteBatch, start, end,
                new Color(80, 200, 255) * (alpha * 0.9f), new Color(80, 200, 255) * (alpha * 0.1f), 1.5f);
        }

        public override Color GetTitleColor(float alpha) {
            return new Color(100, 220, 255) * alpha;
        }

        public override Color GetTextColor(float alpha) {
            return new Color(200, 230, 255) * alpha;
        }

        public override Color GetNumberColor(float progress, float targetProgress, float alpha) {
            if (progress >= targetProgress) {
                return Color.LimeGreen * alpha;
            }
            return Color.Lerp(new Color(100, 200, 255), Color.Cyan, progress / targetProgress) * alpha;
        }

        protected override Color GetProgressBarBgColor(float alpha) {
            return new Color(10, 20, 35) * (alpha * 0.8f);
        }

        protected override Color GetProgressBarStartColor(float alpha) {
            return new Color(60, 180, 255) * alpha;
        }

        protected override Color GetProgressBarEndColor(float alpha) {
            return new Color(100, 220, 255) * alpha;
        }

        protected override Color GetProgressBarGlowColor(float alpha) {
            return new Color(100, 220, 255) * (alpha * 0.4f);
        }

        protected override Color GetProgressBarBorderColor(float alpha) {
            return new Color(80, 200, 255) * (alpha * 0.8f);
        }

        public override void GetParticles(out List<object> particles) {
            particles = new List<object>();
            particles.AddRange(dataParticles);
            particles.AddRange(circuitNodes);
        }

        public override void Reset() {
            base.Reset();
            dataParticles.Clear();
            circuitNodes.Clear();
            dataParticleSpawnTimer = 0;
            circuitNodeSpawnTimer = 0;
        }
    }
}
