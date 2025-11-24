using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.MeleeModify.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class HolyColliderHeld : BaseSwing, IWarpDrawable
    {
        public override int TargetID => ModContent.ItemType<HolyCollider>();
        public override string Texture => CWRConstant.Cay_Wap_Melee + "HolyCollider";
        public override string gradientTexturePath => CWRConstant.ColorBar + "HolyCollider_Bar";

        private int chargeTime; //蓄力时间
        private float weaponMomentum; //武器动量，模拟重量感

        public override void SetSwingProperty() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = 132;
            Projectile.height = 132;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 4;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 2 * UpdateRate;
            distanceToOwner = 32;
            drawTrailTopWidth = 70;
            canDrawSlashTrail = true;
            ownerOrientationLock = true;
            Length = 145;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 2) {
                //蓄力重击
                SoundEngine.PlaySound(SoundID.Item125 with { Pitch = 0.6f, Volume = 1.5f }, Projectile.Center);

                Vector2 toMouse2 = Projectile.Center.To(InMousePos);
                float lengs2 = toMouse2.Length();
                if (lengs2 < Length * Projectile.scale) {
                    lengs2 = Length * Projectile.scale;
                }
                Vector2 targetPos2 = Projectile.Center + toMouse2.UnitVector() * lengs2;
                Vector2 unitToM2 = toMouse2.UnitVector();

                //生成强化的火焰冲击
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), targetPos2, unitToM2
                , ModContent.ProjectileType<HolyColliderExFire>(), Projectile.damage / 6, Projectile.knockBack * 2
                , Owner.whoAmI, 1, Projectile.Center.X, Projectile.Center.Y);

                //屏幕震动
                Owner.CWR().GetScreenShake(12f);

                //后坐力效果
                Owner.velocity -= unitToM2 * 3f;

                //额外的爆炸粒子
                for (int i = 0; i < 30; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(10, 10);
                    Dust charge = Dust.NewDustPerfect(targetPos2, DustID.Torch, vel, 0, Color.Orange, 3f);
                    charge.noGravity = true;
                }
                return;
            }

            //普通攻击
            float lengs = ToMouse.Length();
            if (lengs < Length * Projectile.scale) {
                lengs = Length * Projectile.scale;
            }
            Vector2 targetPos = Owner.GetPlayerStabilityCenter() + ToMouse.UnitVector() * lengs;
            Vector2 unitToM = UnitToMouseV;

            //生成火焰斩击
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), targetPos, unitToM
                , ModContent.ProjectileType<HolyColliderExFire>(), Projectile.damage / 6, Projectile.knockBack, Owner.whoAmI);

            //小范围震动
            Owner.CWR().GetScreenShake(4f);
        }

        public override void SwingAI() {
            //武器重量感模拟
            weaponMomentum = MathHelper.Lerp(weaponMomentum, rotSpeed * 10f, 0.1f);

            if (Projectile.ai[0] == 1) {
                //第一击
                SwingBehavior(starArg: 40, baseSwingSpeed: 5, ler1_UpLengthSengs: 0.14f, ler1_UpSpeedSengs: 0.14f, ler1_UpSizeSengs: 0.018f
                , ler2_DownLengthSengs: 0.006f, ler2_DownSpeedSengs: 0.18f, ler2_DownSizeSengs: 0
                , minClampLength: 160, maxClampLength: 240, ler1Time: 0, maxSwingTime: 32);
                maxSwingTime = 32;

                //挥舞音效
                if (Time == 0) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f, Volume = 1.3f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = 0.1f }, Owner.Center);
                }

                //重量感粒子拖尾
                if (Time % 2 == 0 && rotSpeed > 0.1f) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(50, 50);
                    Dust trail = Dust.NewDustPerfect(dustPos, DustID.Torch, -Projectile.velocity * 0.4f, 0, Color.Orange, 2f);
                    trail.noGravity = true;
                }
                return;
            }
            else if (Projectile.ai[0] == 2) {
                Projectile.localNPCHitCooldown = 6 * UpdateRate;
                //蓄力重击
                canDrawSlashTrail = false;
                OtherMeleeSize = 1.05f;

                SwingBehavior(starArg: 40, baseSwingSpeed: 1.8f, ler1_UpLengthSengs: 0.16f, ler1_UpSpeedSengs: 0.16f, ler1_UpSizeSengs: 0.008f
                , ler2_DownLengthSengs: 0.01f, ler2_DownSpeedSengs: 0.22f, ler2_DownSizeSengs: 0
                , minClampLength: 170, maxClampLength: 250, ler1Time: 8, maxSwingTime: 80);
                maxSwingTime = 80;


                //蓄力音效
                if (Time == 0) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.5f, Volume = 1.5f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = -0.4f, Volume = 0.7f }, Owner.Center);
                }

                //蓄力进度
                chargeTime++;
                float chargeProgress = MathHelper.Clamp(chargeTime / 60f, 0f, 1f);

                //蓄力粒子环绕效果
                if (Time % 3 == 0) {
                    float angle = Time * 0.15f;
                    Vector2 offset = angle.ToRotationVector2() * (60 + chargeProgress * 40);
                    Dust chargeDust = Dust.NewDustPerfect(Projectile.Center + offset, DustID.Torch,
                        -offset * 0.05f, 0, Color.OrangeRed, 2f + chargeProgress);
                    chargeDust.noGravity = true;
                }

                //蓄力完成的额外效果
                if (chargeProgress >= 0.8f && Time % 5 == 0) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 sparkPos = Projectile.Center + Main.rand.NextVector2Circular(80, 80);
                        Dust spark = Dust.NewDustPerfect(sparkPos, DustID.Torch, Vector2.Zero, 0, Color.Gold, 2.5f);
                        spark.noGravity = true;
                    }
                }

                //挥击瞬间的爆发效果
                if (Time == (int)(maxSwingTime * 0.65f)) {
                    Owner.CWR().GetScreenShake(10f);
                }
                return;
            }
            else {
                chargeTime = 0;
            }

            //普通攻击
            SwingBehavior(starArg: 40, baseSwingSpeed: 3.5f, ler1_UpLengthSengs: 0.13f, ler1_UpSpeedSengs: 0.13f, ler1_UpSizeSengs: 0.015f
                , ler2_DownLengthSengs: 0.006f, ler2_DownSpeedSengs: 0.12f, ler2_DownSizeSengs: 0
                , minClampLength: 0, maxClampLength: 0, ler1Time: 10, maxSwingTime: 36);

            //挥舞音效
            if (Time == 0) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 1.1f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing, Owner.Center);
            }

            //火焰拖尾
            if (Time % 2 == 0 && Main.rand.NextBool()) {
                Dust fire = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch);
                fire.velocity *= 0.4f;
                fire.scale = 1.8f;
                fire.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.localNPCHitCooldown = target.IsWormBody() ? -1 : 2 * UpdateRate;

            //击中音效
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundMiss with { Pitch = 0.3f, Volume = 0.8f }, target.Center);

            //基于攻击类型的不同效果
            if (Projectile.ai[0] == 2) {
                //蓄力重击的爆炸效果
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.2f }, target.Center);
                Owner.CWR().GetScreenShake(8f);

                //强力火花爆发
                for (int i = 0; i < 20; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(10, 10);
                    Dust explosion = Dust.NewDustPerfect(target.Center, DustID.Torch, vel, 0, Color.OrangeRed, 3f);
                    explosion.noGravity = true;
                }
            }
            else {
                //普通击中
                Owner.CWR().GetScreenShake(3f);

                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(5, 5);
                    Dust hitDust = Dust.NewDustPerfect(target.Center, DustID.Torch, vel, 0, Color.Orange, 2f);
                    hitDust.noGravity = true;
                }
            }

            //添加燃烧debuff
            target.AddBuff(BuffID.OnFire3, 240);
            if (hit.Crit) {
                target.AddBuff(BuffID.OnFire3, 480);
            }
        }

        bool IWarpDrawable.CanDrawCustom() => true;

        bool IWarpDrawable.DontUseBlueshiftEffect() => true;

        void IWarpDrawable.Warp() => WarpDraw();

        void IWarpDrawable.DrawCustom(SpriteBatch spriteBatch) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle rect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 toOwner = Projectile.Center - Owner.GetPlayerStabilityCenter();
            Vector2 offsetOwnerPos = toOwner.GetNormalVector() * 16 * Projectile.spriteDirection * MeleeSize;
            Vector2 pos = Projectile.Center - RodingToVer(48, toOwner.ToRotation()) + offsetOwnerPos;
            Vector2 drawPos = pos - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY;

            float drawRoting = Projectile.rotation;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }

            //蓄力状态的光效增强
            if (Projectile.ai[0] == 2) {
                float chargeIntensity = MathHelper.Clamp(chargeTime / 60f, 0f, 1f);
                Color glowColor = Color.Lerp(Color.Gold, Color.OrangeRed, chargeIntensity) * (0.5f + chargeIntensity * 0.3f);

                //多层光晕营造蓄力感
                VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, Time, drawPos, null, glowColor * 0.8f
                    , drawRoting, drawOrigin, Projectile.scale * MeleeSize * (1f + chargeIntensity * 0.15f),
                    DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);

                VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, (int)Math.Round(Time * 1.5f), drawPos, null, Color.Gold * 0.4f * chargeIntensity
                    , drawRoting, drawOrigin, Projectile.scale * MeleeSize * (1.1f + chargeIntensity * 0.2f),
                    DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }
            else if (weaponMomentum > 5f) {
                //普通挥舞的动态光效
                float momentum = MathHelper.Clamp(weaponMomentum / 20f, 0f, 1f);
                VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, Time, drawPos, null, Color.Orange * 0.4f * momentum
                    , drawRoting, drawOrigin, Projectile.scale * MeleeSize * (1f + momentum * 0.05f),
                    DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            }

            Main.EntitySpriteDraw(texture, drawPos, new Rectangle?(rect), Color.White
                , drawRoting, drawOrigin, Projectile.scale * MeleeSize, effects, 0);
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) { }
    }
}
