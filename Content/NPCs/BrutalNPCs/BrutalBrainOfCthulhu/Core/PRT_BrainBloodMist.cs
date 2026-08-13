using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core
{
    /// <summary>血雾团：Fog 单帧真 alpha，AlphaBlend 染色，随机旋转+镜像防贴纸感</summary>
    internal class PRT_BrainBloodMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private Color initialColor;
        private float spinRate;
        private SpriteEffects mirror;

        public PRT_BrainBloodMist Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spinRate = Main.rand.NextFloat(-0.02f, 0.02f);
            mirror = Main.rand.NextBool() ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spinRate = 0f;
            mirror = SpriteEffects.None;
        }

        public override void AI() {
            Velocity *= 0.955f;
            Rotation += spinRate;
            float t = LifetimeCompletion;
            //先散开再散尽
            Scale += 0.012f;
            Color = initialColor * (1f - MathF.Pow(t, 1.6f));
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color,
                Rotation, tex.Size() * 0.5f, Scale * 0.6f, mirror, 0f);
            return false;
        }
    }
}
