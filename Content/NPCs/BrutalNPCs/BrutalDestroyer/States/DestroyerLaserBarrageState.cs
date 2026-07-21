using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>侧舷齐射→口吐光柱慢扫</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.LaserBarrage, typeof(DestroyerStateContext))]
    internal class DestroyerLaserBarrageState : DestroyerStateBase
    {
        public override string StateName => "LaserBarrage";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.LaserBarrage;

        #region 节奏常量
        private const int BarrageCharge = 26;
        private const int BeamWindup = 52;
        /// <summary>蓄力末锁定帧，锁定后停跟</summary>
        private const int LockLead = 22;
        private const int Silence = 7;
        private const int Outro = 12;
        #endregion

        private int lastFiredIndex = -1;
        /// <summary>横扫关远距瞬移，防光柱跳变</summary>
        private bool suppressFarSnap;

        //锁定帧由Timer定参，服务端生光柱
        private float startAngle;
        private float sweepSpeedSigned;
        private Vector2 anchorPos;

        public DestroyerLaserBarrageState() {
        }

        public override bool AllowFarSnap => !suppressFarSnap;

        private int BarrageSweep(DestroyerStateContext ctx) => ctx.IsDeathMode ? 60 : 70;
        private float BoltSpeed(DestroyerStateContext ctx) => ctx.IsDeathMode ? 7.5f : 5.5f;

        /// <summary>横扫半弧=总扫角一半</summary>
        private float ArcHalf(DestroyerStateContext ctx) {
            float a = 1.3f;
            if (ctx.IsEnraged) {
                a += 0.2f;
            }
            if (ctx.IsDeathMode) {
                a += 0.1f;
            }
            return a;
        }

        private static int SideSign(NPC npc) => (int)npc.ai[3] == 0 ? 1 : -1;

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            lastFiredIndex = -1;
            suppressFarSnap = false;
            context.RefreshBodySegments();

            //服务端横扫方位进ai[3]
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = Main.rand.Next(2);
                context.Npc.netUpdate = true;
            }
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            int barrageSweep = BarrageSweep(context);
            int chargeEnd = BarrageCharge;
            int barrageEnd = chargeEnd + barrageSweep;
            int windupEnd = barrageEnd + BeamWindup;
            int lockFrame = windupEnd - LockLead;
            int silenceEnd = windupEnd + Silence;
            int fireFrame = silenceEnd + 1;
            int beamEnd = fireFrame + DestroyerMawBeamProj.TotalLife;
            int outroEnd = beamEnd + Outro;

            Timer++;

            //侧舷拉弧(齐射)
            if (Timer <= barrageEnd) {
                float orbitAngle = (npc.Center - player.Center).ToRotation() + 0.016f;
                Vector2 orbitTarget = player.Center + orbitAngle.ToRotationVector2() * 760f;
                SetMovement(context, orbitTarget, 13f, 0.55f);
                context.SlitherStrength = 0.4f;
            }

            //阶段1 充能预热
            if (Timer <= chargeEnd) {
                float p = Timer / (float)chargeEnd;
                context.SetChargeState(2, p * 0.6f);
                DestroyerChargeWave.Push(npc.whoAmI, 1f - p, 0.3f, 0.35f + 0.4f * p);
                if (Timer == chargeEnd - 8) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.55f, Volume = 0.9f }, npc.Center);
                }
                return null;
            }

            //阶段2 侧舷齐射尾→头
            if (Timer <= barrageEnd) {
                context.ResetChargeState();
                float sweepProgress = (Timer - chargeEnd) / (float)barrageSweep;
                DoSweep(context, sweepProgress);
                return null;
            }

            //阶段3 光柱蓄能+预警
            if (Timer <= windupEnd) {
                UpdateBeamWindup(context, (int)Timer, barrageEnd, lockFrame);
                return null;
            }

            //阶段4 静默对起始角
            if (Timer <= silenceEnd) {
                UpdateSilence(context);
                return null;
            }

            //阶段5 喷光柱
            if (Timer == fireFrame) {
                FireBeam(context);
                return null;
            }

            //阶段6 横扫跟权威角
            if (Timer <= beamEnd) {
                UpdateBeamSweep(context);
                return null;
            }

            //阶段7 回场
            if (Timer <= outroEnd) {
                UpdateOutro(context, (Timer - beamEnd) / (float)Outro);
                return null;
            }

            return new DestroyerPatrolState();
        }

        #region 侧舷齐射

        /// <summary>波次齐射，服务端体节开火</summary>
        private void DoSweep(DestroyerStateContext context, float sweepProgress) {
            var segments = context.BodySegments;
            int count = segments.Count;
            if (count == 0) {
                return;
            }

            //波峰从尾(1)推向头(0)
            float phase = 1f - sweepProgress;
            DestroyerChargeWave.Push(context.Npc.whoAmI, phase, 0.14f, 1f);

            if (VaultUtils.isClient) {
                return;
            }

            int idx = Math.Clamp((int)(phase * (count - 1)), 0, count - 1);

            //头在屏外只推进波形不开火
            if (context.Npc.Distance(context.Target.Center) > 1900f) {
                lastFiredIndex = idx;
                return;
            }

            if (lastFiredIndex < 0) {
                lastFiredIndex = idx;
                return;
            }
            if (idx == lastFiredIndex) {
                return;
            }

            int step = idx > lastFiredIndex ? 1 : -1;
            for (int i = lastFiredIndex + step; step > 0 ? i <= idx : i >= idx; i += step) {
                //隔节开火，留出弹幕间隙
                if (i % 2 != 0) {
                    continue;
                }
                NPC segment = segments[i];
                if (segment.active) {
                    FireBolt(context, segment);
                }
            }
            lastFiredIndex = idx;
        }

        private void FireBolt(DestroyerStateContext context, NPC segment) {
            //体节轴=朝前节+PiOver2
            Vector2 bodyAxis = (segment.rotation - MathHelper.PiOver2).ToRotationVector2();
            Vector2 normal = bodyAxis.RotatedBy(MathHelper.PiOver2);
            Vector2 toPlayer = (context.Target.Center - segment.Center).SafeNormalize(Vector2.UnitY);
            //法线取朝向玩家的一侧
            if (Vector2.Dot(normal, toPlayer) < 0f) {
                normal = -normal;
            }

            //侧舷+小幅修正
            Vector2 dir = (normal * 0.62f + toPlayer * 0.38f).SafeNormalize(Vector2.UnitY);
            Vector2 velocity = dir * BoltSpeed(context);

            int damage = (int)(HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(context.Npc, ProjectileID.DeathLaser)));
            Projectile.NewProjectile(segment.GetSource_FromAI(), segment.Center, velocity,
                ModContent.ProjectileType<DestroyerBolt>(), damage, 0f, Main.myPlayer, 0, 1);
        }

        #endregion

        #region 炽核熔射光柱

        private void UpdateBeamWindup(DestroyerStateContext context, int timer, int barrageEnd, int lockFrame) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = false;
            context.JawCommand = 1;//昂首蓄能，强制张开口器

            //昂首飞向玩家上方偏侧的高空阵位
            Vector2 hover = player.Center + new Vector2(SideSign(npc) * 300f, -380f);
            SetMovement(context, hover, 22f, 0.9f);
            context.AccelRate = 0.08f;

            float p = MathHelper.Clamp((timer - barrageEnd) / (float)BeamWindup, 0f, 1f);
            context.SetChargeState(2, 0.35f + 0.65f * p);
            DestroyerChargeWave.Push(npc.whoAmI, 1f - p, 0.32f, 0.5f + 0.5f * p);

            //口器向心汇聚的流光（仅客户端）
            if (!VaultUtils.isServer && timer % 2 == 0 && DestroyerMotionFX.OnScreen(npc.Center)) {
                Vector2 from = npc.Center + Main.rand.NextVector2CircularEdge(150f, 150f);
                PRTLoader.NewParticle<PRT_Spark>(from, (npc.Center - from) * 0.1f,
                    Color.Lerp(new Color(255, 150, 70), Color.White, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1f, 1.7f))?.Configure(false, 16);
            }

            //锁定帧出预警+方向扇
            if (timer == lockFrame) {
                LockAimAndTelegraph(context);
            }
        }

        private void LockAimAndTelegraph(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int side = SideSign(npc);
            float arcHalf = ArcHalf(context);
            float aimAngle = (player.Center - npc.Center).ToRotation();

            startAngle = aimAngle - arcHalf * side;
            sweepSpeedSigned = 2f * arcHalf / DestroyerMawBeamProj.SweepFrames * side;

            int teleDuration = LockLead + Silence + 2;
            //大半弧用起点射线+扇区预告
            PrimeTelegraphLine.SpawnLine(npc, npc.Center, startAngle, teleDuration, true);

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.6f, Volume = 1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.8f, Volume = 0.6f }, npc.Center);
            }
            npc.netUpdate = true;
        }

        private void UpdateSilence(DestroyerStateContext context) {
            NPC npc = context.Npc;
            context.SkipDefaultMovement = true;
            context.JawCommand = 1;
            context.ResetChargeState();//充能骤停，下一刻巨炮
            context.OrbitalVisual = 1;
            npc.damage = 0;
            npc.velocity *= 0.8f;
            //转向起始角，张开口器对准
            npc.rotation = npc.rotation.AngleLerp(startAngle + MathHelper.PiOver2, 0.35f);
        }

        private void FireBeam(DestroyerStateContext context) {
            NPC npc = context.Npc;
            context.SkipDefaultMovement = true;
            suppressFarSnap = true;
            anchorPos = npc.Center;

            Vector2 startDir = startAngle.ToRotationVector2();
            npc.rotation = startAngle + MathHelper.PiOver2;
            npc.velocity = -startDir * 7f;//释放后坐冲量（mass is reaction）
            npc.damage = 0;
            context.OrbitalVisual = 2;
            context.JawCommand = 1;

            if (!VaultUtils.isClient) {
                int damage = HeadPrimeAI.SetMultiplier(46);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<DestroyerMawBeamProj>(), damage, 0f, Main.myPlayer,
                    npc.whoAmI, startAngle, sweepSpeedSigned);
                npc.netUpdate = true;
            }

            if (!VaultUtils.isServer) {
                Vector2 muzzle = npc.Center + startDir * DestroyerMawBeamProj.MuzzleOffset;
                DestroyerMotionFX.SpawnDashBurst(muzzle, startDir);
                DestroyerMotionFX.CameraPunch(muzzle, 12f, 20, "DestroyerMawBeamFire", startDir);
                MachineEffect.TriggerSkyFlash(muzzle, 1f);
                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.2f, Volume = 1.1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1f }, npc.Center);
                DestroyerChargeWave.Push(npc.whoAmI, 0f, 1f, 1f, fullBody: true);
            }
        }

        private void UpdateBeamSweep(DestroyerStateContext context) {
            NPC npc = context.Npc;
            context.SkipDefaultMovement = true;
            suppressFarSnap = true;
            context.OrbitalVisual = 2;
            context.JawCommand = 1;
            npc.damage = 0;

            //持位刹车回拉，pivot稳
            npc.velocity *= 0.9f;
            npc.velocity += (anchorPos - npc.Center) * 0.012f;
            if (npc.velocity.Length() > 8f) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * 8f;
            }

            //口器跟权威光束角
            Projectile beam = DestroyerMawBeamProj.FindFor(npc.whoAmI);
            float beamAngle = beam != null ? beam.rotation : startAngle;
            npc.rotation = npc.rotation.AngleLerp(beamAngle + MathHelper.PiOver2, 0.5f);

            //全身白热脉冲，戏剧化呈现"严重过载"
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f);
            DestroyerChargeWave.Push(npc.whoAmI, 0f, 1f, pulse, fullBody: true);
        }

        private void UpdateOutro(DestroyerStateContext context, float progress) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = false;
            suppressFarSnap = false;
            context.OrbitalVisual = 0;
            context.JawCommand = 0;
            context.SlitherStrength = 0.5f;
            npc.damage = npc.defDamage;

            //恢复巡航姿态、缓缓回到玩家上方
            SetMovement(context, player.Center + new Vector2(0, -460f), MathHelper.Lerp(8f, 16f, progress), 0.5f);
        }

        #endregion

        public override void OnExit(DestroyerStateContext context) {
            base.OnExit(context);
            context.SkipDefaultMovement = false;
            context.OrbitalVisual = 0;
            context.JawCommand = 0;
            context.AccelRate = 0.055f;
            suppressFarSnap = false;
            context.Npc.damage = context.Npc.defDamage;
        }
    }
}
