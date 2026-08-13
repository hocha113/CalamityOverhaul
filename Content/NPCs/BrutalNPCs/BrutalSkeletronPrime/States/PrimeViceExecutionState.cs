using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.States
{
    /// <summary>
    /// 老虎钳处刑（投技）：钳臂抓住玩家高举 → 锯臂贴身研磨 → 炮+激光贴脸齐射 → 钳臂打桩砸地。<br/>
    /// 服务端在 <see cref="HeadPrimeAI.TryBeginViceExecution"/> 锁存脚本锚点后切入本状态；
    /// 时间线各端本地推进（同 PrimeDeathState 容差），四臂位形由 <see cref="ChoreographArm"/> 全端计算，
    /// 被抓玩家位移/伤害由其自己客户端在 PrimeVicePerformancePlayer 中施加
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)PrimeStateIndex.ViceExecution, typeof(PrimeStateContext))]
    internal class PrimeViceExecutionState : PrimeStateBase
    {
        public override string StateName => "ViceExecution";
        public override PrimeStateIndex StateIndex => PrimeStateIndex.ViceExecution;

        #region 时间线常量

        /// <summary>抓握顿帧结束</summary>
        internal const int ClampEnd = 18;
        /// <summary>高举到展示位结束</summary>
        internal const int HoistEnd = 60;
        /// <summary>锯磨接触开始</summary>
        internal const int GrindContactStart = 80;
        /// <summary>锯磨阶段结束</summary>
        internal const int GrindEnd = 124;
        /// <summary>齐射瞄准开始</summary>
        internal const int VolleyAimStart = 124;
        /// <summary>齐射爆发帧</summary>
        internal const int VolleyFireTick = 150;
        /// <summary>齐射阶段结束（此后爆发前静默）</summary>
        internal const int VolleyEnd = 178;
        /// <summary>打桩上提开始</summary>
        internal const int RaiseStart = 202;
        /// <summary>上提顶点帧</summary>
        internal const int ApexTick = 210;
        /// <summary>砸地冲击帧</summary>
        internal const int ImpactTick = 217;
        /// <summary>钉地压制结束，玩家释放</summary>
        internal const int PinEnd = 234;
        /// <summary>投技总时长（释放后为恢复拍）</summary>
        internal const int TotalFrames = 272;

        /// <summary>高举高度 px</summary>
        internal const float HoistHeight = 220f;
        /// <summary>打桩上提额外高度 px</summary>
        internal const float RaiseHeight = 100f;
        /// <summary>钳口到钳体中心距离 px</summary>
        internal const float JawGap = 52f;

        //臂存活掩码位
        internal const int MaskSaw = 1;
        internal const int MaskCannon = 2;
        internal const int MaskLaser = 4;

        //冷却（帧）
        internal const int FullCooldown = 1500;
        internal const int WhiffCooldown = 600;
        internal const int AbortCooldown = 900;

        #endregion

        #region 伤害节拍表

        /// <summary>单拍定义：触发帧 / 钳伤系数 / 所需臂掩码（0=无条件）</summary>
        internal readonly struct GrabBeat
        {
            public readonly int Tick;
            public readonly float Fraction;
            public readonly int RequiredMask;
            public GrabBeat(int tick, float fraction, int requiredMask) {
                Tick = tick;
                Fraction = fraction;
                RequiredMask = requiredMask;
            }
        }

        /// <summary>连段节拍：夹击/研磨×2/齐射/砸地，总系数≈3.25×钳伤，另受累计硬帽与致死钳位约束</summary>
        internal static readonly GrabBeat[] Beats = [
            new GrabBeat(2, 0.5f, 0),
            new GrabBeat(88, 0.4f, MaskSaw),
            new GrabBeat(108, 0.4f, MaskSaw),
            new GrabBeat(VolleyFireTick, 0.8f, MaskCannon | MaskLaser),
            new GrabBeat(ImpactTick, 1.15f, 0),
        ];

        #endregion

        #region 脚本纯函数（全端一致）

        /// <summary>处刑侧向：钳臂天然侧（Side=-1 → +1），钳臂缺失回退 +1</summary>
        internal static float GetSideX() {
            int idx = CWRWorld.primeVice;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC vice = Main.npc[idx];
                if (vice.active && vice.type == NPCID.PrimeVice) {
                    return -vice.ai[PrimeAiSlots.ArmSide];
                }
            }
            return 1f;
        }

        /// <summary>被抓玩家的锚点时间线：抓点→弧线高举→展示→上提→急砸→钉地</summary>
        internal static Vector2 PlayerAnchor(int t, Vector2 grabStart, Vector2 slamPoint, float sideX) {
            Vector2 display = grabStart + new Vector2(0f, -HoistHeight);

            if (t < ClampEnd) {
                //原地被夹，微震
                return grabStart + ClampJitter(t);
            }
            if (t < HoistEnd) {
                float p = MathHelper.SmoothStep(0f, 1f, (t - ClampEnd) / (float)(HoistEnd - ClampEnd));
                Vector2 pos = Vector2.Lerp(grabStart, display, p);
                //弧线上举
                pos.X += sideX * 46f * (float)Math.Sin(p * MathHelper.Pi);
                return pos;
            }
            if (t < RaiseStart) {
                //展示位微沉浮
                return display + new Vector2(0f, (float)Math.Sin(t * 0.07f) * 5f);
            }
            Vector2 apex = display + new Vector2(0f, -RaiseHeight);
            if (t < ApexTick) {
                float p = (t - RaiseStart) / (float)(ApexTick - RaiseStart);
                return Vector2.Lerp(display, apex, 1f - (float)Math.Pow(1f - p, 3));
            }
            if (t < ImpactTick) {
                //加速下砸，冲击帧抵达
                float p = (t - ApexTick) / (float)(ImpactTick - ApexTick);
                return Vector2.Lerp(apex, slamPoint, p * p * p);
            }
            //钉地压制
            return slamPoint + ClampJitter(t) * 0.6f;
        }

        /// <summary>夹击/钉地微震，确定性</summary>
        private static Vector2 ClampJitter(int t) {
            return new Vector2((float)Math.Sin(t * 2.7f), (float)Math.Sin(t * 3.4f + 1.3f)) * 2f;
        }

        /// <summary>从头部 Override 数据求当前玩家锚点</summary>
        internal static Vector2 PlayerAnchorFor(HeadPrimeAI headAI, int t) {
            return PlayerAnchor(t, headAI.GrabStartPoint, headAI.GrabSlamPoint, GetSideX());
        }

        /// <summary>砸地点：从抓取点向下探地，无地则空中定点</summary>
        internal static Vector2 FindSlamPoint(Vector2 grabStart) {
            int tx = (int)(grabStart.X / 16f);
            int ty = (int)(grabStart.Y / 16f);
            for (int dy = 0; dy < 50; dy++) {
                int y = ty + dy;
                if (!WorldGen.InWorld(tx, y, 20)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tx, y);
                if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])) {
                    return new Vector2(grabStart.X, y * 16f - 24f);
                }
            }
            return grabStart + new Vector2(0f, 480f);
        }

        #endregion

        #region 头部状态机

        public override void OnEnter(PrimeStateContext context) {
            base.OnEnter(context);
            context.ViceExecutionTick = 0;
            context.Npc.velocity *= 0.5f;
        }

        public override IPrimeState OnUpdate(PrimeStateContext context) {
            NPC npc = context.Npc;
            npc.damage = 0;
            context.FrameMode = 0;
            context.ViceExecutionTick = Timer;

            HeadPrimeAI owner = context.Owner;
            Vector2 grabStart = owner.GrabStartPoint;
            Vector2 slamPoint = owner.GrabSlamPoint;
            float sideX = GetSideX();
            Vector2 display = grabStart + new Vector2(0f, -HoistHeight);

            UpdateHeadMotion(npc, display, sideX);

            //打桩蓄力期推热感预警
            if (Timer >= RaiseStart && Timer < ImpactTick) {
                context.SetChargeState(1, (Timer - RaiseStart) / (float)(ImpactTick - RaiseStart));
            }
            else {
                context.ResetChargeState();
            }

            PlayBeatEffects(context, grabStart, slamPoint, sideX);

            //服务端异常出口：目标失效/逃逸、钳臂缺失
            if (!VaultUtils.isClient && ShouldAbort(owner)) {
                owner.viceExecutionCooldown = AbortCooldown;
                npc.TargetClosest();
                return new PrimeCommandSequenceState();
            }

            Timer++;
            if (Timer >= TotalFrames && !VaultUtils.isClient) {
                npc.TargetClosest();
                return new PrimeCommandSequenceState();
            }
            return null;
        }

        public override void OnExit(PrimeStateContext context) {
            base.OnExit(context);
            //清投技锁存槽，被抓玩家侧据此立刻解锁
            HeadPrimeAI owner = context.Owner;
            owner.ai[PrimeAiSlots.OverrideGrabTarget] = 0f;
            owner.ai[PrimeAiSlots.OverrideGrabArmsMask] = 0f;
            owner.ai[PrimeAiSlots.OverrideGrabStartX] = 0f;
            owner.ai[PrimeAiSlots.OverrideGrabStartY] = 0f;
            owner.ai[PrimeAiSlots.OverrideGrabSlamX] = 0f;
            owner.ai[PrimeAiSlots.OverrideGrabSlamY] = 0f;
            context.ViceExecutionTick = 0;
            if (!VaultUtils.isClient) {
                context.Npc.netUpdate = true;
            }
        }

        /// <summary>头部退到展示位侧后方凝视，打桩期后仰</summary>
        private void UpdateHeadMotion(NPC npc, Vector2 display, float sideX) {
            Vector2 dest = display + new Vector2(-sideX * 230f, -20f);
            if (Timer >= RaiseStart) {
                dest += new Vector2(-sideX * 30f, -50f);
            }
            npc.velocity = Vector2.Lerp(npc.velocity, (dest - npc.Center) * 0.045f, 0.2f);
            LeanTowards(npc, display);
        }

        /// <summary>被抓目标失效或被传送逃逸则断投</summary>
        private bool ShouldAbort(HeadPrimeAI owner) {
            int targetIndex = owner.GrabTargetIndex;
            if (targetIndex < 0 || targetIndex >= Main.maxPlayers) {
                return true;
            }
            Player target = Main.player[targetIndex];
            if (!target.active || target.dead || target.ghost) {
                return true;
            }
            //回忆药水等瞬移：偏离脚本锚点过远即断投
            Vector2 expected = PlayerAnchorFor(owner, Timer);
            if (target.Center.Distance(expected) > 1000f) {
                return true;
            }
            //钳臂被清（理论上编排期无敌，兜底）
            int viceIdx = CWRWorld.primeVice;
            if (viceIdx < 0 || viceIdx >= Main.maxNPCs
                || !Main.npc[viceIdx].active || Main.npc[viceIdx].type != NPCID.PrimeVice) {
                return true;
            }
            return false;
        }

        #endregion

        #region 世界空间节拍演出（全客户端可见）

        /// <summary>按本地时间线播节拍音效与粒子，声音位置化自然衰减</summary>
        private void PlayBeatEffects(PrimeStateContext context, Vector2 grabStart, Vector2 slamPoint, float sideX) {
            if (VaultUtils.isServer) {
                return;
            }
            int t = Timer;
            int mask = context.Owner.GrabArmsMask;
            Vector2 anchor = PlayerAnchor(t, grabStart, slamPoint, sideX);

            //t2 抓握顿帧：钳口轰合
            if (t == 2) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = -0.5f, Volume = 1.1f }, grabStart);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f, Volume = 0.7f }, grabStart);
                SpawnSparkRing(grabStart, 18, 7f);
                PrimeVicePerformancePlayer.RequestShake(6f, 10);
            }
            //高举伺服
            if (t == 24 || t == 44) {
                SoundEngine.PlaySound(SoundID.Item22 with { Pitch = -0.4f, Volume = 0.45f }, anchor);
            }
            //研磨节拍（锯在场）
            if ((mask & MaskSaw) != 0 && (t == 88 || t == 108)) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = 0.3f, Volume = 0.9f }, anchor);
                SpawnSparkRing(anchor + new Vector2(-sideX * 30f, 0f), 12, 9f);
                PrimeVicePerformancePlayer.RequestShake(5f, 8);
            }
            //齐射警报，节奏加密
            if ((mask & (MaskCannon | MaskLaser)) != 0 && (t == 128 || t == 137 || t == 145)) {
                float pitch = (t - 128) / 17f * 0.4f;
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = pitch, Volume = 0.8f }, anchor);
            }
            //齐射爆发
            if ((mask & (MaskCannon | MaskLaser)) != 0 && t == VolleyFireTick) {
                SoundEngine.PlaySound(SoundID.Item33 with { Pitch = -0.3f, Volume = 1.1f }, anchor);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.2f, Volume = 0.9f }, anchor);
                PrimeVicePerformancePlayer.RequestShake(8f, 14);
            }
            //打桩上提液压
            if (t == RaiseStart) {
                SoundEngine.PlaySound(SoundID.Item61 with { Pitch = -0.5f, Volume = 0.8f }, anchor);
            }
            //下砸热浪
            if (t >= ApexTick && t < ImpactTick) {
                PrimeScreenEffects.PushHeatWake(anchor, MathHelper.PiOver2, 0.55f);
            }
            //砸地冲击
            if (t == ImpactTick) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.4f, Volume = 1.2f }, slamPoint);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, Volume = 1f }, slamPoint);
                SpawnImpactDebris(slamPoint);
                PrimeScreenEffects.PushShockRing(slamPoint, 0.85f, 560f, 22);
                PrimeVicePerformancePlayer.RequestShake(13f, 22);
            }
            //释放开钳
            if (t == PinEnd) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.4f, Volume = 0.8f }, slamPoint);
            }
        }

        /// <summary>环状火花喷溅</summary>
        private static void SpawnSparkRing(Vector2 pos, int count, float speed) {
            Color warm = Color.Lerp(new Color(255, 150, 60), Color.LightGoldenrodYellow, 0.35f);
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(0.5f, 1f) * speed;
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, warm, Main.rand.NextFloat(0.8f, 1.4f))
                    .Configure(true, Main.rand.Next(14, 24));
            }
        }

        /// <summary>砸地尘柱与碎石</summary>
        private static void SpawnImpactDebris(Vector2 slamPoint) {
            Color warm = new Color(255, 130, 60);
            PRTLoader.NewParticle<PRT_MechExplosion>(slamPoint, Vector2.Zero, warm, 1.6f).Configure(30, warm);

            //上喷尘柱
            for (int i = 0; i < 26; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-3.5f, 3.5f), -Main.rand.NextFloat(3f, 10f));
                Dust dust = Dust.NewDustDirect(slamPoint + new Vector2(Main.rand.NextFloat(-40f, 40f), 0f),
                    1, 1, Main.rand.NextBool() ? DustID.Stone : DustID.Smoke, vel.X, vel.Y,
                    80, default, Main.rand.NextFloat(1.2f, 2.2f));
                dust.noGravity = Main.rand.NextBool(3);
            }
            //横扫火花
            for (int i = 0; i < 16; i++) {
                float angle = -MathHelper.Pi * Main.rand.NextFloat();
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 11f);
                PRTLoader.NewParticle<PRT_Spark>(slamPoint, vel,
                    Color.Lerp(warm, Color.LightGoldenrodYellow, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1f, 1.7f)).Configure(true, Main.rand.Next(16, 28));
            }
            //扬尘
            for (int i = 0; i < 8; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.8f, 2f));
                PRTLoader.NewParticle<PRT_Smoke>(slamPoint + Main.rand.NextVector2Circular(30f, 8f), vel,
                    Color.Lerp(new Color(95, 85, 75), new Color(40, 36, 32), Main.rand.NextFloat()),
                    Main.rand.NextFloat(1f, 1.6f)).Configure(Main.rand.Next(40, 70), 0.7f, Main.rand.NextFloat(-0.04f, 0.04f));
            }
            Lighting.AddLight(slamPoint, warm.ToVector3() * 1.4f);
        }

        #endregion

        #region 四臂编排（由 PrimeArm 每帧调用，全端本地计算 + 服务端广播纠偏）

        /// <summary>投技期间四臂位形与视觉总入口</summary>
        internal static void ChoreographArm(NPC npc, NPC head, PrimeArmStateContext ctx) {
            HeadPrimeAI headAI = head.GetOverride<HeadPrimeAI>();
            if (headAI == null) {
                return;
            }

            int t = headAI.ViceExecutionTick;
            Vector2 grabStart = headAI.GrabStartPoint;
            Vector2 slamPoint = headAI.GrabSlamPoint;
            float sideX = GetSideX();
            Vector2 anchor = PlayerAnchor(t, grabStart, slamPoint, sideX);

            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.velocity = Vector2.Zero;

            switch (npc.type) {
                case NPCID.PrimeVice:
                    ChoreographVice(npc, ctx, t, anchor);
                    break;
                case NPCID.PrimeSaw:
                    ChoreographSaw(npc, ctx, t, anchor, sideX);
                    break;
                case NPCID.PrimeCannon:
                    ChoreographShooter(npc, ctx, t, anchor, sideX, isLaser: false);
                    break;
                case NPCID.PrimeLaser:
                    ChoreographShooter(npc, ctx, t, anchor, sideX, isLaser: true);
                    break;
            }
        }

        /// <summary>钳臂：钳口向下压持玩家，全程与玩家锚点刚性同步</summary>
        private static void ChoreographVice(NPC npc, PrimeArmStateContext ctx, int t, Vector2 anchor) {
            //编排期 ArmPreUpdate 不运行，冲击抖动自行衰减
            ctx.ImpactIntensity *= 0.88f;
            Vector2 dest = anchor + new Vector2(0f, -JawGap);
            if (t >= PinEnd) {
                //释放后缓慢抬钳
                dest = anchor + new Vector2(0f, -JawGap - (t - PinEnd) * 1.4f);
                ctx.ClawOpen = true;
            }
            else {
                ctx.ClawOpen = false;
            }

            //抓握首拍强吸附，其后刚性跟随
            float lerp = t < ClampEnd ? 0.35f : 0.6f;
            npc.Center = Vector2.Lerp(npc.Center, dest, lerp);

            //钳口伺服转向正下
            float diff = MathHelper.WrapAngle(0f - npc.rotation);
            npc.rotation += MathHelper.Clamp(diff, -0.2f, 0.2f);

            //夹持应力火花
            if (!VaultUtils.isServer && t < PinEnd && t % 9 == 0) {
                Vector2 pos = anchor + Main.rand.NextVector2Circular(14f, 14f);
                Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.FireworkFountain_Red,
                    0, 0, 100, Color.Yellow, Main.rand.NextFloat(0.7f, 1.2f));
                dust.velocity = Main.rand.NextVector2Circular(2f, 2f);
                dust.noGravity = true;
            }
        }

        /// <summary>锯臂：贴身就位 → 沿玩家身体往复研磨 → 撤开，其余时间侧后待命</summary>
        private static void ChoreographSaw(NPC npc, PrimeArmStateContext ctx, int t, Vector2 anchor, float sideX) {
            Vector2 staging = anchor + new Vector2(-sideX * 185f, -40f);
            Vector2 dest;
            float spinTarget;

            if (t >= HoistEnd && t < GrindContactStart) {
                //就位起转
                float p = (t - HoistEnd) / (float)(GrindContactStart - HoistEnd);
                dest = Vector2.Lerp(staging, anchor + new Vector2(-sideX * 64f, 6f), MathHelper.SmoothStep(0f, 1f, p));
                spinTarget = p;
            }
            else if (t >= GrindContactStart && t < GrindEnd - 6) {
                //贴身研磨，锯身沿玩家纵向往复
                float sweep = (float)Math.Sin((t - GrindContactStart) * 0.33f) * 40f;
                dest = anchor + new Vector2(-sideX * 60f, 6f + sweep);
                spinTarget = 1f;

                //研磨火花瀑布
                if (!VaultUtils.isServer && t % 2 == 0) {
                    Vector2 contact = Vector2.Lerp(npc.Center, anchor, 0.72f);
                    Vector2 vel = new Vector2(sideX * Main.rand.NextFloat(2f, 6f), Main.rand.NextFloat(1f, 5f));
                    PRTLoader.NewParticle<PRT_Spark>(contact, vel,
                        Color.Lerp(new Color(255, 180, 70), Color.White, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.7f, 1.3f)).Configure(true, Main.rand.Next(10, 18));
                }
                //锯片啸叫
                if (!VaultUtils.isServer && t % 24 == 0) {
                    SoundEngine.PlaySound(SoundID.Item23 with { Volume = 0.5f, Pitch = 0.5f }, npc.Center);
                }
            }
            else if (t >= GrindEnd - 6 && t < VolleyAimStart + 8) {
                //撤离
                dest = staging;
                spinTarget = 0.3f;
            }
            else {
                dest = staging;
                spinTarget = t < HoistEnd ? 0.15f : 0.25f;
            }

            npc.Center = Vector2.Lerp(npc.Center, dest, 0.16f);
            AimArmAt(npc, anchor, 0.18f);

            //编排期锯片转速自驱（ArmPreUpdate 不运行）
            ctx.TargetSpinSpeed = spinTarget;
            ctx.SpinSpeed = MathHelper.Lerp(ctx.SpinSpeed, spinTarget, 0.1f);
        }

        /// <summary>炮/激光臂：扇形贴脸就位 → 蓄力警报 → 齐射后坐硝烟，其余时间侧翼待命</summary>
        private static void ChoreographShooter(NPC npc, PrimeArmStateContext ctx, int t, Vector2 anchor, float sideX, bool isLaser) {
            Vector2 staging = anchor + new Vector2(sideX * 205f, isLaser ? 60f : -95f);
            Vector2 station = anchor + new Vector2(sideX * 150f, isLaser ? 44f : -66f);
            Vector2 dest = staging;

            if (t >= VolleyAimStart && t < VolleyEnd) {
                dest = station;
                //齐射后坐踢回
                if (t >= VolleyFireTick) {
                    float kick = 30f * (float)Math.Pow(0.86, t - VolleyFireTick);
                    dest -= ctx.AimDirection * kick;
                }
            }

            npc.Center = Vector2.Lerp(npc.Center, dest, 0.15f);
            AimArmAt(npc, anchor, 0.2f);
            ctx.AimDirection = (anchor - npc.Center).SafeNormalize(Vector2.UnitX);

            //激光蓄力辉光（绘制层读 ChargeGlow 变色）
            if (isLaser) {
                if (t >= VolleyAimStart && t < VolleyFireTick) {
                    ctx.ChargeGlow = MathHelper.Clamp((t - VolleyAimStart) / 26f, 0f, 1f);
                }
                else {
                    ctx.ChargeGlow *= 0.9f;
                }
            }

            if (VaultUtils.isServer) {
                return;
            }

            //蓄力汇聚粒子
            if (t >= VolleyAimStart && t < VolleyFireTick && Main.rand.NextBool(3)) {
                Vector2 muzzle = npc.Center + ctx.AimDirection * 52f;
                Vector2 pos = muzzle + Main.rand.NextVector2Circular(22f, 22f);
                Dust dust = Dust.NewDustDirect(pos, 1, 1, DustID.FireworkFountain_Red,
                    0, 0, 100, isLaser ? Color.Cyan : Color.Orange, Main.rand.NextFloat(0.8f, 1.4f));
                dust.velocity = (muzzle - pos) * 0.12f;
                dust.noGravity = true;
            }

            //齐射帧：炮口焰与贴脸束闪
            if (t == VolleyFireTick) {
                Vector2 muzzle = npc.Center + ctx.AimDirection * 50f;
                ctx.RecoilIntensity = 12f;
                PRTLoader.NewParticle<PRT_Light>(muzzle, Vector2.Zero,
                    isLaser ? Color.Cyan : new Color(255, 170, 60), 1.6f).Configure(14);
                for (int i = 0; i < 10; i++) {
                    Vector2 vel = ctx.AimDirection.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(4f, 10f);
                    PRTLoader.NewParticle<PRT_Spark>(muzzle, vel,
                        isLaser ? Color.Cyan : Color.Orange, Main.rand.NextFloat(0.9f, 1.5f))
                        .Configure(true, Main.rand.Next(12, 20));
                }
                //贴脸射流视觉：沿弹道撒亮点
                for (int i = 0; i < 8; i++) {
                    Vector2 pos = Vector2.Lerp(muzzle, anchor, (i + 0.5f) / 8f);
                    PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero,
                        isLaser ? Color.Cyan : new Color(255, 150, 50), 0.7f).Configure(10);
                }
            }
            //硝烟
            if (t > VolleyFireTick && t < VolleyEnd && ctx.RecoilIntensity > 2f && Main.rand.NextBool(2)) {
                Vector2 smokePos = npc.Center + ctx.AimDirection * 45f;
                Dust dust = Dust.NewDustDirect(smokePos, 1, 1, DustID.Smoke,
                    ctx.AimDirection.X * 2f, ctx.AimDirection.Y * 2f, 100, default, Main.rand.NextFloat(1.2f, 2f));
                dust.noGravity = false;
            }
            ctx.RecoilIntensity *= 0.88f;
        }

        /// <summary>臂伺服瞄准（编排层本地实现，不依赖状态基类）</summary>
        private static void AimArmAt(NPC npc, Vector2 worldTarget, float maxStep) {
            float targetRotation = (worldTarget - npc.Center).ToRotation() - MathHelper.PiOver2;
            float diff = MathHelper.WrapAngle(targetRotation - npc.rotation);
            npc.rotation += MathHelper.Clamp(diff, -maxStep, maxStep);
        }

        #endregion
    }
}
