using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.PlayerTurrets;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.Tiles.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyPlagueTurret : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<PlagueTurret>();
        public override void SetDefaults(Item item) {
            item.CWR().StorageUE = true;
            item.CWR().ConsumeUseUE = 1000;
        }
    }

    internal class ModifyPlagueTurretTile : TileOverride
    {
        public override int TargetID => ModContent.TileType<PlayerPlagueTurret>();
        public override bool? CanDrop(int i, int j, int type) => false;
    }

    internal class PlagueTurretByFriendTP : LaserTurretByHostileTP
    {
        public override int TargetTileID => ModContent.TileType<PlayerPlagueTurret>();
        public override int TargetItem => ModContent.ItemType<IceTurret>();
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
