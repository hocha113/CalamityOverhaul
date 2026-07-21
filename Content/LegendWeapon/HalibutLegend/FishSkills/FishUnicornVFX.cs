using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>虹光穿刺专属贴图缓存（全部复用现有 Masking 资产，不新增贴图）</summary>
    internal class FishUnicornAssets
    {
        /// <summary>细光条，带真 alpha</summary>
        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> Streak = null;
        /// <summary>起跳过曝爆点，只允许 ≤2 帧</summary>
        [VaultLoaden(CWRConstant.Masking + "Flashimpact")]
        internal static Asset<Texture2D> Flash = null;
        /// <summary>枪头底光垫层，占比克制</summary>
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> Glow = null;
    }

    /// <summary>
    /// 虹光星屑，十字闪芒小星，甩出急减速后受重力缓落，带闪烁衰减<br/>
    /// 残影湮灭时蜕落，是整段突刺里活得最久的余韵
    /// </summary>
    internal class PRT_FishUnicornStardust : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarTexture_White";
        public override bool CanPool => true;

        private float twinkleSeed;
        private float gravity;

        public PRT_FishUnicornStardust Configure(int lifetime, float gravityPerFrame = 0.05f) {
            Lifetime = lifetime;
            gravity = gravityPerFrame;
            return this;
        }

        public override void Reset() {
            base.Reset();
            twinkleSeed = 0f;
            gravity = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            twinkleSeed = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            //甩出急减速，随后缓落（终端速度封顶，读作飘而非坠）
            Velocity.X *= 0.955f;
            if (Velocity.Y < 1.6f) {
                Velocity.Y += gravity;
            }

            float lc = LifetimeCompletion;
            float twinkle = 0.66f + 0.34f * MathF.Sin(Time * 0.5f + twinkleSeed);
            Opacity = MathF.Min(lc * 6f, 1f) * (1f - MathF.Pow(lc, 2.4f)) * twinkle;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D star = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = star.Size() * 0.5f;
            Color col = Color with { A = 0 };

            //十字闪芒
            spriteBatch.Draw(star, pos, null, col * (0.85f * Opacity), 0f, origin
                , new Vector2(0.10f, 0.62f) * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, col * (0.70f * Opacity), MathHelper.PiOver2, origin
                , new Vector2(0.08f, 0.40f) * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, col * (0.95f * Opacity), 0f, origin
                , new Vector2(0.16f, 0.16f) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 角尖螺线光段，出生即定角度与长度的定向短光条，原地快速湮灭<br/>
    /// 突刺时按螺旋相位排布成独角螺纹光轨，蓄势时作向心收束光屑
    /// </summary>
    internal class PRT_FishUnicornHelix : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float lengthPixels;

        public PRT_FishUnicornHelix Configure(float rotation, float length, int lifetime) {
            Rotation = rotation;
            lengthPixels = length;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            lengthPixels = 0f;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Velocity *= 0.90f;
            float lc = LifetimeCompletion;
            Opacity = MathF.Min(lc * 5f, 1f) * MathF.Pow(1f - lc, 1.6f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float lenScale = lengthPixels / tex.Height;
            Color col = Color with { A = 0 };

            //宽淡窄亮双层，细线更实，避免糊成光棒
            spriteBatch.Draw(tex, pos, null, col * (0.5f * Opacity), Rotation + MathHelper.PiOver2, origin
                , new Vector2(0.30f, lenScale) * Scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, col * (0.9f * Opacity), Rotation + MathHelper.PiOver2, origin
                , new Vector2(0.12f, lenScale) * Scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
