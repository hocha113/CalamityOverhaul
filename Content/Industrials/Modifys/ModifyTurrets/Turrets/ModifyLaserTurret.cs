using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.PlayerTurrets;
using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyLaserTurret : BaseTurretItem
    {
        public override int TargetID => ModContent.ItemType<LaserTurret>();
    }

    internal class ModifyPlayerLaserTurret : BaseTurretTile
    {
        public override int TargetID => ModContent.TileType<PlayerLaserTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<LaserTurretByFriendTP>();
    }

    internal class ModifyPlayerLaserByHostileTurret : BaseTurretTile
    {
        public override int TargetID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostileLaserTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<LaserTurretByHostileTP>();
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
