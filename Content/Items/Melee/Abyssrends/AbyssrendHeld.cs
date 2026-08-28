using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Abyssrends
{
    /// <summary>
    /// 裂渊持握。四拍：戳、侧斩、反斩、重戳。
    /// 斩击时间语法对齐纠缠之怨：回拉蓄力，蠕进后爆发过冲，收势先持停再回护持位。
    /// 戳刺走收-爆-停：压到死停后一两帧内爆出过冲，重戳到位带枪杆余震，然后真正停帧，收势才放软。
    /// 伤害窗只开在爆发段，碰撞按本帧扫过的弧判定。ai[0] 拍号 ai[1] 斩向
    /// </summary>
    internal class AbyssrendHeld : BaseHeldProj, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => AbyssrendFX.ItemTexture;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<Abyssrend>();

        private const float HeldScale = 0.88f;

        /// <summary>连击拍号 0戳 1斩 2反斩 3重戳</summary>
        private ref float ComboAi => ref Projectile.ai[0];
        /// <summary>斩向 ±1</summary>
        private ref float SwingDirAi => ref Projectile.ai[1];

        private int ComboStage => Math.Clamp((int)ComboAi, 0, 3);
        private bool IsThrust => ComboStage == 0 || ComboStage == 3;
        private bool IsFinisher => ComboStage == 3;

        //阶段时长（逻辑帧，攻速经 speedMul 缩放）
        private float WindupTime => IsThrust ? (IsFinisher ? 8f : 6f) : 6f;
        private float ActiveTime => IsThrust ? (IsFinisher ? 8f : 6f) : 7f;
        private float RecoverTime => IsThrust ? (IsFinisher ? 10f : 8f) : 7f;
        private float TotalTime => WindupTime + ActiveTime + RecoverTime;

        //斩击弧度与回拉，进度 0=发力位 1=收尾位
        private float SwingArc => ComboStage == 2 ? 3.3f : 3.1f;
        private const float PullbackAngle = 0.42f;
        private const float SwingGatherEnd = 0.20f;
        private const float SwingBurstEnd = 0.55f;

        //戳刺持距：玩家原地不动，刺的力量全靠刀在手里的行程，回拉和突出都要大
        private float StabPull => IsFinisher ? -46f : -34f;
        private float StabReach => IsFinisher ? 128f : 84f;

        //戳刺相位（active 段归一化时刻）：蓄势死停→爆发→余震回坐，其后定格
        private const float ThrustGatherEnd = 0.08f;
        private const float ThrustBurstEnd = 0.40f;
        private const float ThrustSettleEnd = 0.72f;

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;
        private float baseAngle;
        private float startAngle;
        private float endAngle;
        private float currentRotation;
        private float lastRotation;
        private float holdout;
        private float bodyLean;
        private bool leanApplied;
        private float sweepCollisionStart;
        private float sweepCollisionEnd;
        private bool sweepDamageActive;
        private bool slashVisualActive;
        private bool strikeSoundPlayed;
        private bool currentFired;
        private bool hitstopApplied;
        private float hitstopFrames;
        private float trailFade;
        private float lastHoldout;
        //平滑后的刺速，供残影与光带读取：顿帧时定格，运动停止后指数衰减
        private float stabSpeedVis;
        private Vector2 handPos;
        private Vector2 mainTip;
        private readonly HashSet<int> hitNPCs = [];

        //刀光按外缘弧长补点，最新样本在 [0]
        private const int TrailMax = 96;
        private const float TrailSampleSpacing = 16f;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;

        private float BladeLen => AbyssrendFX.BladeLength * HeldScale;
        private float ChamberAngle => startAngle - swingSign * PullbackAngle;
        private bool EdgeFlip => !IsThrust && swingSign * lockedDirection < 0;

        /// <summary>
        /// 上一拍收势停在哪，这一拍就从哪起手。
        /// 拍序固定：戳收在正前、正斩收在前下 0.75、反斩收在前上 0.75，
        /// 没有这条衔接，拍与拍之间刀会凭空瞬移
        /// </summary>
        private float EntryAngle => ComboStage switch {
            2 => baseAngle + 0.75f,
            3 => baseAngle - 0.75f,
            _ => baseAngle
        };

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void Initialize() {
            swingSign = SwingDirAi >= 0f ? 1 : -1;
            baseAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(baseAngle);
            lockedDirection = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
            Owner.direction = lockedDirection;

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            startAngle = baseAngle - swingSign * SwingArc * 0.5f;
            endAngle = baseAngle + swingSign * SwingArc * 0.5f;
            currentRotation = lastRotation = startAngle;
            sweepCollisionStart = sweepCollisionEnd = startAngle;
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<Abyssrend>() || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }
            if (elapsed >= TotalTime) {
                Projectile.Kill();
                return;
            }

            handPos = Owner.GetPlayerStabilityCenter();
            slashVisualActive = false;
            sweepDamageActive = false;
            sweepCollisionStart = sweepCollisionEnd = currentRotation;
            lastRotation = currentRotation;
            bodyLean = 0f;

            //顿帧：时间轴停走，几何整体冻结
            float step = speedMul;
            if (hitstopFrames > 0f) {
                hitstopFrames -= 1f;
                step = 0f;
            }

            if (IsThrust) {
                ThrustAI(step);
            }
            else {
                SlashAI(step);
            }

            UpdatePlayerPose();
            ApplyBodyLean();
            HandleParticles();
            HandleLight();
            elapsed += step;
        }

        private void SlashAI(float step) {
            float slashEnd = WindupTime + ActiveTime;
            if (elapsed < WindupTime) {
                //从上一拍的收势位连贯地拉到发力位
                float t = MathHelper.Clamp((elapsed + step) / WindupTime, 0f, 1f);
                currentRotation = MathHelper.Lerp(EntryAngle, ChamberAngle, EaseOutCubic(t));
                holdout = MathHelper.Lerp(6f, -10f, t);
                trailFade = 0f;
            }
            else if (elapsed < slashEnd) {
                slashVisualActive = true;
                float previousT = MathHelper.Clamp((elapsed - WindupTime) / ActiveTime, 0f, 1f);
                float t = MathHelper.Clamp((elapsed - WindupTime + step) / ActiveTime, 0f, 1f);
                float progress = GetSwingProgress(t);
                currentRotation = GetSwingRotation(progress);
                holdout = MathHelper.Lerp(-10f, 22f, SmoothStep01(progress));
                trailFade = 1f;

                //伤害窗=爆发段，覆盖本帧扫过的整段弧，快挥不穿怪
                float damageFrom = MathF.Max(previousT, SwingGatherEnd);
                float damageTo = MathF.Min(t, SwingBurstEnd);
                if (damageTo > damageFrom) {
                    sweepDamageActive = true;
                    sweepCollisionStart = GetSwingRotation(GetSwingProgress(damageFrom));
                    sweepCollisionEnd = GetSwingRotation(GetSwingProgress(damageTo));
                }

                if (!strikeSoundPlayed && t >= SwingGatherEnd) {
                    strikeSoundPlayed = true;
                    PlayStrikeSound();
                }

                PushTrailSamples();

                if (!currentFired && progress >= 0.6f) {
                    currentFired = true;
                    FireUndercurrent();
                }
            }
            else {
                //收势：先持停再回护持位，刀光随刀回收
                float t = MathHelper.Clamp((elapsed - slashEnd + step) / RecoverTime, 0f, 1f);
                const float hold = 0.30f;
                float returnT = SmoothStep01((t - hold) / (1f - hold));
                float guardAngle = baseAngle + swingSign * 0.75f;
                currentRotation = MathHelper.Lerp(endAngle, guardAngle, returnT);
                holdout = MathHelper.Lerp(22f, 6f, returnT);
                trailFade = 1f - SmoothStep01(t);
                TrimTrailToCurrentRotation();
            }
            mainTip = handPos + currentRotation.ToRotationVector2() * (BladeLen + holdout);
        }

        private void ThrustAI(float step) {
            currentRotation = baseAngle;
            float activeEnd = WindupTime + ActiveTime;
            float leanBack = IsFinisher ? -0.11f : -0.05f;
            float leanFwd = IsFinisher ? 0.15f : 0.08f;
            lastHoldout = holdout;
            if (elapsed < WindupTime) {
                //收：快拉慢定，压到底后只剩呼吸颤——蓄势的死停就是爆发的画框
                float t = MathHelper.Clamp((elapsed + step) / WindupTime, 0f, 1f);
                float ease = IsFinisher ? 1f - MathF.Pow(1f - t, 4f) : EaseOutCubic(t);
                currentRotation = MathHelper.Lerp(EntryAngle, baseAngle, ease);
                holdout = MathHelper.Lerp(10f, StabPull, ease) + MathF.Sin(elapsed * 1.4f) * 0.8f * t;
                bodyLean = leanBack * ease;
                trailFade = 0f;
            }
            else if (elapsed < activeEnd) {
                slashVisualActive = true;
                float t = MathHelper.Clamp((elapsed - WindupTime + step) / ActiveTime, 0f, 1f);
                float burst = GetThrustProgress(t);
                holdout = MathHelper.Lerp(StabPull, StabReach, burst);
                //身体跟着爆发帧一起甩出去，而不是全程匀速前倾
                bodyLean = MathHelper.Lerp(leanBack, leanFwd, MathHelper.Clamp(burst, 0f, 1f));
                trailFade = 1f;
                sweepDamageActive = t >= ThrustGatherEnd && t <= 0.92f;

                if (!strikeSoundPlayed && t >= ThrustGatherEnd) {
                    strikeSoundPlayed = true;
                    PlayStrikeSound();
                    SpawnThrustBurstFX();
                    //玩家不动，爆发的冲劲交给镜头：轻戳小推，重戳重锤
                    if (CWRClientConfig.Instance.ScreenVibration) {
                        Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                            handPos, baseAngle.ToRotationVector2(), IsFinisher ? 4.5f : 2f
                            , 5f, IsFinisher ? 7 : 4, 420f, FullName));
                    }
                }
                if (!currentFired && burst >= 0.5f) {
                    currentFired = true;
                    FireUndercurrent();
                }
            }
            else {
                //收势前段仍握满行程（停帧的后半），过了持停才温柔回收
                float t = MathHelper.Clamp((elapsed - activeEnd + step) / RecoverTime, 0f, 1f);
                const float hold = 0.30f;
                float returnT = SmoothStep01((t - hold) / (1f - hold));
                holdout = MathHelper.Lerp(StabReach, 8f, returnT);
                bodyLean = MathHelper.Lerp(leanFwd, 0f, returnT);
                trailFade *= 0.84f;
            }
            if (step > 0f) {
                stabSpeedVis = MathF.Max(MathF.Abs(holdout - lastHoldout), stabSpeedVis * 0.7f);
            }
            mainTip = handPos + currentRotation.ToRotationVector2() * (BladeLen + holdout);
        }

        //斩击进度：蠕进→爆发过冲→回坐。过冲量按弧长折算，反斩略大
        private float GetSwingProgress(float t) {
            const float creep = 0.05f;
            float path = SwingArc + PullbackAngle;
            float overshoot = 1f + (ComboStage == 2 ? 0.12f : 0.10f) / path;
            if (t < SwingGatherEnd) {
                return creep * SmoothStep01(t / SwingGatherEnd);
            }
            if (t < SwingBurstEnd) {
                float burstT = (t - SwingGatherEnd) / (SwingBurstEnd - SwingGatherEnd);
                return MathHelper.Lerp(creep, overshoot, SmoothStep01(burstT));
            }
            return MathHelper.Lerp(overshoot, 1f, SmoothStep01((t - SwingBurstEnd) / (1f - SwingBurstEnd)));
        }

        private float GetSwingRotation(float progress)
            => MathHelper.Lerp(ChamberAngle, endAngle, progress);

        //戳刺进度：蓄势段再压一丝，高次幂爆发首帧吃掉大半行程并过冲，
        //重戳带衰减余震回坐，之后死停——暴力在出生，不在中途
        private float GetThrustProgress(float t) {
            float overshoot = IsFinisher ? 1.16f : 1.09f;
            if (t < ThrustGatherEnd) {
                return -0.04f * SmoothStep01(t / ThrustGatherEnd);
            }
            if (t < ThrustBurstEnd) {
                float burstT = (t - ThrustGatherEnd) / (ThrustBurstEnd - ThrustGatherEnd);
                float snap = 1f - MathF.Pow(1f - burstT, IsFinisher ? 6f : 4.5f);
                return MathHelper.Lerp(-0.04f, overshoot, snap);
            }
            float settleT = MathHelper.Clamp((t - ThrustBurstEnd) / (ThrustSettleEnd - ThrustBurstEnd), 0f, 1f);
            if (IsFinisher) {
                //枪杆余震：两三帧衰减震颤，硬到位的证词
                float quiver = MathF.Cos(settleT * 7.5f) * MathF.Exp(-3.6f * settleT);
                return 1f + (overshoot - 1f) * quiver;
            }
            return MathHelper.Lerp(overshoot, 1f, SmoothStep01(settleT));
        }

        private void PushTrailSamples() {
            float delta = currentRotation - lastRotation;
            if (delta * swingSign <= 0.0001f) {
                TrimTrailToCurrentRotation();
                return;
            }

            float outerRadius = BladeLen + 22f;
            bool appendStart = trailCount == 0;
            int steps = GetAngularSteps(delta, outerRadius, TrailSampleSpacing, TrailMax - 1);
            int retained = Math.Min(trailCount, TrailMax - steps);
            if (retained > 0) {
                Array.Copy(trailRot, 0, trailRot, steps, retained);
            }
            for (int i = 0; i < steps; i++) {
                float amount = 1f - i / (float)steps;
                trailRot[i] = MathHelper.Lerp(lastRotation, currentRotation, amount);
            }
            trailCount = steps + retained;
            if (appendStart && trailCount < TrailMax) {
                trailRot[trailCount++] = lastRotation;
            }
        }

        //收势时把刀已折返经过的样本吃掉，刃口钉在刀上
        private void TrimTrailToCurrentRotation() {
            if (trailCount == 0) {
                return;
            }
            const float angleEpsilon = 0.0001f;
            int firstRetained = 0;
            while (firstRetained < trailCount
                && (trailRot[firstRetained] - currentRotation) * swingSign > angleEpsilon) {
                firstRetained++;
            }
            int retained = trailCount - firstRetained;
            bool headAlreadySampled = retained > 0
                && MathF.Abs(trailRot[firstRetained] - currentRotation) <= angleEpsilon;
            int targetOffset = headAlreadySampled ? 0 : 1;
            int copied = Math.Min(retained, TrailMax - targetOffset);
            if (copied > 0 && (firstRetained != targetOffset || firstRetained > 0)) {
                Array.Copy(trailRot, firstRetained, trailRot, targetOffset, copied);
            }
            trailRot[0] = currentRotation;
            trailCount = copied + targetOffset;
        }

        private static int GetAngularSteps(float delta, float radius, float targetSpacing, int maxSteps) {
            float arcLength = MathF.Abs(delta) * MathF.Max(radius, 1f);
            return Math.Clamp((int)MathF.Ceiling(arcLength / targetSpacing), 1, maxSteps);
        }

        private void UpdatePlayerPose() {
            Owner.direction = lockedDirection;
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;
            //蓄力时手臂收拢，爆发再抻满；后手扣在杆上，双手持械
            Player.CompositeArmStretchAmount stretch = elapsed < WindupTime
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, currentRotation - MathHelper.PiOver2);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters
                , currentRotation - MathHelper.PiOver2 + (swingSign * 0.28f));
            Projectile.Center = Vector2.Lerp(handPos, mainTip, 0.55f);
            Projectile.rotation = currentRotation;
            Projectile.timeLeft = 90;
        }

        //全身参与只留给重戳，轻拍身体保持稳
        private void ApplyBodyLean() {
            if (MathF.Abs(bodyLean) < 0.001f && !leanApplied) {
                return;
            }
            Owner.fullRotation = bodyLean * lockedDirection;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.height);
            leanApplied = true;
        }

        public override void OnKill(int timeLeft) {
            if (leanApplied) {
                Owner.fullRotation = 0f;
                leanApplied = false;
            }
        }

        private void PlayStrikeSound() {
            if (IsThrust) {
                SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with {
                    Pitch = IsFinisher ? -0.35f : 0.05f,
                    Volume = IsFinisher ? 0.85f : 0.6f
                }, handPos);
                SoundEngine.PlaySound(SoundID.Item71 with {
                    Pitch = IsFinisher ? -0.15f : 0.22f,
                    Volume = IsFinisher ? 0.5f : 0.35f
                }, handPos);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = 0.4f, Volume = 0.4f }, handPos);
            }
            else {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                    Pitch = ComboStage == 2 ? -0.05f : 0.18f,
                    Volume = 0.55f
                }, handPos);
                SoundEngine.PlaySound(SoundID.Item85 with { Pitch = 0.35f, Volume = 0.35f }, handPos);
            }
        }

        private void FireUndercurrent() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 dir = currentRotation.ToRotationVector2();
            Vector2 spawn = mainTip - dir * 8f;
            int shots = IsFinisher ? 2 : 1;
            int dmg = (int)(Projectile.damage * 0.55f);
            for (int i = 0; i < shots; i++) {
                float spread = shots == 1 ? 0f : (i == 0 ? -0.18f : 0.18f);
                Vector2 vel = dir.RotatedBy(spread) * (13.5f + ComboStage * 0.8f);
                int target = AbyssrendUndercurrent.FindTarget(spawn, Owner, 720f);
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), spawn, vel
                    , ModContent.ProjectileType<AbyssrendUndercurrent>()
                    , dmg, Projectile.knockBack * 0.35f, Owner.whoAmI
                    , ai0: Main.rand.NextFloat(1000f), ai1: target);
            }
        }

        //爆发帧的水花激射：枪尖排开深渊海水，沿刺线锥形喷出，落点即刺的终点
        private void SpawnThrustBurstFX() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = baseAngle.ToRotationVector2();
            Vector2 tip = handPos + dir * (BladeLen + StabReach);
            int globs = IsFinisher ? 12 : 7;
            for (int i = 0; i < globs; i++) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(tip + Main.rand.NextVector2Circular(8f, 8f)
                    , dir.RotatedByRandom(0.34f) * Main.rand.NextFloat(4f, IsFinisher ? 15f : 11f)
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.4f, 0.7f))
                    .Configure(Main.rand.Next(10, 18));
            }
            int sparks = IsFinisher ? 8 : 5;
            for (int i = 0; i < sparks; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(tip
                    , dir.RotatedByRandom(0.5f) * Main.rand.NextFloat(3f, 9f)
                    , AbyssrendFX.Cyan, Main.rand.NextFloat(0.8f, 1.3f))
                    .Configure(Main.rand.Next(8, 14));
            }
        }

        private void HandleParticles() {
            if (VaultUtils.isServer || !slashVisualActive) {
                return;
            }
            Vector2 along = currentRotation.ToRotationVector2();
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(mainTip + Main.rand.NextVector2Circular(6f, 6f)
                    , along.RotatedByRandom(0.4f) * Main.rand.NextFloat(1.2f, 3.4f)
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.35f, 0.6f))
                    .Configure(Main.rand.Next(10, 16));
            }
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(mainTip
                    , along.RotatedByRandom(0.8f) * Main.rand.NextFloat(1.5f, 4f)
                    , AbyssrendFX.Cyan, Main.rand.NextFloat(0.7f, 1.2f))
                    .Configure(Main.rand.Next(8, 14));
            }
        }

        private void HandleLight() {
            float mul = slashVisualActive ? 1.1f : 0.55f;
            Lighting.AddLight(mainTip, 0.12f * mul, 0.45f * mul, 0.55f * mul);
        }

        public override bool? CanDamage() => sweepDamageActive ? true : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!sweepDamageActive) {
                return false;
            }
            //贴脸补判，近身不落空
            if (targetHitbox.Distance(handPos) <= (IsThrust ? 42f : 50f)) {
                return true;
            }
            float collisionPoint = 0f;
            if (IsThrust) {
                Vector2 stabTip = mainTip + baseAngle.ToRotationVector2() * 14f;
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , handPos, stabTip, 38f, ref collisionPoint);
            }
            float reach = BladeLen + holdout + 14f;
            if (CWRUtils.ArcSweepCulled(targetHitbox, handPos, reach, 50f)) {
                return false;
            }
            int steps = GetAngularSteps(sweepCollisionEnd - sweepCollisionStart, reach, 28f, 64);
            for (int i = 0; i <= steps; i++) {
                float rotation = MathHelper.Lerp(sweepCollisionStart, sweepCollisionEnd, i / (float)steps);
                Vector2 tip = handPos + rotation.ToRotationVector2() * reach;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , handPos, tip, 50f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void CutTiles() {
            if (!sweepDamageActive) {
                return;
            }
            Vector2 tip = mainTip + (mainTip - handPos).SafeNormalize(Vector2.Zero) * 14f;
            Utils.PlotTileLine(handPos, tip, IsThrust ? 36f : 48f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = lockedDirection;
            modifiers.SourceDamage *= ComboStage switch {
                0 => 1.05f,
                1 => 0.92f,
                2 => 0.96f,
                _ => 1.38f
            };
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.45f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }
            target.AddBuff(BuffID.Wet, 180);

            if (!hitstopApplied) {
                hitstopApplied = true;
                //顿帧按拍重分级：戳比斩硬，重戳最硬
                hitstopFrames = IsThrust ? (IsFinisher ? 5f : 3f) : 2f;
            }

            if (CWRClientConfig.Instance.ScreenVibration) {
                float punch = IsFinisher ? 6.5f : (IsThrust ? 4.2f : 3.6f);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, currentRotation.ToRotationVector2(), punch, 6f, IsFinisher ? 8 : 6, 480f, FullName));
            }

            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(target.Center
                    , Main.rand.NextVector2Circular(4.5f, 4.5f)
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.4f, 0.7f))
                    .Configure(Main.rand.Next(12, 18));
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(target.Center
                    , Main.rand.NextVector2Circular(5f, 5f)
                    , AbyssrendFX.Cyan, Main.rand.NextFloat(0.8f, 1.3f))
                    .Configure(Main.rand.Next(8, 14));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //帧内挥速残影：快挥藏行程，弧由拖影撑住
            if (!slashVisualActive || IsThrust) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type]?.Value;
            if (tex == null) {
                return false;
            }
            float angleDelta = MathF.Abs(currentRotation - lastRotation);
            float strength = MathHelper.Clamp((angleDelta - 0.04f) / 0.75f, 0f, 1f);
            if (strength <= 0f) {
                return false;
            }
            Vector2 drawPos = handPos - Main.screenPosition;
            float scale = HeldScale * Projectile.scale;
            int smearCount = Math.Min(5, Math.Max(1, (int)MathF.Ceiling(angleDelta / 0.22f)));
            for (int i = 1; i <= smearCount; i++) {
                float amount = i / (float)(smearCount + 1);
                float rot = MathHelper.Lerp(currentRotation, lastRotation, amount);
                AbyssrendFX.ComputeBladeDrawXform(tex, rot, lockedDirection, EdgeFlip
                    , out Vector2 origin, out float sRot, out SpriteEffects flip);
                Color smear = AbyssrendFX.Cyan * (0.42f * strength * (1f - amount));
                smear.A = 0;
                Main.EntitySpriteDraw(tex, drawPos, null, smear, sRot, origin, scale, flip, 0);
            }
            return false;
        }

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            Texture2D tex = TextureAssets.Projectile[Type]?.Value;
            if (tex == null) {
                return;
            }
            AbyssrendFX.ComputeBladeDrawXform(tex, currentRotation, lockedDirection, EdgeFlip
                , out Vector2 origin, out float rot, out SpriteEffects flip);
            Vector2 drawPos = handPos - Main.screenPosition;
            Color light = Lighting.GetColor((int)(handPos.X / 16f), (int)(handPos.Y / 16f));
            float scale = HeldScale * Projectile.scale;

            if (slashVisualActive && IsThrust) {
                //戳刺纵向残影：读平滑刺速，爆发帧拖满、顿帧定格而不是瞬灭
                float smearStrength = MathHelper.Clamp(stabSpeedVis / 30f, 0f, 1f);
                if (smearStrength > 0.05f) {
                    Color trail = AbyssrendFX.Cyan * (0.34f * smearStrength);
                    trail.A = 0;
                    float spacing = MathHelper.Clamp(stabSpeedVis * 0.45f, 8f, 48f);
                    for (int i = 1; i <= 4; i++) {
                        Vector2 ghostPos = drawPos - baseAngle.ToRotationVector2() * (i * spacing);
                        Main.EntitySpriteDraw(tex, ghostPos, null, trail * (1f - i / 5f), rot, origin, scale, flip, 0);
                    }
                }
            }

            Main.EntitySpriteDraw(tex, drawPos, null, light, rot, origin, scale, flip, 0);
            Color glow = AbyssrendFX.Cyan;
            glow.A = 0;
            float glowMul = slashVisualActive ? 0.35f : 0.18f;
            Main.EntitySpriteDraw(tex, drawPos, null, glow * glowMul, rot, origin, scale, flip, 0);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailFade <= 0.02f) {
                return;
            }
            if (IsThrust) {
                if (elapsed < WindupTime) {
                    return;
                }
                Vector2 dir = baseAngle.ToRotationVector2();
                Vector2[] path = new Vector2[3];
                path[0] = handPos + dir * MathF.Max(0f, holdout * 0.2f);
                path[1] = Vector2.Lerp(handPos, mainTip, 0.45f);
                path[2] = mainTip;
                //光带宽度随平滑刺速鼓起：爆发帧最粗，停帧期间缓慢消气
                float width = (IsFinisher ? 26f : 20f) * (0.75f + MathHelper.Clamp(stabSpeedVis / 36f, 0f, 1f) * 0.6f);
                AbyssrendFX.DrawPathStrip(path, 3, _ => width * trailFade, trailFade);
                return;
            }
            if (trailCount < 3) {
                return;
            }
            float outer = BladeLen + holdout + 18f;
            float inner = BladeLen * 0.42f;
            AbyssrendFX.DrawArcStrip(handPos, trailRot, trailCount, inner, outer, trailFade);
        }

        private static float EaseOutCubic(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return 1f - MathF.Pow(1f - value, 3f);
        }

        private static float SmoothStep01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }
}
