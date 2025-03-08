﻿using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.PlayerTurrets;
using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets.Turrets
{
    internal class ModifyWaterTurret : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<WaterTurret>();
        public override void SetDefaults(Item item) {
            item.CWR().StorageUE = true;
            item.CWR().ConsumeUseUE = 1000;
        }
    }

    internal class ModifyWaterTurretTile : BaseBaseTurretTile
    {
        public override int TargetID => ModContent.TileType<PlayerWaterTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<WaterTurretByFriendTP>();
    }

    internal class ModifyWaterTurretByHostileTile : BaseBaseTurretTile
    {
        public override int TargetID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostileWaterTurret>();
        public override int TargetTPID => TileProcessorLoader.GetModuleID<WaterTurretTP>();
    }

    internal class WaterTurretByFriendTP : WaterTurretTP
    {
        public override int TargetTileID => ModContent.TileType<PlayerWaterTurret>();
        public override int TargetItem => ModContent.ItemType<WaterTurret>();
        public override bool Friend => true;
        public override bool CanDrop => true;
    }

    internal class WaterTurretTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostileWaterTurret>();//愚蠢的同名，让人只能路径标记
        public override string BodyPath => CWRConstant.Turrets + "WaterTurretBody";
        public override string BodyGlowPath => CWRConstant.Turrets + "WaterTurretBodyGlow";
        public override string BarrelPath => CWRConstant.Turrets + "WaterTurretBarrel";
        public override string BarrelGlowPath => CWRConstant.Turrets + "WaterTurretBarrelGlow";
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<HostileWaterTurret>();
        public override void SetTurret() {
            Damage = 24;
            ShootID = ModContent.ProjectileType<WaterShotBuffer>();
        }
    }
}
