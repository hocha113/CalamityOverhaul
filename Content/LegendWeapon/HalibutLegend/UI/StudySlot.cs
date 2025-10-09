using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    internal class StudySlot : UIHandle
    {
        public static StudySlot Instance => UIHandleLoader.GetUIHandleOfType<StudySlot>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public Item Item = new Item();

        //研究相关字段
        private int researchTimer = 0; //当前研究时间（帧数）
        private const int ResearchDuration = 7200; //研究总时长（2分钟 = 7200帧，60fps * 120秒）
        private bool isResearching = false; //是否正在研究

        public override void Update() {
            Item ??= new Item();
            Size = PictureSlot.Size();
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab);

                    //如果正在研究中，则取出物品并停止研究
                    if (isResearching && Item.Alives() && Item.type > ItemID.None) {
                        Main.mouseItem = Item.Clone();
                        Item.TurnToAir();
                        isResearching = false;
                        researchTimer = 0;
                    }
                    //否则放入新物品并开始研究
                    else if (Main.mouseItem.Alives() && Main.mouseItem.type > ItemID.None) {
                        Item = Main.mouseItem.Clone();
                        Main.mouseItem.TurnToAir();
                        isResearching = true;
                        researchTimer = 0;
                    }
                    //如果槽位有物品但鼠标没有，则取出
                    else if (Item.Alives() && Item.type > ItemID.None) {
                        Main.mouseItem = Item.Clone();
                        Item.TurnToAir();
                        isResearching = false;
                        researchTimer = 0;
                    }
                }
            }

            //更新研究进度
            if (isResearching && Item.Alives() && Item.type > ItemID.None) {
                researchTimer+=100;

                //研究完成
                if (researchTimer >= ResearchDuration) {
                    SoundEngine.PlaySound(SoundID.ResearchComplete);
                    isResearching = false;
                    researchTimer = 0;

                    if (FishSkill.UnlockFishs.TryGetValue(Item.type, out FishSkill fishSkill)) {
                        // 使用新的动画方法，从研究槽位中心飞出
                        Vector2 startPos = DrawPosition + new Vector2(26, 26); // 物品图标中心
                        HalibutUIPanel.Instance.AddSkillWithAnimation(fishSkill, startPos);
                    }

                    Item.TurnToAir();
                }
            }
        }

        [VaultLoaden(CWRConstant.UI + "Halibut/")]
        private static Texture2D PictureSlotMask = null;
        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(PictureSlot, UIHitBox, Color.White);

            //计算研究进度
            float pct = isResearching && researchTimer > 0 ? researchTimer / (float)ResearchDuration : 0f;

            Vector2 barTopLeft = DrawPosition + new Vector2(8, 10);

            //绘制研究的物品
            if (Item.Alives() && Item.type > ItemID.None) {
                Color itemColor = Color.White;
                if (isResearching) {
                    //研究中的物品添加发光效果
                    float glow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.2f + 0.8f;
                    itemColor = Color.White * glow;
                }
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, DrawPosition + new Vector2(26, 26), 40, 1f, 0, itemColor);

                //使用颜色渐变表示进度
                Color fillColor = Color.Lerp(Color.Peru, Color.Gold, pct);
                if (isResearching) {
                    //添加脉动效果
                    float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f + 0.9f;
                    fillColor *= pulse;
                }

                int recOffsetY = (int)(PictureSlotMask.Height * (1f - pct));
                Rectangle rectangle = new Rectangle(0, recOffsetY, PictureSlotMask.Width, PictureSlotMask.Height - recOffsetY);
                spriteBatch.Draw(PictureSlotMask, barTopLeft + new Vector2(0, recOffsetY), rectangle, fillColor, 0, Vector2.Zero, 1f, SpriteEffects.None, 0);
            }

            //绘制研究剩余时间文本
            if (isResearching && hoverInMainPage) {
                int remainingSeconds = (ResearchDuration - researchTimer) / 60;
                int minutes = remainingSeconds / 60;
                int seconds = remainingSeconds % 60;
                string timeText = $"{minutes:D2}:{seconds:D2}";

                Vector2 textPos = DrawPosition + new Vector2(30, -0);
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(timeText);
                Utils.DrawBorderString(spriteBatch, timeText, textPos - textSize / 2, Color.Gold, 0.8f);
            }

            //绘制进度百分比
            if (isResearching && pct > 0) {
                string percentText = $"{(int)(pct * 100)}%";
                Vector2 percentPos = DrawPosition + new Vector2(30, 65);
                Vector2 percentSize = FontAssets.MouseText.Value.MeasureString(percentText);
                Utils.DrawBorderString(spriteBatch, percentText, percentPos - percentSize / 2, Color.White, 0.7f);
            }
        }
    }
}
