using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>侧舷齐射→咬合收势→突进入位→冻结锁定→口吐光柱慢扫</summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.LaserBarrage, typeof(DestroyerStateContext))]
    internal class DestroyerLaserBarrageState : DestroyerStateBase
    {
        public override string StateName => "LaserBarrage";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.LaserBarrage;

        #region 节奏常量
        private const int BarrageCharge = 26;
        /// <summary>齐射后咬合刹车，宣告下一幕</summary>
        private const int BrakeBeat = 10;
        /// <summary>突进入位窗口，入位即提前收招</summary>
        private const int InterceptDash = 44;
        /// <summary>昂首定位，位置冻结+鼻锁玩家</summary>
        private const int PoiseTime = 26;
        /// <summary>锁定亮线保持</summary>
        private const int LockHold = 16;
        private const int Silence = 8;
        private const int Outro = 26;
        /// <summary>截止仍远于此则遮罩闪现入镜</summary>
        private const float BlinkDistance = 1200f;
        #endregion

        private int lastFiredIndex = -1;
        /// <summary>入位后位置已承诺，关远距瞬移防预警跳变</summary>
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

        /// <summary>入位锚点：玩家预测位的侧上方，保证演出入镜</summary>
        private static Vector2 InterceptAnchor(DestroyerStateContext ctx) {
            return ctx.Target.Center + ctx.Target.velocity * 16f
                + new Vector2(SideSign(ctx.Npc) * 380f, -300f);
        }

        public override void OnEnter(DestroyerStateContext context) {
            base.OnEnter(context);
            lastFiredIndex = -1;
            suppressFarSnap = false;
            startAngle = 0f;
            sweepSpeedSigned = 0f;
            anchorPos = context.Npc.Center;
            context.RefreshBodySegments();

            //服务端横扫方位进ai[3]
            if (!VaultUtils.isClient) {
                context.Npc.ai[3] = Main.rand.Next(2);
                context.Npc.netUpdate = true;
            }
        }

        public override IDestroyerState OnUpdate(DestroyerStateContext context) {
            NPC npc = context.Npc;

            int barrageSweep = BarrageSweep(context);
            int chargeEnd = BarrageCharge;
            int barrageEnd = chargeEnd + barrageSweep;
            int brakeEnd = barrageEnd + BrakeBeat;
            int dashEnd = brakeEnd + InterceptDash;
            int poiseEnd = dashEnd + PoiseTime;
            int lockEnd = poiseEnd + LockHold;
            int silenceEnd = lockEnd + Silence;
            int fireFrame = silenceEnd + 1;
            int beamEnd = fireFrame + DestroyerMawBeamProj.TotalLife;
            int outroEnd = beamEnd + Outro;

            Timer++;

            //侧舷拉弧(齐射)
            if (Timer <= barrageEnd) {
                Player player = context.Target;
                context.SkipDefaultMovement = false;
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

            //阶段2 侧舷齐射尾→头，蓄力保温不清零
            if (Timer <= barrageEnd) {
                context.SetChargeState(2, 0.22f);
                float sweepProgress = (Timer - chargeEnd) / (float)barrageSweep;
                DoSweep(context, sweepProgress);
                return null;
            }

            //阶段3a 咬合收势：齐射落幕的静默拍
            if (Timer <= brakeEnd) {
                UpdateBrakeBeat(context, barrageEnd);
                return null;
            }

            //阶段3b 突进入位：定向起跳直扑镜头
            if (Timer <= dashEnd) {
                UpdateIntercept(context, brakeEnd, dashEnd);
                return null;
            }

            //阶段3c 昂首定位：位置冻结，鼻锁玩家
            if (Timer <= poiseEnd) {
                UpdatePoise(context, dashEnd, poiseEnd);
                return null;
            }

            //阶段3d 锁定亮线：所见即所射
            if (Timer <= lockEnd) {
                UpdateLockHold(context, poiseEnd);
                return null;
            }

            //阶段4 静默对起始角，咬合→张口
            if (Timer <= silenceEnd) {
                UpdateSilence(context, lockEnd);
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

            //阶段7 散热回场
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

        #region 入位与锁定

        /// <summary>咬合刹车，蓄力硬切归零=风暴前的吸气</summary>
        private void UpdateBrakeBeat(DestroyerStateContext context, int barrageEnd) {
            NPC npc = context.Npc;
            context.SkipDefaultMovement = true;
            suppressFarSnap = true;
            context.ResetChargeState();
            context.JawCommand = 2;
            npc.damage = 0;

            npc.velocity *= 0.78f;
            if (npc.velocity.Length() > 0.8f) {
                npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
            }

            if (Timer == barrageEnd + 1) {
                //液压锁止应力声
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.5f, Volume = 0.8f }, npc.Center);
            }
            if (!VaultUtils.isServer && Timer % 2 == 0) {
                DestroyerMotionFX.SpawnBrakeSparks(npc);
            }
        }

        /// <summary>突进入位：一帧定速起跳，追踪锚点，入位提前收招，超时闪现兜底</summary>
        private void UpdateIntercept(DestroyerStateContext context, int brakeEnd, int dashEnd) {
            NPC npc = context.Npc;
            Player player = context.Target;
            context.SkipDefaultMovement = true;
            suppressFarSnap = true;
            context.JawCommand = 1;
            npc.damage = 0;

            Vector2 anchor = InterceptAnchor(context);
            float dist = npc.Distance(anchor);

            if (Timer == brakeEnd + 1) {
                //定向起跳：直接置速，清掉掉头损耗（launch is a set）
                Vector2 launchDir = (anchor - npc.Center).SafeNormalize(Vector2.UnitY);
                npc.velocity = launchDir * MathHelper.Clamp(dist / 16f, 24f, 46f);
                npc.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 0.9f }, npc.Center);
                if (!VaultUtils.isServer) {
                    DestroyerMotionFX.SpawnDashBurst(npc.Center, launchDir);
                    DestroyerMotionFX.CameraPunch(npc.Center, 4f, 10, "DestroyerMawIntercept", launchDir);
                }
            }

            //追踪入位，速度随距标定，甩不掉
            Vector2 desired = (anchor - npc.Center).SafeNormalize(Vector2.UnitY)
                * MathHelper.Clamp(dist / 13f, 24f, 52f);
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.15f);
            npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

            float p = MathHelper.Clamp((Timer - brakeEnd) / (float)InterceptDash, 0f, 1f);
            context.SetChargeState(2, 0.45f * p);
            DestroyerChargeWave.Push(npc.whoAmI, 1f - p, 0.3f, 0.3f + 0.4f * p);

            //入位提前收招，不等日程
            if (Timer < dashEnd && npc.WithinRange(anchor, 250f)) {
                Timer = dashEnd;
                return;
            }

            //截止仍远：遮罩闪现入镜，演出保底
            if (Timer == dashEnd && npc.Distance(player.Center) > BlinkDistance) {
                Vector2 oldPos = npc.Center;
                npc.Center = player.Center + new Vector2(SideSign(npc) * 380f, -300f);
                Vector2 inDir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                npc.velocity = inDir * 9f;
                npc.netUpdate = true;
                if (!VaultUtils.isServer) {
                    Vector2 jumpDir = (npc.Center - oldPos).SafeNormalize(Vector2.UnitY);
                    DestroyerMotionFX.SpawnDashBurst(oldPos, jumpDir);
                    DestroyerMotionFX.SpawnDashBurst(npc.Center, -jumpDir);
                    MachineEffect.TriggerSkyFlash(npc.Center, 0.8f);
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.4f, Volume = 0.9f }, npc.Center);
                }
            }
        }

        /// <summary>昂首定位：抓锚冻结，鼻锁玩家，蓄力反向漂移</summary>
        private void UpdatePoise(DestroyerStateContext context, int dashEnd, int poiseEnd) {
            NPC npc = context.Npc;
            Player player = context.Target;
            context.SkipDefaultMovement = true;
            suppressFarSnap = true;
            context.JawCommand = 1;
            npc.damage = 0;

            if (Timer == dashEnd + 1) {
                anchorPos = npc.Center;
                npc.netUpdate = true;
            }

            float p = MathHelper.Clamp((Timer - dashEnd) / (float)PoiseTime, 0f, 1f);
            //蓄力只升不降，接管提前收招的中途读数
            float charge = Math.Max(context.ChargeProgress, 0.45f + 0.4f * p);
            context.SetChargeState(2, charge);

            //持位刹车+身体避开自己的武器（drift-back while charging）
            Vector2 aimDir = DirectionToTarget(context);
            Vector2 holdPos = anchorPos - aimDir * (charge * charge * 130f);
            npc.velocity *= 0.86f;
            npc.velocity += (holdPos - npc.Center) * 0.02f;
            if (npc.velocity.Length() > 10f) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * 10f;
            }

            //鼻锁玩家，转率随蓄力衰减="锁线"
            FaceTarget(npc, player.Center, MathHelper.Lerp(0.24f, 0.1f, p));

            DestroyerChargeWave.Push(npc.whoAmI, 1f - p, 0.32f, 0.5f + 0.4f * p);

            if (!VaultUtils.isServer && DestroyerMotionFX.OnScreen(npc.Center)) {
                SpawnConvergingSpark(npc, 0.35f + 0.5f * charge);
                //低鸣震屏随蓄力平方爬升
                if (Timer % 7 == 0) {
                    DestroyerMotionFX.CameraPunch(npc.Center, 1f + 2.2f * charge * charge, 10, "DestroyerMawRumble");
                }
            }
        }

        /// <summary>锁定保持：位置已冻结，预警即真实弹道；鼻回摆起始角=横扫反向预备</summary>
        private void UpdateLockHold(DestroyerStateContext context, int poiseEnd) {
            NPC npc = context.Npc;
            context.SkipDefaultMovement = true;
            suppressFarSnap = true;
            context.JawCommand = 1;
            npc.damage = 0;

            if (Timer == poiseEnd + 1) {
                LockAimAndTelegraph(context);
            }

            float p = MathHelper.Clamp((Timer - poiseEnd) / (float)LockHold, 0f, 1f);
            float charge = Math.Max(context.ChargeProgress, 0.85f + 0.15f * p);
            context.SetChargeState(2, charge);

            //定点保持，禁止再跟随玩家
            npc.velocity *= 0.86f;
            npc.velocity += (anchorPos - npc.Center) * 0.02f;
            if (npc.velocity.Length() > 8f) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * 8f;
            }
            npc.rotation = npc.rotation.AngleLerp(startAngle + MathHelper.PiOver2, 0.22f);

            DestroyerChargeWave.Push(npc.whoAmI, 0f, 0.5f, 0.6f + 0.4f * p);

            if (!VaultUtils.isServer && DestroyerMotionFX.OnScreen(npc.Center)) {
                //72%后停粒子：尖啸前的吸气
                if (p < 0.72f) {
                    SpawnConvergingSpark(npc, 0.9f);
                }
                //沿弧切向流光指示扫向
                if (Timer % 2 == 0) {
                    SpawnSweepChevron(context);
                }
            }
        }

        /// <summary>口器向心汇聚流光（仅客户端）</summary>
        private static void SpawnConvergingSpark(NPC npc, float chance) {
            if (Main.rand.NextFloat() > chance) {
                return;
            }
            Vector2 from = npc.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(110f, 420f);
            PRTLoader.NewParticle<PRT_Spark>(from, (npc.Center - from) * 0.1f,
                Color.Lerp(new Color(255, 150, 70), Color.White, Main.rand.NextFloat()),
                Main.rand.NextFloat(1f, 1.7f))?.Configure(false, 16);
        }

        /// <summary>沿预定横扫弧的切向流光，预演扫向（仅客户端）</summary>
        private void SpawnSweepChevron(DestroyerStateContext context) {
            NPC npc = context.Npc;
            float sign = Math.Sign(sweepSpeedSigned);
            if (sign == 0f) {
                return;
            }
            float arcSpan = 2f * ArcHalf(context);
            float a = startAngle + Main.rand.NextFloat(0.05f, arcSpan * 0.4f) * sign;
            float r = Main.rand.NextFloat(150f, 340f);
            Vector2 pos = npc.Center + a.ToRotationVector2() * r;
            Vector2 vel = (a + MathHelper.PiOver2 * sign).ToRotationVector2() * (3f + r * 0.008f);
            PRTLoader.NewParticle<PRT_Spark>(pos, vel, new Color(255, 150, 70),
                Main.rand.NextFloat(1f, 1.5f))?.Configure(false, 14);
        }

        private void LockAimAndTelegraph(DestroyerStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int side = SideSign(npc);
            float arcHalf = ArcHalf(context);
            float aimAngle = (player.Center - npc.Center).ToRotation();

            startAngle = aimAngle - arcHalf * side;
            sweepSpeedSigned = 2f * arcHalf / DestroyerMawBeamProj.SweepFrames * side;
            anchorPos = npc.Center;

            //位置已冻结，锚头预警线即真实弹道起点，末段自带白闪倒计时
            int teleDuration = LockHold + Silence + 2;
            if (!VaultUtils.isClient) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, startAngle.ToRotationVector2(),
                    ModContent.ProjectileType<DestroyerStrikeTelegraph>(), 0, 0f, Main.myPlayer,
                    npc.whoAmI, -1, DestroyerStrikeTelegraph.PackParams(0, teleDuration));
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.6f, Volume = 1f }, npc.Center);
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.8f, Volume = 0.6f }, npc.Center);
            }
            npc.netUpdate = true;
        }

        private void UpdateSilence(DestroyerStateContext context, int lockEnd) {
            NPC npc = context.Npc;
            context.SkipDefaultMovement = true;
            suppressFarSnap = true;
            context.ResetChargeState();//充能骤停，下一刻巨炮
            context.OrbitalVisual = 1;
            npc.damage = 0;
            npc.velocity *= 0.7f;
            //转向起始角，先咬合再张口迎炮
            npc.rotation = npc.rotation.AngleLerp(startAngle + MathHelper.PiOver2, 0.4f);
            int st = Timer - lockEnd;
            context.JawCommand = st <= Silence - 3 ? 2 : 1;
            if (st == 1) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.25f, Volume = 0.6f }, npc.Center);
            }
        }

        #endregion

        #region 炽核熔射光柱

        private void FireBeam(DestroyerStateContext context) {
            NPC npc = context.Npc;
            context.SkipDefaultMovement = true;
            suppressFarSnap = true;
            anchorPos = npc.Center;

            Vector2 startDir = startAngle.ToRotationVector2();
            npc.rotation = startAngle + MathHelper.PiOver2;
            npc.velocity = -startDir * 9f;//释放后坐冲量（mass is reaction）
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

            //口器跟权威光束角，喷射时高频微颤
            Projectile beam = DestroyerMawBeamProj.FindFor(npc.whoAmI);
            float beamAngle = beam != null ? beam.rotation : startAngle;
            npc.rotation = npc.rotation.AngleLerp(beamAngle + MathHelper.PiOver2, 0.5f)
                + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 46f) * 0.012f;

            //全身白热脉冲，戏剧化呈现"严重过载"
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f);
            DestroyerChargeWave.Push(npc.whoAmI, 0f, 1f, pulse, fullBody: true);
        }

        private void UpdateOutro(DestroyerStateContext context, float progress) {
            NPC npc = context.Npc;
            Player player = context.Target;

            context.SkipDefaultMovement = false;
            suppressFarSnap = false;
            context.OrbitalVisual = 3;//散热
            context.JawCommand = 0;
            context.SlitherStrength = 0.5f * progress;
            //回场期免接触伤，OnExit统一恢复
            npc.damage = 0;

            //恢复巡航姿态、缓缓回到玩家上方
            SetMovement(context, player.Center + new Vector2(0, -460f), MathHelper.Lerp(8f, 16f, progress), 0.5f);

            if (!VaultUtils.isServer && Timer % 4 == 0) {
                DestroyerMotionFX.SpawnBrakeSparks(npc);
            }
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
