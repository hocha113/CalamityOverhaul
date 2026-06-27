using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles
{
    /// <summary>魔焰眼火舌，替代原版<see cref="ProjectileID.EyeFire"/>短寿命喷吐，随飞行膨胀，纯PRT火焰流，近距压制，可被物块挡</summary>
    internal class CursedFlameJet : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = 48;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.ai[0]++;
            float progress = Projectile.ai[0] / 96f;

            //火舌随飞行扩散减速
            Projectile.velocity *= 0.985f;

            //碰撞箱随火焰扩散增大
            if (Projectile.ai[0] == 30 || Projectile.ai[0] == 60) {
                Projectile.Resize(Projectile.width + 12, Projectile.height + 12);
            }

            Lighting.AddLight(Projectile.Center, 1f, 0.45f, 0.1f);

            if (VaultUtils.isServer) {
                return;
            }

            //核心火焰帧动画粒子
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_HellFire>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f + progress * 14f, 8f + progress * 14f),
                    Projectile.velocity * Main.rand.NextFloat(0.3f, 0.6f),
                    Color.White, Main.rand.NextFloat(0.5f, 0.8f) + progress * 0.6f);
            }

            //岩浆余烬
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_LavaFire>(
                    Projectile.Center + VaultUtils.RandVr(6f + progress * 10f),
                    Projectile.velocity * 0.25f + Main.rand.NextVector2Circular(1f, 1f),
                    Color.White, Main.rand.NextFloat(0.7f, 1.1f))?.SetLifetime(8, 18);
            }

            //热浪柔光
            if (Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_TwinsSpark>(Projectile.Center,
                    Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Color.White, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(16, 1);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 120);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.Kill();
            return false;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
