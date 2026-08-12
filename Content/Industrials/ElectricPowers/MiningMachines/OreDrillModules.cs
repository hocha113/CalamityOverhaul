using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MiningMachines
{
    /// <summary>
    /// 定向钻头族:一枚钻头锁定一种(或一对二选一)矿物——解锁其开采资格(豁免镐力),
    /// 并把其产出权重放大到四倍。群系/进度/世界矿源门保持诚实,雪原装叶绿钻头不会出叶绿。<br/>
    /// 图标 = 共用钻头纹 + 右下角内嵌目标矿物贴图,按矿族配色
    /// </summary>
    internal abstract class BaseOreDrillModule : BaseMiningModule
    {
        /// <summary>定向目标矿物 ItemID;成对矿两个都列,世界门自动只放行在场的那一半</summary>
        internal abstract int[] TargetOres { get; }
        /// <summary>定向权重倍率</summary>
        internal virtual float FocusMult => 4f;
        /// <summary>图标内嵌与 tooltip 首位展示的矿物,成对矿按世界实际生成取</summary>
        internal virtual int DisplayOre => TargetOres[0];

        //共用定向钻头纹:两侧导轨压着一根钻杆,尖头向下
        private const string SharedDrillPath =
            "M 0 -0.56 L 0 0.1 M -0.18 0.1 L 0 0.56 L 0.18 0.1 Z "
            + "M -0.46 -0.52 L -0.46 -0.02 M 0.46 -0.52 L 0.46 -0.02 "
            + "M -0.46 -0.52 L -0.26 -0.52 M 0.46 -0.52 L 0.26 -0.52";

        protected sealed override string GlyphKey => "OreDrillShared";
        protected sealed override string GlyphPath => SharedDrillPath;

        public sealed override void CollectUnlockOres(HashSet<int> into) {
            foreach (int ore in TargetOres) {
                into.Add(ore);
            }
        }

        public sealed override void CollectOreFocus(Dictionary<int, float> into) {
            foreach (int ore in TargetOres) {
                //多枚钻头聚焦同一矿时取最大倍率,不叠乘
                into[ore] = into.TryGetValue(ore, out float cur) ? Math.Max(cur, FocusMult) : FocusMult;
            }
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            base.ModifyTooltips(tooltips);
            int index = tooltips.FindIndex(line => line.Name == "MachineModuleTargets");
            if (index < 0) {
                index = tooltips.FindIndex(line => line.Name == "ItemName");
            }
            List<string> names = [];
            foreach (int ore in TargetOres) {
                names.Add(Lang.GetItemNameValue(ore));
            }
            tooltips.Insert(index + 1, new TooltipLine(Mod, "DrillTargets",
                MiningMachineUI.DrillTargetText.Format(string.Join('/', names))) {
                OverrideColor = Color.Lerp(Accent, Color.White, 0.35f),
            });
            tooltips.Insert(index + 2, new TooltipLine(Mod, "DrillEffect",
                MiningMachineUI.DrillEffectText.Value) {
                OverrideColor = new Color(190, 175, 155),
            });
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position
            , Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            base.PreDrawInInventory(spriteBatch, position, frame, drawColor, itemColor, origin, scale);
            //右下角内嵌目标矿物贴图,一眼读出这枚钻头是给谁的;
            //原版矿贴图懒加载,不先 LoadItem 只会画出空气
            Main.instance.LoadItem(DisplayOre);
            VaultUtils.SimpleDrawItem(spriteBatch, DisplayOre, position + new Vector2(9f, 9f) * scale,
                14, 1f, 0, Color.White * (drawColor.A / 255f));
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor
            , ref float rotation, ref float scale, int whoAmI) {
            base.PreDrawInWorld(spriteBatch, lightColor, alphaColor, ref rotation, ref scale, whoAmI);
            Vector2 center = Item.Center - Main.screenPosition;
            float a = MathHelper.Max(lightColor.A / 255f, 0.35f);
            Main.instance.LoadItem(DisplayOre);
            VaultUtils.SimpleDrawItem(spriteBatch, DisplayOre,
                center + new Vector2(9f, 9f).RotatedBy(rotation) * scale, 14, 1f, 0, Color.White * a);
            return false;
        }

        /// <summary>钻头配方共用底:嘉登残料在场则附加</summary>
        protected Recipe BeginDrillRecipe() {
            Recipe recipe = CreateRecipe();
            if (CWRID.DubiousCircuitryAvailable) {
                recipe.AddIngredient(CWRID.Item_DubiousPlating, 4).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 2);
            }
            return recipe;
        }
    }

    /// <summary>铜锡钻头</summary>
    internal class CopperTinDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.CopperOre, ItemID.TinOre];
        internal override int DisplayOre
            => WorldGen.SavedOreTiers.Copper == TileID.Tin ? ItemID.TinOre : ItemID.CopperOre;
        internal override Color Accent => new(205, 125, 65);

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddRecipeGroup(CWRCrafted.TinBarGroup, 6).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    /// <summary>铁铅钻头</summary>
    internal class IronLeadDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.IronOre, ItemID.LeadOre];
        internal override int DisplayOre
            => WorldGen.SavedOreTiers.Iron == TileID.Lead ? ItemID.LeadOre : ItemID.IronOre;
        internal override Color Accent => new(172, 172, 182);

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 6).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    /// <summary>银钨钻头</summary>
    internal class SilverTungstenDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.SilverOre, ItemID.TungstenOre];
        internal override int DisplayOre
            => WorldGen.SavedOreTiers.Silver == TileID.Tungsten ? ItemID.TungstenOre : ItemID.SilverOre;
        internal override Color Accent => new(208, 214, 224);

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddRecipeGroup(CWRCrafted.TungstenBarGroup, 6).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    /// <summary>金铂钻头</summary>
    internal class GoldPlatinumDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.GoldOre, ItemID.PlatinumOre];
        internal override int DisplayOre
            => WorldGen.SavedOreTiers.Gold == TileID.Platinum ? ItemID.PlatinumOre : ItemID.GoldOre;
        internal override Color Accent => new(242, 198, 88);

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddRecipeGroup(CWRCrafted.GoldBarGroup, 6).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    /// <summary>邪金钻头:魔金与猩红矿一枚通吃,跟着世界邪恶走</summary>
    internal class EvilOreDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.DemoniteOre, ItemID.CrimtaneOre];
        internal override int DisplayOre => WorldGen.crimson ? ItemID.CrimtaneOre : ItemID.DemoniteOre;
        internal override Color Accent => new(165, 95, 195);

        public override void AddRecipes() {
            //两种邪恶各给一条配方,拿得到哪种锭就用哪种
            BeginDrillRecipe().
            AddIngredient(ItemID.DemoniteBar, 6).
            AddTile(TileID.Anvils).
            Register();

            BeginDrillRecipe().
            AddIngredient(ItemID.CrimtaneBar, 6).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    /// <summary>陨铁钻头</summary>
    internal class MeteoriteDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.Meteorite];
        internal override Color Accent => new(198, 108, 78);

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddIngredient(ItemID.MeteoriteBar, 6).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    /// <summary>黑曜钻头</summary>
    internal class ObsidianDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.Obsidian];
        internal override Color Accent => new(135, 95, 175);

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddIngredient(ItemID.Obsidian, 20).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    /// <summary>狱岩钻头</summary>
    internal class HellstoneDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.Hellstone];
        internal override Color Accent => new(248, 120, 58);

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddIngredient(ItemID.HellstoneBar, 6).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    /// <summary>化石钻头</summary>
    internal class FossilDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.FossilOre];
        internal override Color Accent => new(218, 198, 148);

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddIngredient(ItemID.FossilOre, 15).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    /// <summary>煤层钻头</summary>
    internal class CoalDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.Coal];
        internal override Color Accent => new(122, 122, 128);

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddIngredient(ItemID.Coal, 10).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    /// <summary>晶簇钻头:六种宝石一枚通吃</summary>
    internal class GemClusterDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [
            ItemID.Amethyst, ItemID.Topaz, ItemID.Sapphire,
            ItemID.Emerald, ItemID.Ruby, ItemID.Diamond,
        ];
        internal override int DisplayOre => ItemID.Diamond;
        internal override Color Accent => new(198, 128, 228);

        protected override void SetModuleDefaults() {
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddIngredient(ItemID.Diamond, 3).
            AddIngredient(ItemID.Ruby, 3).
            AddTile(TileID.Anvils).
            Register();
        }
    }

    /// <summary>钴钯钻头</summary>
    internal class CobaltPalladiumDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.CobaltOre, ItemID.PalladiumOre];
        internal override int DisplayOre
            => WorldGen.SavedOreTiers.Cobalt == TileID.Palladium ? ItemID.PalladiumOre : ItemID.CobaltOre;
        internal override Color Accent => new(98, 148, 238);

        protected override void SetModuleDefaults() {
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 2);
        }

        public override void AddRecipes() {
            //二选一矿各给一条配方
            BeginDrillRecipe().
            AddIngredient(ItemID.CobaltBar, 6).
            AddTile(TileID.MythrilAnvil).
            Register();

            BeginDrillRecipe().
            AddIngredient(ItemID.PalladiumBar, 6).
            AddTile(TileID.MythrilAnvil).
            Register();
        }
    }

    /// <summary>秘银山铜钻头</summary>
    internal class MythrilOrichalcumDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.MythrilOre, ItemID.OrichalcumOre];
        internal override int DisplayOre
            => WorldGen.SavedOreTiers.Mythril == TileID.Orichalcum ? ItemID.OrichalcumOre : ItemID.MythrilOre;
        internal override Color Accent => new(98, 218, 198);

        protected override void SetModuleDefaults() {
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 2);
        }

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddRecipeGroup(CWRCrafted.MythrilBarGroup, 6).
            AddTile(TileID.MythrilAnvil).
            Register();
        }
    }

    /// <summary>精金钛金钻头</summary>
    internal class AdamantiteTitaniumDrill : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.AdamantiteOre, ItemID.TitaniumOre];
        internal override int DisplayOre
            => WorldGen.SavedOreTiers.Adamantite == TileID.Titanium ? ItemID.TitaniumOre : ItemID.AdamantiteOre;
        internal override Color Accent => new(238, 98, 118);

        protected override void SetModuleDefaults() {
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 2);
        }

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddRecipeGroup(CWRCrafted.AdamantiteBarGroup, 6).
            AddTile(TileID.MythrilAnvil).
            Register();
        }
    }

    /// <summary>叶绿钻探:允许矿机开采叶绿矿(类名与本地化键保持既有,存档兼容)</summary>
    internal class ChlorophyteDrillModule : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.ChlorophyteOre];
        internal override Color Accent => new(126, 220, 100);

        protected override void SetModuleDefaults() {
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 3);
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(ItemID.ChlorophyteBar, 8).
                AddIngredient(CWRID.Item_DubiousPlating, 10).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 5).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.ChlorophyteBar, 8).
                AddIngredient(ItemID.SoulofMight, 5).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
        }
    }

    /// <summary>夜明钻探:月总之后,让矿机采得动月亮的碎屑</summary>
    internal class LuminiteDrillModule : BaseOreDrillModule
    {
        internal override int[] TargetOres => [ItemID.LunarOre];
        internal override Color Accent => new(152, 242, 202);

        protected override void SetModuleDefaults() {
            Item.rare = ItemRarityID.Red;
            Item.value = Item.sellPrice(gold: 5);
        }

        public override void AddRecipes() {
            BeginDrillRecipe().
            AddIngredient(ItemID.LunarBar, 6).
            AddTile(TileID.LunarCraftingStation).
            Register();
        }
    }
}
