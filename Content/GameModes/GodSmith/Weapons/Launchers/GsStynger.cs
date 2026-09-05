using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 毒刺发射器重铸：原版触发引信（碰撞爆）保留，出手带后坐冲量。
    /// Stynger Bolt 专属弹药与弹片体系原样保留
    /// </summary>
    internal class GsStynger : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.Stynger;

        protected override string GsDescFallback =>
            "Reforged: detonates on hit, with a kick on the shot. Stynger bolts and shrapnel stay as they were";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchRecoil(player, velocity, 1.0f);
            return null;
        }
    }
}
