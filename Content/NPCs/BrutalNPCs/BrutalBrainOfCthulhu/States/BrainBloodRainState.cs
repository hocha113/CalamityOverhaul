using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>
    /// 二阶段血雨抛射：高位交替瞬移，痉挛蓄势后扇面喷洒重力血滴
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.BloodRain, typeof(BrainStateContext))]
    internal class BrainBloodRainState : BrainStateBase
    {
        public override string StateName => "BloodRain";
        public override BrainStateIndex StateIndex => BrainStateIndex.BloodRain;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int VolleyCount = 3;
        private const int ConvulseTime = 26;
        private const int SprayTime = 12;
        private const int RestTime = 22;
        private const int VolleyLength = ConvulseTime + SprayTime + RestTime;
        internal const int GlobDamage = 12;
        #endregion

        private int side = 1;

        public BrainBloodRainState() {
        }

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            context.Npc.damage = 0;
            if (!VaultUtils.isClient) {
                side = Main.rand.NextBool() ? 1 : -1;
                //首轮高位瞬移
                TeleportHigh(context);
            }
        }

        private void TeleportHigh(BrainStateContext context) {
            Vector2 dest = context.Target.Center + new Vector2(side * 330f, -360f);
            BrainMotion.ServerTeleport(context.Npc, dest, Vector2.Zero);
            side = -side;
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            npc.damage = 0;
            context.BeatIntensity = 0.65f;

            int volley = Timer / VolleyLength;
            int local = Timer % VolleyLength;

            if (volley >= VolleyCount) {
                if (!VaultUtils.isClient) {
                    return new BrainHoverState();
                }
                return null;
            }

            //痉挛蓄势
            if (local < ConvulseTime) {
                float t = local / (float)ConvulseTime;
                context.TelegraphGlow = t;
                if (!VaultUtils.isClient) {
                    npc.velocity *= 0.88f;
                    npc.velocity += Main.rand.NextVector2Circular(0.7f, 0.9f) * t;
                }
                //蓄势上涌雾
                if (!VaultUtils.isServer && local % 4 == 0 && BrainMotion.OnScreen(npc.Center)) {
                    BrainMotion.BloodMistBurst(npc.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), 30f),
                        0.4f + t * 0.4f, 1, 3f);
                }
                return null;
            }

            //喷洒帧
            if (local == ConvulseTime) {
                BrainHeartbeat.Thump(1.05f);
                BrainMotion.Shake(npc.Center, 4f, 10);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.8f, Pitch = -0.35f, MaxInstances = 3 }, npc.Center);
                    BrainMotion.BloodMistBurst(npc.Center, 1.3f, 8, 8f);
                }
                if (!VaultUtils.isClient) {
                    SprayGlobs(context, volley);
                    npc.velocity = Vector2.UnitY * -3.6f;   //上抛后坐
                }
                return null;
            }

            //休整/换位
            if (local == ConvulseTime + SprayTime + 4 && volley < VolleyCount - 1 && !VaultUtils.isClient) {
                TeleportHigh(context);
            }
            if (!VaultUtils.isClient) {
                npc.velocity *= 0.94f;
            }
            return null;
        }

        /// <summary>扇面喷洒重力血滴：跨立玩家预测位</summary>
        private static void SprayGlobs(BrainStateContext context, int volley) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int count = (context.IsDeathMode ? 13 : 10) + volley;
            int damage = GlobDamage + (context.IsDeathMode ? 3 : 0);

            Vector2 predicted = player.Center + player.velocity * 22f;
            Vector2 toTarget = predicted - npc.Center;

            for (int i = 0; i < count; i++) {
                //以竖直抛物为骨架横向铺开
                float lateral = MathHelper.Lerp(-1f, 1f, i / (count - 1f)) + Main.rand.NextFloat(-0.08f, 0.08f);
                float vx = toTarget.X * 0.012f + lateral * 5.6f;
                float vy = Main.rand.NextFloat(-4.5f, -1.5f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(lateral * 26f, 10f),
                    new Vector2(vx, vy), ModContent.ProjectileType<BrainBloodShard>(),
                    damage, 0f, Main.myPlayer, 1f);
            }
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
