using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States
{
    /// <summary>
    /// 蜂蜜迫击炮：高位悬停，腹部蓄力抛射蜜团，砸出夹烧玩家走位的黏滞蜜洼；<br/>
    /// 蜂群伞幕护顶+侧翼补员(这招同时是女王的换弹拍)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.HoneyMortar, typeof(QueenBeeStateContext))]
    internal class QBHoneyMortarState : QueenBeeStateBase
    {
        public override string StateName => "HoneyMortar";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.HoneyMortar;

        private const int MaxTime = 196;
        //三轮抛射帧(首轮蓄力与入场悬停重叠，尾轮后只留短收势)
        private static readonly int[] LobFrames = [46, 104, 162];
        /// <summary>公平阀：夹击落点横向间距，保证蜜洼之间始终留有可穿行的走位缝</summary>
        private const float LandingSpacing = 230f;

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;

            //高位悬停(比毒刺扇更高更远，卖"火炮阵地"感)
            Vector2 hoverPos = player.Center + new Vector2(0f, -430f);
            QueenBeeMotion.SpringHover(npc, hoverPos, 0.014f, 0.1f, 22f);
            FaceTarget(npc, player.Center);

            //蜂群伞幕护顶
            context.Swarm.Declare(SwarmFormation.Umbrella, npc.Center, Vector2.UnitX);
            context.Swarm.PushSignal(0.45f);

            //补员窗口：这招也是女王的"换弹拍"
            if (!VaultUtils.isClient && Timer % 18 == 0) {
                context.Swarm.ServerTopUp(context.IsPhase2 ? 24 : 16, 3);
            }

            //腹部蓄力表现：向下一轮抛射帧渐进
            int nextLob = -1;
            foreach (int frame in LobFrames) {
                if (Timer <= frame) {
                    nextLob = frame;
                    break;
                }
            }
            if (nextLob > 0) {
                float progress = 1f - (nextLob - Timer) / 58f;
                if (progress > 0f) {
                    context.SetChargeState(2, MathHelper.Clamp(progress, 0f, 1f));
                    QueenBeeMotion.ChargeGatherFX(npc.Center + new Vector2(0f, npc.height * 0.3f),
                        MathHelper.Clamp(progress, 0f, 1f), 84f);
                }
            }

            //抛射帧
            foreach (int frame in LobFrames) {
                if (Timer == frame) {
                    LobVolley(context);
                    break;
                }
            }

            if (Timer >= MaxTime) {
                return new QBRepositionState();
            }
            return null;
        }

        /// <summary>一轮蜜团抛射：左右夹击落点，腹部后坐上蹿</summary>
        private void LobVolley(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Vector2 muzzle = npc.Center + new Vector2(0f, npc.height * 0.35f);

            //腹部后坐：整只上蹿一记
            npc.velocity -= Vector2.UnitY * 4.2f;
            SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.7f, Pitch = -0.35f, MaxInstances = 3 }, muzzle);
            QueenBeeMotion.HoneyBurst(muzzle, 1f, 8, false);
            QueenBeeMotion.Shake(npc.Center, 3f, 8);

            if (VaultUtils.isClient) {
                return;
            }

            //夹击落点：脚下+两翼，死亡模式加一发远端封路
            int globCount = context.IsPhase2 ? 3 : 2;
            if (context.IsDeathMode) {
                globCount++;
            }
            for (int i = 0; i < globCount; i++) {
                float lateral = (i - (globCount - 1) * 0.5f) * LandingSpacing + Main.rand.NextFloat(-40f, 40f);
                Vector2 targetPos = player.Center + new Vector2(lateral, 0f);
                Vector2 toTarget = targetPos - muzzle;
                //高抛弹道：水平匀速+按飞行时间配初始竖速
                float flightTime = MathHelper.Clamp(Math.Abs(toTarget.X) / 9f, 26f, 60f);
                float vx = toTarget.X / flightTime;
                float vy = toTarget.Y / flightTime - 0.24f * flightTime * 0.5f;
                Vector2 vel = new Vector2(vx, vy);

                Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, vel,
                    ModContent.ProjectileType<HoneyGlobProj>(), 13, 0f, Main.myPlayer,
                    Main.rand.NextFloat(190f, 250f));
            }
        }
    }
}
