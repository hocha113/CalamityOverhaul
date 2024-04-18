using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class FlintlockpistolHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.FlintlockPistol].Value;
        public override int targetCayItem => ItemID.FlintlockPistol;
        public override int targetCWRItem => ItemID.FlintlockPistol;
        public override void SetRangedProperty(){
            ShootPosToMouLengValue = 6;
            ShootPosNorlLengValue = -5;
            HandDistance = 20;
            HandDistanceY = 3;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.8f;
        }

        public override void FiringShoot(){
            base.FiringShoot();
            CaseEjection();
            SpawnGunFireDust();
        }
    }
}
