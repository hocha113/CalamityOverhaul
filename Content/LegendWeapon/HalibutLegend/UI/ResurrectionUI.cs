using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
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

        //危险阈值
        private const float DangerThreshold = 0.7f; //70%以上开始警告
        private const float CriticalThreshold = 0.9f; //90%以上进入危险状态

        /// <summary>
        /// 获取玩家的复苏系统
        /// </summary>
        private ResurrectionSystem GetResurrectionSystem() {
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return halibutPlayer.ResurrectionSystem;
            }
            return null;
        }

        /// <summary>
        /// 获取复苏进度比例（0-1）
        /// </summary>
        public float ResurrectionRatio {
            get {
                var system = GetResurrectionSystem();
                return system?.Ratio ?? 0f;
            }
        }

        /// <summary>
        /// 获取当前状态对应的颜色
        /// </summary>
        private Color GetStateColor(float ratio) {
            if (ratio >= CriticalThreshold) {
                //危险状态：红色闪烁
                float flash = (float)Math.Sin(warningFlashTimer * 8f) * 0.5f + 0.5f;
                return Color.Lerp(new Color(255, 50, 50), new Color(255, 150, 0), flash);
            }
            else if (ratio >= DangerThreshold) {
                //警告状态：橙黄色
                return Color.Lerp(new Color(255, 200, 50), new Color(255, 100, 50), ratio - DangerThreshold);
            }
            else if (ratio >= 0.4f) {
                //中等状态：黄色到橙色渐变
                return Color.Lerp(new Color(100, 200, 255), new Color(255, 200, 50), (ratio - 0.4f) / 0.3f);
            }
            else {
                //安全状态：蓝色
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

            //位置设置：在大比目鱼头像下方
            Vector2 headPos = HalibutUIHead.Instance.DrawPosition;
            Vector2 headSize = HalibutUIHead.Instance.Size;
            DrawPosition = headPos + new Vector2(headSize.X / 2 + 20, 40);
            Size = HalibutUIAsset.Resurrection.Size();

            //平滑过渡显示值
            float targetValue = resurrectionSystem.CurrentValue;
            displayValue = MathHelper.Lerp(displayValue, targetValue, SmoothSpeed * 0.7f);

            float ratio = resurrectionSystem.Ratio;

            //更新脉动计时器
            pulseTimer += 0.1f;
            warningFlashTimer += 0.1f;

            //根据复苏值调整视觉效果强度
            if (ratio >= CriticalThreshold) {
                //危险状态：强烈抖动
                shakeIntensity = MathHelper.Lerp(shakeIntensity, 3f, 0.2f);
                glowIntensity = MathHelper.Lerp(glowIntensity, 1f, 0.2f);
            }
            else if (ratio >= DangerThreshold) {
                //警告状态：轻微抖动
                shakeIntensity = MathHelper.Lerp(shakeIntensity, 1.5f, 0.15f);
                glowIntensity = MathHelper.Lerp(glowIntensity, 0.6f, 0.15f);
            }
            else {
                //安全状态：无抖动
                shakeIntensity = MathHelper.Lerp(shakeIntensity, 0f, 0.1f);
                glowIntensity = MathHelper.Lerp(glowIntensity, 0.2f, 0.1f);
            }

            //计算抖动偏移
            if (shakeIntensity > 0.1f) {
                shakeOffset = new Vector2(
                    (float)(Math.Sin(pulseTimer * 20f) * shakeIntensity),
                    (float)(Math.Cos(pulseTimer * 15f) * shakeIntensity * 0.5f)
                );
            }
            else {
                shakeOffset = Vector2.Zero;
            }

            //更新粒子
            for (int i = particles.Count - 1; i >= 0; i--) {
                particles[i].Update();
                if (particles[i].IsDead) {
                    particles.RemoveAt(i);
                }
            }

            //生成粒子（危险状态时更频繁）
            if (ratio > DangerThreshold) {
                particleSpawnTimer++;
                int spawnRate = ratio >= CriticalThreshold ? 3 : 8;
                if (particleSpawnTimer >= spawnRate) {
                    particleSpawnTimer = 0;
                    SpawnParticle(ratio);
                }
            }

            UIHitBox = DrawPosition.GetRectangle(Size);
        }

        /// <summary>
        /// 生成粒子效果
        /// </summary>
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

            //绘制百分比文本（悬停时显示）
            Rectangle hitBox = new Rectangle((int)drawPos.X, (int)drawPos.Y,
                HalibutUIAsset.Resurrection.Width, HalibutUIAsset.Resurrection.Height);

            if (hitBox.Contains(Main.MouseScreen.ToPoint())) {
                string percentText = $"{(int)(ratio * 100)}%";
                Vector2 textPos = drawPos + new Vector2(HalibutUIAsset.Resurrection.Width / 2, -20);

                //文本阴影
                Utils.DrawBorderString(spriteBatch, percentText, textPos + new Vector2(1, 1),
                    Color.Black * 0.8f, 0.9f, 0.5f, 0.5f);

                //文本主体
                Color textColor = GetStateColor(ratio);
                Utils.DrawBorderString(spriteBatch, percentText, textPos,
                    textColor, 0.9f, 0.5f, 0.5f);

                //绘制"深渊复苏"标签
                string labelText = "深渊复苏";

                Vector2 labelPos = drawPos + new Vector2(HalibutUIAsset.Resurrection.Width / 2, -36);

                Utils.DrawBorderString(spriteBatch, labelText, labelPos + new Vector2(1, 1),
                    Color.Black * 0.8f, 0.75f, 0.5f, 0.5f);

                Utils.DrawBorderString(spriteBatch, labelText, labelPos,
                    new Color(100, 200, 255), 0.75f, 0.5f, 0.5f);
            }
        }
    }

    /// <summary>
    /// 复苏条粒子效果
    /// </summary>
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
            Velocity.Y += 0.1f; //重力
            Velocity *= 0.98f; //阻力

            //淡出
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

            //绘制粒子核心
            spriteBatch.Draw(pixel, Position, new Rectangle(0, 0, 1, 1),
                drawColor, 0f, new Vector2(0.5f, 0.5f),
                Scale * 2f, SpriteEffects.None, 0f);

            //绘制粒子光晕
            spriteBatch.Draw(pixel, Position, new Rectangle(0, 0, 1, 1),
                drawColor with { A = 0 } * 0.5f, 0f, new Vector2(0.5f, 0.5f),
                Scale * 4f, SpriteEffects.None, 0f);
        }
    }
}
