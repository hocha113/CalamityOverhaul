using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections
{
    /// <summary>
    /// 复苏系统静态扩展入口
    /// </summary>
    public static class ResurrectionHelper
    {
        /// <summary>指定玩家 <see cref="ResurrectionSystem"/></summary>
        public static ResurrectionSystem GetResurrectionSystem(this Player player) {
            if (player?.TryGetOverride<HalibutPlayer>(out var halibutPlayer) == true) {
                return halibutPlayer.ResurrectionSystem;
            }
            return null;
        }

        /// <summary>本地玩家 <see cref="ResurrectionSystem"/></summary>
        public static ResurrectionSystem GetLocalSystem() {
            return Main.LocalPlayer.GetResurrectionSystem();
        }

        /// <summary>指定玩家增加复苏值</summary>
        public static void AddResurrectionValue(this Player player, float amount) {
            player.GetResurrectionSystem()?.AddValue(amount);
        }

        /// <summary>本地玩家增加复苏值</summary>
        public static void AddResurrectionValue(float amount) {
            Main.LocalPlayer.AddResurrectionValue(amount);
        }

        /// <summary>指定玩家减少复苏值</summary>
        public static void SubtractResurrectionValue(this Player player, float amount) {
            player.GetResurrectionSystem()?.SubtractValue(amount);
        }

        /// <summary>本地玩家减少复苏值</summary>
        public static void SubtractResurrectionValue(float amount) {
            Main.LocalPlayer.SubtractResurrectionValue(amount);
        }

        /// <summary>指定玩家设复苏值</summary>
        public static void SetResurrectionValue(this Player player, float value) {
            player.GetResurrectionSystem()?.SetValue(value);
        }

        /// <summary>本地玩家设复苏值</summary>
        public static void SetResurrectionValue(float value) {
            Main.LocalPlayer.SetResurrectionValue(value);
        }

        /// <summary>指定玩家设复苏速度</summary>
        public static void SetResurrectionRate(this Player player, float rate) {
            var system = player.GetResurrectionSystem();
            if (system != null) {
                system.ResurrectionRate = rate;
            }
        }

        /// <summary>本地玩家设复苏速度</summary>
        public static void SetResurrectionRate(float rate) {
            Main.LocalPlayer.SetResurrectionRate(rate);
        }

        /// <summary>指定玩家复苏比例 0-1</summary>
        public static float GetResurrectionRatio(this Player player) {
            return player.GetResurrectionSystem()?.Ratio ?? 0f;
        }

        /// <summary>本地玩家复苏比例 0-1</summary>
        public static float GetResurrectionRatio() {
            return Main.LocalPlayer.GetResurrectionRatio();
        }

        /// <summary>指定玩家是否达复苏阈值</summary>
        public static bool HasReachedThreshold(this Player player, float threshold) {
            return player.GetResurrectionSystem()?.HasReachedThreshold(threshold) ?? false;
        }

        /// <summary>本地玩家是否达复苏阈值</summary>
        public static bool HasReachedThreshold(float threshold) {
            return Main.LocalPlayer.HasReachedThreshold(threshold);
        }

        /// <summary>指定玩家重置复苏值</summary>
        public static void ResetResurrection(this Player player) {
            player.GetResurrectionSystem()?.Reset();
        }

        /// <summary>本地玩家重置复苏值</summary>
        public static void ResetResurrection() {
            Main.LocalPlayer.ResetResurrection();
        }

        /// <summary>指定玩家填满复苏值</summary>
        public static void FillResurrection(this Player player) {
            player.GetResurrectionSystem()?.Fill();
        }

        /// <summary>本地玩家填满复苏值</summary>
        public static void FillResurrection() {
            Main.LocalPlayer.FillResurrection();
        }

        /// <summary>指定玩家开关复苏系统</summary>
        public static void SetResurrectionEnabled(this Player player, bool enabled) {
            var system = player.GetResurrectionSystem();
            if (system != null) {
                system.IsEnabled = enabled;
            }
        }

        /// <summary>本地玩家开关复苏系统</summary>
        public static void SetResurrectionEnabled(bool enabled) {
            Main.LocalPlayer.SetResurrectionEnabled(enabled);
        }
    }
}
