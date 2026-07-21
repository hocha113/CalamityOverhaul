using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.UIs.HudStack
{
    /// <summary>
    /// 左下角 HUD 堆叠；成员实现 <see cref="IBottomLeftHud"/>，定位调 <see cref="ResolveAnchor"/>
    /// <br/>按 HudStackOrder 自下而上顶高，独占时外观不变
    /// </summary>
    internal static class BottomLeftHudStack
    {
        /// <summary>UI 空间屏高，共堆叠者以此构造锚点</summary>
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        private const float StackGap = 10f;//成员竖直间隙
        private const float SmoothFactor = 0.2f;//每帧向目标插值
        private const float SnapEpsilon = 0.25f;//近则吸附，避长尾抖

        private sealed class Slot
        {
            public IBottomLeftHud Hud;
            public int RegisterIndex;//同序号次级键
            public float CurrentDy;//竖直避让，负=顶高
            public float TargetDy;
            public bool ActiveThisFrame;
            public bool WasActive;//重现时吸附，避陈旧滑入
        }

        private static readonly List<Slot> slots = [];
        private static readonly Dictionary<IBottomLeftHud, Slot> lookup = [];
        private static readonly List<Slot> activeScratch = [];//复用，避每帧分配
        //取锚可能在 Update/Draw，上锁避竞态
        private static readonly object gate = new();
        private static int registerCounter;
        private static int lastLayoutFrame = -1;

        /// <summary>堆叠避让后锚点，首次调用自动登记；更新与绘制统一用此值</summary>
        public static Vector2 ResolveAnchor(IBottomLeftHud hud) {
            lock (gate) {
                Slot slot = Register(hud);
                EnsureLayout();
                return hud.HudStackAnchor + new Vector2(0f, slot.CurrentDy);
            }
        }

        /// <summary>当前竖直避让(负=顶高)；一般用 <see cref="ResolveAnchor"/></summary>
        public static float ResolvePushUp(IBottomLeftHud hud) {
            lock (gate) {
                Slot slot = Register(hud);
                EnsureLayout();
                return slot.CurrentDy;
            }
        }

        private static Slot Register(IBottomLeftHud hud) {
            if (lookup.TryGetValue(hud, out Slot slot)) {
                return slot;
            }
            slot = new Slot { Hud = hud, RegisterIndex = registerCounter++ };
            lookup[hud] = slot;
            slots.Add(slot);
            lastLayoutFrame = -1;//立即重排，避首帧错位
            return slot;
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
            //收集本帧活跃
            activeScratch.Clear();
            foreach (Slot slot in slots) {
                bool wants;
                try {
                    wants = slot.Hud.HudStackActive;
                } catch {
                    wants = false;//单成员异常不拖垮队列
                }
                slot.ActiveThisFrame = wants;
                if (wants) {
                    activeScratch.Add(slot);
                }
            }

            //次序升序，注册序次级
            activeScratch.Sort(static (a, b) => {
                int byOrder = a.Hud.HudStackOrder.CompareTo(b.Hud.HudStackOrder);
                return byOrder != 0 ? byOrder : a.RegisterIndex.CompareTo(b.RegisterIndex);
            });

            //自下而上累积避让
            float stackTopY = float.NaN;
            foreach (Slot slot in activeScratch) {
                IBottomLeftHud hud = slot.Hud;
                float anchorY = hud.HudStackAnchor.Y;
                float naturalTopY = anchorY - hud.HudStackTopExtent;
                float naturalBottomY = anchorY + hud.HudStackBottomExtent;

                float targetDy;
                if (float.IsNaN(stackTopY)) {
                    targetDy = 0f;//最底保持自然位
                    stackTopY = naturalTopY;
                }
                else {
                    //底边停靠已堆叠顶边之上，只许向上
                    float desiredBottomY = stackTopY - StackGap;
                    targetDy = MathF.Min(0f, desiredBottomY - naturalBottomY);
                    stackTopY = naturalTopY + targetDy;
                }

                slot.TargetDy = targetDy;
                if (!slot.WasActive) {
                    slot.CurrentDy = targetDy;//隐藏→显示直接吸附
                }
            }

            //推进平滑
            foreach (Slot slot in slots) {
                if (slot.ActiveThisFrame) {
                    slot.CurrentDy = MathHelper.Lerp(slot.CurrentDy, slot.TargetDy, SmoothFactor);
                    if (MathF.Abs(slot.CurrentDy - slot.TargetDy) < SnapEpsilon) {
                        slot.CurrentDy = slot.TargetDy;
                    }
                }
                slot.WasActive = slot.ActiveThisFrame;
            }
        }

        /// <summary>卸载清空，热重载安全</summary>
        internal static void Clear() {
            lock (gate) {
                slots.Clear();
                lookup.Clear();
                activeScratch.Clear();
                registerCounter = 0;
                lastLayoutFrame = -1;
            }
        }
    }

    /// <summary><see cref="BottomLeftHudStack"/> 随模组卸载清理</summary>
    internal sealed class BottomLeftHudStackLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => BottomLeftHudStack.Clear();
    }
}
