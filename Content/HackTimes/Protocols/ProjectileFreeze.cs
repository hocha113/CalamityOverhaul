using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>弹道冻结：把一发弹幕钉在半空</summary>
    internal class ProjectileFreeze : QuickHackDef
    {
        private static readonly Color Frost = new(150, 220, 255);

        public override void SetDefaults() {
            UploadTime = 45;
            RamCost = 2;
            Category = QuickHackCategory.Control;
            SupportedTargets = HackTargetKind.Projectile;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 5;

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;
            if (Main.netMode != NetmodeID.Server) EmitApply(projectile);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryProjectile(target, out Projectile projectile)) {
                EmitApply(projectile);
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!TryRefreshFreeze(target, out Projectile projectile)) return true;
            if (Main.netMode != NetmodeID.Server) EmitHold(projectile, elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (TryRefreshFreeze(target, out Projectile projectile)) {
                EmitHold(projectile, elapsed);
            }
        }

        //速度快照与恢复全交给时停层，这里只负责每帧续租
        private static bool TryRefreshFreeze(IHackTarget target, out Projectile projectile) {
            if (!HackTargets.TryProjectile(target, out projectile)) return false;
            TimeFreezeSystem.RefreshProjectile<ProjectileFreeze>(projectile, 2);
            return true;
        }

        private static void EmitApply(Projectile projectile) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_Spark>(projectile.Center, vel, Frost, 1.0f)
                    ?.Configure(false, 20);
            }
        }

        private static void EmitHold(Projectile projectile, int elapsed) {
            if (elapsed % 12 != 0) return;
            //定住期间只留稀疏的悬停碎屑，别把画面糊住
            Vector2 offset = Main.rand.NextVector2Circular(
                projectile.width * 0.6f + 6f, projectile.height * 0.6f + 6f);
            PRTLoader.NewParticle<PRT_Spark>(projectile.Center + offset,
                Vector2.Zero, Frost, 0.6f)?.Configure(false, 16);
        }
    }
}
