using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Items.Magic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class SHPCHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "SHPC";
        public override int targetCayItem => ModContent.ItemType<SHPC>();
        public override int targetCWRItem => ModContent.ItemType<SHPCEcType>();

        private int level => InWorldBossPhase.Instance.SHPC_Level();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 35;
            HandDistanceY = 5;
            HandFireDistance = 35;
            HandFireDistanceY = 0;
            GunPressure = 0.3f;
            ControlForce = 0.02f;
            Recoil = 0;
            CanRightClick = true;
            EnableRecoilRetroEffect = true;
        }

        public override void HanderPlaySound() {
            if (onFire) {
                SoundEngine.PlaySound(SoundID.Item92, Projectile.Center);
            }
            else if (onFireR) {
                SoundEngine.PlaySound(CommonCalamitySounds.LaserCannonSound, Projectile.Center);
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                Item.useTime = 45;
                Item.mana = 20;
                GunPressure = 0.3f;
                RecoilRetroForceMagnitude = 0;
                switch (level) {
                    case 0:
                        Item.useTime = 60;
                        break;
                    case 1:
                    case 2:
                    case 3:
                        Item.useTime = 55;
                        break;
                    case 4:
                    case 5:
                        Item.useTime = 50;
                        break;
                    default:
                        Item.useTime = 45;
                        break;
                }
            }
            else if (onFireR) {
                Item.useTime = 7;
                Item.mana = 6;
                GunPressure = 0f;
                RecoilRetroForceMagnitude = 6;
                switch (level) {
                    case 0:
                        Item.mana = 3;
                        Item.useTime = 10;
                        break;
                    case 1:
                    case 2:
                    case 3:
                        Item.mana = 4;
                        Item.useTime = 9;
                        break;
                    case 4:
                    case 5:
                        Item.mana = 5;
                        Item.useTime = 8;
                        break;
                    default:
                        Item.mana = 6;
                        Item.useTime = 7;
                        break;
                }
            }
        }

        public override void FiringShoot() {
            int type = ModContent.ProjectileType<PhaseEnergySphere>();
            switch (level) {
                case 0:
                case 1:
                case 2:
                case 3:
                    Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                    , type, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    break;
                case 4:
                case 5:
                case 6:
                    if (level == 4 && NPC.AnyNPCs(NPCID.WallofFlesh)) {
                        Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                        , type, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                        break;
                    }
                    for (int i = 0; i < 2; i++) {
                        Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.8f, 1f)
                            , type, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    }
                    break;
                default:
                    for (int i = 0; i < 3; i++) {
                        Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.22f) * Main.rand.NextFloat(0.8f, 1f)
                            , type, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    }
                    break;
            }
        }

        public override void FiringShootR() {
            int type = ModContent.ProjectileType<PhaseLaser>();
            switch (level) {
                case 0:
                case 1:
                case 2:
                case 3:
                    Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                    , type, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    break;
                case 4:
                case 5:
                case 6:
                    if (level == 4 && NPC.AnyNPCs(NPCID.WallofFlesh)) {
                        Projectile.NewProjectile(Source, GunShootPos, ShootVelocity
                        , type, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                        break;
                    }
                    for (int i = 0; i < 2; i++) {
                        Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.08f) * Main.rand.NextFloat(0.8f, 1f)
                            , type, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    }
                    break;
                default:
                    for (int i = 0; i < 3; i++) {
                        Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.12f) * Main.rand.NextFloat(0.8f, 1f)
                            , type, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                    }
                    break;
            }
        }
    }
}
