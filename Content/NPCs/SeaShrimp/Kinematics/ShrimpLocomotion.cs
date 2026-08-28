using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics
{
    /// <summary>运动模式</summary>
    internal enum ShrimpMoveMode
    {
        /// <summary>贴面爬行（海底/礁壁，法线夹持）</summary>
        SurfaceCrawl,
        /// <summary>水中悬浮巡游</summary>
        Swim,
        /// <summary>弹道段（尾弹/击飞），状态一帧点火后自治</summary>
        Ballistic,
        /// <summary>剧本段（入场/蜕壳/死亡演出）：速度完全由状态脚本掌管</summary>
        Scripted,
    }

    /// <summary>
    /// 运动执行层：状态每帧写意图（薄命令 API），本层持有贴附面/朝向等持久量并落实速度。
    /// 贴附 = 射线捕面 + 圆周法线 + 贴面弹簧 + 切向推进，斜坡陡壁一体处理；
    /// 尾弹 = 虾式后向弹道（身体朝向不翻转，尾先行）。
    /// 全部消费确定性输入，各端结果一致
    /// </summary>
    internal class ShrimpLocomotion
    {
        private NPC npc;

        public ShrimpMoveMode Mode { get; private set; } = ShrimpMoveMode.SurfaceCrawl;
        /// <summary>贴附面外法线（平滑）</summary>
        public Vector2 SurfaceNormal { get; private set; } = -Vector2.UnitY;
        /// <summary>本帧是否有贴附面</summary>
        public bool Attached { get; private set; }
        /// <summary>头前向角（平滑，骨架直接消费）</summary>
        public float Heading { get; private set; } = 0f;
        /// <summary>本帧切向行进向量（含符号，步态前探用）</summary>
        public Vector2 TangentMove { get; private set; } = Vector2.UnitX;
        /// <summary>当前标量速度（表现层读）</summary>
        public float Speed => npc?.velocity.Length() ?? 0f;
        /// <summary>身体当前是否浸水</summary>
        public bool Wet => npc != null && ShrimpTerrain.WetAt(npc.Center);

        //意图（每帧由状态写入，Update 消费后复位）
        private bool hasIntent;
        private ShrimpMoveMode wantMode;
        private Vector2 wantPoint;
        private float wantSpeedScale;

        //弹道段
        private int ballisticFrames;
        private float ballisticBrake;
        private bool braking;

        //爬行标量速度（加速度积分）
        private float crawlSpeed;

        public void Bind(NPC target) => npc = target;

        /// <summary>贴面爬向世界点（自动解算切向与朝向）</summary>
        public void RequestCrawlTo(Vector2 worldPoint, float speedScale = 1f) {
            hasIntent = true;
            wantMode = ShrimpMoveMode.SurfaceCrawl;
            wantPoint = worldPoint;
            wantSpeedScale = speedScale;
        }

        /// <summary>原地驻停（保持贴附，速度归零）</summary>
        public void RequestHold() {
            hasIntent = true;
            wantMode = ShrimpMoveMode.SurfaceCrawl;
            wantPoint = npc.Center + Heading.ToRotationVector2() * 4f;
            wantSpeedScale = 0f;
        }

        /// <summary>游向锚点</summary>
        public void RequestSwim(Vector2 anchor, float speedScale = 1f) {
            hasIntent = true;
            wantMode = ShrimpMoveMode.Swim;
            wantPoint = anchor;
            wantSpeedScale = speedScale;
        }

        /// <summary>剧本模式：本层不碰速度，演出状态自行脚本化位移与朝向</summary>
        public void RequestScripted() {
            hasIntent = true;
            wantMode = ShrimpMoveMode.Scripted;
        }

        /// <summary>剧本段直写朝向（演出摆位）</summary>
        public void ScriptHeading(float heading, float rate = 0.15f)
            => Heading = Heading.AngleLerp(heading, rate);

        /// <summary>
        /// 点火弹道段（尾弹）：一帧定初速，flight 帧内不衰减，此后按 brake 硬刹。
        /// 身体朝向冻结（虾是尾先行的后向弹射）
        /// </summary>
        public void LaunchBallistic(Vector2 velocity, int flightFrames, float brake) {
            npc.velocity = velocity;
            ballisticFrames = flightFrames;
            ballisticBrake = brake;
            braking = false;
            Mode = ShrimpMoveMode.Ballistic;
        }

        /// <summary>弹道段是否结束（刹停）</summary>
        public bool BallisticDone => Mode != ShrimpMoveMode.Ballistic;

        /// <summary>每帧执行（主体 AI 在状态机之后调用）</summary>
        public void Update() {
            if (Mode == ShrimpMoveMode.Ballistic) {
                UpdateBallistic();
                hasIntent = false;
                return;
            }

            if (!hasIntent) {
                //无意图默认驻停
                wantMode = ShrimpMoveMode.SurfaceCrawl;
                wantPoint = npc.Center + Heading.ToRotationVector2() * 4f;
                wantSpeedScale = 0f;
            }
            hasIntent = false;

            Mode = wantMode;
            if (Mode == ShrimpMoveMode.Scripted) {
                //剧本段：只维持贴附探测供步态参考，不写速度
                Attached = ShrimpTerrain.RaycastSurface(npc.Center, Vector2.UnitY,
                    SeaShrimpDirector.RideHeight * 3f, out Vector2 sp);
                if (Attached) {
                    SurfaceNormal = ShrimpTerrain.SampleNormal(sp, Vector2.UnitY);
                }
                crawlSpeed = 0f;
                TangentMove = Heading.ToRotationVector2();
                return;
            }
            if (Mode == ShrimpMoveMode.Swim) {
                UpdateSwim();
            }
            else {
                UpdateCrawl();
            }
        }

        private void UpdateBallistic() {
            if (ballisticFrames > 0) {
                ballisticFrames--;
                //弹道中段微加速：爆发不衰减（Old Duke 冲刺文法）
                npc.velocity *= 1.008f;
                if (ballisticFrames == 0) {
                    braking = true;
                }
                Attached = false;
                return;
            }
            if (braking) {
                npc.velocity *= ballisticBrake;
                if (npc.velocity.Length() < 3f) {
                    braking = false;
                    Mode = ShrimpMoveMode.SurfaceCrawl;
                    crawlSpeed = 0f;
                }
            }
            Attached = false;
        }

        private void UpdateCrawl() {
            //捕面：先沿当前腹侧探，丢面后向世界下方重捕
            bool hit = ShrimpTerrain.RaycastSurface(npc.Center, -SurfaceNormal,
                SeaShrimpDirector.RideHeight * 2.8f, out Vector2 surfPoint);
            if (!hit) {
                hit = ShrimpTerrain.RaycastSurface(npc.Center, Vector2.UnitY,
                    SeaShrimpDirector.RideHeight * 3.4f, out surfPoint);
                if (hit) {
                    SurfaceNormal = ShrimpTerrain.SampleNormal(surfPoint, Vector2.UnitY);
                }
            }

            if (!hit) {
                //悬空：缓沉找地，保持朝向
                Attached = false;
                npc.velocity.X *= 0.97f;
                npc.velocity.Y = MathF.Min(npc.velocity.Y + 0.34f, 9f);
                crawlSpeed *= 0.9f;
                return;
            }

            Attached = true;
            Vector2 sampled = ShrimpTerrain.SampleNormal(surfPoint, -SurfaceNormal);
            float normalAngle = SurfaceNormal.ToRotation().AngleLerp(sampled.ToRotation(), SeaShrimpDirector.NormalLerp);
            SurfaceNormal = normalAngle.ToRotationVector2();

            //切向推进：正切向 = 法线顺转 90°，符号朝目标
            Vector2 tangentPositive = SurfaceNormal.RotatedBy(MathHelper.PiOver2);
            Vector2 toTarget = wantPoint - npc.Center;
            float moveSign = MathF.Sign(Vector2.Dot(tangentPositive, toTarget));
            if (moveSign == 0f) {
                moveSign = 1f;
            }

            //目标够近就驻停，防原地抽搐
            float targetSpeed = SeaShrimpDirector.CrawlSpeed * MathHelper.Clamp(wantSpeedScale, 0f, 1.6f);
            float tangentDist = MathF.Abs(Vector2.Dot(tangentPositive, toTarget));
            if (tangentDist < 30f) {
                targetSpeed = 0f;
            }
            crawlSpeed = MoveTowards(crawlSpeed, targetSpeed, SeaShrimpDirector.CrawlAccel);

            Vector2 tangentMove = tangentPositive * moveSign;
            //贴面弹簧：把体轴钉回离面高度，修正量限幅防弹跳
            Vector2 desiredPos = surfPoint + SurfaceNormal * SeaShrimpDirector.RideHeight;
            Vector2 stick = (desiredPos - npc.Center) * SeaShrimpDirector.SurfaceStick;
            float stickLen = stick.Length();
            if (stickLen > 7f) {
                stick *= 7f / stickLen;
            }

            npc.velocity = tangentMove * crawlSpeed + stick;
            TangentMove = tangentMove;

            //朝向：有速度朝切向，驻停保持
            if (crawlSpeed > 0.4f) {
                Heading = Heading.AngleLerp(tangentMove.ToRotation(), 0.13f);
            }
        }

        private void UpdateSwim() {
            Attached = false;
            //法线缓回世界上方（身体转平）
            float normalAngle = SurfaceNormal.ToRotation().AngleLerp((-Vector2.UnitY).ToRotation(), 0.05f);
            SurfaceNormal = normalAngle.ToRotationVector2();

            Vector2 desired = (wantPoint - npc.Center) * SeaShrimpDirector.SwimApproach;
            float maxSpeed = SeaShrimpDirector.SwimSpeed * MathHelper.Clamp(wantSpeedScale, 0f, 1.6f);
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, desired, SeaShrimpDirector.SwimInertia);
            TangentMove = npc.velocity.SafeNormalize(Heading.ToRotationVector2());
            crawlSpeed = 0f;

            //朝向只认实速：锚点悬停时速度在零附近抖动，低阈值会让整条链原地缠团
            if (npc.velocity.Length() > 3.4f) {
                Heading = Heading.AngleLerp(npc.velocity.ToRotation(), 0.07f);
            }
        }

        /// <summary>入场/重建时硬置朝向</summary>
        public void SnapHeading(float heading) => Heading = heading;

        private static float MoveTowards(float cur, float target, float step)
            => cur < target ? MathF.Min(cur + step, target) : MathF.Max(cur - step, target);
    }
}
