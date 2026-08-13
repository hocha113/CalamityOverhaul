using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>投技本体：钢环绞住玩家→探针环内十字电弧→通电收紧→机头贯穿环心掷飞→散热恢复；
    /// 被抓玩家的锁控/运镜/节拍伤害由 DestroyerGrabPlayer 在其本端施加</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.CoilCrush, typeof(DestroyerStateContext))]
    internal class DestroyerCoilCrushState : DestroyerStateBase
    {
        public override string StateName => "CoilCrush";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.CoilCrush;
        /// <summary>绞缠期间禁远距瞬移阀</summary>
        public override bool AllowFarSnap => false;

        #region 节奏常量(被抓端节拍表在 DestroyerGrabPlayer，改动需同步)
        /// <summary>收环绞紧段末</summary>
        internal const int SeizeEnd = 30;
        /// <summary>探针十字段末</summary>
        internal const int CrossEnd = 108;
        /// <summary>通电收紧段末</summary>
        internal const int SqueezeEnd = 172;
        /// <summary>爆发前静默段末</summary>
        internal const int SilenceEnd = 186;
        /// <summary>贯穿起跳帧(仰起结束)</summary>
        internal const int PierceLaunch = 202;
        /// <summary>贯穿段末</summary>
        internal const int PierceEnd = 224;
        /// <summary>恢复段末，状态总长</summary>
        internal const int RecoverEnd = 268;
        /// <summary>保底出口</summary>
        private const int HardTimeout = 330;

        /// <summary>绞紧后钢环半径</summary>
        internal const float HoldRadius = 235f;
        /// <summary>收紧终态半径</summary>
        internal const float TightRadius = 170f;
        /// <summary>探针驻留半径</summary>
        private const float ProbeRadius = 148f;
        /// <summary>贯穿速度</summary>
        private const float PierceSpeed = 86f;
        #endregion

        //探针索引仅服务端有效，客户端保持-1(镜像 ProbeMatrix 的做法)
        private readonly int[] probeIndices = [-1, -1, -1, -1];
        private bool probesSpawned;
        private bool crossBeat1Fired;
        private bool crossBeat2Fired;
        private bool pierceBoomFired;
        private Vector2 rearAnchor;

        public DestroyerCoilCrushState() {
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            context.SkipDefaultMovement = true;
            probesSpawned = false;
            crossBeat1Fired = false;
            crossBeat2Fired = false;
            pierceBoomFired = false;
            rearAnchor = Vector2.Zero;

            //绞紧咆哮
            SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = -0.15f, Volume = 1.1f }, context.Npc.Center);
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Vector2 center = new(npc.ai[0], npc.ai[1]);

            //全程头无接触伤，连段伤害由被抓端节拍结算
            npc.damage = 0;

            Timer++;

            //猎物离场(死亡/传送)提前收招：直接切巡空，ai[2]同步保证各端一致
            if (!VaultUtils.isClient && Timer < SilenceEnd) {
                int victimIndex = (int)npc.ai[3];
                Player victim = victimIndex >= 0 && victimIndex < Main.maxPlayers ? Main.player[victimIndex] : null;
                if (!victim.Alives() || victim.ghost || victim.Distance(center) > 1600f) {
                    return new DestroyerPatrolState();
                }
            }

            if (Timer <= SeizeEnd) {
                UpdateSeize(context, center);
            }
            else if (Timer <= CrossEnd) {
                UpdateProbeCross(context, center);
            }
            else if (Timer <= SqueezeEnd) {
                UpdateSqueeze(context, center);
            }
            else if (Timer <= SilenceEnd) {
                UpdateSilence(context, center);
            }
            else if (Timer <= PierceEnd) {
                UpdatePierce(context, center);
            }
            else if (Timer <= RecoverEnd) {
                UpdateRecover(context);
            }
            else {
                return new DestroyerPatrolState();
            }

            //保底出口
            if (Timer >= HardTimeout && !VaultUtils.isClient) {
                return new DestroyerPatrolState();
            }

            return null;
        }

        #region 幕一 收环绞紧

        private void UpdateSeize(DestroyerStateContext context, Vector2 center) {
            NPC npc = context.Npc;
            float t = Timer / (float)SeizeEnd;
            //三次幂急缩，最后一刻到位
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            float radius = MathHelper.Lerp(DestroyerCoilLockState.LockRadius, HoldRadius, ease);

            //hit-stop：合拢瞬间环体近乎定格
            float angSpeed = 0.16f;
            if (Timer >= SeizeEnd - 6 && Timer <= SeizeEnd - 2) {
                angSpeed = 0.008f;
                DestroyerChargeWave.Push(npc.whoAmI, 0f, 1f, 1f, fullBody: true);
            }

            DriveRing(context, center, radius, angSpeed);
            context.OrbitalVisual = 2;
            context.JawCommand = Timer > SeizeEnd - 8 ? 2 : 1;

            //合拢定格帧：铛的一声+音爆环
            if (Timer == SeizeEnd - 6) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.6f, Volume = 1.1f }, center);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 0.8f }, center);
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), center, Vector2.Zero,
                        ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 0);
                }
                //被抓者的震屏走运镜片段，这里只给旁观者
                if (!VaultUtils.isServer && Main.myPlayer != (int)npc.ai[3]) {
                    DestroyerMotionFX.CameraPunch(center, 8f, 16, "DestroyerCoilSeize");
                }
            }
        }

        #endregion

        #region 幕二 探针十字

        private void UpdateProbeCross(DestroyerStateContext context, Vector2 center) {
            NPC npc = context.Npc;
            DriveRing(context, center, HoldRadius, 0.085f);
            context.OrbitalVisual = 0;
            context.JawCommand = 1;

            //探针脱离：从体节位置弹出，占位到环内四象限
            if (!probesSpawned && Timer == SeizeEnd + 4) {
                probesSpawned = true;
                if (!VaultUtils.isClient) {
                    SpawnProbes(context, center);
                    npc.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Item25 with { Pitch = -0.2f, Volume = 0.9f }, center);
            }

            //驱动探针驻位(服务端)
            DriveProbes(center);

            //十字节拍一：横向(东西探针)电弧贯穿环心
            if (!crossBeat1Fired && Timer == SeizeEnd + 32) {
                crossBeat1Fired = true;
                if (!VaultUtils.isClient) {
                    SpawnProbeArc(context, 0, 2);
                }
            }
            //十字节拍二：纵向(南北探针)电弧+横向补一道，成完整十字
            if (!crossBeat2Fired && Timer == SeizeEnd + 58) {
                crossBeat2Fired = true;
                if (!VaultUtils.isClient) {
                    SpawnProbeArc(context, 1, 3);
                    SpawnProbeArc(context, 0, 2);
                }
            }

            //扫射期环体保持中等蓄力光
            DestroyerChargeWave.Push(npc.whoAmI, 0f, 1f, 0.35f, fullBody: true);
        }

        /// <summary>四探针从最近体节弹出(服务端)</summary>
        private void SpawnProbes(DestroyerStateContext context, Vector2 center) {
            for (int k = 0; k < 4; k++) {
                Vector2 spawnPos = center + (MathHelper.PiOver2 * k).ToRotationVector2() * ProbeRadius;
                //ai3=-1 走 ProbeAI 阵列接管分支，随生成包同步
                int idx = NPC.NewNPC(context.Npc.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y,
                    NPCID.Probe, 0, 0f, 0f, 0f, -1f);
                if (idx >= 0 && idx < Main.maxNPCs) {
                    probeIndices[k] = idx;
                    Main.npc[idx].velocity = Main.rand.NextVector2Circular(3f, 3f);
                    Main.npc[idx].netUpdate = true;
                }
            }
        }

        /// <summary>探针驻位在环内四象限，缓慢公转(服务端驱动，客户端靠NPC同步)</summary>
        private void DriveProbes(Vector2 center) {
            if (VaultUtils.isClient) {
                return;
            }
            float baseRot = (Timer - SeizeEnd) * 0.004f;
            for (int k = 0; k < 4; k++) {
                if (probeIndices[k] < 0) {
                    continue;
                }
                NPC probe = Main.npc[probeIndices[k]];
                if (!probe.active || probe.type != NPCID.Probe) {
                    probeIndices[k] = -1;
                    continue;
                }
                Vector2 hold = center + (MathHelper.PiOver2 * k + baseRot).ToRotationVector2() * ProbeRadius;
                probe.velocity = (hold - probe.Center) * 0.2f;
                probe.rotation = (center - probe.Center).ToRotation();
                probe.ai[3] = -1f;
                probe.damage = 0;
            }
        }

        /// <summary>对位探针间拉电弧，弧线必过环心(服务端)</summary>
        private void SpawnProbeArc(DestroyerStateContext context, int a, int b) {
            if (probeIndices[a] < 0 || probeIndices[b] < 0) {
                return;
            }
            NPC pa = Main.npc[probeIndices[a]];
            NPC pb = Main.npc[probeIndices[b]];
            if (!pa.active || pa.type != NPCID.Probe || !pb.active || pb.type != NPCID.Probe) {
                return;
            }
            Projectile.NewProjectile(context.Npc.GetSource_FromAI(), pa.Center, Vector2.Zero,
                ModContent.ProjectileType<DestroyerArc>(), 0, 0f, Main.myPlayer, pa.whoAmI, pb.whoAmI);
            //放电后坐
            pa.velocity -= (pb.Center - pa.Center).SafeNormalize(Vector2.Zero) * 5f;
            pb.velocity -= (pa.Center - pb.Center).SafeNormalize(Vector2.Zero) * 5f;
        }

        #endregion

        #region 幕三 通电收紧

        private void UpdateSqueeze(DestroyerStateContext context, Vector2 center) {
            NPC npc = context.Npc;
            float t = (Timer - CrossEnd) / (float)(SqueezeEnd - CrossEnd);
            float radius = MathHelper.Lerp(HoldRadius, TightRadius, t * t);

            //收紧越深转速越快，通电嘶鸣升调；探针继续驻位旁观
            DriveRing(context, center, radius, MathHelper.Lerp(0.09f, 0.12f, t));
            DriveProbes(center);
            context.OrbitalVisual = 0;
            context.JawCommand = 1;

            //环上电弧越收越密
            int arcInterval = (int)MathHelper.Lerp(18f, 10f, t);
            if (!VaultUtils.isClient && (Timer - CrossEnd) % Math.Max(arcInterval, 8) == 0) {
                SpawnRingArc(context);
            }

            //两声收紧应力，升调
            if (Timer == CrossEnd + 12 || Timer == CrossEnd + 44) {
                float pitch = Timer == CrossEnd + 12 ? -0.35f : -0.1f;
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = pitch, Volume = 0.95f }, center);
            }

            //全环电光随收紧爬升
            DestroyerChargeWave.Push(npc.whoAmI, 0f, 1f, 0.4f + 0.5f * t, fullBody: true);
        }

        /// <summary>贯穿前静默：电弧停，光骤暗，只剩环体慢转——尖啸前的吸气</summary>
        private void UpdateSilence(DestroyerStateContext context, Vector2 center) {
            NPC npc = context.Npc;
            DriveRing(context, center, TightRadius, 0.05f);
            DriveProbes(center);
            context.OrbitalVisual = 1;
            context.JawCommand = 2;

            if (Timer == SqueezeEnd + 2) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.75f, Volume = 0.7f }, center);
            }
        }

        /// <summary>环上随机取相隔约1/3圈两节拉弧(服务端，限存量)</summary>
        private void SpawnRingArc(DestroyerStateContext context) {
            var segments = context.BodySegments;
            if (segments.Count < 12) {
                return;
            }
            int arcType = ModContent.ProjectileType<DestroyerArc>();
            int alive = 0;
            foreach (var proj in Main.ActiveProjectiles) {
                if (proj.type == arcType) {
                    alive++;
                }
            }
            if (alive >= 6) {
                return;
            }
            int i = Main.rand.Next(segments.Count);
            int j = (i + segments.Count / 3 + Main.rand.Next(-3, 4) + segments.Count) % segments.Count;
            NPC a = segments[i];
            NPC b = segments[j];
            if (!a.active || !b.active || a.whoAmI == b.whoAmI) {
                return;
            }
            Projectile.NewProjectile(context.Npc.GetSource_FromAI(), a.Center, Vector2.Zero,
                arcType, 0, 0f, Main.myPlayer, a.whoAmI, b.whoAmI);
        }

        #endregion

        #region 幕四 贯穿收尾

        private void UpdatePierce(DestroyerStateContext context, Vector2 center) {
            NPC npc = context.Npc;
            context.JawCommand = 1;

            //仰起：离环上抬，pow(t,8)迟滞后撤蓄势
            if (Timer <= PierceLaunch) {
                if (rearAnchor == Vector2.Zero) {
                    Vector2 radial = (npc.Center - center).SafeNormalize(-Vector2.UnitY);
                    rearAnchor = center + radial * (TightRadius + 430f);
                }
                float t = (Timer - SilenceEnd) / (float)(PierceLaunch - SilenceEnd);
                float reel = (float)Math.Pow(t, 8) * 120f;
                Vector2 hold = rearAnchor + (rearAnchor - center).SafeNormalize(Vector2.Zero) * reel;
                Vector2 desired = (hold - npc.Center) * 0.16f;
                if (desired.Length() > 34f) {
                    desired = desired.SafeNormalize(Vector2.Zero) * 34f;
                }
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.28f);
                FaceTarget(npc, center, MathHelper.Lerp(0.3f, 0.1f, t));
                context.OrbitalVisual = 1;
                context.SetChargeState(1, t);
                context.DashDirection = (center - npc.Center).SafeNormalize(Vector2.UnitY);

                //贯穿起跳帧
                if (Timer == PierceLaunch) {
                    context.ResetChargeState();
                    Vector2 dir = (center - npc.Center).SafeNormalize(Vector2.UnitY);
                    npc.velocity = dir * PierceSpeed;
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                    npc.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.5f, Volume = 1.15f }, center);
                    if (!VaultUtils.isClient) {
                        DestroyerHeatWakeProj.EnsureForHead(npc);
                    }
                    if (!VaultUtils.isServer) {
                        DestroyerMotionFX.SpawnDashBurst(npc.Center, dir);
                    }
                }
                return;
            }

            //贯穿飞行：过环心引爆终结冲击
            context.OrbitalVisual = 2;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            if (!pierceBoomFired && npc.Distance(center) < 130f) {
                pierceBoomFired = true;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), center, Vector2.Zero,
                        ModContent.ProjectileType<DestroyerShockwave>(), 0, 0f, Main.myPlayer, 2);
                    //探针随终结冲击一并殉爆
                    KillProbes();
                }
                if (!VaultUtils.isServer) {
                    DestroyerMotionFX.SpawnImpactBlast(center, 1.35f);
                    //被抓者的终结震屏由运镜片段负责
                    if (Main.myPlayer != (int)npc.ai[3]) {
                        DestroyerMotionFX.CameraPunch(center, 12f, 24, "DestroyerCoilPierce", npc.velocity);
                    }
                }
                DestroyerChargeWave.Push(npc.whoAmI, 0f, 1f, 1f, fullBody: true);
            }
        }

        #endregion

        #region 幕五 散热恢复

        private void UpdateRecover(DestroyerStateContext context) {
            NPC npc = context.Npc;
            context.OrbitalVisual = 3;
            context.JawCommand = 0;

            //阶梯刹车，环体自然散开
            float spd = npc.velocity.Length();
            float brake = spd > 40f ? 0.93f : spd > 25f ? 0.95f : 0.97f;
            npc.velocity *= brake;
            if (npc.velocity.Length() > 0.5f) {
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            }

            if (!VaultUtils.isServer && Timer % 4 == 0) {
                DestroyerMotionFX.SpawnBrakeSparks(npc);
            }

            //恢复段中点清掉探针
            if (Timer == PierceEnd + 20 && !VaultUtils.isClient) {
                KillProbes();
            }
        }

        #endregion

        /// <summary>沿环心定半径公转，体节因跟随链自然贴上钢环</summary>
        private static void DriveRing(DestroyerStateContext context, Vector2 center, float radius, float angSpeed) {
            NPC npc = context.Npc;

            Vector2 rel = npc.Center - center;
            float nextAngle = rel.ToRotation() + angSpeed;
            Vector2 orbitTarget = center + nextAngle.ToRotationVector2() * radius;
            Vector2 desired = orbitTarget - npc.Center;
            //速度不超过剩余距离，低角速(顿帧/静默)时才能真正定住不抖
            float speed = Math.Min(desired.Length(), MathHelper.Clamp(desired.Length() / 4f, 14f, 58f));
            npc.velocity = desired.SafeNormalize(Vector2.Zero) * speed;
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
        }

        /// <summary>清演出探针(服务端)</summary>
        private void KillProbes() {
            for (int k = 0; k < 4; k++) {
                if (probeIndices[k] < 0) {
                    continue;
                }
                NPC probe = Main.npc[probeIndices[k]];
                probeIndices[k] = -1;
                if (!probe.active || probe.type != NPCID.Probe) {
                    continue;
                }
                probe.life = 0;
                probe.HitEffect();
                probe.active = false;
                probe.netUpdate = true;
            }
        }

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 0;
            context.JawCommand = 0;
            context.Npc.damage = context.Npc.defDamage;
            //命中冷却与残余探针清理(服务端)
            if (!VaultUtils.isClient) {
                context.GrabCooldownTimer = DestroyerCoilLockState.GrabCooldown;
                KillProbes();
            }
        }
    }
}
