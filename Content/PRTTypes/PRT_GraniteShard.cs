using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>花岗棱角晶片，落地弹一次，二触碎光</summary>
    internal class PRT_GraniteShard : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private bool useGravity;
        private float gravity;
        private bool canBounce;
        private bool bounced;
        private float spin;

        public PRT_GraniteShard Configure(int lifetime, bool gravity = true
            , float gravityStrength = 0.18f, bool bounce = true, float spinSpeed = 0.22f) {
            Lifetime = lifetime;
            useGravity = gravity;
            this.gravity = gravityStrength;
            canBounce = bounce;
            spin = spinSpeed * (Main.rand.NextBool() ? 1f : -1f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            useGravity = false;
            gravity = 0f;
            canBounce = false;
            bounced = false;
            spin = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            //Lifetime<=0 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 40);
                useGravity = true;
                gravity = 0.18f;
                canBounce = true;
                spin = 0.22f * (Main.rand.NextBool() ? 1f : -1f);
            }
        }

        public override void AI() {
            if (useGravity && Velocity.Y < 15f) {
                Velocity.Y += gravity;
            }
            Velocity.X *= 0.985f;
            Rotation += spin * (0.6f + Math.Abs(Velocity.X) * 0.08f);

            //落地弹一次，二触碎光
            if (Velocity.Y > 0f && Collision.SolidCollision(Position - new Vector2(3f), 6, 6)) {
                if (canBounce && !bounced) {
                    bounced = true;
                    Velocity.Y = -Math.Abs(Velocity.Y) * 0.45f;
                    Velocity.X *= 0.6f;
                    spin *= 1.6f;
                }
                else {
                    Velocity *= 0.3f;
                    if (Lifetime - Time > 7) {
                        Time = Lifetime - 7;
                    }
                }
            }

            Opacity = MathHelper.Clamp(Time / 4f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 4f, 0f, 1f);
            Lighting.AddLight(Position, Color.ToVector3() * 0.24f * Opacity);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;
            Color edge = Color; edge.A = 0;
            Vector2 main = new Vector2(0.32f, 1.05f) * Scale;
            Vector2 facet = new Vector2(0.22f, 0.62f) * Scale;

            spriteBatch.Draw(glow, pos, null, edge * 0.35f * Opacity, 0f, glow.Size() / 2f, Scale * 0.28f, SpriteEffects.None, 0f);
            //主晶面+斜切副面
            spriteBatch.Draw(tex, pos, null, edge * Opacity, Rotation, origin, main, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, edge * 0.7f * Opacity, Rotation + 1.25f, origin, facet, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color.White * 0.75f * Opacity, Rotation, origin, main * new Vector2(0.45f, 0.8f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
