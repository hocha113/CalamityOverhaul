using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.Atlas
{
    /// <summary>
    /// 图鉴深度带分配：AtlasTier → 本表 → 解锁鱼稀有度
    /// 仅影响图鉴布局，不改玩法数值
    /// </summary>
    internal static class AtlasTierMap
    {
        //0浅滩 1远洋 2深海 3深渊
        private static readonly Dictionary<string, int> Curated = new() {
            //浅滩：地表与最早期可得
            ["FishDirt"] = 0,
            ["FishSlime"] = 0,
            ["FishBunny"] = 0,
            ["FishCat"] = 0,
            ["FishMud"] = 0,
            ["FishZombie"] = 0,
            ["FishDrizzle"] = 0,
            ["FishSparkling"] = 0,
            ["FishRock"] = 0,
            ["FishCloud"] = 0,
            //远洋：地下、天空、丛林、沙漠等前困难期环境
            ["FishofCthulu"] = 1,
            ["FishHarpy"] = 1,
            ["FishFrostMinnow"] = 1,
            ["FishPeng"] = 1,
            ["FishCrimsonTiger"] = 1,
            ["FishEaterofPlankton"] = 1,
            ["FishBone"] = 1,
            ["FishBat"] = 1,
            ["FishJewel"] = 1,
            ["FishScorpio"] = 1,
            ["FishAmanita"] = 1,
            ["FishNeonTetra"] = 1,
            ["FishHoney"] = 1,
            ["FishTropicalBarracuda"] = 1,
            ["FishDynamite"] = 1,
            ["FishHunger"] = 1,
            //深海：地狱与困难模式环境
            ["FishObsidian"] = 2,
            ["FishDemonicHell"] = 2,
            ["FishVoodoo"] = 2,
            ["FishFallenStar"] = 2,
            ["FishIchorn"] = 2,
            ["FishCursed"] = 2,
            ["FishUnicorn"] = 2,
            ["FishPrincess"] = 2,
            ["FishPrismite"] = 2,
            ["FishWyverntail"] = 2,
            ["FishVariegatedLard"] = 2,
            ["FishDoubleCod"] = 2,
            ["FishTunabeard"] = 2,
            //深渊：终局强力技能
            ["FishBloodyManowar"] = 3,
            ["FishSwarm"] = 3,
            ["Fishotroning"] = 3,
            ["FishBrimlish"] = 3,
        };

        /// <summary>
        /// 取得技能所属的深度带（0-3）
        /// </summary>
        public static int GetTier(FishSkill skill) {
            if (skill == null) {
                return 0;
            }
            if (skill.AtlasTier >= 0) {
                return System.Math.Clamp(skill.AtlasTier, 0, HalibutTheme.AtlasTierCount - 1);
            }
            if (Curated.TryGetValue(skill.Name, out int tier)) {
                return tier;
            }
            //回退：按解锁鱼的稀有度估一个带
            int fishType = skill.UnlockFishID;
            if (fishType > ItemID.None && fishType < ContentSamples.ItemsByType.Count
                && ContentSamples.ItemsByType.TryGetValue(fishType, out Item fish)) {
                return System.Math.Clamp(fish.rare switch {
                    <= 0 => 0,
                    1 => 1,
                    <= 3 => 2,
                    _ => 3,
                }, 0, HalibutTheme.AtlasTierCount - 1);
            }
            return 0;
        }
    }
}
