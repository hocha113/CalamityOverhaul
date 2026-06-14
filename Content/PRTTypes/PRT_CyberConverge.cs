using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>赛博汇聚粒子，蓄力能量球</summary>
    internal class PRT_CyberConverge : BasePRT
    {
        public override string Texture => CWRConstant.Placeholder;
        public override int InGame_World_MaxCount => 4000;

        private Vector2 target;
        private float initialScale;
        private float rotationSpeed;
        private float aspectRatio;
        private Color edgeColor;
        private float chargeRatio; //0~1，颜色过渡

        public PRT_CyberConverge() {
            aspectRatio = 1f;
        }
        /// <param name="position">粒子初始位置（外围）</param>
        /// <param name="targetPos">汇聚目标位置</param>
        /// <param name="mainColor">主颜色</param>
        /// <param name="edge">边缘颜色</param>
        /// <param name="scale">尺寸</param>
        /// <param name="lifeTime">生存时间</param>
        /// <param name="charge">当前蓄力比例 0~1</param>
        public PRT_CyberConverge(Vector2 position, Vector2 targetPos, Color mainColor, Color edge,
            float scale, int lifeTime, float charge = 0f) {
            Position = position;
            target = targetPos;
            Color = mainColor;
            edgeColor = edge;
            Scale = initialScale = scale;
            Lifetime = lifeTime;
            chargeRatio = charge;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            rotationSpeed = Main.rand.NextFloat(0.05f, 0.12f) * (Main.rand.NextBool() ? 1f : -1f);
            aspectRatio = Main.rand.NextFloat(0.4f, 1.2f);
            //初始速度朝向目标点
            Velocity = (targetPos - position).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(3f, 7f);
        }

        public override bool CanPool => true;
        public void Configure(Vector2 targetPos, Color edge, int lifeTime, float charge = 0f) {
            target = targetPos;
            edgeColor = edge;
            Lifetime = lifeTime;
            chargeRatio = charge;
            initialScale = Scale;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            rotationSpeed = Main.rand.NextFloat(0.05f, 0.12f) * (Main.rand.NextBool() ? 1f : -1f);
            aspectRatio = Main.rand.NextFloat(0.4f, 1.2f);
            Velocity = (target - Position).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(3f, 7f);
        }

        public override void Reset() {
            base.Reset();
            target = default;
            initialScale = 0f;
            rotationSpeed = 0f;
            aspectRatio = 1f;
            edgeColor = default;
            chargeRatio = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            //加速飞向目标
            Vector2 toTarget = target - Position;
            float distSq = toTarget.LengthSquared();
            if (distSq > 4f) {
                Vector2 desired = toTarget.SafeNormalize(Vector2.UnitX);
                float accel = 0.6f + (1f - distSq / (200f * 200f)) * 1.2f; //越近越快
                accel = MathHelper.Clamp(accel, 0.4f, 2.5f);
                Velocity += desired * accel;
                //限速
                float maxSpeed = 12f;
                if (Velocity.LengthSquared() > maxSpeed * maxSpeed) {
                    Velocity = Velocity.SafeNormalize(Vector2.UnitX) * maxSpeed;
                }
            }

            Rotation += rotationSpeed;

            //接近目标时缩小
            float life = LifetimeCompletion;
            float distFactor = MathF.Sqrt(MathHelper.Clamp(distSq / (80f * 80f), 0f, 1f));
            Scale = initialScale * MathHelper.Lerp(0.1f, 1f, distFactor) * (1f - MathF.Pow(life, 3f));

            //透明度
            float flicker = 0.75f + 0.25f * MathF.Sin(Time * 1.2f + chargeRatio * 10f);
            Opacity = flicker * (1f - MathF.Pow(life, 2f));

            //到达目标或寿命终结
            if (distSq < 6f * 6f) {
                Scale *= 0.5f;
                Opacity *= 0.5f;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.05f || Opacity < 0.01f) return false;

            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Vector2 drawPos = Position - Main.screenPosition;

            float w = 5f * Scale;
            float h = 5f * Scale * aspectRatio;
            Vector2 size = new(w, h);
            Vector2 origin = new(0.5f, 0.5f);

            //外层发光边
            Color outer = edgeColor * Opacity * 0.35f;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), outer, Rotation,
                origin, size * 1.5f, SpriteEffects.None, 0f);

            //内层实体
            Color inner = Color * Opacity;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), inner, Rotation,
                origin, size, SpriteEffects.None, 0f);

            //核心高亮
            Color core = Color.Lerp(inner, Color.White, 0.7f) * Opacity;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), core, Rotation,
                origin, size * 0.35f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
