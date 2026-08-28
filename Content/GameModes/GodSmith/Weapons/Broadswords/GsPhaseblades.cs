using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【相位刃七色】材质：等离子束刃。族签名（GsPhaseBladeShared 共享层）：
    /// ①命中积攒充能，刃身随充能延展变亮、嗡鸣升调②满充能后下一次终结拍甩出等离子光弧。
    /// 七色 = 色板 + 音高的次要变奏，机制统一
    /// </summary>

    //==================== 蓝 ====================

    /// <summary>相位蓝：低鸣沉稳的冷蓝束刃</summary>
    internal class GsBluePhaseblade : GsPhasebladeSchemeCore
    {
        public override int TargetItemID => ItemID.BluePhaseblade;
        protected override int HeldProjID => ModContent.ProjectileType<GsBluePhasebladeHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Blue;
    }

    internal class GsBluePhasebladeHeld : GsPhasebladeHeldCore
    {
        protected override int SwordItemID => ItemID.BluePhaseblade;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Blue;
    }

    //==================== 红 ====================

    /// <summary>相位红：音色最沉的炽红束刃</summary>
    internal class GsRedPhaseblade : GsPhasebladeSchemeCore
    {
        public override int TargetItemID => ItemID.RedPhaseblade;
        protected override int HeldProjID => ModContent.ProjectileType<GsRedPhasebladeHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Red;
    }

    internal class GsRedPhasebladeHeld : GsPhasebladeHeldCore
    {
        protected override int SwordItemID => ItemID.RedPhaseblade;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Red;
    }

    //==================== 绿 ====================

    /// <summary>相位绿：中正基准音的翠绿束刃</summary>
    internal class GsGreenPhaseblade : GsPhasebladeSchemeCore
    {
        public override int TargetItemID => ItemID.GreenPhaseblade;
        protected override int HeldProjID => ModContent.ProjectileType<GsGreenPhasebladeHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Green;
    }

    internal class GsGreenPhasebladeHeld : GsPhasebladeHeldCore
    {
        protected override int SwordItemID => ItemID.GreenPhaseblade;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Green;
    }

    //==================== 紫 ====================

    /// <summary>相位紫：音色最深的暗紫束刃</summary>
    internal class GsPurplePhaseblade : GsPhasebladeSchemeCore
    {
        public override int TargetItemID => ItemID.PurplePhaseblade;
        protected override int HeldProjID => ModContent.ProjectileType<GsPurplePhasebladeHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Purple;
    }

    internal class GsPurplePhasebladeHeld : GsPhasebladeHeldCore
    {
        protected override int SwordItemID => ItemID.PurplePhaseblade;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Purple;
    }

    //==================== 白 ====================

    /// <summary>相位白：音色最亮的纯白束刃</summary>
    internal class GsWhitePhaseblade : GsPhasebladeSchemeCore
    {
        public override int TargetItemID => ItemID.WhitePhaseblade;
        protected override int HeldProjID => ModContent.ProjectileType<GsWhitePhasebladeHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.White;
    }

    internal class GsWhitePhasebladeHeld : GsPhasebladeHeldCore
    {
        protected override int SwordItemID => ItemID.WhitePhaseblade;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.White;
    }

    //==================== 黄 ====================

    /// <summary>相位黄：音色偏亮的鎏金束刃</summary>
    internal class GsYellowPhaseblade : GsPhasebladeSchemeCore
    {
        public override int TargetItemID => ItemID.YellowPhaseblade;
        protected override int HeldProjID => ModContent.ProjectileType<GsYellowPhasebladeHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Yellow;
    }

    internal class GsYellowPhasebladeHeld : GsPhasebladeHeldCore
    {
        protected override int SwordItemID => ItemID.YellowPhaseblade;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Yellow;
    }

    //==================== 橙 ====================

    /// <summary>相位橙：微暖音色的橙焰束刃</summary>
    internal class GsOrangePhaseblade : GsPhasebladeSchemeCore
    {
        public override int TargetItemID => ItemID.OrangePhaseblade;
        protected override int HeldProjID => ModContent.ProjectileType<GsOrangePhasebladeHeld>();
        internal override GsPhasebladePalette Palette => GsPhasebladePalette.Orange;
    }

    internal class GsOrangePhasebladeHeld : GsPhasebladeHeldCore
    {
        protected override int SwordItemID => ItemID.OrangePhaseblade;
        protected override GsPhasebladePalette Palette => GsPhasebladePalette.Orange;
    }
}
