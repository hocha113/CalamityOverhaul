using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameInput;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间 UI 面板</summary>
    internal class HackTimeUI : UIHandle
    {
        public static HackTimeUI Instance => UIHandleLoader.GetUIHandleOfType<HackTimeUI>();

        internal HackPanelRenderer Panel { get; private set; } = new();
        internal HackQueueRenderer Queue { get; private set; } = new();
        internal InfiniteHackRenderer InfiniteHack { get; private set; } = new();
        internal ScanInfoRenderer ScanInfo { get; private set; } = new();
        internal HackRamRenderer Ram { get; private set; } = new();

        public override bool Active => HackTime.Active || HackTime.Intensity >= 0.001f;

        public override void Load() {
            Panel.Queue = Queue;
        }

        public override void Update() {
            HackTheme.UpdateProfile();

            Panel.Update();
            //Queue.Update 由 CWRWorld.PostUpdateEverything 驱动
            InfiniteHack.Update();
            ScanInfo.Update();
            Ram.Update();
            //悬停成本 → RAM 弧预扣
            Ram.PreviewCost = Panel.HoveredCostPreview;

            bool mouseOnPanel = Panel.ContainsMouse();
            UpdateClickSelection(mouseOnPanel);
            UpdateScroll();

            //面板悬停拦截穿透
            hoverInMainPage = mouseOnPanel;
            if (hoverInMainPage) {
                player.mouseInterface = true;
            }
        }

        private void UpdateScroll() {
            if (!HackTime.Active || !Panel.CanScroll || !Panel.ViewportContainsMouse()) return;
            int delta = PlayerInput.ScrollWheelDeltaForUI;
            if (delta == 0) return;
            Panel.HandleScroll(delta);
            //不锁的话滚轮会同时翻快捷栏，骇入时快捷栏虽被隐藏但物品仍会换
            PlayerInput.LockVanillaMouseScroll("CalamityOverhaul/HackTimePanel");
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //本层是 InterfaceScaleType.UI，坐标即 UI 空间；几何走 HackTheme / HackRamArcLayout
            Ram.Draw(spriteBatch);
            ScanInfo.Draw(spriteBatch);
            Panel.Draw(spriteBatch);
            //无限模式用风暴，否则普通队列
            if (HackTime.InfiniteHack)
                InfiniteHack.Draw(spriteBatch);
            else
                Queue.Draw(spriteBatch);
        }

        private void UpdateClickSelection(bool mouseOnPanel) {
            if (!HackTime.Active) return;

            //右键取消选中
            if (keyRightPressState == KeyPressState.Pressed && !mouseOnPanel) {
                if (HackTime.CurrentScanTarget != null) {
                    HackTime.DeselectTarget();
                    Panel.Hide();
                }
                return;
            }

            if (keyLeftPressState != KeyPressState.Pressed) return;

            //面板内点击
            if (mouseOnPanel) {
                //无限模式点击协议蓄力
                if (HackTime.InfiniteHack) {
                    if (Panel.HasHoveredSlot && !Panel.HoveredSlotLocked && !InfiniteHack.IsActive)
                        InfiniteHack.BeginCharge();
                }
                else {
                    Panel.HandleClick();
                }
                return;
            }

            //世界点击选目标
            IHackTarget hovered = HackTimeTargeting.HoveredTarget;

            if (hovered != null) {
                if (HackTime.CurrentScanTarget == null
                    || !hovered.TargetEquals(HackTime.CurrentScanTarget)) {
                    HackTime.Select(hovered);
                    //按目标类型过滤协议
                    Panel.Show(hovered.TargetType.Kind);
                }
            }
            else if (HackTime.CurrentScanTarget != null) {
                HackTime.DeselectTarget();
                Panel.Hide();
            }
        }

        public override void UnLoad() {
            Panel = null;
            Queue = null;
            InfiniteHack = null;
            ScanInfo = null;
            Ram = null;
        }
    }
}
