using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal class FishList : UIHandle
    {
        public override bool Active => PiscicultureUI.Instance.Active || bordered_sengs > 0;
        public static FishList Instance => UIHandleLoader.GetUIHandleOfType<FishList>();
        public override float RenderPriority => 2;
        public List<FishDisplayUI> fishDisplayUIs = [];
        public float bordered_sengs;
        public void AddFish(FishSkill fishSkill) {
            FishDisplayUI fishDisplayUI = new FishDisplayUI();
            fishDisplayUI.FishSkill = fishSkill;
            fishDisplayUIs.Add(fishDisplayUI);
        }
        public bool HasFish(FishSkill fishSkill) {
            foreach (var fish in fishDisplayUIs) {
                if (fish.FishSkill.Item.type == fishSkill.Item.type) {
                    return true;
                }
            }
            return false;
        }
        public override void Update() {
            if (PiscicultureUI.Instance.Active) {
                if (bordered_sengs < 0) {
                    bordered_sengs += 0.1f;
                }
            }
            else {
                if (bordered_sengs > 0) {
                    bordered_sengs -= 0.1f;
                }
            }

            DrawPosition =
                new Vector2(PiscicultureUI.Dialogbox.DrawPosition.X + PiscicultureUI.Dialogbox.borderedWidth
                , PiscicultureUI.Dialogbox.DrawPosition.Y);

            float currentXOffset = 0; // 当前列的X偏移量
            float currentYOffset = 0; // 当前列的Y偏移量
            float maxColumnWidth = 0; // 当前列中最大的元素宽度
            const float columnSpacing = 10; // 列间距

            for (int i = 0; i < fishDisplayUIs.Count; i++) {
                if (DrawPosition.Y + currentYOffset + fishDisplayUIs[i].borderedHeight > Main.screenHeight) {
                    currentXOffset += maxColumnWidth + columnSpacing;
                    currentYOffset = 0;
                    maxColumnWidth = 0;
                }
                fishDisplayUIs[i].DrawPosition = DrawPosition + new Vector2(currentXOffset, currentYOffset);
                maxColumnWidth = Math.Max(maxColumnWidth, fishDisplayUIs[i].borderedWidth);
                currentYOffset += fishDisplayUIs[i].borderedHeight;
                fishDisplayUIs[i].Update();
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            for (int i = 0; i < fishDisplayUIs.Count; i++) {
                fishDisplayUIs[i].Draw(spriteBatch);
            }
        }
    }
}
