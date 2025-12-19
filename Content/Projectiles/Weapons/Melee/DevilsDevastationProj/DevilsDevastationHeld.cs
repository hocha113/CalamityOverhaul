using CalamityOverhaul.Content.MeleeModify.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DevilsDevastationProj
{
    internal class DevilsDevastationHeld : BaseSwing
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "DevilsDevastation";
        public override string GlowTexturePath => CWRConstant.Masking + "SplitTrail";
        public override string gradientTexturePath => CWRConstant.ColorBar + "DevilsDevastation_Bar";
        public override int TargetID => CWRID.Item_DevilsDevastation;
        public override void SetSwingProperty() {
            drawTrailHighlight = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = 122;
            Projectile.height = 122;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 4;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            distanceToOwner = 34;
            drawTrailTopWidth = 12;
            canDrawSlashTrail = true;
            ownerOrientationLock = true;
            Length = 120;
        }

        internal void EXDemonBlastAltEffect() {
            SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.72f, Volume = 2.5f }, Projectile.Center);

            float lengs = ToMouse.Length();
            if (lengs < Length * Projectile.scale) {
                lengs = Length * Projectile.scale;
            }
            Vector2 targetPos = Owner.GetPlayerStabilityCenter() + ToMouse.UnitVector() * lengs;
            Vector2 unitToM = UnitToMouseV;

            //增强冲击波粒子效果
            for (int i = 0; i < lengs / 10; i++) {
                Vector2 spwanPos = Owner.GetPlayerStabilityCenter() + unitToM * (1 + i) * 10;
                Dust dust = Dust.NewDustPerfect(spwanPos, DustID.Blood, UnitToMouseV * 8, 125, Color.OrangeRed, 3.5f);
                dust.noGravity = true;
                dust.scale = 6;

                //添加火焰粒子
                if (i % 2 == 0) {
                    Dust flame = Dust.NewDustPerfect(spwanPos, DustID.Torch, UnitToMouseV * 5, 100, Color.Red, 2.5f);
                    flame.noGravity = true;
                }
            }

            //增强爆炸粒子环
            int bloodQuantity = 12;
            for (int i = 0; i < bloodQuantity; i++) {
                Vector2 target = ((float)Math.PI * 2 / bloodQuantity * i + 1).ToRotationVector2() * 350;
                int bloodQuantity2 = 180;
                for (int a = 1; a <= bloodQuantity2; a++) {
                    if (Main.rand.NextBool(2)) {
                        Dust.NewDust(InMousePos, 0, 0, DustID.Blood, target.X / a, target.Y / a, 0, default, 3.5f);
                    }
                }
            }

            //屏幕震动
            Owner.CWR().GetScreenShake(12f);
        }

        internal bool Has16EXDemonBlast() {
            int num = 0;
            foreach (var proj in Main.projectile) {
                if (!proj.active) {
                    continue;
                }
                if (proj.type != ModContent.ProjectileType<EXDemonBlast>()) {
                    continue;
                }
                if (proj.ai[0] == 0 || proj.ai[0] == 1) {
                    num++;
                }
            }
            return num >= 16;
        }

        public override void PostInOwner() {
            if (canShoot && Projectile.ai[0] == 3) {
                EXDemonBlastAltEffect();
            }
            if (Projectile.ai[0] != 3 && !Has16EXDemonBlast()
                && Time > 5 * UpdateRate && Time < 60 * UpdateRate && Time % 10 * UpdateRate == 0
                && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Source, Owner.Center, UnitToMouseV * 6
                , ModContent.ProjectileType<EXDemonBlast>(), Projectile.damage / 8, Projectile.knockBack, Owner.whoAmI);
            }
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 2 || Projectile.ai[0] == 1) {
                //增强刀刃弹幕，更快更强
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Source, Owner.Center, UnitToMouseV.RotatedBy((-1 + i) * 0.08f) * 8
                        , ModContent.ProjectileType<EXOathblade>(), Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI, 1);
                }

                //添加击打音效
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, Owner.Center);
                return;
            }

            if (Projectile.ai[0] == 3) {
                //终结技发射增强效果
                for (int i = 0; i < 6; i++) {
                    Projectile.NewProjectile(Source, InMousePos, (MathHelper.TwoPi / 6 * i).ToRotationVector2() * 4
                    , ModContent.ProjectileType<EXDemonBlastAlt>(), Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI);
                }
                return;
            }
        }

        public override void SwingAI() {
            if (Time == 0) {
                //更有力的挥舞音效
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.1f, Volume = 1.2f }, Owner.position);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = 0.3f, Volume = 0.8f }, Owner.position);
            }

            if (Projectile.ai[0] == 1) {
                //第一击 - 快速上挑
                SwingBehavior(starArg: 63, baseSwingSpeed: 7, ler1_UpLengthSengs: 0.12f, ler1_UpSpeedSengs: 0.15f, ler1_UpSizeSengs: 0.068f
                , ler2_DownLengthSengs: 0.008f, ler2_DownSpeedSengs: 0.16f, ler2_DownSizeSengs: 0
                , minClampLength: 165, maxClampLength: 230, ler1Time: 16, maxSwingTime: 28);

                //添加挥舞尾迹粒子
                if (Time % 2 == 0 && Main.rand.NextBool(2)) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(40, 40);
                    Dust swing = Dust.NewDustPerfect(dustPos, DustID.ShadowbeamStaff, -Projectile.velocity * 0.3f, 0, Color.Purple, 1.5f);
                    swing.noGravity = true;
                }
                return;
            }
            else if (Projectile.ai[0] == 2) {
                //第二击 - 反向横扫
                SwingBehavior(starArg: 63, baseSwingSpeed: -7, ler1_UpLengthSengs: 0.12f, ler1_UpSpeedSengs: 0.15f, ler1_UpSizeSengs: 0.068f
                , ler2_DownLengthSengs: 0.008f, ler2_DownSpeedSengs: 0.16f, ler2_DownSizeSengs: 0
                , minClampLength: 165, maxClampLength: 230, ler1Time: 16, maxSwingTime: 28);

                //添加挥舞尾迹粒子
                if (Time % 2 == 0 && Main.rand.NextBool(2)) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(40, 40);
                    Dust swing = Dust.NewDustPerfect(dustPos, DustID.ShadowbeamStaff, -Projectile.velocity * 0.3f, 0, Color.Purple, 1.5f);
                    swing.noGravity = true;
                }
                return;
            }
            else if (Projectile.ai[0] == 3) {
                //终结技 - 蓄力重击
                shootSengs = 0.28f;
                maxSwingTime = 65;
                canDrawSlashTrail = false;
                SwingBehavior(starArg: 13, baseSwingSpeed: 2.5f, ler1_UpLengthSengs: 0.12f, ler1_UpSpeedSengs: 0.12f, ler1_UpSizeSengs: 0.075f
                , ler2_DownLengthSengs: 0.008f, ler2_DownSpeedSengs: 0.18f, ler2_DownSizeSengs: 0
                , minClampLength: 170, maxClampLength: 210, ler1Time: 6, maxSwingTime: 55);

                //蓄力阶段的粒子效果
                if (Time < maxSwingTime * shootSengs) {
                    float chargeProgress = Time / (maxSwingTime * shootSengs);
                    if (Main.rand.NextBool(3)) {
                        Vector2 offset = Main.rand.NextVector2Circular(60, 60) * chargeProgress;
                        Dust charge = Dust.NewDustPerfect(Projectile.Center + offset, DustID.Blood,
                            -offset * 0.08f, 0, Color.OrangeRed, 2f * chargeProgress);
                        charge.noGravity = true;
                    }
                }

                //屏幕震动
                if (Time == (int)(maxSwingTime * shootSengs)) {
                    Owner.CWR().GetScreenShake(8f);
                }
                return;
            }

            //普通攻击 - 更快速流畅
            SwingBehavior(starArg: 63, baseSwingSpeed: 7.5f, ler1_UpLengthSengs: 0.12f, ler1_UpSpeedSengs: 0.12f, ler1_UpSizeSengs: 0.028f
                , ler2_DownLengthSengs: 0.008f, ler2_DownSpeedSengs: 0.16f, ler2_DownSizeSengs: 0
                , minClampLength: 0, maxClampLength: 0, ler1Time: 6, maxSwingTime: 18);

            //环境粒子效果
            if (Main.rand.NextBool(2)) {
                Dust ambient = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.ShadowbeamStaff);
                ambient.velocity *= 0.5f;
                ambient.scale = 1.2f;
                ambient.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.ShadowFlame, 180);
            target.AddBuff(BuffID.OnFire, 360);
            if (hit.Crit) {
                target.AddBuff(BuffID.ShadowFlame, 540);
                target.AddBuff(BuffID.OnFire, 1080);

                //暴击特效
                SoundEngine.PlaySound(SoundID.NPCHit53 with { Pitch = -0.3f }, target.Center);
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                    Dust crit = Dust.NewDustPerfect(target.Center, DustID.Blood, vel, 0, Color.OrangeRed, 2f);
                    crit.noGravity = true;
                }
            }

            //添加击打音效和轻微震动
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundMiss with { Pitch = 0.5f, Volume = 0.6f }, target.Center);
            Owner.CWR().GetScreenShake(2f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(CWRID.Buff_Shadowflame, 540);
            target.AddBuff(BuffID.OnFire, 1080);
            SoundEngine.PlaySound(SoundID.Item14, target.Center);
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle rect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 toOwner = Projectile.Center - Owner.GetPlayerStabilityCenter();
            Vector2 offsetOwnerPos = toOwner.GetNormalVector() * -6 * Projectile.spriteDirection;
            Vector2 pos = Projectile.Center - RodingToVer(48, toOwner.ToRotation()) + offsetOwnerPos;
            Vector2 drawPos = pos - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY;

            float drawRoting = Projectile.rotation;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }

            //增强光效
            if (Projectile.ai[0] != 0) {
                float intensity = Projectile.ai[0] == 3 ? 1.2f : 0.8f;
                VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, Projectile.timeLeft, drawPos
                , null, Color.Red * intensity, drawRoting, drawOrigin, Projectile.scale * 1.05f, effects);
            }

            Main.EntitySpriteDraw(texture, drawPos, new Rectangle?(rect), Color.White
                , drawRoting, drawOrigin, Projectile.scale * MeleeSize, effects, 0);
        }
    }
}
