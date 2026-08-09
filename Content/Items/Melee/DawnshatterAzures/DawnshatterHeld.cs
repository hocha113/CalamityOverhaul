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
        /// 贯穿突刺首拍步长(px/tick)与逐拍衰减,十三拍合计约 800px
        private const float LungeSpeed = 107f;
        private const float LungeDecay = 0.90f;

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
        /// 上一拍收笔姿态,前摇期从它插值过来,拍间无瞬移
        private Vector2 handoffVec;
        /// B2 突进起点(世界坐标),火线锚定在轨迹上
        private Vector2 lungeAnchor;
        /// B2 撞墙截断,本帧结算后跳到收势
        private bool lungeWallCut;

        //==== 残影环 ====
        private const int GhostMax = 7;
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

        //==== 弧带采样,绘制时按弧长解析重建,头在 index0 ====
        private const int ArcSampleMax = 160;
        /// 采样间距(px),角速度再快也按这个密度补点
        private const float ArcSampleSpacing = 16f;
        private readonly List<DawnshatterRenderer.ArcSample> arcSamples = [];
        /// 本拍弧带弧长(px),shader 按像素尺度采样噪声用
        private float arcStrokeLen = 600f;
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
            //B1 升龙撩,倾角在挥动中扫过(轴向前缩线索),随撩升空
            1 => new BeatDef {
                Kind = 1, Windup = 8f, Active = 13f, Recover = 11f, DamageMul = 1.05f,
                Radius0 = 250f, Radius1 = 280f, ArcStart = 1.55f, ArcEnd = -1.60f,
                Tilt0 = 1.0f, Tilt1 = 0.3f, Roll = 0.15f,
            },
            //B2 贯穿突刺,位置步进 ~800px,人枪弹性
            2 => new BeatDef {
                Kind = 2, Windup = 6f, Active = 13f, Recover = 8f, DamageMul = 1.1f,
            },
            //B3 横扫回旋,贯穿平面,收-爆:缓推三成后一口气抽完,爆发帧小跳
            3 => new BeatDef {
                Kind = 1, Windup = 8f, Active = 16f, Recover = 8f, DamageMul = 1.15f,
                Radius0 = 270f, Radius1 = 300f, ArcStart = -2.3f, ArcEnd = 2.3f,
                Tilt0 = 0.55f, Tilt1 = 0.55f, Roll = -0.2f,
            },
            //B4 日冕终结,悬空加速回旋1.5圈,半径长出
            _ => new BeatDef {
                Kind = 1, Windup = 10f, Active = 17f, Recover = 12f, DamageMul = 1.6f,
                Radius0 = 290f, Radius1 = 340f, ArcStart = -0.6f, ArcEnd = -0.6f + MathHelper.Pi * 3f,
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

        /// <summary>
        /// 收-爆-停挥砍进度:近静止蓄势 → 2~3 帧前置爆发并过冲 → 冻结静止谷 → 末段回坐<br/>
        /// 爆发段用 1−(1−x)³ 而非 SmoothStep,出生即全速;静止谷是衬托爆发的那段"真的不动"
        /// </summary>
        private static float SwingCurve(float t, float creepEnd, float burstEnd, float holdEnd
            , float creepAmt, float overshoot) {
            t = MathHelper.Clamp(t, 0f, 1f);
            if (t < creepEnd) {
                return creepAmt * SmoothStep01(t / creepEnd);
            }
            if (t < burstEnd) {
                float b = (t - creepEnd) / (burstEnd - creepEnd);
                return MathHelper.Lerp(creepAmt, 1f + overshoot, 1f - MathF.Pow(1f - b, 3f));
            }
            if (t < holdEnd) {
                return 1f + overshoot;
            }
            return MathHelper.Lerp(1f + overshoot, 1f, SmoothStep01((t - holdEnd) / (1f - holdEnd)));
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
                    //蓄势 5.5t → 爆发 2.6t 抽完 → 静止谷 2.3t → 回坐
                    1 => SwingCurve(activeT, 0.42f, 0.62f, 0.80f, 0.05f, 0.10f),
                    //蓄势 6.4t → 爆发 3.2t → 静止谷 2.9t(速度是对比,不是连续加速)
                    3 => SwingCurve(activeT, 0.40f, 0.60f, 0.78f, 0.08f, 0.09f),
                    //B4 越转越快的回旋,末段最疾
                    _ => MathF.Pow(activeT, 2.2f),
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
                    return MathHelper.Lerp(RestTip - 26f, 305f, EaseOutExpo((t - d.Windup) / d.Active));
                }
                return MathHelper.Lerp(305f, RestTip, SmoothStep01((t - d.Windup - d.Active) / d.Recover));
            }
            //B2 贯穿突刺:收枪→爆伸并随突进继续前探(人枪弹性)→缓收
            if (t < d.Windup) {
                return MathHelper.Lerp(RestTip, 130f, EaseOutCubic(t / d.Windup));
            }
            if (t < d.Windup + d.Active) {
                return MathHelper.Lerp(130f, 305f, EaseOutExpo((t - d.Windup) / d.Active));
            }
            return MathHelper.Lerp(305f, RestTip, SmoothStep01((t - d.Windup - d.Active) / d.Recover));
        }

        /// <summary>拍的伤害窗(拍内时刻)</summary>
        private static void GetDamageWindows(int beat, in BeatDef d, out Vector2 w1) {
            switch (beat) {
                case 0:
                case 2:
                    w1 = new Vector2(d.Windup, d.Windup + d.Active);
                    break;
                default:
                    //扫击判定对齐爆发+静止谷,不在近静止的蓄势段出伤(命中要与看得见的爆发同步)
                    w1 = new Vector2(d.Windup + d.Active * 0.30f, d.Windup + d.Active * 0.90f);
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
            //先取向再重置,首拍 handoffVec 才有正确的瞄准角可用
            CaptureAim();
            ResetBeatPresentation(index);

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            Projectile.damage = (int)(baseDamage * GetBeat(index).DamageMul);
            //一拍一敌一伤,拍首清免疫表
            ResetLocalImmunity();
            if (!first) {
                Projectile.netUpdate = true;
            }
        }

        /// <summary>拍首演出状态重置,本地拍切换与远端收包共用,远端不得跨拍残留弧带</summary>
        private void ResetBeatPresentation(int index) {
            retractTimer = 0f;
            burstSoundPlayed = false;
            lungeWallCut = false;
            aimFrozen = false;
            ghostCount = 0;
            spinWhooshStage = 0;

            //拍间交接:记住上一拍收笔姿态
            handoffVec = mainVec == Vector2.Zero
                ? aimAngle.ToRotationVector2() * RestTip : mainVec;
            lungeAnchor = Owner.GetPlayerStabilityCenter();

            //拍首清尾+火焰状态回落
            trailFade = 0f;
            heat *= 0.3f;
            pulseRear = index == 0 ? RestTip - 26f : 130f;
            maxTip = pulseRear;
            arcSamples.Clear();
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

            Vector2 prevVec = mainVec;
            mainVec = MotionAt(beatIndex, to, out depthZ);
            //拍间交接:前摇期从上一拍收笔姿态插值过来,姿态连续无瞬移
            if (to < d.Windup) {
                mainVec = Vector2.Lerp(handoffVec, mainVec, EaseOutCubic(to / d.Windup));
            }
            PushGhostTrail(prevVec, mainVec);

            ApplyDisplacement(in d, from, to);
            UpdateFireState(in d, to);
            SpawnBladeFire(in d);

            UpdatePlayerPose();
            Lighting.AddLight(Owner.GetPlayerStabilityCenter() + mainVec * 0.75f
                , new Vector3(1.15f, 0.72f, 0.28f) * 0.7f);

            elapsed = to;
            //撞墙截断:突进被墙终止,跳到收势
            if (lungeWallCut) {
                lungeWallCut = false;
                elapsed = MathF.Max(elapsed, d.Windup + d.Active);
            }

            //取消窗:续拍锁存吃掉七成收势,连段不等收势播完;无锁存才走完整收势+退场
            float cancelAt = d.Windup + d.Active + d.Recover * 0.3f;
            if (queuedNext && elapsed >= cancelAt) {
                StartBeat((beatIndex + 1) % BeatCount);
            }
        }

        /// <summary>位移连招,owner 端驱动运动,远端由玩家位置同步重放</summary>
        private void ApplyDisplacement(in BeatDef d, float from, float to) {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            bool inActive = to >= d.Windup && from < d.Windup + d.Active;
            switch (beatIndex) {
                case 1:
                    //升龙撩:随撩升空
                    if (inActive) {
                        Owner.velocity.Y = -6.5f * Owner.gravDir;
                    }
                    break;
                case 2:
                    LungeStep(in d, from, to);
                    break;
                case 4:
                    //悬空回旋:慢坠驻空
                    if (inActive) {
                        Owner.velocity.Y *= 0.4f;
                    }
                    break;
            }
        }

        /// <summary>
        /// 贯穿突刺位置步进:指数缓出,16px 子步防穿墙,无橡皮筋回传<br/>
        /// 阻挡只看沿冲刺向的推进量——地面托住竖直分量不算撞墙(斜向下突刺曾被误判秒停)
        /// </summary>
        private void LungeStep(in BeatDef d, float from, float to) {
            float u0 = MathF.Max(from - d.Windup, 0f);
            float u1 = MathF.Min(to - d.Windup, d.Active);
            if (u1 <= u0) {
                return;
            }
            float stepLen = LungeSpeed * MathF.Pow(LungeDecay, u0) * (u1 - u0);
            Vector2 dir = aimAngle.ToRotationVector2();
            int subs = Math.Clamp((int)MathF.Ceiling(stepLen / 16f), 1, 16);
            float sub = stepLen / subs;
            for (int i = 0; i < subs; i++) {
                Vector2 allowed = Collision.TileCollision(Owner.position, dir * sub
                    , Owner.width, Owner.height, true, false, (int)Owner.gravDir);
                float gain = Vector2.Dot(allowed, dir);
                if (gain <= 0.2f) {
                    lungeWallCut = true;
                    flashPulse = MathF.Max(flashPulse, 0.6f);
                    break;
                }
                //沿冲刺线推进,不吃地面滑移的横向漂移
                Owner.position += dir * gain;
            }
            Owner.velocity = Vector2.Zero;
            Owner.fallStart = (int)(Owner.position.Y / 16f);
            Owner.GivePlayerImmuneState(4, false);

            //末端残滑,干脆刹停后交还操控
            if (u1 >= d.Active) {
                Owner.velocity = dir * 4f;
            }
        }

        /// <summary>本帧区间与各窗口求交集消费:伤害窗/爆发音/位移冲量事件,高攻速跨阶段不漏</summary>
        private void ConsumeWindows(in BeatDef d, float from, float to) {
            GetDamageWindows(beatIndex, in d, out Vector2 w1);

            dmgFrom = MathF.Max(from, w1.X);
            dmgTo = MathF.Min(to, w1.Y);

            //爆发起点:音效+点燃脉冲+一次性位移冲量,每拍一次"破晓"是离散事件不是渐变
            if (!burstSoundPlayed && to >= d.Windup) {
                burstSoundPlayed = true;
                flashPulse = MathF.Max(flashPulse, 0.65f);
                PlaySwingSound(second: false);

                if (beatIndex == 2) {
                    //突进起点定桩,火线从这里长出
                    lungeAnchor = Owner.GetPlayerStabilityCenter();
                }
                if (Projectile.IsOwnedByLocalPlayer()) {
                    if (beatIndex == 0) {
                        //起手滑步
                        Owner.velocity += aimAngle.ToRotationVector2() * 6f;
                    }
                    else if (beatIndex == 3) {
                        //爆发帧小跳,可空中接
                        Owner.velocity.Y = -3.5f * Owner.gravDir;
                    }
                }
                if (!VaultUtils.isServer && beatIndex == 1) {
                    //踏火起跳,余烬向下喷
                    for (int i = 0; i < 8; i++) {
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-2.5f, 2.5f)
                            , Main.rand.NextFloat(3f, 7f) * Owner.gravDir);
                        PRTLoader.NewParticle<PRT_DawnEmber>(Owner.Bottom + Main.rand.NextVector2Circular(10f, 4f)
                            , vel, default, Main.rand.NextFloat(0.9f, 1.3f)).Configure(Main.rand.Next(14, 22));
                    }
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

            //终结拍收尾爆点:过曝脉冲+余烬环,空中收招附带下坠砸势,震屏仅施术者本地
            if (IsFinisher(beatIndex) && !endPopFired && to >= d.Windup + d.Active) {
                endPopFired = true;
                flashPulse = 1f;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Owner.CWR().ScreenShakeValue = 6f;
                    Owner.velocity.Y = 6f * Owner.gravDir;
                    Owner.noFallDmg = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.35f }, Owner.Center);
                    Vector2 hand = Owner.GetPlayerStabilityCenter();
                    for (int i = 0; i < 14; i++) {
                        float ang = MathHelper.TwoPi * i / 14f + Main.rand.NextFloat(0.2f);
                        Vector2 dir = ang.ToRotationVector2();
                        PRTLoader.NewParticle<PRT_DawnEmber>(hand + dir * Main.rand.NextFloat(150f, 250f)
                            , dir * Main.rand.NextFloat(5f, 10f), default, Main.rand.NextFloat(1f, 1.6f))
                            .Configure(Main.rand.Next(20, 32));
                    }
                }
            }
        }

        /// <summary>
        /// 弧带绘制时按弧长解析重建:MotionAt 是纯函数,几何从函数推导而不是从帧历史累积<br/>
        /// 角速度再快也按 16px 补点,不会退化成多边形(帧历史式采样在爆发段必然稀疏)
        /// </summary>
        private void BuildArcSamples(in BeatDef d) {
            arcSamples.Clear();
            float headT = MathF.Min(elapsed, d.Windup + d.Active);
            float tailT = d.Windup;
            if (headT - tailT < 0.05f) {
                return;
            }

            //粗测弧长定采样密度
            const int probe = 12;
            float arcLen = 0f;
            Vector2 prev = MotionAt(beatIndex, tailT, out _);
            for (int i = 1; i <= probe; i++) {
                Vector2 p = MotionAt(beatIndex, MathHelper.Lerp(tailT, headT, i / (float)probe), out _);
                arcLen += (p - prev).Length();
                prev = p;
            }

            int count = Math.Clamp((int)MathF.Ceiling(arcLen / ArcSampleSpacing), 8, ArcSampleMax);
            arcStrokeLen = MathF.Max(arcLen, 60f);
            float headHeat = MathF.Max(heat, 0.3f);
            for (int i = 0; i < count; i++) {
                float u = i / (count - 1f);
                Vector2 tip = MotionAt(beatIndex, MathHelper.Lerp(headT, tailT, u), out float z);
                arcSamples.Add(new DawnshatterRenderer.ArcSample {
                    Tip = tip,
                    Z = 0.5f + MathHelper.Clamp(z / 220f, -0.5f, 0.5f),
                    //头热尾冷,沿笔画的温度梯度
                    Heat = MathHelper.Lerp(headHeat, 0.3f, u),
                });
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
            }
        }

        /// <summary>位移拍的粒子承载速度,火花不许被留在原地;B2 用确定性步长公式,各端一致</summary>
        private Vector2 CarrierVelocity(in BeatDef d) {
            if (beatIndex == 2) {
                float u = MathHelper.Clamp(elapsed - d.Windup, 0f, d.Active);
                return aimAngle.ToRotationVector2() * (LungeSpeed * MathF.Pow(LungeDecay, u)) * 0.4f;
            }
            return Owner.velocity * 0.4f;
        }

        /// <summary>刃上火:判定窗内余烬喷流+刃缘火舌,密度按攻速补偿;位移拍密度×2初速×1.5并继承载体速度</summary>
        private void SpawnBladeFire(in BeatDef d) {
            if (VaultUtils.isServer || dmgTo <= dmgFrom) {
                return;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            bool moveBeat = beatIndex is 1 or 2 or 4;
            float spdMul = moveBeat ? 1.5f : 1f;
            Vector2 carry = moveBeat ? CarrierVelocity(in d) : Vector2.Zero;

            int times = (int)speedMul;
            if (Main.rand.NextFloat() < speedMul % 1f) {
                times++;
            }
            if (moveBeat) {
                times *= 2;
            }

            if (d.Kind == 1) {
                //扫击:余烬沿切向甩出(离心),火舌径向外舔
                Vector2 tipUnit = mainVec.SafeNormalize(Vector2.UnitX);
                float spin = MathF.Sign(d.ArcEnd - d.ArcStart) * lockedDirection;
                Vector2 tangent = tipUnit.RotatedBy(MathHelper.PiOver2) * spin;
                for (int i = 0; i < times; i++) {
                    Vector2 pos = hand + mainVec * Main.rand.NextFloat(0.72f, 1.02f);
                    Vector2 vel = tangent * (Main.rand.NextFloat(3.5f, 8f) * spdMul)
                        + tipUnit * Main.rand.NextFloat(0.5f, 2.5f) + carry;
                    PRTLoader.NewParticle<PRT_DawnEmber>(pos, vel, default, Main.rand.NextFloat(0.9f, 1.4f))
                        .Configure(Main.rand.Next(18, 30));

                    if (Main.rand.NextBool(2)) {
                        Vector2 outward = (tipUnit - tangent * 0.4f).SafeNormalize(Vector2.UnitY);
                        PRTLoader.NewParticle<PRT_DawnTongue>(hand + mainVec * Main.rand.NextFloat(0.5f, 0.95f)
                            , tangent * 2f + carry, default, Main.rand.NextFloat(0.6f, 1f))
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
                Vector2 vel = unit * (Main.rand.NextFloat(4f, 9f) * spdMul)
                    + perp * Main.rand.NextFloat(-1.6f, 1.6f) + carry;
                PRTLoader.NewParticle<PRT_DawnEmber>(pos, vel, default, Main.rand.NextFloat(0.9f, 1.4f))
                    .Configure(Main.rand.Next(18, 30));

                if (Main.rand.NextBool(2)) {
                    float along = Main.rand.NextFloat(0.45f, 0.9f);
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 outward = (perp * side - unit * 0.35f).SafeNormalize(Vector2.UnitY);
                    PRTLoader.NewParticle<PRT_DawnTongue>(hand + unit * (tip * along), unit * 2f + carry
                        , default, Main.rand.NextFloat(0.6f, 1f))
                        .Configure(outward, Main.rand.NextFloat(0.55f, 0.95f), Main.rand.Next(3, 6));
                }
            }

            //B2 突进沿途向后甩高速拉丝余烬
            if (beatIndex == 2) {
                for (int i = 0; i < 3; i++) {
                    Vector2 pos = Owner.Center - unit * Main.rand.NextFloat(0f, 50f)
                        + Main.rand.NextVector2Circular(14f, 14f);
                    Vector2 vel = carry + (-unit).RotatedByRandom(0.4f) * Main.rand.NextFloat(3f, 8f);
                    PRTLoader.NewParticle<PRT_DawnEmber>(pos, vel, default, Main.rand.NextFloat(1f, 1.5f))
                        .Configure(Main.rand.Next(14, 24));
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

        /// <summary>位移拍身体语言:B1 后仰 B2 前倾 B4 随转摆动,钟形两端归零</summary>
        private float BodyLeanTarget() {
            BeatDef d = GetBeat(beatIndex);
            float activeT = MathHelper.Clamp((elapsed - d.Windup) / d.Active, 0f, 1f);
            float bell = MathF.Sin(activeT * MathHelper.Pi);
            float lean = beatIndex switch {
                1 => -0.18f * bell,
                2 => 0.35f * bell,
                4 => 0.12f * MathF.Sin(activeT * MathHelper.TwoPi),
                _ => 0f,
            };
            return lean * lockedDirection * Owner.gravDir;
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
            Owner.noFallDmg = true;

            //全身编舞四件套:脚底为轴,腿反向补偿,读作"人在发力"而非贴图自转
            bodyLean = MathHelper.Lerp(bodyLean, BodyLeanTarget(), 0.3f);
            if (MathF.Abs(bodyLean) > 0.002f) {
                Owner.fullRotation = bodyLean;
                Owner.fullRotationOrigin = new Vector2(Owner.Hitbox.Width / 2f
                    , Owner.gravDir == -1f ? 0f : Owner.Hitbox.Height);
                Owner.legRotation = -bodyLean;
                Owner.legPosition = (new Vector2(Owner.Hitbox.Width / 2f, Owner.Hitbox.Height)
                    - Owner.fullRotationOrigin).RotatedBy(-bodyLean);
                appliedLean = true;
            }
            else if (appliedLean) {
                ClearBodyLean();
            }
        }

        private void ClearBodyLean() {
            Owner.fullRotation = 0f;
            Owner.legRotation = 0f;
            Owner.legPosition = Vector2.Zero;
            appliedLean = false;
        }

        public override void OnKill(int timeLeft) {
            if (appliedLean) {
                ClearBodyLean();
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

        /// <summary>枪尖走得越远补越多子帧残影,爆发那两三帧才有连成一片的路径拖影</summary>
        private void PushGhostTrail(Vector2 prev, Vector2 cur) {
            if (prev == Vector2.Zero) {
                PushGhost(cur);
                return;
            }
            int n = Math.Clamp((int)((cur - prev).Length() / 38f), 1, 4);
            for (int i = 1; i <= n; i++) {
                PushGhost(Vector2.Lerp(prev, cur, i / (n + 1f)));
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
            Vector2 hand = Owner.GetPlayerStabilityCenter();

            //伪 z 只做 ±12% 的轴向呼吸,立体线索留住、橡皮感去掉
            float depthScale = ViewZ / MathF.Max(ViewZ - depthZ * 0.8f, 220f);

            //残影强度随枪尖位移,快才拖得出影——速度感是对比不是常驻虚影
            BeatDef d = GetBeat(beatIndex);
            bool inActive = elapsed > d.Windup && elapsed < d.Windup + d.Active + d.Recover * 0.25f;
            if (inActive && ghostCount > 1) {
                float smear = MathHelper.Clamp(((mainVec - ghostVecs[0]).Length() - 6f) / 46f, 0f, 1f);
                if (smear > 0.02f) {
                    for (int i = 0; i < ghostCount; i++) {
                        float fade = 0.52f * smear * (1f - (i + 1) / (float)(GhostMax + 1));
                        Color ghostColor = new Color(255, 176, 64) * fade;
                        ghostColor.A = 0;
                        DawnshatterRenderer.DrawSpearQuad(tex, rect, hand, ghostVecs[i]
                            , lockedDirection, ghostColor, depthScale);
                    }
                }
            }

            DawnshatterRenderer.DrawSpearQuad(tex, rect, hand, mainVec, lockedDirection
                , Projectile.GetAlpha(lightColor), depthScale);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            BeatDef d = GetBeat(beatIndex);
            if (trailFade <= 0.02f) {
                return;
            }
            stripSink.Clear();
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            float halfWidth = 32f + heat * 10f;
            Vector2 unit = aimAngle.ToRotationVector2();

            //弧光:按弧长解析重采样,三股蛋形弧带,z 远近分层
            if (d.Kind == 1) {
                BuildArcSamples(in d);
                if (arcSamples.Count < 3) {
                    return;
                }
                DawnshatterRenderer.CollectArcStrips(stripSink, hand, arcSamples, 0.30f, heat, trailFade);
                DawnshatterRenderer.DrawStrips(true, trailFade, heat, flashPulse, arcStrokeLen, stripSink);
                return;
            }

            //B2 贯穿突刺:火线世界锚定在突进轨迹上,从起点长到当前枪尖
            if (beatIndex == 2) {
                if (!burstSoundPlayed) {
                    return;
                }
                float tipDist = Vector2.Distance(lungeAnchor, hand) + mainVec.Length();
                if (tipDist < 90f) {
                    return;
                }
                DawnshatterRenderer.CollectThrustStrips(stripSink, lungeAnchor, unit
                    , 0f, tipDist + 14f, halfWidth, heat, trailFade);
                //刺击条带本就在噪声原作刻度附近,传中性 600 保持既有观感
                DawnshatterRenderer.DrawStrips(false, trailFade, heat, flashPulse, 600f, stripSink);
                return;
            }

            //B0 直刺:火线驻留在刺穿的位置,收势期原地熄灭而非随枪回缩
            if (maxTip <= pulseRear + 14f) {
                return;
            }
            DawnshatterRenderer.CollectThrustStrips(stripSink, hand, unit
                , pulseRear, maxTip + 14f, halfWidth, heat, trailFade);
            DawnshatterRenderer.DrawStrips(false, trailFade, heat, flashPulse, 600f, stripSink);
        }
    }
}
