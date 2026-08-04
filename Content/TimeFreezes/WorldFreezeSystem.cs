using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;

namespace CalamityOverhaul.Content.TimeFreezes
{
    /// <summary>完整世界冻结的 reason 所有权与玩家/世界钩子</summary>
    internal class WorldFreezeSystem : ICWRLoader
    {
        //Liquid.UpdateLiquid 拦截委托
        private delegate void Hook_UpdateLiquid(Action orig);
        //Player.UpdateEquips 拦截委托
        private delegate void Hook_UpdateEquips(Action<Player, int> orig, Player self, int i);
        //Player.ScrollHotbar 拦截委托
        private delegate void Hook_ScrollHotbar(Action<Player, int> orig, Player self, int offset);
        //Player.TrySwitchingLoadout 拦截委托
        private delegate void Hook_TrySwitchingLoadout(Action<Player, int> orig, Player self, int loadoutIndex);
        //Player.QuickBuff/QuickHeal/QuickMana/QuickMount 拦截委托
        private delegate void Hook_QuickAction(Action<Player> orig, Player self);

        //TimeGear 注册名，仅作内部时间速率叠加用
        private const string TimeGearKey = "WorldFreezeSystem";

        void ICWRLoader.UnLoadData() => ResetSession();

        public static bool IsActive { get; private set; }
        internal static bool IsThawing { get; private set; }

        public static IReadOnlyCollection<string> ActiveReasons => activeReasons;

        //同 reason 重复 Activate 幂等
        private static readonly HashSet<string> activeReasons = [];

        void ICWRLoader.SetupData() {
            //拦截液体更新，使水流在冻结期间不再传播
            MethodInfo liquidMethod = typeof(Liquid).GetMethod("UpdateLiquid"
                , BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (liquidMethod != null) {
                VaultHook.Add(liquidMethod, (Hook_UpdateLiquid)OnUpdateLiquidHook);
            }

            //拦截玩家装备更新，阻止饰品在冻结期间继续运行（生成弹幕、扣除冷却等）
            MethodInfo equipMethod = typeof(Player).GetMethod("UpdateEquips"
                , BindingFlags.Public | BindingFlags.Instance, null, [typeof(int)], null);
            if (equipMethod != null) {
                VaultHook.Add(equipMethod, (Hook_UpdateEquips)OnUpdateEquipsHook);
            }

            //拦截手持栏切换（滚轮/手柄加减/点击快捷栏 changeItem 全走这里），冻结期间禁止换手持
            MethodInfo scrollMethod = typeof(Player).GetMethod("ScrollHotbar"
                , BindingFlags.Public | BindingFlags.Instance, null, [typeof(int)], null);
            if (scrollMethod != null) {
                VaultHook.Add(scrollMethod, (Hook_ScrollHotbar)OnScrollHotbarHook);
            }

            //拦截装备配置切换，冻结期间禁止换装
            MethodInfo loadoutMethod = typeof(Player).GetMethod("TrySwitchingLoadout"
                , BindingFlags.Public | BindingFlags.Instance, null, [typeof(int)], null);
            if (loadoutMethod != null) {
                VaultHook.Add(loadoutMethod, (Hook_TrySwitchingLoadout)OnTrySwitchingLoadoutHook);
            }

            //拦截快捷治疗/魔力/增益/坐骑，这些从输入层直接调用，不经过 ItemCheck
            string[] quickActionNames = ["QuickBuff", "QuickHeal", "QuickMana", "QuickMount"];
            foreach (string name in quickActionNames) {
                MethodInfo quickMethod = typeof(Player).GetMethod(name
                    , BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (quickMethod != null) {
                    VaultHook.Add(quickMethod, (Hook_QuickAction)OnQuickActionHook);
                }
            }
        }

        private static void OnUpdateLiquidHook(Action orig) {
            if (IsActive) return;
            orig();
        }

        private static void OnUpdateEquipsHook(Action<Player, int> orig, Player self, int i) {
            if (IsActive) return;
            orig(self, i);
        }

        private static void OnScrollHotbarHook(Action<Player, int> orig, Player self, int offset) {
            if (IsActive) {
                //吞掉本帧滚轮偏移与点击暂存，防止解冻瞬间补切
                self.HotbarOffset = 0;
                self.changeItem = -1;
                return;
            }
            orig(self, offset);
        }

        private static void OnTrySwitchingLoadoutHook(Action<Player, int> orig, Player self, int loadoutIndex) {
            if (IsActive) return;
            orig(self, loadoutIndex);
        }

        private static void OnQuickActionHook(Action<Player> orig, Player self) {
            if (IsActive) return;
            orig(self);
        }

        /// <summary>reason 冻结，幂等</summary>
        public static void Activate(string reason) {
            if (string.IsNullOrEmpty(reason)) {
                return;
            }
            bool wasInactive = !IsActive;
            activeReasons.Add(reason);
            if (wasInactive) {
                IsActive = true;
                TimeGear.Register(TimeGearKey, 0f);
                TimeFreezeSystem.BeginWorldFreeze();
            }
        }

        /// <summary>释放 reason，空才解冻</summary>
        public static void Deactivate(string reason) {
            if (string.IsNullOrEmpty(reason)) {
                return;
            }
            if (!activeReasons.Remove(reason)) {
                return;
            }
            if (activeReasons.Count == 0 && IsActive) {
                FinalizeDeactivate();
            }
        }

        /// <summary>清空 reason，死亡/卸载兜底</summary>
        public static void DeactivateAll() {
            if (activeReasons.Count == 0 && !IsActive) {
                return;
            }
            activeReasons.Clear();
            if (IsActive) {
                FinalizeDeactivate();
            }
        }

        public static bool HasReason(string reason)
            => !string.IsNullOrEmpty(reason) && activeReasons.Contains(reason);

        private static void FinalizeDeactivate() {
            IsActive = false;
            IsThawing = true;
            TimeGear.Unregister(TimeGearKey);
            try {
                TimeFreezeSystem.EndWorldFreeze();
            }
            finally {
                IsThawing = false;
                TimeGear.Unregister(TimeGearKey);
            }
        }

        internal static void ResetSession() {
            activeReasons.Clear();
            IsActive = false;
            IsThawing = false;
            TimeGear.Unregister(TimeGearKey);
            TimeFreezeSystem.ResetSession();
        }

        /// <summary>NPC 冻结，现一律 true</summary>
        internal static bool ShouldFreezeNPC(NPC npc) {
            if (!npc.active) return false;
            return true;
        }

        /// <summary>弹幕冻结，现一律 true</summary>
        internal static bool ShouldFreezeProjectile(Projectile proj) {
            if (!proj.active) return false;
            return true;
        }
    }
}
