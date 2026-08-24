using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using InnoVault.Narrative.Composition;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    internal static class ShenyoGiftComposerExtensions
    {
        /// <summary>
        /// 递唤雨符（镜像真夜 GiftReward）：书符演出起笔（Command）→ 按演出时长对拍等待（Wait）→
        /// 框架奖励弹窗发符纸（Reward，优先进背包装不下才落地）→ 弹窗解析后幂等补录符箧
        /// （Command 调 <see cref="KikasaTalismanOwned.Unlock"/>，防符纸丢失断档）。<br/>
        /// 名册缺场或符物品尚未注册时优雅跳过：不演不弹，只打一行警告，对话正常收尾，
        /// 符物品后续落地即自动生效。不做成败判定，符纸进没进包与"这场戏演过了"无关
        /// </summary>
        public static NarrativeComposer GiftTalisman(this NarrativeComposer composer, string giftId) {
            if (!ShenyoGiftCatalog.TryGet(giftId, out ShenyoGiftEntry entry)) {
                CWRMod.Instance.Logger.Warn($"[ShenyoGift] GiftTalisman: unknown gift id '{giftId}', skipped");
                return composer;
            }

            string key = entry.TalismanKey;
            int itemType = KikasaTalismanItem.ItemTypeForKey(key);
            if (itemType <= 0) {
                CWRMod.Instance.Logger.Warn(
                    $"[ShenyoGift] GiftTalisman: talisman item '{key}' not registered yet, gift '{giftId}' skipped");
                return composer;
            }

            return composer
                .Command(() => KikasaTalismanScribeOverlay.Begin(key))
                //演出收尾后留 6 tick 缓冲再弹奖励，符纸入怀与弹窗衔接不抢拍
                .Wait(KikasaTalismanScribeOverlay.TotalTicksFor(key) + 6)
                .Reward(itemType, title: string.Empty)
                //奖励弹窗是阻塞节点，此命令在其解析后才执行：无论领取与否都补录符箧
                .Command(() => KikasaTalismanOwned.Unlock(Main.LocalPlayer, key));
        }
    }
}
