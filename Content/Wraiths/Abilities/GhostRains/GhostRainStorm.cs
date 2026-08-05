using CalamityOverhaul.Content.Wraiths.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Abilities.GhostRains
{
    /// <summary>
    /// 鬼雨常驻雨幕的时刻表与包络：阴叠→落雨爬升→稳态；收刀走散场帧。<br/>
    /// 权威流程在 <see cref="Projectiles.GhostRainProj"/>，本类只提供共享常量与曲线。
    /// </summary>
    internal static class GhostRainStorm
    {
        //阴叠(1..54] 落雨爬升(54..144] 之后稳态；散场仅在通道断开时触发
        public const int GloomEnd = 54;
        public const int RainfallEnd = 144;
        /// <summary>入雨结算帧，阴叠尽头</summary>
        public const int CommitFrame = GloomEnd;
        /// <summary>收刀/失格后的散场长度</summary>
        public const int FadeFrames = 48;
        /// <summary>域水平半径（世界像素），竖直覆盖天到地；需盖住常见全屏宽度</summary>
        public const float Radius = 1400f;
        //雨蚀与拽入节拍（帧）
        public const int ErodeInterval = 30;
        public const int YankInterval = 50;

        /// <summary>阴幕在场强度包络 0~1；稳态封顶为 1，散场由控制器另算。</summary>
        internal static float Envelope(int age) {
            if (age <= 0) {
                return 0f;
            }
            if (age <= GloomEnd) {
                return 0.6f * age / GloomEnd;
            }
            if (age <= RainfallEnd) {
                return MathHelper.Lerp(0.6f, 0.85f,
                    (age - GloomEnd) / (float)(RainfallEnd - GloomEnd));
            }
            return MathHelper.Lerp(0.85f, 1f, Math.Min(1f, (age - RainfallEnd) / 30f));
        }

        /// <summary>雨密度 0~1；未入雨只漏前兆丝，入雨后爬升并稳态满幕。</summary>
        internal static float RainDensity(int age, bool paid) {
            if (age <= 0) {
                return 0f;
            }
            if (!paid) {
                if (age <= GloomEnd) {
                    float pre = (age - (GloomEnd - 16)) / 16f;
                    return MathHelper.Clamp(pre, 0f, 1f) * 0.12f;
                }
                //入雨失败走散场前的残余前兆
                return 0.08f;
            }
            if (age <= GloomEnd) {
                float pre = (age - (GloomEnd - 16)) / 16f;
                return MathHelper.Clamp(pre, 0f, 1f) * 0.12f;
            }
            if (age <= RainfallEnd) {
                return MathHelper.Lerp(0.12f, 1f,
                    (age - GloomEnd) / (float)(RainfallEnd - GloomEnd));
            }
            return 1f;
        }

        /// <summary>入雨确认时的雨批文字，仅雨幕主的客户端显示。</summary>
        internal static void ShowRainText(Player owner) {
            if (!VaultUtils.isServer && owner?.whoAmI == Main.myPlayer) {
                CombatText.NewText(owner.Hitbox, new Color(150, 170, 175),
                    WraithSystemText.GhostRainRiteText.Value, true);
            }
        }
    }
}
