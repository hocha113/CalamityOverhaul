using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 营地锅上升的蒸汽粒子，强化状态下速度更快范围更广
    /// </summary>
    internal class PRT_CampfireSteam : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";

        private bool isEnhanced;
        private float wobblePhase;

        public override bool CanPool => true;

        public PRT_CampfireSteam Configure(int lifetime, bool enhanced = false) {
            Lifetime = lifetime;
            isEnhanced = enhanced;
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            isEnhanced = false;
            wobblePhase = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 0f;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(45, 75);
            }
            Color = VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(),
                isEnhanced ? Color.Yellow : Color.Yellow, isEnhanced ? Color.Orange : Color.YellowGreen);
        }

        public override void AI() {
            wobblePhase += 0.08f;
            Velocity.X += MathF.Sin(wobblePhase) * (isEnhanced ? 0.045f : 0.03f);
            Velocity.Y *= isEnhanced ? 0.96f : 0.98f;
            Velocity.X *= 0.99f;

            Scale += isEnhanced ? 0.012f : 0.008f;
            Rotation += isEnhanced ? 0.025f : 0.015f;

            //淡入淡出：前段快速淡入，随后随生命周期正弦衰减
            if (Time < 10) {
                Opacity = Time / 10f;
            }
            else {
                Opacity = MathF.Sin(LifetimeCompletion * MathHelper.Pi);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            float drawAlpha = isEnhanced ? Opacity * 0.7f : Opacity * 0.5f;

            spriteBatch.Draw(texture, drawPos, null, Color with { A = 0 } * drawAlpha,
                Rotation, origin, Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, drawPos, null, Color with { A = 0 } * (Opacity * 0.7f),
                Rotation, origin, Scale * 0.5f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
