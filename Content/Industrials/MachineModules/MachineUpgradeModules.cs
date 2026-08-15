using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.MachineModules
{
    //=========================================================================
    // 机器升级模块族:热力 4 + 进料斗(热力|焚化炉) + 风力 3 + 水力 3 + 焚化炉 4 + 通用 1。
    // 图标一律程序化钢牌纹样,意象取工业实物(风箱/阀轮/蛇管/桁架/发条…),
    // 避开 chevron/箭头这类与其他模组撞脸的通用符号
    //=========================================================================

    #region 热力发电机
    /// <summary>保温层:散热×0.55,炉温更持久</summary>
    internal class InsulationLiningModule : BaseMachineModule, IThermalModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.ThermalGenerator;
        public float BurnRateMult => 1f;
        public float HeatYieldMult => 1f;
        public float DissipationMult => 0.55f;
        internal override Color Accent => new(200, 150, 110);

        //夹层壁:双层壁板 + 层间斜纹填充
        protected override string GlyphPath =>
            "M -0.40 -0.42 L -0.40 0.42 M -0.20 -0.42 L -0.20 0.42 "
            + "M 0.20 -0.42 L 0.20 0.42 M 0.40 -0.42 L 0.40 0.42 "
            + "M -0.40 -0.26 L -0.20 -0.06 M -0.40 0.02 L -0.20 0.22 "
            + "M 0.20 -0.06 L 0.40 -0.26 M 0.20 0.22 L 0.40 0.02";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 8).
            AddIngredient(ItemID.Silk, 5).
            AddIngredient(ItemID.StoneBlock, 20).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>强制鼓风:燃速×1.45,火更旺(单份燃料总热量不变,烧得更快)</summary>
    internal class ForcedDraftModule : BaseMachineModule, IThermalModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.ThermalGenerator;
        public float BurnRateMult => 1.45f;
        public float HeatYieldMult => 1f;
        public float DissipationMult => 1f;
        internal override Color Accent => new(235, 120, 60);

        //风箱:折页箱体 + 风嘴 + 出风纹
        protected override string GlyphPath =>
            "M -0.46 -0.26 L -0.08 -0.26 L -0.08 0.26 L -0.46 0.26 Z "
            + "M -0.34 -0.26 L -0.34 0.26 M -0.21 -0.26 L -0.21 0.26 "
            + "M -0.08 0 L 0.18 0 "
            + "M 0.22 -0.12 L 0.36 0 L 0.22 0.12 "
            + "M 0.32 -0.12 L 0.46 0 L 0.32 0.12";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 10).
            AddIngredient(ItemID.Chain, 4).
            AddIngredient(ItemID.Feather, 3).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>缓燃阀:燃速×0.6,细水长流(单份燃料烧得更久)</summary>
    internal class SlowBurnValveModule : BaseMachineModule, IThermalModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.ThermalGenerator;
        public float BurnRateMult => 0.6f;
        public float HeatYieldMult => 1f;
        public float DissipationMult => 1f;
        internal override Color Accent => new(190, 150, 90);

        //细口阀:缩腰管路 + 阀杆手轮
        protected override string GlyphPath =>
            "M -0.46 0.18 L -0.10 0.18 L -0.04 0.08 L 0.04 0.08 L 0.10 0.18 L 0.46 0.18 "
            + "M -0.46 -0.02 L -0.10 -0.02 M 0.10 -0.02 L 0.46 -0.02 "
            + "M 0 0.08 L 0 -0.18 "
            + "M -0.16 -0.26 L 0.16 -0.26 "
            + "M -0.16 -0.26 L 0 -0.18 L 0.16 -0.26";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 8).
            AddIngredient(ItemID.Chain, 5).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>余热回收:燃料产热×1.25</summary>
    internal class HeatRecoveryModule : BaseMachineModule, IThermalModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.ThermalGenerator;
        public float BurnRateMult => 1f;
        public float HeatYieldMult => 1.25f;
        public float DissipationMult => 1f;
        internal override Color Accent => new(215, 120, 80);

        //回形蛇管:烟气走 S 形多吸一道热
        protected override string GlyphPath =>
            "M -0.42 0.40 L -0.42 -0.28 L -0.14 -0.28 L -0.14 0.28 "
            + "L 0.14 0.28 L 0.14 -0.28 L 0.42 -0.28 L 0.42 0.40";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 10).
            AddIngredient(ItemID.HellstoneBar, 4).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>自动进料斗:从近旁存储自动补燃料/可焚物(热力与焚化炉通用)</summary>
    internal class AutoFeedHopperModule : BaseMachineModule, ILogisticsModule
    {
        public override MachineModuleTarget ModuleTargets
            => MachineModuleTarget.ThermalGenerator | MachineModuleTarget.Incinerator;
        public bool AutoFeed => true;
        public bool AutoEject => false;
        internal override Color Accent => new(205, 170, 105);

        //漏斗:锥斗 + 落料口 + 落料点
        protected override string GlyphPath =>
            "M -0.42 -0.38 L 0.42 -0.38 L 0.12 0.06 L 0.12 0.30 L -0.12 0.30 L -0.12 0.06 Z "
            + "M -0.26 -0.38 L -0.04 -0.06 "
            + "M 0 0.38 L 0 0.50";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 12).
            AddIngredient(ItemID.Chest, 1).
            AddIngredient(ItemID.Chain, 4).
            AddTile(TileID.Anvils).
            Register();
    }
    #endregion

    #region 风力发电机
    /// <summary>加长叶片:输出×1.3</summary>
    internal class ExtendedBladesModule : BaseMachineModule, IGeneratorModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.WindGenerator;
        public float OutputMult => 1.3f;
        public float ConditionFloor => 0f;
        public float SpinUpMult => 1f;
        internal override Color Accent => new(170, 205, 220);

        //三叶桨:轮毂放射三片曲叶
        protected override string GlyphPath =>
            "M 0 0 L -0.10 -0.50 Q 0.06 -0.40 0.02 -0.10 Z "
            + "M 0 0 L 0.48 0.16 Q 0.32 0.30 0.06 0.10 Z "
            + "M 0 0 L -0.38 0.34 Q -0.38 0.12 -0.08 0.02 Z";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 8).
            AddRecipeGroup(RecipeGroupID.Wood, 20).
            AddIngredient(ItemID.Silk, 3).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>低风齿轮箱:风速倍率下限 0.8→1.3,微风也能稳定出力</summary>
    internal class LowWindGearboxModule : BaseMachineModule, IGeneratorModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.WindGenerator;
        public float OutputMult => 1f;
        public float ConditionFloor => 1.3f;
        public float SpinUpMult => 1f;
        internal override Color Accent => new(150, 170, 205);

        //双齿轮:大小轮咬合 + 轮齿刻
        protected override string GlyphPath =>
            "M -0.12 -0.28 Q 0.12 -0.28 0.12 -0.04 Q 0.12 0.20 -0.12 0.20 Q -0.36 0.20 -0.36 -0.04 Q -0.36 -0.28 -0.12 -0.28 "
            + "M 0.26 0.08 Q 0.40 0.08 0.40 0.22 Q 0.40 0.36 0.26 0.36 Q 0.12 0.36 0.12 0.22 Q 0.12 0.08 0.26 0.08 "
            + "M -0.12 -0.28 L -0.12 -0.38 M 0.12 -0.04 L 0.22 -0.04 M -0.36 -0.04 L -0.46 -0.04 M -0.12 0.20 L -0.12 0.30 "
            + "M 0.26 0.08 L 0.26 0.00 M 0.40 0.22 L 0.48 0.22 "
            + "M -0.12 -0.08 L -0.08 -0.04 L -0.12 0 L -0.16 -0.04 Z";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 12).
            AddIngredient(ItemID.Chain, 6).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>高空塔架:输出×1.15,可与叶片叠乘</summary>
    internal class TallTowerModule : BaseMachineModule, IGeneratorModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.WindGenerator;
        public float OutputMult => 1.15f;
        public float ConditionFloor => 0f;
        public float SpinUpMult => 1f;
        internal override Color Accent => new(170, 175, 185);

        //桁架塔:收分塔身 + 横杆 + 交叉斜撑
        protected override string GlyphPath =>
            "M -0.26 0.46 L -0.08 -0.46 L 0.08 -0.46 L 0.26 0.46 "
            + "M -0.22 0.26 L 0.22 0.26 M -0.17 0.02 L 0.17 0.02 M -0.12 -0.22 L 0.12 -0.22 "
            + "M -0.22 0.26 L 0.17 0.02 M 0.22 0.26 L -0.17 0.02 "
            + "M -0.17 0.02 L 0.12 -0.22 M 0.17 0.02 L -0.12 -0.22";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 15).
            AddRecipeGroup(RecipeGroupID.Wood, 30).
            AddTile(TileID.Anvils).
            Register();
    }
    #endregion

    #region 水力发电机
    /// <summary>导流罩:输出×1.3</summary>
    internal class FlowShroudModule : BaseMachineModule, IGeneratorModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.HydroGenerator;
        public float OutputMult => 1.3f;
        public float ConditionFloor => 0f;
        public float SpinUpMult => 1f;
        internal override Color Accent => new(110, 190, 205);

        //喇叭口:宽口收束成直管 + 管内流向
        protected override string GlyphPath =>
            "M -0.46 -0.30 Q -0.10 -0.16 0.10 -0.16 M 0.10 -0.16 L 0.46 -0.16 "
            + "M -0.46 0.30 Q -0.10 0.16 0.10 0.16 M 0.10 0.16 L 0.46 0.16 "
            + "M 0.16 -0.05 L 0.28 0 L 0.16 0.05 "
            + "M 0.30 -0.05 L 0.42 0 L 0.30 0.05";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 10).
            AddIngredient(ItemID.Coral, 3).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>快速起转:转速爬升×3,放下水就很快到工作转速</summary>
    internal class QuickSpinModule : BaseMachineModule, IGeneratorModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.HydroGenerator;
        public float OutputMult => 1f;
        public float ConditionFloor => 0f;
        public float SpinUpMult => 3f;
        internal override Color Accent => new(225, 185, 95);

        //发条:蜗簧螺旋 + 外端锚
        protected override string GlyphPath =>
            "M 0 0 Q 0.16 -0.02 0.14 0.12 Q 0.10 0.26 -0.08 0.22 "
            + "Q -0.28 0.16 -0.24 -0.06 Q -0.18 -0.30 0.06 -0.28 "
            + "Q 0.34 -0.24 0.36 0.02 Q 0.34 0.30 0.10 0.38 "
            + "M 0.10 0.38 L 0.22 0.46";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 8).
            AddIngredient(ItemID.Chain, 4).
            AddIngredient(ItemID.FallenStar, 2).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>深水轴承:输出×1.15,可与导流罩叠乘</summary>
    internal class DeepBearingModule : BaseMachineModule, IGeneratorModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.HydroGenerator;
        public float OutputMult => 1.15f;
        public float ConditionFloor => 0f;
        public float SpinUpMult => 1f;
        internal override Color Accent => new(95, 150, 170);

        //同心环:内外圈 + 四粒滚珠
        protected override string GlyphPath =>
            "M 0 -0.34 Q 0.34 -0.34 0.34 0 Q 0.34 0.34 0 0.34 Q -0.34 0.34 -0.34 0 Q -0.34 -0.34 0 -0.34 "
            + "M 0 -0.14 Q 0.14 -0.14 0.14 0 Q 0.14 0.14 0 0.14 Q -0.14 0.14 -0.14 0 Q -0.14 -0.14 0 -0.14 "
            + "M 0 -0.25 L 0 -0.22 M 0.25 0 L 0.22 0 M 0 0.25 L 0 0.22 M -0.25 0 L -0.22 0";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 12).
            AddIngredient(ItemID.Glass, 4).
            AddTile(TileID.Anvils).
            Register();
    }
    #endregion

    #region 焚化炉
    /// <summary>高温电极:熔速×1.45</summary>
    internal class HighTempElectrodeModule : BaseMachineModule, IIncineratorModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.Incinerator;
        public float SmeltSpeedMult => 1.45f;
        public float SmeltEnergyMult => 1f;
        public float DoubleOutputChance => 0f;
        internal override Color Accent => new(255, 150, 60);

        //对针电弧:双电极垂针 + 极间弧光折线 + 坩埚沿
        protected override string GlyphPath =>
            "M -0.34 -0.46 L -0.34 -0.10 M -0.42 -0.46 L -0.26 -0.46 "
            + "M 0.34 -0.46 L 0.34 -0.10 M 0.26 -0.46 L 0.42 -0.46 "
            + "M -0.34 -0.10 L -0.14 0.02 L 0.02 -0.14 L 0.16 0 L 0.34 -0.10 "
            + "M -0.30 0.16 L -0.24 0.30 L 0.24 0.30 L 0.30 0.16";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 8).
            AddIngredient(ItemID.HellstoneBar, 6).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>节能线圈:熔炼能耗×0.6</summary>
    internal class EconomizerCoilModule : BaseMachineModule, IIncineratorModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.Incinerator;
        public float SmeltSpeedMult => 1f;
        public float SmeltEnergyMult => 0.6f;
        public float DoubleOutputChance => 0f;
        internal override Color Accent => new(140, 200, 140);

        //绕组:铁芯横杆 + 两匝拱线 + 两端引脚
        protected override string GlyphPath =>
            "M -0.44 0.18 L 0.44 0.18 "
            + "M -0.36 0.18 Q -0.36 -0.16 -0.18 -0.16 Q 0 -0.16 0 0.18 "
            + "M 0 0.18 Q 0 -0.16 0.18 -0.16 Q 0.36 -0.16 0.36 0.18 "
            + "M -0.44 0.18 L -0.44 0.34 M 0.44 0.18 L 0.44 0.34";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 8).
            AddIngredient(ItemID.Wire, 20).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>双联坩埚:25% 概率双倍产出</summary>
    internal class TwinCrucibleModule : BaseMachineModule, IIncineratorModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.Incinerator;
        public float SmeltSpeedMult => 1f;
        public float SmeltEnergyMult => 1f;
        public float DoubleOutputChance => 0.25f;
        internal override Color Accent => new(240, 180, 90);

        //双杯:共沿双坩埚 + 双液面
        protected override string GlyphPath =>
            "M -0.46 -0.16 L 0.46 -0.16 "
            + "M -0.42 -0.16 L -0.34 0.20 L -0.10 0.20 L -0.02 -0.16 "
            + "M 0.02 -0.16 L 0.10 0.20 L 0.34 0.20 L 0.42 -0.16 "
            + "M -0.32 -0.02 L -0.12 -0.02 M 0.12 -0.02 L 0.32 -0.02";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 10).
            AddIngredient(ItemID.Obsidian, 10).
            AddIngredient(ItemID.ClayBlock, 10).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>自动出料口:熔炼产物直接送进近旁存储</summary>
    internal class AutoEjectChuteModule : BaseMachineModule, ILogisticsModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.Incinerator;
        public bool AutoFeed => false;
        public bool AutoEject => true;
        internal override Color Accent => new(205, 170, 105);

        //溜槽:出料箱 + 斜槽双线 + 落料点
        protected override string GlyphPath =>
            "M -0.46 -0.38 L -0.10 -0.38 L -0.10 -0.14 L -0.46 -0.14 Z "
            + "M -0.10 -0.26 L 0.34 0.10 M -0.22 -0.14 L 0.24 0.22 "
            + "M 0.34 0.10 L 0.24 0.22 "
            + "M 0.32 0.32 L 0.32 0.38 M 0.40 0.42 L 0.40 0.48";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 12).
            AddIngredient(ItemID.Chest, 1).
            AddIngredient(ItemID.Chain, 4).
            AddTile(TileID.Anvils).
            Register();
    }
    #endregion

    #region 通用
    /// <summary>扩容电池:储能上限×1.5,五族机器通用</summary>
    internal class CapacityCellModule : BaseMachineModule, IStorageModule
    {
        public override MachineModuleTarget ModuleTargets
            => MachineModuleTarget.MiningMachine | MachineModuleTarget.ThermalGenerator
            | MachineModuleTarget.WindGenerator | MachineModuleTarget.HydroGenerator
            | MachineModuleTarget.Incinerator;
        public float CapacityMult => 1.5f;
        internal override Color Accent => new(240, 210, 110);

        //极板:电池壳 + 双极柱 + 三层极板
        protected override string GlyphPath =>
            "M -0.34 -0.30 L -0.34 0.38 L 0.34 0.38 L 0.34 -0.30 Z "
            + "M -0.14 -0.30 L -0.14 -0.42 M 0.14 -0.30 L 0.14 -0.42 "
            + "M -0.20 -0.42 L -0.08 -0.42 M 0.08 -0.42 L 0.20 -0.42 "
            + "M -0.20 -0.12 L 0.20 -0.12 M -0.20 0.04 L 0.20 0.04 M -0.20 0.20 L 0.20 0.20";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 10).
            AddIngredient(ItemID.Wire, 10).
            AddIngredient(ItemID.FallenStar, 5).
            AddTile(TileID.Anvils).
            Register();
    }
    #endregion
}
