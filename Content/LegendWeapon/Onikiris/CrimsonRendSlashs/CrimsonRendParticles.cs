using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.CrimsonRendSlashs
{
    /// <summary>刀光燃尽烟：暗红→焦黑 AlphaBlend 染色烟团，缓慢外漂、放大、消散</summary>
    internal class PRT_CrimsonSmoke : BasePRT
    {
        public override string Texture => "CalamityOverhaul/Content/LegendWeapon/Onikiris/Textures/Smoke/SmokeSheet01";
        public override bool CanPool => true;

        private float spin;
        private Color hotColor;
        private Color coldColor;

        public PRT_CrimsonSmoke Configure(int lifetime, Color hot, Color cold, float rotSpeed = 0.012f) {
            Lifetime = lifetime;
            hotColor = hot;
            coldColor = cold;
            spin = rotSpeed * (Main.rand.NextBool() ? 1f : -1f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            hotColor = coldColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            ai[0] = Main.rand.Next(4);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(34, 50);
                hotColor = new Color(120, 24, 30);
                coldColor = new Color(30, 14, 24);
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            Scale *= 1.008f;
            Rotation += spin;
            Velocity *= 0.94f;
            Velocity.Y -= 0.012f;   //烟微微上浮

            Color = Color.Lerp(hotColor, coldColor, MathF.Min(1f, t * 1.3f));
            //快进快出的透明度包络，峰值压低避免烟层堆积吞掉刀光；
            //提前收尾让接近焦黑的末段几乎不可见，白天背景下不残留灰色剪影
            Opacity = MathF.Min(t / 0.12f, 1f) * (1f - SmoothStep01((t - 0.42f) / 0.50f)) * 0.42f;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int index = (int)ai[0];
            Rectangle frame = new(index % 2 * 512, index / 2 * 512, 512, 512);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() * 0.5f, Scale * 0.5f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>冲击火花：加色四芒星拉长条，惯性抛物 + 末段重力下坠</summary>
    internal class PRT_CrimsonSpark : BasePRT
    {
        public override string Texture => "CalamityOverhaul/Content/LegendWeapon/Onikiris/Textures/Impact/StarGlow01";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 4000;

        private Color initialColor;
        private bool gravity;

        public PRT_CrimsonSpark Configure(int lifetime, bool affectedByGravity) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = affectedByGravity;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = false;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Scale *= 0.955f;
            Velocity *= 0.94f;
            if (gravity && Velocity.Length() < 11f) {
                Velocity.X *= 0.96f;
                Velocity.Y += 0.30f;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(LifetimeCompletion, 2.4f));
            if (Scale < 0.04f) {
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //沿速度方向拉长成火花条，叠一层窄条提亮芯部
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.16f, 0.9f, 2.6f);
            Vector2 scale = new Vector2(0.42f, stretch) * Scale;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation
                , tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * 0.8f, Rotation
                , tex.Size() * 0.5f, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>命中火花序列帧：2×2 手绘火花图集单次播放，加色</summary>
    internal class PRT_CrimsonHitFlash : BasePRT
    {
        public override string Texture => "CalamityOverhaul/Content/LegendWeapon/Onikiris/Textures/Impact/HitSparkSheet01";
        public override bool CanPool => true;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 14;
            }
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            Velocity *= 0.9f;
            Opacity = 1f - MathF.Pow(LifetimeCompletion, 3f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int frameIdx = (int)MathHelper.Clamp(LifetimeCompletion * 4f, 0f, 3f);
            Rectangle frame = new(frameIdx % 2 * 128, frameIdx / 2 * 128, 128, 128);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() * 0.5f, Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
