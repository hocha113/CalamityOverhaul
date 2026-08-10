using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>弹道接管：把敌对弹幕改判成友方并原路打回</summary>
    internal class ProjectileHijack : QuickHackDef
    {
        private static readonly Color Signal = new(80, 220, 255);

        public override void SetDefaults() {
            UploadTime = 60;
            RamCost = 3;
            Category = QuickHackCategory.Control;
            SupportedTargets = HackTargetKind.Projectile;
            UnlockedByDefault = false;
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            //只接管真正会伤人的敌对弹；友方弹与纯装饰弹没有接管的意义
            return HackTargets.TryProjectile(target, out Projectile projectile)
                && projectile.hostile && projectile.damage > 0;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                projectile.hostile = false;
                projectile.friendly = true;
                //改判归属，伤害才会记在施法者头上；权威端改完靠 netUpdate 推给各端
                if (caster != null) {
                    projectile.owner = caster.whoAmI;
                }
                projectile.velocity = -projectile.velocity;
                projectile.netUpdate = true;
            }
            if (Main.netMode != NetmodeID.Server) EmitVisual(projectile);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryProjectile(target, out Projectile projectile)) {
                EmitVisual(projectile);
            }
        }

        private static void EmitVisual(Projectile projectile) {
            //沿原飞行方向甩出一道逆行的火花，读作"被掉头"
            Vector2 back = projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = back.RotatedByRandom(0.7f) * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_Spark>(projectile.Center, vel, Signal, 1.1f)
                    ?.Configure(false, 18);
            }
            PRTLoader.NewParticle<PRT_Spark>(projectile.Center, Vector2.Zero,
                Color.White, 1.8f)?.Configure(false, 10);
        }
    }
}
