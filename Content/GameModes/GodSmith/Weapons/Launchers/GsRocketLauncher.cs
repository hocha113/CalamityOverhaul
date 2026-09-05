using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 火箭筒重铸：左键原版直射（直击 ×2 保留），出手带后坐冲量
    /// </summary>
    internal class GsRocketLauncher : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.RocketLauncher;

        protected override string GsDescFallback =>
            "Reforged: shots kick you back, and direct hits hit harder";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchRecoil(player, velocity, 1.5f);
            return null;
        }
    }
}
