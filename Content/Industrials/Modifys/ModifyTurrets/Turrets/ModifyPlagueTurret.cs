using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.PlayerTurrets;
using InnoVault.TileProcessors;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyPlagueTurret : BaseTurretItem
    {
        public override int TargetID => ModContent.ItemType<PlagueTurret>();
    }

    internal class ModifyPlagueTurretTile : BaseTurretTile
    {
        public override int TargetID => ModContent.TileType<PlayerPlagueTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<PlagueTurretByFriendTP>();
    }

    internal class ModifyPlagueTurretByHostileTile : BaseTurretTile
    {
        public override int TargetID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostilePlagueTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<PlagueTurretTP>();
    }

    internal class PlagueTurretByFriendTP : PlagueTurretTP
    {
        public override int TargetTileID => ModContent.TileType<PlayerPlagueTurret>();
        public override int TargetItem => ModContent.ItemType<PlagueTurret>();
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class PlagueTurretTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostilePlagueTurret>();//愚蠢的同名，让人只能路径标记
        public override string BodyPath => CWRConstant.Turrets + "PlagueTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "PlagueTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "PlagueTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "PlagueTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<HostilePlagueTurret>();
        public override void SetTurret() {
            Damage = 24;
            ShootID = ModContent.ProjectileType<PlagueShotBuffer>();
        }
    }
}
