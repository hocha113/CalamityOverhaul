using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutCannonHeld : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "HalibutCannon";
        public override int TargetID => ModContent.ItemType<HalibutCannon>();
        private int level => HalibutCannonOverride.Level;
        public override void SetRangedProperty() {
            ControlForce = 0.05f;
            GunPressure = 0.2f;
            Recoil = 1.1f;
            HandIdleDistanceX = 40;
            HandIdleDistanceY = 8;
            HandFireDistanceX = 40;
            HandFireDistanceY = -3;
            CanCreateSpawnGunDust = false;
        }

        private void Shoot(int num) {
            for (int i = 0; i < num; i++) {
                bool reset = true;

                if (HanderFishItem.TargetFish != null && HanderFishItem.TargetFish.FishSkill != null) {
                    reset = HanderFishItem.TargetFish.FishSkill.PreShoot();
                }

                int projIndex = -1;

                if (reset) {
                    projIndex = Projectile.NewProjectile(Source, ShootPos
                    , ShootVelocity.RotatedBy(Main.rand.NextFloat(-0.03f, 0.03f)) * Main.rand.NextFloat(0.9f, 1.32f)
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                }

                if (projIndex >= 0) {
                    Main.projectile[projIndex].CWR().SpanTypes = (byte)SpanTypesEnum.HalibutCannon;
                }

                if (HanderFishItem.TargetFish != null && HanderFishItem.TargetFish.FishSkill != null) {
                    HanderFishItem.TargetFish.FishSkill.PostShoot(projIndex);
                }
            }
        }

        /// <summary>
        /// 大比目鱼炮射出的子弹会被标记，由这个函数来进行进一步的伤害修改缩放
        /// </summary>
        /// <param name="projectile"></param>
        /// <param name="target"></param>
        /// <param name="modifiers"></param>
        public static void ModifyHalibutAmmoHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers) {
            bool isTorrentialBullet = projectile.type == ModContent.ProjectileType<TorrentialBullet>();

            if (projectile.penetrate > 1 || projectile.penetrate == -1) {
                if (target.IsWormBody()) {
                    if (projectile.penetrate == -1) {
                        projectile.penetrate = 3;
                    }
                    modifiers.FinalDamage *= 0.75f;
                    modifiers.DisableCrit();
                }
            }
        }

        public override void FiringShoot() {
            if (AmmoTypes == ProjectileID.Bullet) {
                AmmoTypes = ModContent.ProjectileType<TorrentialBullet>();
            }

            switch (level) {
                case 0:
                    int proj = Projectile.NewProjectile(Source, ShootPos, ShootVelocity * Main.rand.NextFloat(0.9f, 1.32f)
                        , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    Main.projectile[proj].timeLeft = 90;
                    Main.projectile[proj].CWR().SpanTypes = (byte)SpanTypesEnum.HalibutCannon;
                    break;
                case 1:
                case 2:
                case 3:
                    Shoot(2);
                    break;
                case 4:
                case 5:
                    Shoot(4);
                    break;
                case 6:
                    Shoot(6);
                    break;
                case 7:
                    Shoot(9);
                    break;
                case 8:
                    Shoot(11);
                    break;
                case 9:
                    Shoot(13);
                    break;
                case 10:
                    Shoot(15);
                    break;
                case 11:
                    Shoot(18);
                    break;
                case 12:
                    Shoot(22);
                    break;
                case 13:
                    Shoot(27);
                    break;
                case 14:
                    Shoot(33);
                    break;
            }

            _ = UpdateConsumeAmmo();
        }
    }
}
