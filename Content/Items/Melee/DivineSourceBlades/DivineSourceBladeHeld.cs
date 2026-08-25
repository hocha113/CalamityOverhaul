using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DivineSourceBlades
{
    /// <summary>
    /// 金源灭却刃 HeldProj，四拍连击。
    /// 拍0 逆时针快斩，拍1 顺时针回斩，拍2 椭圆立体环斩，拍3 沿椭圆反斩终结。
    /// ai[0] 拍号，ai[1] 充能标记(出手瞬间快照)
    /// </summary>
    internal class DivineSourceBladeHeld : BaseHeldProj
    {
        public override string Texture => DivineSourceBladeFX.BladeTexture;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<DivineSourceBlade>();

        private const int PhaseRaise = 0;
        private const int PhaseHold = 1;
        private const int PhaseSlash = 2;
        private const int PhaseRecover = 3;

        private static readonly Vector2 GripPixel = DivineSourceBladeFX.GripPixel;
        private static readonly Vector2 TipPixel = DivineSourceBladeFX.TipPixel;
        private const float HeldScale = 0.95f;
        private static float BaseReach => (TipPixel - GripPixel).Length() * HeldScale;

        /// <summary>椭圆拍的回转总角，约 342°，留缺口避免读成量角器圆环</summary>
        private const float LoopSpan = 5.97f;
        /// <summary>椭圆拍起手沿环路反向的预拉角</summary>
        private const float LoopPull = 0.5f;
        /// <summary>倾斜圆压扁率，读作纵深而非贴纸椭圆</summary>
        private const float Squash = 0.56f;

        //阶段时长与几何，InitStage 按拍号写入(已含攻速缩放)
        private int raiseDur = 6;
        private int holdDur = 2;
        private int slashDur = 5;
        private int recoverDur = 8;
        private int totalDur = 21;
        private float raiseBack = 1.95f;
        private float follow = 1.1f;
        private float reachScale = 1f;
        private float slashEasePow = 2.6f;
        private int fanSegments = 42;

        private float baseAngle;
        private float swingDir = 1f;
        private int facingDir = 1;
        private float tiltSign = 1f;
        private float mainAngle;
        private float mainReach;
        private float bladeScaleMul = 1f;
        private float bladeDim = 1f;
        private Vector2 mainTip;
        private float slashProgress;
        private float sweepT;
        private float loopPhiNow;
        private float loopPhiPrev;
        private float fanFade = 1f;
        private int flashTimer;
        private int hitstopTimer;
        private bool hitstopApplied;
        private bool boltsFired;
        private bool waveFired;
        private bool slashSoundPlayed;
        private bool leanApplied;
        private readonly HashSet<int> hitNPCs = [];

        /// <summary>连击拍号 0逆时针/1顺时针/2椭圆环斩/3椭圆反斩</summary>
        private int ComboStage => Math.Clamp((int)Projectile.ai[0], 0, 3);
        private bool IsEllipse => ComboStage >= 2;
        private bool IsFinisher => ComboStage == 3;
        private bool Empowered => Projectile.ai[1] > 0.5f;

        private int Timer {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        private int CurrentPhase {
            get {
                if (Timer <= raiseDur) {
                    return PhaseRaise;
                }
                if (Timer <= raiseDur + holdDur) {
                    return PhaseHold;
                }
                if (Timer <= raiseDur + holdDur + slashDur) {
                    return PhaseSlash;
                }
                return PhaseRecover;
            }
        }

        private float FullReach => BaseReach * reachScale;
        private float TotalSweep => raiseBack + follow;
        private float ArcStart => baseAngle - (swingDir * raiseBack);
        private float ArcEnd => baseAngle + (swingDir * follow);
        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private Vector2 EllipseCenter => Hand + baseAngle.ToRotationVector2() * (FullReach * 0.22f);
        private float ViewZ => MathF.Max(900f, FullReach * 2.6f);

        /// <summary>每拍光矢数量，充能期额外 +2</summary>
        private int BoltCount => ComboStage switch { 0 => 2, 1 => 3, 2 => 4, _ => 5 } + (Empowered ? 2 : 0);
        private float GoldMix => Empowered ? 0.5f : 0f;

        public override void SetDefaults() {
            Projectile.width = 50;
            Projectile.height = 50;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            //一拍一个弹幕，-1=每拍对同一目标只命中一次
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>按拍号写入时长与几何，时长除以攻速，快刀真的变快</summary>
        private void InitStage() {
            baseAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(baseAngle);
            facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : Math.Sign(cos);

            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            int D(int frames) => Math.Max(1, (int)MathF.Round(frames / speed));

            switch (ComboStage) {
                case 0:
                    //逆时针快斩(屏幕角减小方向)
                    raiseDur = D(6);
                    holdDur = D(2);
                    slashDur = D(5);
                    recoverDur = D(8);
                    raiseBack = 1.95f;
                    follow = 1.1f;
                    reachScale = 1f;
                    slashEasePow = 2.6f;
                    fanSegments = 42;
                    swingDir = -1f;
                    break;
                case 1:
                    //顺时针回斩
                    raiseDur = D(5);
                    holdDur = D(2);
                    slashDur = D(5);
                    recoverDur = D(8);
                    raiseBack = 1.95f;
                    follow = 1.1f;
                    reachScale = 1f;
                    slashEasePow = 2.6f;
                    fanSegments = 42;
                    swingDir = 1f;
                    break;
                case 2:
                    //椭圆立体环斩，逆时针
                    raiseDur = D(9);
                    holdDur = D(3);
                    slashDur = D(10);
                    recoverDur = D(10);
                    reachScale = 1.22f;
                    slashEasePow = 3.2f;
                    fanSegments = 64;
                    swingDir = -1f;
                    tiltSign = 1f;
                    break;
                default:
                    //沿椭圆反斩终结，顺时针，倾斜面翻转
                    raiseDur = D(11);
                    holdDur = D(4);
                    slashDur = D(9);
                    recoverDur = D(13);
                    reachScale = 1.38f;
                    slashEasePow = 4.2f;
                    fanSegments = 64;
                    swingDir = 1f;
                    tiltSign = -1f;
                    Projectile.damage = (int)(Projectile.damage * 1.35f);
                    break;
            }

            totalDur = raiseDur + holdDur + slashDur + recoverDur;

            if (!VaultUtils.isServer) {
                float pitch = ComboStage switch { 0 => 0.2f, 1 => 0.35f, 2 => -0.2f, _ => -0.65f };
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = pitch, Volume = IsEllipse ? 0.62f : 0.5f }, Owner.Center);
            }
        }

        /// <summary>倾斜3D圆上 φ 处的投影点，k 为透视系数(近大远小)，zNorm∈[-1,1]</summary>
        private Vector2 EllipsePoint(float phi, out float k, out float zNorm) {
            float R = FullReach;
            //局部坐标系 x 沿瞄准方向
            float lx = MathF.Cos(phi) * R;
            float ly = MathF.Sin(phi) * R * Squash;
            float z = MathF.Sin(phi) * MathF.Sqrt(1f - (Squash * Squash)) * R * tiltSign;
            zNorm = z / R;
            k = MathHelper.Clamp(ViewZ / (ViewZ - z), 0.84f, 1.18f);
            Vector2 dir = baseAngle.ToRotationVector2();
            Vector2 perp = new(-dir.Y, dir.X);
            return EllipseCenter + ((dir * lx) + (perp * ly)) * k;
        }

        /// <summary>椭圆拍的环路参数角，swingDir 决定行进方向</summary>
        private float LoopPhi(float t) => swingDir * ((t * (LoopSpan + LoopPull)) - LoopPull);

        public override void AI() {
            if (Item.type != ModContent.ItemType<DivineSourceBlade>() || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            if (Timer == 0) {
                InitStage();
            }

            //命中顿帧，姿态、扇面、身体前倾一起冻住
            if (hitstopTimer > 0) {
                hitstopTimer--;
            }
            else {
                Timer++;
            }
            if (flashTimer > 0) {
                flashTimer--;
            }

            int phase = CurrentPhase;
            UpdateBladeTransform(phase);
            UpdateBodyLean(phase);

            Owner.ChangeDir(facingDir);
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;

            Player.CompositeArmStretchAmount stretch = phase is PhaseRaise or PhaseRecover
                ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Full;
            Owner.SetCompositeArmFront(true, stretch, mainAngle - MathHelper.PiOver2);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters,
                mainAngle - MathHelper.PiOver2 + (swingDir * 0.25f));

            Projectile.Center = Vector2.Lerp(Hand, mainTip, 0.6f);
            Projectile.rotation = mainAngle;

            HandlePhaseEvents(phase);
            HandleParticles(phase);
            HandleLight(phase);

            if (Timer >= totalDur) {
                Projectile.Kill();
            }
        }

        /// <summary>由 Timer 解算刀角、刀长与扇面进度</summary>
        private void UpdateBladeTransform(int phase) {
            bladeScaleMul = 1f;
            bladeDim = 1f;

            if (IsEllipse) {
                UpdateEllipseTransform(phase);
                return;
            }

            float arcStart = ArcStart;
            float heldAngle = arcStart - (swingDir * 0.07f);

            switch (phase) {
                case PhaseRaise: {
                    float p = Timer / (float)raiseDur;
                    float eased = EaseOutCubic(p);
                    float liftFrom = arcStart + (swingDir * raiseBack * 0.75f);
                    mainAngle = MathHelper.Lerp(liftFrom, arcStart, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.5f, 0.92f, eased);
                    sweepT = 0f;
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    float p = (Timer - raiseDur) / (float)holdDur;
                    mainAngle = MathHelper.Lerp(arcStart, heldAngle, EaseOutQuad(p));
                    mainReach = FullReach * MathHelper.Lerp(0.92f, 0.97f, EaseOutQuad(p));
                    sweepT = 0f;
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    float p = (Timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    float eased = 1f - MathF.Pow(1f - p, slashEasePow);
                    mainAngle = MathHelper.Lerp(heldAngle, ArcEnd, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.97f, 1f, MathF.Sin(p * MathHelper.Pi));
                    sweepT = MathHelper.Clamp(MathF.Abs((mainAngle - arcStart) / TotalSweep), 0f, 1f);
                    break;
                }
                default: {
                    float q = (Timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    float settle = EaseOutQuad(Math.Min(1f, q * 1.8f));
                    mainAngle = ArcEnd + (swingDir * 0.2f * settle);
                    mainReach = FullReach * MathHelper.Lerp(0.97f, 0.78f, EaseInQuad(q));
                    slashProgress = 1f;
                    sweepT = 1f;
                    float fadeDur = MathF.Max(5f, recoverDur * 0.7f);
                    fanFade = MathHelper.Clamp(1f - ((Timer - raiseDur - holdDur - slashDur) / fadeDur), 0f, 1f);
                    break;
                }
            }

            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        /// <summary>椭圆拍，刀尖骑在倾斜圆投影上，刀长随透视呼吸</summary>
        private void UpdateEllipseTransform(int phase) {
            float phi;
            switch (phase) {
                case PhaseRaise: {
                    float p = Timer / (float)raiseDur;
                    //从身前沿环路反向拉到蓄势位，越拉越慢
                    phi = MathHelper.Lerp(swingDir * 0.35f, LoopPhi(0f), EaseOutCubic(p));
                    slashProgress = 0f;
                    sweepT = 0f;
                    loopPhiPrev = loopPhiNow = phi;
                    break;
                }
                case PhaseHold: {
                    //蓄满死寂，只留微颤
                    phi = -(swingDir * LoopPull) + (swingDir * 0.015f * MathF.Sin(Timer * 1.9f));
                    slashProgress = 0f;
                    sweepT = 0f;
                    loopPhiPrev = loopPhiNow = phi;
                    break;
                }
                case PhaseSlash: {
                    float p = (Timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    float eased = 1f - MathF.Pow(1f - p, slashEasePow);
                    loopPhiPrev = loopPhiNow;
                    phi = LoopPhi(eased);
                    loopPhiNow = phi;
                    sweepT = eased;
                    break;
                }
                default: {
                    float q = (Timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    float settle = EaseOutQuad(Math.Min(1f, q * 1.6f));
                    phi = LoopPhi(1f) + (swingDir * 0.18f * settle);
                    loopPhiPrev = loopPhiNow = phi;
                    slashProgress = 1f;
                    sweepT = 1f;
                    float fadeDur = MathF.Max(6f, recoverDur * 0.75f);
                    fanFade = MathHelper.Clamp(1f - ((Timer - raiseDur - holdDur - slashDur) / fadeDur), 0f, 1f);
                    break;
                }
            }

            Vector2 point = EllipsePoint(phi, out float k, out float zNorm);
            mainTip = point;
            Vector2 toTip = mainTip - Hand;
            mainAngle = toTip.ToRotation();
            mainReach = toTip.Length();

            //刀尖钉在椭圆带上，刀长随投影距离呼吸，轴向缩短=最强纵深线索
            bladeScaleMul = MathHelper.Clamp(mainReach / BaseReach, 0.55f, 2.2f);
            bladeDim = MathHelper.Lerp(0.62f, 1f, MathHelper.Clamp((k - 0.84f) / 0.34f, 0f, 1f));

            //起手把刀长收短一些，读作提刀
            if (phase == PhaseRaise) {
                float p = Timer / (float)raiseDur;
                bladeScaleMul *= MathHelper.Lerp(0.68f, 1f, EaseOutCubic(p));
            }
        }

        /// <summary>椭圆拍全身编舞，蓄力后仰、爆发前甩、支点钉在脚底</summary>
        private void UpdateBodyLean(int phase) {
            if (!IsEllipse) {
                return;
            }
            float leanAmp = IsFinisher ? 0.13f : 0.09f;
            float lean = phase switch {
                PhaseRaise => -0.55f * (Timer / (float)raiseDur),
                PhaseHold => -0.62f,
                PhaseSlash => MathHelper.Lerp(-0.62f, 1f, 1f - MathF.Pow(1f - slashProgress, 2.6f)),
                _ => MathHelper.Lerp(1f, 0f, EaseOutQuad(Math.Min(1f,
                    (Timer - raiseDur - holdDur - slashDur) / (float)Math.Max(1, recoverDur - 2)))),
            };
            Owner.fullRotation = facingDir * leanAmp * lean;
            Owner.fullRotationOrigin = new Vector2(Owner.width * 0.5f, Owner.height);
            leanApplied = true;
        }

        public override void OnKill(int timeLeft) {
            //前倾在任何退出路径都要复位，卡住的倾斜比没有更糟
            if (leanApplied && Owner.active) {
                Owner.fullRotation = 0f;
            }
        }

        private void HandlePhaseEvents(int phase) {
            //重拍蓄力完成的瞬间闪一记
            if (IsEllipse && Timer == raiseDur + 1) {
                flashTimer = 12;
            }

            if (phase == PhaseSlash && !slashSoundPlayed) {
                slashSoundPlayed = true;
                if (IsFinisher) {
                    flashTimer = 10;
                }
                if (!VaultUtils.isServer) {
                    SoundStyle whoosh = SoundID.Item71 with {
                        Pitch = ComboStage switch { 0 => 0.18f, 1 => 0.3f, 2 => -0.25f, _ => -0.5f },
                        Volume = IsEllipse ? 1.1f : 0.9f
                    };
                    SoundEngine.PlaySound(whoosh, Owner.Center);
                    if (IsEllipse) {
                        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.45f, Volume = 0.8f }, Owner.Center);
                    }
                }
                if (!VaultUtils.isServer && CWRClientConfig.Instance.ScreenVibration) {
                    Vector2 punchDir = (baseAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                    float strength = ComboStage switch { 0 => 3.5f, 1 => 3.5f, 2 => 6.5f, _ => 9.5f };
                    int frames = ComboStage switch { 0 => 6, 1 => 6, 2 => 9, _ => 12 };
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        Owner.Center, punchDir, strength, 8f, frames, 1100f, FullName));
                }
            }

            //光矢在斩击中段离手，沿出手瞄准方向扇形散开；高攻速下收势兜底防漏发
            if (!boltsFired && (phase == PhaseSlash && slashProgress >= 0.3f || phase == PhaseRecover)) {
                boltsFired = true;
                FireBolts();
            }

            //终结拍轰出巨型新月剑气
            if (IsFinisher && !waveFired && (phase == PhaseSlash && slashProgress >= 0.55f || phase == PhaseRecover)) {
                waveFired = true;
                FireWave();
            }
        }

        private void FireBolts() {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item12 with {
                    Pitch = 0.35f + (ComboStage * 0.08f),
                    Volume = 0.34f
                }, Owner.Center);
            }
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int count = BoltCount;
            float spread = 0.2f + (count * 0.055f);
            Vector2 origin = Vector2.Lerp(Hand, mainTip, 0.55f);
            for (int i = 0; i < count; i++) {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float ang = baseAngle + MathHelper.Lerp(-spread, spread, t);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(9.6f, 11.4f);
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), origin, vel,
                    ModContent.ProjectileType<DivineSourceBoltProjectile>(),
                    (int)(Projectile.damage * 0.42f), Projectile.knockBack * 0.35f, Owner.whoAmI,
                    ai0: Empowered ? 1f : 0f);
            }
        }

        private void FireWave() {
            Vector2 dir = baseAngle.ToRotationVector2();
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item73 with { Pitch = -0.5f, Volume = 1.2f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.1f, Volume = 0.9f }, Owner.Center);
                if (CWRClientConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        Owner.Center, dir, 10f, 7f, 13, 1300f, FullName));
                }
            }
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item),
                Hand + (dir * 46f), dir * 21f,
                ModContent.ProjectileType<DivineSourceWaveProjectile>(),
                (int)(Projectile.damage * 1.9f), Projectile.knockBack * 1.5f, Owner.whoAmI,
                ai0: 1.75f, ai1: swingDir, ai2: Empowered ? 1f : 0f);
        }

        private void HandleParticles(int phase) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 hand = Hand;
            Color cyan = DivineSourceBladeFX.Blend(DivineSourceBladeFX.CyanBright, DivineSourceBladeFX.AuricGold, GoldMix);
            Color azure = DivineSourceBladeFX.Blend(DivineSourceBladeFX.AzureBlue, DivineSourceBladeFX.AuricAmber, GoldMix);

            switch (phase) {
                case PhaseRaise:
                case PhaseHold: {
                    if (!IsEllipse) {
                        //快拍起手极短，零星数据屑点缀
                        if (Main.rand.NextBool(3)) {
                            Vector2 at = Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.45f, 1f));
                            PRTLoader.NewParticle<PRT_CyberSquare>(at, -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f),
                                cyan, Main.rand.NextFloat(0.4f, 0.65f)).Configure(azure, Main.rand.Next(10, 16));
                        }
                        break;
                    }
                    //重拍蓄力，能量屑被拽向刀身
                    float chargeT = phase == PhaseHold ? 1f : Timer / (float)raiseDur;
                    if (phase == PhaseHold || Main.rand.NextBool(2)) {
                        Vector2 anchor = Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.4f, 0.95f));
                        Vector2 offset = Main.rand.NextVector2CircularEdge(80f, 80f);
                        PRTLoader.NewParticle<PRT_CyberSquare>(anchor + offset, -offset * 0.1f,
                            cyan, Main.rand.NextFloat(0.5f, 0.85f) * (0.5f + chargeT * 0.5f))
                            .Configure(azure, Main.rand.Next(12, 18));
                    }
                    if (phase == PhaseHold && Main.rand.NextBool(2)) {
                        PRTLoader.NewParticle<PRT_DivineTechTriangle>(
                            Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.5f, 1f)),
                            -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.2f),
                            cyan, Main.rand.NextFloat(0.05f, 0.09f))
                            .Configure(azure, Main.rand.Next(14, 20));
                    }
                    break;
                }
                case PhaseSlash: {
                    //刃口沿切线甩出三角与方屑
                    Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                    int count = IsEllipse ? 3 : 2;
                    for (int i = 0; i < count; i++) {
                        Vector2 at = Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.5f, 1.02f));
                        PRTLoader.NewParticle<PRT_CyberSquare>(at,
                            sweepVel * Main.rand.NextFloat(3f, 8f) + Main.rand.NextVector2Circular(1f, 1f),
                            cyan, Main.rand.NextFloat(0.5f, 0.9f)).Configure(azure, Main.rand.Next(12, 20));
                    }
                    if (Main.rand.NextBool(IsEllipse ? 1 : 2)) {
                        PRTLoader.NewParticle<PRT_DivineTechTriangle>(
                            Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.6f, 1f)),
                            sweepVel * Main.rand.NextFloat(4f, 9f),
                            Empowered && Main.rand.NextBool(3) ? DivineSourceBladeFX.AuricGold : cyan,
                            Main.rand.NextFloat(0.06f, 0.12f))
                            .Configure(azure, Main.rand.Next(14, 22));
                    }
                    break;
                }
                default: {
                    if (Main.rand.NextBool(5)) {
                        Vector2 at = Vector2.Lerp(hand, mainTip, Main.rand.NextFloat(0.5f, 1f));
                        PRTLoader.NewParticle<PRT_CyberSquare>(at, -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f),
                            cyan, Main.rand.NextFloat(0.35f, 0.6f) * fanFade).Configure(azure, Main.rand.Next(10, 16));
                    }
                    break;
                }
            }
        }

        private void HandleLight(int phase) {
            float flash = flashTimer / 12f;
            float mul = IsEllipse ? 1.25f : 1f;
            Vector3 blue = new Vector3(0.2f, 0.42f, 0.72f) + new Vector3(0.35f, 0.25f, -0.2f) * GoldMix;
            switch (phase) {
                case PhaseRaise: {
                    float p = Timer / (float)raiseDur;
                    Lighting.AddLight(mainTip, blue * (0.3f + p * 0.5f) * mul);
                    break;
                }
                case PhaseHold:
                    Lighting.AddLight(mainTip, blue * (0.8f + flash * 0.6f) * mul);
                    break;
                case PhaseSlash:
                    Lighting.AddLight(Vector2.Lerp(Hand, mainTip, 0.6f), blue * 1.15f * mul);
                    break;
                default:
                    Lighting.AddLight(mainTip, blue * 0.6f * fanFade * mul);
                    break;
            }
        }

        private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - t, 3f);
        private static float EaseOutQuad(float t) => 1f - ((1f - t) * (1f - t));
        private static float EaseInQuad(float t) => t * t;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CurrentPhase != PhaseSlash) {
                return false;
            }
            Vector2 hand = Hand;
            //贴身兜底，画面重叠了却打不到最伤玩家信任
            if (targetHitbox.Distance(hand) <= 46f) {
                return true;
            }

            float width = IsEllipse ? 52f : 46f;
            float collisionPoint = 0f;

            if (!IsEllipse) {
                Vector2 tip = mainTip + (mainAngle.ToRotationVector2() * 12f);
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    hand, tip, width, ref collisionPoint);
            }

            //椭圆拍按本帧扫过的环路分步采样，快扫不漏判
            float from = loopPhiPrev;
            float to = loopPhiNow;
            int steps = Math.Max(1, (int)(MathF.Abs(to - from) / 0.2f) + 1);
            for (int i = 0; i <= steps; i++) {
                float phi = MathHelper.Lerp(from, to, i / (float)steps);
                Vector2 point = EllipsePoint(phi, out _, out _);
                point += (point - Hand).SafeNormalize(Vector2.Zero) * 12f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    hand, point, width, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void CutTiles() {
            if (CurrentPhase != PhaseSlash) {
                return;
            }
            Vector2 tip = mainTip + (mainTip - Hand).SafeNormalize(Vector2.Zero) * 12f;
            Utils.PlotTileLine(Hand, tip, 46f, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //击退跟出手朝向，不跟当帧刀角
            modifiers.HitDirectionOverride = facingDir;
            //四拍落差 轻-轻-重-终结
            modifiers.SourceDamage *= ComboStage switch { 0 => 0.85f, 1 => 0.9f, 2 => 1.25f, _ => 1.7f };
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //本次挥砍对同一目标只转发一次外部命中钩子
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            //命中充能，重拍喂得更多
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<DivineSourcePlayer>().AddCharge(IsEllipse ? 0.028f : 0.02f);
            }

            ApplyImpactFeedback(target.Center);

            SoundEngine.PlaySound(SoundID.Item71 with {
                Pitch = IsEllipse ? 0.0f : 0.35f,
                Volume = IsEllipse ? 0.8f : 0.55f
            }, target.Center);

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<DivineSourceHitFXProjectile>(), 0, 0f, Projectile.owner,
                    ai0: IsFinisher ? 1.1f : IsEllipse ? 0.8f : 0.5f, ai1: Empowered ? 1f : 0f);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => ApplyImpactFeedback(target.Center);

        /// <summary>顿帧一拍只吃一次，按拍重分级；重拍另补切线震屏</summary>
        private void ApplyImpactFeedback(Vector2 hitPos) {
            if (!hitstopApplied && CurrentPhase == PhaseSlash) {
                hitstopApplied = true;
                hitstopTimer = ComboStage switch { 0 => 1, 1 => 1, 2 => 2, _ => 3 };
            }
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration || !IsEllipse) {
                return;
            }
            Vector2 tangent = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(hitPos, tangent,
                IsFinisher ? 5.5f : 4f, 7f, IsFinisher ? 8 : 6, 900f, FullName));
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            DrawArcFan(sb);
            DrawBlade(sb);
            return false;
        }

        /// <summary>科技扇形网格，椭圆拍逐段带投影半径与纵深明暗</summary>
        private void DrawArcFan(SpriteBatch sb) {
            if (sweepT <= 0.03f || fanFade <= 0.02f) {
                return;
            }
            Effect effect = DivineSourceBladeFX.TechArc;
            if (effect == null) {
                DrawArcFallback(sb);
                return;
            }

            int segs = Math.Max(10, (int)(fanSegments * sweepT) + 2);
            var verts = new ColoredVertex[segs * 2];
            var inds = new short[(segs - 1) * 6];

            if (IsEllipse) {
                //椭圆带，外缘骑在倾斜圆投影上，内缘向椭圆心收
                Vector2 center = EllipseCenter;
                float phiStart = LoopPhi(0f);
                for (int i = 0; i < segs; i++) {
                    float t = i / (float)(segs - 1);
                    float u = t * sweepT;
                    float phi = MathHelper.Lerp(phiStart, loopPhiNow, t);
                    Vector2 outerPt = EllipsePoint(phi, out float k, out _);
                    Vector2 inner = center + ((outerPt - center) * 0.42f);
                    float dimT = MathHelper.Clamp((k - 0.84f) / 0.34f, 0f, 1f);
                    byte lum = (byte)(148 + (107 * dimT));
                    Color vcol = new(lum, lum, lum, (byte)(190 + (65 * dimT)));
                    verts[i * 2] = new ColoredVertex(outerPt - Main.screenPosition, vcol, new Vector3(u, 0f, 0f));
                    verts[(i * 2) + 1] = new ColoredVertex(inner - Main.screenPosition, vcol, new Vector3(u, 1f, 0f));
                }
            }
            else {
                float outerR = mainReach * 1.05f;
                float innerR = mainReach * 0.34f;
                Vector2 center = Hand;
                //起点补 0.3 弧度，扇根和刀背衔接
                float arcStart = ArcStart + (swingDir * 0.3f);
                for (int i = 0; i < segs; i++) {
                    float t = i / (float)(segs - 1);
                    float u = t * sweepT;
                    float ang = arcStart + (swingDir * TotalSweep * u);
                    Vector2 dir = ang.ToRotationVector2();
                    float bulge = 1f + (0.05f * MathF.Pow(t, 3f));
                    Vector2 outer = center + (dir * outerR * bulge) - Main.screenPosition;
                    Vector2 inner = center + (dir * innerR) - Main.screenPosition;
                    verts[i * 2] = new ColoredVertex(outer, Color.White, new Vector3(u, 0f, 0f));
                    verts[(i * 2) + 1] = new ColoredVertex(inner, Color.White, new Vector3(u, 1f, 0f));
                }
            }

            for (int i = 0; i < segs - 1; i++) {
                int vi = i * 2;
                int ii = i * 6;
                inds[ii] = (short)vi;
                inds[ii + 1] = (short)(vi + 1);
                inds[ii + 2] = (short)(vi + 2);
                inds[ii + 3] = (short)(vi + 2);
                inds[ii + 4] = (short)(vi + 1);
                inds[ii + 5] = (short)(vi + 3);
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            sb.End();

            BlendState prevBlend = device.BlendState;
            SamplerState prevSampler = device.SamplerStates[0];
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;

            device.BlendState = BlendState.AlphaBlend;
            device.SamplerStates[0] = SamplerState.LinearWrap;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;

            //s1 绑定噪声，shader 侧 register(s1)
            Texture2D noise = DivineSourceBladeFX.PerlinNoise;
            if (noise != null) {
                device.Textures[1] = noise;
            }

            Trail.CalculateRenderingMatrices(out Matrix view, out Matrix projection);
            effect.Parameters["WorldViewProjection"]?.SetValue(view * projection);
            effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
            effect.Parameters["SweepT"]?.SetValue(sweepT);
            effect.Parameters["FadeOut"]?.SetValue(fanFade);
            effect.Parameters["GlowBoost"]?.SetValue((IsEllipse ? 1.35f : 1.15f) + (slashProgress * (IsFinisher ? 0.55f : 0.35f)));
            effect.Parameters["RimIntensity"]?.SetValue(IsEllipse ? 1.5f : 1.25f);
            effect.Parameters["EmpowerMix"]?.SetValue(Empowered ? 0.55f : 0f);
            effect.Parameters["LeadColor"]?.SetValue(DivineSourceBladeFX.TechWhite.ToVector4());
            effect.Parameters["CoreColor"]?.SetValue(DivineSourceBladeFX.CyanBright.ToVector4());
            effect.Parameters["BodyColor"]?.SetValue(DivineSourceBladeFX.AzureBlue.ToVector4());
            effect.Parameters["DeepColor"]?.SetValue(DivineSourceBladeFX.DeepNavy.ToVector4());
            effect.Parameters["AccentColor"]?.SetValue(DivineSourceBladeFX.AuricGold.ToVector4());

            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                Trail.DrawUserPrimitives(verts, inds, device);
            }

            device.BlendState = prevBlend;
            device.SamplerStates[0] = prevSampler;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawArcFallback(SpriteBatch sb) {
            Texture2D wave = DivineSourceBladeFX.WaveFallback;
            if (wave == null) {
                return;
            }
            float alpha = fanFade * (0.35f + (slashProgress * 0.45f));
            Vector2 arcCenter = Hand + (mainAngle.ToRotationVector2() * (mainReach * 0.6f));
            Color c = DivineSourceBladeFX.AzureBlue * alpha;
            c.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c,
                mainAngle + (swingDir * 0.35f), wave.Size() / 2f, new Vector2(0.5f, 0.22f), SpriteEffects.None, 0f);
            Color c2 = DivineSourceBladeFX.CyanBright * (alpha * 0.7f);
            c2.A = 0;
            sb.Draw(wave, arcCenter - Main.screenPosition, null, c2,
                mainAngle + (swingDir * 0.35f), wave.Size() / 2f, new Vector2(0.45f, 0.1f), SpriteEffects.None, 0f);
        }

        private void ComputeBladeDrawXform(Texture2D tex, float angle,
            out Vector2 origin, out float bladeRot, out SpriteEffects flip) {
            bool facingLeft = Owner.direction == -1;
            flip = facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            origin = facingLeft
                ? new Vector2(tex.Width - GripPixel.X, GripPixel.Y)
                : GripPixel;

            Vector2 tipVec = TipPixel - GripPixel;
            if (facingLeft) {
                tipVec.X *= -1f;
            }
            bladeRot = angle - tipVec.ToRotation();
        }

        private void DrawBlade(SpriteBatch sb) {
            Asset<Texture2D> asset = TextureAssets.Projectile[Projectile.type];
            if (asset == null || !asset.IsLoaded) {
                return;
            }
            Texture2D tex = asset.Value;

            int phase = CurrentPhase;
            float flash = flashTimer / 12f;
            float goldMix = GoldMix;

            float glowStrength = phase switch {
                PhaseRaise => IsEllipse ? 0.35f + (0.6f * (Timer / (float)raiseDur)) : 0.55f,
                PhaseHold => IsEllipse ? 1f + (0.1f * MathF.Sin(Timer * 0.6f)) : 0.7f,
                PhaseSlash => IsEllipse ? 1.15f : 0.95f,
                _ => MathHelper.Lerp(0.25f, 0.9f, fanFade),
            };
            glowStrength += goldMix * 0.25f;

            Effect effect = DivineSourceBladeFX.BladeGlow;
            Vector2 handPos = Hand;
            Color light = Lighting.GetColor((int)(handPos.X / 16), (int)(handPos.Y / 16));
            Color bladeCol = Color.Lerp(light, new Color(215, 240, 255), 0.35f + (flash * 0.5f));
            //椭圆拍远半刀身按纵深压暗
            bladeCol *= bladeDim;
            bladeCol.A = 255;

            float scale = HeldScale * bladeScaleMul;

            if (effect != null) {
                effect.Parameters["TotalTime"]?.SetValue((float)Main.GameUpdateCount / 60f);
                effect.Parameters["GlowStrength"]?.SetValue(glowStrength);
                effect.Parameters["FlashBoost"]?.SetValue(flash * flash);
                effect.Parameters["TexelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
                effect.Parameters["BladeDir"]?.SetValue(Vector2.Normalize(TipPixel - GripPixel));
                effect.Parameters["OutlineColor"]?.SetValue(
                    DivineSourceBladeFX.Blend(DivineSourceBladeFX.CyanBright, DivineSourceBladeFX.AuricGold, goldMix).ToVector4());
                effect.Parameters["EnergyColor"]?.SetValue(
                    DivineSourceBladeFX.Blend(DivineSourceBladeFX.AzureBlue, DivineSourceBladeFX.AuricAmber, goldMix).ToVector4());
                effect.Parameters["FlashColor"]?.SetValue(DivineSourceBladeFX.TechWhite.ToVector4());
                Texture2D noise = DivineSourceBladeFX.Noise;
                if (noise != null) {
                    effect.Parameters["NoiseTexture"]?.SetValue(noise);
                }

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

                DrawBladeInstances(sb, tex, phase, handPos, bladeCol, scale);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else {
                DrawBladeInstances(sb, tex, phase, handPos, bladeCol, scale);
                if (flash > 0.05f) {
                    ComputeBladeDrawXform(tex, mainAngle, out Vector2 o, out float r, out SpriteEffects f);
                    Color silhouette = DivineSourceBladeFX.TechWhite * (flash * 0.8f);
                    silhouette.A = 0;
                    sb.Draw(tex, handPos - Main.screenPosition, null, silhouette, r, o, scale, f, 0f);
                }
            }
        }

        private void DrawBladeInstances(SpriteBatch sb, Texture2D tex, int phase,
            Vector2 handPos, Color bladeCol, float scale) {

            if (phase == PhaseSlash && slashProgress > 0.1f) {
                int ghostCount = IsEllipse ? 3 : 2;
                float ghostSpacing = IsEllipse ? 0.26f : 0.19f;
                for (int g = ghostCount; g >= 1; g--) {
                    float ghostAngle = mainAngle - (swingDir * ghostSpacing * g);
                    ComputeBladeDrawXform(tex, ghostAngle, out Vector2 gOrigin, out float gRot, out SpriteEffects gFlip);
                    float ghostAlpha = g switch { 1 => 0.4f, 2 => 0.18f, _ => 0.08f };
                    sb.Draw(tex, handPos - Main.screenPosition, null, bladeCol * ghostAlpha,
                        gRot, gOrigin, scale, gFlip, 0f);
                }
            }

            ComputeBladeDrawXform(tex, mainAngle, out Vector2 origin, out float rot, out SpriteEffects flip);
            sb.Draw(tex, handPos - Main.screenPosition, null, bladeCol, rot, origin, scale, flip, 0f);
        }
    }
}
