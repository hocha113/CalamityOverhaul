using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_LonginusStar : BasePRT
    {
        public Color InitialColor;
        public bool AffectedByGravity;
        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        public override string Texture => CWRConstant.Masking + "Extra_98";
        private Entity Entity;
        private Vector2 EntityPos;
        private Vector2 OldEntityPos;
        private Vector2 EntityVariation;

        public override bool CanPool => true;
        public void Configure(bool affectedByGravity, int lifetime, Entity entity = null) {
            AffectedByGravity = affectedByGravity;
            Lifetime = lifetime;
            Entity = entity;
            InitialColor = Color;
        }

        public override void Reset() {
            base.Reset();
            InitialColor = default;
            AffectedByGravity = false;
            Entity = null;
            EntityPos = default;
            OldEntityPos = default;
            EntityVariation = default;
        }

        public override void AI() {
            Scale *= 0.95f;
            Color = Color.Lerp(InitialColor, Color.Transparent, (float)Math.Pow(LifetimeCompletion, 3D));
            Velocity *= 0.95f;
            if (Velocity.Length() < 12f && AffectedByGravity) {
                Velocity.X *= 0.94f;
                Velocity.Y += 0.25f;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            if (Entity != null) {
                OldEntityPos = EntityPos;
                EntityPos = Entity.Center;
                if (OldEntityPos != Vector2.Zero) {
                    EntityVariation = OldEntityPos.To(EntityPos);
                    Position += EntityVariation;
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Vector2 scale = new Vector2(0.5f, 1.6f) * Scale;
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];

            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color
                , Rotation, texture.Size() * 0.5f, scale, 0, 0f);
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color
                , Rotation, texture.Size() * 0.5f, scale * new Vector2(0.45f, 1f), 0, 0f);
            return false;
        }
    }
}
