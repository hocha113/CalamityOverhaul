using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    //蜂毒，我最喜欢的弓，纹理看起来很养眼
    internal class MalevolenceHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Malevolence";
        public override int TargetID => ModContent.ItemType<Malevolence>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 2;
            HandFireDistanceX = 20;
            DrawArrowMode = -24;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            BowstringData.DeductRectangle = new Rectangle(0, 14, 6, 44);
            BowstringData.TopBowOffset = new Vector2(0, 10);
            BowstringData.CoreOffset = new Vector2(2, 0);
        }
        public override void BowShoot() {
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ModContent.ProjectileType<PlagueArrow>();
            }
            FireOffsetVector = ShootVelocity.RotatedByRandom(0.3f) * Main.rand.NextFloat(0.2f, 0.23f);
            base.BowShoot();
            FireOffsetVector = ShootVelocity.RotatedByRandom(0.3f) * Main.rand.NextFloat(0.2f, 0.23f);
            base.BowShoot();
        }
    }
}
