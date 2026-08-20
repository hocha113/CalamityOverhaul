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
    /// 蜂群漩涡(二阶段)：围笼缓缓收缩，一道缺口绕环游走；女王在外沿内射毒刺施压，<br/>
    /// 中程两次穿心冲刺逼走位；终拍围笼向外炸散(泄压释放)<br/>
    /// npc.ai[0]=旋向(服务端掷骰)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)QueenBeeStateIndex.SwarmVortex, typeof(QueenBeeStateContext))]
    internal class QBSwarmVortexState : QueenBeeStateBase
    {
        public override string StateName => "SwarmVortex";
        public override QueenBeeStateIndex StateIndex => QueenBeeStateIndex.SwarmVortex;

        #region 节奏常量
        private const int TotalTime = 304;
        private const int ContractStart = 36;   //成笼即开始缓收，砍掉无威胁巡航段
        private const int ContractEnd = 264;
        private const float RadiusStart = 430f;
        private const float RadiusEnd = 252f;
        //两次穿心冲刺的蓄力起帧
        private const int Cross1Charge = 96;
        private const int Cross2Charge = 204;
        private const int CrossChargeTime = 22;
        private const int CrossDashTime = 26;
        //公平阀：围笼缺口宽 SwarmDirector.VortexGapWidth(~62°)恒定，转速0.024rad/帧匀速可预判；
        //缺口两沿蜂由 GetEdgeHighlight 常亮标出；笼锚仅0.02慢跟，持续走位可拖拽整笼
        #endregion

        //两次穿心蓄力起帧表(静态避免逐帧分配)
        private static readonly int[] CrossStarts = [Cross1Charge, Cross2Charge];

        private Vector2 cageAnchor;

        public override void OnEnter(QueenBeeStateContext context) {
            base.OnEnter(context);
            cageAnchor = context.Target.Center;
            if (!VaultUtils.isClient) {
                context.Npc.ai[0] = Main.rand.NextBool() ? 1 : -1;
                context.Npc.netUpdate = true;
            }
            QueenBeeMotion.RoarBurst(context.Npc.Center, 0.8f);
        }

        public override IQueenBeeState OnUpdate(QueenBeeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            float spinDir = npc.ai[0] >= 0f ? 1f : -1f;

            Timer++;

            //围笼锚缓跟玩家(可被持续走位拖拽)
            cageAnchor = Vector2.Lerp(cageAnchor, player.Center, 0.02f);

            //收缩曲线
            float contractT = MathHelper.Clamp((Timer - ContractStart) / (float)(ContractEnd - ContractStart), 0f, 1f);
            float radius = MathHelper.SmoothStep(RadiusStart, RadiusEnd, contractT);

            //终拍炸散
            if (Timer == TotalTime - 26) {
                context.Swarm.LaunchRadial(0, SwarmDirector.MaxBees - 1, cageAnchor, 15f);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.8f, Pitch = 0.2f }, cageAnchor);
                QueenBeeMotion.Shake(cageAnchor, 5f, 10);
            }

            if (Timer < TotalTime - 26) {
                context.Swarm.Declare(SwarmFormation.Vortex, cageAnchor, Vector2.UnitX, radius, spinDir * 0.024f);
                context.Swarm.PushSignal(0.8f);
            }
            else {
                context.Swarm.PushSignal(0.3f);
            }

            //女王行为：外沿压制→两次穿心冲刺
            UpdateQueen(context, npc, player, radius);

            if (Timer >= TotalTime) {
                return new QBRepositionState();
            }
            return null;
        }

        private void UpdateQueen(QueenBeeStateContext context, NPC npc, Player player, float radius) {
            //穿心冲刺窗口
            foreach (int chargeStart in CrossStarts) {
                int t = Timer - chargeStart;
                if (t < 0 || t >= CrossChargeTime + CrossDashTime + 14) {
                    continue;
                }

                if (t < CrossChargeTime) {
                    //蓄力：笼外定点，反向吸气
                    Vector2 outDir = (npc.Center - cageAnchor).SafeNormalize(Vector2.UnitX);
                    Vector2 chargePos = cageAnchor + outDir * (radius + 210f + (float)Math.Pow(t / (float)CrossChargeTime, 8f) * 80f);
                    QueenBeeMotion.SpringHover(npc, chargePos, 0.05f, 0.18f, 40f);
                    context.SetChargeState(1, t / (float)CrossChargeTime);
                    context.UseChargePose = t > CrossChargeTime - 12;
                    FaceTarget(npc, cageAnchor);
                    return;
                }
                if (t == CrossChargeTime) {
                    //穿心发射：贯穿笼心directed稍偏玩家
                    Vector2 dir = (Vector2.Lerp(cageAnchor, player.Center, 0.55f) - npc.Center).SafeNormalize(Vector2.UnitY);
                    QueenBeeMotion.DashLaunch(npc, dir, 37f, 1.2f);
                    return;
                }
                if (t < CrossChargeTime + CrossDashTime) {
                    context.UseChargePose = true;
                    context.PushAfterimage(1f);
                    EnableContactDamageIfFast(npc, 20f);
                    FaceByVelocity(npc);
                    return;
                }
                //冲刺后短刹
                QueenBeeMotion.BrakeHard(npc, 0.78f);
                DisableContactDamage(npc);
                return;
            }

            //常态：贴外沿游走，周期向内射慢速毒刺
            float orbit = Timer * 0.014f * (npc.ai[0] >= 0f ? -1f : 1f);
            Vector2 holdPos = cageAnchor + orbit.ToRotationVector2() * (radius + 240f);
            QueenBeeMotion.SpringHover(npc, holdPos, 0.02f, 0.11f, 30f);
            FaceTarget(npc, cageAnchor);

            if (Timer % 42 == 20) {
                Vector2 muzzle = npc.Center + new Vector2(0f, npc.height * 0.32f);
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.55f, Pitch = -0.25f, MaxInstances = 3 }, muzzle);
                if (!VaultUtils.isClient) {
                    Vector2 vel = (player.Center - muzzle).SafeNormalize(Vector2.UnitY) * 5.6f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, vel,
                        ModContent.ProjectileType<BrutalBeeStinger>(), BrutalBeeStinger.BaseDamage, 0f, Main.myPlayer, 0f);
                }
            }
        }

        public override void OnExit(QueenBeeStateContext context) {
            base.OnExit(context);
            DisableContactDamage(context.Npc);
        }
    }
}
