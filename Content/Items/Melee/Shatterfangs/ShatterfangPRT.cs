using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee.Shatterfangs
{
    /// <summary>
    /// 牙骨碎屑，实体感 AlphaBlend 双面小片，落地弹一次、二触即碎
    /// </summary>
    internal class PRT_ToothChip : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float gravity;
        private bool bounced;
        private float spin;
        /// <summary>0~1 根端血染程度</summary>
        private float bloodRoot;

        public PRT_ToothChip Configure(int lifetime, float gravityStrength = 0.22f, float bloodTint = 0f) {
            Lifetime = lifetime;
            gravity = gravityStrength;
            bloodRoot = bloodTint;
            spin = Main.rand.NextFloat(0.16f, 0.32f) * (Main.rand.NextBool() ? 1f : -1f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            gravity = 0f;
            bounced = false;
            spin = 0f;
            bloodRoot = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            //Lifetime<=0 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(24, 40);
                gravity = 0.22f;
                spin = 0.24f * (Main.rand.NextBool() ? 1f : -1f);
            }
        }

        public override void AI() {
            if (Velocity.Y < 16f) {
                Velocity.Y += gravity;
            }
            Velocity.X *= 0.985f;
            Rotation += spin * (0.6f + Math.Abs(Velocity.X) * 0.07f);

            if (Velocity.Y > 0f && Collision.SolidCollision(Position - new Vector2(3f), 6, 6)) {
                if (!bounced) {
                    bounced = true;
                    Velocity.Y = -Math.Abs(Velocity.Y) * 0.4f;
                    Velocity.X *= 0.55f;
                    spin *= 1.5f;
                }
                else {
                    Velocity *= 0.25f;
                    spin *= 0.75f;
                    if (Lifetime - Time > 6) {
                        Time = Lifetime - 6;
                    }
                }
            }

            Opacity = MathHelper.Clamp(Time / 3f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 5f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            Vector2 main = new Vector2(0.30f, 0.92f) * Scale;
            Vector2 facet = new Vector2(0.20f, 0.58f) * Scale;

            //斜切暗面垫底+象牙主面
            Color dark = Color.Lerp(Color, ShatterfangFX.IvoryDark, 0.72f) * (0.85f * Opacity);
            spriteBatch.Draw(tex, pos, null, dark, Rotation + 1.15f, origin, facet, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, main, SpriteEffects.None, 0f);

            //根端血染，沿长轴偏一头
            if (bloodRoot > 0.01f) {
                Vector2 rootOff = (Rotation + MathHelper.PiOver2).ToRotationVector2() * (tex.Height * main.Y * 0.2f);
                spriteBatch.Draw(tex, pos + rootOff, null, ShatterfangFX.BloodMain * (bloodRoot * Opacity)
                    , Rotation, origin, main * new Vector2(0.8f, 0.42f), SpriteEffects.None, 0f);
            }

            //细窄高光面
            spriteBatch.Draw(tex, pos, null, Color.White * (0.5f * Opacity)
                , Rotation, origin, main * new Vector2(0.38f, 0.5f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
