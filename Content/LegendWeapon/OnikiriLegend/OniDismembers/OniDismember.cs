using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers
{
    /// <summary>肢解切口、快照中心局部像素系下的一条无限直线</summary>
    internal struct DismemberCut
    {
        /// <summary>切线上一点（快照中心局部像素）</summary>
        public Vector2 PointLocal;
        /// <summary>单位法线，碎片沿 ±法线分离</summary>
        public Vector2 Normal;
        /// <summary>切口出生时刻（entry.Timer 时基）；可为未来帧</summary>
        public int Birth;
        /// <summary>本切口滞拍帧数（亮起 → 分离），供触发方与外层斩切演出对齐；0=立即分离</summary>
        public int Hold;
    }

    /// <summary>一次肢解演出的有限世界空间刀路</summary>
    internal readonly struct DismemberStroke
    {
        public readonly Vector2 Center;
        public readonly float Angle;
        public readonly float HalfLength;
        public readonly float Width;

        public DismemberStroke(Vector2 center, float angle, float halfLength, float width) {
            Center = center;
            Angle = MathHelper.WrapAngle(angle);
            HalfLength = Math.Max(halfLength, 1f);
            Width = Math.Max(width, 1f);
        }

        public Vector2 Direction => Angle.ToRotationVector2();
        public Vector2 Start => Center - Direction * HalfLength;
        public Vector2 End => Center + Direction * HalfLength;
    }

    /// <summary>肢解碎片、快照 quad 被切割线裁出的凸多边形</summary>
    internal class DismemberPiece
    {
        /// <summary>顶点（快照中心局部像素，恒为未位移的原始身体空间）</summary>
        public Vector2[] Verts;
        public Vector2 Centroid;
        /// <summary>与 <see cref="DismemberEntry.Cuts"/> 对齐</summary>
        public readonly List<sbyte> CutSides = [];
        /// <summary>与 Cuts 对齐的分离旋转量（弧度，含符号），继承自父片避免新刀导致旧旋转跳变</summary>
        public readonly List<float> CutSpins = [];
        public float JitterPhase;
    }

    /// <summary>单个被肢解 NPC 的视觉状态</summary>
    internal class DismemberEntry
    {
        public int NpcIndex;
        public int NpcType;          //槽位复用校验

        public int Timer;
        public int Duration;
        public float Seed;
        /// <summary>定身锚点，或仅视觉模式下随目标中心移动的绘制锚点。</summary>
        public Vector2 AnchorCenter;
        /// <summary>仅替换外观，不创建锁定状态；用于不能冻结 AI 的 Boss。</summary>
        public bool VisualOnly;
        public bool BehindTiles;
        /// <summary>快照 RT 像素尺寸，0=服务器/未初始化（无视觉）</summary>
        public int SnapWidth;
        public int SnapHeight;
        /// <summary>渲染端完成快照捕获后置位，此前本体照常绘制</summary>
        public bool Captured;
        public int CaptureFailures;
        public float DriftMax;
        public readonly List<DismemberCut> Cuts = [];
        public readonly List<DismemberPiece> Pieces = [];

        /// <summary>尾段整体淡出 0..1</summary>
        public float FadeAlpha => 1f - MathHelper.Clamp(
            (Timer - (Duration - OniDismember.FadeFrames)) / (float)OniDismember.FadeFrames, 0f, 1f);
    }

    /// <summary>鬼切肢解主控. 切断后锁关节</summary>
    internal class OniDismember : ICWRLoader
    {
        /// <summary>单个 NPC 的停止状态，不代表存在切口视觉</summary>
        private sealed class DismemberLockEntry
        {
            public int NpcIndex;
            public int NpcType;
            public int Timer;
            public int Duration;
            public Vector2 AnchorCenter;
            public TimeFreezeLease FreezeLease;
        }

        /// <summary>默认最大切口数</summary>
        public const int DefaultMaxCuts = 16;
        /// <summary>默认碎片总数上限，足以容纳 16 条直线在矩形内产生的理论最大分区</summary>
        public const int DefaultMaxPieces = 160;
        private static int maxCuts = DefaultMaxCuts;
        private static int maxPieces = DefaultMaxPieces;
        /// <summary>最大切口数，可在运行时调整；已存在的切口不受下调影响</summary>
        public static int MaxCuts {
            get => maxCuts;
            set => maxCuts = Math.Max(value, 1);
        }
        /// <summary>碎片总数上限，可在运行时调整；已存在的碎片不受下调影响</summary>
        public static int MaxPieces {
            get => maxPieces;
            set => maxPieces = Math.Max(value, 2);
        }
        /// <summary>滞拍帧数、切口亮起 → 碎片开始分离的间隔（居合语法、斩击已完成，世界还没反应过来）</summary>
        public const int HoldFrames = 12;
        /// <summary>分离滑开动画帧数</summary>
        public const int SeparateFrames = 26;
        /// <summary>结束淡出帧数</summary>
        public const int FadeFrames = 24;
        /// <summary>默认肢解持续帧数</summary>
        public const int DefaultDuration = 300;
        /// <summary>波及传播速度 px/帧</summary>
        public const float WaveSpeed = 120f;
        //小于该面积(px²)的裁剪结果视为未切中，不产生碎片

        private const float MinPieceArea = 24f;
        //同帧引爆的成员共享一声脆响与碎晶总预算，防蠕虫全身齐崩时 80 连音爆

        private const int ShardBudgetPerTick = 120;

        /// <summary>所有活跃视觉肢解状态</summary>
        internal static readonly List<DismemberEntry> Entries = [];
        /// <summary>所有活跃停止状态，群组中未被刀路触及的体节只进入此列表</summary>
        private static readonly List<DismemberLockEntry> lockEntries = [];
        //TriggerGroup 群组收集复用容器

        private static readonly List<NPC> groupScratch = [];
        private static uint lastBurstTick;
        private static int shardsThisTick;
        /// <summary>快照 RT 注册表（npcIndex → RT）</summary>
        internal static readonly Dictionary<int, RenderTarget2D> SnapRTs = [];

        void ICWRLoader.UnLoadData() {
            Clear();
            MaxCuts = DefaultMaxCuts;
            MaxPieces = DefaultMaxPieces;
        }

        /// <summary>肢解目标、切线过 npc.Center，角度为世界空间弧度</summary>
        public static bool Trigger(NPC npc, float cutAngle, int duration = DefaultDuration)
            => Trigger(npc, npc?.Center ?? Vector2.Zero, cutAngle, duration);

        /// <summary>肢解目标。首次调用建立定格并捕获快照；对已肢解目标重复调用则追加一条切口</summary>
        /// <param name="npc">目标 NPC（boss 亦可）</param>
        /// <param name="cutPointWorld">切线经过的世界坐标（会被收拢进身体范围）</param>
        /// <param name="cutAngle">切线方向（世界空间弧度）</param>
        /// <param name="duration">从当前帧起的持续帧数，尾段含 <see cref="FadeFrames"/> 帧淡出</param>
        /// <param name="holdFrames">本切口滞拍帧数（亮起 → 分离）；冻结与伤口亮线即刻建立，
        /// 分离推迟到滞拍结束，供外层斩切演出（如 <see cref="OniFinaleCut"/>）把引爆帧压到同一拍；0=立即分离</param>
        /// <param name="birthDelay">切口亮起延迟帧数、冻结与快照即刻建立，伤口线推迟出现</param>
        public static bool Trigger(NPC npc, Vector2 cutPointWorld, float cutAngle,
            int duration = DefaultDuration, int holdFrames = HoldFrames, int birthDelay = 0) {
            if (npc == null || !npc.active) {
                return false;
            }

            holdFrames = Math.Max(holdFrames, 0);
            birthDelay = Math.Max(birthDelay, 0);
            int effectiveDuration = Math.Max(duration, birthDelay + holdFrames + FadeFrames);
            DismemberLockEntry lockEntry = ApplyLock(npc, effectiveDuration);
            return TriggerVisual(npc, lockEntry, cutPointWorld, cutAngle,
                duration, holdFrames, birthDelay);
        }

        /// <summary>
        /// 仅捕获并切分目标外观，不建立冻结锁；视觉锚点逐帧跟随目标中心。
        /// 用于 Boss 等不可中断 AI 的目标。
        /// </summary>
        public static bool TriggerVisualOnly(NPC npc, Vector2 cutPointWorld, float cutAngle,
            int duration, int holdFrames = HoldFrames) {
            if (npc == null || !npc.active) {
                return false;
            }

            holdFrames = Math.Max(holdFrames, 0);
            duration = Math.Max(duration, holdFrames + FadeFrames);
            DismemberEntry entry = GetEntry(npc.whoAmI);
            if (entry == null || entry.NpcType != npc.type) {
                if (entry != null) {
                    Entries.Remove(entry);
                }
                entry = CreateEntry(npc, npc.Center, duration, holdFrames);
                entry.VisualOnly = true;
                Entries.Add(entry);
            }
            else {
                entry.Duration = Math.Max(entry.Duration, entry.Timer + duration);
                if (entry.VisualOnly) {
                    entry.AnchorCenter = npc.Center;
                }
            }

            AddCut(entry, cutPointWorld, cutAngle, holdFrames);
            return true;
        }

        /// <summary>停止多实体目标，只在有限刀路实际触及的体节建立肢解视觉</summary>
        public static bool TriggerGroup(NPC npc, in DismemberStroke stroke,
            int duration = DefaultDuration, int holdFrames = HoldFrames) {
            if (npc == null || !npc.active) {
                return false;
            }

            NpcGroupHelper.CollectGroup(npc, groupScratch);
            if (groupScratch.Count <= 1) {
                Vector2 point = TryGetPathCutPoint(npc, in stroke, out Vector2 pathPoint)
                    ? pathPoint
                    : stroke.Center;
                return Trigger(npc, point, stroke.Angle, duration, holdFrames);
            }

            holdFrames = Math.Max(holdFrames, 0);
            Vector2 strokeCenter = stroke.Center;

            //落刀点附近的体节先建立视觉，快照捕获顺序保持稳定
            groupScratch.Sort((a, b) => Vector2.DistanceSquared(a.Center, strokeCenter)
                .CompareTo(Vector2.DistanceSquared(b.Center, strokeCenter)));

            int effectiveDuration = Math.Max(duration, holdFrames + FadeFrames);
            bool any = false;
            foreach (NPC member in groupScratch) {
                any |= ApplyLock(member, effectiveDuration) != null;
            }

            foreach (NPC member in groupScratch) {
                bool pathTouchesMember = TryGetPathCutPoint(member, in stroke, out Vector2 point);
                if (!pathTouchesMember && member.whoAmI != npc.whoAmI) {
                    continue;
                }
                if (!pathTouchesMember) {
                    point = stroke.Center;
                }

                int delay = 0;
                if (holdFrames > 1) {
                    delay = Math.Min((int)(Vector2.Distance(member.Center, stroke.Center) / WaveSpeed), holdFrames - 1);
                }

                DismemberLockEntry lockEntry = GetLockEntry(member.whoAmI);
                TriggerVisual(member, lockEntry, point, stroke.Angle,
                    duration, holdFrames - delay, delay);
            }
            groupScratch.Clear();
            return any;
        }

        private static bool TriggerVisual(NPC npc, DismemberLockEntry lockEntry,
            Vector2 cutPointWorld, float cutAngle, int duration, int holdFrames, int birthDelay) {
            if (lockEntry == null) {
                return false;
            }

            DismemberEntry entry = GetEntry(npc.whoAmI);
            if (entry == null || entry.NpcType != npc.type) {
                if (entry != null) {
                    Entries.Remove(entry);
                }
                entry = CreateEntry(npc, lockEntry.AnchorCenter, duration, birthDelay + holdFrames);
                Entries.Add(entry);
            }
            else {
                entry.Duration = Math.Max(entry.Duration
                    , entry.Timer + Math.Max(duration, birthDelay + holdFrames + FadeFrames));
            }

            entry.VisualOnly = false;
            entry.AnchorCenter = lockEntry.AnchorCenter;
            AddCut(entry, cutPointWorld, cutAngle, holdFrames, birthDelay);
            return true;
        }

        private static bool TryGetPathCutPoint(NPC npc, in DismemberStroke stroke, out Vector2 cutPoint) {
            Vector2 pathStart = stroke.Start;
            Vector2 pathEnd = stroke.End;
            float collisionPoint = 0f;
            if (!Collision.CheckAABBvLineCollision(npc.Hitbox.TopLeft(), npc.Hitbox.Size(),
                pathStart, pathEnd, stroke.Width, ref collisionPoint)) {
                cutPoint = default;
                return false;
            }

            Vector2 path = pathEnd - pathStart;
            float t = Vector2.Dot(npc.Center - pathStart, path) / path.LengthSquared();
            cutPoint = pathStart + path * MathHelper.Clamp(t, 0f, 1f);
            return true;
        }

        /// <summary>提前解除、进入淡出，随后自然恢复</summary>
        public static void Release(int npcIndex) {
            DismemberEntry entry = GetEntry(npcIndex);
            if (entry != null) {
                entry.Duration = Math.Min(entry.Duration, entry.Timer + FadeFrames);
            }
            DismemberLockEntry lockEntry = GetLockEntry(npcIndex);
            if (lockEntry != null) {
                lockEntry.Duration = Math.Min(lockEntry.Duration, lockEntry.Timer + FadeFrames);
            }
        }

        /// <inheritdoc cref="Release(int)"/>
        public static void Release(NPC npc) {
            if (npc != null) {
                Release(npc.whoAmI);
            }
        }

        /// <summary>立刻清掉指定 NPC 槽的冻结、分裂与快照</summary>
        internal static void ClearTarget(NPC npc) {
            if (npc == null) {
                return;
            }
            int npcIndex = npc.whoAmI;
            Entries.RemoveAll(entry => entry.NpcIndex == npcIndex);
            DismemberLockEntry lockEntry = GetLockEntry(npcIndex);
            if (lockEntry != null) {
                TimeFreezeSystem.ReleaseNPC(npc, lockEntry.FreezeLease);
            }
            lockEntries.RemoveAll(entry => entry.NpcIndex == npcIndex);
            if (SnapRTs.Remove(npcIndex, out RenderTarget2D snapshot)) {
                snapshot.SafeDispose();
            }
        }

        public static bool IsDismembered(int npcIndex) => GetEntry(npcIndex) != null;

        public static bool IsLocked(int npcIndex) {
            DismemberLockEntry entry = GetLockEntry(npcIndex);
            if (entry == null || npcIndex < 0 || npcIndex >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[npcIndex];
            return npc.active && npc.type == entry.NpcType
                && TimeFreezeSystem.IsLeaseActive(npc, entry.FreezeLease);
        }

        //==================== 目标资格与点选判定 ====================

        /// <summary>点选判定框相对数值碰撞箱的外扩比例，贴图普遍大于碰撞箱，按体型给容差</summary>
        private const float SelectionPadRatio = 0.35f;
        /// <summary>点选外扩的下限，小体型也得点得住</summary>
        private const float MinSelectionPad = 26f;
        /// <summary>点选外扩的上限，巨型 Boss 的判定框不至于罩住半个屏幕</summary>
        private const float MaxSelectionPad = 168f;

        /// <summary>
        /// 肢解资格。刻意比 <see cref="NPC.CanBeChasedBy"/> 宽：免伤、无敌、不可锁定、
        /// 低血上限的目标一律放行（这些状态只说明它打不动，不说明它斩不开），只挡友方与镇民。
        /// 点选、落刀与服务端复核共用这一条，三处口径不一致就会出现"点得到但斩不了"
        /// </summary>
        internal static bool CanBeSevered(NPC npc)
            => npc?.active == true && npc.life > 0 && !npc.friendly && !npc.isLikeATownNPC;

        /// <summary>
        /// 点选判定框的外扩量。只取 <see cref="NPC.width"/>/<see cref="NPC.height"/> 这类
        /// 服务端同样持有的数值，不读 <see cref="NPC.frame"/>：帧尺寸在服务端为空，
        /// 且多帧贴图的裁剪高度并不等于可见身体
        /// </summary>
        internal static float SelectionPad(NPC npc) {
            if (npc == null) {
                return 0f;
            }
            float span = MathF.Max(npc.width, npc.height);
            if (!float.IsFinite(span)) {
                return MinSelectionPad;
            }
            return MathHelper.Clamp(span * SelectionPadRatio, MinSelectionPad, MaxSelectionPad);
        }

        /// <summary>点到点选判定框的距离，框内为 0</summary>
        internal static float DistanceToSelection(NPC npc, Vector2 point) {
            if (npc == null) {
                return float.MaxValue;
            }
            float pad = SelectionPad(npc);
            Rectangle box = npc.Hitbox;
            Vector2 nearest = new(MathHelper.Clamp(point.X, box.Left - pad, box.Right + pad),
                MathHelper.Clamp(point.Y, box.Top - pad, box.Bottom + pad));
            return Vector2.Distance(point, nearest);
        }

        /// <summary>点是否落在数值碰撞箱本体内，用于"点准了"优先于"只沾到外扩边"</summary>
        internal static bool ContainsBody(NPC npc, Vector2 point) {
            if (npc == null) {
                return false;
            }
            Rectangle box = npc.Hitbox;
            return point.X >= box.Left && point.X <= box.Right
                && point.Y >= box.Top && point.Y <= box.Bottom;
        }

        /// <summary>立刻清空全部肢解状态（世界卸载兜底）</summary>
        public static void Clear() {
            foreach (DismemberLockEntry entry in lockEntries) {
                ReleaseLock(entry);
            }
            Entries.Clear();
            lockEntries.Clear();
            groupScratch.Clear();
            DisposeAllSnapshots();
        }

        internal static DismemberEntry GetEntry(int npcIndex) {
            for (int i = 0; i < Entries.Count; i++) {
                if (Entries[i].NpcIndex == npcIndex) {
                    return Entries[i];
                }
            }
            return null;
        }

        private static DismemberLockEntry GetLockEntry(int npcIndex) {
            for (int i = 0; i < lockEntries.Count; i++) {
                if (lockEntries[i].NpcIndex == npcIndex) {
                    return lockEntries[i];
                }
            }
            return null;
        }

        private static DismemberLockEntry ApplyLock(NPC npc, int duration) {
            DismemberLockEntry entry = GetLockEntry(npc.whoAmI);
            if (entry == null || entry.NpcType != npc.type
                || !TimeFreezeSystem.IsLeaseActive(npc, entry.FreezeLease)) {
                if (entry != null) {
                    lockEntries.Remove(entry);
                }
                entry = new DismemberLockEntry {
                    NpcIndex = npc.whoAmI,
                    NpcType = npc.type,
                    Duration = Math.Max(duration, FadeFrames),
                    AnchorCenter = npc.Center,
                };
                entry.FreezeLease = TimeFreezeSystem.AcquireNPC<OniDismember>(npc,
                    entry.AnchorCenter, npc.whoAmI, TimeFreezeAnchorPriority.Effect);
                lockEntries.Add(entry);
            }
            else {
                entry.Duration = Math.Max(entry.Duration, entry.Timer + duration);
            }

            return entry;
        }

        private static DismemberEntry CreateEntry(NPC npc, Vector2 anchorCenter, int duration, int holdFrames) {
            DismemberEntry entry = new() {
                NpcIndex = npc.whoAmI,
                NpcType = npc.type,
                Duration = Math.Max(duration, FadeFrames + holdFrames),
                Seed = Main.rand.NextFloat(),
                AnchorCenter = anchorCenter,
                BehindTiles = npc.behindTiles,
            };

            if (!Main.dedServ) {
                ComputeSnapSize(npc, out int snapW, out int snapH);
                entry.SnapWidth = snapW;
                entry.SnapHeight = snapH;
                entry.DriftMax = MathHelper.Clamp(MathF.Max(entry.SnapWidth, entry.SnapHeight) * 0.05f, 6f, 30f);

                float hw = entry.SnapWidth * 0.5f;
                float hh = entry.SnapHeight * 0.5f;
                entry.Pieces.Add(new DismemberPiece {
                    Verts = [new(-hw, -hh), new(hw, -hh), new(hw, hh), new(-hw, hh)],
                    Centroid = Vector2.Zero,
                    JitterPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                });
            }
            return entry;
        }

        private const float CapturePaddingRatio = 0.25f;
        private const float MinCapturePadding = 24f;
        private const float MaxCapturePadding = 160f;
        private const int MinCaptureDimension = 64;
        private const int MaxCaptureDimension = 1600;

        /// <summary>当前姿态的可视身体轴对齐尺寸，不含捕获留白</summary>
        internal static Vector2 ComputeBodySize(NPC npc) {
            if (npc == null) {
                return Vector2.Zero;
            }

            Vector2 hitboxSize = new(MathF.Max(npc.width, 0f), MathF.Max(npc.height, 0f));
            Vector2 frameSize = ResolveCurrentFrameSize(npc, hitboxSize);

            float scale = MathF.Abs(npc.scale);
            if (!float.IsFinite(scale)) {
                scale = 1f;
            }
            float spriteWidth = frameSize.X * scale;
            float spriteHeight = frameSize.Y * scale;

            float rotation = float.IsFinite(npc.rotation) ? npc.rotation : 0f;
            float cos = MathF.Abs(MathF.Cos(rotation));
            float sin = MathF.Abs(MathF.Sin(rotation));
            float rotatedWidth = spriteWidth * cos + spriteHeight * sin;
            float rotatedHeight = spriteWidth * sin + spriteHeight * cos;

            float width = MathF.Max(rotatedWidth, hitboxSize.X);
            float height = MathF.Max(rotatedHeight, hitboxSize.Y);
            if (!float.IsFinite(width)) {
                width = hitboxSize.X;
            }
            if (!float.IsFinite(height)) {
                height = hitboxSize.Y;
            }
            return new Vector2(MathF.Max(width, 1f), MathF.Max(height, 1f));
        }

        /// <summary>当前姿态的保守捕获 RT 尺寸</summary>
        internal static void ComputeSnapSize(NPC npc, out int width, out int height) {
            Vector2 bodySize = ComputeBodySize(npc);
            float padX = MathHelper.Clamp(bodySize.X * CapturePaddingRatio,
                MinCapturePadding, MaxCapturePadding);

            float drawOffsetY = npc?.gfxOffY ?? 0f;
            if (npc?.ModNPC != null) {
                drawOffsetY += npc.ModNPC.DrawOffsetY;
            }
            if (!float.IsFinite(drawOffsetY)) {
                drawOffsetY = 0f;
            }
            float padY = MathHelper.Clamp(bodySize.Y * CapturePaddingRatio + MathF.Abs(drawOffsetY),
                MinCapturePadding, MaxCapturePadding);

            width = ClampCaptureDimension(bodySize.X + padX * 2f);
            height = ClampCaptureDimension(bodySize.Y + padY * 2f);
        }

        private static Vector2 ResolveCurrentFrameSize(NPC npc, Vector2 hitboxSize) {
            float width = MathF.Max(npc.frame.Width, 0f);
            float height = MathF.Max(npc.frame.Height, 0f);

            if (width <= 0f) {
                width = hitboxSize.X;
            }
            if (height <= 0f) {
                height = hitboxSize.Y;
            }

            if ((width <= 0f || height <= 0f) && !Main.dedServ
                && npc.type >= NPCID.None && npc.type < TextureAssets.Npc.Length) {
                Main.instance.LoadNPC(npc.type);
                Texture2D texture = TextureAssets.Npc[npc.type].Value;
                int frames = Math.Max(Main.npcFrameCount[npc.type], 1);
                if (width <= 0f) {
                    width = texture.Width;
                }
                if (height <= 0f) {
                    height = texture.Height / (float)frames;
                }
            }

            return new Vector2(MathF.Max(width, 1f), MathF.Max(height, 1f));
        }

        private static int ClampCaptureDimension(float size) {
            if (!float.IsFinite(size)) {
                return MaxCaptureDimension;
            }
            int pixels = (int)MathF.Ceiling(MathF.Max(size, MinCaptureDimension));
            pixels = (pixels + 1) & ~1;
            return Math.Clamp(pixels, MinCaptureDimension, MaxCaptureDimension);
        }

        private static void AddCut(DismemberEntry entry, Vector2 cutPointWorld, float cutAngle,
            int holdFrames, int birthDelay = 0) {
            if (entry.SnapWidth <= 0 || entry.Cuts.Count >= MaxCuts) {
                return;
            }

            //切点收拢进身体范围，保证切线穿过快照 quad

            Vector2 local = cutPointWorld - entry.AnchorCenter;
            local.X = MathHelper.Clamp(local.X, -entry.SnapWidth * 0.35f, entry.SnapWidth * 0.35f);
            local.Y = MathHelper.Clamp(local.Y, -entry.SnapHeight * 0.35f, entry.SnapHeight * 0.35f);
            Vector2 dir = cutAngle.ToRotationVector2();
            DismemberCut cut = new() {
                PointLocal = local,
                Normal = new Vector2(-dir.Y, dir.X),
                Birth = entry.Timer + birthDelay,   //未来出生=波及调度，亮起前几何已切好但无位移无辉光

                Hold = holdFrames,
            };

            SplitPieces(entry, in cut);
            entry.Cuts.Add(cut);

            //零滞拍且非波及调度

            if (cut.Hold <= 0 && birthDelay <= 0 && !Main.dedServ) {
                SeparationBurst(entry, in cut);
            }
        }

        /// <summary>用切线把现有碎片各自一分为二（Sutherland–Hodgman 半平面裁剪）</summary>
        private static void SplitPieces(DismemberEntry entry, in DismemberCut cut) {
            int remainingSplits = Math.Max(MaxPieces - entry.Pieces.Count, 0);
            int potentialSplits = Math.Min(entry.Pieces.Count, remainingSplits);
            List<DismemberPiece> next = new(entry.Pieces.Count + potentialSplits);
            foreach (DismemberPiece piece in entry.Pieces) {
                if (remainingSplits <= 0) {
                    piece.CutSides.Add(0);
                    piece.CutSpins.Add(0f);
                    next.Add(piece);
                    continue;
                }

                List<Vector2> posSide = ClipHalfPlane(piece.Verts, cut.PointLocal, cut.Normal, 1f);
                List<Vector2> negSide = ClipHalfPlane(piece.Verts, cut.PointLocal, cut.Normal, -1f);

                bool canSplit = posSide.Count >= 3 && negSide.Count >= 3
                    && PolyArea(posSide) >= MinPieceArea && PolyArea(negSide) >= MinPieceArea;

                if (!canSplit) {
                    //未切中的碎片对本刀不产生位移

                    piece.CutSides.Add(0);
                    piece.CutSpins.Add(0f);
                    next.Add(piece);
                    continue;
                }
                next.Add(MakeChild(piece, posSide, 1));
                next.Add(MakeChild(piece, negSide, -1));
                remainingSplits--;
            }
            entry.Pieces.Clear();
            entry.Pieces.AddRange(next);
        }

        private static DismemberPiece MakeChild(DismemberPiece parent, List<Vector2> verts, sbyte side) {
            DismemberPiece child = new() {
                Verts = [.. verts],
                Centroid = PolyCentroid(verts),
                JitterPhase = Main.rand.NextFloat(MathHelper.TwoPi),
            };
            //继承父片的历史切口关系，追加本刀

            child.CutSides.AddRange(parent.CutSides);
            child.CutSpins.AddRange(parent.CutSpins);
            child.CutSides.Add(side);
            child.CutSpins.Add(side * Main.rand.NextFloat(0.018f, 0.05f));
            return child;
        }

        /// <summary>保留 dot(v-p, n)*side ≥ 0 半平面的凸多边形裁剪（面影纸裂共用）</summary>
        internal static List<Vector2> ClipHalfPlane(Vector2[] poly, Vector2 p, Vector2 n, float side) {
            List<Vector2> result = new(poly.Length + 2);
            for (int i = 0; i < poly.Length; i++) {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % poly.Length];
                float da = Vector2.Dot(a - p, n) * side;
                float db = Vector2.Dot(b - p, n) * side;
                if (da >= 0f) {
                    result.Add(a);
                }
                if (da >= 0f != db >= 0f) {
                    result.Add(Vector2.Lerp(a, b, da / (da - db)));
                }
            }
            return result;
        }

        internal static float PolyArea(List<Vector2> poly) {
            float area = 0f;
            for (int i = 0; i < poly.Count; i++) {
                Vector2 p0 = poly[i];
                Vector2 p1 = poly[(i + 1) % poly.Count];
                area += p0.X * p1.Y - p1.X * p0.Y;
            }
            return MathF.Abs(area) * 0.5f;
        }

        internal static Vector2 PolyCentroid(List<Vector2> poly) {
            float signedArea = 0f;
            Vector2 acc = Vector2.Zero;
            for (int i = 0; i < poly.Count; i++) {
                Vector2 p0 = poly[i];
                Vector2 p1 = poly[(i + 1) % poly.Count];
                float cross = p0.X * p1.Y - p1.X * p0.Y;
                signedArea += cross;
                acc += (p0 + p1) * cross;
            }
            if (MathF.Abs(signedArea) < 1e-4f) {
                //退化多边形回退顶点均值

                Vector2 avg = Vector2.Zero;
                foreach (Vector2 v in poly) {
                    avg += v;
                }
                return avg / Math.Max(poly.Count, 1);
            }
            return acc / (3f * signedArea);
        }

        /// <summary>滞拍后缓出的分离曲线 0..1（hold 为该切口自己的滞拍帧数）</summary>
        internal static float SeparationCurve(int age, int hold) {
            if (age < hold) {
                return 0f;
            }
            return OniFinaleRenderer.EaseOutCubic((age - hold) / (float)SeparateFrames);
        }

        /// <summary>碎片本帧位移与旋转、各切口贡献按各自时基独立缓动，全部到位后叠加僵直微颤</summary>
        internal static void GetPieceMotion(DismemberEntry entry, DismemberPiece piece, out Vector2 offset, out float rotation) {
            offset = Vector2.Zero;
            rotation = 0f;
            float settled = 0f;
            for (int i = 0; i < entry.Cuts.Count; i++) {
                sbyte side = i < piece.CutSides.Count ? piece.CutSides[i] : (sbyte)0;
                if (side == 0) {
                    continue;
                }
                float curve = SeparationCurve(entry.Timer - entry.Cuts[i].Birth, entry.Cuts[i].Hold);
                if (curve <= 0f) {
                    continue;
                }
                offset += entry.Cuts[i].Normal * (side * entry.DriftMax * curve);
                rotation += piece.CutSpins[i] * curve;
                if (curve > settled) {
                    settled = curve;
                }
            }
            if (settled >= 0.999f) {
                //僵住后的极轻微颤、尸身"绷着"的张力

                float t = Main.GlobalTimeWrappedHourly;
                offset.X += MathF.Sin(t * 21.3f + piece.JitterPhase) * 0.4f;
                offset.Y += MathF.Cos(t * 17.7f + piece.JitterPhase * 1.7f) * 0.4f;
            }
        }

        /// <summary>逐帧、挂 PostUpdateNPCs</summary>
        internal static void UpdateAll() {
            for (int i = lockEntries.Count - 1; i >= 0; i--) {
                DismemberLockEntry entry = lockEntries[i];
                NPC npc = Main.npc[entry.NpcIndex];
                if (!npc.active || npc.type != entry.NpcType
                    || !TimeFreezeSystem.IsLeaseActive(npc, entry.FreezeLease)) {
                    lockEntries.RemoveAt(i);
                    continue;
                }

                entry.Timer++;
                if (entry.Timer >= entry.Duration) {
                    TimeFreezeSystem.ReleaseNPC(npc, entry.FreezeLease);
                    lockEntries.RemoveAt(i);
                    continue;
                }
            }

            for (int i = Entries.Count - 1; i >= 0; i--) {
                DismemberEntry entry = Entries[i];
                NPC npc = Main.npc[entry.NpcIndex];
                if (!npc.active || npc.type != entry.NpcType) {
                    Entries.RemoveAt(i);
                    continue;
                }

                entry.Timer++;
                if (entry.Timer >= entry.Duration) {
                    Entries.RemoveAt(i);
                    continue;
                }
                if (entry.VisualOnly) {
                    entry.AnchorCenter = npc.Center;
                }

                if (!Main.dedServ) {
                    foreach (DismemberCut cut in entry.Cuts) {
                        if (cut.Hold > 0 && entry.Timer - cut.Birth == cut.Hold) {
                            SeparationBurst(entry, in cut);
                        }
                    }
                }
            }
        }

        private static void ReleaseLock(DismemberLockEntry entry) {
            if (entry.NpcIndex < 0 || entry.NpcIndex >= Main.maxNPCs) {
                return;
            }
            NPC npc = Main.npc[entry.NpcIndex];
            if (npc.active && npc.type == entry.NpcType) {
                TimeFreezeSystem.ReleaseNPC(npc, entry.FreezeLease);
            }
        }

        /// <summary>断开瞬间的声画、沿切线迸出碎晶 + 脆响。 群组齐崩时同帧只留一声脆响</summary>
        private static void SeparationBurst(DismemberEntry entry, in DismemberCut cut) {
            if (Main.GameUpdateCount != lastBurstTick) {
                lastBurstTick = Main.GameUpdateCount;
                shardsThisTick = 0;
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.4f, Volume = 0.55f }, entry.AnchorCenter);
            }
            int shardCount = Math.Min(10, ShardBudgetPerTick - shardsThisTick);
            if (shardCount <= 0) {
                return;
            }
            shardsThisTick += shardCount;

            Vector2 tangent = new(-cut.Normal.Y, cut.Normal.X);
            float halfLen = MathF.Min(entry.SnapWidth, entry.SnapHeight) * 0.4f;
            for (int k = 0; k < shardCount; k++) {
                Vector2 pos = entry.AnchorCenter + cut.PointLocal + tangent * Main.rand.NextFloat(-1f, 1f) * halfLen;
                Vector2 vel = cut.Normal * Main.rand.NextFloat(1.5f, 4.5f) * (Main.rand.NextBool() ? 1f : -1f)
                    + tangent * Main.rand.NextFloat(-1f, 1f);
                Color c = Main.rand.NextBool(3) ? new Color(255, 238, 215) : new Color(255, 115, 62);
                PRTLoader.NewParticle<PRT_OniShard>(pos, vel, c, Main.rand.NextFloat(0.35f, 0.7f))
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.25f, 0.25f)
                        , Main.rand.NextFloat(1.4f, 2.4f), affectedByGravity: true);
            }
        }

        /// <summary>取或建目标专属快照 RT（仅绘制线程调用）</summary>
        internal static RenderTarget2D EnsureSnapshotRT(GraphicsDevice gd, DismemberEntry entry) {
            if (SnapRTs.TryGetValue(entry.NpcIndex, out RenderTarget2D rt) && rt != null && !rt.IsDisposed
                && rt.Width == entry.SnapWidth && rt.Height == entry.SnapHeight) {
                return rt;
            }
            rt?.Dispose();
            try {
                rt = new RenderTarget2D(gd, entry.SnapWidth, entry.SnapHeight, false
                    , SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            } catch {
                return null;
            }
            SnapRTs[entry.NpcIndex] = rt;
            return rt;
        }

        internal static void DisposeAllSnapshots() {
            foreach (RenderTarget2D rt in SnapRTs.Values) {
                rt.SafeDispose();
            }
            SnapRTs.Clear();
        }
    }

    /// <summary>肢解状态逐帧维护与世界卸载清理</summary>
    internal sealed class OniDismemberSystem : ModSystem
    {
        public override void PostUpdateNPCs() => OniDismember.UpdateAll();

        public override void OnWorldUnload() => OniDismember.Clear();
    }
}
