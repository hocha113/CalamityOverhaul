using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs.Vanilla
{
    internal class RedRyderHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.RedRyder].Value;
        public override int targetCayItem => ItemID.RedRyder;
        public override int targetCWRItem => ItemID.RedRyder;
        public override void SetRangedProperty()
        {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0.8f;
            ControlForce = 0.05f;
            Recoil = 2.4f;
        }
    }
}
