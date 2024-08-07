﻿using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles.Vanilla;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 木剑
    /// </summary>
    internal class RWoodenSword : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.WoodenSword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }
    /// <summary>
    /// 乌木剑
    /// </summary>
    internal class RWoodenSword2 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.EbonwoodSword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }
    /// <summary>
    /// 红木剑
    /// </summary>
    internal class RWoodenSword3 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.RichMahoganySword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }
    /// <summary>
    /// 珍珠木剑
    /// </summary>
    internal class RWoodenSword4 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.PearlwoodSword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }
    /// <summary>
    /// 暗影木剑
    /// </summary>
    internal class RWoodenSword5 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.ShadewoodSword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }
    /// <summary>
    /// 棕榈木剑
    /// </summary>
    internal class RWoodenSword6 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.PalmWoodSword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }
    /// <summary>
    /// 针叶木剑
    /// </summary>
    internal class RWoodenSword7 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.BorealWoodSword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }
    /// <summary>
    /// 灰烬木剑
    /// </summary>
    internal class RWoodenSword8 : BaseRItem
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.AshWoodSword;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<EarlierSwordHeld>();
    }
}
