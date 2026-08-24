using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns
{
    /// <summary>
    /// 鞭笞演出层。力量感三律：蓄力占大头（腕拉过根线的反向运动+死静顿拍）、
    /// 抽击一瞬（3~4 帧极锐 ease-out，段长瞬时拉伸成鞭）、命中有回应（顿帧钉腕+
    /// 血水迸溅+屏震+击退）。鞭笞=左右两记侧抽+合掌下砸终结；自动鞭击=单记侧抽缩规格。
    /// 打击帧与 <see cref="KikasaScourge"/> 权威节拍共用一份常量，命中数字恰落在鞭中那一帧。
    /// 腕逐帧追踪活体，目标中途死亡余手满足地缓沉收场；绘制由
    /// <see cref="KikasaDrownFX.Draw"/> 转来，与沉溺鬼手同一批次口径
    /// </summary>
    internal static class KikasaScourgeFX
    {
        //==================== 编舞常量 ====================

        private const int BurstFrames = 14;
        private const int PoiseFrames = 4;
        private const int StrikeFrames = 3;
        private const int LeadFrames = BurstFrames + PoiseFrames + StrikeFrames;
        private const int ImpactHoldFrames = 3;
        private const int SmashPoiseFrames = 5;
        private const int SmashDropFrames = 4;
        private const int SinkOutFrames = 22;

        private enum JobKind : byte
        {
            /// <summary>侧抽：从一侧蓄力横鞭穿目标</summary>
            SideLash,
            /// <summary>合掌下砸：高举过顶砸向目标上缘</summary>
            Smash,
        }

        private sealed class LashJob
        {
            public JobKind Kind;
            /// <summary>出手侧 ±1；Smash 为 0，各手沿用自己根位所在侧</summary>
            public int Side;
            public int BurstStart;
            public int PoiseStart;
            public int StrikeStart;
            /// <summary>命中帧，与权威打击拍同帧</summary>
            public int ContactFrame;
            public int RecoverEnd;
        }

        private sealed class ScourgeHand
        {
            public KikasaHandRig Rig;
            /// <summary>根位所在侧（决定肘向与回收姿态）</summary>
            public int RootSide;
            public readonly List<LashJob> Jobs = [];
            public int JobCursor;
            //本 job 的一次性演出闩
            public int FxJobIndex = -1;
            public bool BurstFxDone;
            public bool WhooshDone;
            public bool ImpactFxDone;
        }

        private sealed class ScourgeShow
        {
            public int ScourgeId;
            public int OwnerIndex;
            public byte Kind;
            public float Seed;
            public NetworkNPCIdentity Target;
            public ScourgeHand[] Hands;
            public int Timer;
            public float LakeY;
            /// <summary>目标包围盒半尺寸（起演帧定格，尺寸不追帧防抖）</summary>
            public Vector2 TargetHalf;
            /// <summary>最后一次成功解析的目标中心，死后姿态基点</summary>
            public Vector2 LastCenter;
            /// <summary>体型水花系数，同沉溺口径</summary>
            public float SplashScale = 1f;
            /// <summary>目标没了/湖塌了：全员缓沉收场</summary>
            public bool SinkingOut;
            public int SinkTimer;
            /// <summary>合掌命中反馈的演出级闩：两手同拍，只响一次</summary>
            public bool SmashFxDone;
            public bool Done;
        }

        private static readonly List<ScourgeShow> shows = [];

        //鬼雨异化时随观看域冷化，与沉溺同一对色
        private static Color BloodTint => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color FoamGlow => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));

        public static void Clear() => shows.Clear();

        /// <summary>该玩家有任何鞭击演出在场（自动鞭击的自我抑制口径）</summary>
        internal static bool HasActiveShowFor(int ownerIndex) {
            for (int i = 0; i < shows.Count; i++) {
                if (shows[i].OwnerIndex == ownerIndex && !shows[i].Done) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>该玩家有鞭笞（不含自动鞭击）在场：沉入键的占用口径，小骚扰不挡玩家出手</summary>
        internal static bool HasPressBlockingShowFor(int ownerIndex) {
            for (int i = 0; i < shows.Count; i++) {
                if (shows[i].OwnerIndex == ownerIndex && !shows[i].Done
                    && shows[i].Kind == KikasaScourge.KindPunish) {
                    return true;
                }
            }
            return false;
        }

        //==================== 起演 ====================

        internal static void StartShow(int ownerWho, int scourgeId, float seed,
            byte kind, NetworkNPCIdentity target) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < shows.Count; i++) {
                if (shows[i].ScourgeId == scourgeId) {
                    return;
                }
            }
            if (ownerWho < 0 || ownerWho >= Main.maxPlayers
                || Main.player[ownerWho]?.active != true
                || !target.TryResolve(out NPC npc)) {
                return;
            }

            Player owner = Main.player[ownerWho];
            float lakeY = owner.GetModPlayer<KikasaDomainPlayer>().LakeWorldY;

            ScourgeShow show = new() {
                ScourgeId = scourgeId,
                OwnerIndex = ownerWho,
                Kind = kind,
                Seed = seed,
                Target = target,
                LakeY = lakeY,
                TargetHalf = new Vector2(npc.width, npc.height) * 0.5f,
                LastCenter = npc.Center,
            };
            show.SplashScale = MathHelper.Clamp(
                MathF.Sqrt(npc.width * (float)npc.height) / 30f, 0.9f, 2.4f);

            BuildHands(show, npc);
            shows.Add(show);

            //鞭笞占用沉入键：锁到演出结束再加整段冷却，HUD 弧随之走完整时长
            if (kind == KikasaScourge.KindPunish && ownerWho == Main.myPlayer) {
                KikasaDrown.LockLocal(KikasaScourge.PunishLengthFrames
                    + KikasaScourge.PunishCooldownFrames);
            }

            if (IsViewedOwner(ownerWho)) {
                //起手：湖底一声闷雷，这次不是馋，是怒
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.85f, MaxInstances = 2 },
                    new Vector2(npc.Center.X, lakeY));
                if (kind == KikasaScourge.KindPunish) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.3f, Pitch = -0.9f, MaxInstances = 1 },
                        new Vector2(npc.Center.X, lakeY));
                }
            }
        }

        /// <summary>
        /// 排程建手：鞭笞两只（先手侧随种子，两记侧抽错帧，双双转入合掌下砸）；
        /// 自动鞭击单只。每个 job 的命中帧从权威节拍表倒推各段起点
        /// </summary>
        private static void BuildHands(ScourgeShow show, NPC target) {
            int[] beats = KikasaScourge.BeatsOf(show.Kind);
            float area = target.width * target.height;
            float handScale = MathHelper.Clamp(MathF.Sqrt(area) / 38f, 0.85f, 1.35f);

            if (show.Kind == KikasaScourge.KindAmbient) {
                int side = KikasaScourge.StrikeSide(show.Seed, show.Kind, 0);
                ScourgeHand hand = MakeHand(show, side, handScale * 0.9f, 0);
                //回收窗+化水窗(SinkOutFrames)恰好收在演出总长上，手不凭空消失
                hand.Jobs.Add(MakeLashJob(side, beats[0],
                    recoverFrames: KikasaScourge.AmbientLengthFrames - SinkOutFrames
                        - beats[0] - ImpactHoldFrames));
                show.Hands = [hand];
                return;
            }

            int sideA = KikasaScourge.StrikeSide(show.Seed, show.Kind, 0);
            int sideB = -sideA;
            ScourgeHand handA = MakeHand(show, sideA, handScale * 1.1f, 0);
            ScourgeHand handB = MakeHand(show, sideB, handScale * 1.1f, 1);

            handA.Jobs.Add(MakeLashJob(sideA, beats[0], recoverFrames: 12));
            handB.Jobs.Add(MakeLashJob(sideB, beats[1], recoverFrames: 10));

            //合掌：两手在各自回收后转入上举，命中帧共享终结拍
            int smashContact = beats[2];
            int recoverEnd = Math.Min(smashContact + ImpactHoldFrames + 9,
                KikasaScourge.PunishLengthFrames - SinkOutFrames);
            ScourgeHand[] pair = [handA, handB];
            foreach (ScourgeHand hand in pair) {
                int riseStart = hand.Jobs[^1].RecoverEnd + 1;
                hand.Jobs.Add(new LashJob {
                    Kind = JobKind.Smash,
                    Side = 0,
                    BurstStart = riseStart,
                    PoiseStart = smashContact - SmashDropFrames - SmashPoiseFrames,
                    StrikeStart = smashContact - SmashDropFrames,
                    ContactFrame = smashContact,
                    RecoverEnd = recoverEnd,
                });
            }
            show.Hands = [handA, handB];
        }

        private static LashJob MakeLashJob(int side, int contactFrame, int recoverFrames)
            => new() {
                Kind = JobKind.SideLash,
                Side = side,
                BurstStart = contactFrame - LeadFrames,
                PoiseStart = contactFrame - PoiseFrames - StrikeFrames,
                StrikeStart = contactFrame - StrikeFrames,
                ContactFrame = contactFrame,
                RecoverEnd = contactFrame + ImpactHoldFrames + recoverFrames,
            };

        private static ScourgeHand MakeHand(ScourgeShow show, int side, float scale, int index) {
            float jx = (Hash(show.Seed, index * 5 + 3) - 0.5f) * 36f;
            float spread = MathHelper.Clamp(show.TargetHalf.X * 1.6f + 90f, 120f, 460f);
            Vector2 root = new(show.LastCenter.X + side * spread + jx, show.LakeY + 2f);
            float reach = Vector2.Distance(root, show.LastCenter);

            KikasaHandRig rig = new() {
                Root = root,
                Wrist = new Vector2(root.X, show.LakeY + 14f),
                SegmentLength = 40f,
                Tension = 0.75f,
                BendDir = side,
                Curl = -0.1f,
                Opacity = 0f,
                Scale = scale * (1f + MathHelper.Clamp((reach - 340f) / 1100f, 0f, 1f)),
                Seed = show.Seed + index * 7.77f,
                FrontLayer = true,
            };
            return new ScourgeHand { Rig = rig, RootSide = side };
        }

        private static float Hash(float seed, int k) {
            float h = MathF.Sin(seed * 12.9898f + k * 78.233f) * 43758.547f;
            return h - MathF.Floor(h);
        }

        //==================== 推进 ====================

        public static void Update() {
            for (int i = shows.Count - 1; i >= 0; i--) {
                ScourgeShow show = shows[i];
                UpdateShow(show);
                if (show.Done) {
                    shows.RemoveAt(i);
                }
            }
        }

        private static bool LakeAlive(int ownerIndex) {
            if (ownerIndex < 0 || ownerIndex >= Main.maxPlayers) {
                return false;
            }
            Player owner = Main.player[ownerIndex];
            return owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT >= 0.9f;
        }

        private static void UpdateShow(ScourgeShow show) {
            bool visible = IsViewedOwner(show.OwnerIndex);
            show.Timer++;
            int t = show.Timer;

            if (show.SinkingOut) {
                UpdateSinkOut(show);
                return;
            }
            if (!LakeAlive(show.OwnerIndex)) {
                BeginSinkOut(show);
                return;
            }

            bool targetAlive = show.Target.TryResolve(out NPC target) && target.life > 0;
            if (targetAlive) {
                show.LastCenter = target.Center;
            }
            else if (AnyImpactPending(show, t)) {
                //还没抽完目标就没了：湖收手，抽空气不是力量是滑稽
                BeginSinkOut(show);
                return;
            }

            //目标脚下水线的怒意：小涟漪密拍（鞭笞才有，自动鞭击不闹）
            if (visible && show.Kind == KikasaScourge.KindPunish && t % 8 == 3) {
                KikasaDomainDeco.RippleAt(new Vector2(
                    show.LastCenter.X + Main.rand.NextFloat(-16f, 16f) * show.SplashScale,
                    show.LakeY), 0.28f * show.SplashScale);
            }

            for (int i = 0; i < show.Hands.Length; i++) {
                UpdateHand(show, show.Hands[i], t, visible);
            }

            if (t >= KikasaScourge.LengthOf(show.Kind)) {
                show.Done = true;
            }
        }

        /// <summary>还有未到的命中拍（含正抽到一半），此时丢失目标应收手而非继续演完</summary>
        private static bool AnyImpactPending(ScourgeShow show, int t) {
            for (int i = 0; i < show.Hands.Length; i++) {
                List<LashJob> jobs = show.Hands[i].Jobs;
                for (int j = 0; j < jobs.Count; j++) {
                    if (t < jobs[j].ContactFrame) {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void BeginSinkOut(ScourgeShow show) {
            show.SinkingOut = true;
            show.SinkTimer = 0;
            if (IsViewedOwner(show.OwnerIndex)) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 2 },
                    new Vector2(show.LastCenter.X, show.LakeY));
            }
        }

        private static void UpdateSinkOut(ScourgeShow show) {
            show.SinkTimer++;
            float wt = MathHelper.Clamp(show.SinkTimer / (float)SinkOutFrames, 0f, 1f);
            for (int i = 0; i < show.Hands.Length; i++) {
                KikasaHandRig rig = show.Hands[i].Rig;
                if (rig.Opacity <= 0.01f) {
                    continue;
                }
                //满足地攥了一把，弧线缩回水里
                rig.Curl = MathHelper.Lerp(rig.Curl, 0.8f, 0.25f);
                rig.Tension = MathHelper.Lerp(rig.Tension, 0.5f, 0.2f);
                Vector2 home = new(rig.Root.X, show.LakeY + 34f);
                rig.Wrist = Vector2.Lerp(rig.Wrist, home, 0.12f + wt * 0.24f);
                rig.SegmentLength = MathHelper.Clamp(
                    Vector2.Distance(rig.Root, rig.Wrist) * 1.15f / KikasaHandRig.ArmSegmentCount,
                    26f, 240f);
                rig.Opacity = 1f - wt;
                rig.Drain = wt * 0.8f;
                rig.Grip = MathHelper.Clamp(1f - rig.Tension * 1.4f, 0f, 1f);
                rig.Solve();
            }
            if (show.SinkTimer >= SinkOutFrames) {
                show.Done = true;
            }
        }

        //==================== 单手编舞 ====================

        private static void UpdateHand(ScourgeShow show, ScourgeHand hand, int t, bool visible) {
            KikasaHandRig rig = hand.Rig;
            List<LashJob> jobs = hand.Jobs;

            //推进 job 游标；换 job 清一次性演出闩
            while (hand.JobCursor < jobs.Count - 1 && t > jobs[hand.JobCursor].RecoverEnd) {
                hand.JobCursor++;
            }
            LashJob job = jobs[hand.JobCursor];
            if (hand.FxJobIndex != hand.JobCursor) {
                hand.FxJobIndex = hand.JobCursor;
                hand.WhooshDone = false;
                hand.ImpactFxDone = false;
            }

            //末 job 收尾后进入化水谢幕
            if (hand.JobCursor == jobs.Count - 1 && t > job.RecoverEnd) {
                float dt = MathHelper.Clamp(
                    (t - job.RecoverEnd) / (float)SinkOutFrames, 0f, 1f);
                rig.Curl = MathHelper.Lerp(rig.Curl, 0.7f, 0.2f);
                rig.Tension = MathHelper.Lerp(rig.Tension, 0.55f, 0.15f);
                rig.Wrist = Vector2.Lerp(rig.Wrist,
                    new Vector2(rig.Root.X, show.LakeY + 30f), 0.1f + dt * 0.2f);
                rig.Drain = dt * 0.9f;
                rig.Opacity = MathHelper.Clamp(1f - (dt - 0.35f) / 0.65f, 0f, 1f);
                SolveWithSlack(rig, 1.12f);
                return;
            }

            if (t < job.BurstStart) {
                if (rig.Opacity <= 0.01f) {
                    //还没轮到它出水
                    return;
                }
                //两 job 之间的空窗：贴水悬停喘一口
                rig.Wrist = Vector2.Lerp(rig.Wrist,
                    new Vector2(rig.Root.X + hand.RootSide * 24f, show.LakeY - 26f), 0.15f);
                rig.Tension = MathHelper.Lerp(rig.Tension, 0.7f, 0.1f);
                rig.Curl = MathHelper.Lerp(rig.Curl, 0.1f, 0.1f);
                SolveWithSlack(rig, 1.15f);
                return;
            }

            if (job.Kind == JobKind.SideLash) {
                UpdateSideLash(show, hand, job, t, visible);
            }
            else {
                UpdateSmash(show, hand, job, t, visible);
            }

            rig.Grip = MathHelper.Clamp(1f - rig.Tension * 1.4f, 0f, 1f);
            rig.Foam = MathHelper.Lerp(rig.Foam, 0.4f, 0.1f);
        }

        //侧抽四点：蓄力位（目标外上方，腕拉过根线）→ 顿 → 抽穿点（对侧）→ 甩透回收

        private static Vector2 CoilPoint(ScourgeShow show, LashJob job, float scale)
            => show.LastCenter + new Vector2(
                job.Side * (show.TargetHalf.X + 120f * scale),
                -show.TargetHalf.Y * 0.55f - 95f * scale);

        private static Vector2 ThroughPoint(ScourgeShow show, LashJob job, float scale)
            => show.LastCenter + new Vector2(
                -job.Side * (show.TargetHalf.X * 0.7f + 50f * scale),
                show.TargetHalf.Y * 0.15f);

        private static void UpdateSideLash(ScourgeShow show, ScourgeHand hand,
            LashJob job, int t, bool visible) {
            KikasaHandRig rig = hand.Rig;
            float scale = rig.Scale;

            if (t < job.PoiseStart) {
                //破水蓄力：根先动腕滞后的过冲弧，甩到目标外上方的蓄力位
                if (!hand.BurstFxDone) {
                    hand.BurstFxDone = true;
                    rig.Opacity = 1f;
                    rig.Foam = 1f;
                    if (visible) {
                        KikasaDomainDeco.SplashAt(rig.Root, 6);
                        KikasaDomainDeco.RippleAt(rig.Root, 0.8f);
                        SoundEngine.PlaySound(SoundID.SplashWeak with {
                            Volume = show.Kind == KikasaScourge.KindAmbient ? 0.4f : 0.55f,
                            Pitch = -0.4f,
                            MaxInstances = 3
                        }, rig.Root);
                    }
                }
                float bt = (t - job.BurstStart + 1) / (float)BurstFrames;
                float ease = 1f - MathF.Pow(1f - bt, 2.6f);
                Vector2 start = new(rig.Root.X, show.LakeY + 12f);
                Vector2 coil = CoilPoint(show, job, scale);
                Vector2 ctrl = rig.Root + (coil - rig.Root) * 0.5f
                    + new Vector2(job.Side * 40f, -80f * scale);
                Vector2 a = Vector2.Lerp(start, ctrl, ease);
                Vector2 b = Vector2.Lerp(ctrl, coil, ease);
                rig.Wrist = Vector2.Lerp(a, b, ease);
                rig.Tension = MathHelper.Lerp(0.75f, 0.9f, ease);
                rig.Curl = MathHelper.Lerp(rig.Curl, -0.15f, 0.35f);
                SolveWithSlack(rig, 1.15f);
                return;
            }

            if (t < job.StrikeStart) {
                //死静顿拍：只再向后攒 8px，力量住在这份反向里
                Vector2 coil = CoilPoint(show, job, scale);
                Vector2 back = coil + new Vector2(job.Side * 8f, -3f);
                rig.Wrist = Vector2.Lerp(rig.Wrist, back, 0.4f);
                rig.Tension = MathHelper.Lerp(rig.Tension, 0.95f, 0.5f);
                rig.Curl = MathHelper.Lerp(rig.Curl, -0.2f, 0.4f);
                SolveWithSlack(rig, 1.1f);
                return;
            }

            if (t < job.ContactFrame) {
                //抽击：极锐 ease-out 掠过目标中心，臂拉成鞭
                if (!hand.WhooshDone) {
                    hand.WhooshDone = true;
                    if (visible) {
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                            Volume = show.Kind == KikasaScourge.KindAmbient ? 0.4f : 0.6f,
                            Pitch = 0.1f,
                            MaxInstances = 3
                        }, rig.Wrist);
                    }
                }
                float st = (t - job.StrikeStart + 1) / (float)StrikeFrames;
                float ease = 1f - MathF.Pow(1f - st, 3.4f);
                rig.Wrist = Vector2.Lerp(CoilPoint(show, job, scale),
                    ThroughPoint(show, job, scale), ease);
                rig.Tension = 0.12f;
                rig.Curl = -0.2f;
                //瞬时拉伸：臂几乎绷成直线，上限放宽让它真够得着
                SolveWithSlack(rig, 1.06f, 300f);
                return;
            }

            if (t < job.ContactFrame + ImpactHoldFrames) {
                //顿帧：腕钉在穿透点上，世界替它停一拍
                if (!hand.ImpactFxDone) {
                    hand.ImpactFxDone = true;
                    ImpactFx(show, hand, job, visible, smash: false);
                }
                rig.Wrist = show.LastCenter + new Vector2(
                    -job.Side * show.TargetHalf.X * 0.3f, show.TargetHalf.Y * 0.1f);
                rig.Tension = 0.1f;
                SolveWithSlack(rig, 1.06f, 300f);
                return;
            }

            //甩透卸力：顺势荡到对侧外缘再松下来
            float rt = MathHelper.Clamp(
                (t - job.ContactFrame - ImpactHoldFrames + 1)
                / (float)Math.Max(job.RecoverEnd - job.ContactFrame - ImpactHoldFrames, 1), 0f, 1f);
            Vector2 followOut = show.LastCenter + new Vector2(
                -job.Side * (show.TargetHalf.X + 150f * scale),
                -show.TargetHalf.Y * 0.25f - 20f * rt);
            rig.Wrist = Vector2.Lerp(rig.Wrist, followOut, 0.3f * (1f - rt) + 0.08f);
            rig.Tension = MathHelper.Lerp(rig.Tension, 0.5f, 0.2f);
            rig.Curl = MathHelper.Lerp(rig.Curl, 0.15f, 0.2f);
            if (visible && t % 5 == 2 && rig.Opacity > 0.5f) {
                //甩透后的水珠沿臂洒落
                Vector2 mid = Vector2.Lerp(rig.Root, rig.WristSolved, 0.6f);
                PRTLoader.NewParticle<PRT_GhostRainDrop>(mid,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(1.4f, 2.4f)),
                    BloodTint * 0.45f, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(12, 20), 0f);
            }
            SolveWithSlack(rig, 1.12f);
        }

        //合掌下砸：缓慢上举（威压式蓄力）→ 顿 → 急坠合掌 → 大水花

        private static void UpdateSmash(ScourgeShow show, ScourgeHand hand,
            LashJob job, int t, bool visible) {
            KikasaHandRig rig = hand.Rig;
            float scale = rig.Scale;
            Vector2 risePoint = show.LastCenter + new Vector2(
                hand.RootSide * 42f * scale,
                -show.TargetHalf.Y - 215f * scale);

            if (t < job.PoiseStart) {
                //上举：慢起快收的 smoothstep，双手向目标顶上合拢
                float rt = MathHelper.Clamp(
                    (t - job.BurstStart + 1) / (float)Math.Max(job.PoiseStart - job.BurstStart, 1), 0f, 1f);
                float ease = rt * rt * (3f - 2f * rt);
                rig.Wrist = Vector2.Lerp(rig.Wrist, risePoint, 0.06f + ease * 0.22f);
                rig.Tension = MathHelper.Lerp(rig.Tension, 0.75f, 0.1f);
                rig.Curl = MathHelper.Lerp(rig.Curl, 0.45f, 0.08f);
                SolveWithSlack(rig, 1.15f);
                return;
            }

            if (t < job.StrikeStart) {
                //顶点死静：连水面泡沫都压住
                rig.Wrist = Vector2.Lerp(rig.Wrist, risePoint + new Vector2(0f, -5f), 0.35f);
                rig.Tension = MathHelper.Lerp(rig.Tension, 0.85f, 0.4f);
                rig.Foam = MathHelper.Lerp(rig.Foam, 0.1f, 0.3f);
                SolveWithSlack(rig, 1.1f);
                return;
            }

            if (t < job.ContactFrame) {
                //急坠：加速度曲线砸向目标上缘
                if (!hand.WhooshDone) {
                    hand.WhooshDone = true;
                    if (visible) {
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                            Volume = 0.65f, Pitch = -0.2f, MaxInstances = 2
                        }, rig.Wrist);
                    }
                }
                float st = (t - job.StrikeStart + 1) / (float)SmashDropFrames;
                float ease = st * st * st;
                Vector2 smashPoint = show.LastCenter + new Vector2(
                    hand.RootSide * 14f * scale, -show.TargetHalf.Y * 0.2f);
                rig.Wrist = Vector2.Lerp(risePoint, smashPoint, ease);
                rig.Tension = MathHelper.Lerp(rig.Tension, 0.15f, 0.5f);
                rig.Curl = MathHelper.Lerp(rig.Curl, 0.1f, 0.4f);
                SolveWithSlack(rig, 1.06f, 300f);
                return;
            }

            if (t < job.ContactFrame + ImpactHoldFrames + 1) {
                //合掌顿帧：两手同拍，命中反馈闩在演出上只响一次
                if (!show.SmashFxDone) {
                    show.SmashFxDone = true;
                    ImpactFx(show, hand, job, visible, smash: true);
                }
                rig.Wrist = show.LastCenter + new Vector2(
                    hand.RootSide * 12f * scale, -show.TargetHalf.Y * 0.15f);
                rig.Tension = 0.08f;
                SolveWithSlack(rig, 1.06f, 300f);
                return;
            }

            //压住目标一拍再松手
            float ht = MathHelper.Clamp(
                (t - job.ContactFrame - ImpactHoldFrames)
                / (float)Math.Max(job.RecoverEnd - job.ContactFrame - ImpactHoldFrames, 1), 0f, 1f);
            rig.Wrist = Vector2.Lerp(rig.Wrist,
                show.LastCenter + new Vector2(hand.RootSide * 30f * scale, -show.TargetHalf.Y * 0.4f - 24f * ht),
                0.12f);
            rig.Tension = MathHelper.Lerp(rig.Tension, 0.45f, 0.15f);
            SolveWithSlack(rig, 1.1f);
        }

        /// <summary>命中反馈：血水迸溅+水线涟漪+闷响水响双层+屏震，合掌全量加码</summary>
        private static void ImpactFx(ScourgeShow show, ScourgeHand hand, LashJob job,
            bool visible, bool smash) {
            if (!visible) {
                return;
            }
            bool ambient = show.Kind == KikasaScourge.KindAmbient;
            float s = show.SplashScale;
            Vector2 hit = show.LastCenter;
            Vector2 waterline = new(hit.X, show.LakeY);

            if (smash) {
                KikasaDomainDeco.SplashAt(waterline, Math.Min((int)(12 * s), 24));
                KikasaDomainDeco.RippleAt(waterline, 1.6f * s);
                KikasaDomainDeco.RippleAt(waterline + new Vector2(30f * s, 0f), 0.7f * s);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Volume = 0.7f, Pitch = -0.55f, MaxInstances = 1
                }, hit);
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.85f, Pitch = -0.35f, MaxInstances = 2
                }, waterline);
                ShakeViewer(4f * MathF.Min(s, 1.7f));
                PRTLoader.NewParticle<PRT_GhostRainMist>(hit, new Vector2(0f, -0.5f),
                    new Color(58, 18, 20) * 0.7f, Main.rand.NextFloat(0.5f, 0.7f))
                    ?.Configure(Main.rand.Next(40, 60));
            }
            else {
                KikasaDomainDeco.RippleAt(waterline, 0.8f * MathF.Min(s, 1.5f));
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Volume = ambient ? 0.32f : 0.5f, Pitch = -0.25f, MaxInstances = 2
                }, hit);
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = ambient ? 0.35f : 0.5f, Pitch = 0.05f, MaxInstances = 3
                }, hit);
                ShakeViewer(ambient ? 1.2f : 2.4f);
            }

            //血水朝甩出方向迸溅；合掌向两侧压出
            int burst = smash ? 9 : 6;
            for (int i = 0; i < burst; i++) {
                Vector2 vel = smash
                    ? new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), Main.rand.NextFloat(-1.2f, 0.6f))
                    : new Vector2(-job.Side * Main.rand.NextFloat(1.2f, 3.4f),
                        Main.rand.NextFloat(-1.8f, 0.8f));
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    hit + Main.rand.NextVector2Circular(show.TargetHalf.X * 0.4f, show.TargetHalf.Y * 0.4f),
                    vel, BloodTint * 0.6f, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 24), 0f);
            }
        }

        /// <summary>段长按当前根腕距动态定标后解算；strike 期上限放宽即瞬时拉伸</summary>
        private static void SolveWithSlack(KikasaHandRig rig, float slack, float maxSeg = 240f) {
            rig.SegmentLength = MathHelper.Clamp(
                Vector2.Distance(rig.Root, rig.Wrist) * slack / KikasaHandRig.ArmSegmentCount,
                26f, maxSeg);
            rig.Solve();
        }

        //==================== 绘制（由 KikasaDrownFX.Draw 转来，批次口径一致） ====================

        internal static void Draw(SpriteBatch spriteBatch, int viewedOwner,
            Effect handFx, Texture2D noise, bool shaderOk) {
            if (shows.Count == 0) {
                return;
            }
            DrawGlow(spriteBatch, viewedOwner);
            DrawHands(spriteBatch, viewedOwner, handFx, noise, shaderOk);
        }

        //根口泡沫光：手在外面，湖在它脚下打转

        private static void DrawGlow(SpriteBatch spriteBatch, int viewedOwner) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            bool begun = false;
            Vector2 origin = glow.Size() * 0.5f;
            foreach (ScourgeShow show in shows) {
                if (show.OwnerIndex != viewedOwner) {
                    continue;
                }
                for (int i = 0; i < show.Hands.Length; i++) {
                    KikasaHandRig rig = show.Hands[i].Rig;
                    if (rig.Opacity <= 0.05f) {
                        continue;
                    }
                    float a = 0.22f * rig.Opacity * (0.6f + 0.4f * rig.Foam);
                    if (!begun) {
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                            SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                            null, Main.GameViewMatrix.TransformationMatrix);
                        begun = true;
                    }
                    spriteBatch.Draw(glow, rig.Root - Main.screenPosition, null,
                        FoamGlow * a, 0f, origin,
                        new Vector2(30f / glow.Width * 2.0f, 12f / glow.Height) * rig.Scale,
                        SpriteEffects.None, 0f);
                }
            }
            if (begun) {
                spriteBatch.End();
            }
        }

        //条带层：与沉溺鬼手同一 shader 与装配口径；缺编时线链回退

        private static void DrawHands(SpriteBatch spriteBatch, int viewedOwner,
            Effect handFx, Texture2D noise, bool shaderOk) {

            if (!shaderOk) {
                Texture2D pixel = VaultAsset.placeholder2?.Value;
                if (pixel == null) {
                    return;
                }
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone,
                    null, Main.GameViewMatrix.TransformationMatrix);
                foreach (ScourgeShow show in shows) {
                    if (show.OwnerIndex != viewedOwner) {
                        continue;
                    }
                    for (int i = 0; i < show.Hands.Length; i++) {
                        show.Hands[i].Rig.DrawFallback(spriteBatch, pixel);
                    }
                }
                spriteBatch.End();
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            handFx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            handFx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            handFx.Parameters["uNoiseTex"]?.SetValue(noise);

            foreach (ScourgeShow show in shows) {
                if (show.OwnerIndex != viewedOwner) {
                    continue;
                }
                for (int i = 0; i < show.Hands.Length; i++) {
                    KikasaHandRig rig = show.Hands[i].Rig;
                    if (rig.Opacity <= 0.01f) {
                        continue;
                    }
                    handFx.Parameters["uOpacity"]?.SetValue(rig.Opacity);
                    handFx.Parameters["uGrip"]?.SetValue(rig.Grip);
                    handFx.Parameters["uSeed"]?.SetValue(rig.Seed);
                    handFx.Parameters["uFoam"]?.SetValue(rig.Foam);
                    handFx.Parameters["uDrain"]?.SetValue(rig.Drain);

                    var armVerts = rig.BuildArmStrip();
                    var palmVerts = rig.BuildPalmStrip();
                    foreach (EffectPass pass in handFx.CurrentTechnique.Passes) {
                        pass.Apply();
                        device.DrawUserPrimitives(PrimitiveType.TriangleStrip, armVerts, 0, armVerts.Length - 2);
                        device.DrawUserPrimitives(PrimitiveType.TriangleStrip, palmVerts, 0, palmVerts.Length - 2);
                        for (int k = 0; k < 5; k++) {
                            var fingerVerts = rig.BuildFingerStrip(k);
                            device.DrawUserPrimitives(PrimitiveType.TriangleStrip, fingerVerts, 0, fingerVerts.Length - 2);
                        }
                    }
                }
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }

        private static bool IsViewedOwner(int ownerIndex) {
            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            return viewed != null && viewed.Player.whoAmI == ownerIndex;
        }

        private static void ShakeViewer(float amount)
            => Main.LocalPlayer?.CWR()?.GetScreenShake(amount);
    }
}
