using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;
using Terraria;
using Terraria.Graphics.Light;

namespace CalamityOverhaul.Common
{
    /// <summary>screenTarget 管线的技术门禁（光照/水质/RT 绑定）。不含任何玩法配置偏好</summary>
    internal static class RenderQualitySafety
    {
        /// <summary><see cref="Main.screenTarget"/> 是否为当前活动 RT</summary>
        public static bool IsScreenTargetActive(GraphicsDevice graphicsDevice) {
            if (graphicsDevice == null) {
                return false;
            }
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                return false;
            }

            RenderTargetBinding[] bindings = graphicsDevice.GetRenderTargets();
            if (bindings == null || bindings.Length == 0) {
                return false;
            }
            return bindings[0].RenderTarget == Main.screenTarget;
        }

        /// <summary>screenTarget 管线技术性不可用：复古/迷幻光照下原版直绘屏幕并释放 RT；低/关水波质量同判。
        /// 一切依赖 screenTarget 的捕获/后处理用这个，勿与 <see cref="DomainVisuals"/> 混用</summary>
        public static bool ScreenTargetUnavailable() {
            if (Lighting.Mode == LightMode.Retro || Lighting.Mode == LightMode.Trippy) {
                return true;
            }

            return IsLowWaterQuality();
        }

        //水质设置名跨版本漂移，反射试读

        private static readonly string[] WaterQualityMemberNames = [
            "WaveQuality", "waveQuality", "WaterQuality", "waterQuality",
            "LiquidQuality", "liquidQuality"
        ];

        private static bool IsLowWaterQuality() {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            Type mainType = typeof(Main);

            foreach (string memberName in WaterQualityMemberNames) {
                FieldInfo field = mainType.GetField(memberName, flags);
                if (field != null && IsLowQualityValue(field.GetValue(null))) {
                    return true;
                }

                PropertyInfo property = mainType.GetProperty(memberName, flags);
                if (property != null && IsLowQualityValue(property.GetValue(null))) {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLowQualityValue(object value) {
            if (value == null) {
                return false;
            }

            string text = value.ToString();
            if (text.Equals("Off", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Low", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Disabled", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (value is bool enabled) {
                return !enabled;
            }

            if (value is IConvertible convertible) {
                try {
                    return convertible.ToDouble(null) <= 1d;
                } catch {
                    return false;
                }
            }

            return false;
        }
    }

    /// <summary>领域演出的玩家偏好，与 <see cref="RenderQualitySafety"/> 正交。
    /// 仅赛博空间 / 海域等路径使用；鬼域故意不吃此偏好</summary>
    internal static class DomainVisuals
    {
        /// <summary>开启后领域走简约回退（少后处理、少装饰层）</summary>
        public static bool Concise => CWRClientConfig.Instance.DomainConciseDisplay;
    }
}
