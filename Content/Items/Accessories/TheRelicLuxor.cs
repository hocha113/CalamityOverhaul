using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Accessories;
using CalamityMod.Items.Materials;
using CalamityOverhaul;
using CalamityOverhaul.Content.CWRDamageTypes;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories
{
    internal class TheRelicLuxor : ModItem
    {
        public override string Texture => CWRConstant.Item + "TheRelicLuxor";

        public override bool IsLoadingEnabled(Mod mod) {
            return false;
        }

        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(6, 4));
            ItemID.Sets.AnimatesAsSoul[Item.type] = true;
            ItemID.Sets.ItemNoGravity[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.width = 58;
            Item.height = 48;
            Item.value = CalamityGlobalItem.RarityHotPinkBuyPrice;
            Item.rare = ItemRarityID.Orange;
            Item.accessory = true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            Player player = Main.player[Main.myPlayer];
            if (tooltips == null || player == null) return;

            Item item = player.HeldItem;
            if (item == null) return;

            TooltipLine cumstops = tooltips.FirstOrDefault((x) => x.Text.Contains("[tips]") && x.Mod == "Terraria");
            if (cumstops == null) return;

            if (item.CountsAsClass<EndlessDamageClass>()) {
                OnIfyTops(tooltips, cumstops);
                return;
            }

            if (item.CountsAsClass<MeleeDamageClass>() || item.CountsAsClass<TrueMeleeNoSpeedDamageClass>()) {
                cumstops.Text = CWRUtils.Translation(
                    "刀刃的挥舞将发射出炽热的灵魂\n"
                    + "忠！诚！",
                    "The wave of the blade will emit a fiery soul\n"
                    + "Loyal! !"
                    );
                cumstops.OverrideColor = Color.Lerp(Color.Red, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            }
            else if (item.CountsAsClass<ThrowingDamageClass>()) {
                cumstops.Text = CWRUtils.Translation(
                    "投掷出高速弹跳的耀界之灵",
                    "Hurl the Spirit of Glory with a high speed bounce"
                    );
                cumstops.OverrideColor = Color.Lerp(Color.BlueViolet, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            }
            else if (item.CountsAsClass<RangedDamageClass>()) {
                cumstops.Text = CWRUtils.Translation(
                    "枪口将迸发出耀界闪电",
                    "The muzzle of the gun will burst forth lightning"
                    );
                cumstops.OverrideColor = Color.Lerp(Color.AliceBlue, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            }
            else if (item.CountsAsClass<MagicDamageClass>()) {
                cumstops.Text = CWRUtils.Translation(
                    "散落的魔力将凝聚为金源炸弹",
                    "The scattered magic will condense into the gold source bomb"
                    );
                cumstops.OverrideColor = Color.Lerp(Color.Gold, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            }
            else if (item.CountsAsClass<SummonDamageClass>()) {
                cumstops.Text = CWRUtils.Translation(
                    "召唤泛金能量体为你而战",
                    "Summon Pangold Energies to fight for you"
                    );
                cumstops.OverrideColor = Color.Lerp(Color.LightGoldenrodYellow, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            }
            else {
                cumstops.Text = CWRUtils.Translation(
                    "耀界之神将对你提供多种援助",
                    "The Gods of Glory will offer you many kinds of assistance"
                    );
                cumstops.OverrideColor = Color.Lerp(Color.LavenderBlush, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
            }

            List<TooltipLine> newTooltips = new List<TooltipLine>(tooltips);

            KeyboardState state = Keyboard.GetState();
            if (state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift)) {
                OnIfyTops(tooltips, cumstops);
            }
            else {
                foreach (TooltipLine line in tooltips.ToList()) //复制 tooltips 集合，以便在遍历时修改
                {
                    if (line.Name == "Assmt")
                        line.Hide();
                }
            }
        }

        public void OnIfyTops(List<TooltipLine> tooltips, TooltipLine cumstops) {
            cumstops.Text = CWRUtils.Translation(
                    CWRUtils.Translation(
                    "耀界之神将对你提供多种援助：",
                    "The Gods of Glory will offer you many kinds of assistance:"
                    ));
            cumstops.OverrideColor = Color.Lerp(Color.LavenderBlush, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);


            TooltipLine newLine1 = new TooltipLine(Mod, "Assmt", CWRUtils.Translation(
                "投掷出高速弹跳的耀界之灵",
                "Hurl the Spirit of Glory with a high speed bounce"
                )
                ) {
                OverrideColor = Color.Lerp(Color.BlueViolet, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f)
            };

            TooltipLine newLine2 = new TooltipLine(Mod, "Assmt", CWRUtils.Translation(
                "枪口将迸发出耀界闪电",
                "The muzzle of the gun will burst forth lightning"
                )
                ) {
                OverrideColor = Color.Lerp(Color.AliceBlue, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f)
            };

            TooltipLine newLine3 = new TooltipLine(Mod, "Assmt", CWRUtils.Translation(
                "散落的魔力将凝聚为金源炸弹",
                "The scattered magic will condense into the gold source bomb"
                )
                ) {
                OverrideColor = Color.Lerp(Color.Gold, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f)
            };

            TooltipLine newLine4 = new TooltipLine(Mod, "Assmt", CWRUtils.Translation(
                "召唤泛金能量体为你而战",
                "Summon Pangold Energies to fight for you"
                )
                ) {
                OverrideColor = Color.Lerp(Color.LightGoldenrodYellow, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f)
            };

            TooltipLine newLine5 = new TooltipLine(Mod, "Assmt", CWRUtils.Translation(
                "刀刃的挥舞将发射出炽热的灵魂\n"
                + "\"忠！诚！\"",
                "The wave of the blade will emit a fiery soul\n"
                + "\"Loyal! !\""
                )
                ) {
                OverrideColor = Color.Lerp(Color.Red, Color.Goldenrod, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f)
            };

            tooltips.Add(newLine1);
            tooltips.Add(newLine2);
            tooltips.Add(newLine3);
            tooltips.Add(newLine4);
            tooltips.Add(newLine5);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            //player.CWR().theRelicLuxor = 1;
        }

        public override void AddRecipes() {
            Recipe.Create(Type)
                .AddIngredient(ModContent.ItemType<LuxorsGift>())
                .AddIngredient(ModContent.ItemType<DivineGeode>(), 13)
                .AddIngredient(ModContent.ItemType<UnholyEssence>(), 13)
                .AddRecipeGroup(CWRRecipes.ARGroup, 1)
                .Register();
        }
    }
}
