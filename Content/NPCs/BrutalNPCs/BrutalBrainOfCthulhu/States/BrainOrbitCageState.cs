using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.States
{
    /// <summary>
    /// 飞眼轨道阵·收缩牢笼：环阵随心跳逐拍收缩，缺口永远开在背对脑的一侧
    /// 终拍：脑裂隙穿入环心放射血环，沿缺口方向留生路
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)BrainStateIndex.OrbitCage, typeof(BrainStateContext))]
    internal class BrainOrbitCageState : BrainStateBase
    {
        public override string StateName => "OrbitCage";
        public override BrainStateIndex StateIndex => BrainStateIndex.OrbitCage;
        public override bool AllowFarSnap => false;

        #region 节奏常量
        private const int GatherTime = 46;
        private const int ContractCount = 4;
        private const int ContractPulse = 14;   //收缩脉冲帧数（判定窗口）
        private const int HoldTime = 34;        //锁笼持留
        private const int FinisherTime = 52;    //裂隙穿入+放射
        private const float StartRadius = 640f;
        private const float StepRadius = 95f;
        /// <summary>放射血珠伤害（原始值）</summary>
        internal const int ShardDamage = 13;
        #endregion

        private bool finisherRiftSpawned;
        private bool finisherFired;
        /// <summary>集结完成时的全局拍号（各端各自捕获，随同步时钟对齐）</summary>
        private long startBeat = -1;

        public BrainOrbitCageState() {
        }

        private Vector2 CageCenter(BrainStateContext context) =>
            new(context.Master.ai[0], context.Master.ai[1]);

        private int BeatPeriodOf(BrainStateContext context) => context.IsPhase2 ? 40 : 54;

        private int ContractEnd(BrainStateContext context) =>
            GatherTime + (ContractCount + 1) * BeatPeriodOf(context);

        public override void OnEnter(BrainStateContext context) {
            base.OnEnter(context);
            finisherRiftSpawned = false;
            finisherFired = false;
            startBeat = -1;
            context.Npc.damage = 0;

            if (!VaultUtils.isClient) {
                //笼心快照写入同步槽
                context.Master.ai[0] = context.Target.Center.X;
                context.Master.ai[1] = context.Target.Center.Y;
                context.Npc.netUpdate = true;
                context.RefreshCreepers();
            }
        }

        public override IBrainState OnUpdate(BrainStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //飞眼死太多则中止（袋会因数量闸自动停用本招）
            if (Timer % 30 == 0) {
                context.RefreshCreepers();
            }
            if (!VaultUtils.isClient && context.Creepers.Count < 3 && Timer < ContractEnd(context)) {
                return new BrainHoverState();
            }

            Vector2 center = CageCenter(context);
            int period = BeatPeriodOf(context);
            context.BeatIntensity = 0.75f;

            //收缩与全局心跳时钟对齐：音画判定同拍
            long beatIndex = (long)(npc.ai[3] / period);
            int beatPhase = (int)(npc.ai[3] % period);
            if (startBeat < 0 && Timer >= GatherTime) {
                startBeat = beatIndex;
            }
            int contracted = startBeat >= 0 ? (int)Math.Clamp(beatIndex - startBeat, 0, ContractCount) : 0;
            float prevR = StartRadius - StepRadius * Math.Max(contracted - 1, 0);
            float targetR = StartRadius - StepRadius * contracted;
            float stepT = contracted > 0 ? MathHelper.Clamp(beatPhase / (float)ContractPulse, 0f, 1f) : 0f;
            float radius = MathHelper.Lerp(prevR, targetR, BrainMotion.SharpOut(stepT, 7));
            bool damagePulse = contracted > 0 && beatPhase < ContractPulse;

            //缺口：第二次收缩后开在背对脑的一侧
            float gapAngle = -10f;
            float gapHalf = 0f;
            if (contracted >= 2 && Timer < ContractEnd(context) + HoldTime) {
                gapAngle = (center - npc.Center).ToRotation();
                gapHalf = 0.46f;
            }

            //旋转相位（慢旋，收缩瞬间带一点回拧）
            float spin = Timer * 0.006f - contracted * 0.12f;

            BrainFormationChannel.PushCage(center, radius, spin, gapAngle, gapHalf,
                damagePulse, Math.Max(context.Creepers.Count, 1));

            //收缩拍的画面反馈（与心音同帧）
            if (damagePulse && beatPhase == 1) {
                BrainHeartbeat.Thump(0.95f);
                if (!VaultUtils.isServer && BrainMotion.OnScreen(center, 800f)) {
                    BrainMotion.FleshSquish(center, 0.6f, -0.5f);
                }
            }

            //脑：笼外巡曳（阶段一）
            if (Timer < ContractEnd(context) + HoldTime) {
                npc.damage = 0;
                if (!VaultUtils.isClient) {
                    float prowlAngle = Timer * 0.011f + MathHelper.Pi;
                    Vector2 prowlPos = center + prowlAngle.ToRotationVector2() * (radius + 300f);
                    BrainMotion.SpringHover(npc, prowlPos, 0.02f, 0.11f, 24f);
                }
                context.TelegraphGlow = damagePulse ? 0.5f : 0.2f;
                return null;
            }

            //终幕：裂隙穿入环心
            int finisherTimer = Timer - ContractEnd(context) - HoldTime;
            if (!finisherRiftSpawned) {
                finisherRiftSpawned = true;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), center, Vector2.Zero,
                        ModContent.ProjectileType<BrainTeleportRift>(), 0, 0f, Main.myPlayer, 1f);
                }
            }

            if (finisherTimer < 26) {
                //穿入前摇：裂隙搏动，飞眼环僵持
                npc.damage = 0;
                return null;
            }

            if (!finisherFired) {
                finisherFired = true;
                if (!VaultUtils.isClient) {
                    //先记缺口向（背对穿入前的脑位），再瞬移
                    float finisherGap = (center - npc.Center).ToRotation();
                    BrainMotion.ServerTeleport(npc, center, Vector2.Zero);
                    KillRifts();
                    FireRadialShards(context, center, finisherGap);
                }
                BrainHeartbeat.Thump(1.3f, 0.93f);
                BrainMotion.Shake(center, 6f, 14);
                BrainMotion.Roar(center, 1f, -0.1f);
            }

            //放射后短懈怠+笼散
            npc.damage = npc.defDamage;
            if (finisherTimer >= FinisherTime && !VaultUtils.isClient) {
                return new BrainHoverState();
            }
            return null;
        }

        /// <summary>放射血环：沿笼缺口方向留生路</summary>
        private static void FireRadialShards(BrainStateContext context, Vector2 center, float gapAngleForFinisher) {
            NPC npc = context.Npc;
            int count = context.IsDeathMode ? 14 : 11;
            int damage = ShardDamage + (context.IsDeathMode ? 3 : 0);
            //缺口方向沿用笼缺口（脑已在心，取穿入前朝向的反向即笼缺口向）
            float gap = gapAngleForFinisher;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                if (Math.Abs(MathHelper.WrapAngle(angle - gap)) < 0.5f) {
                    continue;
                }
                Vector2 vel = angle.ToRotationVector2() * 9.6f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), center, vel,
                    ModContent.ProjectileType<BrainBloodShard>(), damage, 0f, Main.myPlayer, 0f);
            }
        }

        private static void KillRifts() {
            int riftType = ModContent.ProjectileType<BrainTeleportRift>();
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == riftType) {
                    proj.Kill();
                }
            }
        }

        public override void OnExit(BrainStateContext context) {
            base.OnExit(context);
            BrainFormationChannel.Clear();
            context.Npc.damage = context.Npc.defDamage;
            if (!VaultUtils.isClient) {
                KillRifts();
            }
        }
    }
}
