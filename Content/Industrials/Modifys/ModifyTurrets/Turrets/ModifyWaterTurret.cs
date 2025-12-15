using InnoVault.TileProcessors;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyWaterTurret : BaseTurretItem
    {
        public override int TargetID => CWRID.Item_WaterTurret;
    }

    internal class ModifyWaterTurretTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_PlayerWaterTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<WaterTurretByFriendTP>();
    }

    internal class ModifyWaterTurretByHostileTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_HostileWaterTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<WaterTurretTP>();
    }

    internal class WaterTurretByFriendTP : WaterTurretTP
    {
        public override int TargetTileID => CWRID.Tile_PlayerWaterTurret;
        public override int TargetItem => CWRID.Item_WaterTurret;
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class WaterTurretTP : BaseTurretTP
    {
        public override int TargetTileID => CWRID.Tile_HostileWaterTurret;
        public override string BodyPath => CWRConstant.Turrets + "WaterTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "WaterTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "WaterTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "WaterTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => CWRID.Item_HostileWaterTurret;
        public override void SetTurret() {
            Damage = 24;
            ShootID = CWRID.Proj_WaterShotBuffer;
            BarrelOffsetY = -2;
            BarrelOffsetX = 22;
        }
    }
}
