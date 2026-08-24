using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans;
using InnoVault.Narrative.Composition;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Shenyo.Gifts
{
    internal static class ShenyoGiftComposerExtensions
    {
        /// <summary>
        /// 递唤雨符（镜像真夜 GiftReward）：书符演出起笔（Command）与框架奖励弹窗（Reward，
        /// 优先进背包装不下才落地）同拍发出，弹窗解析后幂等补录符箧
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
                //书符演出与弹窗同拍起：符纸在身侧写就，弹窗当即可领。
                //不能拿 Wait 去等演出收尾，Wait 期间会话不吃任何输入，上一句台词会被按死两三秒
                .Command(() => KikasaTalismanScribeOverlay.Begin(key))
                .Reward(itemType, title: string.Empty)
                //奖励弹窗是阻塞节点，此命令在其解析后才执行：无论领取与否都补录符箧
                .Command(() => KikasaTalismanOwned.Unlock(Main.LocalPlayer, key));
        }
    }
}
