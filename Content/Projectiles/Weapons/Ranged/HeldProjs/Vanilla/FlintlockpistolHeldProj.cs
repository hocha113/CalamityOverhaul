using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class FlintlockpistolHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.FlintlockPistol].Value;
        public override int TargetID => ItemID.FlintlockPistol;
        public override void SetRangedProperty() {
            InOwner_HandState_AlwaysSetInFireRoding = true;
            ShootPosToMouLengValue = 6;
            ShootPosNorlLengValue = -5;
            HandIdleDistanceX = 20;
            HandIdleDistanceY = 3;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.6f;
            Onehanded = true;
            SpwanGunDustMngsData.splNum = 0.3f;
        }
    }
}
