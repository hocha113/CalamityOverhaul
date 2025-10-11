using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    /// <summary>
    /// 深渊复苏系统辅助类
    /// </summary>
    public static class ResurrectionHelper
    {
        /// <summary>
        /// 获取指定玩家的复苏系统
        /// </summary>
        public static ResurrectionSystem GetSystem(this Player player) {
            if (player?.TryGetOverride<HalibutPlayer>(out var halibutPlayer) == true) {
                return halibutPlayer.ResurrectionSystem;
            }
            return null;
        }

        /// <summary>
        /// 获取本地玩家的复苏系统
        /// </summary>
        public static ResurrectionSystem GetLocalSystem() {
            return GetSystem(Main.LocalPlayer);
        }

        /// <summary>
        /// 为指定玩家增加复苏值
        /// </summary>
        public static void AddResurrectionValue(this Player player, float amount) {
            GetSystem(player)?.AddValue(amount);
        }

        /// <summary>
        /// 为本地玩家增加复苏值
        /// </summary>
        public static void AddResurrectionValue(float amount) {
            AddResurrectionValue(Main.LocalPlayer, amount);
        }

        /// <summary>
        /// 为指定玩家减少复苏值
        /// </summary>
        public static void SubtractResurrectionValue(this Player player, float amount) {
            GetSystem(player)?.SubtractValue(amount);
        }

        /// <summary>
        /// 为本地玩家减少复苏值
        /// </summary>
        public static void SubtractResurrectionValue(float amount) {
            SubtractResurrectionValue(Main.LocalPlayer, amount);
        }

        /// <summary>
        /// 设置指定玩家的复苏值
        /// </summary>
        public static void SetResurrectionValue(Player player, float value) {
            GetSystem(player)?.SetValue(value);
        }

        /// <summary>
        /// 设置本地玩家的复苏值
        /// </summary>
        public static void SetResurrectionValue(float value) {
            SetResurrectionValue(Main.LocalPlayer, value);
        }

        /// <summary>
        /// 设置指定玩家的复苏速度
        /// </summary>
        public static void SetResurrectionRate(this Player player, float rate) {
            var system = GetSystem(player);
            if (system != null) {
                system.ResurrectionRate = rate;
            }
        }

        /// <summary>
        /// 设置本地玩家的复苏速度
        /// </summary>
        public static void SetResurrectionRate(float rate) {
            SetResurrectionRate(Main.LocalPlayer, rate);
        }

        /// <summary>
        /// 获取指定玩家的复苏进度比例（0-1）
        /// </summary>
        public static float GetResurrectionRatio(this Player player) {
            return GetSystem(player)?.Ratio ?? 0f;
        }

        /// <summary>
        /// 获取本地玩家的复苏进度比例（0-1）
        /// </summary>
        public static float GetResurrectionRatio() {
            return GetResurrectionRatio(Main.LocalPlayer);
        }

        /// <summary>
        /// 检查指定玩家的复苏值是否达到指定阈值
        /// </summary>
        public static bool HasReachedThreshold(this Player player, float threshold) {
            return GetSystem(player)?.HasReachedThreshold(threshold) ?? false;
        }

        /// <summary>
        /// 检查本地玩家的复苏值是否达到指定阈值
        /// </summary>
        public static bool HasReachedThreshold(this float threshold) {
            return HasReachedThreshold(Main.LocalPlayer, threshold);
        }

        /// <summary>
        /// 重置指定玩家的复苏值
        /// </summary>
        public static void ResetResurrection(this Player player) {
            GetSystem(player)?.Reset();
        }

        /// <summary>
        /// 重置本地玩家的复苏值
        /// </summary>
        public static void ResetResurrection() {
            ResetResurrection(Main.LocalPlayer);
        }

        /// <summary>
        /// 填满指定玩家的复苏值
        /// </summary>
        public static void FillResurrection(this Player player) {
            GetSystem(player)?.Fill();
        }

        /// <summary>
        /// 填满本地玩家的复苏值
        /// </summary>
        public static void FillResurrection() {
            FillResurrection(Main.LocalPlayer);
        }

        /// <summary>
        /// 启用/禁用指定玩家的复苏系统
        /// </summary>
        public static void SetEnabled(this Player player, bool enabled) {
            var system = GetSystem(player);
            if (system != null) {
                system.IsEnabled = enabled;
            }
        }

        /// <summary>
        /// 启用/禁用本地玩家的复苏系统
        /// </summary>
        public static void SetEnabled(bool enabled) {
            SetEnabled(Main.LocalPlayer, enabled);
        }
    }
}
