﻿using CalamityMod;
using CalamityOverhaul.Content.RangedModify.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{

    internal abstract class BaseMagicGun : BaseGun
    {
        /// <summary>
        /// 额外法力恢复延迟，默认为<see cref="Player.maxRegenDelay"/>的对应值
        /// </summary>
        protected float SetRegenDelayValue;

        public sealed override void SetRangedProperty() {
            Projectile.DamageType = DamageClass.Magic;
            SetMagicProperty();
        }

        public virtual void SetMagicProperty() {

        }

        public override void SpanProj() {
            if (CanFire) {
                Owner.manaRegenDelay = 4;
            }
            if (onFire || onFireR) {
                if (ShootCoolingValue <= 0) {
                    if (ForcedConversionTargetAmmoFunc.Invoke()) {
                        AmmoTypes = ToTargetAmmo;
                    }

                    if (SetRegenDelayValue == 0) {
                        SetRegenDelayValue = Owner.maxRegenDelay;
                    }

                    if (Owner.CheckMana(Item)) {
                        SetShootAttribute();

                        if (Projectile.IsOwnedByLocalPlayer()) {
                            if (onFire) {
                                FiringShoot();
                            }
                            if (onFireR) {
                                FiringShootR();
                            }
                            if (Owner.Calamity().luxorsGift || ModOwner.TheRelicLuxor > 0) {
                                LuxirEvent();
                            }
                            if (GlobalItemBehavior) {
                                ItemLoaderInFireSetBaver();
                            }
                        }

                        PostFiringShoot();

                        if (EnableRecoilRetroEffect) {
                            OffsetPos -= ShootVelocity.UnitVector() * RecoilRetroForceMagnitude;
                        }
                        if (FiringDefaultSound) {
                            HanderPlaySound();
                        }
                        if (CanCreateRecoilBool) {
                            CreateRecoil();
                        }
                        if (FireLight > 0) {
                            Lighting.AddLight(ShootPos, VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(0.3f, 0.65f), Color.Red, Color.Gold).ToVector3() * FireLight);
                        }

                        int mana = (int)(Item.mana * Owner.manaCost);
                        Owner.statMana -= mana;
                        Owner.manaRegenDelay = SetRegenDelayValue;
                        if (Owner.statMana < 0) {
                            Owner.statMana = 0;
                        }
                    }

                    ShootCoolingValue += Item.useTime;
                    onFireR = onFire = false;
                    PostShootEverthing();
                }
            }
        }
    }
}
