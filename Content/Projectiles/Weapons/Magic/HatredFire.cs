using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class HatredFire : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Magic + "NofaceFire";

        private Player Owner => CWRUtils.GetPlayerInstance(Projectile.owner);

        private ref float Time => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 42;
            Projectile.height = 42;
            Projectile.friendly = true;
            Projectile.alpha = 0;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.frameCounter = Main.rand.Next(4);
        }

        public void SpanDust() {
            for (int i = 0; i <= 360; i += 3) {
                Vector2 vr = new Vector2(3f, 3f).RotatedBy(MathHelper.ToRadians(i));
                int num = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height
                    , DustID.RedTorch, vr.X, vr.Y, 200, new Color(232, 251, 250, 200), 1.4f);
                Main.dust[num].noGravity = true;
                Main.dust[num].position = Projectile.Center;
                Main.dust[num].velocity = vr;
            }
        }

        public override void AI() {
            CWRUtils.ClockFrame(ref Projectile.frameCounter, 5, 3);
            Projectile.alpha += 3;
            if (Projectile.alpha > 255)
                Projectile.alpha = 255;

            if (Owner == null) {
                Projectile.Kill();
                return;
            }

            float sengs = Math.Abs(MathF.Sin(Projectile.timeLeft * CWRUtils.atoR * 6));
            float maxShaking = 20;
            Projectile.scale = 1 + sengs * 0.5f;
            Projectile.position -= new Vector2(0, sengs * 0.5f);

            Projectile.rotation += Math.Sign(Projectile.velocity.X) * 0.05f;
            if (Projectile.rotation > MathHelper.ToRadians(maxShaking))
                Projectile.rotation = MathHelper.ToRadians(maxShaking);
            if (Projectile.rotation < MathHelper.ToRadians(-maxShaking))
                Projectile.rotation = MathHelper.ToRadians(-maxShaking);

            //在一定时间后开始追踪附近的敌人
            NPC target = Projectile.Center.FindClosestNPC(1900);
            if (target != null && Projectile.timeLeft < 1080) {
                Projectile.ChasingBehavior2(target.Center + Projectile.velocity * 100, 1, 0.1f);

                Vector2 toTarget = Projectile.Center.To(target.Center);

                if (toTarget.LengthSquared() > 400 * 400) {
                    Projectile.alpha -= 7;//对于透明度的衰减需要大于默认的全局增加速度
                }

                if (Projectile.IsOwnedByLocalPlayer()) {
                    if (Projectile.alpha <= 0) {
                        SpanDust();
                        Projectile.Center = target.Center + Main.rand.NextVector2Unit() * 200;
                        Projectile.netUpdate = true;
                        Projectile.netUpdate2 = true;
                        SpanDust();
                    }

                    if (toTarget.LengthSquared() < 600 * 600) {
                        int types = ModContent.ProjectileType<FateCluster>();
                        if (Time % 15 == 0 && Owner.ownedProjectileCounts[types] <= 36) {
                            Vector2 vr = toTarget.UnitVector() * 13;
                            Projectile.NewProjectile(
                                Owner.parent(),
                                Projectile.Center + Main.rand.NextVector2Unit() * 16,
                                vr,
                                types,
                                Projectile.damage,
                                0,
                                Owner.whoAmI,
                                3
                                );

                            Projectile.timeLeft -= 30;
                        }
                    }
                }
            }

            Time++;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);

            Projectile.position = Projectile.Center;
            Projectile.width = Projectile.height = 84;
            Projectile.position.X = Projectile.position.X - Projectile.width / 2;
            Projectile.position.Y = Projectile.position.Y - Projectile.height / 2;
            Projectile.maxPenetrate = -1;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.Damage();

            // 生成爆炸粒子，从各种意义上讲，这段代码是不讨喜的
            int maxFore;
            for (int i = 0; i < 3; i = maxFore + 1) {
                int dust0 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 100, default, 1.5f);
                Main.dust[dust0].position = Projectile.Center + Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * Projectile.width / 2f;
                maxFore = i;
            }
            for (int j = 0; j < 10; j = maxFore + 1) {
                int dust2 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 0, default, 2.5f);
                Main.dust[dust2].position = Projectile.Center + Vector2.UnitY.RotatedByRandom(3.1415927410125732) * (float)Main.rand.NextDouble() * Projectile.width / 2f;
                Main.dust[dust2].noGravity = true;
                Dust dust = Main.dust[dust2];
                dust.velocity *= 2f;
                maxFore = j;
            }
            for (int k = 0; k < 5; k = maxFore + 1) {
                int dust3 = Dust.NewDust(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.RedTorch, 0f, 0f, 0, default, 1.5f);
                Main.dust[dust3].position = Projectile.Center + Vector2.UnitX.RotatedByRandom(3.1415927410125732).RotatedBy((double)Projectile.velocity.ToRotation(), default) * Projectile.width / 2f;
                Main.dust[dust3].noGravity = true;
                Dust dust = Main.dust[dust3];
                dust.velocity *= 2f;
                maxFore = k;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(
                mainValue,
                Projectile.Center - Main.screenPosition,
                CWRUtils.GetRec(mainValue, Projectile.frameCounter, 4),
                Color.White * (Projectile.alpha / 255f),
                Projectile.rotation,
                CWRUtils.GetOrig(mainValue, 4),
                new Vector2(1, Projectile.scale),
                SpriteEffects.None
                );
            return false;
        }
    }
}
