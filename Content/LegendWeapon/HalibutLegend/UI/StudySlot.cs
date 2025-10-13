using CalamityOverhaul.Common;
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
        private int researchTimer = 0;//当前研究时间（帧数）
        private const int ResearchDuration = 7200;//研究总时长（2分钟 = 7200帧，60fps * 120秒）
        private bool isResearching = false;//是否正在研究

        //复苏系统交互
        private const float ResurrectionMaxIncreasePerFish = 15f;//每条新鱼提升上限

        ///<summary>
        ///纯逻辑更新: 研究进度推进、研究完成处理、复苏系统数值变更
        ///</summary>
        internal void LogicUpdate() {
            if (!isResearching) {
                return;
            }
            if (!Item.Alives() || Item.type <= ItemID.None) {
                isResearching = false;
                researchTimer = 0;
                return;
            }
            researchTimer += 100;//推进进度 (与原逻辑保持一致的加速)
            if (researchTimer < ResearchDuration) {
                return;
            }
            SoundEngine.PlaySound(SoundID.ResearchComplete);
            isResearching = false;
            researchTimer = 0;
            bool unlockedNewFish = false;
            Vector2 startPos = DrawPosition + new Vector2(26, 26);
            if (FishSkill.UnlockFishs.TryGetValue(Item.type, out FishSkill fishSkill)) {
                HalibutUIPanel.Instance.AddSkillWithAnimation(fishSkill, startPos);
                unlockedNewFish = true;
            }
            if (unlockedNewFish) {
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    var res = halibutPlayer.ResurrectionSystem;
                    float oldMax = res.MaxValue;
                    res.MaxValue = oldMax + ResurrectionMaxIncreasePerFish;
                    res.Reset();//清空当前复苏值
                    float flyNum = Math.Clamp(ResurrectionMaxIncreasePerFish / 3f, 4f, 18f);
                    ResurrectionUI.Instance?.TriggerImproveEffect(startPos, (int)flyNum);
                }
            }
            Item.TurnToAir();
        }

        public override void Update() {
            Item ??= new Item();
            Size = PictureSlot.Size();
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    bool dontStudy = false;
                    HalibutUIPanel.Instance.halibutUISkillSlots.ForEach(slot => {
                        if (slot.FishSkill.UnlockFishID == Main.mouseItem.type) {
                            dontStudy = true;
                        }
                    });
                    if (!FishSkill.UnlockFishs.ContainsKey(Main.mouseItem.type)) {
                        dontStudy = true;//如果不是可以研究的东西也不能被研究啊
                    }
                    if (dontStudy) {//你已经研究过这个鱼了
                        SoundEngine.PlaySound(CWRSound.ButtonZero);
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.Grab);
                        if (isResearching && Item.Alives() && Item.type > ItemID.None) {//正在研究则取出终止
                            Main.mouseItem = Item.Clone();
                            Item.TurnToAir();
                            isResearching = false;
                            researchTimer = 0;
                        }
                        else if (Main.mouseItem.Alives() && Main.mouseItem.type > ItemID.None) {//放入新物品开始
                            Item = Main.mouseItem.Clone();
                            Main.mouseItem.TurnToAir();
                            isResearching = true;
                            researchTimer = 0;
                        }
                        else if (Item.Alives() && Item.type > ItemID.None) {//槽位有物品取出
                            Main.mouseItem = Item.Clone();
                            Item.TurnToAir();
                            isResearching = false;
                            researchTimer = 0;
                        }
                    }
                }
            }
        }

        [VaultLoaden(CWRConstant.UI + "Halibut/")]
        private static Texture2D PictureSlotMask = null;
        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(PictureSlot, UIHitBox, Color.White);
            float pct = isResearching && researchTimer > 0 ? researchTimer / (float)ResearchDuration : 0f;
            Vector2 barTopLeft = DrawPosition + new Vector2(8, 10);
            if (Item.Alives() && Item.type > ItemID.None) {
                Color itemColor = Color.White;
                if (isResearching) {
                    float glow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.2f + 0.8f;
                    itemColor = Color.White * glow;
                }
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, DrawPosition + new Vector2(26, 26), 40, 1f, 0, itemColor);
                Color fillColor = Color.Lerp(Color.White, Color.Blue, pct);
                if (isResearching) {
                    float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f + 0.9f;
                    fillColor *= pulse;
                }
                int recOffsetY = (int)(PictureSlotMask.Height * (1f - pct));
                Rectangle rectangle = new Rectangle(0, recOffsetY, PictureSlotMask.Width, PictureSlotMask.Height - recOffsetY);
                spriteBatch.Draw(PictureSlotMask, barTopLeft + new Vector2(0, recOffsetY), rectangle, fillColor, 0, Vector2.Zero, 1f, SpriteEffects.None, 0);
            }
            if (isResearching && hoverInMainPage) {
                int remainingSeconds = (ResearchDuration - researchTimer) / 60;
                int minutes = remainingSeconds / 60;
                int seconds = remainingSeconds % 60;
                string timeText = $"{minutes:D2}:{seconds:D2}";
                Vector2 textPos = DrawPosition + new Vector2(30, -0);
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(timeText);
                Utils.DrawBorderString(spriteBatch, timeText, textPos - textSize / 2, Color.Gold, 0.8f);
            }
            if (isResearching && pct > 0) {
                string percentText = $"{(int)(pct * 100)}%";
                Vector2 percentPos = DrawPosition + new Vector2(30, 65);
                Vector2 percentSize = FontAssets.MouseText.Value.MeasureString(percentText);
                Utils.DrawBorderString(spriteBatch, percentText, percentPos - percentSize / 2, Color.White, 0.7f);
            }
        }
    }
}
