using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.PlayerTurrets;
using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyOnyxTurret : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<OnyxTurret>();
        public override void SetDefaults(Item item) {
            item.CWR().StorageUE = true;
            item.CWR().ConsumeUseUE = 1000;
        }
    }

    internal class ModifyOnyxTurretTile : BaseBaseTurretTile
    {
        public override int TargetID => ModContent.TileType<PlayerOnyxTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<OnyxTurretByFriendTP>();
    }

    internal class ModifyOnyxTurretByHostileTile : BaseBaseTurretTile
    {
        public override int TargetID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostileOnyxTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<OnyxTurretTP>();
    }

    internal class OnyxTurretByFriendTP : OnyxTurretTP
    {
        public override int TargetTileID => ModContent.TileType<PlayerOnyxTurret>();
        public override int TargetItem => ModContent.ItemType<OnyxTurret>();
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class OnyxTurretTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostileOnyxTurret>();//愚蠢的同名，让人只能路径标记
        public override string BodyPath => CWRConstant.Turrets + "OnyxTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "OnyxTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "OnyxTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "OnyxTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<HostileOnyxTurret>();
        public override void SetTurret() {
            Damage = 24;
            ShootID = ModContent.ProjectileType<OnyxShotBuffer>();
        }
    }
}
