using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.UIs.HudStack
{
    /// <summary>
    /// 左下角 HUD 堆叠管理器（“HUD 队列”）
    /// <br/>各成员实现 <see cref="IBottomLeftHud"/> 并在定位时调用 <see cref="ResolveAnchor"/> 取锚点；
    /// 每帧收集想显示的成员，按 <see cref="IBottomLeftHud.HudStackOrder"/> 自下而上排布：底部成员保持原位，
    /// 上方成员被平滑顶高，刚好让出下方占用高度以避免重叠
    /// <br/>典型场景：手持比目鱼时 <c>HalibutHud</c> 占底，义眼 <c>CstmVisualEyeHUD</c> 自动上移悬浮其上
    /// <br/>成员无需互相感知，新增 HUD 实现接口并改用本类取锚点即自动纳入排布
    /// </summary>
    internal static class BottomLeftHudStack
    {
        /// <summary>
        /// UI 空间下的屏幕高度（任意调用语境取值一致，随 <see cref="Main.UIScale"/> 校正）
        /// <br/>可能与他人共堆叠的成员应以此构造锚点，避免缩放下漂移
        /// </summary>
        public static float UIScreenH => PlayerInput.RealScreenHeight / Main.UIScale;

        //堆叠成员之间的竖直间隙（像素）
        private const float StackGap = 10f;
        //避让偏移平滑系数：每逻辑帧向目标插值的比例
        private const float SmoothFactor = 0.2f;
        //偏移与目标足够接近时直接吸附，避免长尾抖动
        private const float SnapEpsilon = 0.25f;

        private sealed class Slot
        {
            public IBottomLeftHud Hud;
            //注册先后，作为同序号成员的稳定次级排序键
            public int RegisterIndex;
            //当前平滑后的竖直避让偏移（负值=向上顶高）
            public float CurrentDy;
            //本帧目标竖直避让偏移
            public float TargetDy;
            //本帧是否想显示（排布时填充）
            public bool ActiveThisFrame;
            //上一帧是否在显示，用于在“重新出现”时直接吸附到目标位置
            public bool WasActive;
        }

        private static readonly List<Slot> slots = [];
        private static readonly Dictionary<IBottomLeftHud, Slot> lookup = [];
        //复用的本帧活跃成员缓存，避免每帧分配
        private static readonly List<Slot> activeScratch = [];
        //保护登记表与排布状态：UI 取锚点的调用可能落在更新或绘制阶段，统一上锁避免竞态/重入
        private static readonly object gate = new();
        private static int registerCounter;
        private static int lastLayoutFrame = -1;

        /// <summary>
        /// 取得该 HUD 经堆叠避让后的最终锚点，首次调用自动登记成员
        /// <br/>更新与绘制（含命中盒）统一用本方法返回值定位，以保证交互与绘制一致
        /// </summary>
        public static Vector2 ResolveAnchor(IBottomLeftHud hud) {
            lock (gate) {
                Slot slot = Register(hud);
                EnsureLayout();
                return hud.HudStackAnchor + new Vector2(0f, slot.CurrentDy);
            }
        }

        /// <summary>取得该 HUD 当前竖直避让偏移（负值=被顶高），一般直接用 <see cref="ResolveAnchor"/> 即可</summary>
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
            //新成员加入需要立即重排，避免其首帧落在错误位置
            lastLayoutFrame = -1;
            return slot;
        }

        private static void EnsureLayout() {
            //同一逻辑帧只重排一次：保证同帧内多个成员、多次取锚得到一致结果
            int frame = (int)Main.GameUpdateCount;
            if (frame == lastLayoutFrame) {
                return;
            }
            lastLayoutFrame = frame;
            Layout();
        }

        private static void Layout() {
            //1. 收集本帧想显示的成员
            activeScratch.Clear();
            foreach (Slot slot in slots) {
                bool wants;
                try {
                    wants = slot.Hud.HudStackActive;
                } catch {
                    //单个成员判定异常不应拖垮整条队列
                    wants = false;
                }
                slot.ActiveThisFrame = wants;
                if (wants) {
                    activeScratch.Add(slot);
                }
            }

            //2. 次序升序、注册序为次级键稳定排序（越小越靠近底部）
            activeScratch.Sort(static (a, b) => {
                int byOrder = a.Hud.HudStackOrder.CompareTo(b.Hud.HudStackOrder);
                return byOrder != 0 ? byOrder : a.RegisterIndex.CompareTo(b.RegisterIndex);
            });

            //3. 自下而上累积避让；stackTopY 记录已堆叠内容的顶边（UI 空间，值越小越高）
            float stackTopY = float.NaN;
            foreach (Slot slot in activeScratch) {
                IBottomLeftHud hud = slot.Hud;
                float anchorY = hud.HudStackAnchor.Y;
                float naturalTopY = anchorY - hud.HudStackTopExtent;
                float naturalBottomY = anchorY + hud.HudStackBottomExtent;

                float targetDy;
                if (float.IsNaN(stackTopY)) {
                    //最底部成员保持自然位置
                    targetDy = 0f;
                    stackTopY = naturalTopY;
                }
                else {
                    //顶高使其底边停靠在已堆叠顶边之上（留出间隙）；只允许向上，避免被拽进下方成员
                    float desiredBottomY = stackTopY - StackGap;
                    targetDy = MathF.Min(0f, desiredBottomY - naturalBottomY);
                    stackTopY = naturalTopY + targetDy;
                }

                slot.TargetDy = targetDy;
                //从隐藏切到显示：直接吸附到目标，避免从陈旧位置滑入
                if (!slot.WasActive) {
                    slot.CurrentDy = targetDy;
                }
            }

            //4. 推进平滑并刷新显示状态（含本帧未显示者）
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

        /// <summary>卸载时清空登记表，避免残留旧实例（热重载安全）</summary>
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

    /// <summary>让 <see cref="BottomLeftHudStack"/> 的静态登记表随模组卸载一并清理</summary>
    internal sealed class BottomLeftHudStackLoader : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => BottomLeftHudStack.Clear();
    }
}
