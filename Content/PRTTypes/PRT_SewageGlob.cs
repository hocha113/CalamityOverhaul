using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.PRTTypes
{
    /// <summary>
    /// 浓稠污水团：比鬼雨滴更重更黏，速度拉伸成条。<br/>
    /// 汇聚模式下弧线扑向目标点被吸收；散落模式下吃重力，触地摊成一滩后熄。
    /// </summary>
    internal class PRT_SewageGlob : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 400;

        private Color initialColor;
        private Vector2 homeTarget;
        private bool homing;
        private bool splatting;
        private int splatTicks;
        private float wobbleSeed;

        /// <summary>散落模式：重力下坠，触地摊开</summary>
        public PRT_SewageGlob Configure(int lifetime) {
            Lifetime = lifetime;
            homing = false;
            initialColor = Color;
            wobbleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        /// <summary>汇聚模式：弧线扑向目标点，抵达即被吸收</summary>
        public PRT_SewageGlob Configure(int lifetime, Vector2 target) {
            Lifetime = lifetime;
            homeTarget = target;
            homing = true;
            initialColor = Color;
            wobbleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            homeTarget = default;
            homing = false;
            splatting = false;
            splatTicks = 0;
            wobbleSeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 40;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            if (splatting) {
                Velocity = Vector2.Zero;
                splatTicks++;
                Color = Color.Lerp(initialColor, Color.Transparent, splatTicks / 10f);
                if (splatTicks >= 10) {
                    active = false;
                }
                return;
            }

            if (homing) {
                Vector2 toTarget = homeTarget - Position;
                float distance = toTarget.Length();
                //末段速度可达 11px/帧，命中半径放宽；越过目标即视作被吸收，防止绕圈
                bool passed = LifetimeCompletion > 0.35f
                    && Vector2.Dot(toTarget, Velocity) < 0f;
                if (distance < 14f || passed || Time >= Lifetime - 1) {
                    active = false;
                    return;
                }
                //吸力随时间增强，末段几乎直扑
                float pull = MathHelper.Lerp(0.05f, 0.4f, LifetimeCompletion);
                Vector2 desired = toTarget.SafeNormalize(Vector2.Zero)
                    * MathHelper.Clamp(distance * 0.14f, 2.2f, 11f);
                Velocity = Vector2.Lerp(Velocity, desired, pull);
            }
            else {
                Velocity.X *= 0.985f;
                Velocity.Y = Math.Min(Velocity.Y + 0.42f, 13f);
                //触地摊开
                if (Velocity.Y > 1.5f
                    && Collision.SolidCollision(Position - new Vector2(2f, 2f), 4, 4)) {
                    splatting = true;
                    return;
                }
            }

            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            float t = LifetimeCompletion;
            if (t > 0.8f) {
                Color = Color.Lerp(initialColor, Color.Transparent, (t - 0.8f) / 0.2f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            if (splatting) {
                //摊成一滩横向水渍
                float k = splatTicks / 10f;
                Vector2 scale = new Vector2(0.5f * (1f + k * 1.6f), 0.12f) * Scale;
                spriteBatch.Draw(tex, pos, null, Color, 0f, origin, scale, SpriteEffects.None, 0f);
                return false;
            }

            //黏稠团：速度拉伸 + 呼吸蠕动
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.07f, 0f, 1.1f);
            float wobble = 1f + MathF.Sin(Time * 0.5f + wobbleSeed) * 0.12f;
            Vector2 body = new Vector2(0.30f * wobble * (1f - stretch * 0.3f),
                0.34f * (1f + stretch * 1.7f)) * Scale;
            spriteBatch.Draw(tex, pos, null, Color, Rotation, origin, body, SpriteEffects.None, 0f);
            //暗核，加一层浓度
            spriteBatch.Draw(tex, pos, null, Color * 0.7f, Rotation, origin,
                body * new Vector2(0.55f, 0.8f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
