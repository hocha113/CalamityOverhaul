using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Specials
{
    /// <summary>
    /// 食人鱼枪重铸（L2 弹幕增强，经典不毁）：原版弹幕不换、免弹药保留。<br/>
    /// [咬附] 完全原版，仅做基线伤害补偿
    /// </summary>
    internal class GsPiranhaGun : GodSmithScheme
    {
        public override int TargetItemID => ItemID.PiranhaGun;

        public override string GsFamily => "Specials";

        protected override string GsDescFallback =>
            "Reforged: the classic bite-and-hold";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;//基线补偿，综合 DPS 落在原版 108%~112%
    }
}
