using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules
{
    /// <summary>改件聚合：收集已装备改件 Apply 得 ShootContext</summary>
    internal static class SHPCModificationSystem
    {
        /// <summary>解析射击上下文，player 空则默认</summary>
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

        /// <summary>槽位改件实例，未装备 null</summary>
        public static SHPCModuleItem GetEquippedAt(Player player, int slotIdx) {
            if (player == null) {
                return null;
            }
            return player.GetModPlayer<SHPCPlayer>().GetModule(slotIdx)?.ModItem as SHPCModuleItem;
        }

        /// <summary>是否装备指定改件；持久衍生弹幕每帧自检</summary>
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

        /// <summary>遍历已装备改件执行 action</summary>
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
