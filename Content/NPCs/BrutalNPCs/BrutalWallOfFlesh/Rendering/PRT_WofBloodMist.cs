using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering
{
    /// <summary>血雾团：Fog 贴图染暗红，慢漂+缓涨+消散</summary>
    internal class PRT_WofBloodMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;

        private float drift;
        private SpriteEffects mirror;

        public PRT_WofBloodMist Configure(int lt, float opacity) {
            Lifetime = lt;
            Opacity = opacity;
            return this;
        }

        public override void Reset() {
            base.Reset();
            drift = 0f;
            mirror = SpriteEffects.None;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            drift = Main.rand.NextFloat(-0.012f, 0.012f);
            mirror = Main.rand.NextBool() ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(55, 90);
            }
            if (Opacity <= 0f) {
                Opacity = 0.5f;
            }
        }

        public override void AI() {
            Scale += 0.006f;
            Rotation += drift;
            Velocity *= 0.965f;

            float fade = Utils.GetLerpValue(1f, 0.6f, LifetimeCompletion, true)
                * Utils.GetLerpValue(0f, 0.12f, LifetimeCompletion, true);
            Color = new Color(120, 20, 26) * (Opacity * fade);
            if (LifetimeCompletion >= 1f) {
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation, tex.Size() / 2f, Scale, mirror, 0);
            return false;
        }
    }
}
