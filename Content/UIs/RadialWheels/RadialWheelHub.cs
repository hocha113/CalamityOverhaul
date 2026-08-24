using CalamityOverhaul.Common;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.RadialWheels
{
    /// <summary>
    /// 快捷转盘协调层；成员实现 <see cref="IRadialWheel"/>，定位调 <see cref="ResolveCenter"/>
    /// <br/>一个键开齐所有够格的盘，按 WheelStackOrder 自下而上顶高，独占时位置不变
    /// <br/>光标按最近中心归属唯一焦点盘，非焦点盘不吃悬停与点击
    /// </summary>
    internal static class RadialWheelHub
    {
        //全部转盘共用一个 reason，多盘并存时时停不会被谁单独放掉
        private const string FreezeReason = "RadialWheel";

        /// <summary>独占时的锚点 Y 占屏比，中央偏下</summary>
        public const float AnchorYRatio = 0.72f;

        private const float StackGap = 18f;//相邻两盘的竖直间隙
        private const float ScreenMargin = 12f;//整组夹紧时的留边
        private const float SmoothFactor = 0.25f;//每帧向目标插值
        private const float SnapEpsilon = 0.35f;//近则吸附，避长尾抖

        #region UI 空间坐标
        //UIHandle 的 Update/Draw 处在 UI 缩放层内，逻辑帧看到的却是原始后台缓冲尺寸
        //跨语境的转盘布局一律走这组换算，禁止直接读 Main.screenWidth/Height

        /// <summary>UI 空间屏宽，任何调用语境下取值一致</summary>
        public static float UIScreenW => PlayerInput.RealScreenWidth / Main.UIScale;

        /// <summary>UI 空间屏高，任何调用语境下取值一致</summary>
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        /// <summary>UI 空间鼠标位置，任何调用语境下取值一致</summary>
        public static Vector2 UIMouse => new Vector2(PlayerInput.MouseX, PlayerInput.MouseY) / Main.UIScale;

        /// <summary>独占锚点，横向居中、纵向偏下</summary>
        public static Vector2 ResolveAnchor() => new(UIScreenW * 0.5f, UIScreenH * AnchorYRatio);
        #endregion

        private sealed class Slot
        {
            public IRadialWheel Wheel;
            public int RegisterIndex;//同序号次级键
            public Vector2 CurrentCenter;
            public Vector2 TargetCenter;
            public bool OpenThisFrame;
            public bool WasOpen;//重现时吸附，避陈旧滑入
        }

        private static readonly List<Slot> slots = [];
        private static readonly Dictionary<IRadialWheel, Slot> lookup = [];
        private static readonly List<Slot> activeScratch = [];//复用，避每帧分配
        private static readonly List<IRadialWheel> openScratch = [];
        //取位置可能在 Update/Draw，上锁避竞态
        private static readonly object gate = new();
        private static int registerCounter;
        private static int lastLayoutFrame = -1;
        private static IRadialWheel focused;
        private static IRadialWheel backdropOwner;
        private static IRadialWheel hintOwner;
        private static bool freezeOwned;

        /// <summary>登记成员；持有者每帧调 <see cref="ResolveCenter"/> 时自动完成</summary>
        private static Slot Register(IRadialWheel wheel) {
            if (lookup.TryGetValue(wheel, out Slot slot)) {
                return slot;
            }
            slot = new Slot {
                Wheel = wheel,
                RegisterIndex = registerCounter++,
                CurrentCenter = ResolveAnchor(),
                TargetCenter = ResolveAnchor(),
            };
            lookup[wheel] = slot;
            slots.Add(slot);
            lastLayoutFrame = -1;//立即重排，避首帧错位
            return slot;
        }

        /// <summary>避让排布后的中心；首次调用自动登记，更新与绘制统一用此值</summary>
        public static Vector2 ResolveCenter(IRadialWheel wheel) {
            if (wheel == null) {
                return ResolveAnchor();
            }
            lock (gate) {
                Slot slot = Register(wheel);
                EnsureLayout();
                return slot.CurrentCenter;
            }
        }

        /// <summary>是否有任意转盘展开</summary>
        public static bool AnyOpen {
            get {
                lock (gate) {
                    EnsureLayout();
                    return activeScratch.Count > 0;
                }
            }
        }

        /// <summary>光标本帧是否归属该盘；非焦点盘必须放弃悬停与点击</summary>
        public static bool IsFocused(IRadialWheel wheel) {
            if (wheel == null) {
                return false;
            }
            lock (gate) {
                EnsureLayout();
                return focused == wheel;
            }
        }

        /// <summary>该盘是否负责画全屏压暗；多盘并存时归武器盘，避免两层滤镜叠暗</summary>
        public static bool OwnsBackdrop(IRadialWheel wheel) {
            if (wheel == null) {
                return false;
            }
            lock (gate) {
                EnsureLayout();
                return backdropOwner == wheel;
            }
        }

        /// <summary>该盘是否负责画按键提示；归最底那个盘，否则提示会糊在下方盘上</summary>
        public static bool OwnsHint(IRadialWheel wheel) {
            if (wheel == null) {
                return false;
            }
            lock (gate) {
                EnsureLayout();
                return hintOwner == wheel;
            }
        }

        /// <summary>统一开关键，由 <see cref="RadialWheelKeyPlayer"/> 在 ProcessTriggers 里驱动</summary>
        public static void HandleKey() {
            ModKeybind key = CWRKeySystem.RadialWheel_Key;
            if (key == null) {
                return;
            }
            if (key.JustPressed) {
                //全屏界面（任务书/湖心景等）摊开时不开盘，与 B 键无够格盘时的静默同口径
                if (!FullScreenUIHub.AnyOpen) {
                    OpenEligible();
                }
                return;
            }
            if (!key.JustReleased) {
                return;
            }
            IRadialWheel commitTarget;
            lock (gate) {
                EnsureLayout();
                if (activeScratch.Count == 0) {
                    return;
                }
                commitTarget = focused;
            }
            //松开即提交焦点盘的悬停项，随后全部收起
            commitTarget?.WheelCommitHovered();
            CloseAll(silent: false);
        }

        /// <summary>开齐本帧够格的盘；只有第一个出声，避免多盘齐开时音效叠一起</summary>
        private static void OpenEligible() {
            openScratch.Clear();
            lock (gate) {
                EnsureLayout();
                //按次序收集，保证"第一个"始终是最底那个
                slots.Sort(CompareSlot);
                foreach (Slot slot in slots) {
                    bool eligible;
                    try {
                        eligible = !slot.Wheel.WheelIsOpen && slot.Wheel.WheelCanOpen;
                    } catch {
                        eligible = false;//单成员异常不拖垮队列
                    }
                    if (eligible) {
                        openScratch.Add(slot.Wheel);
                    }
                }
            }
            if (openScratch.Count == 0) {
                return;
            }
            //锁外开盘：WheelOpen 会回读 Hub（登记、取中心）
            for (int i = 0; i < openScratch.Count; i++) {
                openScratch[i].WheelOpen(silent: i > 0);
            }
            openScratch.Clear();
            lock (gate) {
                AcquireFreeze();
                lastLayoutFrame = -1;//开盘当帧立刻重排，新盘直接吸附到位
                EnsureLayout();
            }
        }

        /// <summary>收起全部；只有第一个出声</summary>
        public static void CloseAll(bool silent) {
            openScratch.Clear();
            lock (gate) {
                slots.Sort(CompareSlot);
                foreach (Slot slot in slots) {
                    bool open;
                    try {
                        open = slot.Wheel.WheelIsOpen;
                    } catch {
                        open = false;
                    }
                    if (open) {
                        openScratch.Add(slot.Wheel);
                    }
                }
            }
            for (int i = 0; i < openScratch.Count; i++) {
                openScratch[i].WheelClose(silent || i > 0);
            }
            openScratch.Clear();
            lock (gate) {
                ReleaseFreeze();
                focused = null;
                //归属留给淡出，见 Layout 里的同款说明
                lastLayoutFrame = -1;
            }
        }

        private static int CompareSlot(Slot a, Slot b) {
            int byOrder = a.Wheel.WheelStackOrder.CompareTo(b.Wheel.WheelStackOrder);
            return byOrder != 0 ? byOrder : a.RegisterIndex.CompareTo(b.RegisterIndex);
        }

        private static void EnsureLayout() {
            //同逻辑帧只排一次
            int frame = (int)Main.GameUpdateCount;
            if (frame == lastLayoutFrame) {
                return;
            }
            lastLayoutFrame = frame;
            Layout();
        }

        private static void Layout() {
            //收集本帧展开的盘
            activeScratch.Clear();
            foreach (Slot slot in slots) {
                bool open;
                try {
                    open = slot.Wheel.WheelIsOpen;
                } catch {
                    open = false;//单成员异常不拖垮队列
                }
                slot.OpenThisFrame = open;
                if (open) {
                    activeScratch.Add(slot);
                }
            }
            activeScratch.Sort(CompareSlot);

            if (activeScratch.Count == 0) {
                focused = null;
                //压暗与提示的归属保持不变：持有者要靠它画完自己的淡出，
                //这里清空会让全屏压暗在松键当帧硬切消失，而转盘还在淡
                ReleaseFreeze();
                CommitSmoothing();
                return;
            }

            //一个盘都没开过时不该持有时停，兜底补一次
            AcquireFreeze();
            PlaceTargets();
            CommitSmoothing();
            ResolveFocus();

            //压暗归次序最大者（武器盘），提示归最底者
            backdropOwner = activeScratch[^1].Wheel;
            hintOwner = activeScratch[0].Wheel;
        }

        /// <summary>自下而上排布：最底那个占独占锚点，其余依次上顶，整组再夹进屏内</summary>
        private static void PlaceTargets() {
            Vector2 anchor = ResolveAnchor();
            int count = activeScratch.Count;

            float cursorY = anchor.Y;
            for (int i = 0; i < count; i++) {
                Slot slot = activeScratch[i];
                slot.TargetCenter = new Vector2(anchor.X, cursorY);
                if (i + 1 < count) {
                    cursorY -= FootprintOf(slot) + StackGap + FootprintOf(activeScratch[i + 1]);
                }
            }

            float groupBottom = activeScratch[0].TargetCenter.Y + FootprintOf(activeScratch[0]);
            float groupTop = activeScratch[^1].TargetCenter.Y - FootprintOf(activeScratch[^1]);
            float screenH = UIScreenH;
            float available = screenH - ScreenMargin * 2f;
            float groupH = groupBottom - groupTop;

            float shift;
            if (groupH >= available) {
                //放不下就整组居中，宁可两头贴边也不要一头飞出屏幕
                shift = ScreenMargin + available * 0.5f - (groupTop + groupH * 0.5f);
            }
            else if (groupTop < ScreenMargin) {
                shift = ScreenMargin - groupTop;
            }
            else if (groupBottom > screenH - ScreenMargin) {
                shift = screenH - ScreenMargin - groupBottom;
            }
            else {
                shift = 0f;
            }

            if (shift != 0f) {
                foreach (Slot slot in activeScratch) {
                    slot.TargetCenter = new Vector2(slot.TargetCenter.X, slot.TargetCenter.Y + shift);
                }
            }
        }

        private static float FootprintOf(Slot slot) {
            try {
                return MathF.Max(slot.Wheel.WheelFootprintRadius, 1f);
            } catch {
                return 1f;
            }
        }

        private static void CommitSmoothing() {
            foreach (Slot slot in slots) {
                if (slot.OpenThisFrame) {
                    if (!slot.WasOpen) {
                        slot.CurrentCenter = slot.TargetCenter;//隐藏→显示直接吸附
                    }
                    else {
                        slot.CurrentCenter = Vector2.Lerp(slot.CurrentCenter, slot.TargetCenter, SmoothFactor);
                        if (Vector2.Distance(slot.CurrentCenter, slot.TargetCenter) < SnapEpsilon) {
                            slot.CurrentCenter = slot.TargetCenter;
                        }
                    }
                    //把排布结果推回持有者，命中与绘制共用同一个中心
                    try {
                        slot.Wheel.WheelSetCenter(slot.CurrentCenter);
                    } catch {
                        //绘制侧异常不拖垮排布
                    }
                }
                slot.WasOpen = slot.OpenThisFrame;
            }
        }

        /// <summary>
        /// 光标归属最近中心的那个盘（竖排时分界就是两盘中间一条水平线）
        /// <br/>不设外半径上限，"往外甩即选中"的手感得以保留，同时不会两盘同时高亮
        /// </summary>
        private static void ResolveFocus() {
            Vector2 mouse = UIMouse;
            float best = float.MaxValue;
            IRadialWheel bestWheel = null;
            foreach (Slot slot in activeScratch) {
                float dist = Vector2.DistanceSquared(mouse, slot.CurrentCenter);
                if (dist < best) {
                    best = dist;
                    bestWheel = slot.Wheel;
                }
            }
            focused = bestWheel;
        }

        private static void AcquireFreeze() {
            if (freezeOwned) {
                return;
            }
            //只在单人模式生效世界冻结
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Activate(FreezeReason);
            }
            freezeOwned = true;
        }

        private static void ReleaseFreeze() {
            if (!freezeOwned) {
                return;
            }
            WorldFreezeSystem.Deactivate(FreezeReason);
            freezeOwned = false;
        }

        #region 共享极坐标数学
        //各盘的命中与扇区分割完全同构，集中在此避免各写一份

        /// <summary>光标是否落在中心死区内</summary>
        public static bool IsCenterHovered(Vector2 center, float deadZoneR) {
            return (UIMouse - center).Length() < deadZoneR;
        }

        /// <summary>极坐标命中，死区内或无扇区返回 -1；首扇区中线朝正上方</summary>
        public static int HitTest(Vector2 center, int sectorCount, float deadZoneR) {
            if (sectorCount <= 0) {
                return -1;
            }
            Vector2 offset = UIMouse - center;
            if (offset.Length() < deadZoneR) {
                return -1;
            }
            if (sectorCount == 1) {
                return 0;
            }
            float ang = MathF.Atan2(offset.Y, offset.X);
            float normalized = ang + MathHelper.PiOver2;
            while (normalized < 0) {
                normalized += MathHelper.TwoPi;
            }
            while (normalized >= MathHelper.TwoPi) {
                normalized -= MathHelper.TwoPi;
            }
            float sectorSize = MathHelper.TwoPi / sectorCount;
            float shifted = normalized + sectorSize * 0.5f;
            if (shifted >= MathHelper.TwoPi) {
                shifted -= MathHelper.TwoPi;
            }
            return Math.Clamp((int)(shifted / sectorSize), 0, sectorCount - 1);
        }

        /// <summary>扇区角度区间，屏幕系向右 0 向下正；首扇区中线朝正上方</summary>
        public static void GetSectorAngles(int idx, int sectorCount, float gap
            , out float aStart, out float aEnd) {
            if (sectorCount <= 0) {
                aStart = 0f;
                aEnd = 0f;
                return;
            }
            float sectorSize = MathHelper.TwoPi / sectorCount;
            float mid = -MathHelper.PiOver2 + idx * sectorSize;
            aStart = mid - sectorSize * 0.5f + gap * 0.5f;
            aEnd = mid + sectorSize * 0.5f - gap * 0.5f;
        }
        #endregion

        /// <summary>清空登记与时停，进出世界与热重载都要走一遍</summary>
        internal static void Clear() {
            lock (gate) {
                ReleaseFreeze();
                slots.Clear();
                lookup.Clear();
                activeScratch.Clear();
                openScratch.Clear();
                focused = null;
                backdropOwner = null;
                hintOwner = null;
                registerCounter = 0;
                lastLayoutFrame = -1;
            }
        }
    }

    /// <summary>统一开关键的宿主，键归 Hub 分发，各转盘不再各管一个键</summary>
    internal sealed class RadialWheelKeyPlayer : ModPlayer
    {
        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            if (Player.whoAmI != Main.myPlayer || Main.dedServ) {
                return;
            }
            RadialWheelHub.HandleKey();
        }
    }

    /// <summary><see cref="RadialWheelHub"/> 随模组卸载清理</summary>
    internal sealed class RadialWheelHubLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => RadialWheelHub.Clear();
    }

    /// <summary>
    /// 静态登记跨世界不会自然失效：开着盘退回主菜单会把 <c>freezeOwned</c> 与登记表一起留到下个世界，
    /// 表现为转盘再也不触发时停、其它 HUD 被永久判为“有盘展开”。与 WorldFreezeSystem 同口径逐世界重置。
    /// </summary>
    internal sealed class RadialWheelLifecycleSystem : ModSystem
    {
        public override void OnWorldLoad() => RadialWheelHub.Clear();

        public override void OnWorldUnload() => RadialWheelHub.Clear();

        public override void ClearWorld() => RadialWheelHub.Clear();
    }
}
