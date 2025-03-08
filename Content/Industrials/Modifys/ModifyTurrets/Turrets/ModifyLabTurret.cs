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
    internal class ModifyLabTurret : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<LabTurret>();
        public override void SetDefaults(Item item) {
            item.CWR().StorageUE = true;
            item.CWR().ConsumeUseUE = 1000;
        }
    }

    internal class ModifyLabTurretTile : BaseBaseTurretTile
    {
        public override int TargetID => ModContent.TileType<PlayerLabTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<LabTurretByFriendTP>();
    }

    internal class ModifyLabTurretByHostileTile : BaseBaseTurretTile
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
        public override string BodyPath => CWRConstant.Turrets + "LaserTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "LaserTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "LaserTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "LaserTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<HostileLabTurret>();
        public override void SetTurret() {
            Damage = 24;
            ShootID = ModContent.ProjectileType<DraedonLaserBuffer>();
        }
    }
}
