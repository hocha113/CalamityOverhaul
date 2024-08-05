using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 铅宽剑
    /// </summary>
    internal class RMineralSword : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.LeadBroadsword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }

    /// <summary>
    /// 锡宽剑
    /// </summary>
    internal class RMineralSword2 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.TinBroadsword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }

    /// <summary>
    /// 钨宽剑
    /// </summary>
    internal class RMineralSword3 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.TungstenBroadsword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }

    /// <summary>
    /// 铂金宽剑
    /// </summary>
    internal class RMineralSword4 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.PlatinumBroadsword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }

    /// <summary>
    /// 金阔剑
    /// </summary>
    internal class RMineralSword5 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.GoldBroadsword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }

    /// <summary>
    /// 铁阔剑
    /// </summary>
    internal class RMineralSword6 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.IronBroadsword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }

    /// <summary>
    /// 铜阔剑
    /// </summary>
    internal class RMineralSword7 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.CopperBroadsword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }

    /// <summary>
    /// 银阔剑
    /// </summary>
    internal class RMineralSword8 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.SilverBroadsword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }

    /// <summary>
    /// 仙人掌剑
    /// </summary>
    internal class RMineralSword9 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.CactusSword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }
}
