using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.FlameTurrets
{
    /// <summary>
    /// 火焰塔喷焰:短寿命穿透火舌,速度衰减+判定随行程胀大形成锥形覆盖。
    /// 普通 ModProjectile,由权威端生成,spawn包天然广播;本体不绘制,
    /// 火焰=温度梯度火舌PRT为主+少量Dust垫底,尾段转暗烟,触地留焦痕
    /// </summary>
    internal class FlameTurretFire : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 40;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.aiStyle = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.DamageType = DamageClass.Default;
        }

        public override void AI() {
            //首帧偶发喷焰声,避免每发都响
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.rand.NextBool(6)) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.35f }, Projectile.Center);
                }
            }

            //速度衰减,行程约300px,末端自然停摆
            Projectile.velocity *= 0.96f;

            //判定盒随行程胀大,读作锥形扩散
            float progress = 1f - Projectile.timeLeft / 40f;
            int size = (int)MathHelper.Lerp(16f, 44f, progress);
            if (Projectile.width != size) {
                Projectile.Resize(size, size);
            }

            Lighting.AddLight(Projectile.Center, 0.8f, 0.4f, 0.1f);

            //屏外不发表现粒子(判定照常)
            if (!VaultUtils.IsPointOnScreen(Projectile.Center - Main.screenPosition, 220)) {
                return;
            }

            //火舌本体:温度梯度PRT承载,喷口段更密读作点燃拍
            Vector2 spawnPos = Projectile.position + new Vector2(
                Main.rand.NextFloat(Projectile.width), Main.rand.NextFloat(Projectile.height));
            Vector2 tongueVel = Projectile.velocity * 0.72f + VaultUtils.RandVr(0.7f);
            PRTLoader.NewParticle<PRT_DefFlameTongue>(spawnPos, tongueVel, Color.White,
                0.55f + progress * 0.75f)?.Configure(Main.rand.Next(14, 21));
            if (progress < 0.12f) {
                //出膛头两帧补一口白热,喷口读作更烫
                PRTLoader.NewParticle<PRT_DefFlameTongue>(Projectile.Center,
                    Projectile.velocity * 0.9f + VaultUtils.RandVr(0.4f), Color.White, 0.5f)
                    ?.Configure(Main.rand.Next(10, 14));
            }

            //燃屑:带重力的迸点,读作有质量的火
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_DefEmber>(spawnPos,
                    Projectile.velocity * 0.5f + VaultUtils.RandVr(1.2f), new Color(255, 190, 90),
                    Main.rand.NextFloat(0.4f, 0.8f))?.Configure(Main.rand.Next(16, 28), 0.07f, 0.985f);
            }

            //尾段转暗烟:温度梯度的终点是烟不是突然消失
            if (progress > 0.45f && Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_DefSmoke>(spawnPos,
                    Projectile.velocity * 0.15f + new Vector2(0, -0.5f), new Color(58, 46, 42) * 0.4f,
                    Main.rand.NextFloat(0.16f, 0.28f))?.Configure(Main.rand.Next(30, 46));
            }

            //Dust垫底层:少量补隙
            Dust fire = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                DustID.Torch, Projectile.velocity.X * 0.3f, Projectile.velocity.Y * 0.3f,
                100, default, 1.1f + progress * 1.2f);
            fire.noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //狱火:比普通着火更痛的持续烧灼
            target.AddBuff(BuffID.OnFire3, 180);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //触地焦痕:短存的烧灼残迹+溅起的燃屑,读作持续烧灼过这里
            if (VaultUtils.IsPointOnScreen(Projectile.Center - Main.screenPosition, 220)) {
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_DefScorch>(Projectile.Center, Vector2.Zero,
                        new Color(255, 140, 60), Main.rand.NextFloat(0.55f, 0.9f))
                        ?.Configure(Main.rand.Next(40, 58));
                }
                for (int i = 0; i < 2; i++) {
                    Vector2 vel = -oldVelocity.UnitVector().RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f))
                        * Main.rand.NextFloat(0.8f, 2.4f);
                    PRTLoader.NewParticle<PRT_DefEmber>(Projectile.Center, vel, new Color(255, 170, 70),
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24), 0.12f);
                }
            }
            Projectile.Kill();
            return false;
        }

        //本体不绘制,火焰全由火舌PRT+Dust表现
        public override bool PreDraw(ref Color lightColor) => false;
    }
}
