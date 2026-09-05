using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Modifys.RTerraBlades
{
    /// <summary>
    /// 泰拉之刃手持，五拍连段走收-爆-停：<br/>
    /// 拍0 夜落（厚重下劈）/ 拍1 破晓（厚重上撩）/ 拍2、3 轮转（快速倾斜椭圆环斩，倾面交替）/ 拍4 泰拉大回旋（绕身一圈半的螺旋扩张回旋斩）<br/>
    /// 每拍四相：提刀蓄势 → 死寂驻谷 → 前载爆发 → 过冲硬停；命中顿帧从收势尾巴等量扣回<br/>
    /// 刀光=刀尖真实轨迹扫过的体，头永远钉在刀上；椭圆与回旋走倾斜 3D 圆透视投影，刀身长随纵深呼吸<br/>
    /// ai[0]=拍号 ai[1]=挥向符号（正=下劈向，手持侧再乘朝向）
    /// </summary>
    internal class RTerraBladeHeld : BaseHeldProj, IPrimitiveDrawable, IOverlayDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.TerraBlade");

        private enum Kind { Chop, Loop, Spin }

        /// <summary>单拍定义，时长为设计帧（攻速缩放后消费）</summary>
        private readonly struct BeatDef(Kind kind, float raise, float hold, float slash, float recover
            , float easePow, float overdrift, float leanAmp, float damageMul, float hitStop
            , float belly, float tailWidth, float soul, float swingPitch, float[] boltClocks, float[] boltSouls)
        {
            public readonly Kind Kind = kind;
            public readonly float Raise = raise;
            public readonly float Hold = hold;
            public readonly float Slash = slash;
            public readonly float Recover = recover;
            public readonly float EasePow = easePow;      //爆发缓动指数，越大越前载
            public readonly float Overdrift = overdrift;  //收势前冲角
            public readonly float LeanAmp = leanAmp;      //身体编舞幅度
            public readonly float DamageMul = damageMul;
            public readonly float HitStop = hitStop;      //本拍顿帧预算
            public readonly float Belly = belly;          //刀光最厚处弧向位置
            public readonly float TailWidth = tailWidth;  //刀光尾端带宽占比
            public readonly float Soul = soul;            //0 夜 1 光 0.5 双魂
            public readonly float SwingPitch = swingPitch;
            public readonly float[] BoltClocks = boltClocks;  //泰拉之光离手的刀路进度
            public readonly float[] BoltSouls = boltSouls;
            public float Total => Raise + Hold + Slash + Recover;
        }

        private static readonly BeatDef[] Table = [
            //拍0 夜落：厚重下劈
            new(Kind.Chop, 9f, 3f, 5f, 9f, 2.7f, 0.18f, 0.09f, 1.00f, 3f, 0.72f, 0.28f, 0f, -0.28f, [0.55f], [0f]),
            //拍1 破晓：厚重上撩
            new(Kind.Chop, 8f, 3f, 5f, 9f, 2.7f, 0.18f, 0.09f, 1.00f, 3f, 0.72f, 0.28f, 1f, -0.20f, [0.55f], [1f]),
            //拍2 轮转·光：快速环斩
            new(Kind.Loop, 4f, 1f, 6f, 5f, 2.4f, 0.12f, 0.05f, 0.75f, 1.5f, 0.60f, 0.36f, 1f, 0.12f, [0.30f, 0.75f], [1f, 0f]),
            //拍3 轮转·夜：反向环斩
            new(Kind.Loop, 3f, 1f, 6f, 5f, 2.4f, 0.12f, 0.05f, 0.75f, 1.5f, 0.60f, 0.36f, 0f, 0.18f, [0.30f, 0.75f], [0f, 1f]),
            //拍4 泰拉大回旋
            new(Kind.Spin, 10f, 3f, 11f, 12f, 2.2f, 0.25f, 0.12f, 1.50f, 3.5f, 0.50f, 0.45f, 0.5f, -0.45f, [0.22f, 0.42f, 0.62f, 0.82f], [1f, 0f, 1f, 0f]),
        ];

        //==================== 几何常量 ====================
        /// <summary>手→刀尖基准距离</summary>
        private const float BaseReach = 150f;
        /// <summary>刀身贴图中心停在手→刀尖的几成处</summary>
        private const float BladePark = 0.46f;
        /// <summary>劈砍弧总角与蓄势回拉角</summary>
        private const float ChopArc = 3.0f;
        private const float ChopPull = 0.5f;
        /// <summary>环斩总角（留缺口，不读成量角器）与预拉角</summary>
        private const float LoopSpan = 5.9f;
        private const float LoopPull = 0.5f;
        private const float LoopSquash = 0.52f;
        private const float LoopStretch = 1.10f;
        private const float LoopReachMul = 1.05f;
        /// <summary>回旋总角（一圈又三成）、预拉角、螺旋半径起止</summary>
        private const float SpinSpan = MathHelper.TwoPi * 1.3f;
        private const float SpinPull = 0.6f;
        private const float SpinSquash = 0.56f;
        private const float SpinRadiusFrom = 0.92f;
        private const float SpinRadiusTo = 1.32f;
        /// <summary>回旋刀光只保留刀后这段弧，旧段裁掉免得自叠成环</summary>
        private const float SpinVisibleArc = 5.4f;
        /// <summary>刀光内缘占刀长比例（带=刀的外 68%）</summary>
        private const float RibbonInnerFrac = 0.32f;
        /// <summary>刀光外扩光晕垫比例，与 TerraBlade.fx 的 HaloFrac 锁定</summary>
        private const float HaloFrac = 0.14f;
        /// <summary>伤害窗起点（爆发相位比例）</summary>
        private const float DamageStartT = 0.02f;
        private const int TrailMax = 128;
        private const float TrailSampleSpacing = 16f;

        //==================== 状态 ====================
        private BeatDef Beat => Table[Math.Clamp((int)Projectile.ai[0], 0, Table.Length - 1)];
        private int BeatIndex => Math.Clamp((int)Projectile.ai[0], 0, Table.Length - 1);
        private Kind BeatKind => Beat.Kind;
        private bool IsChop => BeatKind == Kind.Chop;
        private bool IsLoop => BeatKind == Kind.Loop;
        private bool IsSpin => BeatKind == Kind.Spin;

        private float elapsed;
        private float speedMul = 1f;
        private float sizeMul = 1f;
        private int lockedDirection = 1;
        private int swingSign = 1;       //屏幕挥向
        private float tiltSign = 1f;     //椭圆倾面
        private float baseAngle;
        private float chopStart;
        private float chopEnd;
        private float sweepNow;          //当前扫掠参数（劈=刀角，环/旋=φ）
        private float currentRotation;   //手→刀尖方向角
        private Vector2 bladeTip;
        private float reachMul = 1f;     //提刀收放（只影响本体绘制）
        private float bladeDim = 1f;     //纵深压暗
        private float sweepCollisionStart;
        private float sweepCollisionEnd;
        private bool sweepDamageActive;
        private bool slashKicked;
        private bool stopBeatDone;
        private bool holdFlashDone;
        private int boltsReleased;
        private float trailFade;
        private float slashBirth;
        private float burstFlash;        //爆发帧结构闪，逐帧衰减
        private float hitStopFrames;
        private float hitStopSpent;
        private float bodyLean;
        private bool bodyLeanApplied;
        private float prevTrailValue;
        private float seed;
        private readonly HashSet<int> hitNPCs = [];

        private readonly float[] trailVal = new float[TrailMax];
        private int trailCount;

        private float FullReach => BaseReach * sizeMul * (IsLoop ? LoopReachMul : 1f);
        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private Vector2 LoopCenter => Hand + baseAngle.ToRotationVector2() * (FullReach * 0.22f);
        private float ViewZ => MathF.Max(900f, FullReach * 2.8f);
        private float SlashStart => Beat.Raise + Beat.Hold;
        private float SlashEnd => SlashStart + Beat.Slash;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            //一拍一个弹幕，-1=每拍对同一目标只命中一次
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void Initialize() {
            //朝向从出手向（随生成包同步）推，远端首帧还没收到鼠标包也能对齐
            baseAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(baseAngle);
            lockedDirection = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);
            if (lockedDirection == 0) {
                lockedDirection = 1;
            }
            Owner.direction = lockedDirection;

            //ai[1] 只给交替符号，实际扫向乘上朝向
            int sign = Projectile.ai[1] >= 0f ? 1 : -1;
            swingSign = sign * lockedDirection;
            tiltSign = IsLoop ? sign : 1f;

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }
            sizeMul = Owner.GetAdjustedItemScale(Item);
            if (sizeMul <= 0f) {
                sizeMul = 1f;
            }

            chopStart = baseAngle - swingSign * ChopArc * 0.5f;
            chopEnd = baseAngle + swingSign * ChopArc * 0.5f;
            seed = Projectile.identity % 97 * 0.113f;

            SetSweepPose(LiftValue);
            prevTrailValue = sweepNow;
            sweepCollisionStart = sweepCollisionEnd = sweepNow;
        }

        //==================== 几何 ====================

        //起手位：劈=弧内略入；环/旋=沿环路正向一点
        private float LiftValue => IsChop ? chopStart + swingSign * 0.35f : swingSign * 0.35f;
        //蓄势位：劈=回拉到枪膛角；环/旋=沿环路反向预拉
        private float ChamberValue => IsChop ? chopStart - swingSign * ChopPull : -swingSign * (IsSpin ? SpinPull : LoopPull);
        //终点位
        private float EndValue => IsChop ? chopEnd : (IsSpin ? SpinPhi(1f) : LoopPhi(1f));

        private float LoopPhi(float t) => swingSign * (t * (LoopSpan + LoopPull) - LoopPull);
        private float SpinPhi(float t) => swingSign * (t * (SpinSpan + SpinPull) - SpinPull);

        /// <summary>爆发缓动，前载爆发+动量制动尾</summary>
        private float EasedSlash(float t) => 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), Beat.EasePow);

        /// <summary>由扫掠插值位求扫掠参数（劈=刀角，环/旋=φ）</summary>
        private float SweepValue(float eased) => BeatKind switch {
            Kind.Chop => MathHelper.Lerp(ChamberValue, chopEnd, eased),
            Kind.Loop => LoopPhi(eased),
            _ => SpinPhi(eased),
        };

        /// <summary>回旋半径随环路进度螺旋外扩，读作扩张的冲击环而非量角器</summary>
        private float SpinRadius(float phi) {
            float t = MathHelper.Clamp((swingSign * phi + SpinPull) / (SpinSpan + SpinPull), 0f, 1f);
            return FullReach * MathHelper.Lerp(SpinRadiusFrom, SpinRadiusTo, SmoothStep01(t));
        }

        /// <summary>倾斜 3D 圆上 φ 处的投影点，k 为透视系数（近大远小），zNorm∈[-1,1]</summary>
        private Vector2 RingPoint(float phi, out float k, out float zNorm) {
            bool spin = IsSpin;
            float R = spin ? SpinRadius(phi) : FullReach;
            float squash = spin ? SpinSquash : LoopSquash;
            float stretch = spin ? 1f : LoopStretch;
            Vector2 center = spin ? Hand : LoopCenter;
            float lx = MathF.Cos(phi) * R * stretch;
            float ly = MathF.Sin(phi) * R * squash;
            float z = MathF.Sin(phi) * MathF.Sqrt(1f - squash * squash) * R * tiltSign;
            zNorm = z / R;
            float viewZ = MathF.Max(ViewZ, R * 2.8f);
            k = MathHelper.Clamp(viewZ / (viewZ - z), 0.84f, 1.18f);
            Vector2 dir = baseAngle.ToRotationVector2();
            Vector2 perp = new(-dir.Y, dir.X);
            return center + (dir * lx + perp * ly) * k;
        }

        private Vector2 TipAt(float value)
            => IsChop ? Hand + value.ToRotationVector2() * FullReach : RingPoint(value, out _, out _);

        /// <summary>写入本帧扫掠位，统一解算刀尖、刀角与纵深明暗</summary>
        private void SetSweepPose(float value) {
            sweepNow = value;
            if (IsChop) {
                currentRotation = value;
                bladeTip = Hand + value.ToRotationVector2() * FullReach;
                bladeDim = 1f;
                return;
            }
            bladeTip = RingPoint(value, out float k, out _);
            currentRotation = (bladeTip - Hand).ToRotation();
            //远半只轻压，保住刃口发光的可读性
            bladeDim = MathHelper.Lerp(0.74f, 1f, MathHelper.Clamp((k - 0.84f) / 0.34f, 0f, 1f));
        }

        //==================== 运动 ====================

        public override void AI() {
            sweepDamageActive = false;
            if (Item.type != ItemID.TerraBlade || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            //命中顿帧：几何全冻，时长记账后从收势扣回
            if (hitStopFrames > 0f) {
                hitStopFrames--;
                UpdatePlayerPose();
                ApplyBodyLean();
                return;
            }

            float effectiveTotal = Beat.Total - hitStopSpent;
            if (elapsed >= effectiveTotal) {
                Projectile.Kill();
                return;
            }

            float frameEnd = MathF.Min(elapsed + speedMul, effectiveTotal);
            UpdateMotion(frameEnd);
            HandleChamberEvent(frameEnd);
            ConsumeSlashInterval(frameEnd);
            if (frameEnd > SlashEnd) {
                //高攻速跨相位不漏发
                HandleVolley(1f);
            }

            //硬停拍：前冲收尽的一瞬在刀尖泄能
            float stopBeatTime = SlashEnd + Beat.Recover * 0.45f;
            if (!stopBeatDone && frameEnd >= stopBeatTime) {
                stopBeatDone = true;
                DoStopBeat();
            }

            //刀光跟着实际扫掠推进（爆发与前冲共用），刀头永远贴着带头
            if (trailFade > 0.01f && (sweepNow - prevTrailValue) * swingSign > 0.0001f) {
                PushTrailSamples(prevTrailValue, sweepNow);
            }
            prevTrailValue = sweepNow;

            if (burstFlash > 0f) {
                burstFlash = MathF.Max(0f, burstFlash - 0.22f * speedMul);
            }

            UpdatePlayerPose();
            ApplyBodyLean();
            HandleParticles(frameEnd);
            Lighting.AddLight(Vector2.Lerp(Hand, bladeTip, 0.7f)
                , RTerraBlade.TerraMain.ToVector3() * (0.7f + 0.7f * trailFade));
            elapsed = frameEnd;
        }

        /// <summary>由时间解算本帧姿态：四相收-爆-停</summary>
        private void UpdateMotion(float t) {
            BeatDef b = Beat;
            if (t <= b.Raise) {
                //提刀蓄势：快拉慢定，刀身收短读作提刀，身体渐次后仰
                float p = t / b.Raise;
                float eased = EaseOutCubic(p);
                SetSweepPose(MathHelper.Lerp(LiftValue, ChamberValue, eased));
                reachMul = MathHelper.Lerp(0.62f, 0.95f, eased);
                trailFade = 0f;
                slashBirth = 0f;
                bodyLean = -b.LeanAmp * 0.62f * eased;
            }
            else if (t <= SlashStart) {
                //死寂驻谷：蓄满憋劲只留微颤
                float tremble = 0.015f * MathF.Sin(t * 1.9f + seed);
                SetSweepPose(ChamberValue + swingSign * tremble);
                reachMul = MathHelper.Lerp(0.95f, 1f, (t - b.Raise) / b.Hold);
                trailFade = 0f;
                slashBirth = 0f;
                bodyLean = -b.LeanAmp * 0.62f;
            }
            else if (t <= SlashEnd) {
                //前载爆发：头几帧掠过大半打击区，余下动量制动
                float p = (t - SlashStart) / b.Slash;
                float eased = EasedSlash(p);
                SetSweepPose(SweepValue(eased));
                reachMul = 1f;
                trailFade = 1f;
                slashBirth = SmoothStep01(p / 0.22f);
                bodyLean = IsSpin ? SpinLean() : MathHelper.Lerp(-b.LeanAmp * 0.62f, b.LeanAmp, eased);
            }
            else {
                //过冲硬停：动量带着刀前冲一小段后死停，刀光原地烧尽
                float q = (t - SlashEnd) / b.Recover;
                float settle = EaseOutQuad(MathF.Min(1f, q * 1.8f));
                SetSweepPose(EndValue + swingSign * b.Overdrift * settle);
                reachMul = MathHelper.Lerp(1f, 0.88f, SmoothStep01(q));
                trailFade = 1f - SmoothStep01(q / 0.78f);
                slashBirth = 1f;
                float leanFrom = IsSpin ? SpinLean() : b.LeanAmp;
                bodyLean = MathHelper.Lerp(leanFrom, 0f, EaseOutQuad(MathF.Min(1f, q * 1.4f)));
            }
        }

        /// <summary>回旋拍身体随刀摆：刀在哪侧就往哪侧倾（屏幕系，预先除掉朝向）</summary>
        private float SpinLean()
            => Beat.LeanAmp * MathHelper.Clamp((bladeTip.X - Hand.X) / FullReach, -1f, 1f) * lockedDirection;

        /// <summary>蓄满瞬间：刀尖一记小脉冲环；回旋另垫聚魂低鸣</summary>
        private void HandleChamberEvent(float frameEnd) {
            if (holdFlashDone || frameEnd < Beat.Raise) {
                return;
            }
            holdFlashDone = true;
            if (VaultUtils.isServer || IsLoop) {
                return;
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(bladeTip, Vector2.Zero, RTerraBlade.SoulBright(Beat.Soul), 0f)
                ?.Configure(0.02f, IsSpin ? 0.34f : 0.22f, IsSpin ? 12 : 9);
            if (IsSpin) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.45f, Pitch = -0.35f }, Owner.Center);
            }
        }

        /// <summary>消费本帧与爆发阶段的交集：伤害窗、爆发事件、泰拉之光离手与刃缘火花</summary>
        private void ConsumeSlashInterval(float frameEnd) {
            float fromTime = MathF.Max(elapsed, SlashStart);
            float toTime = MathF.Min(frameEnd, SlashEnd);
            if (toTime <= fromTime) {
                return;
            }

            float fromT = (fromTime - SlashStart) / Beat.Slash;
            float toT = (toTime - SlashStart) / Beat.Slash;

            //高攻速跨阶段不漏刀：伤害扫掠界取本帧实际掠过的区间
            float damageFrom = MathF.Max(fromT, DamageStartT);
            if (toT > damageFrom) {
                sweepDamageActive = true;
                sweepCollisionStart = SweepValue(EasedSlash(damageFrom));
                sweepCollisionEnd = SweepValue(EasedSlash(toT));
            }

            if (!slashKicked) {
                slashKicked = true;
                burstFlash = 1f;
                KickSlash();
            }

            HandleVolley(EasedSlash(toT));

            //刃缘翠火花沿切线甩出，三分之一染魂色
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 along = Vector2.Lerp(Hand, bladeTip, Main.rand.NextFloat(0.55f, 1f));
                Vector2 tangent = currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
                Color c = Main.rand.NextBool(3) ? RTerraBlade.SoulColor(Beat.Soul) : RTerraBlade.TerraBright;
                PRTLoader.NewParticle<PRT_Spark>(along, tangent * Main.rand.NextFloat(3f, 8f), c
                    , Main.rand.NextFloat(0.6f, 1.1f))?.Configure(false, 9);
            }
        }

        /// <summary>刀光取样：沿外缘弧长补点（劈存角度，环/旋存 φ）；回旋只留刀后一段弧</summary>
        private void PushTrailSamples(float fromValue, float toValue) {
            float delta = toValue - fromValue;
            if (delta * swingSign <= 0.0001f) {
                return;
            }
            float sampleRadius = IsChop ? FullReach + 24f : FullReach * (IsSpin ? SpinRadiusTo : LoopStretch);
            bool appendStart = trailCount == 0;
            int steps = GetAngularSteps(delta, sampleRadius, TrailSampleSpacing, TrailMax - 1);
            int retained = Math.Min(trailCount, TrailMax - steps);
            if (retained > 0) {
                Array.Copy(trailVal, 0, trailVal, steps, retained);
            }
            for (int i = 0; i < steps; i++) {
                float amount = 1f - i / (float)steps;
                trailVal[i] = MathHelper.Lerp(fromValue, toValue, amount);
            }
            trailCount = steps + retained;
            if (appendStart && trailCount < TrailMax) {
                trailVal[trailCount++] = fromValue;
            }
            if (!IsSpin) {
                return;
            }
            float head = trailVal[0];
            for (int i = 0; i < trailCount; i++) {
                if (MathF.Abs(head - trailVal[i]) > SpinVisibleArc) {
                    trailCount = Math.Max(i, 2);
                    break;
                }
            }
        }

        private static int GetAngularSteps(float delta, float radius, float targetSpacing, int maxSteps) {
            float arcLength = MathF.Abs(delta) * MathF.Max(radius, 1f);
            return Math.Clamp((int)MathF.Ceiling(arcLength / targetSpacing), 1, maxSteps);
        }

        private static float SmoothStep01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), 3f);
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        //==================== 爆发与硬停 ====================

        /// <summary>爆发帧的声音与震屏：环斩轻、劈砍重、回旋最重并双魂并鸣</summary>
        private void KickSlash() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = Beat.SwingPitch, Volume = IsLoop ? 0.75f : 0.95f }, Owner.Center);
            switch (BeatKind) {
                case Kind.Chop:
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.35f, Volume = 0.8f }, Owner.Center);
                    break;
                case Kind.Loop:
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.15f, Volume = 0.45f }, Owner.Center);
                    break;
                default:
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.5f, Volume = 1f }, Owner.Center);
                    //双魂并鸣：圣光钟鸣垫一记，夜色低鸣再垫一记
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.1f, Volume = 0.4f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.55f, Volume = 0.35f }, Owner.Center);
                    break;
            }

            if (Projectile.owner == Main.myPlayer) {
                Main.LocalPlayer?.CWR()?.GetScreenShake(IsSpin ? 5.5f : IsLoop ? 2.2f : 3.5f);
            }
            if (CWRClientConfig.Instance.ScreenVibration) {
                Vector2 punchDir = (baseAngle + swingSign * MathHelper.PiOver2).ToRotationVector2();
                float strength = IsSpin ? 8f : IsLoop ? 2.6f : 4.5f;
                int frames = IsSpin ? 12 : IsLoop ? 5 : 7;
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Owner.Center, punchDir, strength, 7f, frames, 1000f, FullName));
            }
        }

        /// <summary>硬停拍演出：刀尖泄能脉冲环 + 切向甩出的惯性火花，回旋另配重落声</summary>
        private void DoStopBeat() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 tip = bladeTip;
            Vector2 tangent = currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
            PRTLoader.NewParticle<PRT_StarPulseRing>(tip, Vector2.Zero, RTerraBlade.SoulBright(Beat.Soul), 0f)
                ?.Configure(0.03f, IsSpin ? 0.55f : IsLoop ? 0.25f : 0.35f, IsSpin ? 13 : 9);
            int count = IsSpin ? 8 : IsLoop ? 3 : 5;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(tip + Main.rand.NextVector2Circular(8f, 8f)
                    , tangent * Main.rand.NextFloat(4f, 9f) + Main.rand.NextVector2Circular(1.5f, 1.5f)
                    , Main.rand.NextBool(3) ? RTerraBlade.TerraCore : RTerraBlade.TerraBright
                    , Main.rand.NextFloat(0.7f, 1.2f))?.Configure(false, Main.rand.Next(8, 14));
            }
            if (IsSpin) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.7f, Pitch = -0.2f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = -0.6f }, Owner.Center);
            }
        }

        //==================== 泰拉之光 ====================

        /// <summary>按刀路进度分批离手；同一帧可放多发，几何一律按排程点重算</summary>
        private void HandleVolley(float clock) {
            float[] clocks = Beat.BoltClocks;
            while (boltsReleased < clocks.Length && clock >= clocks[boltsReleased]) {
                FireBolt(boltsReleased);
                boltsReleased++;
            }
        }

        /// <summary>劈砍自刀身沿瞄准线直射；环斩/回旋自刀尖沿环心径向绽放，绽放期一过被追踪拽回目标</summary>
        private void FireBolt(int index) {
            float clock = Beat.BoltClocks[index];
            float soul = Beat.BoltSouls[index];
            Vector2 tip = TipAt(SweepValue(clock));
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.42f, Pitch = 0.1f + 0.08f * index }, tip);
            }
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Vector2 aim = baseAngle.ToRotationVector2();
            Vector2 origin;
            Vector2 vel;
            float bloom;
            float size;
            switch (BeatKind) {
                case Kind.Chop:
                    origin = Vector2.Lerp(Hand, tip, 0.86f);
                    vel = aim * 15f;
                    bloom = 0f;
                    size = 1f;
                    break;
                case Kind.Loop:
                    origin = tip;
                    vel = (tip - LoopCenter).SafeNormalize(aim) * 5f;
                    bloom = 10f;
                    size = 0.85f;
                    break;
                default:
                    origin = tip;
                    vel = (tip - Hand).SafeNormalize(aim) * 6f;
                    bloom = 12f;
                    size = 1f;
                    break;
            }

            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), origin, vel
                , ModContent.ProjectileType<TerraLightProj>(), Math.Max(1, (int)(Projectile.damage * RTerraBlade.BoltDamageMul))
                , Projectile.knockBack * 0.5f, Owner.whoAmI, soul, bloom, size);
        }

        //==================== 姿态 ====================

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (currentRotation.ToRotationVector2() * lockedDirection).ToRotation();
            //蓄势与收势手臂放松，爆发全伸
            bool relaxed = elapsed < Beat.Raise || elapsed > SlashEnd;
            Player.CompositeArmStretchAmount stretch = relaxed
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, currentRotation - MathHelper.PiOver2);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters
                , currentRotation - MathHelper.PiOver2 + swingSign * 0.28f);
            Projectile.Center = Vector2.Lerp(Hand, bladeTip, 0.55f);
            Projectile.rotation = currentRotation;
            Projectile.timeLeft = 90;
        }

        /// <summary>全身参与：蓄力后仰、爆发前扑、回旋随刀摆；支点钉脚底，坐骑/冲刺旋转让位</summary>
        private void ApplyBodyLean() {
            CWRPlayer modPlayer = Owner.CWR();
            if (Owner.mount.Active || (modPlayer != null && modPlayer.IsRotatingDuringDash)) {
                bodyLeanApplied = false;
                return;
            }
            Owner.fullRotation = bodyLean * lockedDirection * Owner.gravDir;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.gravDir >= 0f ? Owner.height : 0f);
            bodyLeanApplied = true;
        }

        /// <summary>任何退出路径都交还 fullRotation，卡住的倾斜比没有更糟</summary>
        public override void OnKill(int timeLeft) {
            if (bodyLeanApplied && Owner.active) {
                Owner.fullRotation = 0f;
                bodyLeanApplied = false;
            }
        }

        //==================== 粒子 ====================

        /// <summary>蓄势期能量屑被拽向刀身（快拍只零星几粒），收势期刀光带上翠屑上飘</summary>
        private void HandleParticles(float t) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 hand = Hand;
            Color soul = RTerraBlade.SoulColor(Beat.Soul);

            if (t <= SlashStart) {
                if (IsLoop) {
                    if (Main.rand.NextBool(3)) {
                        PRTLoader.NewParticle<PRT_Light>(Vector2.Lerp(hand, bladeTip, Main.rand.NextFloat(0.5f, 1f))
                            , -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f), RTerraBlade.TerraBright
                            , Main.rand.NextFloat(0.10f, 0.16f))?.Configure(10, 0.8f);
                    }
                    return;
                }
                float chargeT = MathHelper.Clamp(t / Beat.Raise, 0f, 1f);
                if (t > Beat.Raise || Main.rand.NextBool(2)) {
                    Vector2 anchor = Vector2.Lerp(hand, bladeTip, Main.rand.NextFloat(0.4f, 0.95f));
                    Vector2 offset = Main.rand.NextVector2CircularEdge(70f, 70f);
                    PRTLoader.NewParticle<PRT_Light>(anchor + offset, -offset * 0.11f
                        , Main.rand.NextBool(3) ? soul : RTerraBlade.TerraBright
                        , Main.rand.NextFloat(0.14f, 0.24f) * (0.5f + chargeT * 0.5f))?.Configure(12, 0.9f);
                }
                return;
            }

            if (t > SlashEnd && trailFade > 0.1f && trailCount > 2 && Main.rand.NextBool(3)) {
                int idx = Main.rand.Next(1, trailCount);
                Vector2 at = Vector2.Lerp(hand, TipAt(trailVal[idx]), Main.rand.NextFloat(0.6f, 1f));
                PRTLoader.NewParticle<PRT_Light>(at, -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.1f)
                    , Main.rand.NextBool(4) ? soul : RTerraBlade.TerraBright
                    , Main.rand.NextFloat(0.10f, 0.18f) * trailFade)?.Configure(14, 0.85f);
            }
        }

        //==================== 命中 ====================

        public override bool? CanDamage() => sweepDamageActive ? null : false;

        /// <summary>贪婪判定：本帧扫过的区间逐段采样，贴身段单独兜一次</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!sweepDamageActive) {
                return false;
            }
            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(10, 10);
            Vector2 hand = Hand;
            //画面重叠了却打不到最伤玩家信任
            if (greedyBox.Distance(hand) <= 50f) {
                return true;
            }

            const float width = 54f;
            float collisionPoint = 0f;
            if (IsChop) {
                float reach = FullReach + 12f;
                if (CWRUtils.ArcSweepCulled(greedyBox, hand, reach, width)) {
                    return false;
                }
                int steps = GetAngularSteps(sweepCollisionEnd - sweepCollisionStart, reach, 24f, 64);
                for (int i = 0; i <= steps; i++) {
                    float rotation = MathHelper.Lerp(sweepCollisionStart, sweepCollisionEnd, i / (float)steps);
                    Vector2 tip = hand + rotation.ToRotationVector2() * reach;
                    if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(), hand, tip, width, ref collisionPoint)) {
                        return true;
                    }
                }
                return false;
            }

            //环/旋按本帧扫过的环路分步采样，快扫不漏判
            float delta = sweepCollisionEnd - sweepCollisionStart;
            int phiSteps = Math.Clamp((int)(MathF.Abs(delta) / 0.18f) + 1, 1, 64);
            for (int i = 0; i <= phiSteps; i++) {
                float phi = MathHelper.Lerp(sweepCollisionStart, sweepCollisionEnd, i / (float)phiSteps);
                Vector2 point = RingPoint(phi, out _, out _);
                point += (point - hand).SafeNormalize(Vector2.Zero) * 12f;
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size(), hand, point, width, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>割草断藤跟着扫掠走，贴身段也算</summary>
        public override void CutTiles() {
            if (!sweepDamageActive) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Vector2 hand = Hand;
            const int samples = 3;
            for (int i = 0; i <= samples; i++) {
                float value = MathHelper.Lerp(sweepCollisionStart, sweepCollisionEnd, i / (float)samples);
                Vector2 tip = TipAt(value);
                tip += (tip - hand).SafeNormalize(Vector2.Zero) * 12f;
                Utils.PlotTileLine(hand, tip, 46f, DelegateMethods.CutTiles);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //击退跟出手朝向，不跟当帧刀角
            modifiers.HitDirectionOverride = lockedDirection;
            modifiers.SourceDamage *= Beat.DamageMul;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本次挥砍对同一目标只转发一次外部命中钩子
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }
            ApplyImpactFeedback(target.Center);
            if (!VaultUtils.isServer) {
                Vector2 sweepDir = (currentRotation + swingSign * MathHelper.PiOver2).ToRotationVector2();
                SpawnHitBurst(target.Center, sweepDir, IsSpin ? 1f : IsLoop ? 0.5f : 0.8f, Beat.Soul);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            ApplyImpactFeedback(target.Center);
            if (!VaultUtils.isServer) {
                Vector2 sweepDir = (currentRotation + swingSign * MathHelper.PiOver2).ToRotationVector2();
                SpawnHitBurst(target.Center, sweepDir, IsSpin ? 1f : 0.6f, Beat.Soul);
            }
        }

        /// <summary>命中迸溅：翠火花顺挥向锥形甩出，两粒魂色火花点睛，重拍加一圈翠环</summary>
        internal static void SpawnHitBurst(Vector2 pos, Vector2 sweepDir, float power, float soul) {
            int sparks = 4 + (int)(power * 6f);
            for (int i = 0; i < sparks; i++) {
                Vector2 vel = sweepDir.RotatedByRandom(0.75) * Main.rand.NextFloat(4f, 9f + power * 5f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel
                    , Main.rand.NextBool(4) ? RTerraBlade.TerraCore : RTerraBlade.TerraBright
                    , Main.rand.NextFloat(0.8f, 1.5f))?.Configure(false, Main.rand.Next(10, 18));
            }
            Color soulCol = RTerraBlade.SoulBright(soul);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(pos, sweepDir.RotatedByRandom(1.2) * Main.rand.NextFloat(3f, 7f)
                    , soulCol, Main.rand.NextFloat(0.7f, 1.1f))?.Configure(false, Main.rand.Next(10, 16));
            }
            if (power >= 0.75f) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(pos, Vector2.Zero, RTerraBlade.TerraBright, 0f)
                    ?.Configure(0.04f, 0.28f + 0.2f * power, 10);
            }
        }

        /// <summary>顿帧记账：预算内冻结几何，时长由收势扣回；重拍另补沿刀向的震屏</summary>
        private void ApplyImpactFeedback(Vector2 hitPos) {
            float want = IsSpin ? 3.5f : IsLoop ? 1.5f : 3f;
            float grant = MathF.Min(want, Beat.HitStop - hitStopSpent);
            if (grant > 0f) {
                hitStopFrames += grant;
                hitStopSpent += grant;
            }
            if (VaultUtils.isServer || IsLoop || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(hitPos, currentRotation.ToRotationVector2()
                , IsSpin ? 5f : 4f, 5f, IsSpin ? 9 : 7, 800f, FullName));
        }

        //==================== 绘制 ====================

        //残影与本体都不画在实体层：实体层会被刀光盖住，全部改画在 Overlay 层
        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>刀光内缘占刀长比例：回旋带薄一点免得读成实心盘</summary>
        private float InnerFrac => IsSpin ? 0.46f : IsLoop ? 0.34f : RibbonInnerFrac;

        /// <summary>反向拍翻刃：贴图刃口镜像到挥动前缘</summary>
        private void GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset) {
            bool edgeFlip = swingSign < 0;
            bool flipVertically = (lockedDirection < 0) != edgeFlip;
            effect = flipVertically ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flipVertically ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }

        /// <summary>刀身画多大由手→尖距离反推：中心停在 BladePark，刀尖恰好顶到刀光刃缘</summary>
        private void GetBladePose(Texture2D tex, float sweepValue, out Vector2 drawPos, out float rotation, out float scale) {
            Vector2 hand = Hand;
            Vector2 toTip = TipAt(sweepValue) - hand;
            float len = MathF.Max(toTip.Length(), 1f);
            rotation = toTip.ToRotation();
            float spriteAxis = MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            scale = len * (1f - BladePark) * 2f / spriteAxis * reachMul;
            drawPos = hand + toTip * (BladePark * reachMul);
        }

        void IOverlayDrawable.DrawOverlay(SpriteBatch sb) {
            Texture2D tex = TerraBladeFX.ItemTex(ItemID.TerraBlade);
            Vector2 origin = tex.Size() / 2f;
            GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);

            if (TerraBladeFX.Shader == null) {
                DrawArcFallback(sb);
            }

            DrawBladeGhosts(sb, tex, origin, effect, rotOffset);

            Vector2 hand = Hand;
            GetBladePose(tex, sweepNow, out Vector2 drawPos, out float rotation, out float scale);
            Vector2 screenPos = drawPos - Main.screenPosition;
            Color soulCol = RTerraBlade.SoulColor(Beat.Soul);

            //底光垫：小面积魂色软光压在刀身之下
            Texture2D glow = TerraBladeFX.SoftGlow;
            if (glow != null) {
                Color under = soulCol * (0.28f * (0.35f + 0.25f * trailFade + 0.4f * burstFlash));
                under.A = 0;
                sb.Draw(glow, Vector2.Lerp(hand, bladeTip, 0.7f) - Main.screenPosition, null, under, 0f
                    , glow.Size() / 2f, 0.9f * sizeMul, SpriteEffects.None, 0f);
            }

            //刀身本体：环境光偏白翠，远半按纵深压暗
            Color light = Lighting.GetColor((int)(hand.X / 16f), (int)(hand.Y / 16f));
            Color body = Color.Lerp(light, new Color(220, 255, 225), 0.4f) * bladeDim;
            body.A = 255;
            sb.Draw(tex, screenPos, null, body, rotation + rotOffset, origin, scale, effect, 0f);

            //能量镀层：翠色加色常驻，爆发帧再压一层魂色闪
            Color sheath = RTerraBlade.TerraBright * ((0.30f + 0.25f * trailFade) * bladeDim);
            sheath.A = 0;
            sb.Draw(tex, screenPos, null, sheath, rotation + rotOffset, origin, scale * 1.05f, effect, 0f);
            if (burstFlash > 0.02f) {
                Color flash = RTerraBlade.SoulBright(Beat.Soul) * (0.55f * burstFlash);
                flash.A = 0;
                sb.Draw(tex, screenPos, null, flash, rotation + rotOffset, origin, scale * 1.08f, effect, 0f);
            }

            //刃尖星芒：只住爆发头几帧，白是结构不是增益
            Texture2D star = TerraBladeFX.StarBlack;
            if (star != null && burstFlash > 0.05f) {
                Vector2 tipPos = bladeTip - Main.screenPosition;
                Color sc = RTerraBlade.SoulBright(Beat.Soul) * (0.8f * burstFlash);
                sc.A = 0;
                float ss = (0.10f + 0.10f * burstFlash) * sizeMul;
                sb.Draw(star, tipPos, null, sc, elapsed * 0.3f, star.Size() / 2f, ss, SpriteEffects.None, 0f);
                sb.Draw(star, tipPos, null, sc * 0.6f, -elapsed * 0.2f + MathHelper.PiOver4, star.Size() / 2f, ss * 0.6f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 沿刀光路径按角距取样重画刀身：近刃两影泄翠光（加色），旧影真 alpha 夜紫剪影沉进刀光内沉，
        /// 随刀光烧尽一起消散；环/旋残影骑在投影环上，位置/角度/刀长逐影重算
        /// </summary>
        private void DrawBladeGhosts(SpriteBatch sb, Texture2D tex, Vector2 origin, SpriteEffects effect, float rotOffset) {
            float strength = trailFade * slashBirth;
            if (trailCount < 2 || strength <= 0.03f) {
                return;
            }

            const float GhostHeadGap = 0.20f;   //贴着真刀的一段不画，免得糊住本体
            const float GhostSpacing = 0.30f;   //相邻残影角距
            const int GhostMax = 6;
            Span<float> ghostVal = stackalloc float[GhostMax];
            Span<float> ghostAge = stackalloc float[GhostMax];
            int count = 0;
            float arc = 0f;
            float nextEmit = GhostHeadGap;
            float maxArc = GhostHeadGap + GhostSpacing * GhostMax;
            for (int i = 1; i < trailCount && count < GhostMax; i++) {
                arc += MathF.Abs(trailVal[i - 1] - trailVal[i]);
                if (arc < nextEmit) {
                    continue;
                }
                ghostVal[count] = trailVal[i];
                ghostAge[count] = MathHelper.Clamp((arc - GhostHeadGap) / (maxArc - GhostHeadGap), 0f, 1f);
                count++;
                nextEmit += GhostSpacing;
            }

            //旧影先画，新影压上
            for (int k = count - 1; k >= 0; k--) {
                float fall = 1f - ghostAge[k];
                GetBladePose(tex, ghostVal[k], out Vector2 pos, out float rotation, out float scale);
                Color ghostColor;
                if (ghostAge[k] < 0.3f) {
                    ghostColor = RTerraBlade.TerraBright * (0.45f * fall * strength);
                    ghostColor.A = 0;
                }
                else {
                    ghostColor = RTerraBlade.NightDeep * (0.55f * fall * strength);
                }
                sb.Draw(tex, pos - Main.screenPosition, null, ghostColor, rotation + rotOffset, origin, scale, effect, 0f);
            }
        }

        /// <summary>着色器缺失时的翠弧回退</summary>
        private void DrawArcFallback(SpriteBatch sb) {
            Texture2D wave = TerraBladeFX.WaveFallback;
            if (wave == null || trailFade <= 0.02f) {
                return;
            }
            float alpha = trailFade * (0.35f + 0.4f * slashBirth);
            Vector2 arcCenter = Hand + currentRotation.ToRotationVector2() * (FullReach * 0.6f);
            Color c = RTerraBlade.TerraMain * alpha;
            c.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c, currentRotation + swingSign * 0.35f
                , wave.Size() / 2f, new Vector2(0.5f, 0.24f), SpriteEffects.None, 0f);
            Color c2 = RTerraBlade.TerraBright * (alpha * 0.7f);
            c2.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c2, currentRotation + swingSign * 0.35f
                , wave.Size() / 2f, new Vector2(0.45f, 0.11f), SpriteEffects.None, 0f);
        }

        /// <summary>刀光条带：外缘=刀尖轨迹外扩光晕垫，内缘向手/环心收；顶点色 rgb 纵深明暗，UV.x 尾→头</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect fx = TerraBladeFX.Shader;
            Texture2D noise = TerraBladeFX.Noise;
            if (fx == null || noise == null) {
                return;
            }

            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 hand = Hand;
            Vector2 ringCenter = IsSpin ? hand : LoopCenter;
            float innerFrac = InnerFrac;
            //光晕垫外扩：带厚 T=(1-innerFrac)×刀长，垫=T×HaloFrac/(1-HaloFrac)，刃缘正好落在刀尖轨迹上
            float padFrac = (1f - innerFrac) * HaloFrac / (1f - HaloFrac);
            float totalArc = 0f;
            for (int i = 1; i < trailCount; i++) {
                totalArc += MathF.Abs(trailVal[i - 1] - trailVal[i]);
            }
            float traveledArc = 0f;
            for (int i = 0; i < trailCount; i++) {
                if (i > 0) {
                    traveledArc += MathF.Abs(trailVal[i - 1] - trailVal[i]);
                }
                float factor = totalArc > 0.0001f
                    ? 1f - traveledArc / totalArc
                    : 1f - i / (float)Math.Max(trailCount - 1, 1);
                Vector2 outerPos;
                Vector2 innerPos;
                Color vcol = Color.White;
                if (IsChop) {
                    Vector2 dir = trailVal[i].ToRotationVector2();
                    outerPos = hand + dir * (FullReach * (1f + padFrac));
                    innerPos = hand + dir * (FullReach * innerFrac);
                }
                else {
                    Vector2 pt = RingPoint(trailVal[i], out float k, out _);
                    Vector2 spoke = pt - ringCenter;
                    outerPos = ringCenter + spoke * (1f + padFrac);
                    innerPos = ringCenter + spoke * innerFrac;
                    float dimT = MathHelper.Clamp((k - 0.84f) / 0.34f, 0f, 1f);
                    byte lum = (byte)(150 + 105 * dimT);
                    vcol = new Color(lum, lum, lum, (byte)(200 + 55 * dimT));
                }
                bars[i * 2] = new VertexPositionColorTexture(outerPos.ToVector3(), vcol, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture(innerPos.ToVector3(), vcol, new Vector2(factor, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;
            //噪声走 s1 寄存器，先绑贴图再 Apply
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            fx.CurrentTechnique = fx.Techniques["TechSlash"];
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uFade"]?.SetValue(trailFade);
            fx.Parameters["uBirth"]?.SetValue(slashBirth);
            fx.Parameters["uSoul"]?.SetValue(Beat.Soul);
            fx.Parameters["uHeat"]?.SetValue(1f + 0.9f * burstFlash);
            fx.Parameters["uBelly"]?.SetValue(Beat.Belly);
            fx.Parameters["uTailWidth"]?.SetValue(Beat.TailWidth);
            fx.Parameters["uSeed"]?.SetValue(seed);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
            TerraBladeFX.ReleaseNoiseSlot(device);
        }
    }
}
