using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.FlameTurrets
{
    /// <summary>
    /// 火焰塔喷焰:短寿命穿透火舌,速度衰减+判定随行程胀大形成锥形覆盖。
    /// 普通 ModProjectile,由权威端生成,spawn包天然广播;本体不绘制,火焰全由Dust表现
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

            //火舌本体:火焰Dust为主,少量烟尾
            for (int i = 0; i < 2; i++) {
                Dust fire = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Torch, Projectile.velocity.X * 0.3f, Projectile.velocity.Y * 0.3f,
                    100, default, 1.2f + progress * 1.4f);
                fire.noGravity = true;
            }
            if (Main.rand.NextBool(5)) {
                Dust smoke = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Smoke, 0f, -0.6f, 140, default, 0.9f);
                smoke.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //狱火:比普通着火更痛的持续烧灼
            target.AddBuff(BuffID.OnFire3, 180);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.Kill();
            return false;
        }

        //本体不绘制,火焰全由Dust表现
        public override bool PreDraw(ref Color lightColor) => false;
    }
}
