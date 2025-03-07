using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.PlayerTurrets;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.Tiles.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyIceTurret : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<IceTurret>();
        public override void SetDefaults(Item item) {
            item.CWR().StorageUE = true;
            item.CWR().ConsumeUseUE = 1000;
        }
    }

    internal class ModifyIceTurretTile : TileOverride
    {
        public override int TargetID => ModContent.TileType<PlayerIceTurret>();
        public override bool? CanDrop(int i, int j, int type) => false;
    }

    internal class IceTurretByFriendTP : LaserTurretByHostileTP
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
        }
    }
}
