using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Ambience
{
    //滤镜着色数据:Apply 在 FilterManager.EndCapture 时刻被调,
    //此刻上载相机仿射/分级色/噪声绑定(帧内零滞后),再走原版参数链
    internal sealed class HadalWaterShaderData : ScreenShaderData
    {
        public HadalWaterShaderData(Asset<Effect> shader, string passName) : base(shader, passName) { }

        public override void Apply() {
            HadalAmbience.ApplyFilterUniforms(Shader);
            base.Apply();
        }
    }

    /// <summary>
    /// 深渊海沟氛围调度(C 路):深度光照衰减(日光带天光→暮光带残光→午夜带以下
    /// 近全黑只剩光源半径,光色随深度冷移)、黑暗呼吸包络、水体合成滤镜
    /// (浑浊纱+丁达尔光束+超深渊暗角,单 pass 收敛)的激活与参数上载。<br/>
    /// 纯客户端表现,零同步包,服务端早退;非激活 presence=0 即早退零开销。<br/>
    /// 深度一律走 <see cref="HadalworldMetrics.DepthFraction"/>(brief §2 协议);
    /// 压暗全部来自光照系统与滤镜拷屏改写,不涉加色批暗层(brief §5 第一定律)
    /// </summary>
    internal class HadalAmbience : ModSystem, ICWRLoader
    {
        internal const string FilterName = "CWRMod:HadalWater";

        //==== Debug 静态口(TestItem 验收用) ====
        /// <summary>主世界强制开启(验收用,连带天空与粒子)</summary>
        internal static bool ForceEnable;
        /// <summary>伪深度(行):≥0 时替代玩家真实行,轮带验收用;-1=关闭</summary>
        internal static float FakeRow = -1f;

        private static float presence;
        /// <summary>全局淡入淡出 0~1(进出子世界包络,姊妹系统共用)</summary>
        internal static float Presence => presence;

        //黑暗的呼吸:0~1 相位调制慢正弦(约 11s 主周期+44s 相位漂移防读出循环)
        private static float breath;
        internal static float Breath => breath;

        //本 tick 分级采样缓存(光钩子直接消费,避免每钩重采样)
        private static HadalGradeKey grade;
        private static float gradeFrac;

        internal static bool Want => (Hadalworld.Active || ForceEnable) && !Main.gameMenu;

        internal static float CurrentRow() {
            if (FakeRow >= 0f) {
                return FakeRow;
            }
            Player player = Main.LocalPlayer;
            return player != null && player.active ? player.Center.Y / 16f : 0f;
        }

        void ICWRLoader.LoadAsset() {
            if (EffectLoader.HadalWater == null) {
                return;
            }
            //第二参数是"pass 名"不是 technique 名:传错会 NRE 并把 FilterManager
            //的批撂在半开状态连锁崩溃(ScrapSiege 2026-08 事故)
            Filters.Scene[FilterName] = new Filter(
                new HadalWaterShaderData(EffectLoader.HadalWater, "HadalFilter"), EffectPriority.High);
        }

        void ICWRLoader.UnLoadData() {
            presence = 0f;
            breath = 0f;
            ForceEnable = false;
            FakeRow = -1f;
        }

        public override void OnWorldLoad() => HardReset();
        public override void OnWorldUnload() => HardReset();

        private static void HardReset() {
            presence = 0f;
            breath = 0f;
            grade = default;
            gradeFrac = 0f;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            bool want = Want;
            presence = MathHelper.Lerp(presence, want ? 1f : 0f, want ? 0.05f : 0.10f);
            if (!want && presence < 0.004f) {
                presence = 0f;
                SetFilterActive(false);
                return;
            }

            gradeFrac = HadalworldMetrics.DepthFraction(CurrentRow() * 16f);
            grade = HadalDepthProfile.Sample(gradeFrac);

            float t = Main.GlobalTimeWrappedHourly;
            breath = 0.5f + 0.5f * MathF.Sin(t * 0.556f + 1.7f * MathF.Sin(t * 0.144f));

            bool shaderReady = EffectLoader.HadalWater?.Value != null;
            SetFilterActive(shaderReady && presence > 0.02f);
        }

        private static void SetFilterActive(bool active) {
            //索引器对未注册键返回 null;Activate/Deactivate 对未注册键抛异常,必须先判空
            Filter filter = Filters.Scene[FilterName];
            if (filter == null) {
                return;
            }
            if (active && !filter.IsActive()) {
                Filters.Scene.Activate(FilterName);
            }
            else if (!active && filter.IsActive()) {
                Filters.Scene.Deactivate(FilterName);
            }
        }

        //==================== 光照钩子(乘法可组合,与他系统天然共存) ====================

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (Main.dedServ || presence <= 0.001f) {
                return;
            }
            //光色冷移:白→水青→暮蓝→近黑,背景比物块暗得更快
            tileColor = Color.Lerp(tileColor, grade.SunTile,
                MathHelper.Clamp(grade.SunTileF * presence, 0f, 0.98f));
            backgroundColor = Color.Lerp(backgroundColor, grade.SunBg,
                MathHelper.Clamp(grade.SunBgF * presence, 0f, 1f));
        }

        public override void ModifyLightingBrightness(ref float scale) {
            if (Main.dedServ || presence <= 0.001f) {
                return;
            }
            //scale 是逐格衰减率(VFX.md 裁定):深处光源半径收缩即"水变稠",
            //数值克制(0.93 底);呼吸包络只向下微压,无白闪
            float bright = grade.Bright * (1f - HadalDepthProfile.BreathAmp(gradeFrac) * breath);
            scale *= MathHelper.Lerp(1f, bright, presence);
        }

        //==================== 滤镜 uniform 上载 ====================

        /// <summary>
        /// 合成滤镜全参数重设(uniform 是设备全局状态,每调用点全参数上载)。<br/>
        /// EndCapture 已把重力翻转预翻正,仿射只逆 ZoomMatrix(镜像 DungeonworldFogSystem)
        /// </summary>
        internal static void ApplyFilterUniforms(Effect fx) {
            if (fx == null) {
                return;
            }
            Matrix inv = Matrix.Invert(Main.GameViewMatrix.ZoomMatrix);
            Vector2 worldScale = new(inv.M11, inv.M22);
            Vector2 worldOffset = new(inv.M41 + Main.screenPosition.X, inv.M42 + Main.screenPosition.Y);

            //屏顶/屏底世界深度各采一次,shader 内按 uv.y 线性插值
            //(屏幕纵跨 ≈ 世界高度 1.5%,分带过渡数百行,线性即光滑)
            float topFrac = HadalworldMetrics.DepthFraction(worldOffset.Y);
            float bottomFrac = HadalworldMetrics.DepthFraction(
                worldOffset.Y + Main.screenHeight * worldScale.Y);
            HadalGradeKey top = HadalDepthProfile.Sample(topFrac);
            HadalGradeKey bottom = HadalDepthProfile.Sample(bottomFrac);
            //呼吸把浑浊度轻推:黑暗变稠与亮度脉动同拍
            float thick = 1f + HadalDepthProfile.BreathAmp(gradeFrac) * breath * 2.2f;

            fx.Parameters["uScreenSize"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            fx.Parameters["uWorldScale"]?.SetValue(worldScale);
            fx.Parameters["uWorldOffset"]?.SetValue(worldOffset);
            fx.Parameters["uSeaLevelPx"]?.SetValue(HadalworldMetrics.SeaLevelRow * 16f);
            fx.Parameters["uVeilTop"]?.SetValue(top.Veil.ToVector3());
            fx.Parameters["uTurbTop"]?.SetValue(MathHelper.Clamp(top.Turbidity * thick, 0f, 0.8f));
            fx.Parameters["uVeilBottom"]?.SetValue(bottom.Veil.ToVector3());
            fx.Parameters["uTurbBottom"]?.SetValue(MathHelper.Clamp(bottom.Turbidity * thick, 0f, 0.8f));
            //光束是加进画面的光,色值即强度(拷屏改写无混合语义,不涉 A=0 陷阱)
            fx.Parameters["uRayColor"]?.SetValue(new Vector3(0.30f, 0.44f, 0.44f));
            fx.Parameters["uRayStrength"]?.SetValue(grade.Rays);
            fx.Parameters["uRayFadeInv"]?.SetValue(1f / HadalDepthProfile.RaySpanPx);
            fx.Parameters["uVignette"]?.SetValue(grade.Vignette);
            fx.Parameters["uPresence"]?.SetValue(presence);
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);

            //s1 噪声按寄存器绑定(硬规);EndCapture 帧末统一清纹理槽,无需手动解绑
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null && !noise.IsDisposed) {
                gd.Textures[1] = noise;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
            }
        }

        /// <summary>一行状态摘要(TestItem 验收用)</summary>
        internal static string StatusLine() {
            return $"[海沟氛围] presence{presence:F2} 行{CurrentRow():F0} 深度{gradeFrac:F3}"
                + $" 带{HadalworldMetrics.GetZone((int)CurrentRow())} 亮度{grade.Bright:F3}"
                + $" 浑浊{grade.Turbidity:F2} 光束{grade.Rays:F2} 呼吸{breath:F2}";
        }
    }
}
