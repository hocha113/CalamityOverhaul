using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;
using Terraria;
using Terraria.Graphics.Light;

namespace CalamityOverhaul.Common
{
    internal static class RenderQualitySafety
    {
        /// <summary>
        /// <see cref="Main.screenTarget"/> 是否为当前活动 RT
        /// <br/>钩子未绑 screenTarget 时强写会顶替 backbuffer 致全屏/UI 消失，RT 管线特效动手前先调本方法
        /// </summary>
        public static bool IsScreenTargetActive(GraphicsDevice graphicsDevice) {
            if (graphicsDevice == null) return false;
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) return false;

            RenderTargetBinding[] bindings = graphicsDevice.GetRenderTargets();
            if (bindings == null || bindings.Length == 0) return false;
            return bindings[0].RenderTarget == Main.screenTarget;
        }

        //tModLoader/Terraria 版本间该设置名可能不同，反射读取可避免绑定具体字段名。
        private static readonly string[] WaterQualityMemberNames = [
            "WaveQuality", "waveQuality", "WaterQuality", "waterQuality",
            "LiquidQuality", "liquidQuality"
        ];

        public static bool NeedsScreenTargetFallback() {
            if (Lighting.Mode == LightMode.Retro || Lighting.Mode == LightMode.Trippy || CWRServerConfig.Instance.DomainConciseDisplay) {
                return true;
            }

            return IsLowWaterQuality();
        }

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
            if (value == null) return false;

            string text = value.ToString();
            if (text.Equals("Off", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Low", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Disabled", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (value is bool enabled) return !enabled;

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
}
