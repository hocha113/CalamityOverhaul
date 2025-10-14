using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal class RightButtonUI : UIHandle
    {
        public static RightButtonUI Instance => UIHandleLoader.GetUIHandleOfType<RightButtonUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public float hoverSengs;

        public override void Update() {
            Size = RightButton.Size();
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            int maxOffset = Math.Max(0, HalibutUIPanel.Instance.halibutUISkillSlots.Count - HalibutUIPanel.maxVisibleSlots);
            bool canScroll = HalibutUIPanel.Instance.scrollOffset < maxOffset;

            if (hoverInMainPage && canScroll) {
                if (hoverSengs < 1f) {
                    hoverSengs += 0.15f;
                }
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = 0.2f });
                    //排队多步滚动
                    HalibutUIPanel.Instance.QueueScroll(HalibutUIPanel.scrollStep);
                }
            }
            else {
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
            }

            hoverSengs = Math.Clamp(hoverSengs, 0f, 1f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            // 根据是否能继续向右滚动来调整颜色
            int maxOffset = Math.Max(0, HalibutUIPanel.Instance.halibutUISkillSlots.Count - HalibutUIPanel.maxVisibleSlots);
            bool canScroll = HalibutUIPanel.Instance.scrollOffset < maxOffset;
            Color buttonColor = canScroll ? Color.White : Color.Gray * 0.5f;

            // 绘制发光效果
            if (canScroll) {
                spriteBatch.Draw(RightButton, DrawPosition + Size / 2, null, Color.Gold with { A = 0 } * hoverSengs, 0, Size / 2, 1.2f, SpriteEffects.None, 0);
            }

            // 绘制按钮
            spriteBatch.Draw(RightButton, DrawPosition, null, buttonColor);
        }
    }
}
