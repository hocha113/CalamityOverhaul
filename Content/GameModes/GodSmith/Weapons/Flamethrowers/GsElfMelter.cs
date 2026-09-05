using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flamethrowers
{
    /// <summary>
    /// 融雪精灵喷火器重铸（L3 手持接管）：喷射器框架的圣诞版。<br/>
    /// [融雪扇焰] 宽锥焰流，贴地留残焰补丁；气压呼吸节奏同火焰喷射器
    /// </summary>
    internal class GsElfMelter : GodSmithScheme
    {
        public override int TargetItemID => ItemID.ElfMelter;

        public override string GsFamily => "Flamethrowers";

        protected override string GsDescFallback =>
            "Reforged: wide red-green flame laced with thaw mist\nPressure rules still apply: long sprays shorten the flame";
        public override bool? GsCanUseItem(Item item, Player player) {
            if (HeldAlive<GsElfMelterHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsElfMelterHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            return false;
        }
    }

    /// <summary>
    /// 融雪精灵喷火器手持弹幕：喷射器基类原样
    /// </summary>
    internal class GsElfMelterHeld : GsFlamerHeldBase
    {
        protected override int HeldTargetItemID => ItemID.ElfMelter;
    }
}
