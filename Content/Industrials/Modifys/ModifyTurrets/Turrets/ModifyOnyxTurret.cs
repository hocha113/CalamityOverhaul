using InnoVault.TileProcessors;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyOnyxTurret : BaseTurretItem
    {
        public override int TargetID => CWRID.Item_OnyxTurret;
    }

    internal class ModifyOnyxTurretTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_PlayerOnyxTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<OnyxTurretByFriendTP>();
    }

    internal class ModifyOnyxTurretByHostileTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_HostileOnyxTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<OnyxTurretTP>();
    }

    internal class OnyxTurretByFriendTP : OnyxTurretTP
    {
        public override int TargetTileID => CWRID.Tile_PlayerOnyxTurret;
        public override int TargetItem => CWRID.Item_OnyxTurret;
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class OnyxTurretTP : BaseTurretTP
    {
        public override int TargetTileID => CWRID.Tile_HostileOnyxTurret;
        public override string BodyPath => CWRConstant.Turrets + "OnyxTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "OnyxTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "OnyxTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "OnyxTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => CWRID.Item_HostileOnyxTurret;
        public override void SetTurret() {
            Damage = 24;
            MaxFindMode = 500;
            ShootID = CWRID.Proj_OnyxShotBuffer;
            BarrelOffsetY = 2;
            BarrelOffsetX = 22;
        }
    }
}
