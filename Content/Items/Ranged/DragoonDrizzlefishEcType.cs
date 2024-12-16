using CalamityMod.Items.Fishing.BrimstoneCragCatches;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class DragoonDrizzlefishEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Item + "Fishing/BrimstoneCragCatches/DragoonDrizzlefish";
        public override void SetDefaults() {
            Item.SetItemCopySD<DragoonDrizzlefish>();
            Item.CWR().CartridgeType = CartridgeUIEnum.JAR;
            Item.SetCartridgeGun<DragoonDrizzlefishHeld>(22);
        }
    }

    internal class RDragoonDrizzlefish : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<DragoonDrizzlefish>();
        public override int ProtogenesisID => ModContent.ItemType<DragoonDrizzlefishEcType>();
        public override void SetDefaults(Item item) {
            item.CWR().CartridgeType = CartridgeUIEnum.JAR;
            item.SetCartridgeGun<DragoonDrizzlefishHeld>(22);
        }
    }

    internal class DragoonDrizzlefishHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Item + "Fishing/BrimstoneCragCatches/DragoonDrizzlefish";
        public override int targetCayItem => ModContent.ItemType<DragoonDrizzlefish>();
        public override int targetCWRItem => ModContent.ItemType<DragoonDrizzlefishEcType>();
        private int fireIndex;
        public override void SetRangedProperty() {
            Onehanded = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            ShootPosToMouLengValue = 6;
            ShootPosNorlLengValue = -5;
            HandDistance = 20;
            HandDistanceY = 3;
            GunPressure = 0.2f;
            ControlForce = 0.05f;
            Recoil = 0.6f;
            SpwanGunDustMngsData.splNum = 0.3f;
            CanCreateCaseEjection = false;
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Handgun;
        }

        public override void FiringShoot() {
            Vector2 newShootVer = ShootVelocity.RotatedByRandom(MathHelper.ToRadians(5.5f));
            int shotType = ModContent.ProjectileType<DrizzlefishFireball>();
            if (fireIndex < 3) {
                shotType = ModContent.ProjectileType<DrizzlefishFireball>();
                fireIndex++;
            }
            else {
                shotType = ModContent.ProjectileType<DrizzlefishFire>();
                fireIndex = 0;
            }
            Projectile.NewProjectile(Source, GunShootPos, newShootVer, shotType
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0f, Main.rand.Next(2));
        }
    }
}
