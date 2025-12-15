using InnoVault.TileProcessors;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyLabTurret : BaseTurretItem
    {
        public override int TargetID => CWRID.Item_LabTurret;
    }

    internal class ModifyLabTurretTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_PlayerLabTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<LabTurretByFriendTP>();
    }

    internal class ModifyLabTurretByHostileTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_DraedonLabTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<LabTurretTP>();
    }

    internal class LabTurretByFriendTP : LabTurretTP
    {
        public override int TargetTileID => CWRID.Tile_PlayerLabTurret;
        public override int TargetItem => CWRID.Item_LabTurret;
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class LabTurretTP : BaseTurretTP
    {
        public override int TargetTileID => CWRID.Tile_DraedonLabTurret;
        public override string BodyPath => CWRConstant.Turrets + "LabTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "LabTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "LabTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "LabTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => CWRID.Item_HostileLabTurret;
        public override void SetTurret() {
            Damage = 24;
            ShootID = CWRID.Proj_DraedonLaserBuffer;
            BarrelOffsetY = 2;
            BarrelOffsetX = 28;
        }
    }
}
