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

        //成员名加载后不会漂移：句柄只解析一次；读值结果帧戳缓存，
        //同一帧内约 20 处后处理各问一次也只反射读一遍
        private static bool waterMembersResolved;
        private static FieldInfo[] waterFields;
        private static PropertyInfo[] waterProps;
        private static uint lowWaterFrame = uint.MaxValue;
        private static bool lowWaterCache;

        private static void ResolveWaterMembers() {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            Type mainType = typeof(Main);
            var fields = new System.Collections.Generic.List<FieldInfo>();
            var props = new System.Collections.Generic.List<PropertyInfo>();
            foreach (string memberName in WaterQualityMemberNames) {
                FieldInfo field = mainType.GetField(memberName, flags);
                if (field != null) {
                    fields.Add(field);
                }
                PropertyInfo property = mainType.GetProperty(memberName, flags);
                if (property != null) {
                    props.Add(property);
                }
            }
            waterFields = [.. fields];
            waterProps = [.. props];
            waterMembersResolved = true;
        }

        private static bool IsLowWaterQuality() {
            if (lowWaterFrame == Main.GameUpdateCount) {
                return lowWaterCache;
            }
            lowWaterFrame = Main.GameUpdateCount;

            if (!waterMembersResolved) {
                ResolveWaterMembers();
            }

            bool low = false;
            foreach (FieldInfo field in waterFields) {
                if (IsLowQualityValue(field.GetValue(null))) {
                    low = true;
                    break;
                }
            }
            if (!low) {
                foreach (PropertyInfo property in waterProps) {
                    if (IsLowQualityValue(property.GetValue(null))) {
                        low = true;
                        break;
                    }
                }
            }
            lowWaterCache = low;
            return low;
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
