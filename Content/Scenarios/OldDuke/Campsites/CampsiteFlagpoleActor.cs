using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.Campsites
{
    /// <summary>营地旗杆</summary>
    internal class CampsiteFlagpoleActor : Actor
    {
        private float swayTimer;

        public override void OnSpawn(params object[] args) {
            Width = 60;
            Height = 160;
            DrawExtendMode = 200;
            DrawLayer = ActorDrawLayer.BeforePlayers;
            swayTimer = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            swayTimer += 0.02f;
            if (swayTimer > MathHelper.TwoPi) {
                swayTimer -= MathHelper.TwoPi;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (OldDukeCampsite.Oldflagpole == null) {
                return false;
            }

            Texture2D flagTexture = OldDukeCampsite.Oldflagpole;
            Vector2 screenPos = Position - Main.screenPosition;
            Vector2 origin = new Vector2(flagTexture.Width / 2f, flagTexture.Height);

            float swayAmount = MathF.Sin(swayTimer * 2f) * 0.08f;
            Color lc = Lighting.GetColor((Position / 16).ToPoint());

            spriteBatch.Draw(flagTexture, screenPos, null, lc, swayAmount, origin, 1f, SpriteEffects.None, 0f);

            //飘动重影
            for (int i = 1; i <= 2; i++) {
                float offsetAmount = i * 3f;
                float alpha = 0.3f / i;
                Vector2 offset = new Vector2(-offsetAmount * MathF.Sin(swayAmount), 0);

                spriteBatch.Draw(flagTexture, screenPos + offset, null, lc * alpha, swayAmount, origin, 1f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
