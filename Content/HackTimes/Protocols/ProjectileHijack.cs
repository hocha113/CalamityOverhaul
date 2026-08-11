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

            if (Main.netMode != NetmodeID.MultiplayerClient) Hijack(projectile);
            if (Main.netMode != NetmodeID.Server) EmitVisual(projectile);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return;
            Hijack(projectile);
            EmitVisual(projectile);
        }

        /// <summary>
        /// 接管这件事没有一条能同步的路，只能各端自己翻一遍。<br/>
        /// <c>owner</c> 保持原样（敌对弹一般是 255）：改成玩家索引会把服务端唯一的
        /// 推送通道关掉，而 <c>hostile</c> / <c>friendly</c> 不在任何原版包里，
        /// 显式补一发 <c>SyncProjectile</c> 又会被客户端按 owner+identity 反查未果、
        /// 当成新弹再生成一发。<br/>
        /// 于是：owner 仍是 255，改判后的 NPC 命中只由服务端结算
        /// （伤害靠 <c>SyncNPC</c> 回传），而各端各自翻标志与速度，
        /// 才能让玩家碰撞判定与观感在每台机器上都对。<br/>
        /// 不能置 <c>netUpdate</c>：权威速度包与本端翻转会叠在一起，把弹幕又翻回去
        /// </summary>
        private static void Hijack(Projectile projectile) {
            projectile.hostile = false;
            projectile.friendly = true;
            projectile.velocity = -projectile.velocity;
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
