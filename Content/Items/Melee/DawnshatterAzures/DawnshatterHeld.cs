using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DawnshatterAzures
{
    /// <summary>
    /// 苍穹破晓手持,常驻五拍连段控制器<br/>
    /// 按住左键滚动连段,拍尾无续拍锁存则收枪退场;拍状态 owner 权威,拍切换经 NetHeldSend 广播<br/>
    /// mainVec 单一真源:碰撞/贴图/手臂/光照全部由它派生;扫击拍的 mainVec 来自倾斜圆透视投影(z 驱动枪身伸缩)<br/>
    /// 攻速=时间膨胀(elapsed += speedMul),判定窗/音效/相位切换按本帧区间与窗口的交集消费,高攻速不漏
    /// </summary>
    internal class DawnshatterHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<DawnshatterAzure>();

        private const int FrameCount = 4;
        private const int BeatCount = 5;
        /// 静止持距(手到枪尖)
        private const float RestTip = 150f;
        /// 透视参考距离,镜像 CrimsonSlashRenderer.ViewZ 量级
        private const float ViewZ = 900f;

        /// 拍定义,时长单位 tick(逻辑帧,乘时间膨胀前)
        private struct BeatDef
        {
            public int Kind;            //0刺 1扫 2旋
            public float Windup;
            public float Active;
            public float Recover;
            public float DamageMul;
            public float Radius0;       //扫/旋起始半径
            public float Radius1;
            public float ArcStart;      //相对瞄准角(弧度,含扫向符号)
            public float ArcEnd;
            public float Tilt0;         //圆面绕瞄准轴倾角
            public float Tilt1;
            public float Roll;          //投影后滚转,×朝向
            public float Total => Windup + Active + Recover;
        }

        //==== 连段状态(owner 权威,经 NetHeldSend 同步) ====
        private int beatIndex;
        private float elapsed;
        private float aimAngle;
        private bool queuedNext;

        private float speedMul = 1f;
        private int lockedDirection = 1;
        private float retractTimer;
        private int hitstopTimer;
        private float baseDamage;
        private bool aimFrozen;

        //==== 本帧派生量 ====
        /// 持握点指向枪尖,单一真源
        private Vector2 mainVec;
        /// 枪尖伪 z,+朝观者
        private float depthZ;
        /// 本帧伤害窗交集(拍内时刻),无则 <0
        private float dmgFrom = -1f;
        private float dmgTo = -1f;
        private bool burstSoundPlayed;
        private bool pulse2Started;

        //==== 残影环 ====
        private const int GhostMax = 4;
        private readonly Vector2[] ghostVecs = new Vector2[GhostMax];
        private int ghostCount;

        //==== 火焰演出状态 ====
        /// 点燃度,爆发期升满,收势冷却
        private float heat;
        /// 命中过曝脉冲,快衰减
        private float flashPulse;
        /// 条带整体透明度,前摇清尾
        private float trailFade;
        /// 刺击条带尾锚(手到尾),脉冲起点
        private float pulseRear = RestTip;
        /// 本拍刺出的最远端,火线驻留在被刺穿的位置而非随枪回缩
        private float maxTip;
        private readonly List<VertexPositionColorTexture[]> stripSink = [];

        //==== 弧带采样环,头在 index0 ====
        private const int ArcSampleMax = 90;
        private const float ArcSampleSpacing = 15f;
        private readonly List<DawnshatterRenderer.ArcSample> arcSamples = [];
        /// 上次采样的 φ,只在推进方向取样,过冲回坐不产生锯齿
        private float lastSweepPhi = float.NaN;
        private bool endPopFired;
        /// 终结拍旋转中的分段呼啸,已播到第几声
        private int spinWhooshStage;
        /// 终结拍身体后仰前甩,平滑量
        private float bodyLean;
        private bool appliedLean;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 54;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//每拍手动清免疫,一拍一敌一伤
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 90;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        //==== 拍表 ====

        private static BeatDef GetBeat(int i) => i switch {
            //B0 破晓直刺,快启动
            0 => new BeatDef {
                Kind = 0, Windup = 3f, Active = 7f, Recover = 9f, DamageMul = 1f,
            },
            //B1 上撩挑斩,倾角在挥动中扫过(轴向前缩线索)
            1 => new BeatDef {
                Kind = 1, Windup = 8f, Active = 13f, Recover = 11f, DamageMul = 1.05f,
                Radius0 = 175f, Radius1 = 205f, ArcStart = 1.15f, ArcEnd = -1.25f,
                Tilt0 = 1.0f, Tilt1 = 0.3f, Roll = 0.15f,
            },
            //B2 二连刺,拍内双相位+前倾微冲步
            2 => new BeatDef {
                Kind = 2, Windup = 6f, Active = 16f, Recover = 10f, DamageMul = 0.9f,
            },
            //B3 横扫回旋,贯穿平面,收-爆:缓推三成后一口气抽完
            3 => new BeatDef {
                Kind = 1, Windup = 8f, Active = 16f, Recover = 8f, DamageMul = 1.15f,
                Radius0 = 175f, Radius1 = 205f, ArcStart = -2.3f, ArcEnd = 2.3f,
                Tilt0 = 0.55f, Tilt1 = 0.55f, Roll = -0.2f,
            },
            //B4 日冕终结,加速回旋1.5圈,半径长出
            _ => new BeatDef {
                Kind = 1, Windup = 10f, Active = 17f, Recover = 12f, DamageMul = 1.6f,
                Radius0 = 190f, Radius1 = 260f, ArcStart = -0.6f, ArcEnd = -0.6f + MathHelper.Pi * 3f,
                Tilt0 = 0.45f, Tilt1 = 0.45f, Roll = -0.2f,
            },
        };

        private static bool IsFinisher(int i) => i == BeatCount - 1;

        //==== 曲线工具 ====

        private static float SmoothStep01(float v) {
            v = MathHelper.Clamp(v, 0f, 1f);
            return v * v * (3f - 2f * v);
        }

        private static float EaseOutCubic(float v) {
            v = MathHelper.Clamp(v, 0f, 1f);
            return 1f - MathF.Pow(1f - v, 3f);
        }

        private static float EaseOutExpo(float v) {
            v = MathHelper.Clamp(v, 0f, 1f);
            return v >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * v);
        }

        /// 收-爆-停挥砍进度,creep 缓推→爆发过冲→回坐
        private static float SwingCurve(float t, float creepEnd, float burstEnd, float creepAmt, float overshoot) {
            t = MathHelper.Clamp(t, 0f, 1f);
            if (t < creepEnd) {
                return creepAmt * SmoothStep01(t / creepEnd);
            }
            if (t < burstEnd) {
                return MathHelper.Lerp(creepAmt, 1f + overshoot, SmoothStep01((t - creepEnd) / (burstEnd - creepEnd)));
            }
            return MathHelper.Lerp(1f + overshoot, 1f, SmoothStep01((t - burstEnd) / (1f - burstEnd)));
        }

        //==== 三维运动求值 ====

        /// <summary>倾斜圆上一点的透视投影;圆面绕瞄准轴倾 tilt,z=+朝观者</summary>
        private static Vector2 ProjectTiltedCircle(Vector2 axisUnit, float radius, float phi, float tilt, float roll, out float z) {
            Vector2 perp = axisUnit.RotatedBy(MathHelper.PiOver2);
            float sinPhi = MathF.Sin(phi);
            Vector2 planar = axisUnit * MathF.Cos(phi) + perp * sinPhi * MathF.Cos(tilt);
            z = sinPhi * MathF.Sin(tilt) * radius;
            float k = ViewZ / MathF.Max(ViewZ - z, 220f);
            Vector2 result = planar * radius * k;
            return roll != 0f ? result.RotatedBy(roll) : result;
        }

        /// <summary>扫击拍内 t 时刻的圆参数角(未镜像)</summary>
        private static float SweepPhiAt(int beat, in BeatDef d, float t) {
            float activeT = MathHelper.Clamp((t - d.Windup) / d.Active, 0f, 1f);
            float sign = MathF.Sign(d.ArcEnd - d.ArcStart);
            if (t < d.Windup) {
                //蓄力回拉过 chamber
                float w = EaseOutCubic(t / d.Windup);
                return d.ArcStart - sign * 0.3f * w;
            }
            if (t < d.Windup + d.Active) {
                float p = beat switch {
                    1 => SwingCurve(activeT, 0.28f, 0.66f, 0.05f, 0.04f),
                    //B3 收-爆:缓推压到 8%,爆发段一口气抽完(速度是对比不是连续加速)
                    3 => SwingCurve(activeT, 0.30f, 0.72f, 0.08f, 0.03f),
                    _ => activeT * activeT,                  //B4 加速回旋
                };
                return MathHelper.Lerp(d.ArcStart, d.ArcEnd, p);
            }
            //收势:持在收笔角,缓回半程守势
            float r = SmoothStep01((t - d.Windup - d.Active) / d.Recover);
            float hold = IsFinisher(beat) ? 0.30f : 0.18f;
            float back = r <= hold ? 0f : SmoothStep01((r - hold) / (1f - hold));
            return MathHelper.Lerp(d.ArcEnd, d.ArcEnd - sign * 0.55f, back);
        }

        /// <summary>拍内 t 时刻的 mainVec,纯函数,碰撞与绘制共用</summary>
        private Vector2 MotionAt(int beat, float t, out float z) {
            z = 0f;
            BeatDef d = GetBeat(beat);
            Vector2 aimUnit = aimAngle.ToRotationVector2();

            if (d.Kind == 0 || d.Kind == 2) {
                return aimUnit * ThrustTipAt(beat, in d, t);
            }

            float phi = SweepPhiAt(beat, in d, t);
            float activeT = MathHelper.Clamp((t - d.Windup) / d.Active, 0f, 1f);
            float radius = MathHelper.Lerp(d.Radius0, d.Radius1, activeT);
            float tilt = MathHelper.Lerp(d.Tilt0, d.Tilt1, activeT);
            //φ 相对瞄准角,朝向左时镜像扫向
            return ProjectTiltedCircle(aimUnit, radius, phi * lockedDirection, tilt, d.Roll * lockedDirection, out z);
        }

        /// 刺击持距包络(手到枪尖)
        private static float ThrustTipAt(int beat, in BeatDef d, float t) {
            if (beat == 0) {
                if (t < d.Windup) {
                    return MathHelper.Lerp(RestTip, RestTip - 26f, EaseOutCubic(t / d.Windup));
                }
                if (t < d.Windup + d.Active) {
                    return MathHelper.Lerp(RestTip - 26f, 320f, EaseOutExpo((t - d.Windup) / d.Active));
                }
                return MathHelper.Lerp(320f, RestTip, SmoothStep01((t - d.Windup - d.Active) / d.Recover));
            }
            //B2 双相位:短刺→回坐→长刺
            if (t < 6f) {
                return MathHelper.Lerp(RestTip, 118f, EaseOutCubic(t / 6f));
            }
            if (t < 11f) {
                return MathHelper.Lerp(118f, 300f, EaseOutExpo((t - 6f) / 5f));
            }
            if (t < 15f) {
                return MathHelper.Lerp(300f, 218f, SmoothStep01((t - 11f) / 4f));
            }
            if (t < 22f) {
                return MathHelper.Lerp(218f, 360f, EaseOutExpo((t - 15f) / 7f));
            }
            return MathHelper.Lerp(360f, RestTip, SmoothStep01((t - 22f) / 10f));
        }

        /// <summary>拍的伤害窗(拍内时刻);B2 有两段,w2 无效时 x>y</summary>
        private static void GetDamageWindows(int beat, in BeatDef d, out Vector2 w1, out Vector2 w2) {
            w2 = new Vector2(1f, 0f);
            switch (beat) {
                case 0:
                    w1 = new Vector2(d.Windup, d.Windup + d.Active);
                    break;
                case 2:
                    w1 = new Vector2(6f, 11f);
                    w2 = new Vector2(15f, 22f);
                    break;
                default:
                    w1 = new Vector2(d.Windup, d.Windup + d.Active * 0.92f);
                    break;
            }
        }

        //==== 生命周期 ====

        public override void Initialize() {
            baseDamage = Projectile.damage;
            StartBeat(0, first: true);
        }

        private void StartBeat(int index, bool first = false) {
            beatIndex = index;
            elapsed = 0f;
            queuedNext = false;
            ResetBeatPresentation(index);

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            Projectile.damage = (int)(baseDamage * GetBeat(index).DamageMul);
            //一拍一敌一伤,拍首清免疫表
            ResetLocalImmunity();
            CaptureAim();
            if (!first) {
                Projectile.netUpdate = true;
            }
        }

        /// <summary>拍首演出状态重置,本地拍切换与远端收包共用,远端不得跨拍残留弧带</summary>
        private void ResetBeatPresentation(int index) {
            retractTimer = 0f;
            burstSoundPlayed = false;
            pulse2Started = false;
            aimFrozen = false;
            ghostCount = 0;
            spinWhooshStage = 0;

            //拍首清尾+火焰状态回落
            trailFade = 0f;
            heat *= 0.3f;
            pulseRear = index == 0 ? RestTip - 26f : 118f;
            maxTip = pulseRear;
            arcSamples.Clear();
            lastSweepPhi = float.NaN;
            endPopFired = false;
        }

        private void ResetLocalImmunity() {
            for (int i = 0; i < Projectile.localNPCImmunity.Length; i++) {
                Projectile.localNPCImmunity[i] = 0;
            }
        }

        private void CaptureAim() {
            Vector2 unit = UnitToMouseV;
            if (unit == Vector2.Zero) {
                unit = Vector2.UnitX * Owner.direction;
            }
            aimAngle = unit.ToRotation();
            lockedDirection = MathF.Sign(unit.X) == 0 ? Owner.direction : Math.Sign(unit.X);
        }

        public override void NetHeldSend(BinaryWriter writer) {
            writer.Write((byte)beatIndex);
            writer.Write(elapsed);
            writer.Write(aimAngle);
        }

        public override void NetHeldReceive(BinaryReader reader) {
            int newBeat = reader.ReadByte();
            float newElapsed = reader.ReadSingle();
            aimAngle = reader.ReadSingle();
            if (newBeat != beatIndex) {
                //远端拍切换,完整重置拍内演出状态
                beatIndex = newBeat;
                ResetBeatPresentation(newBeat);
                Projectile.damage = (int)(baseDamage * GetBeat(newBeat).DamageMul);
            }
            elapsed = newElapsed;
            lockedDirection = MathF.Cos(aimAngle) >= 0f ? 1 : -1;
        }

        public override BitsByte SendBitsByte(BitsByte flags) {
            flags = base.SendBitsByte(flags);
            flags[2] = queuedNext;
            return flags;
        }

        public override void ReceiveBitsByte(BitsByte flags) {
            base.ReceiveBitsByte(flags);
            queuedNext = flags[2];
        }

        //==== 主循环 ====

        public override void AI() {
            if (Item.type != ModContent.ItemType<DawnshatterAzure>()) {
                Projectile.Kill();
                return;
            }

            //顿帧:冻结时间线,姿态驻留
            if (hitstopTimer > 0) {
                hitstopTimer--;
                UpdatePlayerPose();
                return;
            }

            VaultUtils.ClockFrame(ref Projectile.frame, 5, FrameCount - 1);
            BeatDef d = GetBeat(beatIndex);
            float total = d.Total;

            //收枪退场
            if (elapsed >= total) {
                if (DownLeft) {
                    //连段已断,从头再来
                    StartBeat(0);
                    UpdatePlayerPose();
                    return;
                }
                retractTimer += speedMul;
                mainVec = Vector2.Lerp(mainVec, aimAngle.ToRotationVector2() * RestTip
                    , SmoothStep01(retractTimer / 6f));
                dmgFrom = dmgTo = -1f;
                //火线在退场期原地熄灭,痕迹不许随弹幕死
                trailFade *= 0.78f;
                heat *= 0.9f;
                flashPulse *= 0.55f;
                if (retractTimer >= 6f && (trailFade <= 0.05f || retractTimer >= 14f)) {
                    Projectile.Kill();
                    return;
                }
                UpdatePlayerPose();
                return;
            }

            float from = elapsed;
            float to = MathF.Min(elapsed + speedMul, total);

            //方向锁定窗:前摇+判定前25%,迟交付
            if (!aimFrozen) {
                if (from < d.Windup + d.Active * 0.25f) {
                    CaptureAim();
                }
                else {
                    aimFrozen = true;
                    Projectile.netUpdate = true;
                }
            }

            //续拍锁存:前摇+4t 之后按下即锁
            if (!queuedNext && from >= d.Windup + 4f && DownLeft) {
                queuedNext = true;
                Projectile.netUpdate = true;
            }

            ConsumeWindows(in d, from, to);

            PushGhost(mainVec);
            mainVec = MotionAt(beatIndex, to, out depthZ);

            UpdateFireState(in d, to);
            SpawnBladeFire(in d);
            if (d.Kind == 1) {
                SampleArc(in d, from, to);
            }

            UpdatePlayerPose();
            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + mainVec * 0.75f
                , new Vector3(1.15f, 0.72f, 0.28f) * 0.7f);

            elapsed = to;

            //取消窗:续拍锁存吃掉七成收势,连段不等收势播完;无锁存才走完整收势+退场
            float cancelAt = d.Windup + d.Active + d.Recover * 0.3f;
            if (queuedNext && elapsed >= cancelAt) {
                StartBeat((beatIndex + 1) % BeatCount);
            }
        }

        /// <summary>本帧区间与各窗口求交集消费:伤害窗/爆发音/双刺二段事件,高攻速跨阶段不漏</summary>
        private void ConsumeWindows(in BeatDef d, float from, float to) {
            GetDamageWindows(beatIndex, in d, out Vector2 w1, out Vector2 w2);

            dmgFrom = MathF.Max(from, w1.X);
            dmgTo = MathF.Min(to, w1.Y);
            if (dmgTo <= dmgFrom && w2.X < w2.Y) {
                dmgFrom = MathF.Max(from, w2.X);
                dmgTo = MathF.Min(to, w2.Y);
            }

            //爆发起点:音效+点燃脉冲,每拍一次"破晓"是离散事件不是渐变
            float burstAt = beatIndex == 2 ? 6f : d.Windup;
            if (!burstSoundPlayed && to >= burstAt) {
                burstSoundPlayed = true;
                flashPulse = MathF.Max(flashPulse, 0.65f);
                PlaySwingSound(second: false);
            }

            //B2 二段:清免疫+补第二声+点燃+前倾冲步
            if (beatIndex == 2 && !pulse2Started && to >= 15f) {
                pulse2Started = true;
                ResetLocalImmunity();
                flashPulse = MathF.Max(flashPulse, 0.5f);
                PlaySwingSound(second: true);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Owner.velocity += aimAngle.ToRotationVector2() * 5f;
                }
            }

            //终结拍转速爬升中的分段呼啸,一圈半不能只响一声
            if (IsFinisher(beatIndex) && !VaultUtils.isServer) {
                float activeT = (to - d.Windup) / d.Active;
                if (spinWhooshStage == 0 && activeT >= 0.4f) {
                    spinWhooshStage = 1;
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.7f, Pitch = -0.05f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.25f, Pitch = 0.3f }, Owner.Center);
                }
                else if (spinWhooshStage == 1 && activeT >= 0.75f) {
                    spinWhooshStage = 2;
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = 0.15f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.3f, Pitch = 0.45f }, Owner.Center);
                }
            }

            //终结拍收尾爆点:过曝脉冲+余烬环+烟,震屏仅施术者本地
            if (IsFinisher(beatIndex) && !endPopFired && to >= d.Windup + d.Active) {
                endPopFired = true;
                flashPulse = 1f;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Owner.CWR().ScreenShakeValue = 6f;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.35f }, Owner.Center);
                    Vector2 hand = Owner.GetPlayerStabilityCenter();
                    for (int i = 0; i < 14; i++) {
                        float ang = MathHelper.TwoPi * i / 14f + Main.rand.NextFloat(0.2f);
                        Vector2 dir = ang.ToRotationVector2();
                        PRTLoader.NewParticle<PRT_DawnEmber>(hand + dir * Main.rand.NextFloat(120f, 190f)
                            , dir * Main.rand.NextFloat(5f, 10f), default, Main.rand.NextFloat(1f, 1.6f))
                            .Configure(Main.rand.Next(20, 32));
                    }
                    for (int i = 0; i < 5; i++) {
                        Vector2 dir = Main.rand.NextVector2Unit();
                        PRTLoader.NewParticle<PRT_DawnSoot>(hand + dir * Main.rand.NextFloat(90f, 170f)
                            , dir * Main.rand.NextFloat(1f, 2.5f), default, Main.rand.NextFloat(0.8f, 1.3f));
                    }
                }
            }
        }

        /// <summary>弧带取样:只在 φ 推进方向取,过冲回坐不产生锯齿;间距按枪尖弧长</summary>
        private void SampleArc(in BeatDef d, float from, float to) {
            if (to <= d.Windup || from >= d.Windup + d.Active) {
                return;
            }
            float f = MathF.Max(from, d.Windup);
            float t = MathF.Min(to, d.Windup + d.Active);
            float sign = MathF.Sign(d.ArcEnd - d.ArcStart);
            int sub = Math.Clamp((int)MathF.Ceiling((t - f) * 3f), 1, 12);
            for (int i = 1; i <= sub; i++) {
                float ti = MathHelper.Lerp(f, t, i / (float)sub);
                float phi = SweepPhiAt(beatIndex, in d, ti);
                if (!float.IsNaN(lastSweepPhi) && (phi - lastSweepPhi) * sign <= 0.0005f) {
                    continue;
                }
                lastSweepPhi = phi;
                Vector2 tip = MotionAt(beatIndex, ti, out float z);
                if (arcSamples.Count > 0 && (tip - arcSamples[0].Tip).Length() < ArcSampleSpacing) {
                    continue;
                }
                arcSamples.Insert(0, new DawnshatterRenderer.ArcSample {
                    Tip = tip,
                    Z = 0.5f + MathHelper.Clamp(z / 220f, -0.5f, 0.5f),
                    Heat = MathF.Max(heat, 0.3f),
                });
                if (arcSamples.Count > ArcSampleMax) {
                    arcSamples.RemoveAt(arcSamples.Count - 1);
                }
            }
        }

        /// <summary>点燃语法:收势冷却,爆发升满;火线亮度独立于枪身姿态</summary>
        private void UpdateFireState(in BeatDef d, float t) {
            flashPulse *= 0.55f;
            if (t < d.Windup) {
                heat *= 0.86f;
                trailFade = 0f;
            }
            else if (t < d.Windup + d.Active) {
                float p = (t - d.Windup) / d.Active;
                heat = MathF.Max(heat, MathF.Min(p * 2.4f, 1f));
                trailFade = 1f;
                maxTip = MathF.Max(maxTip, mainVec.Length());
            }
            else {
                heat *= 0.955f;
                trailFade *= 0.88f;
                //余烬冷却成烟,火线上零星冒出
                if (!VaultUtils.isServer && trailFade > 0.15f && Main.rand.NextBool(4)) {
                    Vector2 hand = Owner.GetPlayerStabilityCenter();
                    Vector2 sootPos;
                    if (d.Kind == 1) {
                        if (arcSamples.Count == 0) {
                            return;
                        }
                        sootPos = hand + arcSamples[Main.rand.Next(arcSamples.Count)].Tip * Main.rand.NextFloat(0.82f, 1f);
                    }
                    else {
                        sootPos = hand + aimAngle.ToRotationVector2() * Main.rand.NextFloat(pulseRear, maxTip);
                    }
                    PRTLoader.NewParticle<PRT_DawnSoot>(sootPos, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f))
                        , default, Main.rand.NextFloat(0.7f, 1.1f));
                }
            }
        }

        /// <summary>刃上火:判定窗内余烬喷流+刃缘火舌,粒子密度按攻速补偿;扫击切向甩,刺击顺枪喷</summary>
        private void SpawnBladeFire(in BeatDef d) {
            if (VaultUtils.isServer || dmgTo <= dmgFrom) {
                return;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();

            int times = (int)speedMul;
            if (Main.rand.NextFloat() < speedMul % 1f) {
                times++;
            }

            if (d.Kind == 1) {
                //扫击:余烬沿切向甩出(离心),火舌径向外舔
                Vector2 tipUnit = mainVec.SafeNormalize(Vector2.UnitX);
                float spin = MathF.Sign(d.ArcEnd - d.ArcStart) * lockedDirection;
                Vector2 tangent = tipUnit.RotatedBy(MathHelper.PiOver2) * spin;
                for (int i = 0; i < times; i++) {
                    Vector2 pos = hand + mainVec * Main.rand.NextFloat(0.72f, 1.02f);
                    Vector2 vel = tangent * Main.rand.NextFloat(3.5f, 8f) + tipUnit * Main.rand.NextFloat(0.5f, 2.5f);
                    PRTLoader.NewParticle<PRT_DawnEmber>(pos, vel, default, Main.rand.NextFloat(0.9f, 1.4f))
                        .Configure(Main.rand.Next(18, 30));

                    if (Main.rand.NextBool(2)) {
                        Vector2 outward = (tipUnit - tangent * 0.4f).SafeNormalize(Vector2.UnitY);
                        PRTLoader.NewParticle<PRT_DawnTongue>(hand + mainVec * Main.rand.NextFloat(0.5f, 0.95f)
                            , tangent * 2f, default, Main.rand.NextFloat(0.6f, 1f))
                            .Configure(outward, Main.rand.NextFloat(0.55f, 0.95f), Main.rand.Next(3, 6));
                    }
                }
                return;
            }

            Vector2 unit = aimAngle.ToRotationVector2();
            Vector2 perp = unit.RotatedBy(MathHelper.PiOver2);
            float tip = mainVec.Length();
            for (int i = 0; i < times; i++) {
                Vector2 pos = hand + unit * (tip - Main.rand.NextFloat(0f, 34f)) + perp * Main.rand.NextFloat(-7f, 7f);
                Vector2 vel = unit * Main.rand.NextFloat(4f, 9f) + perp * Main.rand.NextFloat(-1.6f, 1.6f);
                PRTLoader.NewParticle<PRT_DawnEmber>(pos, vel, default, Main.rand.NextFloat(0.9f, 1.4f))
                    .Configure(Main.rand.Next(18, 30));

                if (Main.rand.NextBool(2)) {
                    float along = Main.rand.NextFloat(0.45f, 0.9f);
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 outward = (perp * side - unit * 0.35f).SafeNormalize(Vector2.UnitY);
                    PRTLoader.NewParticle<PRT_DawnTongue>(hand + unit * (tip * along), unit * 2f
                        , default, Main.rand.NextFloat(0.6f, 1f))
                        .Configure(outward, Main.rand.NextFloat(0.55f, 0.95f), Main.rand.Next(3, 6));
                }
            }
        }

        private void PlaySwingSound(bool second) {
            if (VaultUtils.isServer) {
                return;
            }
            if (IsFinisher(beatIndex)) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 1f, Pitch = -0.35f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.65f, Pitch = -0.15f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.45f, Pitch = 0.1f }, Owner.Center);
                return;
            }
            float pitch = -0.1f + beatIndex * 0.1f + (second ? 0.12f : 0f);
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = pitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.3f, Pitch = 0.25f + pitch * 0.5f }, Owner.Center);
        }

        /// <summary>终结拍身体语言:蓄力后仰→爆发前甩→收势归位,其余拍归零</summary>
        private float BodyLeanTarget() {
            if (!IsFinisher(beatIndex)) {
                return 0f;
            }
            BeatDef d = GetBeat(beatIndex);
            if (elapsed < d.Windup) {
                return -0.06f * EaseOutCubic(elapsed / d.Windup);
            }
            if (elapsed < d.Windup + d.Active) {
                float p = MathHelper.Clamp((elapsed - d.Windup) / d.Active, 0f, 1f);
                return MathHelper.Lerp(-0.06f, 0.09f, SmoothStep01(p * 1.6f));
            }
            float r = MathHelper.Clamp((elapsed - d.Windup - d.Active) / d.Recover, 0f, 1f);
            return MathHelper.Lerp(0.09f, 0f, SmoothStep01(r));
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (mainVec * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full
                , mainVec.ToRotation() - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + mainVec * 0.5f;
            Projectile.rotation = mainVec.ToRotation();
            Projectile.timeLeft = 90;

            //身体不当木桩,仅终结拍小幅倾身,平滑过渡防跨拍跳变
            bodyLean = MathHelper.Lerp(bodyLean, BodyLeanTarget() * lockedDirection, 0.3f);
            if (MathF.Abs(bodyLean) > 0.002f) {
                Owner.fullRotation = bodyLean;
                Owner.fullRotationOrigin = Owner.Size / 2f;
                appliedLean = true;
            }
            else if (appliedLean) {
                Owner.fullRotation = 0f;
                appliedLean = false;
            }
        }

        public override void OnKill(int timeLeft) {
            if (appliedLean) {
                Owner.fullRotation = 0f;
            }
        }

        private void PushGhost(Vector2 vec) {
            if (vec == Vector2.Zero) {
                return;
            }
            for (int i = Math.Min(ghostCount, GhostMax - 1); i > 0; i--) {
                ghostVecs[i] = ghostVecs[i - 1];
            }
            ghostVecs[0] = vec;
            if (ghostCount < GhostMax) {
                ghostCount++;
            }
        }

        //==== 判定 ====

        public override bool? CanDamage() => dmgTo > dmgFrom ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (dmgTo <= dmgFrom) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            //本帧扫过的区间逐步打线段,防高速漏判
            Vector2 tipA = MotionAt(beatIndex, dmgFrom, out _);
            Vector2 tipB = MotionAt(beatIndex, dmgTo, out _);
            int steps = Math.Clamp((int)((tipB - tipA).Length() / 26f) + 1, 1, 48);
            for (int i = 0; i <= steps; i++) {
                float t = MathHelper.Lerp(dmgFrom, dmgTo, i / (float)steps);
                Vector2 tip = hand + MotionAt(beatIndex, t, out _) * 1.05f;
                float point = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , hand, tip, 34f, ref point)) {
                    return true;
                }
            }
            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = mainVec.X > 0 ? 1 : -1;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.5f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //转发物品命中钩子(饰品吸血等)
            ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
            NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
            PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);

            target.AddBuff(BuffID.OnFire3, 360);
            target.AddBuff(BuffID.Daybreak, 300);

            //命中顿帧,终结拍更重
            hitstopTimer = Math.Max(hitstopTimer, IsFinisher(beatIndex) ? 4 : 2);
            flashPulse = 1f;

            if (!VaultUtils.isServer) {
                bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
                SoundEngine.PlaySound((steel ? SoundID.NPCHit4 : SoundID.NPCHit1) with {
                    Pitch = steel ? 0.05f : -0.15f, Volume = 0.7f
                }, target.Center);

                //命中余烬迸发,钢面更快更亮更散
                Vector2 unit = mainVec.SafeNormalize(Vector2.UnitX);
                int burst = IsFinisher(beatIndex) ? 10 : 6;
                for (int i = 0; i < burst; i++) {
                    Vector2 vel = unit.RotatedByRandom(steel ? 0.9f : 0.55f)
                        * Main.rand.NextFloat(steel ? 6f : 4f, steel ? 13f : 9f);
                    PRTLoader.NewParticle<PRT_DawnEmber>(target.Center + Main.rand.NextVector2Circular(10f, 10f)
                        , vel, default, Main.rand.NextFloat(0.9f, 1.5f)).Configure(Main.rand.Next(16, 26));
                }
            }

            if (IsFinisher(beatIndex) && CWRServerConfig.Instance.ScreenVibration) {
                var modifier = new PunchCameraModifier(target.Center
                    , mainVec.ToRotation().ToRotationVector2(), 5f, 6f, 10, 800f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
            }
        }

        //==== 绘制(P0 贴图+残影级) ====

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Rectangle rect = tex.GetRectangle(Projectile.frame, FrameCount);
            Vector2 origin = rect.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float rot = mainVec.ToRotation();
            float drawRot = rot + (lockedDirection > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 * 3f);
            SpriteEffects effects = lockedDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //伪 z 驱动枪身轴向伸缩,最强的立体线索
            float k = ViewZ / MathF.Max(ViewZ - depthZ * 0.8f, 220f);
            float scale = 0.7f * MathHelper.Clamp(k, 0.72f, 1.18f);

            //挥砍残影
            BeatDef d = GetBeat(beatIndex);
            bool inActive = elapsed > d.Windup && elapsed < d.Windup + d.Active;
            if (inActive && ghostCount > 0) {
                for (int i = 0; i < ghostCount; i++) {
                    float fade = 0.34f * (1f - (i + 1) / (float)(GhostMax + 1));
                    Vector2 gv = ghostVecs[i];
                    float gRot = gv.ToRotation();
                    float gDrawRot = gRot + (lockedDirection > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 * 3f);
                    Color ghostColor = new Color(255, 176, 64) * fade;
                    ghostColor.A = 0;
                    Main.EntitySpriteDraw(tex, hand + gv * 0.5f - Main.screenPosition, rect, ghostColor
                        , gDrawRot, origin, scale, effects, 0);
                }
            }

            Main.EntitySpriteDraw(tex, hand + mainVec * 0.5f - Main.screenPosition, rect
                , Projectile.GetAlpha(lightColor), drawRot, origin, scale, effects, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            BeatDef d = GetBeat(beatIndex);
            if (trailFade <= 0.02f) {
                return;
            }
            stripSink.Clear();
            Vector2 hand = Owner.GetPlayerStabilityCenter();

            //弧光:采样环生成三股蛋形弧带,z 远近分层
            if (d.Kind == 1) {
                if (arcSamples.Count < 3) {
                    return;
                }
                DawnshatterRenderer.CollectArcStrips(stripSink, hand, arcSamples, 0.34f, heat, trailFade);
                DawnshatterRenderer.DrawStrips(true, trailFade, heat, flashPulse, stripSink);
                return;
            }

            //刺击:火线驻留在刺穿的位置,收势期原地熄灭而非随枪回缩
            if (maxTip <= pulseRear + 14f) {
                return;
            }
            Vector2 unit = aimAngle.ToRotationVector2();
            float halfWidth = 24f + heat * 8f;
            DawnshatterRenderer.CollectThrustStrips(stripSink, hand, unit
                , pulseRear, maxTip + 14f, halfWidth, heat, trailFade);
            DawnshatterRenderer.DrawStrips(false, trailFade, heat, flashPulse, stripSink);
        }
    }
}
