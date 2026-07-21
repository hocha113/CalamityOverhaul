using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>赛博汇聚，蓄力球</summary>
    internal class PRT_CyberConverge : BasePRT
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override int InGame_World_MaxCount => 4000;

        private Vector2 target;
        private float initialScale;
        private float rotationSpeed;
        private float aspectRatio;
        private Color edgeColor;
        private float chargeRatio; //0~1 色过渡

        public PRT_CyberConverge() {
            aspectRatio = 1f;
        }
        /// <param name="charge">蓄力比 0~1</param>
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
            Vector2 toTarget = target - Position;
            float distSq = toTarget.LengthSquared();
            if (distSq > 4f) {
                Vector2 desired = toTarget.SafeNormalize(Vector2.UnitX);
                float accel = 0.6f + (1f - distSq / (200f * 200f)) * 1.2f; //越近越快
                accel = MathHelper.Clamp(accel, 0.4f, 2.5f);
                Velocity += desired * accel;
                float maxSpeed = 12f;
                if (Velocity.LengthSquared() > maxSpeed * maxSpeed) {
                    Velocity = Velocity.SafeNormalize(Vector2.UnitX) * maxSpeed;
                }
            }

            Rotation += rotationSpeed;

            float life = LifetimeCompletion;
            float distFactor = MathF.Sqrt(MathHelper.Clamp(distSq / (80f * 80f), 0f, 1f));
            Scale = initialScale * MathHelper.Lerp(0.1f, 1f, distFactor) * (1f - MathF.Pow(life, 3f));

            float flicker = 0.75f + 0.25f * MathF.Sin(Time * 1.2f + chargeRatio * 10f);
            Opacity = flicker * (1f - MathF.Pow(life, 2f));

            if (distSq < 6f * 6f) {
                Scale *= 0.5f;
                Opacity *= 0.5f;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            if (Scale < 0.05f || Opacity < 0.01f) return false;

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Position - Main.screenPosition;

            float w = 5f * Scale;
            float h = 5f * Scale * aspectRatio;
            Vector2 size = new(w, h);
            Vector2 origin = new(0.5f, 0.5f);

            Color outer = edgeColor * Opacity * 0.35f;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), outer, Rotation,
                origin, size * 1.5f, SpriteEffects.None, 0f);

            Color inner = Color * Opacity;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), inner, Rotation,
                origin, size, SpriteEffects.None, 0f);

            Color core = Color.Lerp(inner, Color.White, 0.7f) * Opacity;
            spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), core, Rotation,
                origin, size * 0.35f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
