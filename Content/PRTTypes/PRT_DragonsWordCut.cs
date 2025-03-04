using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    internal class PRT_DragonsWordCut : BasePRT
    {
        public Color InitialColor;
        public bool AffectedByGravity;
        public float Ylength = 1f;
        public float Xlength = 0.6f;
        public override string Texture => "CalamityMod/Particles/LargeSpark";
        public PRT_DragonsWordCut(Vector2 relativePosition, Vector2 velocity
            , bool affectedByGravity, int lifetime, float scale, Color color) {
            Position = relativePosition;
            Velocity = velocity;
            AffectedByGravity = affectedByGravity;
            Scale = scale;
            Lifetime = lifetime;
            Color = InitialColor = color;
        }

        public override void SetProperty() {
            SetLifetime = true;
            PRTDrawMode = PRTDrawModeEnum.NonPremultiplied;
        }

        public override void AI() {
            Scale *= 0.9f;
            Color = Color.Lerp(InitialColor, Color.Transparent, (float)Math.Pow(LifetimeCompletion, 3D));
            Velocity *= 0.95f;
            Ylength *= 1.25f;
            Xlength *= 0.7f;

            if (Velocity.Length() < 12f && AffectedByGravity) {
                Velocity.X *= 0.94f;
                Velocity.Y += 0.25f;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Vector2 scale = new Vector2(Xlength, Ylength) * Scale;
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(texture, Position - Main.screenPosition, null
                , Color.Gold, Rotation, texture.Size() * 0.5f, scale * new Vector2(0.85f, 1f), 0, 0f);
            spriteBatch.Draw(texture, Position - Main.screenPosition
                , null, Color, Rotation, texture.Size() * 0.5f, scale, 0, 0f);
            return false;
        }
    }
}
