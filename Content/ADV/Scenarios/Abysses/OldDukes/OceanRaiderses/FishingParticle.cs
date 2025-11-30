using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses
{
    internal enum FishingParticleType
    {
        Fish,
        Crate,
        Seashell
    }

    //钓鱼粒子类
    internal class FishingParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public FishingParticleType Type;
        public float Scale;
        public float Rotation;
        public int Life;
        private float alpha = 1f;

        //水龙卷螺旋运动参数
        private float spiralAngle;
        private float spiralRadius;
        private float spiralSpeed;
        private float heightProgress;
        private Vector2 vortexCenter;
        private Vector2 targetPos;
        private bool inVortex;
        private float swimPhase;

        public void Update(Vector2 targetPos) {
            this.targetPos = targetPos;
            Life--;

            if (!inVortex) {
                UpdateSwimming();
                CheckEnterVortex();
            }
            else {
                UpdateVortexMotion();
            }

            //旋转动画
            Rotation += inVortex ? 0.15f : 0.08f;
        }

        //水下游动阶段
        private void UpdateSwimming() {
            swimPhase += 0.08f;

            //模拟鱼类在水中的游动
            float waveOffset = (float)Math.Sin(swimPhase) * 3f;
            Vector2 swimDir = Vector2.UnitY * -1f;
            swimDir.X += waveOffset * 0.1f;

            Velocity = Vector2.Lerp(Velocity, swimDir * 2f, 0.1f);
            Position += Velocity;

            //随机游动偏移
            if (Main.rand.NextBool(15)) {
                Position += Main.rand.NextVector2Circular(2f, 1f);
            }
        }

        //检测是否进入水龙卷范围
        private void CheckEnterVortex() {
            float distanceToTarget = Vector2.Distance(Position, targetPos);

            //距离吸入口100像素内进入水龙卷效果
            if (distanceToTarget < (Life > 60 ? 100f : 300)) {
                inVortex = true;
                vortexCenter = targetPos;
                spiralAngle = (Position - targetPos).ToRotation();
                spiralRadius = distanceToTarget;
                spiralSpeed = 0.08f + Main.rand.NextFloat(0.04f);
                heightProgress = 0f;
            }
        }

        //水龙卷螺旋运动
        private void UpdateVortexMotion() {
            heightProgress += 0.015f;

            //螺旋上升
            spiralAngle += spiralSpeed;

            //半径逐渐缩小（上小下大）
            float targetRadius = MathHelper.Lerp(spiralRadius, 15f, heightProgress);
            spiralRadius = MathHelper.Lerp(spiralRadius, targetRadius, 0.1f);

            //计算螺旋位置
            Vector2 offset = spiralAngle.ToRotationVector2() * spiralRadius;

            //添加上下波动
            float verticalWave = (float)Math.Sin(spiralAngle * 3f) * 4f * (1f - heightProgress);

            //目标位置
            Vector2 targetPosition = vortexCenter + offset;
            targetPosition.Y -= heightProgress * 80f + verticalWave;

            //平滑移动
            Position = Vector2.Lerp(Position, targetPosition, 0.15f);

            //接近吸入口时淡出
            float distanceToCenter = Vector2.Distance(Position, vortexCenter);
            if (distanceToCenter < 25f) {
                alpha -= 0.08f;
                Scale *= 0.95f;
            }

            //速度随高度增加
            spiralSpeed += 0.001f;
        }

        public bool ShouldRemove() => Life <= 0 || alpha <= 0 || heightProgress >= 1.2f;

        public void Draw(SpriteBatch spriteBatch) {
            //预加载纹理
            Main.instance.LoadItem(ItemID.Bass);
            Main.instance.LoadItem(ItemID.WoodenCrate);
            Main.instance.LoadItem(ItemID.Seashell);

            Texture2D texture = Type switch {
                FishingParticleType.Fish => TextureAssets.Item[ItemID.Bass].Value,
                FishingParticleType.Crate => TextureAssets.Item[ItemID.WoodenCrate].Value,
                _ => TextureAssets.Item[ItemID.Seashell].Value
            };

            float fadeProgress = 1f - Life / 120f;
            Color drawColor = Color.White * alpha * (1f - fadeProgress * 0.5f);

            //在水龙卷中时添加发光效果
            if (inVortex) {
                float glowIntensity = (float)Math.Sin(heightProgress * MathHelper.Pi) * 0.6f;
                drawColor = Color.Lerp(drawColor, new Color(150, 220, 255), glowIntensity);
            }

            Vector2 drawPos = Position - Main.screenPosition;

            //绘制主体
            spriteBatch.Draw(texture, drawPos, null, drawColor, Rotation,
                texture.Size() / 2, Scale * 0.5f, SpriteEffects.None, 0);

            //在水龙卷中绘制光晕
            if (inVortex && alpha > 0.3f) {
                Color glowColor = new Color(100, 200, 255, 0) * (alpha * 0.4f);
                spriteBatch.Draw(texture, drawPos, null, glowColor, Rotation,
                    texture.Size() / 2, Scale * 0.65f, SpriteEffects.None, 0);
            }
        }
    }
}
