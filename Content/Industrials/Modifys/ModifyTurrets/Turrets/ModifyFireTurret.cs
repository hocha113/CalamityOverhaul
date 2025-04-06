using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.PlayerTurrets;
using InnoVault.TileProcessors;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyFireTurret : BaseTurretItem
    {
        public override int TargetID => ModContent.ItemType<FireTurret>();
    }

    internal class ModifyFireTurretTile : BaseTurretTile
    {
        public override int TargetID => ModContent.TileType<PlayerFireTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<FireTurretByFriendTP>();
    }

    internal class ModifyFireTurretByHostileTile : BaseTurretTile
    {
        public override int TargetID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostileFireTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<FireTurretTP>();
    }

    internal class FireTurretByFriendTP : FireTurretTP
    {
        public override int TargetTileID => ModContent.TileType<PlayerFireTurret>();
        public override int TargetItem => ModContent.ItemType<FireTurret>();
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class FireTurretTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostileFireTurret>();//愚蠢的同名，让人只能路径标记
        public override string BodyPath => CWRConstant.Turrets + "FireTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "FireTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "FireTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "FireTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<HostileFireTurret>();
        public override void SetTurret() {
            FireTime = 10;
            Damage = 24;
            MaxFindMode = 320;
            SingleEnergyConsumption = 4;
            ShootID = ModContent.ProjectileType<FireShotBuffer>();
        }
    }
}
