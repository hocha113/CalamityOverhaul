﻿using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 铅宽剑
    /// </summary>
    internal class RMineralSword : CWRItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.LeadBroadsword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }

    /// <summary>
    /// 锡宽剑
    /// </summary>
    internal class RMineralSword2 : CWRItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.TinBroadsword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }

    /// <summary>
    /// 钨宽剑
    /// </summary>
    internal class RMineralSword3 : CWRItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.TungstenBroadsword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }

    /// <summary>
    /// 铂金宽剑
    /// </summary>
    internal class RMineralSword4 : CWRItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.PlatinumBroadsword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }

    /// <summary>
    /// 金阔剑
    /// </summary>
    internal class RMineralSword5 : CWRItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.GoldBroadsword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }

    /// <summary>
    /// 铁阔剑
    /// </summary>
    internal class RMineralSword6 : CWRItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.IronBroadsword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }

    /// <summary>
    /// 铜阔剑
    /// </summary>
    internal class RMineralSword7 : CWRItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.CopperBroadsword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }

    /// <summary>
    /// 银阔剑
    /// </summary>
    internal class RMineralSword8 : CWRItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.SilverBroadsword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }

    /// <summary>
    /// 仙人掌剑
    /// </summary>
    internal class RMineralSword9 : CWRItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.CactusSword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }
}
