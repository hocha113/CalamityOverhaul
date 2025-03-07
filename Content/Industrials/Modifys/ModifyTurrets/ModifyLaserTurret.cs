using CalamityMod.Projectiles.Turret;
using CalamityMod.Tiles.DraedonStructures;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Threading;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys.ModifyTurrets
{
    internal class ModifyLaserTurret
    {

    }

    internal class LaserTurretByFriendTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<HostileLaserTurret>();
        public override Asset<Texture2D> GetBodyAsset => LaserTurretByHostileTP.Body;
        public override Asset<Texture2D> GetBarrelAsset => LaserTurretByHostileTP.Barrel;
        public override void SetTurret() {
            Damage = 24;
            ShootID = ModContent.ProjectileType<LaserShotBuffer>();
        }
    }

    internal class LaserTurretByHostileTP : BaseTurretTP, ICWRLoader
    {
        public override int TargetTileID => ModContent.TileType<HostileLaserTurret>();
        public static Asset<Texture2D> Body { get; private set; }
        public static Asset<Texture2D> Barrel { get; private set; }
        public override Asset<Texture2D> GetBodyAsset => Body;
        public override Asset<Texture2D> GetBarrelAsset => Barrel;
        public override bool Friend => false;
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
