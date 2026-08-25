using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.Abyssrends
{
    /// <summary>深渊水团，真 alpha 暗体，沿速度拉成条，禁加色实心球</summary>
    internal class PRT_AbyssGlob : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 280;

        private float stretch;
        private float seed;

        public PRT_AbyssGlob Configure(int lifetime, float stretchMul = 1f) {
            Lifetime = lifetime;
            stretch = stretchMul;
            return this;
        }

        public override void Reset() {
            base.Reset();
            stretch = 1f;
            seed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            seed = Main.rand.NextFloat(64f);
            if (Lifetime <= 0) {
                Lifetime = 18;
            }
        }

        public override void AI() {
            Velocity *= 0.94f;
            Velocity.Y += 0.04f;
            float lc = LifetimeCompletion;
            Opacity = (1f - lc) * (1f - lc);
            Scale *= 0.985f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            float speed = Velocity.Length();
            float rot = speed > 0.4f ? Velocity.ToRotation() + MathHelper.PiOver2 : Rotation;
            float longAxis = MathHelper.Clamp(1f + speed * 0.12f, 1f, 2.6f) * stretch;
            var scale = new Vector2(Scale * 0.55f, Scale * 0.55f * longAxis);
            Color col = Color * Opacity;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, col, rot
                , tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>表层荧光星屑，只做点状加色，不当事体</summary>
    internal class PRT_AbyssSpark : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SoftGlow";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 220;

        public PRT_AbyssSpark Configure(int lifetime) {
            Lifetime = lifetime;
            return this;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 12;
            }
        }

        public override void AI() {
            Velocity *= 0.9f;
            float lc = LifetimeCompletion;
            Opacity = 1f - lc;
            Scale *= 0.96f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Color col = Color;
            col.A = 0;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, col * Opacity, Rotation
                , tex.Size() * 0.5f, Scale * 0.22f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
