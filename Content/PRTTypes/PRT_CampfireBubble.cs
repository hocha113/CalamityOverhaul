using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 营地锅内沸腾的小气泡粒子
    /// </summary>
    internal class PRT_CampfireBubble : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle6";

        private float driftPhase;

        public override bool CanPool => true;

        public PRT_CampfireBubble Configure(int lifetime) {
            Lifetime = lifetime;
            driftPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            driftPhase = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(20, 35);
            }
            Color = Main.rand.NextBool()
                ? new Color(140, 200, 120, 200)
                : new Color(160, 220, 140, 220);
        }

        public override void AI() {
            driftPhase += 0.15f;
            Velocity.X += MathF.Sin(driftPhase) * 0.01f;
            Velocity *= 0.98f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            float alpha = MathF.Sin(LifetimeCompletion * MathHelper.Pi);

            //外圈
            spriteBatch.Draw(texture, drawPos, null, Color * (alpha * 0.5f),
                0f, origin, Scale * 1.4f, SpriteEffects.None, 0f);
            //内核
            spriteBatch.Draw(texture, drawPos, null, Color * alpha,
                0f, origin, Scale * 0.7f, SpriteEffects.None, 0f);
            //高光
            Vector2 highlightOffset = new Vector2(-Scale * 1.5f, -Scale * 1.5f);
            spriteBatch.Draw(texture, drawPos + highlightOffset, null, new Color(255, 255, 255, 150) * alpha,
                0f, origin, Scale * 0.25f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
