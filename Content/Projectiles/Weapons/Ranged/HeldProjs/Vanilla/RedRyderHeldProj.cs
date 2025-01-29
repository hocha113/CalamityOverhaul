using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class RedRyderHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.RedRyder].Value;
        public override int TargetID => ItemID.RedRyder;
        public override void SetRangedProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandIdleDistanceX = 15;
            HandIdleDistanceY = 0;
            GunPressure = 0.6f;
            ControlForce = 0.05f;
            Recoil = 1.4f;
            if (!MagazineSystem) {
                FireTime += 30;
            }
        }
    }
}
