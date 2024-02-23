﻿using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class TyrannysEndHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "TyrannysEnd";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.TyrannysEnd>();
        public override int targetCWRItem => ModContent.ItemType<TyrannysEnd>();
        /// <summary>
        /// 装弹提醒，一般来讲会依赖左键的按键事件进行更新
        /// </summary>
        protected bool loadingReminder;
        /// <summary>
        /// 是否开始装弹
        /// </summary>
        protected bool onKreload;
        /// <summary>
        /// 是否已经装好了弹药
        /// </summary>
        protected bool isKreload;
        /// <summary>
        /// 装弹计时器
        /// </summary>
        protected int kreloadTime;

        public override void SetRangedProperty() {
            ControlForce = 0.04f;
            GunPressure = 0.85f;
            Recoil = 13f;
            HandDistance = 40;
        }

        protected virtual SoundStyle loadTheRounds => CWRSound.CaseEjection2;

        public override void InOwner() {
            if (!CWRKeySystem.ADS_Key.Old)
                Owner.scope = false;

            if (heldItem.type != ItemID.None)
                isKreload = heldItem.CWR().IsKreload;

            ArmRotSengsFront = 30 * CWRUtils.atoR;
            ArmRotSengsBack = 150 * CWRUtils.atoR;

            Projectile.Center = Owner.Center + new Vector2(DirSign * HandDistance, 5);
            Projectile.rotation = DirSign > 0 ? MathHelper.ToRadians(10) : MathHelper.ToRadians(170);
            Projectile.timeLeft = 2;
            SetHeld();

            if (!Owner.mouseInterface) {
                if (Owner.PressKey()) {
                    Owner.direction = ToMouse.X > 0 ? 1 : -1;
                    Projectile.rotation = GunOnFireRot;
                    Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * (HandDistance + 5) + new Vector2(0, -5);
                    ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation)) * DirSign;
                    if (HaveAmmo && isKreload) {//并进需要子弹，还需要判断是否已经装弹
                        onFire = true;
                        Projectile.ai[1]++;
                    }
                }
                else {
                    onFire = false;
                }

                if (Owner.PressKey(false) && !isKreload && kreloadTime == 0) {//如果没有装弹，那么按下右键时开始装弹
                    onKreload = true;
                    kreloadTime = heldItem.useTime;
                }

                if (onKreload) {//装弹过程
                    ArmRotSengsFront = (MathHelper.PiOver2 - (Projectile.rotation)) * DirSign + 0.3f;
                    ArmRotSengsFront += MathF.Sin(Time * 0.3f) * 0.7f;
                    kreloadTime--;
                    if (kreloadTime == heldItem.useTime - 1) {
                        SoundEngine.PlaySound(CWRSound.CaseEjection with { Volume = 0.6f }, Projectile.Center);
                    }
                    if (kreloadTime == heldItem.useTime / 2) {
                        SoundEngine.PlaySound(loadTheRounds, Projectile.Center);
                        Vector2 vr = (Projectile.rotation - Main.rand.NextFloat(-0.1f, 0.1f) * DirSign).ToRotationVector2() * -Main.rand.NextFloat(3, 7) + Owner.velocity;
                        int proj = Projectile.NewProjectile(Projectile.parent(), Projectile.Center, vr, ModContent.ProjectileType<GunCasing>(), 10, Projectile.knockBack, Owner.whoAmI);
                        Main.projectile[proj].scale = 2;
                    }
                    if (kreloadTime == heldItem.useTime / 3) {
                        UpdateConsumeAmmo();
                    }
                    if (kreloadTime <= 0) {//时间完成后设置装弹状态并准备下一次发射
                        onKreload = false;
                        isKreload = true;
                        heldItem.CWR().IsKreload = true;
                        kreloadTime = 0;
                    }
                }

                if (Owner.PressKey()) {
                    if (!isKreload && loadingReminder) {
                        SoundEngine.PlaySound(CWRSound.Ejection, Projectile.Center);
                        CombatText.NewText(Owner.Hitbox, Color.Gold, CWRLocText.GetTextValue("CaseEjection_TextContent"));
                        loadingReminder = false;
                    }
                }
                else {
                    loadingReminder = true;
                }
            }
        }

        public virtual void OnSpanProjFunc() {
            SoundEngine.PlaySound(heldItem.UseSound, Projectile.Center);
            DragonsBreathRifleHeldProj.SpawnGunDust(Projectile, Projectile.Center, ShootVelocity);
            Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<BMGBullet>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void SpanProj() {
            if (onFire && Projectile.ai[1] > 10) {
                OnSpanProjFunc();
                if (Owner.Calamity().luxorsGift || Owner.CWR().theRelicLuxor > 0) {
                    LuxirEvent();//因为重写了SpanProj，所以这里需要手动调用一次
                }
                CreateRecoil();
                loadingReminder = false;//在发射后设置一下装弹提醒开关，防止进行一次有效射击后仍旧弹出提示
                isKreload = false;
                heldItem.CWR().IsKreload = false;
                Projectile.ai[1] = 0;
                onFire = false;
            }
        }
    }
}
