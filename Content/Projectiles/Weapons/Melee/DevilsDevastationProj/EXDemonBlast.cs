using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DevilsDevastationProj
{
    internal class EXDemonBlast : BaseHeldProj
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "DemonBlast";
        public Vector2 MoveVector2;
        public Vector2 ChargeBackPosition; //蓄力后退位置
        public Vector2 pos = new Vector2(0, -1);
        public ref float Rand => ref Projectile.localAI[0];
        private int Time;
        private int ChargeTime; //蓄力时间
        private bool isCharging = true; //是否正在蓄力
        private bool hasLaunched; //是否已经发射
        private float chargeIntensity; //蓄力强度

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DontCancelChannelOnKill[Type] = true;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.penetrate = 1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
            Projectile.penetrate = 16;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Rand = Main.rand.Next(50, 100);
            double angle = Main.rand.NextDouble() * 2d * Math.PI;
            MoveVector2.X = (float)(Math.Sin(angle) * Rand);
            MoveVector2.Y = (float)(Math.Cos(angle) * Rand);
            Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Projectile.spriteDirection = Main.rand.NextBool() ? 1 : -1;
            Projectile.CWR().Viscosity = true;
        }

        public override bool? CanCutTiles() => Projectile.ai[0] != 0;

        public override bool? CanHitNPC(NPC target) => !target.friendly && Projectile.ai[0] != 0 ? null : false;

        public override void AI() {
            if (Time == 0) {
                pos = UnitToMouseV * 2;
                //计算蓄力后退位置（在玩家身后）
                Vector2 directionToMouse = Projectile.DirectionTo(InMousePos);
                ChargeBackPosition = Owner.GetPlayerStabilityCenter() - directionToMouse.RotatedBy(Rand / 60f - MathHelper.Pi / 2) * 180;
            }

            //蓄力阶段
            if (isCharging && Projectile.ai[0] == 0) {
                ChargeTime++;
                chargeIntensity = Math.Min(ChargeTime / 15f, 1f); //蓄力强度逐渐增加

                //后退蓄力动画
                Vector2 targetPosition = Vector2.Lerp(
                    Owner.GetPlayerStabilityCenter() + MoveVector2,
                    ChargeBackPosition,
                    CWRUtils.EaseOutBack(chargeIntensity)
                );

                Projectile.position = targetPosition;

                //蓄力旋转效果
                Projectile.rotation = ToMouseA + MathHelper.PiOver4;

                //蓄力粒子效果
                if (Main.rand.NextBool(2)) {
                    Vector2 offset = Main.rand.NextVector2Circular(20, 20) * chargeIntensity;
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + offset, DustID.ShadowbeamStaff,
                        -offset * 0.1f, 0, Color.Purple, 1.5f * chargeIntensity);
                    dust.noGravity = true;
                }

                //完成蓄力
                if (ChargeTime >= 25 || !DownLeft) {
                    isCharging = false;
                    SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact with {
                        Pitch = -0.3f,
                        Volume = 0.8f
                    }, Projectile.position);

                    //爆发式冲刺
                    Vector2 launchDirection = Projectile.DirectionTo(InMousePos);
                    Projectile.velocity = launchDirection * (20 + chargeIntensity * 10);
                    Projectile.extraUpdates = 3;
                    Projectile.ai[0] = 1;
                    hasLaunched = true;

                    //发射时的震撼效果
                    for (int i = 0; i < 20; i++) {
                        Vector2 vel = Main.rand.NextVector2Circular(8, 8);
                        Dust shockDust = Dust.NewDustPerfect(Projectile.Center, DustID.ShadowbeamStaff,
                            vel, 0, Color.Purple, 2f);
                        shockDust.noGravity = true;
                    }
                }
            }
            //发射后阶段
            else if (hasLaunched && Projectile.ai[0] == 1) {
                //快速旋转以增强力量感
                float targetRot = Projectile.velocity.ToRotation();
                Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetRot + MathHelper.PiOver4, 0.25f);

                //拖尾粒子效果
                if (Main.rand.NextBool()) {
                    Dust trail = Dust.NewDustPerfect(Projectile.Center, DustID.ShadowbeamStaff,
                        -Projectile.velocity * 0.3f, 0, Color.Pink, 1.8f);
                    trail.noGravity = true;
                }
            }

            //透明度处理
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 8;
            }

            Time++;
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode();

            //增强爆炸效果
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode, Projectile.position);

            //爆炸粒子
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(12, 12);
                Dust explosion = Dust.NewDustPerfect(Projectile.Center, DustID.ShadowbeamStaff,
                    velocity, 0, Color.Purple, Main.rand.NextFloat(2f, 3f));
                explosion.noGravity = true;
            }

            CWRUtils.SplashDust(Projectile, 21, DustID.ShadowbeamStaff, DustID.ShadowbeamStaff, 13, Main.DiscoColor);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() / 2;

            //绘制拖尾
            if (Projectile.ai[0] == 1) {
                for (int k = 0; k < Projectile.oldPos.Length; k++) {
                    Vector2 offsetPos = Projectile.oldPos[k].To(Projectile.position);
                    Vector2 drawPos = Projectile.Center - Main.screenPosition - offsetPos;
                    float trailAlpha = (Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length;
                    Color color = Color.Lerp(Color.Pink, Color.Purple, k / (float)Projectile.oldPos.Length) * trailAlpha;
                    Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin,
                        Projectile.scale * (1f - k * 0.05f), SpriteEffects.None, 0);
                }
            }

            //蓄力光效
            if (isCharging && chargeIntensity > 0) {
                Color chargeGlow = Color.Purple * chargeIntensity * 0.6f;
                VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, Time,
                    Projectile.Center - Main.screenPosition, null, chargeGlow, Projectile.rotation,
                    drawOrigin, Projectile.scale * (1f + chargeIntensity * 0.3f), 0);
            }

            //主体绘制
            Color mainColor = isCharging ? Color.Lerp(lightColor, Color.Purple, chargeIntensity * 0.5f) : lightColor;
            VaultUtils.DrawRotatingMarginEffect(Main.spriteBatch, texture, Time,
                Projectile.Center - Main.screenPosition, null, Color.Pink, Projectile.rotation,
                drawOrigin, Projectile.scale, 0);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null,
                Projectile.GetAlpha(mainColor), Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
