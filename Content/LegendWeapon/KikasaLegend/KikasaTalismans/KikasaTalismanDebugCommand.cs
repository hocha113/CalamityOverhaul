#if DEBUG
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 唤雨符验收一键路径，仅调试构建存在（正式获取：礼物符走礼物戏，霖/潦/沛走合成）：<br/>
    /// /fuall：解锁注册表全部符 Key 入符箧，并发放全部礼物符纸（SortOrder≥100 的 24 张）<br/>
    /// /fuall &lt;Key&gt;：解锁并单发一张（如 /fuall FuSha）
    /// </summary>
    internal class FuAllCommand : ModCommand
    {
        /// <summary>礼物符的 SortOrder 起点，之下是合成符（只录符箧不发纸）</summary>
        private const int GiftSortOrderStart = 100;

        public override string Command => "fuall";
        public override CommandType Type => CommandType.Chat;
        public override string Description => "解锁全部唤雨符并发放礼物符纸（调试）；/fuall <Key> 单发";

        public override void Action(CommandCaller caller, string input, string[] args) {
            if (args.Length > 0) {
                GiveOne(caller, args[0]);
                return;
            }
            Player player = caller.Player;
            int unlocked = 0, given = 0;
            foreach (KikasaTalismanDefinition definition in KikasaTalismanRegistry.All) {
                if (KikasaTalismanOwned.Unlock(player, definition.Key)) {
                    unlocked++;
                }
                if (definition.SortOrder < GiftSortOrderStart) {
                    continue;
                }
                int type = KikasaTalismanItem.ItemTypeForKey(definition.Key);
                if (type > 0) {
                    player.QuickSpawnItem(player.GetSource_Misc("CWR_FuAllDebug"), type);
                    given++;
                }
            }
            caller.Reply($"符箧新录 {unlocked} 键，发放礼物符纸 {given} 张", Color.LightSkyBlue);
        }

        private static void GiveOne(CommandCaller caller, string key) {
            if (!KikasaTalismanRegistry.TryGet(key, out KikasaTalismanDefinition definition)) {
                caller.Reply($"未注册的符 Key：{key}", Color.IndianRed);
                return;
            }
            Player player = caller.Player;
            KikasaTalismanOwned.Unlock(player, definition.Key);
            int type = KikasaTalismanItem.ItemTypeForKey(definition.Key);
            if (type <= 0) {
                caller.Reply($"符 {key} 无对应符纸物品", Color.IndianRed);
                return;
            }
            player.QuickSpawnItem(player.GetSource_Misc("CWR_FuAllDebug"), type);
        }
    }
}
#endif
