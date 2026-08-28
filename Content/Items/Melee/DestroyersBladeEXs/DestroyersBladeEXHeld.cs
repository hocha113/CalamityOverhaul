using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DestroyersBladeEXs
{
    /// <summary>
    /// 毁灭者之刃EX 沉重三拍挥砍。拍0/1 正反手重劈,拍2 终结巨新月。
    /// 节奏走蓄-加速-巅峰-制动:深蓄力回拉后,重刃从静止拖出加速,
    /// 巅峰匀速掠过打击区,动量带着刀滑过终点线制动,过冲回坐死停。
    /// 加减速全程可见,力量感来自质量驱动的挥砍过程。
    /// 命中顿帧从收势尾巴等量扣回,总帧守恒(轻拍20/终结28)。ai[0]=拍号 ai[1]=挥向
    /// </summary>
    internal class DestroyersBladeEXHeld : BaseHeldProj, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBladeEX";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<DestroyersBladeEX>()).DisplayName;

        private ref float ComboIndex => ref Projectile.ai[0];
        private ref float SwingDirAi => ref Projectile.ai[1];

        private bool IsFinisher => ComboIndex >= 2f;
        private bool Empowered => Owner.GetModPlayer<DestroyerEXPlayer>().Empowered;

        //阶段时长(逻辑帧,攻速缩放)。轻拍 7+8+5=20,终结 10+11+7=28,总帧与旧版守恒
        //挥砍窗拉长:加速与制动过程全程可见,蓄力和收势各让出几帧
        private float WindupTime => IsFinisher ? 10f : 7f;
        private float SlashTime => IsFinisher ? 11f : 8f;
        private float RecoverTime => IsFinisher ? 7f : 5f;
        private float TotalTime => WindupTime + SlashTime + RecoverTime;
        //挥砍弧度:轻拍压椭圆弯月,终结才配巨新月
        private float SwingArc => IsFinisher ? 5.5f : 3.4f;
        //刀尖距持握点长度
        private float BladeReach => 150f * (IsFinisher ? 1.08f : 1f);
        //蓄力回拉角:深回拉,沉重感的主要来源
        private float PullbackAngle => IsFinisher ? 1.05f : 0.72f;

        //挥砍三段整形:加速段终点、巅峰段终点、过冲顶点(前向行程终点)
        private float SwingAccelEnd => IsFinisher ? 0.32f : 0.30f;
        private float SwingPeakEnd => 0.58f;
        private float SwingApexTime => IsFinisher ? 0.85f : 0.84f;
        //伤害窗起点(挥砍相位),加速段刀一动就开始咬
        private const float DamageStartT = 0.12f;

        //命中顿帧预算(帧),从收势尾巴等量扣回
        private const float HitStopBudget = 3f;

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;
        private float startAngle;
        private float endAngle;
        private float currentRotation;
        private float sweepCollisionStart;
        private float sweepCollisionEnd;
        private bool sweepDamageActive;
        private bool slashSoundPlayed;
        private bool peakShakeDone;
        private bool shotsFired;
        private bool stopBeatDone;
        private float trailFade;
        //出鞘成形度 0~1,刀光刃口先现、黑体后涌,消灭亮相的突兀感
        private float slashBirth;
        private float hitStopFrames;
        private float hitStopSpent;
        private float bodyLean;

        //刀光按外缘弧长补点
        private const int TrailMax = 96;
        private const float TrailSampleSpacing = 18f;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => sweepDamageActive;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float reach = BladeReach * Projectile.scale;
            if (CWRUtils.ArcSweepCulled(targetHitbox, hand, reach, 54f)) {
                return false;
            }
            int steps = GetAngularSteps(sweepCollisionEnd - sweepCollisionStart, reach, 24f, 64);
            for (int i = 0; i <= steps; i++) {
                float rotation = MathHelper.Lerp(sweepCollisionStart, sweepCollisionEnd, i / (float)steps);
                Vector2 tip = hand + rotation.ToRotationVector2() * reach;
                float collisionPoint = 0f;
                //宽刃:线判定加厚,贴脸与擦刃都要咬住
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , hand, tip, 54f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void Initialize() {
            swingSign = Math.Sign(SwingDirAi);
            if (swingSign == 0) {
                swingSign = 1;
            }

            lockedDirection = Math.Sign(ToMouse.X);
            if (lockedDirection == 0) {
                lockedDirection = Owner.direction;
            }
            Owner.direction = lockedDirection;

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            float baseAngle = Projectile.velocity.ToRotation();
            startAngle = baseAngle - swingSign * SwingArc * 0.5f;
            endAngle = baseAngle + swingSign * SwingArc * 0.5f;
            currentRotation = startAngle;
            sweepCollisionStart = sweepCollisionEnd = startAngle;

            //出手打断潜行,各端同拍
            Owner.GetModPlayer<DestroyerEXPlayer>().NoteAttack();

            //蓄力不配音效,安静的回拉衬托挥砍主声(对齐纠缠之怨)
            if (IsFinisher) {
                Projectile.damage = (int)(Projectile.damage * 1.4f);
                Projectile.scale = 1.22f;
            }
            else {
                Projectile.scale = 1.06f;
            }
        }

        public override void AI() {
            sweepDamageActive = false;
            sweepCollisionStart = sweepCollisionEnd = currentRotation;
            if (Item.type != ModContent.ItemType<DestroyersBladeEX>()) {
                Projectile.Kill();
                return;
            }

            //命中顿帧:几何全冻,时长记账后从收势扣回
            if (hitStopFrames > 0f) {
                hitStopFrames--;
                UpdatePlayerPose();
                ApplyBodyLean();
                return;
            }

            float effectiveTotal = TotalTime - hitStopSpent;
            if (elapsed >= effectiveTotal) {
                Projectile.Kill();
                return;
            }

            float frameEnd = MathF.Min(elapsed + speedMul, effectiveTotal);
            float slashEnd = WindupTime + SlashTime;
            float slashFromTime = MathF.Max(elapsed, WindupTime);
            float slashToTime = MathF.Min(frameEnd, slashEnd);

            if (slashToTime > slashFromTime) {
                //消费本帧与挥砍阶段的交集,高攻速跨阶段不漏刀
                float fromT = (slashFromTime - WindupTime) / SlashTime;
                float toT = (slashToTime - WindupTime) / SlashTime;
                float progress = GetSwingProgress(toT);
                float slashRotation = GetSwingRotation(progress);

                float damageFrom = MathF.Max(fromT, DamageStartT);
                float damageTo = MathF.Min(toT, SwingApexTime);
                if (damageTo > damageFrom) {
                    sweepDamageActive = true;
                    sweepCollisionStart = GetSwingRotation(GetSwingProgress(damageFrom));
                    sweepCollisionEnd = GetSwingRotation(GetSwingProgress(damageTo));
                }

                if (!slashSoundPlayed && toT >= SwingAccelEnd * 0.5f) {
                    slashSoundPlayed = true;
                    if (!VaultUtils.isServer) {
                        //音效对齐纠缠之怨:Item1 主挥砍按拍走音高,终结叠 Item71 低啸
                        float swingPitch = (int)ComboIndex switch { 0 => -0.18f, 1 => -0.12f, _ => -0.45f };
                        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = swingPitch }, Owner.Center);
                        if (IsFinisher) {
                            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.35f, Volume = 0.9f }, Owner.Center);
                        }
                    }
                }

                //震屏留到巅峰段入口,与最大角速度同拍
                if (!peakShakeDone && toT >= SwingAccelEnd) {
                    peakShakeDone = true;
                    if (!VaultUtils.isServer) {
                        Main.LocalPlayer?.CWR()?.GetScreenShake(IsFinisher ? 5f : 2.6f);
                    }
                }

                PushTrailInterval(fromT, toT);

                if (!shotsFired && progress >= 0.58f) {
                    shotsFired = true;
                    FireShots(slashRotation);
                }

                //硬停拍:回坐完成的一瞬在刀尖泄能,小环加切向甩出的惯性火花
                float stopBeatT = SwingApexTime + 0.45f * (1f - SwingApexTime);
                if (!stopBeatDone && toT >= stopBeatT) {
                    stopBeatDone = true;
                    if (!VaultUtils.isServer) {
                        Vector2 tip = Owner.GetPlayerStabilityCenter()
                            + endAngle.ToRotationVector2() * BladeReach * Projectile.scale;
                        Vector2 tangent = endAngle.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                        PRTLoader.NewParticle<PRT_StarPulseRing>(tip, Vector2.Zero, new Color(255, 70, 45), 0f)
                            ?.Configure(0.03f, IsFinisher ? 0.5f : 0.3f, IsFinisher ? 12 : 9);
                        for (int i = 0; i < (IsFinisher ? 6 : 4); i++) {
                            PRTLoader.NewParticle<PRT_SparkAlpha>(tip + Main.rand.NextVector2Circular(8f, 8f)
                                , tangent * Main.rand.NextFloat(4f, 9f) + Main.rand.NextVector2Circular(1.5f, 1.5f)
                                , Main.rand.NextBool(3) ? Color.White : new Color(255, 60, 35)
                                , Main.rand.NextFloat(0.7f, 1.2f))?.Configure(false, Main.rand.Next(8, 14));
                        }
                    }
                }

                //刃缘熔渣火花
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 along = Owner.GetPlayerStabilityCenter()
                        + slashRotation.ToRotationVector2() * Main.rand.NextFloat(BladeReach * 0.5f, BladeReach);
                    Vector2 tangent = slashRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                    PRTLoader.NewParticle<PRT_Spark>(along, tangent * Main.rand.NextFloat(3f, 7f)
                        , Main.rand.NextBool(4) ? Color.White : new Color(255, 40, 30)
                        , Main.rand.NextFloat(0.6f, 1.1f)).Configure(false, 9);
                }
            }

            if (frameEnd <= WindupTime) {
                //深蓄力:重刃加速再减速拖向身后,末端沉进枪膛位近静止(爆发前的憋劲),全拍身体后仰
                float t = frameEnd / WindupTime;
                currentRotation = MathHelper.Lerp(startAngle, ChamberAngle, SmoothStep01(t));
                trailFade = 0f;
                slashBirth = 0f;
                bodyLean = -(IsFinisher ? 0.06f : 0.028f) * SmoothStep01(t);
            }
            else if (frameEnd <= slashEnd) {
                //加速-巅峰-制动:重刃从静止拖出、掠过打击区、动量滑进过冲,身体随之前扑
                float t = (frameEnd - WindupTime) / SlashTime;
                currentRotation = GetSwingRotation(GetSwingProgress(t));
                trailFade = 1f;
                //加速段走完从无到有,刃口先现、黑体后涌
                slashBirth = SmoothStep01(t / SwingAccelEnd);
                bodyLean = MathHelper.Lerp(IsFinisher ? -0.06f : -0.028f, IsFinisher ? 0.10f : 0.05f
                    , SmoothStep01((t - SwingAccelEnd * 0.5f) / 0.5f));
            }
            else {
                //收势回守,前段死停驻谷,顿帧扣掉的时长在这里兑现
                float t = (frameEnd - slashEnd) / RecoverTime;
                float hold = IsFinisher ? 0.42f : 0.34f;
                float returnT = SmoothStep01((t - hold) / (1f - hold));
                float baseAngle = (startAngle + endAngle) * 0.5f;
                float guardAngle = baseAngle + swingSign * (IsFinisher ? 1.1f : 0.9f);
                currentRotation = MathHelper.Lerp(endAngle, guardAngle, returnT);
                trailFade = 1f - SmoothStep01(t);
                slashBirth = 1f;
                bodyLean = MathHelper.Lerp(IsFinisher ? 0.10f : 0.05f, 0f, SmoothStep01(t * 1.5f));
                TrimTrailToRotation(currentRotation);
            }

            UpdatePlayerPose();
            ApplyBodyLean();
            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.7f
                , new Vector3(0.9f, 0.12f, 0.08f));
            elapsed = frameEnd;
        }

        private float ChamberAngle => startAngle - swingSign * PullbackAngle;

        private float GetSwingProgress(float t) {
            float accelEnd = SwingAccelEnd;
            float peakEnd = SwingPeakEnd;
            float apex = SwingApexTime;
            float path = SwingArc + PullbackAngle;
            float overshoot = (IsFinisher ? 0.30f : 0.22f) / path;

            //由速度剖面反解行程占比:加速段二次缓入、巅峰段匀速、制动段速度线性衰减到顶点归零,段间C1连续
            float vPeak = (1f + overshoot) / (peakEnd - accelEnd * 0.5f + (apex - peakEnd) * 0.5f);
            float accelDist = vPeak * accelEnd * 0.5f;

            if (t < accelEnd) {
                //惯性起步:重刃从静止拖出,加速过程可见
                float s = t / accelEnd;
                return accelDist * s * s;
            }
            if (t < peakEnd) {
                //巅峰:最大角速度匀速掠过打击区
                return accelDist + vPeak * (t - accelEnd);
            }
            if (t < apex) {
                //制动:动量带着刀滑过终点线,减速进过冲顶点(顶点瞬时静止)
                float s = (t - peakEnd) / (apex - peakEnd);
                return accelDist + vPeak * (peakEnd - accelEnd) + vPeak * (apex - peakEnd) * s * (1f - 0.5f * s);
            }
            //过冲回坐:前45%窗口收回,余下死停(挫)
            float settleT = (t - apex) / (1f - apex);
            float snap = SmoothStep01(MathF.Min(settleT / 0.45f, 1f));
            return MathHelper.Lerp(1f + overshoot, 1f, snap);
        }

        private float GetSwingRotation(float progress)
            => MathHelper.Lerp(ChamberAngle, endAngle, progress);

        private static float SmoothStep01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static int GetAngularSteps(float delta, float radius, float targetSpacing, int maxSteps) {
            float arcLength = MathF.Abs(delta) * MathF.Max(radius, 1f);
            return Math.Clamp((int)MathF.Ceiling(arcLength / targetSpacing), 1, maxSteps);
        }

        private void PushTrailInterval(float fromT, float toT) {
            float forwardTo = MathF.Min(toT, SwingApexTime);
            if (forwardTo > fromT) {
                PushTrailSamples(GetSwingRotation(GetSwingProgress(fromT))
                    , GetSwingRotation(GetSwingProgress(forwardTo)));
            }
            if (toT > SwingApexTime) {
                TrimTrailToRotation(GetSwingRotation(GetSwingProgress(toT)));
            }
        }

        private void PushTrailSamples(float fromRotation, float toRotation) {
            //终结斩跨过 PI,保留未包裹角度
            float delta = toRotation - fromRotation;
            if (delta * swingSign <= 0.0001f) {
                TrimTrailToRotation(toRotation);
                return;
            }

            float outerRadius = (BladeReach + 22f) * Projectile.scale;
            bool appendStart = trailCount == 0;
            int steps = GetAngularSteps(delta, outerRadius, TrailSampleSpacing, TrailMax - 1);
            int retained = Math.Min(trailCount, TrailMax - steps);
            if (retained > 0) {
                Array.Copy(trailRot, 0, trailRot, steps, retained);
            }
            for (int i = 0; i < steps; i++) {
                float amount = 1f - i / (float)steps;
                trailRot[i] = MathHelper.Lerp(fromRotation, toRotation, amount);
            }
            trailCount = steps + retained;
            if (appendStart && trailCount < TrailMax) {
                trailRot[trailCount++] = fromRotation;
            }
        }

        private void TrimTrailToRotation(float rotation) {
            if (trailCount == 0) {
                return;
            }

            const float angleEpsilon = 0.0001f;
            int firstRetained = 0;
            while (firstRetained < trailCount
                && (trailRot[firstRetained] - rotation) * swingSign > angleEpsilon) {
                firstRetained++;
            }

            int retained = trailCount - firstRetained;
            bool headAlreadySampled = retained > 0
                && MathF.Abs(trailRot[firstRetained] - rotation) <= angleEpsilon;
            int targetOffset = headAlreadySampled ? 0 : 1;
            int copied = Math.Min(retained, TrailMax - targetOffset);
            if (copied > 0 && (firstRetained != targetOffset || firstRetained > 0)) {
                Array.Copy(trailRot, firstRetained, trailRot, targetOffset, copied);
            }

            trailRot[0] = rotation;
            trailCount = copied + targetOffset;
        }

        /// <summary>爆发帧的弹幕齐射:红白光束 + 影子弹幕,终结加量,歼灭态另出头颅</summary>
        private void FireShots(float slashRotation) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            bool emp = Empowered;
            int combo = (int)ComboIndex;
            int beams = combo switch {
                0 => emp ? 2 : 1,
                1 => emp ? 1 : 0,
                _ => emp ? 2 : 1,
            };
            int shadows = combo switch {
                0 => emp ? 1 : 0,
                1 => emp ? 3 : 2,
                _ => emp ? 5 : 3,
            };

            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 spawnPos = hand + slashRotation.ToRotationVector2() * BladeReach * 0.5f;
            float empFlag = emp ? 1f : 0f;

            for (int i = 0; i < beams; i++) {
                float offset = beams > 1 ? MathHelper.Lerp(-0.06f, 0.06f, i / (float)(beams - 1)) : 0f;
                Vector2 velocity = UnitToMouseV.RotatedBy(offset) * Item.shootSpeed;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPos, velocity
                    , ModContent.ProjectileType<DestroyersBeamEX>(), (int)(Projectile.damage * 0.5f)
                    , Projectile.knockBack * 0.4f, Projectile.owner, 0f, empFlag);
            }
            for (int i = 0; i < shadows; i++) {
                float offset = shadows > 1 ? MathHelper.Lerp(-0.4f, 0.4f, i / (float)(shadows - 1)) : 0f;
                Vector2 velocity = UnitToMouseV.RotatedBy(offset) * Item.shootSpeed * 0.62f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPos, velocity
                    , ModContent.ProjectileType<DestroyerShadowBolt>(), (int)(Projectile.damage * 0.35f)
                    , Projectile.knockBack * 0.3f, Projectile.owner, 0f, empFlag);
            }
            if (emp && IsFinisher) {
                //歼灭协议终结斩:额外吐出毁灭者头颅
                Vector2 velocity = UnitToMouseV * 7f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), hand - Vector2.UnitY * 20f, velocity
                    , ModContent.ProjectileType<DestroyerHeadMissile>(), (int)(Projectile.damage * 1.6f)
                    , Projectile.knockBack, Projectile.owner);
            }
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentRotation - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + currentRotation.ToRotationVector2() * BladeReach * 0.55f;
            Projectile.timeLeft = 90;
        }

        /// <summary>全身参与:蓄力后仰、爆发前扑,支点钉在脚底,轻拍小幅终结拍大幅</summary>
        private void ApplyBodyLean() {
            Owner.fullRotation = bodyLean * lockedDirection;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.height);
        }

        public override void OnKill(int timeLeft) {
            Owner.fullRotation = 0f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //顿帧记账:预算内冻结几何,时长由收势扣回
            float want = IsFinisher ? 3.5f : 2.5f;
            float grant = MathF.Min(want, HitStopBudget - hitStopSpent);
            if (grant > 0f) {
                hitStopFrames += grant;
                hitStopSpent += grant;
            }

            if (!VaultUtils.isServer) {
                //命中不配音效(对齐纠缠之怨),受击反馈交给顿帧、粒子与原版受击声
                for (int i = 0; i < (IsFinisher ? 9 : 5); i++) {
                    PRTLoader.NewParticle<PRT_SparkAlpha>(target.Center, Main.rand.NextVector2Circular(7f, 7f)
                        , Main.rand.NextBool(4) ? Color.White : new Color(255, 40, 30)
                        , Main.rand.NextFloat(1f, 2f)).Configure(false, Main.rand.Next(10, 18));
                }
                //暗渣崩出:小口黑烟带挥砍冲量甩出,几帧内泄劲悬停
                Vector2 sweepDir = (currentRotation + swingSign * MathHelper.PiOver2).ToRotationVector2();
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_KikasaHoundSmoke>(target.Center + Main.rand.NextVector2Circular(10f, 10f)
                        , sweepDir * Main.rand.NextFloat(2f, 4.5f) + Main.rand.NextVector2Circular(1f, 1f)
                        , new Color(14, 3, 5) * 0.85f, Main.rand.NextFloat(0.18f, 0.3f))?.Configure(Main.rand.Next(16, 26), 0.008f);
                }
                if (IsFinisher) {
                    Color warm = new Color(255, 70, 40);
                    PRTLoader.NewParticle<PRT_MechExplosion>(target.Center, Main.rand.NextVector2Circular(1.5f, 1.5f)
                        , warm, 0.9f).Configure(Main.rand.Next(18, 28), warm);
                }
            }

            if (IsFinisher && CWRClientConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(target.Center
                    , currentRotation.ToRotationVector2(), 5f, 5f, 9, 800f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        //残影不画在这层:实体层会被刀光黑体盖住,全部改画在 Overlay 层
        public override bool PreDraw(ref Color lightColor) => false;

        private void GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset) {
            bool edgeFlip = swingSign * lockedDirection < 0;
            bool flipVertically = (lockedDirection < 0) != edgeFlip;
            effect = flipVertically ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flipVertically ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float dist = BladeReach * 0.5f * Projectile.scale;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);

            //刀身残影:压在刀光带之上、真刀之下,快速运动的滞留视像
            DrawBladeGhosts(tex, origin, hand, dist, effect, rotOffset);

            //刀身本体 + 辉光层
            Color lightColor = Lighting.GetColor((int)(hand.X / 16f), (int)(hand.Y / 16f));
            Vector2 drawPos = hand + currentRotation.ToRotationVector2() * dist - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, currentRotation + rotOffset, origin
                , Projectile.scale, effect, 0);
            Texture2D glow = DestroyersBladeEX.Glow.Value;
            Main.EntitySpriteDraw(glow, drawPos, null, Color.White, currentRotation + rotOffset, glow.Size() / 2f
                , Projectile.scale, effect, 0);
        }

        /// <summary>
        /// 沿刀光弧线按角距取样重画刀身:近刃两影泄红热(加色),旧影真 alpha 黑剪影沉进刀光,
        /// 随刀光淡出一起消散,收势期跟着裁剪回拢
        /// </summary>
        private void DrawBladeGhosts(Texture2D tex, Vector2 origin, Vector2 hand, float dist
            , SpriteEffects effect, float rotOffset) {
            float strength = trailFade * slashBirth;
            if (trailCount < 2 || strength <= 0.03f) {
                return;
            }

            const float GhostHeadGap = 0.20f;   //贴着真刀的一段不画,免得糊住本体
            const float GhostSpacing = 0.30f;   //相邻残影角距
            const int GhostMax = 7;
            Span<float> ghostRot = stackalloc float[GhostMax];
            Span<float> ghostAge = stackalloc float[GhostMax];
            int count = 0;
            float arc = 0f;
            float nextEmit = GhostHeadGap;
            float maxArc = GhostHeadGap + GhostSpacing * GhostMax;
            for (int i = 1; i < trailCount && count < GhostMax; i++) {
                arc += MathF.Abs(trailRot[i - 1] - trailRot[i]);
                if (arc < nextEmit) {
                    continue;
                }
                ghostRot[count] = trailRot[i];
                ghostAge[count] = MathHelper.Clamp((arc - GhostHeadGap) / (maxArc - GhostHeadGap), 0f, 1f);
                count++;
                nextEmit += GhostSpacing;
            }

            //旧影先画,新影压上
            for (int k = count - 1; k >= 0; k--) {
                float fall = 1f - ghostAge[k];
                Vector2 pos = hand + ghostRot[k].ToRotationVector2() * dist - Main.screenPosition;
                Color ghostColor;
                if (ghostAge[k] < 0.3f) {
                    //新影:炽红余温
                    ghostColor = new Color(255, 58, 34) * (0.5f * fall * strength);
                    ghostColor.A = 0;
                }
                else {
                    //旧影:黑剪影,真 alpha 才能在红光里压出暗形
                    ghostColor = new Color(20, 5, 9) * (0.6f * fall * strength);
                }
                Main.EntitySpriteDraw(tex, pos, null, ghostColor, ghostRot[k] + rotOffset, origin
                    , Projectile.scale, effect, 0);
            }
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.DestroyerEXSlash?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = Owner.GetPlayerStabilityCenter();
            float outer = (BladeReach + 22f) * Projectile.scale;
            float inner = BladeReach * 0.14f;
            float totalArc = 0f;
            for (int i = 1; i < trailCount; i++) {
                totalArc += MathF.Abs(trailRot[i - 1] - trailRot[i]);
            }
            float traveledArc = 0f;
            for (int i = 0; i < trailCount; i++) {
                if (i > 0) {
                    traveledArc += MathF.Abs(trailRot[i - 1] - trailRot[i]);
                }
                float factor = totalArc > 0.0001f
                    ? 1f - traveledArc / totalArc
                    : 1f - i / (float)Math.Max(trailCount - 1, 1);
                Vector2 dir = trailRot[i].ToRotationVector2();
                bars[i * 2] = new VertexPositionColorTexture((center + dir * outer).ToVector3()
                    , Color.White, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((center + dir * inner).ToVector3()
                    , Color.White, new Vector2(factor, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            //噪声走 s1 寄存器,先绑贴图再 Apply
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(trailFade);
            effect.Parameters["empowerMix"]?.SetValue(Empowered ? 1f : 0f);
            effect.Parameters["segCount"]?.SetValue(MathF.Max(5f, SwingArc * 2.0f));
            effect.Parameters["birthProgress"]?.SetValue(slashBirth);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
