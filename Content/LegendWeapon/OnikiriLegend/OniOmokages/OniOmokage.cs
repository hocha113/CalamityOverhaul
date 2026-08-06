using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Tutorial;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniOmokages
{
    internal enum OmokageState : byte
    {
        PendingCapture,
        Separating,
        Armed,
        Cutting,
        Burning,
    }

    /// <summary>单个 NPC 的共享快照、同一目标的多幅面影共用一张 RT</summary>
    internal class OmokageSnap
    {
        public int NpcType;          //槽位复用校验
        public uint NpcSpawnToken;

        public int Width;
        public int Height;
        public float PaperWidth;
        public float PaperHeight;
        /// <summary>渲染端完成捕获后置位，此前纸面走无快照回退绘制</summary>
        public bool Captured;
        public bool CaptureUnavailable;
        public int CaptureFailures;
        public RenderTarget2D RT;
        public Rectangle SourceFrame;
        public float SourceScale;
        public float SourceRotation;
        public float SourceDrawOffsetY;
        public SpriteEffects SourceEffects;
    }

    /// <summary>单幅面影、留在过去位置的水墨纸人</summary>
    internal class OmokageEntry
    {
        public int Id;
        public int NpcIndex;
        public int NpcType;          //槽位复用校验
        public uint NpcSpawnToken;

        /// <summary>完成剥离后的稳定锚点</summary>
        public Vector2 AnchorCenter;
        public Vector2 RenderCenter;
        public Vector2 CaptureCenter;
        public Vector2 SeparationStart;
        public int SnapWidth;
        public int SnapHeight;
        /// <summary>纸人显示与交互尺寸（px），和捕获 RT 尺寸分离</summary>
        public float PaperWidth;
        public float PaperHeight;
        public int Timer;
        public int Lifetime;
        public float Seed;
        public float SwayPhase;
        public OmokageState State;
        public int StateTimer;
        public int SeparationDuration;
        public float SeparationRevealStart;
        public float SeparationDevelopStart;
        public float Reveal;
        public float Develop;

        public bool Cut;
        /// <summary>落刀点（纸面中心局部像素，与身体局部 1:1 对应）</summary>
        public Vector2 CutLocal;
        public float CutAngle;
        /// <summary>落刀后经过帧数（含滞拍延迟段；裂开进度用 <see cref="SplitAge"/>）</summary>
        public int CutAge;
        /// <summary>纸裂延迟、落刀帧 → 纸面实际裂开（=刀线演出引爆帧）的帧数，0=落刀即裂</summary>
        public int CutDelay;
        /// <summary>纸面实际裂开后的帧数；负值=刀线演出滞拍中，纸还完整</summary>
        public int SplitAge => CutAge - CutDelay;
        //落刀时暂存、裂纸帧发射脉冲用的结算参数

        public int PendingDamage;
        public float PendingKnockback;
        public int PendingPlayer = -1;
        /// <summary>裁出的纸片（纸面中心局部像素）；未成功分割时为整张纸单片</summary>
        public readonly List<Vector2[]> Halves = [];
        /// <summary>与 Halves 对齐、±1=沿切线法线哪侧滑开，0=不滑</summary>
        public readonly List<sbyte> HalfSides = [];

        public bool Burning;
        public int BurnTimer;

        /// <summary>纸人显示与交互半尺寸</summary>
        public Vector2 PaperHalf => new(PaperWidth * 0.5f, PaperHeight * 0.5f);
        public bool IsArmed => State == OmokageState.Armed;
        public bool IsLive => State is OmokageState.PendingCapture or OmokageState.Separating or OmokageState.Armed;

        /// <summary>综合可见度、寿命尾段淡出 × 烧散 × 斩纸消散</summary>
        public float Alpha {
            get {
                float a = Reveal * (1f - MathHelper.Clamp(
                    (Timer - (Lifetime - OniOmokage.FadeFrames)) / (float)OniOmokage.FadeFrames, 0f, 1f));
                if (Burning) {
                    a *= 1f - MathHelper.Clamp(BurnTimer / (float)OniOmokage.BurnFrames, 0f, 1f);
                }
                if (Cut) {
                    a *= 1f - MathHelper.Clamp((SplitAge - OniOmokage.CutLingerFrames)
                        / (float)(OniOmokage.CutVanishFrames - OniOmokage.CutLingerFrames), 0f, 1f);
                }
                return a;
            }
        }

        /// <summary>墨晕溶解进度 0..1、随龄缓慢晕开，烧散时疾速推满</summary>
        public float Dissolve {
            get {
                float d = MathHelper.Clamp(Timer / (float)Lifetime, 0f, 1f) * 0.45f;
                if (Burning) {
                    d = MathF.Max(d, BurnTimer / (float)OniOmokage.BurnFrames);
                }
                return d;
            }
        }
    }

    /// <summary>飞向真身的赤线脉冲，到达帧结算肢解与伤害</summary>
    internal class OmokagePulse
    {
        public int NpcIndex;
        public int NpcType;
        public uint NpcSpawnToken;
        /// <summary>落刀点相对 npc.Center 的偏移（到达时以当时位置重算）</summary>
        public Vector2 BodyLocal;
        public float CutAngle;
        public int Timer;
        public int Travel;
        /// <summary>发射点（纸面落刀点世界坐标），绘制用</summary>
        public Vector2 StartWorld;
        public int Damage;
        public float Knockback;
        public int PlayerWhoAmI;

        public float Progress => MathHelper.Clamp(Timer / (float)Travel, 0f, 1f);
    }

    /// <summary>面影. 领域印记位水墨残影</summary>
    internal class OniOmokage : ICWRLoader
    {
        /// <summary>寿命尾段淡出帧数</summary>
        public const int FadeFrames = 30;
        /// <summary>离里/收域/断链的快速烧散帧数</summary>
        public const int BurnFrames = 20;
        /// <summary>斩纸后两半滑开的动画帧数</summary>
        public const int CutSlideFrames = 14;
        /// <summary>斩纸后纸片保持可见的帧数，随后开始消散</summary>
        public const int CutLingerFrames = 20;
        /// <summary>斩纸后纸片彻底移除的帧数</summary>
        public const int CutVanishFrames = 44;
        /// <summary>同目标两幅面影的最小间距（px），防原地叠影</summary>
        public const float MinImprintGap = 24f;

        /// <summary>默认寿命（帧），调试可改</summary>
        public static int Lifetime = 900;
        /// <summary>全局面影上限，超出移除最旧</summary>
        public static int MaxEchoes = 24;
        /// <summary>单目标面影上限，超出移除该目标最旧</summary>
        public static int PerNpcCap = 3;
        /// <summary>翻转入里时自动快门（调试开关）</summary>
        public static bool AutoShutterOnFlip = true;
        /// <summary>媒介再生成冷却（帧/每 NPC）、里世界维持循环的节奏阀</summary>
        public static int ReimprintCooldown = 120;
        /// <summary>挂新影失败（间距/容量）后的重试间隔（帧）</summary>
        private const int ReimprintRetry = 30;
        internal const int MaxCaptureFailures = 2;
        private const float SourceClearance = 24f;
        private const float PlacementGap = 2f;
        private const int SeparateMinFrames = 10;
        private const int SeparateMaxFrames = 18;
        private const float PaperFringePadding = 6f;

        /// <summary>所有活跃面影</summary>
        internal static readonly List<OmokageEntry> Entries = [];
        /// <summary>飞行中的传导脉冲</summary>
        internal static readonly List<OmokagePulse> Pulses = [];
        /// <summary>共享快照注册表（npcIndex → 快照）</summary>
        internal static readonly Dictionary<int, OmokageSnap> Snaps = [];
        //每 NPC 的再生成冷却计时（客户端本地，离里/清场清空）

        private static readonly Dictionary<int, int> reimprintTimers = [];
        //再生成计时器的周期性剔除暂存

        private static readonly List<int> reimprintPrune = [];
        private static int nextEntryId = 1;

        void ICWRLoader.UnLoadData() {
            Entries.Clear();
            Pulses.Clear();
            Main.QueueMainThreadAction(DisposeAllSnaps);
        }

        /// <summary>在 npc 当前位置挂一幅面影（快照捕获由渲染线程随后完成）；任意存活 NPC 均可，不分敌我</summary>
        public static bool Imprint(NPC npc) {
            if (Main.dedServ || npc == null || !npc.active || npc.life <= 0) {
                return false;
            }
            if (OnikiriTutorialTargetGlobal.IsTutorialTarget(npc, out _, out _)) {
                return false;
            }
            uint spawnToken = OniOmokageIdentity.GetToken(npc);

            //同目标近距离已有面影则不重复挂

            int perNpc = 0;
            OmokageEntry oldestOfNpc = null;
            foreach (OmokageEntry e in Entries) {
                if (e.NpcIndex != npc.whoAmI || e.NpcType != npc.type
                    || e.NpcSpawnToken != spawnToken) {
                    continue;
                }
                if (e.IsLive && Vector2.DistanceSquared(e.CaptureCenter, npc.Center) < MinImprintGap * MinImprintGap) {
                    return false;
                }
                perNpc++;
                if (oldestOfNpc == null || e.Timer > oldestOfNpc.Timer) {
                    oldestOfNpc = e;
                }
            }
            if (perNpc >= PerNpcCap && oldestOfNpc != null) {
                Entries.Remove(oldestOfNpc);
            }
            if (Entries.Count >= MaxEchoes) {
                RemoveOldest();
            }

            OmokageSnap snap = EnsureSnap(npc);
            if (snap == null) {
                return false;
            }

            Vector2 center = npc.Center;
            Entries.Add(new OmokageEntry {
                Id = AllocateEntryId(),
                NpcIndex = npc.whoAmI,
                NpcType = npc.type,
                NpcSpawnToken = spawnToken,
                AnchorCenter = center,
                RenderCenter = center,
                CaptureCenter = center,
                SnapWidth = snap.Width,
                SnapHeight = snap.Height,
                PaperWidth = snap.PaperWidth,
                PaperHeight = snap.PaperHeight,
                Lifetime = Math.Max(Lifetime, FadeFrames + 10),
                Seed = Main.rand.NextFloat(),
                SwayPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                State = OmokageState.PendingCapture,
            });
            return true;
        }

        /// <summary>纸人显示与交互尺寸、直接取当前 NPC 身形 AABB</summary>
        private static void ComputePaperSize(NPC npc, out float width, out float height) {
            Vector2 bodySize = OniDismember.ComputeBodySize(npc);
            float drawOffsetY = GetDrawOffsetY(npc);
            width = MathF.Max(bodySize.X + PaperFringePadding * 2f, 1f);
            height = MathF.Max(bodySize.Y + (PaperFringePadding + MathF.Abs(drawOffsetY)) * 2f, 1f);
        }

        private static float GetDrawOffsetY(NPC npc) {
            float offset = npc?.gfxOffY ?? 0f;
            if (npc?.ModNPC != null) {
                offset += npc.ModNPC.DrawOffsetY;
            }
            return float.IsFinite(offset) ? offset : 0f;
        }

        private static int AllocateEntryId() {
            int id = nextEntryId++;
            if (id == 0) {
                id = nextEntryId++;
            }
            return id;
        }

        internal static void ForgetReimprintTimer(int npcIndex) {
            reimprintTimers.Remove(npcIndex);
        }

        /// <summary>快门、屏内（含 200px 余量）全部存活 NPC 各挂一幅（不分敌我），返回成功数量</summary>
        public static int ImprintVisible() {
            if (Main.dedServ) {
                return 0;
            }
            Rectangle view = new((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200,
                Main.screenWidth + 400, Main.screenHeight + 400);
            int count = 0;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!view.Intersects(npc.Hitbox)) {
                    continue;
                }
                if (Imprint(npc)) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>斩击判定、线段扫过所有未斩纸面</summary>
        /// <param name="player">攻击发起者（伤害归属与震屏）</param>
        /// <param name="start">斩击线段起点（世界坐标）</param>
        /// <param name="end">斩击线段终点（世界坐标）</param>
        /// <param name="damage">到达帧对真身结算的伤害</param>
        /// <param name="knockback">击退</param>
        public static bool TryCut(Player player, Vector2 start, Vector2 end, int damage, float knockback) {
            if (Main.dedServ || player == null) {
                return false;
            }

            bool anyCut = false;
            foreach (OmokageEntry entry in Entries) {
                if (!entry.IsArmed || entry.Alpha < 0.35f) {
                    continue;
                }
                if (!SegmentIntersectsRect(start, end, entry.AnchorCenter, entry.PaperHalf, out Vector2 hitPoint)) {
                    continue;
                }
                //首幅独享终斩刀线，其余共享同一引爆节拍（防世界级效果同帧叠爆）

                CutEntry(player, entry, hitPoint, (end - start).ToRotation(), damage, knockback, leadFx: !anyCut);
                anyCut = true;
            }
            return anyCut;
        }

        /// <summary>技能定向斩纸、在 worldPoint 附近（容差内）找最近的未斩纸面并落刀</summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="worldPoint">落刀点（世界坐标）</param>
        /// <param name="cutAngle">切线角度（弧度）</param>
        /// <param name="damage">脉冲到达帧对真身结算的伤害</param>
        /// <param name="knockback">击退</param>
        /// <param name="tolerance">点到纸面矩形的最大距离（px）</param>
        public static bool SeverAt(Player player, Vector2 worldPoint, float cutAngle,
            int damage, float knockback, float tolerance = 90f) {
            if (Main.dedServ || player == null) {
                return false;
            }
            OmokageEntry entry = PickEntryNear(worldPoint, tolerance);
            if (entry == null) {
                return false;
            }
            CutEntry(player, entry, worldPoint, cutAngle, damage, knockback, leadFx: true);
            return true;
        }

        internal static bool SeverEntry(Player player, int entryId, Vector2 worldPoint,
            float cutAngle, int damage, float knockback) {
            if (Main.dedServ || player == null || entryId == 0) {
                return false;
            }
            foreach (OmokageEntry entry in Entries) {
                if (entry.Id != entryId || !entry.IsArmed || entry.Alpha < 0.35f) {
                    continue;
                }
                CutEntry(player, entry, worldPoint, cutAngle, damage, knockback, leadFx: true);
                return true;
            }
            return false;
        }

        /// <summary>离 point 最近的可斩纸面（点到纸面矩形距离 ≤ pad）</summary>
        public static OmokageEntry PickEntryNear(Vector2 point, float pad) {
            OmokageEntry best = null;
            float bestD = float.MaxValue;
            foreach (OmokageEntry entry in Entries) {
                if (!entry.IsArmed || entry.Alpha < 0.35f) {
                    continue;
                }
                float d = DistanceToRect(point, entry.AnchorCenter, entry.PaperHalf);
                if (d <= pad && d < bestD) {
                    bestD = d;
                    best = entry;
                }
            }
            return best;
        }

        /// <summary>点到轴对齐矩形的距离（矩形内为 0）</summary>
        internal static float DistanceToRect(Vector2 point, Vector2 rectCenter, Vector2 rectHalf) {
            Vector2 d = point - rectCenter;
            Vector2 clamped = new(MathHelper.Clamp(d.X, -rectHalf.X, rectHalf.X)
                , MathHelper.Clamp(d.Y, -rectHalf.Y, rectHalf.Y));
            return Vector2.Distance(d, clamped);
        }

        /// <summary>清空全部面影、脉冲与快照</summary>
        public static void Clear() {
            Entries.Clear();
            Pulses.Clear();
            reimprintTimers.Clear();
            DisposeAllSnaps();
        }

        /// <summary>清掉指定 NPC 槽的面影、脉冲与快照</summary>
        internal static void ReleaseTarget(NPC npc) {
            if (npc == null) {
                return;
            }
            int npcIndex = npc.whoAmI;
            Entries.RemoveAll(entry => entry.NpcIndex == npcIndex);
            Pulses.RemoveAll(pulse => pulse.NpcIndex == npcIndex);
            reimprintTimers.Remove(npcIndex);
            if (Snaps.Remove(npcIndex, out OmokageSnap snap)) {
                snap.RT?.Dispose();
            }
        }

        /// <summary>离开里世界（翻回表/收域）、全部面影快速烧散</summary>
        internal static void BurnAll() {
            foreach (OmokageEntry entry in Entries) {
                StartBurn(entry);
            }
        }

        private static void StartBurn(OmokageEntry entry) {
            if (!entry.Burning) {
                entry.Burning = true;
                entry.BurnTimer = 0;
                entry.State = OmokageState.Burning;
                entry.StateTimer = 0;
            }
        }

        private static void RemoveOldest() {
            int oldestIdx = -1;
            int oldestTimer = -1;
            for (int i = 0; i < Entries.Count; i++) {
                if (Entries[i].Timer > oldestTimer) {
                    oldestTimer = Entries[i].Timer;
                    oldestIdx = i;
                }
            }
            if (oldestIdx >= 0) {
                Entries.RemoveAt(oldestIdx);
            }
        }

        private static void CutEntry(Player player, OmokageEntry entry, Vector2 hitWorld,
            float cutAngle, int damage, float knockback, bool leadFx) {

            if (!entry.IsArmed) {
                return;
            }

            //落刀点收拢进纸面有效范围，保证裁剪线穿过纸张

            Vector2 half = entry.PaperHalf;
            Vector2 local = hitWorld - entry.AnchorCenter;
            local.X = MathHelper.Clamp(local.X, -half.X * 0.4f, half.X * 0.4f);
            local.Y = MathHelper.Clamp(local.Y, -half.Y * 0.4f, half.Y * 0.4f);

            entry.Cut = true;
            entry.State = OmokageState.Cutting;
            entry.StateTimer = 0;
            entry.CutLocal = local;
            entry.CutAngle = cutAngle;
            entry.CutAge = 0;
            entry.CutDelay = OniFinaleCut.HoldFrames;
            entry.PendingDamage = damage;
            entry.PendingKnockback = knockback;
            entry.PendingPlayer = player.whoAmI;
            BuildHalves(entry);

            //纸上落刀点起终斩刀线、滞拍 → 纳刀引爆，纸在引爆帧才真正裂开；

            //伤害走脉冲端结算，刀线零伤害纯演出

            if (leadFx && player.whoAmI == Main.myPlayer) {
                OniFinaleCut.Fire(player, entry.AnchorCenter + local, cutAngle, 0, 0f);
            }
            Inscriptions.Deeds.OniMeiDeedEvents.NotifyOmokageSever(player);

            if (entry.CutDelay <= 0) {
                OnPaperSplit(entry);
            }
        }

        /// <summary>纸面真正裂开的一帧（刀线引爆帧）、撕裂声画迸发，赤线脉冲此刻才启程</summary>
        private static void OnPaperSplit(OmokageEntry entry) {
            //纸裂、与纸层剥落同源的撕裂声 + 沿刀线迸出纸屑碎晶

            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.6f, MaxInstances = 3 }, entry.AnchorCenter);
            SpawnCutScraps(entry);

            //赤线脉冲、距离越远飞得越久，clamp 6~14 帧

            NPC npc = ValidTarget(entry.NpcIndex, entry.NpcType, entry.NpcSpawnToken);
            if (npc == null || entry.PendingPlayer < 0) {
                return;   //因果已断、纸裂而无处传导

            }
            float dist = Vector2.Distance(entry.AnchorCenter, npc.Center);
            Pulses.Add(new OmokagePulse {
                NpcIndex = entry.NpcIndex,
                NpcType = entry.NpcType,
                NpcSpawnToken = entry.NpcSpawnToken,
                BodyLocal = entry.CutLocal,
                CutAngle = entry.CutAngle,
                Travel = (int)MathHelper.Clamp(dist / 24f, 6f, 14f),
                StartWorld = entry.AnchorCenter + entry.CutLocal,
                Damage = entry.PendingDamage,
                Knockback = entry.PendingKnockback,
                PlayerWhoAmI = entry.PendingPlayer,
            });
            //发射帧单声风铃、因果启程

            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 2 }, entry.AnchorCenter);
        }

        /// <summary>斩纸碎屑、和纸屑为主、鬼红碎晶点缀，沿刀线两侧迸出（与肢解断口同语汇）</summary>
        private static void SpawnCutScraps(OmokageEntry entry) {
            Vector2 dir = entry.CutAngle.ToRotationVector2();
            Vector2 nrm = new(-dir.Y, dir.X);
            if (!ClipLineToRect(entry.CutLocal, dir, entry.PaperHalf, out float t0, out float t1)) {
                t0 = -20f;
                t1 = 20f;
            }

            for (int k = 0; k < 12; k++) {
                Vector2 pos = entry.AnchorCenter + entry.CutLocal
                    + dir * MathHelper.Lerp(t0, t1, Main.rand.NextFloat()) * 0.85f;
                Vector2 vel = nrm * Main.rand.NextFloat(1.2f, 3.6f) * (Main.rand.NextBool() ? 1f : -1f)
                    + dir * Main.rand.NextFloat(-0.8f, 0.8f);
                Color c = Main.rand.NextBool(3) ? new Color(214, 36, 28) : new Color(233, 224, 202);
                PRTLoader.NewParticle<PRT_OniShard>(pos, vel, c, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.Next(16, 26), Main.rand.NextFloat(-0.2f, 0.2f),
                        Main.rand.NextFloat(1.2f, 2.0f), affectedByGravity: true);
            }
        }

        /// <summary>过 point 沿 dir 的无限直线与中心在原点的矩形求交</summary>
        internal static bool ClipLineToRect(Vector2 point, Vector2 dir, Vector2 rectHalf, out float t0, out float t1) {
            t0 = float.MinValue;
            t1 = float.MaxValue;
            for (int axis = 0; axis < 2; axis++) {
                float p = axis == 0 ? dir.X : dir.Y;
                float o = axis == 0 ? point.X : point.Y;
                float half = axis == 0 ? rectHalf.X : rectHalf.Y;

                if (MathF.Abs(p) < 1e-5f) {
                    if (MathF.Abs(o) > half) {
                        return false;
                    }
                    continue;
                }
                float tA = (-half - o) / p;
                float tB = (half - o) / p;
                if (tA > tB) {
                    (tA, tB) = (tB, tA);
                }
                t0 = MathF.Max(t0, tA);
                t1 = MathF.Min(t1, tB);
                if (t0 > t1) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>切线把整张纸裁成两半；退化情况（贴角掠过）保留整纸单片不滑动</summary>
        private static void BuildHalves(OmokageEntry entry) {
            entry.Halves.Clear();
            entry.HalfSides.Clear();

            Vector2 half = entry.PaperHalf;
            Vector2[] quad = [new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y)];
            Vector2 dir = entry.CutAngle.ToRotationVector2();
            Vector2 normal = new(-dir.Y, dir.X);

            List<Vector2> pos = OniDismember.ClipHalfPlane(quad, entry.CutLocal, normal, 1f);
            List<Vector2> neg = OniDismember.ClipHalfPlane(quad, entry.CutLocal, normal, -1f);

            if (pos.Count >= 3 && neg.Count >= 3
                && OniDismember.PolyArea(pos) >= 48f && OniDismember.PolyArea(neg) >= 48f) {
                entry.Halves.Add([.. pos]);
                entry.HalfSides.Add(1);
                entry.Halves.Add([.. neg]);
                entry.HalfSides.Add(-1);
            }
            else {
                entry.Halves.Add(quad);
                entry.HalfSides.Add(0);
            }
        }

        private static void UpdateAppearanceState(OmokageEntry entry, NPC npc) {
            if (entry.State == OmokageState.PendingCapture) {
                entry.StateTimer++;
                if (npc != null && Snaps.TryGetValue(entry.NpcIndex, out OmokageSnap snap)
                    && snap.NpcType == entry.NpcType && snap.NpcSpawnToken == entry.NpcSpawnToken
                    && snap.Captured) {
                    BeginSeparation(entry, npc);
                }
                return;
            }

            if (entry.State == OmokageState.Armed) {
                if (npc != null && OverlapsSource(entry, npc, entry.RenderCenter)) {
                    BeginSeparation(entry, npc);
                }
                return;
            }

            if (entry.State != OmokageState.Separating) {
                return;
            }

            entry.StateTimer++;
            float progress = MathHelper.Clamp(entry.StateTimer / (float)Math.Max(entry.SeparationDuration, 1), 0f, 1f);
            float eased = OniFinaleRenderer.EaseOutCubic(progress);
            entry.RenderCenter = Vector2.Lerp(entry.SeparationStart, entry.AnchorCenter, eased);
            entry.Reveal = MathHelper.Lerp(entry.SeparationRevealStart, 1f, eased);
            float developEase = MathHelper.Clamp((progress - 0.15f) / 0.85f, 0f, 1f);
            entry.Develop = MathHelper.Lerp(entry.SeparationDevelopStart, 1f, developEase);
            if (progress < 1f) {
                return;
            }

            entry.RenderCenter = entry.AnchorCenter;
            entry.Reveal = 1f;
            entry.Develop = 1f;
            if (npc == null) {
                StartBurn(entry);
                return;
            }
            if (OverlapsSource(entry, npc, entry.RenderCenter)) {
                BeginSeparation(entry, npc);
                return;
            }

            entry.State = OmokageState.Armed;
            entry.StateTimer = 0;
        }

        private static void BeginSeparation(OmokageEntry entry, NPC npc) {
            Vector2 destination = entry.CaptureCenter;
            if (OverlapsSource(entry, npc, destination)) {
                destination = PickSeparatedCenter(entry, npc);
            }

            entry.State = OmokageState.Separating;
            entry.StateTimer = 0;
            entry.SeparationStart = entry.RenderCenter;
            entry.AnchorCenter = destination;
            float distance = Vector2.Distance(entry.SeparationStart, destination);
            entry.SeparationDuration = Math.Clamp((int)MathF.Ceiling(distance / 12f), SeparateMinFrames, SeparateMaxFrames);
            entry.SeparationRevealStart = entry.Reveal;
            entry.SeparationDevelopStart = entry.Develop;
        }

        private static Vector2 PickSeparatedCenter(OmokageEntry entry, NPC npc) {
            Rectangle sourceBounds = npc.Hitbox;
            sourceBounds.Inflate((int)SourceClearance, (int)SourceClearance);
            Vector2 sourceCenter = new(sourceBounds.Center.X, sourceBounds.Center.Y);
            float sideDistance = sourceBounds.Width * 0.5f + entry.PaperHalf.X + PlacementGap;
            float topDistance = sourceBounds.Height * 0.5f + entry.PaperHalf.Y + PlacementGap;
            Span<Vector2> candidates = stackalloc Vector2[3] {
                sourceCenter - Vector2.UnitX * sideDistance,
                sourceCenter + Vector2.UnitX * sideDistance,
                sourceCenter - Vector2.UnitY * topDistance,
            };

            Vector2 best = candidates[0];
            float bestScore = float.MinValue;
            for (int i = 0; i < candidates.Length; i++) {
                float score = ScoreSeparatedCenter(entry, npc, candidates[i], sourceBounds);
                if (score > bestScore) {
                    bestScore = score;
                    best = candidates[i];
                }
            }
            return best;
        }

        private static float ScoreSeparatedCenter(OmokageEntry entry, NPC npc, Vector2 candidate,
            Rectangle sourceBounds) {
            Rectangle bounds = GetPaperBounds(candidate, entry.PaperHalf);
            if (bounds.Intersects(sourceBounds)) {
                return float.MinValue;
            }

            Rectangle view = new((int)Main.screenPosition.X, (int)Main.screenPosition.Y,
                Main.screenWidth, Main.screenHeight);
            Rectangle visible = Rectangle.Intersect(bounds, view);
            float area = Math.Max((float)bounds.Width * bounds.Height, 1f);
            float visibleArea = Math.Max((float)visible.Width * visible.Height, 0f);
            float score = visibleArea / area * 1000f;
            if (view.Left <= bounds.Left && view.Right >= bounds.Right
                && view.Top <= bounds.Top && view.Bottom >= bounds.Bottom) {
                score += 300f;
            }

            foreach (OmokageEntry other in Entries) {
                if (ReferenceEquals(entry, other)) {
                    continue;
                }
                Vector2 otherCenter = other.State == OmokageState.Separating
                    ? other.AnchorCenter : other.RenderCenter;
                Rectangle otherBounds = GetPaperBounds(otherCenter, other.PaperHalf);
                otherBounds.Inflate(12, 12);
                Rectangle overlap = Rectangle.Intersect(bounds, otherBounds);
                score -= (float)overlap.Width * overlap.Height / area * 1800f;
            }

            float away = MathF.Sign(npc.Center.X - Main.LocalPlayer.Center.X);
            if (away != 0f && MathF.Sign(candidate.X - npc.Center.X) == away) {
                score += 40f;
            }
            return score;
        }

        private static bool OverlapsSource(OmokageEntry entry, NPC npc, Vector2 center) {
            Rectangle sourceBounds = npc.Hitbox;
            sourceBounds.Inflate((int)SourceClearance, (int)SourceClearance);
            return GetPaperBounds(center, entry.PaperHalf).Intersects(sourceBounds);
        }

        private static Rectangle GetPaperBounds(Vector2 center, Vector2 half) {
            int left = (int)MathF.Floor(center.X - half.X);
            int top = (int)MathF.Floor(center.Y - half.Y);
            int right = (int)MathF.Ceiling(center.X + half.X);
            int bottom = (int)MathF.Ceiling(center.Y + half.Y);
            return new Rectangle(left, top, Math.Max(right - left, 1), Math.Max(bottom - top, 1));
        }

        /// <summary>逐帧(客户端)、挂 PostUpdateEverything</summary>
        internal static void Update() {
            UpdatePulses();
            UpdateReimprint();

            for (int i = Entries.Count - 1; i >= 0; i--) {
                OmokageEntry entry = Entries[i];
                NPC npc = ValidTarget(entry.NpcIndex, entry.NpcType, entry.NpcSpawnToken);
                if (!entry.Burning && npc == null) {
                    StartBurn(entry);
                }
                UpdateAppearanceState(entry, npc);
                if (entry.State != OmokageState.PendingCapture) {
                    entry.Timer++;
                }

                if (entry.Cut) {
                    entry.CutAge++;
                    if (entry.SplitAge == 0) {
                        //刀线引爆帧、纸裂 + 脉冲启程

                        OnPaperSplit(entry);
                    }
                    if (entry.SplitAge >= CutVanishFrames) {
                        Entries.RemoveAt(i);
                        continue;
                    }
                }

                if (entry.Burning) {
                    entry.BurnTimer++;
                    if (entry.BurnTimer >= BurnFrames) {
                        Entries.RemoveAt(i);
                        continue;
                    }
                }
                else if (entry.Timer >= entry.Lifetime) {
                    Entries.RemoveAt(i);
                }
            }
        }

        /// <summary>里世界媒介维持循环、敌人会留下媒介，被用掉的媒介过冷却后再留一幅新的。 条件</summary>
        private static void UpdateReimprint() {
            OniDomainPlayer domain = OniDomain.Local;
            if (domain == null || domain.Phase != OniDomainPhase.Ura || !domain.WorldIsUra) {
                if (reimprintTimers.Count > 0) {
                    reimprintTimers.Clear();
                }
                return;
            }

            Rectangle view = new((int)Main.screenPosition.X - 200, (int)Main.screenPosition.Y - 200,
                Main.screenWidth + 400, Main.screenHeight + 400);

            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.life <= 0 || !npc.CanBeChasedBy() || !view.Intersects(npc.Hitbox)) {
                    continue;
                }
                if (CWRLoad.WormBodys.Contains(npc.type)) {
                    continue;
                }
                //僵直中的尸身不留新影、解除定格、重新动起来后冷却才开始走

                if (OniDismember.IsLocked(npc.whoAmI)) {
                    reimprintTimers[npc.whoAmI] = ReimprintCooldown;
                    continue;
                }
                if (HasLivePaper(npc)) {
                    reimprintTimers.Remove(npc.whoAmI);
                    continue;
                }

                int t = reimprintTimers.TryGetValue(npc.whoAmI, out int v) ? v : ReimprintCooldown;
                if (--t <= 0) {
                    if (Imprint(npc)) {
                        reimprintTimers.Remove(npc.whoAmI);
                        continue;
                    }
                    t = ReimprintRetry;   //间距/容量受限，稍后重试

                }
                reimprintTimers[npc.whoAmI] = t;
            }

            //周期性剔除失效槽位的残留计时

            if (Main.GameUpdateCount % 60 == 0 && reimprintTimers.Count > 0) {
                reimprintPrune.Clear();
                foreach (int index in reimprintTimers.Keys) {
                    if (index < 0 || index >= Main.maxNPCs || !Main.npc[index].active) {
                        reimprintPrune.Add(index);
                    }
                }
                foreach (int index in reimprintPrune) {
                    reimprintTimers.Remove(index);
                }
            }
        }

        /// <summary>该 NPC 是否还挂着"活的"（未斩未烧）面影</summary>
        private static bool HasLivePaper(NPC npc) {
            foreach (OmokageEntry entry in Entries) {
                if (entry.NpcIndex == npc.whoAmI && entry.NpcType == npc.type
                    && entry.NpcSpawnToken == OniOmokageIdentity.GetToken(npc)
                    && entry.IsLive) {
                    return true;
                }
            }
            return false;
        }

        private static void UpdatePulses() {
            for (int i = Pulses.Count - 1; i >= 0; i--) {
                OmokagePulse pulse = Pulses[i];
                pulse.Timer++;
                if (pulse.Timer < pulse.Travel) {
                    continue;
                }
                Pulses.RemoveAt(i);

                NPC npc = ValidTarget(pulse.NpcIndex, pulse.NpcType, pulse.NpcSpawnToken);
                if (npc == null) {
                    continue;   //因果落空，脉冲无声消散

                }

                //到达帧、切口按落刀点 1:1 映射到身体 + 伤害结算；

                //真身只承接纸面映射出的局部刀路；同组其余体节保持停止，不复制切口
                Vector2 cutCenter = npc.Center + pulse.BodyLocal;
                DismemberStroke stroke = new(cutCenter, pulse.CutAngle,
                    MathF.Max(npc.Size.Length(), 64f), OniFinaleCut.VisualPathWidth);
                OniDismember.TriggerGroup(npc, in stroke, holdFrames: 0);

                Player player = Main.player[pulse.PlayerWhoAmI];
                if (player != null && player.active && pulse.Damage > 0) {
                    int hitDirection = MathF.Cos(pulse.CutAngle) >= 0f ? 1 : -1;
                    player.ApplyDamageToNPC(npc, pulse.Damage, pulse.Knockback, hitDirection, false);
                }

                //太鼓闷击 + 震屏、因果落地

                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.5f, Pitch = -0.8f, MaxInstances = 2 }, npc.Center);
                if (pulse.PlayerWhoAmI == Main.myPlayer) {
                    Main.LocalPlayer.CWR().GetScreenShake(3f);
                }
            }
        }

        /// <summary>绑定目标的存活实例，死亡/槽位复用返回 null</summary>
        internal static NPC ValidTarget(int npcIndex, int npcType, uint spawnToken) {
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[npcIndex];
            return npc.active && npc.type == npcType
                && OniOmokageIdentity.GetToken(npc) == spawnToken ? npc : null;
        }

        private static OmokageSnap EnsureSnap(NPC npc) {
            uint spawnToken = OniOmokageIdentity.GetToken(npc);
            if (Snaps.TryGetValue(npc.whoAmI, out OmokageSnap snap)
                && snap.NpcType == npc.type && snap.NpcSpawnToken == spawnToken) {
                return snap;
            }
            snap?.RT?.Dispose();

            snap = new OmokageSnap {
                NpcType = npc.type,
                NpcSpawnToken = spawnToken,
            };
            UpdateSnapAppearance(npc, snap);
            Snaps[npc.whoAmI] = snap;
            return snap;
        }

        internal static void RefreshSnapForCapture(NPC npc, OmokageSnap snap) {
            if (npc == null || snap == null || snap.Captured || snap.NpcType != npc.type
                || snap.NpcSpawnToken != OniOmokageIdentity.GetToken(npc)) {
                return;
            }
            UpdateSnapAppearance(npc, snap);
            foreach (OmokageEntry entry in Entries) {
                if (entry.NpcIndex != npc.whoAmI || entry.NpcType != npc.type
                    || entry.NpcSpawnToken != snap.NpcSpawnToken) {
                    continue;
                }
                entry.SnapWidth = snap.Width;
                entry.SnapHeight = snap.Height;
                entry.PaperWidth = snap.PaperWidth;
                entry.PaperHeight = snap.PaperHeight;
            }
        }

        private static void UpdateSnapAppearance(NPC npc, OmokageSnap snap) {
            OniDismember.ComputeSnapSize(npc, out snap.Width, out snap.Height);
            ComputePaperSize(npc, out snap.PaperWidth, out snap.PaperHeight);
            snap.SourceFrame = GetSourceFrame(npc);
            float sourceScale = MathF.Abs(npc.scale);
            if (!float.IsFinite(sourceScale) || sourceScale <= 0.001f) {
                sourceScale = 1f;
            }
            snap.SourceScale = sourceScale;
            snap.SourceRotation = float.IsFinite(npc.rotation) ? npc.rotation : 0f;
            snap.SourceDrawOffsetY = GetDrawOffsetY(npc);
            snap.SourceEffects = npc.spriteDirection == -1
                ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        }

        private static Rectangle GetSourceFrame(NPC npc) {
            Main.instance.LoadNPC(npc.type);
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            if (frame.Width > 0 && frame.Height > 0 && frame.Left >= 0 && frame.Top >= 0
                && frame.Width <= texture.Width && frame.Height <= texture.Height
                && frame.Left <= texture.Width - frame.Width
                && frame.Top <= texture.Height - frame.Height) {
                return frame;
            }

            int frames = Math.Max(Main.npcFrameCount[npc.type], 1);
            float scale = MathF.Abs(npc.scale);
            if (!float.IsFinite(scale) || scale <= 0.001f) {
                scale = 1f;
            }
            int frameHeight = Math.Max(texture.Height / frames, 1);
            float conservativeWidth = MathF.Min(MathF.Max(npc.width / scale, 1f) * 2f, texture.Width);
            float conservativeHeight = MathF.Min(MathF.Max(npc.height / scale, 1f) * 2f, frameHeight);
            int width = Math.Clamp((int)MathF.Ceiling(conservativeWidth), 1, texture.Width);
            int height = Math.Clamp((int)MathF.Ceiling(conservativeHeight), 1, texture.Height);
            int left = Math.Clamp(frame.Left, 0, Math.Max(texture.Width - width, 0));
            int top = Math.Clamp(frame.Top, 0, Math.Max(texture.Height - height, 0));
            return new Rectangle(left, top, width, height);
        }

        internal static void DisposeAllSnaps() {
            foreach (OmokageSnap snap in Snaps.Values) {
                snap.RT?.Dispose();
            }
            Snaps.Clear();
        }

        /// <summary>线段 vs 轴对齐矩形（Liang–Barsky），命中返回穿越段中点作落刀点</summary>
        private static bool SegmentIntersectsRect(Vector2 start, Vector2 end,
            Vector2 rectCenter, Vector2 rectHalf, out Vector2 hitPoint) {

            hitPoint = default;
            Vector2 d = end - start;
            Vector2 min = rectCenter - rectHalf;
            Vector2 max = rectCenter + rectHalf;
            float t0 = 0f, t1 = 1f;

            for (int axis = 0; axis < 2; axis++) {
                float p = axis == 0 ? d.X : d.Y;
                float o = axis == 0 ? start.X : start.Y;
                float lo = axis == 0 ? min.X : min.Y;
                float hi = axis == 0 ? max.X : max.Y;

                if (MathF.Abs(p) < 1e-5f) {
                    if (o < lo || o > hi) {
                        return false;
                    }
                    continue;
                }
                float tA = (lo - o) / p;
                float tB = (hi - o) / p;
                if (tA > tB) {
                    (tA, tB) = (tB, tA);
                }
                t0 = MathF.Max(t0, tA);
                t1 = MathF.Min(t1, tB);
                if (t0 > t1) {
                    return false;
                }
            }

            hitPoint = start + d * ((t0 + t1) * 0.5f);
            return true;
        }
    }

    internal sealed class OniOmokageIdentity : GlobalNPC
    {
        private static uint nextSpawnToken = 1;

        internal uint SpawnToken;

        public override bool InstancePerEntity => true;

        public override void OnSpawn(NPC npc, IEntitySource source) {
            SpawnToken = AllocateSpawnToken();
            OniOmokage.ForgetReimprintTimer(npc.whoAmI);
        }

        internal static uint GetToken(NPC npc) {
            if (npc == null) {
                return 0;
            }
            OniOmokageIdentity identity = npc.GetGlobalNPC<OniOmokageIdentity>();
            if (identity.SpawnToken == 0) {
                identity.SpawnToken = AllocateSpawnToken();
            }
            return identity.SpawnToken;
        }

        private static uint AllocateSpawnToken() {
            uint token = nextSpawnToken++;
            if (token == 0) {
                token = nextSpawnToken++;
            }
            return token;
        }
    }

    /// <summary>面影逐帧维护与世界卸载清理</summary>
    internal sealed class OniOmokageSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            OniOmokage.Update();
        }

        public override void ClearWorld() {
            if (Main.dedServ) {
                return;
            }
            OniOmokage.Clear();
        }
    }
}
