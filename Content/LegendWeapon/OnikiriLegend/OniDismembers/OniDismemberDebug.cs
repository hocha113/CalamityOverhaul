using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers
{
    /// <summary>肢解管线临时诊断日志、排查部分机器看不到切开效果用，定位后整体移除。
    /// 输出统一带 [OniDismemberDebug] 前缀，按 key 节流防刷屏</summary>
    internal static class OniDismemberDebug
    {
        private static readonly Dictionary<string, uint> lastLogTick = [];

        /// <summary>按 key 节流输出，同 key 默认每 60 帧最多一条</summary>
        internal static void Log(string key, string message, int throttleTicks = 60) {
            if (lastLogTick.TryGetValue(key, out uint last)
                && Main.GameUpdateCount - last < (uint)throttleTicks) {
                return;
            }
            lastLogTick[key] = Main.GameUpdateCount;
            CWRMod.Instance.Logger.Info($"[OniDismemberDebug] {message}");
        }

        /// <summary>渲染环境快照、降级判定涉及的全部现场值</summary>
        internal static string Env() {
            string device;
            try {
                GraphicsDevice gd = Main.instance?.GraphicsDevice;
                device = gd == null ? "null" : $"{gd.Adapter?.Description}/{gd.GraphicsProfile}";
            }
            catch {
                device = "unavailable";
            }
            return $"Lighting={Lighting.Mode} WaveQuality={Main.WaveQuality}"
                + $" DomainConciseDisplay={CWRServerConfig.Instance?.DomainConciseDisplay}"
                + $" NeedsFallback={RenderQualitySafety.NeedsScreenTargetFallback()}"
                + $" drawToScreen={Main.drawToScreen}"
                + $" screenTarget={Describe(Main.screenTarget)} screenSwap={Describe(RenderHandleLoader.ScreenSwap)}"
                + $" netMode={Main.netMode} screen={Main.screenWidth}x{Main.screenHeight}"
                + $" zoom={Main.GameZoomTarget} uiScale={Main.UIScale} gpu={device}";
        }

        /// <summary>当前绑定的渲染目标描述，定位第三方渲染 mod 抢占 RT 用</summary>
        internal static string CurrentRT(GraphicsDevice gd) {
            if (gd == null) {
                return "noDevice";
            }
            RenderTargetBinding[] bindings = gd.GetRenderTargets();
            if (bindings == null || bindings.Length == 0) {
                return "backbuffer";
            }
            if (bindings[0].RenderTarget is RenderTarget2D rt) {
                return rt == Main.screenTarget ? "screenTarget" : $"foreignRT({rt.Width}x{rt.Height})";
            }
            return bindings[0].RenderTarget?.GetType().Name ?? "null";
        }

        private static string Describe(RenderTarget2D rt)
            => rt == null ? "null" : rt.IsDisposed ? "disposed" : $"{rt.Width}x{rt.Height}";
    }
}
