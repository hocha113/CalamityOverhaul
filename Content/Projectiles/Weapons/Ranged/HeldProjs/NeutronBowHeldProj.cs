using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class NeutronBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronBow";
        public override int TargetID => NeutronBow.PType;
        private float Charge {
            get => ((NeutronBow)Item.ModItem).Charge;
            set => ((NeutronBow)Item.ModItem).Charge = value;
        }
        private bool canAltFire;
        private int uiframe;
        private bool level1 = true;
        private bool level2 = true;
        private bool level3 = true;
        public override bool IsLoadingEnabled(Mod mod) {
            return true;
        }
        public override void SetRangedProperty() {
            InOwner_HandState_AlwaysSetInFireRoding = true;
            ForcedConversionTargetAmmoFunc = () => true;
            ISForcedConversionDrawAmmoInversion = true;
            ToTargetAmmo = ModContent.ProjectileType<NeutronArrow>();
            DrawArrowMode = -25;
            CanRightClick = true;
        }

        private void NewText(string key, int offsetY = 0) {
            Rectangle rectext = Owner.Hitbox;
            rectext.Y -= offsetY;
            CombatText.NewText(rectext, new Color(155, 200, 100 + offsetY), key, true);
        }

        private void RightCharge() {
            if (!onFireR) {
                BowArrowDrawNum = 1;
                Charge = 0;
                level1 = level2 = level3 = true;
                return;
            }

            if (Charge < 80) {
                Charge += 0.5f;
                BowArrowDrawNum = 1;
                Item.useTime = 20;
                ShootCoolingValue = Charge / 4;
                if (ShootCoolingValue > 2) {
                    if (level1) {
                        NewText(NeutronBow.Lang1.Value, 0);
                        SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = -0.3f, Volume = 0.6f }, Projectile.Center);
                        level1 = false;
                    }
                }
                if (Charge > 30) {
                    if (level2) {
                        NewText(NeutronBow.Lang2.Value, 60);
                        SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);
                        level2 = false;
                    }
                    BowArrowDrawNum++;
                }
                if (Charge > 60) {
                    if (level3) {
                        NewText(NeutronBow.Lang3.Value, 120);
                        SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = -0.1f, Volume = 0.8f }, Projectile.Center);
                        level3 = false;
                        Charge = 80;
                    }
                    BowArrowDrawNum++;
                }
            }
            else {
                BowArrowDrawNum = 3;
            }

            if (ShootCoolingValue > 19) {
                if (!canAltFire) {
                    for (int i = 0; i < 3; i++) {
                        PRT_LonginusWave pulse = new PRT_LonginusWave(Projectile.Center, ShootVelocity, Color.BlueViolet
                            , new Vector2(1.5f, 3f) * (0.8f - i * 0.1f), ShootVelocity.ToRotation(), 0.62f, 0.12f, 60, Projectile);
                        PRTLoader.AddParticle(pulse);
                    }
                    canAltFire = true;
                }
                ShootCoolingValue = 19;
            }
        }

        public override void PostInOwner() {
            if (CanFire && CanSpanProj()) {
                RightCharge();
                VaultUtils.ClockFrame(ref Projectile.frame, 2, 6);
                VaultUtils.ClockFrame(ref uiframe, 5, 6);
            }
            else {
                BowArrowDrawNum = 1;
                Charge = 0;
                Projectile.frame = 0;
                uiframe = 0;
                level1 = level2 = level3 = true;
                if (canAltFire) {
                    onFireR = true;
                    ShootCoolingValue = 21;
                    canAltFire = false;
                }
            }
        }

        public override void SetShootAttribute() {
            if (onFire) {
                FiringDefaultSound = true;
                Item.useTime = 20;
            }
        }

        public override void HanderPlaySound() {
            if (onFireR) {
                SoundEngine.PlaySound(CWRSound.Gun_Magnum_Shoot
                    with { Pitch = 0.7f, Volume = 0.6f }, Projectile.Center);
                return;
            }
            base.HanderPlaySound();
        }

        public override void BowShootR() {
            Owner.CWR().SetScreenShake(6.2f);
            AmmoTypes = ModContent.ProjectileType<EXNeutronArrow>();
            for (int i = 0; i < 3; i++) {
                int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedBy((-1 + i) * 0.25f)
                , AmmoTypes, WeaponDamage * (i == 1 ? 5 : 3), WeaponKnockback, Owner.whoAmI, 0);
                Main.projectile[proj].CWR().SpanTypes = (byte)ShootSpanTypeValue;
                Main.projectile[proj].SetArrowRot();
            }
        }

        public override void PostBowShoot() {
            if (onFireR) {
                level1 = level2 = level3 = true;
                Charge = 0;
            }
        }

        public override void BowDraw(Vector2 drawPos, ref Color lightColor) {
            if (Item != null && !Item.IsAir && Item.type == NeutronBow.PType) {
                NeutronGlaiveHeldAlt.DrawBar(Owner, Charge / 60f * 80, uiframe);
            }
            Main.EntitySpriteDraw(TextureValue, drawPos, TextureValue.GetRectangle(Projectile.frame, 7), CanFire ? Color.White : lightColor
                , Projectile.rotation, VaultUtils.GetOrig(TextureValue, 7), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
