using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections
{
    /// <summary>
    /// 深渊复苏系统辅助类，提供便捷的静态访问方法和扩展方法
    /// </summary>
    public static class ResurrectionHelper
    {
        /// <summary>
        /// 获取指定玩家的复苏系统
        /// </summary>
        public static ResurrectionSystem GetResurrectionSystem(this Player player) {
            if (player?.TryGetOverride<HalibutPlayer>(out var halibutPlayer) == true) {
                return halibutPlayer.ResurrectionSystem;
            }
            return null;
        }

        /// <summary>
        /// 获取本地玩家的复苏系统
        /// </summary>
        public static ResurrectionSystem GetLocalSystem() {
            return Main.LocalPlayer.GetResurrectionSystem();
        }

        /// <summary>
        /// 为指定玩家增加复苏值
        /// </summary>
        public static void AddResurrectionValue(this Player player, float amount) {
            player.GetResurrectionSystem()?.AddValue(amount);
        }

        /// <summary>
        /// 为本地玩家增加复苏值
        /// </summary>
        public static void AddResurrectionValue(float amount) {
            Main.LocalPlayer.AddResurrectionValue(amount);
        }

        /// <summary>
        /// 为指定玩家减少复苏值
        /// </summary>
        public static void SubtractResurrectionValue(this Player player, float amount) {
            player.GetResurrectionSystem()?.SubtractValue(amount);
        }

        /// <summary>
        /// 为本地玩家减少复苏值
        /// </summary>
        public static void SubtractResurrectionValue(float amount) {
            Main.LocalPlayer.SubtractResurrectionValue(amount);
        }

        /// <summary>
        /// 设置指定玩家的复苏值
        /// </summary>
        public static void SetResurrectionValue(this Player player, float value) {
            player.GetResurrectionSystem()?.SetValue(value);
        }

        /// <summary>
        /// 设置本地玩家的复苏值
        /// </summary>
        public static void SetResurrectionValue(float value) {
            Main.LocalPlayer.SetResurrectionValue(value);
        }

        /// <summary>
        /// 设置指定玩家的复苏速度
        /// </summary>
        public static void SetResurrectionRate(this Player player, float rate) {
            var system = player.GetResurrectionSystem();
            if (system != null) {
                system.ResurrectionRate = rate;
            }
        }

        /// <summary>
        /// 设置本地玩家的复苏速度
        /// </summary>
        public static void SetResurrectionRate(float rate) {
            Main.LocalPlayer.SetResurrectionRate(rate);
        }

        /// <summary>
        /// 获取指定玩家的复苏进度比例（0-1）
        /// </summary>
        public static float GetResurrectionRatio(this Player player) {
            return player.GetResurrectionSystem()?.Ratio ?? 0f;
        }

        /// <summary>
        /// 获取本地玩家的复苏进度比例（0-1）
        /// </summary>
        public static float GetResurrectionRatio() {
            return Main.LocalPlayer.GetResurrectionRatio();
        }

        /// <summary>
        /// 检查指定玩家的复苏值是否达到指定阈值
        /// </summary>
        public static bool HasReachedThreshold(this Player player, float threshold) {
            return player.GetResurrectionSystem()?.HasReachedThreshold(threshold) ?? false;
        }

        /// <summary>
        /// 检查本地玩家的复苏值是否达到指定阈值
        /// </summary>
        public static bool HasReachedThreshold(float threshold) {
            return Main.LocalPlayer.HasReachedThreshold(threshold);
        }

        /// <summary>
        /// 重置指定玩家的复苏值
        /// </summary>
        public static void ResetResurrection(this Player player) {
            player.GetResurrectionSystem()?.Reset();
        }

        /// <summary>
        /// 重置本地玩家的复苏值
        /// </summary>
        public static void ResetResurrection() {
            Main.LocalPlayer.ResetResurrection();
        }

        /// <summary>
        /// 填满指定玩家的复苏值
        /// </summary>
        public static void FillResurrection(this Player player) {
            player.GetResurrectionSystem()?.Fill();
        }

        /// <summary>
        /// 填满本地玩家的复苏值
        /// </summary>
        public static void FillResurrection() {
            Main.LocalPlayer.FillResurrection();
        }

        /// <summary>
        /// 启用/禁用指定玩家的复苏系统
        /// </summary>
        public static void SetResurrectionEnabled(this Player player, bool enabled) {
            var system = player.GetResurrectionSystem();
            if (system != null) {
                system.IsEnabled = enabled;
            }
        }

        /// <summary>
        /// 启用/禁用本地玩家的复苏系统
        /// </summary>
        public static void SetResurrectionEnabled(bool enabled) {
            Main.LocalPlayer.SetResurrectionEnabled(enabled);
        }
    }
}
