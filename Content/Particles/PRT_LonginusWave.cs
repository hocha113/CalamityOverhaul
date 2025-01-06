using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using static CalamityMod.CalamityUtils;

namespace CalamityOverhaul.Content.Particles
{
    internal class PRT_LonginusWave : BasePRT
    {
        public override string Texture => "CalamityMod/Particles/HollowCircleHardEdge";
        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            SetLifetime = true;
        }
        private float OriginalScale;
        private float FinalScale;
        private Vector2 Squish;
        private Color BaseColor;
        private Entity Entity;
        private Vector2 EntityPos;
        private Vector2 OldEntityPos;
        private Vector2 EntityVariation;
        public PRT_LonginusWave(Vector2 position, Vector2 velocity, Color color, Vector2 squish, float rotation, float originalScale, float finalScale, int lifeTime, Entity entity) {
            Position = position;
            Velocity = velocity;
            BaseColor = color;
            OriginalScale = originalScale;
            FinalScale = finalScale;
            Scale = originalScale;
            Lifetime = lifeTime;
            Squish = squish;
            Rotation = rotation;
            Entity = entity;
        }

        public override void AI() {
            float pulseProgress = PiecewiseAnimation(LifetimeCompletion, new CurveSegment[] { new CurveSegment(EasingType.PolyOut, 0f, 0f, 1f, 4) });
            Scale = MathHelper.Lerp(OriginalScale, FinalScale, pulseProgress);

            Opacity = (float)Math.Sin(MathHelper.PiOver2 + LifetimeCompletion * MathHelper.PiOver2);

            Color = BaseColor * Opacity;
            Lighting.AddLight(Position, Color.R / 255f, Color.G / 255f, Color.B / 255f);
            Velocity *= 0.95f;

            if (Entity != null) {
                OldEntityPos = EntityPos;
                EntityPos = Entity.Center;
                if (OldEntityPos != Vector2.Zero) {
                    EntityVariation = OldEntityPos.To(EntityPos);
                    Position += EntityVariation;
                }
                //Projectile projectile = ((Projectile)Entity);
                //if (projectile != null)
                //    Rotation = projectile.rotation;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * Opacity
                , Rotation, tex.Size() / 2f, Scale * Squish, SpriteEffects.None, 0);
            return false;
        }
    }
}
