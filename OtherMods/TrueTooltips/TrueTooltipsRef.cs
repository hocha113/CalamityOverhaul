using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using InnoVault.GameSystem;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.TrueTooltips
{
    /// <summary>
    /// 工具提示接管类模组（True Tooltips / Tooltip Icon 系）与传奇说明面板的冲突屏蔽（反馈四 #4/#9/#15）。<br/>
    /// 传奇面板走 ModItem.PreDrawTooltip 返回 false 自绘，但 tML 的四个 tooltip 绘制派发口
    /// （PreDrawTooltip/PreDrawTooltipLine/PostDrawTooltipLine/PostDrawTooltip）仍会枚举
    /// 所有 GlobalItem——Pre 的返回值只挡原生行文字，Line/Post 族更是无条件执行，
    /// 接管类模组在这些口自绘整套 tooltip，与面板同屏叠画。<br/>
    /// 名单模组任一在场时，用 InnoVault 高优先级钩子（VaultHook + DetourConfig，
    /// 稳定压过 MonoModHooks.Add 的无配置钩）把四个派发口整体检拦：
    /// 面板物品只跑 ModItem 侧，全部 GlobalItem 跳过；其余物品原样放行
    /// </summary>
    internal class TrueTooltipsRef : ICWRLoader
    {
        /// <summary>已知会接管/改绘 tooltip 的模组内部名，任一在场即挂屏蔽钩</summary>
        private static readonly string[] ConflictModNames = ["TrueTooltips", "TooltipIcon", "TooltipIconPatch"];

        /// <summary>是否存在冲突模组且屏蔽已就位</summary>
        public static bool Has { get; private set; }

        //说明面板物品类型集，SetupData 期由四传奇 Override 的 ID 建立
        private static HashSet<int> panelItemTypes;

        //自持钩子记录：CWR 卸载先于 InnoVault，提前撤钩防悬指本程序集；
        //键与 VaultHook 缓存一致，卸载时一并从其字典摘除避免二次处置
        private static readonly List<(MethodBase Method, Delegate Hook)> hookRecords = [];

        private delegate bool On_PreDrawTooltip_Delegate(Item item, ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y);
        private delegate void On_PostDrawTooltip_Delegate(Item item, ReadOnlyCollection<DrawableTooltipLine> lines);
        private delegate bool On_PreDrawTooltipLine_Delegate(Item item, DrawableTooltipLine line, ref int yOffset);
        private delegate void On_PostDrawTooltipLine_Delegate(Item item, DrawableTooltipLine line);

        void ICWRLoader.SetupData() {
            if (Main.dedServ) {
                return;
            }

            Has = false;
            foreach (string name in ConflictModNames) {
                if (ModLoader.HasMod(name)) {
                    Has = true;
                    break;
                }
            }
            if (!Has) {
                return;
            }

            panelItemTypes = [KikasaOverride.ID, OnikiriOverride.ID, HalibutOverride.ID, SHPCOverride.ID];

            //方法组直传：检拦方法首参是 orig 委托，编译器按自然委托类型合成（对齐 WikithisRef 写法）
            TryHook("PreDrawTooltip", OnPreDrawTooltipHook);
            TryHook("PreDrawTooltipLine", OnPreDrawTooltipLineHook);
            TryHook("PostDrawTooltipLine", OnPostDrawTooltipLineHook);
            TryHook("PostDrawTooltip", OnPostDrawTooltipHook);

            CWRMod.Instance.Logger.Info($"TrueTooltipsRef: tooltip takeover shield armed ({hookRecords.Count}/4 dispatchers hooked).");
        }

        void ICWRLoader.UnLoadData() {
            foreach ((MethodBase method, Delegate hookDelegate) in hookRecords) {
                if (!VaultHook.Hooks.TryRemove((method, hookDelegate), out Hook hook) || hook == null) {
                    continue;
                }
                if (hook.IsApplied) {
                    hook.Undo();
                }
                hook.Dispose();
            }
            hookRecords.Clear();
            panelItemTypes = null;
            Has = false;
        }

        private static void TryHook(string methodName, Delegate hookDelegate) {
            MethodInfo method = typeof(ItemLoader).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null) {
                CWRMod.Instance.Logger.Warn($"TrueTooltipsRef: ItemLoader.{methodName} not found, this dispatcher is unshielded.");
                return;
            }
            VaultHook.Add(method, hookDelegate, VaultHook.DefaultHookPriority);
            hookRecords.Add((method, hookDelegate));
        }

        /// <summary>该物品是否由传奇说明面板全权自绘</summary>
        private static bool IsPanelItem(Item item)
            => item != null && panelItemTypes != null && panelItemTypes.Contains(item.type);

        private static bool OnPreDrawTooltipHook(On_PreDrawTooltip_Delegate orig,
            Item item, ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y) {
            if (!IsPanelItem(item)) {
                return orig(item, lines, ref x, ref y);
            }
            //面板物品只跑自家 ModItem（面板绘制入口），跳过全部 GlobalItem
            return item.ModItem?.PreDrawTooltip(lines, ref x, ref y) ?? true;
        }

        private static bool OnPreDrawTooltipLineHook(On_PreDrawTooltipLine_Delegate orig,
            Item item, DrawableTooltipLine line, ref int yOffset) {
            if (!IsPanelItem(item)) {
                return orig(item, line, ref yOffset);
            }
            return item.ModItem?.PreDrawTooltipLine(line, ref yOffset) ?? true;
        }

        private static void OnPostDrawTooltipLineHook(On_PostDrawTooltipLine_Delegate orig,
            Item item, DrawableTooltipLine line) {
            if (!IsPanelItem(item)) {
                orig(item, line);
                return;
            }
            item.ModItem?.PostDrawTooltipLine(line);
        }

        private static void OnPostDrawTooltipHook(On_PostDrawTooltip_Delegate orig,
            Item item, ReadOnlyCollection<DrawableTooltipLine> lines) {
            if (!IsPanelItem(item)) {
                orig(item, lines);
                return;
            }
            item.ModItem?.PostDrawTooltip(lines);
        }
    }
}
