﻿using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 深渊复苏进度条UI
    /// </summary>
    internal class ResurrectionUI : UIHandle
    {
        public static ResurrectionUI Instance => UIHandleLoader.GetUIHandleOfType<ResurrectionUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None; //手动调用

        //UI相关数据（不再管理复苏值，仅用于显示）
        private float displayValue = 0f; //实际显示的复苏值（用于平滑过渡）
        private const float SmoothSpeed = 0.15f; //平滑速度

        //视觉效果相关
        private float shakeIntensity = 0f; //抖动强度
        private Vector2 shakeOffset = Vector2.Zero; //抖动偏移
        private float pulseTimer = 0f; //脉动计时器
        private float glowIntensity = 0f; //发光强度
        private float warningFlashTimer = 0f; //警告闪烁计时器

        //粒子效果
        private readonly System.Collections.Generic.List<ResurrectionParticle> particles = [];
        private int particleSpawnTimer = 0;

        //改良演出粒子（研究新鱼导致上限上升）
        private readonly System.Collections.Generic.List<ImprovePulse> improvePulses = [];
        private readonly System.Collections.Generic.List<ImproveFlyParticle> improveFlyParticles = [];
        private float improveFlash = 0f; //上限提升时的闪光
        private float lastKnownMax = -1f;

        //危险阈值
        private const float DangerThreshold = 0.7f; //70%以上开始警告
        private const float CriticalThreshold = 0.9f; //90%以上进入危险状态

        /// <summary>
        /// 在研究新的鱼完成时触发复苏条改良演出
        /// </summary>
        public void TriggerImproveEffect(Vector2 worldStartPos, int flyCount, float oldMax, float newMax) {
            if (flyCount < 1) {
                flyCount = 1;
            }
            if (flyCount > 30) {
                flyCount = 30;
            }
            lastKnownMax = newMax;
            improveFlash = 1.2f; //立即闪光
            for (int i = 0; i < flyCount; i++) {
                float delay = i * 4f;
                improveFlyParticles.Add(new ImproveFlyParticle(worldStartPos, delay));
            }
        }

        /// <summary>
        /// 获取玩家的复苏系统
        /// </summary>
        private ResurrectionSystem GetResurrectionSystem() {
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return halibutPlayer.ResurrectionSystem;
            }
            return null;
        }

        public float ResurrectionRatio {
            get {
                var system = GetResurrectionSystem();
                return system?.Ratio ?? 0f;
            }
        }

        private Color GetStateColor(float ratio) {
            if (ratio >= CriticalThreshold) {
                float flash = (float)Math.Sin(warningFlashTimer * 8f) * 0.5f + 0.5f;
                return Color.Lerp(new Color(255, 50, 50), new Color(255, 150, 0), flash);
            }
            else if (ratio >= DangerThreshold) {
                return Color.Lerp(new Color(255, 200, 50), new Color(255, 100, 50), ratio - DangerThreshold);
            }
            else if (ratio >= 0.4f) {
                return Color.Lerp(new Color(100, 200, 255), new Color(255, 200, 50), (ratio - 0.4f) / 0.3f);
            }
            else {
                return Color.Lerp(new Color(50, 150, 255), new Color(100, 200, 255), ratio / 0.4f);
            }
        }

        public override void Update() {
            if (HalibutUIHead.Instance == null || HalibutUIAsset.Resurrection == null) {
                return;
            }

            var resurrectionSystem = GetResurrectionSystem();
            if (resurrectionSystem == null) {
                return;
            }

            Vector2 headPos = HalibutUIHead.Instance.DrawPosition;
            Vector2 headSize = HalibutUIHead.Instance.Size;
            DrawPosition = headPos + new Vector2(headSize.X / 2 + 20, 40);
            Size = HalibutUIAsset.Resurrection.Size();

            float targetValue = resurrectionSystem.CurrentValue;
            displayValue = MathHelper.Lerp(displayValue, targetValue, SmoothSpeed * 0.7f);

            float ratio = resurrectionSystem.Ratio;

            pulseTimer += 0.1f;
            warningFlashTimer += 0.1f;

            if (ratio >= CriticalThreshold) {
                shakeIntensity = MathHelper.Lerp(shakeIntensity, 3f, 0.2f);
                glowIntensity = MathHelper.Lerp(glowIntensity, 1f, 0.2f);
            }
            else if (ratio >= DangerThreshold) {
                shakeIntensity = MathHelper.Lerp(shakeIntensity, 1.5f, 0.15f);
                glowIntensity = MathHelper.Lerp(glowIntensity, 0.6f, 0.15f);
            }
            else {
                shakeIntensity = MathHelper.Lerp(shakeIntensity, 0f, 0.1f);
                glowIntensity = MathHelper.Lerp(glowIntensity, 0.2f, 0.1f);
            }

            if (shakeIntensity > 0.1f) {
                shakeOffset = new Vector2(
                    (float)(Math.Sin(pulseTimer * 20f) * shakeIntensity),
                    (float)(Math.Cos(pulseTimer * 15f) * shakeIntensity * 0.5f)
                );
            }
            else {
                shakeOffset = Vector2.Zero;
            }

            for (int i = particles.Count - 1; i >= 0; i--) {
                particles[i].Update();
                if (particles[i].IsDead) {
                    particles.RemoveAt(i);
                }
            }

            if (ratio > DangerThreshold) {
                particleSpawnTimer++;
                int spawnRate = ratio >= CriticalThreshold ? 3 : 8;
                if (particleSpawnTimer >= spawnRate) {
                    particleSpawnTimer = 0;
                    SpawnParticle(ratio);
                }
            }

            //改良飞行粒子更新
            for (int i = improveFlyParticles.Count - 1; i >= 0; i--) {
                improveFlyParticles[i].Update(this);
                if (improveFlyParticles[i].Arrived) {
                    Vector2 center = GetBarCenter();
                    improvePulses.Add(new ImprovePulse(center));
                    improveFlyParticles.RemoveAt(i);
                }
            }

            for (int i = improvePulses.Count - 1; i >= 0; i--) {
                improvePulses[i].Update();
                if (improvePulses[i].Finished) {
                    improvePulses.RemoveAt(i);
                }
            }

            if (improveFlash > 0f) {
                improveFlash -= 0.05f;
                if (improveFlash < 0f) {
                    improveFlash = 0f;
                }
            }

            UIHitBox = DrawPosition.GetRectangle(Size);
        }

        internal Vector2 GetBarCenter() {
            return DrawPosition + shakeOffset + new Vector2(HalibutUIAsset.Resurrection.Width / 2f, HalibutUIAsset.Resurrection.Height / 2f);
        }

        private void SpawnParticle(float ratio) {
            Vector2 barPos = DrawPosition + new Vector2(24, 12) + shakeOffset;
            Vector2 particlePos = barPos + new Vector2(Main.rand.NextFloat(0, 52), Main.rand.NextFloat(-2, 10));
            Vector2 velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, -0.5f));
            Color color = GetStateColor(ratio);
            particles.Add(new ResurrectionParticle(particlePos, velocity, color));
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (HalibutUIHead.Instance == null || !HalibutUIHead.Instance.Active) {
                return;
            }

            var resurrectionSystem = GetResurrectionSystem();
            if (resurrectionSystem == null) {
                return;
            }

            float ratio = displayValue / resurrectionSystem.MaxValue;
            ratio = Math.Clamp(ratio, 0f, 1f);

            Vector2 drawPos = DrawPosition + shakeOffset;

            //绘制粒子（在底层）
            foreach (var particle in particles) {
                particle.Draw(spriteBatch);
            }
            foreach (var fp in improveFlyParticles) {
                fp.Draw(spriteBatch);
            }

            //绘制阴影
            spriteBatch.Draw(HalibutUIAsset.Resurrection, drawPos + new Vector2(2, 2), null,
                Color.Black * 0.5f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            //绘制底部边框
            spriteBatch.Draw(HalibutUIAsset.Resurrection, drawPos, null,
                Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            //绘制进度填充条
            if (ratio > 0.01f) {
                Vector2 barPos = drawPos + new Vector2(24, 12); //偏移值
                int fillWidth = (int)(52 * ratio); //填充宽度

                if (fillWidth > 0) {
                    Rectangle sourceRect = new Rectangle(0, 0, fillWidth, HalibutUIAsset.ResurrectionTop.Height);
                    Color fillColor = GetStateColor(ratio);

                    //绘制底层暗色（营造深度感）
                    spriteBatch.Draw(HalibutUIAsset.ResurrectionTop, barPos + new Vector2(0, 1), sourceRect,
                        fillColor * 0.6f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

                    //绘制主填充条
                    spriteBatch.Draw(HalibutUIAsset.ResurrectionTop, barPos, sourceRect,
                        fillColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

                    //绘制发光效果
                    if (glowIntensity > 0.1f) {
                        Color glowColor = fillColor with { A = 0 };
                        float pulse = (float)Math.Sin(pulseTimer * 3f) * 0.3f + 0.7f;

                        spriteBatch.Draw(HalibutUIAsset.ResurrectionTop, barPos, sourceRect,
                            glowColor * glowIntensity * pulse * 0.6f, 0f, Vector2.Zero,
                            new Vector2(1f, 1.2f), SpriteEffects.None, 0f);
                    }

                    //绘制高光（顶部亮带）
                    Rectangle highlightRect = new Rectangle(0, 0, fillWidth, 2);
                    spriteBatch.Draw(HalibutUIAsset.ResurrectionTop, barPos, highlightRect,
                        Color.White * 0.4f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                }
            }

            //绘制前景边框（增强立体感）
            spriteBatch.Draw(HalibutUIAsset.Resurrection, drawPos, null,
                Color.White * 0.3f, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            //绘制危险状态的警告边框
            if (ratio >= DangerThreshold) {
                float flash = (float)Math.Sin(warningFlashTimer * 6f) * 0.5f + 0.5f;
                Color warningColor = ratio >= CriticalThreshold
                    ? new Color(255, 50, 50)
                    : new Color(255, 200, 50);

                spriteBatch.Draw(HalibutUIAsset.Resurrection, drawPos, null,
                    warningColor with { A = 0 } * flash * 0.5f, 0f, Vector2.Zero,
                    1.05f, SpriteEffects.None, 0f);
            }

            //改良脉冲绘制（在最上层但在文字下）
            foreach (var pulse in improvePulses) {
                pulse.Draw(spriteBatch);
            }

            if (improveFlash > 0f) {
                float flashAlpha = Math.Min(1f, improveFlash);
                spriteBatch.Draw(HalibutUIAsset.Resurrection, drawPos, null,
                    new Color(120, 220, 255) with { A = 0 } * flashAlpha * 0.5f, 0f, Vector2.Zero, 1.08f, SpriteEffects.None, 0f);
            }

            Rectangle hitBox = new Rectangle((int)drawPos.X, (int)drawPos.Y,
                HalibutUIAsset.Resurrection.Width, HalibutUIAsset.Resurrection.Height);

            if (hitBox.Contains(Main.MouseScreen.ToPoint())) {
                string percentText = $"{(int)(ratio * 100)}%";
                Vector2 textPos = drawPos + new Vector2(HalibutUIAsset.Resurrection.Width / 2, -20);
                Utils.DrawBorderString(spriteBatch, percentText, textPos + new Vector2(1, 1),
                    Color.Black * 0.8f, 0.9f, 0.5f, 0.5f);
                Color textColor = GetStateColor(ratio);
                Utils.DrawBorderString(spriteBatch, percentText, textPos,
                    textColor, 0.9f, 0.5f, 0.5f);
                string labelText = "深渊复苏";
                Vector2 labelPos = drawPos + new Vector2(HalibutUIAsset.Resurrection.Width / 2, -36);
                Utils.DrawBorderString(spriteBatch, labelText, labelPos + new Vector2(1, 1),
                    Color.Black * 0.8f, 0.75f, 0.5f, 0.5f);
                Utils.DrawBorderString(spriteBatch, labelText, labelPos,
                    new Color(100, 200, 255), 0.75f, 0.5f, 0.5f);
                if (lastKnownMax > 0f) {
                    string maxText = $"上限 {resurrectionSystem.MaxValue:F0}";
                    Vector2 maxPos = drawPos + new Vector2(HalibutUIAsset.Resurrection.Width / 2, 52);
                    Utils.DrawBorderString(spriteBatch, maxText, maxPos + new Vector2(1, 1), Color.Black * 0.6f, 0.6f, 0.5f, 0.5f);
                    Utils.DrawBorderString(spriteBatch, maxText, maxPos, new Color(160, 230, 255), 0.6f, 0.5f, 0.5f);
                }
            }
        }
    }

    internal class ResurrectionParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public float Scale;
        public float Alpha;
        public int Life;
        public int MaxLife;
        public bool IsDead => Life >= MaxLife;
        public ResurrectionParticle(Vector2 position, Vector2 velocity, Color color) {
            Position = position;
            Velocity = velocity;
            Color = color;
            Scale = Main.rand.NextFloat(0.5f, 1.2f);
            Alpha = 1f;
            Life = 0;
            MaxLife = Main.rand.Next(30, 60);
        }
        public void Update() {
            Life++;
            Position += Velocity;
            Velocity.Y += 0.1f;
            Velocity *= 0.98f;
            float lifeRatio = Life / (float)MaxLife;
            Alpha = 1f - lifeRatio;
            Scale *= 0.98f;
        }
        public void Draw(SpriteBatch spriteBatch) {
            if (IsDead) {
                return;
            }
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color drawColor = Color * Alpha;
            spriteBatch.Draw(pixel, Position, new Rectangle(0, 0, 1, 1),
                drawColor, 0f, new Vector2(0.5f, 0.5f),
                Scale * 2f, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, Position, new Rectangle(0, 0, 1, 1),
                drawColor with { A = 0 } * 0.5f, 0f, new Vector2(0.5f, 0.5f),
                Scale * 4f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 改良飞行粒子：从研究槽位飞向复苏条
    /// </summary>
    internal class ImproveFlyParticle
    {
        private Vector2 startPos;
        private Vector2 currentPos;
        private float time;
        private float delay;
        private float progress;
        private float duration;
        private float rotation;
        private float scale;
        public bool Arrived;
        public ImproveFlyParticle(Vector2 start, float delayFrames) {
            startPos = start;
            currentPos = start;
            delay = delayFrames;
            duration = 50f + Main.rand.NextFloat(10f);
            rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            scale = Main.rand.NextFloat(0.6f, 1.1f);
            Arrived = false;
        }
        public void Update(ResurrectionUI ui) {
            if (Arrived) {
                return;
            }
            time++;
            if (time < delay) {
                return;
            }
            float t = (time - delay) / duration;
            if (t >= 1f) {
                t = 1f;
                Arrived = true;
            }
            progress = EaseOutCubic(t);
            Vector2 target = ui.GetBarCenter();
            Vector2 mid = (startPos + target) / 2f + new Vector2(0, -40f - Main.rand.NextFloat(30f));
            currentPos = Bezier(startPos, mid, target, progress);
            rotation += 0.15f;
        }
        public void Draw(SpriteBatch spriteBatch) {
            if (Arrived) {
                return;
            }
            if (time < delay) {
                return;
            }
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float alpha = 1f - progress * 0.3f;
            Color col = Color.Lerp(new Color(80, 180, 255), new Color(200, 240, 255), progress) * alpha;
            Vector2 size = new Vector2(6f * scale, 2f * scale);
            spriteBatch.Draw(pixel, currentPos, new Rectangle(0, 0, 1, 1), col, rotation, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, currentPos, new Rectangle(0, 0, 1, 1), col * 0.5f, rotation + MathHelper.PiOver2, new Vector2(0.5f, 0.5f), size * 0.6f, SpriteEffects.None, 0f);
        }
        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
        private static float EaseOutCubic(float t) {
            return 1f - (float)Math.Pow(1f - t, 3f);
        }
    }

    /// <summary>
    /// 改良脉冲：到达后在复苏条位置扩散的光圈
    /// </summary>
    internal class ImprovePulse
    {
        private Vector2 center;
        private float progress;
        private const float Duration = 40f;
        public bool Finished => progress >= 1f;
        public ImprovePulse(Vector2 c) {
            center = c;
            progress = 0f;
        }
        public void Update() {
            if (Finished) {
                return;
            }
            progress += 1f / Duration;
            if (progress > 1f) {
                progress = 1f;
            }
        }
        public void Draw(SpriteBatch spriteBatch) {
            if (Finished) {
                return;
            }
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            float eased = EaseOutCubic(progress);
            float radius = MathHelper.Lerp(10f, 95f, eased);
            float thickness = MathHelper.Lerp(8f, 1.5f, eased);
            float alpha = 1f - eased;
            Color col = Color.Lerp(new Color(90, 190, 255), new Color(200, 230, 255), eased) * alpha * 0.8f;
            int segs = 60;
            float step = MathHelper.TwoPi / segs;
            for (int i = 0; i < segs; i++) {
                float a1 = i * step;
                float a2 = (i + 1) * step;
                Vector2 p1 = center + a1.ToRotationVector2() * radius;
                Vector2 p2 = center + a2.ToRotationVector2() * radius;
                Vector2 diff = p2 - p1;
                float len = diff.Length();
                float rot = diff.ToRotation();
                spriteBatch.Draw(pixel, p1, new Rectangle(0, 0, 1, 1), col, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
            }
        }
        private static float EaseOutCubic(float t) {
            return 1f - (float)Math.Pow(1f - t, 3f);
        }
    }
}
