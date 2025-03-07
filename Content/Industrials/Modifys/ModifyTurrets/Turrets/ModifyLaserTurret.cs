using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.PlayerTurrets;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.Tiles.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyLaserTurret : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<LaserTurret>();
        public override void SetDefaults(Item item) {
            item.CWR().StorageUE = true;
            item.CWR().ConsumeUseUE = 1000;
        }
    }

    internal class ModifyPlayerLaserTurret : TileOverride
    {
        public override int TargetID => ModContent.TileType<PlayerLaserTurret>();
        public override bool? CanDrop(int i, int j, int type) => false;
    }

    internal class LaserTurretByFriendTP : LaserTurretByHostileTP
    {
        public override int TargetTileID => ModContent.TileType<PlayerLaserTurret>();
        public override int TargetItem => ModContent.ItemType<LaserTurret>();
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class LaserTurretByHostileTP : BaseTurretTP, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostileLaserTurret>();//愚蠢的同名，让人只能路径标记
        public override string BodyPath => CWRConstant.Turrets + "LaserTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "LaserTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "LaserTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "LaserTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<HostileLaserTurret>();
        public override void SetTurret() {
            Damage = 24;
            ShootID = ModContent.ProjectileType<LaserShotBuffer>();
        }
    }
}
