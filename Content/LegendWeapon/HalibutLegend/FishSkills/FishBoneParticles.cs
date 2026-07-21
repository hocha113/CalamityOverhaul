using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>鱼骨调色板</summary>
    internal static class FishBonePalette
    {
        public static readonly Color Ivory = new(226, 216, 194);   //骨面象牙白
        public static readonly Color Aged = new(196, 180, 150);    //陈年骨黄
        public static readonly Color Shadow = new(122, 108, 88);   //骨缝暗褐
        public static readonly Color Chalk = new(212, 206, 194);   //钙粉灰白

        /// <summary>骨屑取色</summary>
        public static Color Chip() => Color.Lerp(Ivory, Aged, Main.rand.NextFloat());
    }

    /// <summary>鱼骨锐利骨屑</summary>
    internal class PRT_FishBoneShard : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float gravity;
        private float spin;
        private bool bounced;

        public PRT_FishBoneShard Configure(int lifetime, float gravityStrength = 0.3f) {
            Lifetime = lifetime;
            gravity = gravityStrength;
            return this;
        }

        public override void Reset() {
            base.Reset();
            gravity = 0f;
            spin = 0f;
            bounced = false;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spin = Main.rand.NextFloat(0.16f, 0.34f) * (Main.rand.NextBool() ? 1f : -1f);
            //防漏 Configure 兜底
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(22, 34);
            }
            if (gravity == 0f) {
                gravity = 0.3f;
            }
        }

        public override void AI() {
            if (Velocity.Y < 15f) {
                Velocity.Y += gravity;
            }
            Velocity.X *= 0.982f;
            Rotation += spin * (0.7f + Math.Abs(Velocity.X) * 0.06f);

            //落地弹一次，第二次触地快速收尾
            if (Velocity.Y > 0f && Collision.SolidCollision(Position - new Vector2(3f), 6, 6)) {
                if (!bounced) {
                    bounced = true;
                    Velocity.Y = -Math.Abs(Velocity.Y) * 0.35f;
                    Velocity.X *= 0.5f;
                    spin *= 1.5f;
                }
                else {
                    Velocity *= 0.2f;
                    if (Lifetime - Time > 6) {
                        Time = Lifetime - 6;
                    }
                }
            }

            Opacity = MathHelper.Clamp(Time / 3f, 0f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 4f, 0f, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            //哑光吃环境光，保底两成半防纯黑
            Color env = Lighting.GetColor(Position.ToTileCoordinates());
            Color lit = Color.Lerp(Color.MultiplyRGB(env), Color, 0.25f) * Opacity;

            //快时顺速度微拉长
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.035f, 0f, 0.5f);
            Vector2 body = new Vector2(0.2f, 0.62f * (1f + stretch)) * Scale;

            //短哑光残尾，一节旧位置减淡影
            if (Velocity.Length() > 2.5f) {
                spriteBatch.Draw(tex, pos - Velocity * 1.2f, null, lit * 0.25f
                    , Rotation - spin * 2f, origin, body * 0.85f, SpriteEffects.None, 0f);
            }

            //暗褐衬底压厚度，主晶面 + 斜切副晶面拼锐角
            spriteBatch.Draw(tex, pos, null, FishBonePalette.Shadow.MultiplyRGB(env) * (Opacity * 0.55f)
                , Rotation, origin, body * 1.12f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, lit, Rotation, origin, body, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, lit * 0.8f, Rotation + 1.15f, origin
                , body * new Vector2(0.85f, 0.55f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>钙粉尘雾</summary>
    internal class PRT_FishBoneDust : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private float spin;
        private float settle;

        public PRT_FishBoneDust Configure(int lifetime, float settleSpeed = 0.014f) {
            Lifetime = lifetime;
            settle = settleSpeed;
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            settle = 0.014f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            ai[0] = Main.rand.Next(4);
            spin = Main.rand.NextFloat(0.006f, 0.016f) * (Main.rand.NextBool() ? 1f : -1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(28, 42);
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            Scale *= 1.008f;
            Rotation += spin;
            Velocity *= 0.90f;
            Velocity.Y += settle;

            //快进慢出、峰值压低
            float tail = MathHelper.Clamp((t - 0.30f) / 0.65f, 0f, 1f);
            Opacity = MathF.Min(t / 0.12f, 1f) * (1f - tail * tail * (3f - 2f * tail)) * 0.42f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int index = (int)ai[0];
            int frameSize = tex.Width / 2;
            Rectangle frame = new(index % 2 * frameSize, index / 2 * frameSize, frameSize, frameSize);
            Color env = Lighting.GetColor(Position.ToTileCoordinates());
            Color lit = Color.Lerp(Color.MultiplyRGB(env), Color, 0.30f);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, lit * Opacity, Rotation
                , frame.Size() * 0.5f, Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
