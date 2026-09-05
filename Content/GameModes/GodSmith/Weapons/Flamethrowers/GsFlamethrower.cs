using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flamethrowers
{
    /// <summary>
    /// 火焰喷射器重铸（L3 手持接管）：持续焰流。<br/>
    /// [扇焰] 宽锥 26° 短程，贴地留 2 秒残焰补丁。<br/>
    /// 持续压喷 3 秒「气压」渐满，焰程缩至 60%，松手回压，喷吐有呼吸节奏。
    /// 凝胶按原版节拍逐发消耗（held 内 PickAmmo，1:1）
    /// </summary>
    internal class GsFlamethrower : GodSmithScheme
    {
        public override int TargetItemID => ItemID.Flamethrower;

        public override string GsFamily => "Flamethrowers";

        protected override string GsDescFallback =>
            "Reforged: a wide cone that leaves burning ground\nSustained spraying drains pressure and shortens the flame; ease off to recover";
        public override bool? GsCanUseItem(Item item, Player player) {
            if (HeldAlive<GsFlamethrowerHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsFlamethrowerHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            return false;
        }
    }

    /// <summary>
    /// 火焰喷射器手持弹幕：焰流生成与凝胶消耗全部自管，姿态手写
    /// </summary>
    internal class GsFlamethrowerHeld : GsFlamerHeldBase
    {
        protected override int HeldTargetItemID => ItemID.Flamethrower;
    }
}
