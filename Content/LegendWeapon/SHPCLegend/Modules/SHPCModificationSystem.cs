using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules
{
    /// <summary>
    /// 改件聚合中心：根据玩家的 <see cref="SHPCPlayer"/> 收集所有已装备改件，
    /// 调用其 <see cref="SHPCModuleItem.Apply"/> 得到最终射击上下文
    /// </summary>
    internal static class SHPCModificationSystem
    {
        /// <summary>
        /// 解析玩家当前射击上下文。玩家为空时返回中性默认值
        /// </summary>
        public static ShootContext Resolve(Player player) {
            ShootContext ctx = ShootContext.Default;
            if (player == null) {
                return ctx;
            }
            SHPCPlayer sp = player.GetModPlayer<SHPCPlayer>();
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                Item m = sp.GetModule(i);
                if (m == null || m.ModItem is not SHPCModuleItem mod) {
                    continue;
                }
                mod.Apply(ref ctx);
            }
            if (sp.OverkillStacks > 0) {
                ctx.DamageMul += sp.OverkillStacks * 0.02f;
            }
            return ctx;
        }

        /// <summary>
        /// 获取玩家指定槽位上装备的改件 ModItem 实例（未装备返回 null）
        /// </summary>
        public static SHPCModuleItem GetEquippedAt(Player player, int slotIdx) {
            if (player == null) {
                return null;
            }
            return player.GetModPlayer<SHPCPlayer>().GetModule(slotIdx)?.ModItem as SHPCModuleItem;
        }

        /// <summary>
        /// 玩家当前是否装备了指定类型的改件。
        /// 持久型衍生弹幕（悬浮炮臂、全息标线等）应每帧用它自检，改件卸下后立即消亡
        /// </summary>
        public static bool HasModule<T>(Player player) where T : SHPCModuleItem {
            if (player == null) {
                return false;
            }
            SHPCPlayer sp = player.GetModPlayer<SHPCPlayer>();
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                if (sp.GetModule(i)?.ModItem is T) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 遍历玩家当前所有已装备改件，对每个改件实例执行指定操作
        /// </summary>
        public static void ForEachModule(Player player, Action<SHPCModuleItem> action) {
            if (player == null) {
                return;
            }
            SHPCPlayer sp = player.GetModPlayer<SHPCPlayer>();
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                Item m = sp.GetModule(i);
                if (m == null || m.ModItem is not SHPCModuleItem mod) {
                    continue;
                }
                action(mod);
            }
        }
    }
}
