using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 头盔镶嵌配方系统：矿石头盔按档位成链（锡/铜→铅/铁→钨/银→金/铂金；钴/钯金→秘银/山铜→精金/钛金），
    /// 每顶高一档头盔另有一组神匠模式专属配方：原版材料 + 低一档任一头盔，成品把那顶头盔连同它自己的链一起镶进去
    /// （数据写在 <see cref="GodSmithHelmetNestItem"/>，穿好整套后逐级继承套装奖励）。<br/>
    /// 金/铂金头盔在神匠模式下另需魔矿锭或血腥锭：原版配方挂「神匠沉眠」条件让位，
    /// 神匠侧补一条不镶嵌的素配方。所有配方只在神匠模式开启时可见
    /// </summary>
    internal class GsHelmetNestSystem : ModSystem
    {
        /// <summary>一档里的一种矿石：Heads 首项是配方组的代表头盔，Craftable 是有原版配方可克隆的成品头盔</summary>
        private readonly record struct NestOre(string Name, int[] Heads, int[] Craftable);

        private static readonly NestOre[][] PreHardmodeTiers = [
            [
                new NestOre("Copper", [ItemID.CopperHelmet], [ItemID.CopperHelmet]),
                new NestOre("Tin", [ItemID.TinHelmet], [ItemID.TinHelmet]),
            ],
            [
                new NestOre("Iron", [ItemID.IronHelmet, ItemID.AncientIronHelmet], [ItemID.IronHelmet]),
                new NestOre("Lead", [ItemID.LeadHelmet], [ItemID.LeadHelmet]),
            ],
            [
                new NestOre("Silver", [ItemID.SilverHelmet], [ItemID.SilverHelmet]),
                new NestOre("Tungsten", [ItemID.TungstenHelmet], [ItemID.TungstenHelmet]),
            ],
            [
                new NestOre("Gold", [ItemID.GoldHelmet, ItemID.AncientGoldHelmet], [ItemID.GoldHelmet]),
                new NestOre("Platinum", [ItemID.PlatinumHelmet], [ItemID.PlatinumHelmet]),
            ],
        ];

        private static readonly NestOre[][] HardmodeTiers = [
            [
                new NestOre("Cobalt", [ItemID.CobaltHelmet, ItemID.CobaltHat, ItemID.CobaltMask],
                    [ItemID.CobaltHelmet, ItemID.CobaltHat, ItemID.CobaltMask]),
                new NestOre("Palladium", [ItemID.PalladiumHelmet, ItemID.PalladiumHeadgear, ItemID.PalladiumMask],
                    [ItemID.PalladiumHelmet, ItemID.PalladiumHeadgear, ItemID.PalladiumMask]),
            ],
            [
                new NestOre("Mythril", [ItemID.MythrilHelmet, ItemID.MythrilHood, ItemID.MythrilHat],
                    [ItemID.MythrilHelmet, ItemID.MythrilHood, ItemID.MythrilHat]),
                new NestOre("Orichalcum", [ItemID.OrichalcumHelmet, ItemID.OrichalcumHeadgear, ItemID.OrichalcumMask],
                    [ItemID.OrichalcumHelmet, ItemID.OrichalcumHeadgear, ItemID.OrichalcumMask]),
            ],
            [
                new NestOre("Adamantite", [ItemID.AdamantiteHelmet, ItemID.AdamantiteHeadgear, ItemID.AdamantiteMask],
                    [ItemID.AdamantiteHelmet, ItemID.AdamantiteHeadgear, ItemID.AdamantiteMask]),
                new NestOre("Titanium", [ItemID.TitaniumHelmet, ItemID.TitaniumHeadgear, ItemID.TitaniumMask],
                    [ItemID.TitaniumHelmet, ItemID.TitaniumHeadgear, ItemID.TitaniumMask]),
            ],
        ];

        /// <summary>金/铂金头盔在神匠模式下额外消耗的邪恶矿锭数量</summary>
        private const int EvilBarCost = 5;

        private const string EvilBarGroup = "CWRMod:GsEvilBar";

        private static string HelmetGroup(in NestOre ore) => "CWRMod:GsHelmets" + ore.Name;

        public override void AddRecipeGroups() {
            string any = Language.GetTextValue("LegacyMisc.37");
            RecipeGroup.RegisterGroup(EvilBarGroup, new RecipeGroup(
                () => $"{any} {Lang.GetItemNameValue(ItemID.DemoniteBar)}", ItemID.DemoniteBar, ItemID.CrimtaneBar));
            RegisterHelmetGroups(PreHardmodeTiers);
            RegisterHelmetGroups(HardmodeTiers);
        }

        private static void RegisterHelmetGroups(NestOre[][] tiers) {
            //只有作材料的档位（非顶档）且有多顶头盔的矿石才需要配方组
            for (int t = 0; t < tiers.Length - 1; t++) {
                foreach (NestOre ore in tiers[t]) {
                    if (ore.Heads.Length <= 1) {
                        continue;
                    }
                    int[] heads = ore.Heads;
                    RecipeGroup.RegisterGroup(HelmetGroup(ore), new RecipeGroup(
                        () => GodSmithHelmetNestItem.GroupName(heads), heads));
                }
            }
        }

        public override void AddRecipes() {
            AddFamily(PreHardmodeTiers, topTierNeedsEvilBars: true);
            AddFamily(HardmodeTiers, topTierNeedsEvilBars: false);
            RegisterSlots(PreHardmodeTiers);
            RegisterSlots(HardmodeTiers);
        }

        public override void Unload() => GodSmithHelmetNestItem.Slots.Clear();

        /// <summary>
        /// 把档位关系登记给 tooltip 提示：每顶可合成的高档头盔记下它能接收的低一档头盔组，
        /// 每顶低档头盔（含远古变体）记下它能镶进的高一档成品头盔组。与 <see cref="AddFamily"/> 同一份档位表，配方与提示不会错位
        /// </summary>
        private static void RegisterSlots(NestOre[][] tiers) {
            for (int t = 0; t < tiers.Length; t++) {
                foreach (NestOre ore in tiers[t]) {
                    if (t > 0) {
                        foreach (int head in ore.Craftable) {
                            foreach (NestOre lower in tiers[t - 1]) {
                                GodSmithHelmetNestItem.SlotFor(head).Lower.Add(lower.Heads);
                            }
                        }
                    }
                    if (t < tiers.Length - 1) {
                        foreach (int head in ore.Heads) {
                            foreach (NestOre upper in tiers[t + 1]) {
                                GodSmithHelmetNestItem.SlotFor(head).Upper.Add(upper.Craftable);
                            }
                        }
                    }
                }
            }
        }

        private static void AddFamily(NestOre[][] tiers, bool topTierNeedsEvilBars) {
            for (int t = 1; t < tiers.Length; t++) {
                bool top = topTierNeedsEvilBars && t == tiers.Length - 1;
                foreach (NestOre ore in tiers[t]) {
                    foreach (int head in ore.Craftable) {
                        foreach (Recipe vanilla in VanillaRecipesFor(head)) {
                            if (top) {
                                //顶档素配方：原版材料 + 邪恶矿锭，不镶嵌
                                Recipe plain = vanilla.Clone();
                                plain.AddRecipeGroup(EvilBarGroup, EvilBarCost);
                                plain.AddCondition(GameModeText.GodSmithRecipeOn, () => GameModeSystem.GodSmithActive);
                                plain.Register();
                            }
                            foreach (NestOre lower in tiers[t - 1]) {
                                Recipe nest = vanilla.Clone();
                                if (lower.Heads.Length > 1) {
                                    nest.AddRecipeGroup(HelmetGroup(lower));
                                }
                                else {
                                    nest.AddIngredient(lower.Heads[0]);
                                }
                                if (top) {
                                    nest.AddRecipeGroup(EvilBarGroup, EvilBarCost);
                                }
                                nest.AddCondition(GameModeText.GodSmithRecipeOn, () => GameModeSystem.GodSmithActive);
                                nest.AddOnCraftCallback(EmbedOnCraft);
                                nest.Register();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>该成品的全部原版配方（先收集再返回，避免注册新配方时改动遍历范围）</summary>
        private static List<Recipe> VanillaRecipesFor(int resultType) {
            List<Recipe> found = [];
            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe recipe = Main.recipe[i];
                if (recipe.Mod == null && recipe.createItem.type == resultType) {
                    found.Add(recipe);
                }
            }
            return found;
        }

        /// <summary>成品回调：把消耗掉的那顶低档头盔（连同它的链）镶进成品</summary>
        private static void EmbedOnCraft(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            foreach (Item consumed in consumedItems) {
                if (consumed == null || consumed.IsAir || consumed.type == item.type
                    || !GodSmithArmorScheme.SchemeByHead.ContainsKey(consumed.type)) {
                    continue;
                }
                GodSmithHelmetNestItem.Embed(item, consumed);
                return;
            }
        }

        public override void PostAddRecipes() {
            //金/铂金的原版素配方在神匠模式下让位给带邪恶矿锭的神匠配方
            int[] topHeads = [ItemID.GoldHelmet, ItemID.PlatinumHelmet];
            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe recipe = Main.recipe[i];
                if (recipe.Mod != null) {
                    continue;
                }
                for (int k = 0; k < topHeads.Length; k++) {
                    if (recipe.createItem.type == topHeads[k]) {
                        recipe.AddCondition(GameModeText.GodSmithRecipeOff, () => !GameModeSystem.GodSmithActive);
                        break;
                    }
                }
            }
        }
    }
}
