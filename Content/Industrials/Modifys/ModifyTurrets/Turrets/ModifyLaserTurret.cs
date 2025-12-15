using InnoVault.TileProcessors;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyLaserTurret : BaseTurretItem
    {
        public override int TargetID => CWRID.Item_LaserTurret;
    }

    internal class ModifyPlayerLaserTurret : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_PlayerLaserTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<LaserTurretByFriendTP>();
    }

    internal class ModifyPlayerLaserByHostileTurret : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_HostileLaserTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<LaserTurretByHostileTP>();
    }

    internal class LaserTurretByFriendTP : LaserTurretByHostileTP
    {
        public override int TargetTileID => CWRID.Tile_PlayerLaserTurret;
        public override int TargetItem => CWRID.Item_LaserTurret;
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class LaserTurretByHostileTP : BaseTurretTP, ICWRLoader
    {
        public override int TargetTileID => CWRID.Tile_HostileLaserTurret;
        public override string BodyPath => CWRConstant.Turrets + "LaserTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "LaserTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "LaserTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "LaserTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => CWRID.Item_HostileLaserTurret;
        public override void SetTurret() {
            Damage = 24;
            ShootID = CWRID.Proj_LaserShotBuffer;
        }
    }
}
