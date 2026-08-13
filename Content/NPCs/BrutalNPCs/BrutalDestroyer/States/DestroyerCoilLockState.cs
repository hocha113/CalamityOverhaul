using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>投技前置：环心锁死，体节沿收缩螺旋卷成钢环；期间环体无害，逃出警告圈即空抓</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.CoilLock, typeof(DestroyerStateContext))]
    internal class DestroyerCoilLockState : DestroyerStateBase
    {
        public override string StateName => "CoilLock";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.CoilLock;
        /// <summary>自带环轨走位，关远距瞬移阀</summary>
        public override bool AllowFarSnap => false;

        #region 节奏与判定常量
        /// <summary>锁环预警时长，玩家的逃逸窗口</summary>
        internal const int LockDuration = 78;
        /// <summary>空抓合拢演出时长</summary>
        private const int WhiffTime = 26;
        /// <summary>保底出口</summary>
        private const int HardTimeout = LockDuration + 60;
        /// <summary>抓取判定半径(警告圈)</summary>
        internal const float GrabRadius = 380f;
        /// <summary>锁环末端环半径</summary>
        internal const float LockRadius = 420f;
        /// <summary>命中后冷却(45s)</summary>
        internal const int GrabCooldown = 2700;
        /// <summary>空抓冷却(15s)</summary>
        internal const int WhiffCooldown = 900;
        #endregion

        private float entryRadius = -1f;
        private bool whiffClangFired;

        public DestroyerCoilLockState() {
        }

        /// <summary>投技触发闸门，只在服务端/单机端评估（Encircle 收环完成时调）</summary>
        internal static bool CanStartCoilGrab(DestroyerStateContext context) {
            //扣押到激怒阶段 + 冷却完毕
            if (!context.IsEnraged || context.GrabCooldownTimer > 0) {
                return false;
            }
            Player target = context.Target;
            if (!target.Alives() || target.ghost) {
                return false;
            }
            //世界时停或本体被时停期间不得触发
            if (TimeFreezeSystem.IsAnyGlobalFreezeActive || TimeFreezeSystem.IsFrozen(context.Npc)) {
                return false;
            }
            //单机端可感知运镜占用；联机端由客户端片段优先级兜底
            if (!Main.dedServ && CutsceneDirector.IsPlaying) {
                return false;
            }
            //包围确实成形(头仍贴在环轨上)才有资格收环
            if (context.Npc.Distance(target.Center) > 1250f) {
                return false;
            }
            return true;
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            entryRadius = -1f;
            whiffClangFired = false;

            NPC npc = context.Npc;
            //服务端锁死环心与猎物，随 ChangeState 的 netUpdate 一并同步
            if (!VaultUtils.isClient) {
                Player target = context.Target;
                npc.ai[0] = target.Center.X;
                npc.ai[1] = target.Center.Y;
                npc.ai[3] = target.whoAmI;
                npc.netUpdate = true;
                //警告圈载体
                Projectile.NewProjectile(npc.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<DestroyerCoilRingProj>(), 0, 0f, Main.myPlayer, npc.whoAmI);
            }

            //专属锁环警号，音高与常规预警区分
            SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.35f, Volume = 1.05f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Thunder with { Pitch = -0.6f, Volume = 0.6f }, npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Vector2 center = new(npc.ai[0], npc.ai[1]);

            //整个预警期头无接触伤，逃逸窗是真窗口(体节免伤见 DestroyerBodyAI)
            npc.damage = 0;
            context.JawCommand = 1;

            Timer++;

            //收缩螺旋卷环
            if (Timer <= LockDuration) {
                UpdateSpiral(context, center);

                //T-36 定拍预警音
                if (Timer == LockDuration - 36) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f, Volume = 0.9f }, center);
                }

                //收环期偶发体节电弧，宣告"通电中"
                if (!VaultUtils.isClient && Timer % 18 == 0) {
                    SpawnSegmentArc(context);
                }

                //抓取判定：只在服务端/单机端定夺
                if (Timer == LockDuration && !VaultUtils.isClient) {
                    int victimIndex = (int)npc.ai[3];
                    Player victim = victimIndex >= 0 && victimIndex < Main.maxPlayers ? Main.player[victimIndex] : null;
                    if (victim.Alives() && !victim.ghost && victim.Distance(center) <= GrabRadius) {
                        return new DestroyerCoilCrushState();
                    }
                    //逃逸成功→空抓，较短冷却
                    context.GrabCooldownTimer = WhiffCooldown;
                }
                return null;
            }

            //空抓：钢环猛然合拢在空气上
            UpdateWhiff(context, center);

            if (Timer >= LockDuration + WhiffTime && !VaultUtils.isClient) {
                //扑空后顺势入冲刺蓄力，保持旧节奏
                return new DestroyerDashPrepareState();
            }
            //保底出口
            if (Timer >= HardTimeout && !VaultUtils.isClient) {
                return new DestroyerPatrolState();
            }

            return null;
        }

        /// <summary>沿锁死环心收缩螺旋，把体节卷上钢环</summary>
        private void UpdateSpiral(DestroyerStateContext context, Vector2 center) {
            NPC npc = context.Npc;

            Vector2 rel = npc.Center - center;
            if (entryRadius < 0f) {
                entryRadius = MathHelper.Clamp(rel.Length(), 520f, 1150f);
            }

            float t = Math.Min(Timer / (float)LockDuration, 1f);
            float ease = 1f - (1f - t) * (1f - t);
            float targetRadius = MathHelper.Lerp(entryRadius, LockRadius, ease);

            float curAngle = rel.ToRotation();
            float angSpeed = MathHelper.Lerp(0.055f, 0.105f, t);
            float nextAngle = curAngle + angSpeed;

            Vector2 orbitTarget = center + nextAngle.ToRotationVector2() * targetRadius;
            Vector2 desired = orbitTarget - npc.Center;
            float speed = MathHelper.Clamp(desired.Length() / 6f, 26f, 54f);
            npc.velocity = desired.SafeNormalize(Vector2.Zero) * speed;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //锁环蓄力波，尾→头循环涌动
            context.SetChargeState(5, t);
            Rendering.DestroyerChargeWave.Push(npc.whoAmI, 1f - (Timer * 0.03f) % 1f, 0.24f, 0.35f + 0.5f * t);
        }

        /// <summary>空抓合拢：环体急缩+金属应力声，读作"扑空"</summary>
        private void UpdateWhiff(DestroyerStateContext context, Vector2 center) {
            NPC npc = context.Npc;
            int wt = Timer - LockDuration;

            //短暂全环白闪定格，掩盖端间时序差(若服务端其实判了抓取，ai[2]会在此窗内到达)
            if (wt <= 8) {
                Rendering.DestroyerChargeWave.Push(npc.whoAmI, 0f, 1f, 0.9f, fullBody: true);
            }

            if (!whiffClangFired && wt == 9) {
                whiffClangFired = true;
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.55f, Volume = 1f }, center);
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.2f, Volume = 0.8f }, center);
                if (!VaultUtils.isServer) {
                    Rendering.DestroyerMotionFX.CameraPunch(center, 5f, 12, "DestroyerCoilWhiff");
                }
            }

            //快速合拢到 300，随后放缓
            float t = MathHelper.Clamp(wt / (float)WhiffTime, 0f, 1f);
            float targetRadius = MathHelper.Lerp(LockRadius, 300f, 1f - (1f - t) * (1f - t) * (1f - t));

            Vector2 rel = npc.Center - center;
            float nextAngle = rel.ToRotation() + 0.14f;
            Vector2 orbitTarget = center + nextAngle.ToRotationVector2() * targetRadius;
            Vector2 desired = orbitTarget - npc.Center;
            npc.velocity = desired.SafeNormalize(Vector2.Zero) * MathHelper.Clamp(desired.Length() / 5f, 24f, 50f);
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            //合拢刹车火花
            if (!VaultUtils.isServer && wt > 8 && Timer % 3 == 0) {
                Rendering.DestroyerMotionFX.SpawnBrakeSparks(npc);
            }
        }

        /// <summary>环上取相隔约1/3圈的两节拉电弧(服务端)</summary>
        private static void SpawnSegmentArc(DestroyerStateContext context) {
            var segments = context.BodySegments;
            if (segments.Count < 12) {
                return;
            }
            int i = Main.rand.Next(segments.Count);
            int j = (i + segments.Count / 3 + Main.rand.Next(-2, 3) + segments.Count) % segments.Count;
            NPC a = segments[i];
            NPC b = segments[j];
            if (!a.active || !b.active || a.whoAmI == b.whoAmI) {
                return;
            }
            Projectile.NewProjectile(context.Npc.GetSource_FromAI(), a.Center, Vector2.Zero,
                ModContent.ProjectileType<DestroyerArc>(), 0, 0f, Main.myPlayer, a.whoAmI, b.whoAmI);
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
