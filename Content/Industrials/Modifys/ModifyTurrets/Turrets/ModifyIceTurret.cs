using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.PlayerTurrets;
using InnoVault.TileProcessors;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyIceTurret : BaseTurretItem
    {
        public override int TargetID => ModContent.ItemType<IceTurret>();
    }

    internal class ModifyIceTurretTile : BaseTurretTile
    {
        public override int TargetID => ModContent.TileType<PlayerIceTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<IceTurretByFriendTP>();
    }

    internal class ModifyIceTurretByHostileTile : BaseTurretTile
    {
        public override int TargetID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostileIceTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<IceTurretTP>();
    }

    internal class IceTurretByFriendTP : IceTurretTP
    {
        public override int TargetTileID => ModContent.TileType<PlayerIceTurret>();
        public override int TargetItem => ModContent.ItemType<IceTurret>();
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class IceTurretTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostileIceTurret>();//愚蠢的同名，让人只能路径标记
        public override string BodyPath => CWRConstant.Turrets + "IceTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "IceTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "IceTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "IceTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<HostileIceTurret>();
        public override void SetTurret() {
            Damage = 24;
            ShootID = ModContent.ProjectileType<IceShotBuffer>();
            BarrelOffsetY = -4;
            BarrelOffsetX = 22;
        }
    }
}
