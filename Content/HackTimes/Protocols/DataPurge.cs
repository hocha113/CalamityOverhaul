using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>数据清除：抹掉这一发，连带清掉附近的同型弹</summary>
    internal class DataPurge : QuickHackDef
    {
        //连带清除半径
        private const float PurgeRadius = 260f;

        private static readonly Color Void = new(190, 120, 255);

        public override void SetDefaults() {
            UploadTime = 75;
            RamCost = 3;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.Projectile;
            UnlockedByDefault = false;
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            //不给玩家用来自杀式清掉自己的弹幕
            return HackTargets.TryProjectile(target, out Projectile projectile)
                && !projectile.friendly;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;
            Vector2 center = projectile.Center;
            int type = projectile.type;

            if (Main.netMode != NetmodeID.Server) EmitPurge(center);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                PurgeOne(projectile);
                //同型连带：弹幕通常成串发射，只清一发几乎没有手感
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile other = Main.projectile[i];
                    if (!other.active || other.friendly || other.type != type) continue;
                    if (Vector2.DistanceSquared(other.Center, center)
                        > PurgeRadius * PurgeRadius) {
                        continue;
                    }
                    if (Main.netMode != NetmodeID.Server) EmitPurge(other.Center);
                    PurgeOne(other);
                }
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryProjectile(target, out Projectile projectile)) {
                EmitPurge(projectile.Center);
            }
        }

        private static void PurgeOne(Projectile projectile) {
            projectile.active = false;
            projectile.netUpdate = true;
        }

        private static void EmitPurge(Vector2 center) {
            //向心收束而不是炸开，读作"被吸进去删掉"
            for (int i = 0; i < 12; i++) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(26f, 26f);
                PRTLoader.NewParticle<PRT_Spark>(center + offset,
                    -offset * 0.16f, Void, 0.9f)?.Configure(false, 14);
            }
        }
    }
}
