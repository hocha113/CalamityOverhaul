using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.Projectiles.Boss.Destroyer;
using CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.States
{
    /// <summary>
    /// 侧舷波次 → 炽核熔射：
    /// <br/>1. 压缩版侧舷齐射——一道充能波沿躯体奔向头部，波峰扫过的体节朝侧舷喷等离子弹（单程，删掉旧的回扫尿点）；
    /// <br/>2. 口吐光柱杀招——蠕虫昂首蓄能、锁定预警、静默吸气，随后口器喷出
    /// <see cref="DestroyerMawBeamProj"/> 巨型红色熔射光柱，缓慢横扫半场。
    /// <para>光柱扫射角速度刻意压低 + 展开期无伤害 + 锁定后停止跟踪（公平阀），避免远端切向无解。</para>
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DestroyerStateIndex.LaserBarrage, typeof(DestroyerStateContext))]
    internal class DestroyerLaserBarrageState : DestroyerStateBase
    {
        public override string StateName => "LaserBarrage";
        public override DestroyerStateIndex StateIndex => DestroyerStateIndex.LaserBarrage;

        #region 节奏常量
        private const int BarrageCharge = 26;
        private const int BeamWindup = 52;
        /// <summary>蓄力末段提前锁定瞄准的帧数（锁定后不再跟踪玩家——公平阀）</summary>
        private const int LockLead = 22;
        private const int Silence = 7;
        private const int Outro = 12;
        #endregion

        private int lastFiredIndex = -1;
        /// <summary>光柱横扫期间关闭"远距回归瞬移阀"，避免扫射中途头部瞬移导致光柱跳变</summary>
        private bool suppressFarSnap;

        //以下字段在锁定帧由 Timer 确定性求出，光柱由服务端按此权威参数生成并同步
        private float startAngle;
        private float sweepSpeedSigned;
        private Vector2 anchorPos;

        public DestroyerLaserBarrageState() {
        }

        public override bool AllowFarSnap => !suppressFarSnap;

        private int BarrageSweep(DestroyerStateContext ctx) => ctx.IsDeathMode ? 60 : 70;
        private float BoltSpeed(DestroyerStateContext ctx) => ctx.IsDeathMode ? 7.5f : 5.5f;

        /// <summary>横扫半弧：决定光柱总扫角 = 2×arcHalf；越大越华丽，但角速度同步抬高，注意公平阈值</summary>
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

            //服务端决定横扫方位并经 ai[3] 同步，保证多端光柱方向一致
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

            //侧舷阵位：绕玩家缓慢拉弧线，让身体侧面朝向玩家（仅齐射阶段）
            if (Timer <= barrageEnd) {
                float orbitAngle = (npc.Center - player.Center).ToRotation() + 0.016f;
                Vector2 orbitTarget = player.Center + orbitAngle.ToRotationVector2() * 760f;
                SetMovement(context, orbitTarget, 13f, 0.55f);
                context.SlitherStrength = 0.4f;
            }

            //阶段1：压缩充能预热——一道波从尾部推向头部，不开火
            if (Timer <= chargeEnd) {
                float p = Timer / (float)chargeEnd;
                context.SetChargeState(2, p * 0.6f);
                DestroyerChargeWave.Push(npc.whoAmI, 1f - p, 0.3f, 0.35f + 0.4f * p);
                if (Timer == chargeEnd - 8) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.55f, Volume = 0.9f }, npc.Center);
                }
                return null;
            }

            //阶段2：单程侧舷齐射（尾→头），删去旧的间隙+回扫尿点
            if (Timer <= barrageEnd) {
                context.ResetChargeState();
                float sweepProgress = (Timer - chargeEnd) / (float)barrageSweep;
                DoSweep(context, sweepProgress);
                return null;
            }

            //阶段3：口吐光柱蓄能——昂首飞向高空阵位、汇聚能量，末段锁定并打出预警
            if (Timer <= windupEnd) {
                UpdateBeamWindup(context, (int)Timer, barrageEnd, lockFrame);
                return null;
            }

            //阶段4：静默吸气——视觉骤停、机体微滞，转向起始角
            if (Timer <= silenceEnd) {
                UpdateSilence(context);
                return null;
            }

            //阶段5：释放——口器喷出巨型熔射光柱
            if (Timer == fireFrame) {
                FireBeam(context);
                return null;
            }

            //阶段6：缓慢横扫——头部锚定持位，口器朝向权威光束角随其转动
            if (Timer <= beamEnd) {
                UpdateBeamSweep(context);
                return null;
            }

            //阶段7：收尾回场
            if (Timer <= outroEnd) {
                UpdateOutro(context, (Timer - beamEnd) / (float)Outro);
                return null;
            }

            return new DestroyerPatrolState();
        }

        #region 侧舷齐射

        /// <summary>
        /// 推进一帧波次齐射：推送充能波视觉，并在服务端对波峰扫过的体节开火
        /// </summary>
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

            //公平阀：头部远在玩家视野外时只推进波形不开火，避免"屏幕外射来弹幕"
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
            //体节轴向：体节rotation = 朝向前节方向 + PiOver2
            Vector2 bodyAxis = (segment.rotation - MathHelper.PiOver2).ToRotationVector2();
            Vector2 normal = bodyAxis.RotatedBy(MathHelper.PiOver2);
            Vector2 toPlayer = (context.Target.Center - segment.Center).SafeNormalize(Vector2.UnitY);
            //法线取朝向玩家的一侧
            if (Vector2.Dot(normal, toPlayer) < 0f) {
                normal = -normal;
            }

            //侧舷方向 + 小幅瞄准修正：波形弹幕但仍具威胁
            Vector2 dir = (normal * 0.62f + toPlayer * 0.38f).SafeNormalize(Vector2.UnitY);
            Vector2 velocity = dir * BoltSpeed(context);

            int damage = (int)(HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(context.Npc, ProjectileID.DeathLaser)) * 0.5f);
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

            //锁定帧：冻结瞄准、求出权威参数、打出预警线 + 方向扇
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
            //大半弧无法用单扇形如实预告：起点射线 + 起始扇区告知出生位置与旋转方向（内部仅服务端生成、同步）
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
            context.ResetChargeState();//充能视觉骤停——下一刻就是巨炮
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
                int damage = HeadPrimeAI.SetMultiplier(CWRRef.GetProjectileDamage(npc, ProjectileID.DeathLaser));
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

            //持位：刹车 + 轻微回拉锚点（重型机体后坐后缓缓归位，pivot 基本不动以保证扫射可读）
            npc.velocity *= 0.9f;
            npc.velocity += (anchorPos - npc.Center) * 0.012f;
            if (npc.velocity.Length() > 8f) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * 8f;
            }

            //口器朝向权威光束角（读取已同步的光柱弹幕，多端一致）
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
