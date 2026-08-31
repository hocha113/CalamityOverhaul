using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips
{
    /// <summary>
    /// 鞭刑节拍的拍点微光：on-beat 窗口开启瞬间在鞭柄闪一记菱星，
    /// 是归属者的个人节奏读数（单粒、短命、加色）
    /// </summary>
    internal class PRT_GsWhipBeatSpark : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarTexture_White";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 200;

        private Color baseColor;
        private bool colorCaptured;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Lifetime = 16;
        }

        public override void AI() {
            //首帧才捕获入场色：规避 SetProperty 与外部赋色的时序依赖
            if (!colorCaptured) {
                colorCaptured = true;
                baseColor = Color;
            }
            //先撑开后收拢的一次心跳
            float t = LifetimeCompletion;
            Scale = (t < 0.3f ? MathHelper.Lerp(0.4f, 1f, t / 0.3f)
                : MathHelper.Lerp(1f, 0.15f, (t - 0.3f) / 0.7f)) * 0.16f;
            Color = baseColor * (1f - t * t);
            Velocity *= 0.9f;
            Rotation += 0.05f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = TexValue;
            Vector2 pos = Position - Main.screenPosition;
            //本 PRT 走 AdditiveBlend 批（源因子 = SourceAlpha）：A=0 会让整层源项归零、物理隐形。
            //强度全部由 RGB 承载（AI 已按寿命衰减 RGB），A 写满 255 只当因子 1 用，镜像 ShockRingDraw 回退层的防御
            Color outer = (Color * 0.55f) with { A = 255 };
            Color core = Color with { A = 255 };
            float wave = 1f + 0.15f * MathF.Sin(LifetimeCompletion * MathHelper.Pi);
            spriteBatch.Draw(tex, pos, null, outer, Rotation + MathHelper.PiOver4,
                tex.Size() * 0.5f, Scale * wave * 1.6f, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, core, Rotation,
                tex.Size() * 0.5f, Scale * wave, SpriteEffects.None, 0f);
            return false;
        }

        public override void Reset() {
            base.Reset();
            baseColor = default;
            colorCaptured = false;
        }
    }
}
