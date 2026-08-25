using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Abyssrends
{
    /// <summary>
    /// 高压空化爆发。ai[0] 半径倍率。伤害窗对准崩开段，不是内收段
    /// </summary>
    internal class AbyssrendBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 36;
        private const float BaseRadius = 168f;

        private float SizeMul => Projectile.ai[0] > 0.05f ? Projectile.ai[0] : 1f;
        private int Age => Lifetime - Projectile.timeLeft;
        private float Progress => MathHelper.Clamp(Age / (float)Lifetime, 0f, 1f);
        private float VisibleRadius => BaseRadius * SizeMul * MathHelper.Lerp(0.35f, 1f, EaseOut(Progress));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (Progress > 0.48f && Progress < 0.85f) {
                Lighting.AddLight(Projectile.Center, 0.25f, 0.75f, 0.85f);
            }
            else {
                Lighting.AddLight(Projectile.Center, 0.08f, 0.22f, 0.3f);
            }

            if (VaultUtils.isServer) {
                return;
            }
            if (Age == 18) {
                int count = (int)(14 * SizeMul);
                for (int i = 0; i < count; i++) {
                    Vector2 dir = Main.rand.NextVector2Unit();
                    PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center + dir * 8f
                        , dir * Main.rand.NextFloat(3.5f, 8f)
                        , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                        , Main.rand.NextFloat(0.5f, 0.9f))
                        .Configure(Main.rand.Next(16, 26), 1.5f);
                }
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_AbyssSpark>(Projectile.Center
                        , Main.rand.NextVector2Circular(6f, 6f)
                        , AbyssrendFX.Foam, Main.rand.NextFloat(0.9f, 1.4f))
                        .Configure(14);
                }
            }
        }

        public override bool? CanDamage() => Progress >= 0.48f && Progress <= 0.82f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            float boomR = BaseRadius * SizeMul * MathHelper.Lerp(0.2f, 1f, (Progress - 0.48f) / 0.34f);
            return targetHitbox.Distance(Projectile.Center) <= boomR;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 240);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = Progress < 0.12f ? Progress / 0.12f : MathHelper.Clamp((1f - Progress) / 0.18f, 0f, 1f);
            fade = MathF.Max(fade, Progress > 0.35f && Progress < 0.9f ? 1f : fade);
            AbyssrendFX.DrawCanvasTech("TechBurst", Projectile.Center, AbyssrendFX.QuadPx(VisibleRadius)
                , Progress, fade);
            return false;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
