using Microsoft.Xna.Framework;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 气力 HUD 的只读数据快照。玩法层只需提供当前值与上限，
    /// 消耗残影、恢复流速等演出状态全部由 HUD 根据相邻帧差值推导
    /// </summary>
    internal readonly struct OniVigorSnapshot
    {
        public readonly float Value;
        public readonly float MaxValue;

        public OniVigorSnapshot(float value, float maxValue) {
            Value = value;
            MaxValue = maxValue;
        }

        public float Ratio => MaxValue > 0f
            ? MathHelper.Clamp(Value / MaxValue, 0f, 1f)
            : 0f;
    }

    /// <summary>
    /// 气力数据源。实现应根据传入玩家读取实例数据，不应把玩家数值存进静态字段
    /// </summary>
    internal interface IOniVigorSource
    {
        bool TryGetVigor(Player player, out OniVigorSnapshot snapshot);
    }

    /// <summary>
    /// 气力 HUD 与未来玩法层之间的接缝。当前未挂接数据源时返回预览值，
    /// 玩法完成后调用 <see cref="SetSource"/> 即可替换，不需要改动 HUD 或 shader
    /// </summary>
    internal static class OniVigorData
    {
        private const float PreviewValue = 72f;
        private const float PreviewMaxValue = 100f;

        private static IOniVigorSource source;

        public static void SetSource(IOniVigorSource value) => source = value;

        public static bool TryGet(Player player, out OniVigorSnapshot snapshot) {
            if (player == null || !player.active) {
                snapshot = default;
                return false;
            }

            if (source == null) {
                snapshot = new OniVigorSnapshot(PreviewValue, PreviewMaxValue);
                return true;
            }

            if (!source.TryGetVigor(player, out snapshot)
                || !float.IsFinite(snapshot.Value)
                || !float.IsFinite(snapshot.MaxValue)
                || snapshot.MaxValue <= 0f) {
                snapshot = default;
                return false;
            }

            return true;
        }
    }
}
