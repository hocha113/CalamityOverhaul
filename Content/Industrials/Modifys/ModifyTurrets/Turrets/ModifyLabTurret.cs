using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.DraedonStructures;
using CalamityMod.Tiles.PlayerTurrets;
using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyLabTurret : BaseTurretItem
    {
        public override int TargetID => ModContent.ItemType<LabTurret>();
    }

    internal class ModifyLabTurretTile : BaseTurretTile
    {
        public override int TargetID => ModContent.TileType<PlayerLabTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<LabTurretByFriendTP>();
    }

    internal class ModifyLabTurretByHostileTile : BaseTurretTile
    {
        public override int TargetID => ModContent.TileType<DraedonLabTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<LabTurretTP>();
    }

    internal class LabTurretByFriendTP : LabTurretTP
    {
        public override int TargetTileID => ModContent.TileType<PlayerLabTurret>();
        public override int TargetItem => ModContent.ItemType<LabTurret>();
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class LabTurretTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<DraedonLabTurret>();
        public override string BodyPath => CWRConstant.Turrets + "LabTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "LabTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "LabTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "LabTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<HostileLabTurret>();
        public override void SetTurret() {
            Damage = 24;
            ShootID = ModContent.ProjectileType<DraedonLaserBuffer>();
            BarrelOffsetY = 2;
            BarrelOffsetX = 28;
        }
    }
}
