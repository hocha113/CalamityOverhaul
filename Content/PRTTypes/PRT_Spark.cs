using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_Spark : BasePRT
    {
        public Color InitialColor;
        public bool AffectedByGravity;
        public Entity entity;
        public override int InGame_World_MaxCount => 8000;//用量大
        public override bool CanPool => true;
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public PRT_Spark Configure(bool affectedByGravity, int lifetime, Entity entity = null) {
            InitialColor = Color;
            AffectedByGravity = affectedByGravity;
            Lifetime = lifetime;
            this.entity = entity;
            return this;
        }
        public override void Reset() {
            base.Reset();
            InitialColor = default;
            AffectedByGravity = false;
            entity = null;
        }
        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
        public override void AI() {
            Scale *= 0.95f;
            Color = Color.Lerp(InitialColor, Color.Transparent, (float)Math.Pow(LifetimeCompletion, 3D));
            Velocity *= 0.95f;
            if (Velocity.Length() < 12f && AffectedByGravity) {
                Velocity.X *= 0.94f;
                Velocity.Y += 0.25f;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            if (entity != null) {
                if (entity.active) {
                    Position += entity.velocity;
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Vector2 scale = new Vector2(0.5f, 1.6f) * Scale;
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];

            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation
                , texture.Size() * 0.5f, scale, 0, 0f);
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color, Rotation
                , texture.Size() * 0.5f, scale * new Vector2(0.45f, 1f), 0, 0f);

            return false;
        }
    }
}
