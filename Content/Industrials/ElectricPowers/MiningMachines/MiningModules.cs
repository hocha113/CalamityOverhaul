using CalamityOverhaul.Content.Industrials.MachineModules;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.MiningMachines
{
    /// <summary>
    /// 矿机升级模块:插入矿机模块槽生效,可拆卸转移。<br/>
    /// 物品外观与通用 tooltip 由 <see cref="BaseMachineModule"/> 承担;<br/>
    /// 效果通过 <see cref="IMiningModule"/> 被 <see cref="BaseMiningMachineTP.RefreshModifiers"/> 聚合
    /// </summary>
    internal abstract class BaseMiningModule : BaseMachineModule, IMiningModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.MiningMachine;

        public virtual float PickPowerBonus => 0f;
        public virtual float WorkIntervalMult => 1f;
        public virtual float YieldChanceMult => 1f;
        public virtual float EnergyCostMult => 1f;
        public virtual float RareByproductMult => 1f;
        public virtual float VeinWeightMult => 1f;
        public virtual float ScanSizeMult => 1f;
        public virtual float DoubleDropChance => 0f;
        public virtual bool SmeltOutput => false;
        public virtual bool ChestDeposit => false;
        public virtual void CollectUnlockOres(HashSet<int> into) { }
        public virtual void CollectOreFocus(Dictionary<int, float> into) { }
    }

    /// <summary>挖掘强化:作业周期缩短 30%</summary>
    internal class ExcavationBoosterModule : BaseMiningModule
    {
        public override float WorkIntervalMult => 0.7f;
        internal override Color Accent => new(230, 160, 70);

        //离心调速器:立轴顶帽 + 两甩臂 + 两坠球 + 底座——工业语境里"转速"的正字,
        //避开 chevron/箭头这类和其他模组撞脸的通用符号
        protected override string GlyphPath =>
            "M 0 -0.52 L 0 0.30 "
            + "M -0.12 -0.52 L 0.12 -0.52 "
            + "M 0 -0.40 L -0.34 0.00 "
            + "M 0 -0.40 L 0.34 0.00 "
            + "M -0.34 -0.04 L -0.26 0.04 L -0.34 0.12 L -0.42 0.04 Z "
            + "M 0.34 -0.04 L 0.42 0.04 L 0.34 0.12 L 0.26 0.04 Z "
            + "M -0.20 0.30 L 0.20 0.30";

        protected override void SetModuleDefaults() {
            Item.value = Item.sellPrice(gold: 1);
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 10).
                AddIngredient(CWRID.Item_DubiousPlating, 3).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 3).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 10).
                AddIngredient(ItemID.Cog, 6).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>镐力强化:附加 60% 镐力,触及更高矿阶</summary>
    internal class PickPowerBoosterModule : BaseMiningModule
    {
        public override float PickPowerBonus => 60f;
        internal override Color Accent => new(120, 168, 210);

        //镐:弧形镐头 + 直柄 + 缠握纹
        protected override string GlyphPath =>
            "M -0.56 -0.16 Q 0 -0.66 0.56 -0.16 "
            + "M 0 -0.42 L 0 0.60 "
            + "M -0.11 0.30 L 0.11 0.38 M -0.11 0.46 L 0.11 0.54";

        protected override void SetModuleDefaults() {
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 8).
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 3).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 8).
                AddIngredient(ItemID.Diamond, 2).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>效率强化:周期能耗降至 60%</summary>
    internal class EfficiencyModule : BaseMiningModule
    {
        public override float EnergyCostMult => 0.6f;
        internal override Color Accent => new(140, 210, 170);

        //一道细腰闪电,读作"省着用电"
        protected override string GlyphPath =>
            "M 0.08 -0.58 L -0.22 0.04 L 0.02 0.04 L -0.08 0.58 L 0.26 -0.08 L 0.02 -0.08 Z";

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.TungstenBarGroup, 8).
                AddIngredient(CWRID.Item_DubiousPlating, 3).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 3).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.TungstenBarGroup, 8).
                AddIngredient(ItemID.Wire, 8).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>产出强化:产出判定概率 ×1.35</summary>
    internal class YieldBoosterModule : BaseMiningModule
    {
        public override float YieldChanceMult => 1.35f;
        internal override Color Accent => new(235, 190, 90);

        //满载矿斗 + 上行箭头
        protected override string GlyphPath =>
            "M -0.4 0.36 L 0.4 0.36 M -0.3 0.36 L -0.3 0.56 L 0.3 0.56 L 0.3 0.36 "
            + "M 0 0.2 L 0 -0.5 M -0.2 -0.28 L 0 -0.52 L 0.2 -0.28";

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 8).
                AddIngredient(CWRID.Item_DubiousPlating, 3).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 3).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 8).
                AddIngredient(ItemID.Diamond, 1).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>勘探强化:稀有副产物(宝石/化石/陨石/残料)权重 ×3</summary>
    internal class ProspectorModule : BaseMiningModule
    {
        public override float RareByproductMult => 3f;
        internal override Color Accent => new(190, 130, 220);

        //放大镜 + 一点星耀
        protected override string GlyphPath =>
            "M -0.1 -0.52 Q -0.5 -0.52 -0.5 -0.12 Q -0.5 0.28 -0.1 0.28 Q 0.3 0.28 0.3 -0.12 Q 0.3 -0.52 -0.1 -0.52 Z "
            + "M 0.2 0.16 L 0.52 0.52 "
            + "M 0.38 -0.38 L 0.54 -0.38 M 0.46 -0.46 L 0.46 -0.30";

        protected override void SetModuleDefaults() {
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 5).
                AddIngredient(ItemID.Ruby, 2).
                AddIngredient(ItemID.Diamond, 2).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 3).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.GoldBarGroup, 5).
                AddIngredient(ItemID.Ruby, 2).
                AddIngredient(ItemID.Diamond, 2).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>勘探阵列:扫描范围(宽与深)×1.5,看见更远的矿脉</summary>
    internal class SurveyArrayModule : BaseMiningModule
    {
        public override float ScanSizeMult => 1.5f;
        internal override Color Accent => new(120, 200, 220);

        //基点 + 两圈外扩的探波
        protected override string GlyphPath =>
            "M 0 0.44 "
            + "M -0.3 0.2 Q 0 -0.08 0.3 0.2 "
            + "M -0.5 -0.04 Q 0 -0.44 0.5 -0.04";

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.TungstenBarGroup, 6).
                AddIngredient(ItemID.Glass, 12).
                AddIngredient(ItemID.Lens, 2).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 3).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.TungstenBarGroup, 6).
                AddIngredient(ItemID.Glass, 12).
                AddIngredient(ItemID.Lens, 2).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>矿脉聚焦:矿脉加成权重 ×1.6,产出更贴着真实矿脉走</summary>
    internal class VeinFocusModule : BaseMiningModule
    {
        public override float VeinWeightMult => 1.6f;
        internal override Color Accent => new(230, 140, 70);

        //准星四刻 + 中央一段矿脉折线
        protected override string GlyphPath =>
            "M 0 -0.55 L 0 -0.32 M 0 0.32 L 0 0.55 M -0.55 0 L -0.32 0 M 0.32 0 L 0.55 0 "
            + "M -0.16 0.06 L -0.04 -0.1 L 0.06 0.02 L 0.18 -0.12";

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 10).
                AddIngredient(ItemID.Sapphire, 2).
                AddIngredient(CWRID.Item_DubiousPlating, 3).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 10).
                AddIngredient(ItemID.Sapphire, 2).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>双倍装载:每次产出有 25% 概率翻倍</summary>
    internal class DoubleLoadModule : BaseMiningModule
    {
        public override float DoubleDropChance => 0.25f;
        internal override Color Accent => new(240, 200, 110);

        //两只错叠的矿箱
        protected override string GlyphPath =>
            "M -0.46 -0.34 L 0.06 -0.34 L 0.06 0.12 L -0.46 0.12 Z "
            + "M -0.06 -0.12 L 0.46 -0.12 L 0.46 0.34 L -0.06 0.34 Z";

        protected override void SetModuleDefaults() {
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 2);
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.MythrilBarGroup, 10).
                AddIngredient(ItemID.SoulofMight, 5).
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 5).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.MythrilBarGroup, 10).
                AddIngredient(ItemID.SoulofMight, 5).
                AddTile(TileID.MythrilAnvil).
                Register();
            }
        }
    }

    /// <summary>过载核心:周期 ×0.55,能耗 ×1.6——烧电换转速</summary>
    internal class OverdriveCoreModule : BaseMiningModule
    {
        public override float WorkIntervalMult => 0.55f;
        public override float EnergyCostMult => 1.6f;
        internal override Color Accent => new(240, 100, 70);

        //四芒核心 + 四点飞溅
        protected override string GlyphPath =>
            "M 0 -0.5 L 0.13 -0.13 L 0.5 0 L 0.13 0.13 L 0 0.5 L -0.13 0.13 L -0.5 0 L -0.13 -0.13 Z "
            + "M 0.38 -0.38 M -0.38 -0.38 M 0.38 0.38 M -0.38 0.38";

        protected override void SetModuleDefaults() {
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(ItemID.HellstoneBar, 8).
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 3).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.HellstoneBar, 10).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>节流阀:周期 ×1.5,能耗 ×0.45——慢工省电,挂机取舍</summary>
    internal class ThrottleValveModule : BaseMiningModule
    {
        public override float WorkIntervalMult => 1.5f;
        public override float EnergyCostMult => 0.45f;
        internal override Color Accent => new(150, 170, 190);

        //菱形阀体 + 十字阀杆
        protected override string GlyphPath =>
            "M 0 -0.5 L 0.5 0 L 0 0.5 L -0.5 0 Z "
            + "M 0 -0.5 L 0 0.5 M -0.5 0 L 0.5 0";

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.TinBarGroup, 12).
                AddIngredient(CWRID.Item_DubiousPlating, 3).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(CWRCrafted.TinBarGroup, 12).
                AddIngredient(ItemID.Chain, 5).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>集装组件:产出直接存入近旁的箱子等存储,存不下才落地</summary>
    internal class ChestLinkModule : BaseMiningModule
    {
        public override bool ChestDeposit => true;
        internal override Color Accent => new(200, 160, 90);

        //带盖矿箱 + 一支入箱箭头
        protected override string GlyphPath =>
            "M -0.42 -0.04 L 0.42 -0.04 L 0.42 0.5 L -0.42 0.5 Z "
            + "M -0.42 0.16 L 0.42 0.16 "
            + "M 0 -0.54 L 0 -0.16 M -0.16 -0.34 L 0 -0.14 L 0.16 -0.34";

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 10).
                AddIngredient(ItemID.Chest, 1).
                AddIngredient(CWRID.Item_MysteriousCircuitry, 3).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddRecipeGroup(RecipeGroupID.IronBar, 10).
                AddIngredient(ItemID.Chest, 1).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }

    /// <summary>现场熔炼:产出按原版配比熔成锭,能耗 ×1.4</summary>
    internal class SmelterModule : BaseMiningModule
    {
        public override bool SmeltOutput => true;
        public override float EnergyCostMult => 1.4f;
        internal override Color Accent => new(235, 130, 60);

        //炉身炉门 + 三点炉烟
        protected override string GlyphPath =>
            "M -0.4 0.52 L -0.4 -0.08 L -0.12 -0.32 L 0.12 -0.32 L 0.4 -0.08 L 0.4 0.52 Z "
            + "M -0.14 0.52 L -0.14 0.2 L 0.14 0.2 L 0.14 0.52 "
            + "M -0.06 -0.48 M 0.1 -0.56 M -0.2 -0.6";

        protected override void SetModuleDefaults() {
            Item.value = Item.sellPrice(gold: 1, silver: 50);
        }

        public override void AddRecipes() {
            if (CWRID.DubiousCircuitryAvailable) {
                CreateRecipe().
                AddIngredient(ItemID.Furnace, 1).
                AddIngredient(ItemID.HellstoneBar, 6).
                AddIngredient(CWRID.Item_DubiousPlating, 5).
                AddTile(TileID.Anvils).
                Register();
            }
            else {
                CreateRecipe().
                AddIngredient(ItemID.Furnace, 1).
                AddIngredient(ItemID.HellstoneBar, 6).
                AddTile(TileID.Anvils).
                Register();
            }
        }
    }
}
