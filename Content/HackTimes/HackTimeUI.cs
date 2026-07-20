using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇客时间 UI 面板</summary>
    internal class HackTimeUI : UIHandle
    {
        public static HackTimeUI Instance => UIHandleLoader.GetUIHandleOfType<HackTimeUI>();

        //骇入面板
        internal HackPanelRenderer Panel { get; private set; } = new();
        //上传队列
        internal HackQueueRenderer Queue { get; private set; } = new();
        //无限骇入风暴
        internal InfiniteHackRenderer InfiniteHack { get; private set; } = new();
        //扫描信息面板
        internal ScanInfoRenderer ScanInfo { get; private set; } = new();
        //RAM 弧形 HUD
        internal HackRamRenderer Ram { get; private set; } = new();

        public override bool Active => HackTime.Active || HackTime.Intensity >= 0.001f;

        public override void Load() {
            Panel.Queue = Queue;
        }

        public override void Update() {
            //敌我双主色随当前目标过渡
            HackTheme.UpdateProfile();

            Panel.Update();
            //Queue.Update 由 CWRWorld.PostUpdateEverything 驱动
            InfiniteHack.Update();
            ScanInfo.Update();
            Ram.Update();
            //悬停协议成本传给RAM弧做预扣闪烁
            Ram.PreviewCost = Panel.HoveredCostPreview;

            bool mouseOnPanel = Panel.ContainsMouse();
            UpdateClickSelection(mouseOnPanel);

            //面板悬停拦截穿透
            hoverInMainPage = mouseOnPanel;
            if (hoverInMainPage) {
                player.mouseInterface = true;
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            //脱离 UIScaleMatrix，原始像素坐标
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
                    if (Panel.HasHoveredSlot && !InfiniteHack.IsActive)
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
