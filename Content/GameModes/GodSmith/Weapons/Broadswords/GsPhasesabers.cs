using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【光剑七色（Phasesaber 档）】材质：精制等离子束刃。族签名升级（GsPhaseBladeShared 共享层）：
    /// ①充能上限更高、刃长延展更远②未满充能的终结拍也甩小光刃延展斩
    /// ③满充能过载光弧更大，命中再炸开等离子爆裂。七色 = 色板 + 音高的次要变奏，机制统一
    /// </summary>

    //==================== 蓝 ====================

    /// <summary>光剑蓝：低鸣沉稳的冷蓝精制束刃</summary>
    internal class GsBluePhasesaber : GsPhasesaberSchemeCore
    {
        public override int TargetItemID => ItemID.BluePhasesaber;
        protected override int HeldProjID => ModContent.ProjectileType<GsBluePhasesaberHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Blue;
    }

    internal class GsBluePhasesaberHeld : GsPhasesaberHeldCore
    {
        protected override int SwordItemID => ItemID.BluePhasesaber;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Blue;
    }

    //==================== 红 ====================

    /// <summary>光剑红：音色最沉的炽红精制束刃</summary>
    internal class GsRedPhasesaber : GsPhasesaberSchemeCore
    {
        public override int TargetItemID => ItemID.RedPhasesaber;
        protected override int HeldProjID => ModContent.ProjectileType<GsRedPhasesaberHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Red;
    }

    internal class GsRedPhasesaberHeld : GsPhasesaberHeldCore
    {
        protected override int SwordItemID => ItemID.RedPhasesaber;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Red;
    }

    //==================== 绿 ====================

    /// <summary>光剑绿：中正基准音的翠绿精制束刃</summary>
    internal class GsGreenPhasesaber : GsPhasesaberSchemeCore
    {
        public override int TargetItemID => ItemID.GreenPhasesaber;
        protected override int HeldProjID => ModContent.ProjectileType<GsGreenPhasesaberHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Green;
    }

    internal class GsGreenPhasesaberHeld : GsPhasesaberHeldCore
    {
        protected override int SwordItemID => ItemID.GreenPhasesaber;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Green;
    }

    //==================== 紫 ====================

    /// <summary>光剑紫：音色最深的暗紫精制束刃</summary>
    internal class GsPurplePhasesaber : GsPhasesaberSchemeCore
    {
        public override int TargetItemID => ItemID.PurplePhasesaber;
        protected override int HeldProjID => ModContent.ProjectileType<GsPurplePhasesaberHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Purple;
    }

    internal class GsPurplePhasesaberHeld : GsPhasesaberHeldCore
    {
        protected override int SwordItemID => ItemID.PurplePhasesaber;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Purple;
    }

    //==================== 白 ====================

    /// <summary>光剑白：音色最亮的纯白精制束刃</summary>
    internal class GsWhitePhasesaber : GsPhasesaberSchemeCore
    {
        public override int TargetItemID => ItemID.WhitePhasesaber;
        protected override int HeldProjID => ModContent.ProjectileType<GsWhitePhasesaberHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.White;
    }

    internal class GsWhitePhasesaberHeld : GsPhasesaberHeldCore
    {
        protected override int SwordItemID => ItemID.WhitePhasesaber;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.White;
    }

    //==================== 黄 ====================

    /// <summary>光剑黄：音色偏亮的鎏金精制束刃</summary>
    internal class GsYellowPhasesaber : GsPhasesaberSchemeCore
    {
        public override int TargetItemID => ItemID.YellowPhasesaber;
        protected override int HeldProjID => ModContent.ProjectileType<GsYellowPhasesaberHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Yellow;
    }

    internal class GsYellowPhasesaberHeld : GsPhasesaberHeldCore
    {
        protected override int SwordItemID => ItemID.YellowPhasesaber;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Yellow;
    }

    //==================== 橙 ====================

    /// <summary>光剑橙：微暖音色的橙焰精制束刃</summary>
    internal class GsOrangePhasesaber : GsPhasesaberSchemeCore
    {
        public override int TargetItemID => ItemID.OrangePhasesaber;
        protected override int HeldProjID => ModContent.ProjectileType<GsOrangePhasesaberHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Orange;
    }

    internal class GsOrangePhasesaberHeld : GsPhasesaberHeldCore
    {
        protected override int SwordItemID => ItemID.OrangePhasesaber;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Orange;
    }
}
