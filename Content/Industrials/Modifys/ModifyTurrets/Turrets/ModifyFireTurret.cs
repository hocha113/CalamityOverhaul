using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.PlayerTurrets;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.Tiles.Core;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyFireTurret : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<FireTurret>();
        public override void SetDefaults(Item item) {
            item.CWR().StorageUE = true;
            item.CWR().ConsumeUseUE = 1000;
        }
    }

    internal class ModifyFireTurretTile : TileOverride
    {
        public override int TargetID => ModContent.TileType<PlayerFireTurret>();
        public override bool? CanDrop(int i, int j, int type) => false;
    }

    internal class FireTurretByFriendTP : LaserTurretByHostileTP
    {
        public override int TargetTileID => ModContent.TileType<PlayerFireTurret>();
        public override int TargetItem => ModContent.ItemType<IceTurret>();
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
            SingleEnergyConsumption = 4;
            ShootID = ModContent.ProjectileType<FireShotBuffer>();
        }
    }
}
