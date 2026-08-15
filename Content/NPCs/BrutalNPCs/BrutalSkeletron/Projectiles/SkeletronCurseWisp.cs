using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles
{
    /// <summary>诅咒烛灵：领域收拢环，ai[0]=初始角，ai[1]=锚X，ai[2]=锚Y，全程确定性轨道</summary>
    internal class SkeletronCurseWisp : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const float StartRadius = 640f;
        internal const float EndRadius = 190f;
        internal const int OrbitFrames = 300;
        internal const int FlareFrames = 30;

        private ref float Angle0 => ref Projectile.ai[0];
        private ref float AnchorX => ref Projectile.ai[1];
        private ref float AnchorY => ref Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = OrbitFrames + FlareFrames;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Age++;

            //确定性轨道：收拢螺旋
            float t = MathHelper.Clamp(Age / OrbitFrames, 0f, 1f);
            float radius = MathHelper.Lerp(StartRadius, EndRadius, MathF.Pow(t, 1.25f));
            float angle = Angle0 + Age * 0.016f;
            Vector2 anchor = new Vector2(AnchorX, AnchorY);
            Projectile.Center = anchor + angle.ToRotationVector2() * radius;
            Projectile.velocity = Vector2.Zero;

            //末段回燃预警
            float flare = MathHelper.Clamp((Age - OrbitFrames) / (float)FlareFrames, 0f, 1f);
            Projectile.scale = 1f + flare * 0.8f;

            if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1.6f, -0.7f)),
                    SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(Main.rand.Next(14, 24));
            }

            Lighting.AddLight(Projectile.Center, SkeletronRenderHelper.GhostCyan.ToVector3() * (0.5f + flare * 0.5f));
        }

        /// <summary>点燃淡入后才有杀伤</summary>
        public override bool? CanDamage() => Age > 16 ? null : false;

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(Projectile.Center,
                    Main.rand.NextVector2Circular(3.4f, 3.4f),
                    SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1f, 1.7f))?.Configure(Main.rand.Next(18, 30));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //烛焰双层冷焰顶点quad：外鞘大焰 + 内芯小焰，末段回燃染诅咒紫
            float fadeIn = MathHelper.Clamp(Age / 16f, 0f, 1f);
            float flick = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.whoAmI * 1.7f);
            float flare = MathHelper.Clamp((Age - OrbitFrames) / (float)FlareFrames, 0f, 1f);
            //烛焰朝上，随风微摆
            float sway = 0.16f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + Projectile.whoAmI * 2.3f);
            float axis = -MathHelper.PiOver2 + sway;
            float seed = Projectile.whoAmI * 0.173f;
            Vector2 root = Projectile.Center + new Vector2(0f, 14f * Projectile.scale);

            SkeletronFlameRender.Push(root, axis,
                new Vector2(30f, 58f) * Projectile.scale * flick,
                0.55f + flare * 0.45f, seed, 0.3f + flare * 0.6f,
                0.85f * fadeIn);
            SkeletronFlameRender.Push(root + new Vector2(0f, -4f * Projectile.scale), axis,
                new Vector2(14f, 32f) * Projectile.scale * flick,
                0.9f, seed + 0.41f, 0.1f,
                0.9f * fadeIn);
            return false;
        }
    }
}
