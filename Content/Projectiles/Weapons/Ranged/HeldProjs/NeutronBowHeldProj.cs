using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.NeutronBowProjs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class NeutronBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronBow";
        public override int targetCayItem => NeutronBow.PType;
        public override int targetCWRItem => NeutronBow.PType;

        private float Charge {
            get => ((NeutronBow)Item.ModItem).Charge;
            set => ((NeutronBow)Item.ModItem).Charge = value;
        }

        private int uiframe;
        private bool level1 = true;
        private bool level2 = true;
        private bool level3 = true;
        public override bool IsLoadingEnabled(Mod mod) {
            return true;//暂时不要在这个版本中出现
        }
        public override void SetRangedProperty() {
            ForcedConversionTargetAmmoFunc = () => true;
            ISForcedConversionDrawAmmoInversion = true;
            ToTargetAmmo = ModContent.ProjectileType<NeutronArrow>();
            HandDistance = 15;
            HandFireDistance = 22;
            DrawArrowMode = -25;
            CanRightClick = true;
        }

        private void NewText(string key, int offsetY = 0) {
            Rectangle rectext = Owner.Hitbox;
            rectext.Y -= offsetY;
            CombatText.NewText(rectext, new Color(155, 200, 100 + offsetY), CWRLocText.GetTextValue(key), true);
        }

        public override void PostInOwner() {
            if (CanFire) {
                if (onFireR) {
                    if (Charge < 80) {
                        Charge += 0.5f;
                        Item.useTime = 20;
                        ShootCoolingValue = Charge / 4;
                        BowArrowDrawNum = 1;
                        if (ShootCoolingValue > 2) {
                            if (level1) {
                                NewText("Wap_NeutronBow_LoadingText1", 0);
                                SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = -0.3f, Volume = 0.6f }, Projectile.Center);
                                level1 = false;
                            }
                        }
                        if (Charge > 30) {
                            if (level2) {
                                NewText("Wap_NeutronBow_LoadingText2", 60);
                                SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);
                                level2 = false;
                            }
                            BowArrowDrawNum++;
                        }
                        if (Charge > 60) {
                            if (level3) {
                                NewText("Wap_NeutronBow_LoadingText3", 120);
                                SoundEngine.PlaySound(CWRSound.loadTheRounds with { Pitch = -0.1f, Volume = 0.8f }, Projectile.Center);
                                level3 = false;
                            }
                            BowArrowDrawNum++;
                        }
                    }
                    else {
                        BowArrowDrawNum = 3;
                    }
                }
                else {
                    BowArrowDrawNum = 1;
                    Charge = 0;
                    level1 = level2 = level3 = true;
                }
                CWRUtils.ClockFrame(ref Projectile.frame, 2, 15);
                CWRUtils.ClockFrame(ref uiframe, 5, 6);
            }
            else {
                BowArrowDrawNum = 1;
                Charge = 0;
                Projectile.frame = 0;
                uiframe = 0;
                level1 = level2 = level3 = true;
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
            ModOwner.SetScreenShake(6.2f);
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

        public override void BowDraw(ref Color lightColor) {
            if (Item != null && !Item.IsAir && Item.type == NeutronBow.PType) {
                NeutronGlaiveHeld.DrawBar(Owner, Charge, uiframe);
            }
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(TextureValue, Projectile.frame, 16), CanFire ? Color.White : lightColor
                , Projectile.rotation, CWRUtils.GetOrig(TextureValue, 16), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
