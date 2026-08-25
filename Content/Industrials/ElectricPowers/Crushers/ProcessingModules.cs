using CalamityOverhaul.Content.Industrials.MachineModules;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers
{
    //=========================================================================
    // 加工链(粉碎机/回收机)共用物流模块:进料斗与出料槽。
    // 既有的 AutoFeedHopperModule/AutoEjectChuteModule 面向热力/焚化炉,
    // targets 不含新机器,故加工链自带一对,图标沿用程序化钢牌语言
    //=========================================================================

    /// <summary>加工进料斗:粉碎机/回收机从近旁存储自动补料</summary>
    internal class ProcessingFeedHopper : BaseMachineModule, ILogisticsModule
    {
        public override MachineModuleTarget ModuleTargets
            => MachineModuleTarget.Crusher | MachineModuleTarget.Recycler;
        public bool AutoFeed => true;
        public bool AutoEject => false;
        internal override Color Accent => new(200, 175, 120);

        //宽口漏斗:双层锥斗 + 落料点,与热力进料斗的单锥区分
        protected override string GlyphPath =>
            "M -0.46 -0.42 L 0.46 -0.42 L 0.14 0.02 L 0.14 0.26 L -0.14 0.26 L -0.14 0.02 Z "
            + "M -0.30 -0.42 L -0.06 -0.10 M 0.30 -0.42 L 0.06 -0.10 "
            + "M 0 0.34 L 0 0.46";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 12).
            AddIngredient(ItemID.Chest, 1).
            AddIngredient(ItemID.Chain, 4).
            AddTile(TileID.Anvils).
            Register();
    }

    /// <summary>加工出料槽:粉碎机/回收机产物直接送进近旁存储</summary>
    internal class ProcessingEjectChute : BaseMachineModule, ILogisticsModule
    {
        public override MachineModuleTarget ModuleTargets
            => MachineModuleTarget.Crusher | MachineModuleTarget.Recycler;
        public bool AutoFeed => false;
        public bool AutoEject => true;
        internal override Color Accent => new(170, 195, 150);

        //出料辊道:出料箱 + 斜辊三点 + 落料点
        protected override string GlyphPath =>
            "M -0.46 -0.40 L -0.10 -0.40 L -0.10 -0.14 L -0.46 -0.14 Z "
            + "M -0.06 -0.06 L 0.38 0.22 "
            + "M -0.02 0.06 L 0.02 0.10 M 0.12 0.14 L 0.16 0.18 M 0.26 0.22 L 0.30 0.26 "
            + "M 0.38 0.34 L 0.38 0.46";

        public override void AddRecipes() => CreateRecipe().
            AddRecipeGroup(RecipeGroupID.IronBar, 12).
            AddIngredient(ItemID.Chest, 1).
            AddIngredient(ItemID.Chain, 4).
            AddTile(TileID.Anvils).
            Register();
    }
}
