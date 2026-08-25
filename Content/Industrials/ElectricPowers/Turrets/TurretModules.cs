using CalamityOverhaul.Content.Industrials.MachineModules;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets
{
    //=========================================================================
    // 防御塔族共用升级模块:测距/装填/节能三枚,target=Turret 全族通用。
    // 消费点在 BaseTurretTP 的 Effective* 与各塔的 EffectiveConsumePerTick;
    // 光环塔(护盾/治疗)按光环半径消费射程域,没有开火节奏故射速域对其无效。
    // 图标沿用程序化钢牌语言,意象取火控实物(瞄具/弹匣/电容)
    //=========================================================================

    /// <summary>测距镜组:索敌半径×1.25(光环塔为光环半径)</summary>
    internal class TurretRangefinderModule : BaseMachineModule, ITurretModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.Turret;
        public float RangeMult => 1.25f;
        public float RateMult => 1f;
        public float EnergyMult => 1f;
        internal override Color Accent => new(150, 200, 225);

        //瞄具:外环 + 四向断开刻线 + 中心觇点
        protected override string GlyphPath =>
            "M 0 -0.34 Q 0.34 -0.34 0.34 0 Q 0.34 0.34 0 0.34 Q -0.34 0.34 -0.34 0 Q -0.34 -0.34 0 -0.34 "
            + "M 0 -0.48 L 0 -0.26 M 0 0.26 L 0 0.48 "
            + "M -0.48 0 L -0.26 0 M 0.26 0 L 0.48 0 "
            + "M -0.05 0 L 0.05 0";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 10).
            AddIngredient(ItemID.Lens, 2).
            AddIngredient(ItemID.Glass, 5).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>快速装填机构:开火/脉冲节拍×1.3(开火间隔缩短约23%)</summary>
    internal class TurretAutoloaderModule : BaseMachineModule, ITurretModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.Turret;
        public float RangeMult => 1f;
        public float RateMult => 1.3f;
        public float EnergyMult => 1f;
        internal override Color Accent => new(215, 190, 120);

        //斜置弹匣:匣体 + 双发弹横线 + 供弹唇短杆
        protected override string GlyphPath =>
            "M -0.20 -0.44 L 0.26 -0.34 L 0.14 0.44 L -0.32 0.34 Z "
            + "M -0.18 -0.14 L 0.18 -0.06 M -0.22 0.08 L 0.14 0.16 "
            + "M 0.26 -0.34 L 0.40 -0.44";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 12).
            AddIngredient(ItemID.Chain, 6).
            AddIngredient(ItemID.Wire, 10).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>火控节能模块:单发与持续耗电×0.75</summary>
    internal class TurretPowerSaverModule : BaseMachineModule, ITurretModule
    {
        public override MachineModuleTarget ModuleTargets => MachineModuleTarget.Turret;
        public float RangeMult => 1f;
        public float RateMult => 1f;
        public float EnergyMult => 0.75f;
        internal override Color Accent => new(150, 205, 140);

        //电容对板:双极板 + 两侧引线 + 右路接地三横
        protected override string GlyphPath =>
            "M -0.08 -0.34 L -0.08 0.16 M 0.08 -0.34 L 0.08 0.16 "
            + "M -0.44 -0.09 L -0.08 -0.09 M 0.08 -0.09 L 0.44 -0.09 "
            + "M 0.44 -0.09 L 0.44 0.26 "
            + "M 0.30 0.26 L 0.50 0.26 M 0.34 0.34 L 0.46 0.34 M 0.38 0.42 L 0.42 0.42";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 8).
            AddIngredient(ItemID.Wire, 15).
            AddIngredient(ItemID.FallenStar, 3).
            AddTile(TileID.Anvils).
            Register();
    }
}
