using InnoVault.TileProcessors;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyPlagueTurret : BaseTurretItem
    {
        public override int TargetID => CWRID.Item_PlagueTurret;
    }

    internal class ModifyPlagueTurretTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_PlayerPlagueTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<PlagueTurretByFriendTP>();
    }

    internal class ModifyPlagueTurretByHostileTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_HostilePlagueTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<PlagueTurretTP>();
    }

    internal class PlagueTurretByFriendTP : PlagueTurretTP
    {
        public override int TargetTileID => CWRID.Tile_PlayerPlagueTurret;
        public override int TargetItem => CWRID.Item_PlagueTurret;
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class PlagueTurretTP : BaseTurretTP
    {
        public override int TargetTileID => CWRID.Tile_HostilePlagueTurret;
        public override string BodyPath => CWRConstant.Turrets + "PlagueTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "PlagueTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "PlagueTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "PlagueTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => CWRID.Item_HostilePlagueTurret;
        public override void SetTurret() {
            Damage = 24;
            ShootID = CWRID.Proj_PlagueShotBuffer;
        }
    }
}
