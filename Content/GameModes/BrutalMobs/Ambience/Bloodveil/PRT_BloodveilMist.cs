using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Bloodveil
{
    /// <summary>
    /// 血月低空红雾团：Fog 真 alpha 雾羽贴地漂带，随风缓移、微幅起伏、
    /// 缓慢膨胀后散尽。幅度镜像 Woodsong 暮雾的克制级，只作衬底不抢戏
    /// </summary>
    internal class PRT_BloodveilMist : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Fog";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 96;

        /// <summary>峰值透明度（×初始色系数，克制在氛围级）</summary>
        private const float PeakAlpha = 0.30f;

        private Color initialColor;
        private float spinRate;
        private float bobPhase;

        public PRT_BloodveilMist Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            //生成帧不跑 AI：首帧直绘的 Color 预乘首帧包络（t=0 时为 0），防单帧闪现
            Color = initialColor * 0f;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            spinRate = Main.rand.NextFloat(-0.006f, 0.006f);
            bobPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            spinRate = 0f;
            bobPhase = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 360;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //横向缓随风，纵向微幅起伏（贴地漂带不上天）
            Velocity.X = MathHelper.Lerp(Velocity.X, Main.windSpeedCurrent * 0.6f, 0.01f);
            Velocity.Y = MathF.Sin(bobPhase + Time * 0.02f) * 0.05f;
            Rotation += spinRate;
            Scale += 0.0012f;

            float t = LifetimeCompletion;
            //慢进慢出：正弦包络铺满生命周期
            float env = MathF.Sin(MathHelper.Pi * t);
            Color = initialColor * (env * PeakAlpha);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color,
                Rotation, tex.Size() * 0.5f, Scale * 0.24f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
