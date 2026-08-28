using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics
{
    /// <summary>运动模式</summary>
    internal enum ShrimpMoveMode
    {
        /// <summary>凝视逼近：头恒对目标，环距弹簧进退（NightmareReaper 式分镜）</summary>
        Stalk,
        /// <summary>直线游动（离场/回位等直达移动）</summary>
        Swim,
        /// <summary>弹道段（尾弹/击飞），状态一帧点火后自治</summary>
        Ballistic,
        /// <summary>剧本段（入场/蜕壳/死亡演出）：速度完全由状态脚本掌管</summary>
        Scripted,
    }

    /// <summary>弹道段身轴跟随方式：身轴不对齐冲刺线会读作"侧身滑行"</summary>
    internal enum BallisticHeading
    {
        /// <summary>冻结（短促位移/竖直上跳）</summary>
        Frozen,
        /// <summary>头先行：身轴贴向速度方向（犁浪冲锋）</summary>
        HeadFirst,
        /// <summary>尾先行：身轴贴向速度反方向（虾式后向弹射）</summary>
        TailFirst,
    }

    /// <summary>
    /// 运动执行层（NightmareReaper 动作语言）：boss 悬浮于开阔空间，头永远以恒速转向目标；
    /// 逼近走环距弹簧——远了爬近、入环停住漂移、过远或贴脸再动（同一弹簧自然进退）。
    /// 身体从不贴地形（noTileCollide），双螯的"抓地感"由骨架的空间抓握系统承担。
    /// 全部消费确定性输入，各端结果一致
    /// </summary>
    internal class ShrimpLocomotion
    {
        private NPC npc;

        public ShrimpMoveMode Mode { get; private set; } = ShrimpMoveMode.Stalk;
        /// <summary>头前向角（恒速转向，骨架直接消费）</summary>
        public float Heading { get; private set; }
        /// <summary>本帧行进向量（步态划桨参考）</summary>
        public Vector2 TangentMove { get; private set; } = Vector2.UnitX;
        /// <summary>名义上方向（保留给个别姿态偏置消费）</summary>
        public Vector2 SurfaceNormal => -Vector2.UnitY;
        /// <summary>身体当前是否浸水</summary>
        public bool Wet => npc != null && ShrimpTerrain.WetAt(npc.Center);

        //意图（每帧由状态写入，Update 消费后复位）
        private bool hasIntent;
        private ShrimpMoveMode wantMode;
        private Vector2 wantPoint;
        private float wantSpeedScale;
        private float wantHoldDist;
        private bool holdIntent;

        //环距弹簧滞回：在环内漂移，出环再动
        private bool stalking = true;

        //驻停转身意图（RequestHoldFacing 写，UpdateStalk 消费）
        private bool faceIntent;
        private float faceHeading;
        private float faceRate;

        //弹道段
        private int ballisticFrames;
        private float ballisticBrake;
        private bool braking;
        private BallisticHeading ballisticHeading;

        public void Bind(NPC target) => npc = target;

        /// <summary>凝视逼近目标点：holdDistance 为驻停环距（0=直达）</summary>
        public void RequestCrawlTo(Vector2 worldPoint, float speedScale = 1f,
            float holdDistance = SeaShrimpDirector.StalkHoldDistance) {
            hasIntent = true;
            holdIntent = false;
            wantMode = ShrimpMoveMode.Stalk;
            wantPoint = worldPoint;
            wantSpeedScale = speedScale;
            wantHoldDist = holdDistance;
        }

        /// <summary>原地驻停漂移（蓄力/齐射用；不转头，姿态由状态自持）</summary>
        public void RequestHold() {
            hasIntent = true;
            holdIntent = true;
            wantMode = ShrimpMoveMode.Stalk;
            faceIntent = false;
        }

        /// <summary>
        /// 驻停并转身对线（冲刺/水炮蓄力用）：漂移泄速的同时身轴以给定速率贴向目标角。
        /// 转率由调用方随蓄力进度衰减——越接近锁定转得越慢，锁线读得出来
        /// </summary>
        public void RequestHoldFacing(float heading, float turnRate) {
            hasIntent = true;
            holdIntent = true;
            wantMode = ShrimpMoveMode.Stalk;
            faceIntent = true;
            faceHeading = heading;
            faceRate = turnRate;
        }

        /// <summary>直线游向锚点（离场等直达移动）</summary>
        public void RequestSwim(Vector2 anchor, float speedScale = 1f) {
            hasIntent = true;
            holdIntent = false;
            wantMode = ShrimpMoveMode.Swim;
            wantPoint = anchor;
            wantSpeedScale = speedScale;
        }

        /// <summary>剧本模式：本层不碰速度，演出状态自行脚本化位移与朝向</summary>
        public void RequestScripted() {
            hasIntent = true;
            holdIntent = false;
            wantMode = ShrimpMoveMode.Scripted;
        }

        /// <summary>剧本段直写朝向（演出摆位）</summary>
        public void ScriptHeading(float heading, float rate = 0.15f)
            => Heading = Heading.AngleLerp(heading, rate);

        /// <summary>
        /// 点火弹道段（尾弹/冲锋）：一帧定初速，flight 帧内不衰减，此后按 brake 硬刹。
        /// headingFollow 决定身轴是否贴合速度轴——不对齐的冲刺读作侧身滑行（设计缺陷修复 2026-08）
        /// </summary>
        public void LaunchBallistic(Vector2 velocity, int flightFrames, float brake,
            BallisticHeading headingFollow = BallisticHeading.Frozen) {
            npc.velocity = velocity;
            ballisticFrames = flightFrames;
            ballisticBrake = brake;
            braking = false;
            ballisticHeading = headingFollow;
            Mode = ShrimpMoveMode.Ballistic;
        }

        /// <summary>弹道段是否结束（刹停）</summary>
        public bool BallisticDone => Mode != ShrimpMoveMode.Ballistic;

        /// <summary>入场/重建时硬置朝向</summary>
        public void SnapHeading(float heading) => Heading = heading;

        /// <summary>每帧执行（主体 AI 在状态机之后调用）</summary>
        public void Update() {
            if (Mode == ShrimpMoveMode.Ballistic) {
                UpdateBallistic();
                hasIntent = false;
                return;
            }

            if (!hasIntent) {
                holdIntent = true;
                wantMode = ShrimpMoveMode.Stalk;
            }
            hasIntent = false;

            Mode = wantMode;
            switch (Mode) {
                case ShrimpMoveMode.Scripted:
                    TangentMove = Heading.ToRotationVector2();
                    return;
                case ShrimpMoveMode.Swim:
                    UpdateSwim();
                    return;
                default:
                    UpdateStalk();
                    return;
            }
        }

        private void UpdateBallistic() {
            //身轴贴向速度轴：头先行贴正向，尾先行贴反向;刹停低速段不再纠(避免尾速抖动带歪)
            if (ballisticHeading != BallisticHeading.Frozen && npc.velocity.Length() > 3.5f) {
                float axis = npc.velocity.ToRotation()
                    + (ballisticHeading == BallisticHeading.TailFirst ? MathHelper.Pi : 0f);
                Heading = Heading.AngleLerp(axis, 0.22f);
                TangentMove = Heading.ToRotationVector2();
            }
            if (ballisticFrames > 0) {
                ballisticFrames--;
                //弹道中段微加速：爆发不衰减（Old Duke 冲刺文法）
                npc.velocity *= 1.008f;
                if (ballisticFrames == 0) {
                    braking = true;
                }
                return;
            }
            if (braking) {
                npc.velocity *= ballisticBrake;
                if (npc.velocity.Length() < 3f) {
                    braking = false;
                    Mode = ShrimpMoveMode.Stalk;
                    stalking = true;
                }
            }
        }

        /// <summary>
        /// 凝视逼近：头以恒速转向目标；环距弹簧 (d-hold) 决定进退加速度，
        /// 入环停住漂移、出环（过远/贴脸）再动——同一条公式自然完成贴近与后撤
        /// </summary>
        private void UpdateStalk() {
            if (holdIntent) {
                //驻停漂移：泄速；有转身意图则身轴贴向目标角（冲刺蓄力的锁线动作）
                npc.velocity *= 0.95f;
                if (faceIntent) {
                    Heading = Heading.AngleTowards(faceHeading, faceRate);
                    faceIntent = false;
                }
                TangentMove = Heading.ToRotationVector2();
                return;
            }

            Vector2 to = wantPoint - npc.Center;
            float d = to.Length();
            Vector2 dir = to.SafeNormalize(Vector2.UnitX);

            //恒速转头：蓄意而不慌乱
            Heading = Heading.AngleTowards(dir.ToRotation(), SeaShrimpDirector.StalkTurnRate);

            float hold = wantHoldDist;
            if (hold <= 1f) {
                //直达：无环，一路进逼
                npc.velocity = npc.velocity * 0.95f + dir * (d / 20f * 0.05f * wantSpeedScale);
            }
            else if (stalking) {
                npc.velocity = npc.velocity * 0.95f + dir * ((d - hold) / 20f * 0.05f * wantSpeedScale);
                if (MathF.Abs(d - hold) < hold * 0.12f) {
                    stalking = false;
                }
            }
            else {
                npc.velocity *= 0.97f;
                if (d > SeaShrimpDirector.StalkResumeFar || d < SeaShrimpDirector.StalkResumeNear) {
                    stalking = true;
                }
            }
            TangentMove = Heading.ToRotationVector2();
        }

        private void UpdateSwim() {
            Vector2 desired = (wantPoint - npc.Center) * SeaShrimpDirector.SwimApproach;
            float maxSpeed = SeaShrimpDirector.SwimSpeed * MathHelper.Clamp(wantSpeedScale, 0f, 1.6f);
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, desired, SeaShrimpDirector.SwimInertia);
            TangentMove = npc.velocity.SafeNormalize(Heading.ToRotationVector2());

            //朝向只认实速：锚点悬停时速度在零附近抖动，低阈值会让整条链原地缠团
            if (npc.velocity.Length() > 3.4f) {
                Heading = Heading.AngleLerp(npc.velocity.ToRotation(), 0.07f);
            }
        }
    }
}
