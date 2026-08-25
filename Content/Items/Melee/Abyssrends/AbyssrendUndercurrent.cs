using CalamityOverhaul.Common;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Abyssrends
{
    /// <summary>
    /// 海洋暗流。发射后先沿出手方向冲出，再锁敌弯折加速，不是匀速飞针。
    /// ai[0] 噪声种子 ai[1] 目标 whoAmI（-1 自行搜寻）
    /// </summary>
    internal class AbyssrendUndercurrent : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int MaxLife = 78;
        private const float MaxSpeed = 19f;
        private const int LockDelay = 7;
        private const int TrailLen = 14;

        private float Seed => Projectile.ai[0];
        private int TargetIndex {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        private float wander;
        private float lockT;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = MaxLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 14;
            Projectile.CWR().PierceResist = true;
        }

        public override void AI() {
            int age = MaxLife - Projectile.timeLeft;
            wander = MathF.Sin((age + Seed) * 0.31f) * (1f - lockT);

            if (age >= LockDelay) {
                if (TargetIndex < 0 || !ValidTarget(TargetIndex)) {
                    TargetIndex = FindTarget(Projectile.Center, Main.player[Projectile.owner], 680f);
                    if (TargetIndex >= 0) {
                        Projectile.netUpdate = true;
                    }
                }
                if (ValidTarget(TargetIndex)) {
                    lockT = MathHelper.Clamp(lockT + 0.045f, 0f, 1f);
                    NPC npc = Main.npc[TargetIndex];
                    Vector2 want = (npc.Center - Projectile.Center).SafeNormalize(Projectile.velocity) * MaxSpeed;
                    float chase = 0.10f + lockT * 0.12f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, chase);
                }
                else {
                    Projectile.velocity *= 1.012f;
                    if (Projectile.velocity.Length() > MaxSpeed) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * MaxSpeed;
                    }
                }
            }
            else {
                Projectile.velocity *= 1.04f;
            }

            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity += perp * wander * (2.4f * (1f - lockT));
            if (Projectile.velocity.Length() > MaxSpeed * 1.15f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * MaxSpeed * 1.15f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.08f, 0.32f, 0.4f);

            if (VaultUtils.isServer) {
                return;
            }
            if (age % 2 == 0) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center
                    , -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.6f, 0.6f)
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.22f, 0.4f))
                    .Configure(10, 1.4f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 180);
            if (target.whoAmI == TargetIndex) {
                TargetIndex = FindTarget(Projectile.Center, Main.player[Projectile.owner], 520f, target.whoAmI);
            }
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(target.Center
                    , Main.rand.NextVector2Circular(3.5f, 3.5f)
                    , AbyssrendFX.Body, Main.rand.NextFloat(0.3f, 0.55f))
                    .Configure(12);
            }
            PRTLoader.NewParticle<PRT_AbyssSpark>(target.Center
                , Main.rand.NextVector2Circular(4f, 4f)
                , AbyssrendFX.Cyan, 1f)
                .Configure(10);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center
                    , Main.rand.NextVector2Circular(3f, 3f)
                    , AbyssrendFX.Deep, Main.rand.NextFloat(0.3f, 0.5f))
                    .Configure(14);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            Vector2[] path = new Vector2[TrailLen];
            int count = 0;
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                path[count++] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }
            if (count < 2) {
                path[0] = Projectile.Center - Projectile.velocity;
                path[1] = Projectile.Center;
                count = 2;
            }
            else {
                path[count - 1] = Projectile.Center;
            }
            float lifeFade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0.15f, 1f);
            AbyssrendFX.DrawPathStrip(path, count, i => {
                float t = i / (float)Math.Max(count - 1, 1);
                return MathHelper.Lerp(6f, 16f, t) * lifeFade;
            }, lifeFade);
        }

        private static bool ValidTarget(int idx) {
            if (idx < 0 || idx >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[idx];
            return npc.active && npc.CanBeChasedBy();
        }

        public static int FindTarget(Vector2 from, Player owner, float range, int ignore = -1) {
            int best = -1;
            float bestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (i == ignore || !npc.CanBeChasedBy(owner)) {
                    continue;
                }
                int id = npc.realLife >= 0 ? npc.realLife : npc.whoAmI;
                float dist = Vector2.Distance(from, Main.npc[id].Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = id;
                }
            }
            return best;
        }
    }
}
