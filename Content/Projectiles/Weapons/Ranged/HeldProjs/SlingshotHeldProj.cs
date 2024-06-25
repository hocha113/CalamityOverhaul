using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SlingshotHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Item_Ranged + "Slingshot";
        public override int targetCayItem => ModContent.ItemType<Slingshot>();
        public override int targetCWRItem => ModContent.ItemType<Slingshot>();
        public override void SetRangedProperty() {
            HandDistance = 10;
            HandFireDistance = 16;
            HandFireDistanceY = 6;
            IsBow = false;
            ForcedConversionTargetAmmoFunc = () => true;
            ToTargetAmmo = ModContent.ProjectileType<PebbleBall>();
        }

        public override void BowShoot() {
            FireOffsetPos = ShootVelocity.GetNormalVector() * 5 * DirSign;
            base.BowShoot();
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs
            , List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            if (CanFire) {
                overPlayers.Add(index);
            }
        }
    }
}
