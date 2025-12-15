using InnoVault.TileProcessors;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyFireTurret : BaseTurretItem
    {
        public override int TargetID => CWRID.Item_FireTurret;
    }

    internal class ModifyFireTurretTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_PlayerFireTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<FireTurretByFriendTP>();
    }

    internal class ModifyFireTurretByHostileTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_HostileFireTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<FireTurretTP>();
    }

    internal class FireTurretByFriendTP : FireTurretTP
    {
        public override int TargetTileID => CWRID.Tile_PlayerFireTurret;
        public override int TargetItem => CWRID.Item_FireTurret;
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class FireTurretTP : BaseTurretTP
    {
        public override int TargetTileID => CWRID.Tile_HostileFireTurret;
        public override string BodyPath => CWRConstant.Turrets + "FireTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "FireTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "FireTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "FireTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => CWRID.Item_HostileFireTurret;
        public override void SetTurret() {
            FireTime = 10;
            Damage = 24;
            MaxFindMode = 320;
            SingleEnergyConsumption = 4;
            ShootID = CWRID.Proj_FireShotBuffer;
        }
    }
}
