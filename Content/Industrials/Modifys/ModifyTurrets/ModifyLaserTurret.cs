using CalamityMod.Items.Placeables.PlaceableTurrets;
using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.PlayerTurrets;
using CalamityOverhaul.Content.RemakeItems.Core;
using CalamityOverhaul.Content.Tiles.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets
{
    internal class ModifyLaserTurret : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<LaserTurret>();
        public override void SetDefaults(Item item) {
            item.CWR().StorageUE = true;
            item.CWR().ConsumeUseUE = 1000;
        }
    }

    internal class ModifyPlayerLaserTurret : TileOverride
    {
        public override int TargetID => ModContent.TileType<PlayerLaserTurret>();
        public override bool? CanDrop(int i, int j, int type) => false;
    }

    internal class LaserTurretByFriendTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<PlayerLaserTurret>();
        public override Asset<Texture2D> GetBodyAsset => LaserTurretByHostileTP.Body;
        public override Asset<Texture2D> GetBarrelAsset => LaserTurretByHostileTP.Barrel;
        public override int TargetItem => ModContent.ItemType<LaserTurret>();
        public override void SetTurret() {
            Damage = 24;
            ShootID = ModContent.ProjectileType<LaserShotBuffer>();
        }
    }

    internal class LaserTurretByHostileTP : BaseTurretTP, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<CalamityMod.Tiles.DraedonStructures.HostileLaserTurret>();//愚蠢的同名，让人只能路径标记
        public static Asset<Texture2D> Body { get; private set; }
        public static Asset<Texture2D> Barrel { get; private set; }
        public override Asset<Texture2D> GetBodyAsset => Body;
        public override Asset<Texture2D> GetBarrelAsset => Barrel;
        public override bool Friend => false;
        public override bool CanDrop => false;
        public override int TargetItem => ModContent.ItemType<HostileLaserTurret>();
        void ICWRLoader.LoadAsset() {
            Body = CWRUtils.GetT2DAsset(CWRConstant.Turrets + "LaserTurretBody");
            Barrel = CWRUtils.GetT2DAsset(CWRConstant.Turrets + "LaserTurretBarrel");
        }
        void ICWRLoader.UnLoadData() {
            Body = null;
            Barrel = null;
        }

        public override void SetTurret() {
            Damage = 24;
            ShootID = ModContent.ProjectileType<LaserShotBuffer>();
        }
    }
}
