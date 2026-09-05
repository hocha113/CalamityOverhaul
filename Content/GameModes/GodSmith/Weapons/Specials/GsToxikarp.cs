using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Specials
{
    /// <summary>
    /// 毒鳔枪重铸（L2 弹幕增强）：免弹药保留，原版毒泡不换载体。<br/>
    /// [毒泡流] 原版速射，仅做基线伤害补偿
    /// </summary>
    internal class GsToxikarp : GodSmithScheme
    {
        public override int TargetItemID => ItemID.Toxikarp;

        public override string GsFamily => "Specials";

        protected override string GsDescFallback =>
            "Reforged: the classic rapid-fire bubbles\nNo ammo, as always";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//基线补偿，综合 DPS 落在原版 108%~112%
    }
}
