using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 潮汐掌击：手部闪现至侧翼→反向蓄势→贯穿突进→硬刹→睁眼硬直。
    /// 接触伤害由部件 AI 按速度门控（各端确定性），核心裸露后由真眼执行冲撞版
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.TidalPalms, typeof(MLordContext))]
    internal class MLordTidalPalmsState : MLordStateBase
    {
        public override string StateName => "TidalPalms";
        public override MLordStateIndex StateIndex => MLordStateIndex.TidalPalms;

        //子相位帧段
        internal const int BlinkLen = 12;
        internal const int WindupLen = 40;
        internal const int DashLen = 10;
        internal const int SkidLen = 16;
        internal const int RecoverLen = 22;
        internal const int CycleLen = BlinkLen + WindupLen + DashLen + SkidLen + RecoverLen;
        internal const int SlamCount = 3;
        internal const int PunishTail = 46;
        internal const float DashSpeed = 47f;

        /// <summary>本轮各执行者缓存的冲线方向（服务端锁定）</summary>
        private Vector2 lockedDashDir;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvAttackSeed] = Main.rand.Next(1, 100000);
                context.Npc.netUpdate = true;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //核心退到后景高位旁观（拉开镜头层次）
            HoverTo(npc, target.Center + new Vector2(0f, -300f), 6f, 0.04f);
            UpdateLean(context);

            int totalLen = SlamCount * CycleLen + PunishTail;
            int slamIndex = Timer / CycleLen;
            int sub = Timer % CycleLen;

            if (slamIndex < SlamCount) {
                NPC performer = ResolvePerformer(context, slamIndex);
                if (performer != null && !VaultUtils.isClient) {
                    DriveSlamServer(context, performer, slamIndex, sub);
                }
                //头部掩护弹：蓄势中拍点一发直射
                if (sub == BlinkLen + WindupLen / 2 && !VaultUtils.isClient) {
                    SpawnCoverBolt(context);
                }
            }

            Timer++;
            if (Timer >= totalLen) {
                return NextAttack(context);
            }
            return null;
        }

        /// <summary>本拍执行者：存活手轮换；无手则由真眼冲撞</summary>
        internal static NPC ResolvePerformer(MLordContext context, int slamIndex) {
            MLordPartsStatus parts = context.Parts;
            //存活手列表（有序，两端确定性一致）
            Span<int> hands = stackalloc int[2];
            int handCount = 0;
            if (parts.LeftHandAlive && parts.LeftHand >= 0) {
                hands[handCount++] = parts.LeftHand;
            }
            if (parts.RightHandAlive && parts.RightHand >= 0) {
                hands[handCount++] = parts.RightHand;
            }
            if (handCount > 0) {
                return Main.npc[hands[slamIndex % handCount]];
            }

            //真眼冲撞版
            int[] eyes = new int[3];
            int eyeCount = MLordFacts.ScanFreeEyes(context.Npc, eyes);
            if (eyeCount > 0) {
                return Main.npc[eyes[slamIndex % eyeCount]];
            }
            return null;
        }

        /// <summary>服务端驱动一拍掌击</summary>
        private void DriveSlamServer(MLordContext context, NPC performer, int slamIndex, int sub) {
            Player target = context.Target;
            int seed = (int)context.Owner.ai[MLordAiSlots.OvAttackSeed];
            float side = MLordConstellationProj.Hash01(seed, slamIndex) > 0.5f ? 1f : -1f;

            if (sub == 0) {
                //闪现落位：玩家侧翼
                Vector2 blinkPos = target.Center + new Vector2(side * 560f, -30f + MLordConstellationProj.Hash01(seed, slamIndex + 40) * 120f - 60f);
                performer.Center = blinkPos;
                performer.velocity = Vector2.Zero;
                performer.netUpdate = true;
                if (!VaultUtils.isServer) {
                    MLordScreenFX.StarBurst(blinkPos, 0.8f, 10);
                }
            }
            else if (sub < BlinkLen + WindupLen) {
                //反向蓄势：pow(t,6) 后仰，末端猛然回吸
                float t = (sub - BlinkLen) / (float)WindupLen;
                Vector2 away = (performer.Center - target.Center).SafeNormalize(Vector2.UnitX * side);
                performer.velocity = away * MathF.Pow(t, 6f) * 20f;
                //锁定冲线方向（蓄势后半段收敛，前半段跟踪）
                if (t < 0.55f) {
                    lockedDashDir = (target.Center + target.velocity * 8f - performer.Center).SafeNormalize(Vector2.UnitX * -side);
                }
                if (sub == BlinkLen + WindupLen - 8 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = -0.4f, MaxInstances = 4 }, performer.Center);
                }
            }
            else if (sub == BlinkLen + WindupLen) {
                //一帧点火全速
                performer.velocity = lockedDashDir * DashSpeed;
                performer.netUpdate = true;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_WyvernDiveDown with { Volume = 1f, Pitch = -0.2f }, performer.Center);
                    MLordScreenFX.Punch(performer.Center, 6f, 10, lockedDashDir);
                }
            }
            else if (sub < BlinkLen + WindupLen + DashLen) {
                //冲线复合加速
                performer.velocity *= 1.02f;
            }
            else if (sub < BlinkLen + WindupLen + DashLen + SkidLen) {
                //硬刹
                performer.velocity *= 0.76f;
            }
            else {
                //硬直悬停
                performer.velocity *= 0.85f;
            }
        }

        /// <summary>头部（或核心）掩护直射弹</summary>
        private void SpawnCoverBolt(MLordContext context) {
            NPC origin = context.Parts.Head >= 0 && context.Parts.HeadAlive
                ? Main.npc[context.Parts.Head] : context.Npc;
            Vector2 aim = (context.Target.Center - origin.Center).SafeNormalize(Vector2.UnitY);
            Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center + aim * 40f, aim * 7.5f,
                ProjectileID.PhantasmalBolt, ScaleDamage(context, MLordDirector.BoltDamage), 0f, Main.myPlayer);
        }

        #region 部件侧只读节拍查询（部件 AI 姿态/预警共用）

        /// <summary>当前拍执行者索引与子相位；不在掌击态返回 false</summary>
        internal static bool TryGetBeat(MLordContext context, int stateTimer, out int slamIndex, out int sub) {
            slamIndex = stateTimer / CycleLen;
            sub = stateTimer % CycleLen;
            return slamIndex < SlamCount;
        }

        /// <summary>蓄势期（部件绘制预警线用）</summary>
        internal static bool InWindup(int sub) => sub >= BlinkLen && sub < BlinkLen + WindupLen;
        /// <summary>冲线期</summary>
        internal static bool InDash(int sub) => sub >= BlinkLen + WindupLen && sub < BlinkLen + WindupLen + DashLen + SkidLen / 2;
        /// <summary>硬直期（眼睁开）</summary>
        internal static bool InRecover(int sub) => sub >= BlinkLen + WindupLen + DashLen + SkidLen;

        #endregion
    }
}
