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
            return HackTargets.TryProjectile(target, out Projectile projectile)
                && IsPurgeable(projectile);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;
            //落点与类型都要在击杀前取：Kill 会跑 OnKill，
            //不少爆炸弹在那里改自己的位置和体积
            Vector2 center = projectile.Center;
            int type = projectile.type;

            if (Main.netMode != NetmodeID.Server) EmitPurge(center);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                PurgeOne(projectile);
                //同型连带：弹幕通常成串发射，只清一发几乎没有手感
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile other = Main.projectile[i];
                    if (!other.active || other.type != type
                        || !IsPurgeable(other)) {
                        continue;
                    }
                    if (Vector2.DistanceSquared(other.Center, center)
                        > PurgeRadius * PurgeRadius) {
                        continue;
                    }
                    if (Main.netMode != NetmodeID.Server) EmitPurge(other.Center);
                    PurgeOne(other);
                }
                if (Main.netMode == NetmodeID.Server) {
                    //效果本身的 EffectApply 广播排在 OnApply 之后，那时弹幕已经死了，
                    //弹幕身份取不出来，包根本发不出去，远端只能靠这条旁路看到表现
                    HackTimeNetSync.BroadcastPointCue(HackPointCue.DataPurge,
                        caster?.whoAmI ?? 0, center);
                }
            }
            return true;
        }

        /// <summary>
        /// 远端的清除表现，落点由权威端直接带过来。<br/>
        /// 刻意不实现 <c>OnReplicatedApply</c>：弹幕已经被击杀，那条路要么取不到目标，
        /// 要么撞上被复用的槽位画到别的弹幕头上
        /// </summary>
        internal static void PlayPurgeCue(Vector2 center) => EmitPurge(center);

        //钩爪与浮标都是 friendly=false, hostile=false，只排除 friendly 会把它们一起清掉
        private static bool IsPurgeable(Projectile projectile)
            => !projectile.friendly && (projectile.hostile || projectile.trap);

        private static void PurgeOne(Projectile projectile) {
            //必须走 Kill：直接置 active=false 既跳过 PreKill/OnKill，
            //也发不出任何同步，Projectile.Update 每帧清 netUpdate，
            //且只在 active && owner == Main.myPlayer 时才发，服务端的 myPlayer 是 255
            int identity = projectile.identity;
            int owner = projectile.owner;
            projectile.Kill();
            if (Main.netMode == NetmodeID.Server) {
                //29 号按 owner+identity 反查本机槽位，owner 255 的世界弹也认
                NetMessage.SendData(MessageID.KillProjectile, -1, -1, null,
                    identity, owner);
            }
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
