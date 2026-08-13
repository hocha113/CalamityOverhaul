using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering
{
    /// <summary>血肉碎块：不规则剪影+重力翻滚+湿亮边，落地压扁</summary>
    internal class PRT_WofGore : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "HitJagged01";
        public override bool CanPool => true;

        private float spin;
        private bool landed;
        private SpriteEffects mirror;

        public PRT_WofGore Configure(int lt) {
            Lifetime = lt;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            landed = false;
            mirror = SpriteEffects.None;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(-0.16f, 0.16f);
            mirror = Main.rand.NextBool() ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(46, 80);
            }
            Opacity = 1f;
        }

        public override void AI() {
            if (!landed) {
                Velocity.Y += 0.34f;
                if (Velocity.Y > 16f) {
                    Velocity.Y = 16f;
                }
                Rotation += spin * (0.5f + Velocity.Length() * 0.05f);

                //落入实体块则压扁停驻
                Point tile = ((Position + Velocity) / 16f).ToPoint();
                if (WorldGen.InWorld(tile.X, tile.Y, 4) && WorldGen.SolidTile(tile.X, tile.Y)) {
                    landed = true;
                    Velocity = Vector2.Zero;
                }
            }
            else {
                Scale *= 0.985f;
            }

            //末段溶失
            float fade = Utils.GetLerpValue(1f, 0.72f, LifetimeCompletion, true);
            Opacity = fade;
            if (Opacity < 0.02f) {
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 pos = Position - Main.screenPosition;
            Vector2 orig = tex.Size() / 2f;
            float squash = landed ? 0.55f : 1f;
            Vector2 scale = new Vector2(Scale, Scale * squash);
            //暗肉底色
            Color meat = new Color(96, 14, 18) * Opacity;
            spriteBatch.Draw(tex, pos, null, meat, Rotation, orig, scale, mirror, 0);
            //湿亮偏移高光，读出体积
            Color wet = new Color(190, 42, 46) * (Opacity * 0.6f);
            spriteBatch.Draw(tex, pos - new Vector2(1.5f, 2f), null, wet, Rotation, orig, scale * 0.82f, mirror, 0);
            return false;
        }
    }
}
