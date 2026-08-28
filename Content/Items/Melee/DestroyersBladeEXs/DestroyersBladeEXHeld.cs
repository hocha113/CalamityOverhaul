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
    /// 毁灭者之刃EX 沉重三拍挥砍,节奏走收-爆-停(对齐金源灭却刃的挥舞文法)。
    /// 拍0 正手重劈,拍1 椭圆立体环斩(倾斜3D圆投影,近大远小),拍2 终结巨新月。
    /// 每拍四相:提刀蓄势(快拉慢定)→死寂驻谷(蓄满憋劲,只留微颤)→
    /// 前载爆发(爆发帧一口气掠过大半打击区,余下几帧动量制动)→
    /// 过冲硬停(动量带着刀滑过终点线,前冲一小段后死停)。
    /// 命中顿帧从收势尾巴等量扣回,总帧守恒。ai[0]=拍号 ai[1]=挥向
    /// </summary>
    internal class DestroyersBladeEXHeld : BaseHeldProj, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "DestroyersBladeEX";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<DestroyersBladeEX>()).DisplayName;

        private ref float ComboIndex => ref Projectile.ai[0];
        private ref float SwingDirAi => ref Projectile.ai[1];

        private bool IsEllipse => (int)ComboIndex == 1;
        private bool IsFinisher => ComboIndex >= 2f;
        private bool Empowered => Owner.GetModPlayer<DestroyerEXPlayer>().Empowered;

        //阶段时长(逻辑帧,攻速缩放)与挥砍几何,Initialize 按拍号写入
        private float windupTime = 6f;
        private float holdTime = 2f;
        private float slashTime = 5f;
        private float recoverTime = 7f;
        private float swingArc = 3.4f;      //圆弧拍的挥砍弧度
        private float pullbackAngle = 0.72f;//蓄力回拉角,沉重感的主要来源
        private float slashEasePow = 2.7f;  //爆发缓动指数:越大越前载
        private float overdrift = 0.16f;    //收势前冲角,动量滑过终点线
        private float leanAmp = 0.05f;      //身体编舞幅度

        private float TotalTime => windupTime + holdTime + slashTime + recoverTime;

        //椭圆拍(拍1):倾斜3D圆投影,主轴沿瞄准方向拉长
        private const float LoopSpan = 5.9f;   //环斩总角,留缺口避免读成量角器圆环
        private const float LoopPull = 0.5f;   //起手沿环路反向的预拉角
        private const float EllipseSquash = 0.52f;
        private const float MajorStretch = 1.12f;
        private float tiltSign = 1f;
        private float loopPhiNow;

        //刀尖距持握点长度
        private float BladeReach => 150f * (IsFinisher ? 1.08f : IsEllipse ? 1.05f : 1f);
        private float FullReach => BladeReach * Projectile.scale;
        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private Vector2 EllipseCenter => Hand + baseAngle.ToRotationVector2() * (FullReach * 0.22f);
        private float ViewZ => MathF.Max(900f, FullReach * 2.6f);

        //伤害窗起点(爆发相位),刀一动就开始咬
        private const float DamageStartT = 0.02f;
        //命中顿帧预算(帧),从收势尾巴等量扣回
        private const float HitStopBudget = 3f;

        private float elapsed;
        private float speedMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;
        private float baseAngle;
        private float startAngle;
        private float endAngle;
        private float currentRotation;  //手→刀尖方向角,姿态与绘制共用
        private Vector2 bladeTip;
        private float reachMul = 1f;    //刀身收放比例,提刀读感(仅影响本体绘制)
        private float bladeDim = 1f;    //椭圆拍远半纵深压暗
        private float sweepCollisionStart;  //本帧伤害扫掠界(圆弧拍=角度,椭圆拍=φ)
        private float sweepCollisionEnd;
        private bool sweepDamageActive;
        private bool slashKicked;
        private bool shotsFired;
        private bool stopBeatDone;
        private float trailFade;
        //出鞘成形度 0~1,刀光刃口先现、黑体后涌
        private float slashBirth;
        private float hitStopFrames;
        private float hitStopSpent;
        private float bodyLean;
        private float prevTrailValue;

        //刀光按外缘弧长补点(圆弧拍存角度,椭圆拍存φ)
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
            Vector2 hand = Hand;
            float collisionPoint = 0f;

            if (IsEllipse) {
                //椭圆拍按本帧扫过的环路分步采样,快扫不漏判
                float delta = sweepCollisionEnd - sweepCollisionStart;
                int phiSteps = Math.Clamp((int)(MathF.Abs(delta) / 0.18f) + 1, 1, 64);
                for (int i = 0; i <= phiSteps; i++) {
                    float phi = MathHelper.Lerp(sweepCollisionStart, sweepCollisionEnd, i / (float)phiSteps);
                    Vector2 point = EllipsePoint(phi, out _, out _);
                    point += (point - hand).SafeNormalize(Vector2.Zero) * 12f;
                    if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                        , hand, point, 54f, ref collisionPoint)) {
                        return true;
                    }
                }
                return false;
            }

            float reach = FullReach;
            if (CWRUtils.ArcSweepCulled(targetHitbox, hand, reach, 54f)) {
                return false;
            }
            int steps = GetAngularSteps(sweepCollisionEnd - sweepCollisionStart, reach, 24f, 64);
            for (int i = 0; i <= steps; i++) {
                float rotation = MathHelper.Lerp(sweepCollisionStart, sweepCollisionEnd, i / (float)steps);
                Vector2 tip = hand + rotation.ToRotationVector2() * reach;
                //宽刃:线判定加厚,贴脸与擦刃都要咬住
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , hand, tip, 54f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void CutTiles() {
            if (!sweepDamageActive) {
                return;
            }
            Utils.PlotTileLine(Hand, bladeTip, 46f, DelegateMethods.CutTiles);
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

            baseAngle = Projectile.velocity.ToRotation();

            //出手打断潜行,各端同拍
            Owner.GetModPlayer<DestroyerEXPlayer>().NoteAttack();

            //蓄力不配音效,安静的回拉与驻谷衬托爆发主声(对齐纠缠之怨)
            switch ((int)ComboIndex) {
                case 0:
                    //正手重劈
                    windupTime = 6f;
                    holdTime = 2f;
                    slashTime = 5f;
                    recoverTime = 7f;
                    swingArc = 3.4f;
                    pullbackAngle = 0.72f;
                    slashEasePow = 2.7f;
                    overdrift = 0.16f;
                    leanAmp = 0.05f;
                    Projectile.scale = 1.06f;
                    break;
                case 1:
                    //椭圆立体环斩,中量拍
                    windupTime = 8f;
                    holdTime = 2f;
                    slashTime = 8f;
                    recoverTime = 8f;
                    slashEasePow = 3.0f;
                    overdrift = 0.14f;
                    leanAmp = 0.10f;
                    tiltSign = swingSign;
                    Projectile.scale = 1.10f;
                    Projectile.damage = (int)(Projectile.damage * 1.15f);
                    break;
                default:
                    //终结巨新月
                    windupTime = 9f;
                    holdTime = 3f;
                    slashTime = 7f;
                    recoverTime = 9f;
                    swingArc = 5.5f;
                    pullbackAngle = 1.05f;
                    slashEasePow = 2.5f;
                    overdrift = 0.22f;
                    leanAmp = 0.12f;
                    Projectile.scale = 1.22f;
                    Projectile.damage = (int)(Projectile.damage * 1.4f);
                    break;
            }

            startAngle = baseAngle - swingSign * swingArc * 0.5f;
            endAngle = baseAngle + swingSign * swingArc * 0.5f;

            SetSweepPose(LiftValue);
            prevTrailValue = SweepNow;
            sweepCollisionStart = sweepCollisionEnd = SweepNow;
        }

        //起手位:刀在身前略入弧内,提刀拉向蓄势位
        private float LiftValue => IsEllipse ? swingSign * 0.35f : startAngle + swingSign * 0.35f;
        //蓄势位:圆弧拍回拉到枪膛角,椭圆拍沿环路反向预拉
        private float ChamberValue => IsEllipse ? -swingSign * LoopPull : startAngle - swingSign * pullbackAngle;
        //终点位
        private float EndValue => IsEllipse ? LoopPhi(1f) : endAngle;
        //当前扫掠参数(圆弧拍=刀角,椭圆拍=φ)
        private float SweepNow => IsEllipse ? loopPhiNow : currentRotation;

        /// <summary>椭圆拍的环路参数角,swingSign 决定行进方向</summary>
        private float LoopPhi(float t) => swingSign * (t * (LoopSpan + LoopPull) - LoopPull);

        /// <summary>爆发缓动:前载爆发+动量制动尾</summary>
        private float EasedSlash(float t) => 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), slashEasePow);

        /// <summary>由扫掠参数求扫掠插值位(圆弧拍=角度,椭圆拍=φ)</summary>
        private float SweepValue(float eased)
            => IsEllipse ? LoopPhi(eased) : MathHelper.Lerp(ChamberValue, endAngle, eased);

        /// <summary>倾斜3D圆上 φ 处的投影点,k 为透视系数(近大远小),zNorm∈[-1,1]</summary>
        private Vector2 EllipsePoint(float phi, out float k, out float zNorm) {
            float R = FullReach;
            float lx = MathF.Cos(phi) * R * MajorStretch;
            float ly = MathF.Sin(phi) * R * EllipseSquash;
            float z = MathF.Sin(phi) * MathF.Sqrt(1f - EllipseSquash * EllipseSquash) * R * tiltSign;
            zNorm = z / R;
            k = MathHelper.Clamp(ViewZ / (ViewZ - z), 0.84f, 1.18f);
            Vector2 dir = baseAngle.ToRotationVector2();
            Vector2 perp = new(-dir.Y, dir.X);
            return EllipseCenter + (dir * lx + perp * ly) * k;
        }

        /// <summary>写入本帧扫掠位,统一解算刀尖、刀角与纵深明暗</summary>
        private void SetSweepPose(float value) {
            if (IsEllipse) {
                loopPhiNow = value;
                bladeTip = EllipsePoint(value, out float k, out _);
                currentRotation = (bladeTip - Hand).ToRotation();
                //远半只轻压,保住刃口发光的可读性
                bladeDim = MathHelper.Lerp(0.74f, 1f, MathHelper.Clamp((k - 0.84f) / 0.34f, 0f, 1f));
                return;
            }
            currentRotation = value;
            bladeTip = Hand + value.ToRotationVector2() * FullReach;
            bladeDim = 1f;
        }

        public override void AI() {
            sweepDamageActive = false;
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
            UpdateMotion(frameEnd);
            ConsumeSlashInterval(frameEnd);

            //硬停拍:前冲收尽的一瞬在刀尖泄能,小环加切向甩出的惯性火花
            float stopBeatTime = windupTime + holdTime + slashTime + recoverTime * 0.5f;
            if (!stopBeatDone && frameEnd >= stopBeatTime) {
                stopBeatDone = true;
                DoStopBeat();
            }

            //刀光跟着实际扫掠推进(爆发与前冲共用),刀头永远贴着带头
            float sweepNow = SweepNow;
            if (trailFade > 0.01f && (sweepNow - prevTrailValue) * swingSign > 0.0001f) {
                PushTrailSamples(prevTrailValue, sweepNow);
            }
            prevTrailValue = sweepNow;

            UpdatePlayerPose();
            ApplyBodyLean();
            Lighting.AddLight(Vector2.Lerp(Hand, bladeTip, 0.7f), new Vector3(0.9f, 0.12f, 0.08f));
            elapsed = frameEnd;
        }

        /// <summary>由时间解算本帧姿态:四相收-爆-停</summary>
        private void UpdateMotion(float t) {
            float slashStart = windupTime + holdTime;
            float slashEnd = slashStart + slashTime;

            if (t <= windupTime) {
                //提刀蓄势:快拉慢定,刀身收短读作提刀,身体渐次后仰
                float p = t / windupTime;
                float eased = EaseOutCubic(p);
                SetSweepPose(MathHelper.Lerp(LiftValue, ChamberValue, eased));
                reachMul = MathHelper.Lerp(0.62f, 0.95f, eased);
                trailFade = 0f;
                slashBirth = 0f;
                bodyLean = -leanAmp * 0.62f * eased;
            }
            else if (t <= slashStart) {
                //死寂驻谷:蓄满憋劲只留微颤,这一拍静止是爆发的画框
                float tremble = 0.015f * MathF.Sin(t * 1.9f);
                SetSweepPose(ChamberValue + swingSign * tremble);
                reachMul = MathHelper.Lerp(0.95f, 1f, (t - windupTime) / holdTime);
                trailFade = 0f;
                slashBirth = 0f;
                bodyLean = -leanAmp * 0.62f;
            }
            else if (t <= slashEnd) {
                //前载爆发:爆发帧一口气掠过大半打击区,余下几帧动量制动,身体前扑
                float p = (t - slashStart) / slashTime;
                float eased = EasedSlash(p);
                SetSweepPose(SweepValue(eased));
                reachMul = 1f;
                trailFade = 1f;
                slashBirth = SmoothStep01(p / 0.22f);
                bodyLean = MathHelper.Lerp(-leanAmp * 0.62f, leanAmp, eased);
            }
            else {
                //过冲硬停:动量带着刀前冲一小段后死停,刀光原地散场
                float q = (t - slashEnd) / recoverTime;
                float settle = EaseOutQuad(MathF.Min(1f, q * 1.8f));
                SetSweepPose(EndValue + swingSign * overdrift * settle);
                reachMul = MathHelper.Lerp(1f, 0.88f, SmoothStep01(q));
                trailFade = 1f - SmoothStep01(q / 0.75f);
                slashBirth = 1f;
                bodyLean = MathHelper.Lerp(leanAmp, 0f, EaseOutQuad(MathF.Min(1f, q * 1.4f)));
            }
        }

        /// <summary>消费本帧与爆发阶段的交集:伤害窗、爆发事件、弹幕齐射与刃缘火花</summary>
        private void ConsumeSlashInterval(float frameEnd) {
            float slashStart = windupTime + holdTime;
            float slashEnd = slashStart + slashTime;
            float fromTime = MathF.Max(elapsed, slashStart);
            float toTime = MathF.Min(frameEnd, slashEnd);
            if (toTime <= fromTime) {
                return;
            }

            float fromT = (fromTime - slashStart) / slashTime;
            float toT = (toTime - slashStart) / slashTime;

            //高攻速跨阶段不漏刀:伤害扫掠界取本帧实际掠过的区间
            float damageFrom = MathF.Max(fromT, DamageStartT);
            if (toT > damageFrom) {
                sweepDamageActive = true;
                sweepCollisionStart = SweepValue(EasedSlash(damageFrom));
                sweepCollisionEnd = SweepValue(EasedSlash(toT));
            }

            //爆发帧事件:主挥砍音效+切向震屏,与最大角速度同拍
            if (!slashKicked) {
                slashKicked = true;
                KickSlash();
            }

            if (!shotsFired && EasedSlash(toT) >= 0.5f) {
                shotsFired = true;
                FireShots(currentRotation);
            }

            //刃缘熔渣火花
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 along = Vector2.Lerp(Hand, bladeTip, Main.rand.NextFloat(0.5f, 1f));
                Vector2 tangent = currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                PRTLoader.NewParticle<PRT_Spark>(along, tangent * Main.rand.NextFloat(3f, 7f)
                    , Main.rand.NextBool(4) ? Color.White : new Color(255, 40, 30)
                    , Main.rand.NextFloat(0.6f, 1.1f)).Configure(false, 9);
            }
        }

        /// <summary>爆发帧的声音与震屏:轻-中-重逐拍加码</summary>
        private void KickSlash() {
            if (VaultUtils.isServer) {
                return;
            }
            //音效对齐纠缠之怨:Item1 主挥砍按拍走音高,重拍叠 Item71 低啸
            float swingPitch = (int)ComboIndex switch { 0 => -0.18f, 1 => -0.3f, _ => -0.45f };
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = swingPitch }, Owner.Center);
            if (IsEllipse) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.7f }, Owner.Center);
            }
            else if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.35f, Volume = 0.9f }, Owner.Center);
            }

            Main.LocalPlayer?.CWR()?.GetScreenShake(IsFinisher ? 5f : IsEllipse ? 4f : 2.6f);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Vector2 punchDir = (baseAngle + swingSign * MathHelper.PiOver2).ToRotationVector2();
                float strength = IsFinisher ? 7f : IsEllipse ? 5f : 3f;
                int frames = IsFinisher ? 10 : IsEllipse ? 8 : 6;
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Owner.Center, punchDir, strength, 7f, frames, 1000f, FullName));
            }
        }

        /// <summary>硬停拍演出:刀尖泄能小环 + 切向甩出的惯性火花</summary>
        private void DoStopBeat() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 tip = bladeTip;
            Vector2 tangent = currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
            PRTLoader.NewParticle<PRT_StarPulseRing>(tip, Vector2.Zero, new Color(255, 70, 45), 0f)
                ?.Configure(0.03f, IsFinisher ? 0.5f : 0.3f, IsFinisher ? 12 : 9);
            for (int i = 0; i < (IsFinisher ? 6 : 4); i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(tip + Main.rand.NextVector2Circular(8f, 8f)
                    , tangent * Main.rand.NextFloat(4f, 9f) + Main.rand.NextVector2Circular(1.5f, 1.5f)
                    , Main.rand.NextBool(3) ? Color.White : new Color(255, 60, 35)
                    , Main.rand.NextFloat(0.7f, 1.2f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        private static float SmoothStep01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), 3f);
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        private static int GetAngularSteps(float delta, float radius, float targetSpacing, int maxSteps) {
            float arcLength = MathF.Abs(delta) * MathF.Max(radius, 1f);
            return Math.Clamp((int)MathF.Ceiling(arcLength / targetSpacing), 1, maxSteps);
        }

        private void PushTrailSamples(float fromValue, float toValue) {
            float delta = toValue - fromValue;
            if (delta * swingSign <= 0.0001f) {
                return;
            }

            float sampleRadius = IsEllipse ? FullReach * MajorStretch : (BladeReach + 22f) * Projectile.scale;
            bool appendStart = trailCount == 0;
            int steps = GetAngularSteps(delta, sampleRadius, TrailSampleSpacing, TrailMax - 1);
            int retained = Math.Min(trailCount, TrailMax - steps);
            if (retained > 0) {
                Array.Copy(trailRot, 0, trailRot, steps, retained);
            }
            for (int i = 0; i < steps; i++) {
                float amount = 1f - i / (float)steps;
                trailRot[i] = MathHelper.Lerp(fromValue, toValue, amount);
            }
            trailCount = steps + retained;
            if (appendStart && trailCount < TrailMax) {
                trailRot[trailCount++] = fromValue;
            }
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

            Vector2 hand = Hand;
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
            //蓄势与收势手臂放松,爆发全伸
            bool relaxed = elapsed < windupTime || elapsed > windupTime + holdTime + slashTime;
            Player.CompositeArmStretchAmount stretch = relaxed
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, currentRotation - MathHelper.PiOver2);
            Projectile.Center = Vector2.Lerp(Hand, bladeTip, 0.55f);
            Projectile.timeLeft = 90;
        }

        /// <summary>全身参与:蓄力后仰、爆发前扑,支点钉在脚底,轻拍小幅重拍大幅</summary>
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

            if (!IsEllipse && !IsFinisher) {
                return;
            }
            if (CWRClientConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(target.Center
                    , currentRotation.ToRotationVector2(), IsFinisher ? 5f : 4f, 5f, IsFinisher ? 9 : 7, 800f, FullName);
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

        /// <summary>解算某扫掠位上的刀身绘制参数(椭圆拍刀长随投影距离呼吸,轴向缩短=最强纵深线索)</summary>
        private void GetBladePose(float sweepValue, out Vector2 drawPos, out float rotation, out float scale) {
            Vector2 hand = Hand;
            if (IsEllipse) {
                Vector2 tip = EllipsePoint(sweepValue, out _, out _);
                Vector2 toTip = tip - hand;
                float len = MathF.Max(toTip.Length(), 1f);
                rotation = toTip.ToRotation();
                scale = Projectile.scale * MathHelper.Clamp(len / FullReach, 0.55f, 2f) * reachMul;
                drawPos = hand + toTip * (0.55f * reachMul);
                return;
            }
            rotation = sweepValue;
            scale = Projectile.scale;
            drawPos = hand + sweepValue.ToRotationVector2() * (FullReach * 0.55f * reachMul);
        }

        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);

            //刀身残影:压在刀光带之上、真刀之下,快速运动的滞留视像
            DrawBladeGhosts(tex, origin, effect, rotOffset);

            //刀身本体 + 辉光层,椭圆拍远半按纵深压暗
            Vector2 hand = Hand;
            GetBladePose(SweepNow, out Vector2 drawPos, out float rotation, out float scale);
            Color lightColor = Lighting.GetColor((int)(hand.X / 16f), (int)(hand.Y / 16f)) * bladeDim;
            lightColor.A = 255;
            Main.EntitySpriteDraw(tex, drawPos - Main.screenPosition, null, lightColor, rotation + rotOffset, origin
                , scale, effect, 0);
            Texture2D glow = DestroyersBladeEX.Glow.Value;
            Color glowColor = Color.White * bladeDim;
            glowColor.A = 255;
            Main.EntitySpriteDraw(glow, drawPos - Main.screenPosition, null, glowColor, rotation + rotOffset, glow.Size() / 2f
                , scale, effect, 0);
        }

        /// <summary>
        /// 沿刀光路径按角距取样重画刀身:近刃两影泄红热(加色),旧影真 alpha 黑剪影沉进刀光,
        /// 随刀光淡出一起消散;椭圆拍残影骑在投影环上,位置/角度/刀长逐影重算
        /// </summary>
        private void DrawBladeGhosts(Texture2D tex, Vector2 origin, SpriteEffects effect, float rotOffset) {
            float strength = trailFade * slashBirth;
            if (trailCount < 2 || strength <= 0.03f) {
                return;
            }

            const float GhostHeadGap = 0.20f;   //贴着真刀的一段不画,免得糊住本体
            const float GhostSpacing = 0.30f;   //相邻残影角距
            const int GhostMax = 7;
            Span<float> ghostVal = stackalloc float[GhostMax];
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
                ghostVal[count] = trailRot[i];
                ghostAge[count] = MathHelper.Clamp((arc - GhostHeadGap) / (maxArc - GhostHeadGap), 0f, 1f);
                count++;
                nextEmit += GhostSpacing;
            }

            //旧影先画,新影压上
            for (int k = count - 1; k >= 0; k--) {
                float fall = 1f - ghostAge[k];
                GetBladePose(ghostVal[k], out Vector2 pos, out float rotation, out float scale);
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
                Main.EntitySpriteDraw(tex, pos - Main.screenPosition, null, ghostColor, rotation + rotOffset, origin
                    , scale, effect, 0);
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
            Vector2 center = Hand;
            Vector2 ellipseCenter = EllipseCenter;
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
                Vector2 outerPos;
                Vector2 innerPos;
                Color vcol = Color.White;
                if (IsEllipse) {
                    //椭圆带:刃线骑在倾斜圆投影上,外扩光晕区,内缘向椭圆心收,远半按纵深压暗
                    Vector2 pt = EllipsePoint(trailRot[i], out float k, out _);
                    Vector2 spoke = pt - ellipseCenter;
                    outerPos = ellipseCenter + spoke * 1.13f;
                    innerPos = ellipseCenter + spoke * 0.30f;
                    float dimT = MathHelper.Clamp((k - 0.84f) / 0.34f, 0f, 1f);
                    byte lum = (byte)(150 + 105 * dimT);
                    vcol = new Color(lum, lum, lum, (byte)(200 + 55 * dimT));
                }
                else {
                    Vector2 dir = trailRot[i].ToRotationVector2();
                    outerPos = center + dir * outer;
                    innerPos = center + dir * inner;
                }
                bars[i * 2] = new VertexPositionColorTexture(outerPos.ToVector3(), vcol, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture(innerPos.ToVector3(), vcol, new Vector2(factor, 1f));
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
            effect.Parameters["segCount"]?.SetValue(IsEllipse ? 13f : MathF.Max(5f, swingArc * 2.0f));
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
