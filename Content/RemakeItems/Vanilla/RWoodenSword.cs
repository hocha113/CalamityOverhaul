using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 木剑
    /// </summary>
    internal class RWoodenSword : ItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.WoodenSword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }
    /// <summary>
    /// 乌木剑
    /// </summary>
    internal class RWoodenSword2 : ItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.EbonwoodSword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }
    /// <summary>
    /// 红木剑
    /// </summary>
    internal class RWoodenSword3 : ItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.RichMahoganySword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }
    /// <summary>
    /// 珍珠木剑
    /// </summary>
    internal class RWoodenSword4 : ItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.PearlwoodSword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }
    /// <summary>
    /// 暗影木剑
    /// </summary>
    internal class RWoodenSword5 : ItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.ShadewoodSword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }
    /// <summary>
    /// 棕榈木剑
    /// </summary>
    internal class RWoodenSword6 : ItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.PalmWoodSword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }
    /// <summary>
    /// 针叶木剑
    /// </summary>
    internal class RWoodenSword7 : ItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.BorealWoodSword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }
    /// <summary>
    /// 灰烬木剑
    /// </summary>
    internal class RWoodenSword8 : ItemOverride
    {
        public override bool IsVanilla => true;
        public override int TargetID => ItemID.AshWoodSword;
        public override void SetDefaults(Item item) => EarlierSwordHeld.Set(item);
    }
}
