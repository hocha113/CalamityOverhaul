using System.Collections.Generic;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.Generator.Biomass
{
    /// <summary>
    /// 生物质燃料表:只收农业废料流(种子/草药/蘑菇/鱼获/农产),与热电机的
    /// <see cref="FuelItems"/> 刻意分离,仅凝胶等少数条目双表共存。
    /// 燃烧时长复用 <see cref="FuelItems.GetBurnDuration"/> 的 sqrt 缩放
    /// </summary>
    internal class BiomassFuel
    {
        /// <summary>物品→热值;与 FuelItems 的重叠面:Gel/PinkGel/GlowingMushroom/Acorn/Hay/Vine</summary>
        public static readonly Dictionary<int, int> BiomassToCombustion = new() {
            //=== 凝胶(史莱姆培养槽的产出,闭环主燃料) ===
            { ItemID.Gel, 60 },
            { ItemID.PinkGel, 120 },

            //=== 蘑菇(蘑菇农场机的产出) ===
            { ItemID.Mushroom, 12 },
            { ItemID.GlowingMushroom, 15 },
            { ItemID.VileMushroom, 10 },
            { ItemID.ViciousMushroom, 10 },

            //=== 草药(草药农场机的过剩产出) ===
            { ItemID.Daybloom, 12 },
            { ItemID.Moonglow, 12 },
            { ItemID.Blinkroot, 12 },
            { ItemID.Deathweed, 12 },
            { ItemID.Waterleaf, 12 },
            { ItemID.Fireblossom, 12 },
            { ItemID.Shiverthorn, 12 },

            //=== 种子(草药机回填后仍会溢出) ===
            { ItemID.DaybloomSeeds, 6 },
            { ItemID.MoonglowSeeds, 6 },
            { ItemID.BlinkrootSeeds, 6 },
            { ItemID.DeathweedSeeds, 6 },
            { ItemID.WaterleafSeeds, 6 },
            { ItemID.FireblossomSeeds, 6 },
            { ItemID.ShiverthornSeeds, 6 },
            { ItemID.GrassSeeds, 5 },
            { ItemID.JungleGrassSeeds, 6 },
            { ItemID.MushroomGrassSeeds, 6 },
            { ItemID.CorruptSeeds, 6 },
            { ItemID.CrimsonSeeds, 6 },
            { ItemID.HallowedSeeds, 6 },
            { ItemID.PumpkinSeed, 8 },

            //=== 农产与杂料 ===
            { ItemID.Pumpkin, 35 },
            { ItemID.Cactus, 18 },
            { ItemID.Hay, 12 },
            { ItemID.Acorn, 15 },
            { ItemID.Vine, 40 },
            { ItemID.JungleSpores, 25 },

            //=== 过剩鱼获(自动钓鱼机的产出流) ===
            { ItemID.Bass, 25 },
            { ItemID.Trout, 25 },
            { ItemID.Salmon, 25 },
            { ItemID.AtlanticCod, 25 },
            { ItemID.Tuna, 25 },
            { ItemID.RedSnapper, 25 },
            { ItemID.NeonTetra, 20 },
            { ItemID.ArmoredCavefish, 20 },
            { ItemID.Damselfish, 20 },
            { ItemID.CrimsonTigerfish, 20 },
            { ItemID.FrostMinnow, 20 },
            { ItemID.SpecularFish, 20 },
            { ItemID.PrincessFish, 20 },
            { ItemID.GoldenCarp, 30 },
            { ItemID.FlarefinKoi, 30 },
            { ItemID.Obsidifish, 28 },
            { ItemID.VariegatedLardfish, 25 },
            { ItemID.Ebonkoi, 25 },
            { ItemID.Hemopiranha, 25 },
            { ItemID.Rockfish, 25 },
            { ItemID.Stinkfish, 25 },
            { ItemID.Shrimp, 15 },
            { ItemID.Prismite, 28 },
            { ItemID.ChaosFish, 30 },
        };

        /// <summary>是不是生物质燃料</summary>
        public static bool IsBiomass(int itemType) => BiomassToCombustion.ContainsKey(itemType);
    }
}
