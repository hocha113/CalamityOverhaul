using InnoVault.TileProcessors;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyIceTurret : BaseTurretItem
    {
        public override int TargetID => CWRID.Item_IceTurret;
    }

    internal class ModifyIceTurretTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_PlayerIceTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<IceTurretByFriendTP>();
    }

    internal class ModifyIceTurretByHostileTile : BaseTurretTile
    {
        public override int TargetID => CWRID.Tile_HostileIceTurret;
        public override int TargetTPID => TileProcessorLoader.GetModuleID<IceTurretTP>();
    }

    internal class IceTurretByFriendTP : IceTurretTP
    {
        public override int TargetTileID => CWRID.Tile_PlayerIceTurret;
        public override int TargetItem => CWRID.Item_IceTurret;
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class IceTurretTP : BaseTurretTP
    {
        public override int TargetTileID => CWRID.Tile_HostileIceTurret;
        public override string BodyPath => CWRConstant.Turrets + "IceTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "IceTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "IceTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "IceTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => CWRID.Item_HostileIceTurret;
        public override void SetTurret() {
            Damage = 24;
            MaxFindMode = 200;
            ShootID = CWRID.Proj_IceShotBuffer;
            BarrelOffsetY = -4;
            BarrelOffsetX = 22;
        }
    }
}
