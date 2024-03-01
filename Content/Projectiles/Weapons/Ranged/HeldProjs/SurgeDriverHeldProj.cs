using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SurgeDriverHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "SurgeDriver";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.SurgeDriver>();
        public override int targetCWRItem => ModContent.ItemType<SurgeDriverEcType>();
        private int bulletNum {
            get => heldItem.CWR().NumberBullets;
            set => heldItem.CWR().NumberBullets = value;
        }

        public override void SetRangedProperty() {
            loadTheRounds = CWRSound.CaseEjection2 with { Pitch = -0.2f };
            kreloadMaxTime = 90;
            fireTime = 10;
            HandDistance = 52;
            ShootPosNorlLengValue = -8;
            ShootPosToMouLengValue = 30;
            RepeatedCartridgeChange = true;
        }

        public override bool WhetherStartChangingAmmunition() {
            return base.WhetherStartChangingAmmunition() && bulletNum < heldItem.CWR().AmmoCapacity;
        }

        public override void KreloadSoundCaseEjection() {
            base.KreloadSoundCaseEjection();
        }

        public override void KreloadSoundloadTheRounds() {
            base.KreloadSoundloadTheRounds();
        }

        public override bool PreFireReloadKreLoad() {
            if (bulletNum <= 0) {
                
                loadingReminder = false;//在发射后设置一下装弹提醒开关，防止进行一次有效射击后仍旧弹出提示
                isKreload = false;
                if (heldItem.type != ItemID.None) {
                    heldItem.CWR().IsKreload = false;
                }

                bulletNum = 0;
            }
            return false;
        }

        public override void OnKreLoad() {
            bulletNum = heldItem.CWR().AmmoCapacity;
        }

        public override void OnSpanProjFunc() {
            Projectile.NewProjectile(Owner.parent(), GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void PostSpanProjFunc() {
            bulletNum--;
        }
    }
}
