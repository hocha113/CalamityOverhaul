using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    [VaultLoaden(CWRConstant.UI + "Halibut/")]//用反射标签加载对应文件夹下的所有资源
    internal static class HalibutUIAsset
    {
        //按钮，大小46*26
        public static Texture2D Button;
        //大比目鱼的头像图标，放置在屏幕左下角作为UI的入口，大小74*74
        public static Texture2D Head;
        //左侧边栏，大小218*42
        public static Texture2D LeftSidebar;
        //面板，大小242*214
        public static Texture2D Panel;
        //图标栏，大小60*52
        public static Texture2D PictureSlot;
        //技能图标，大小170*34，共五帧，对应五种技能的图标
        public static Texture2D Skillcon;
    }

    internal class HalibutUIHead : UIHandle
    {
        public static HalibutUIHead Instance => UIHandleLoader.GetUIHandleOfType<HalibutUIHead>();
        private bool _active;
        public override bool Active {
            get {
                if (!Main.playerInventory || !_active) {
                    _active = player.GetItem().type == HalibutOverride.ID;
                }
                return _active;
            }
        }
        public bool Open;
        public int HeldSkillID = -1;
        public override void Update() {
            Size = Head.Size();
            DrawPosition = new Vector2(-4, Main.screenHeight - Size.Y);
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    Open = !Open;
                    SoundEngine.PlaySound(CWRSound.ButtonZero);
                }
            }

            HalibutUILeftSidebar.Instance.Update();
            HalibutUIPanel.Instance.Update();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            HalibutUIPanel.Instance.Draw(spriteBatch);
            HalibutUILeftSidebar.Instance.Draw(spriteBatch);

            spriteBatch.Draw(Head, UIHitBox, Color.White);

            if (HeldSkillID >= 0) {//添加一个14的偏移量让这个技能图标刚好覆盖眼睛
                spriteBatch.Draw(Skillcon, DrawPosition + new Vector2(14), Skillcon.GetRectangle(HeldSkillID, 5), Color.White);
            }
        }
    }

    internal class HalibutUILeftSidebar : UIHandle
    {
        public static HalibutUILeftSidebar Instance => UIHandleLoader.GetUIHandleOfType<HalibutUILeftSidebar>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public float Sengs;
        public override void Update() {
            if (HalibutUIHead.Instance.Open) {
                if (Sengs < 1f) {
                    Sengs += 0.1f;
                }
            }
            else {
                if (Sengs > 0f && HalibutUIPanel.Instance.Sengs <= 0f) {//面板完全关闭后侧边栏才开始关闭
                    Sengs -= 0.1f;
                }
            }

            Sengs = Math.Clamp(Sengs, 0f, 1f);

            Size = LeftSidebar.Size();
            int topHeight = (int)((Size.Y - 60) * Sengs);//侧边栏要抬高的距离，减60是为了让侧边栏别完全升出来
            DrawPosition = new Vector2(0, Main.screenHeight - HalibutUIHead.Instance.Size.Y - 20 - topHeight);//28刚好是侧边栏顶部高礼帽的高度
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);


        }
        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(LeftSidebar, UIHitBox, Color.White);
        }
    }

    internal class HalibutUIPanel : UIHandle
    {
        public static HalibutUIPanel Instance => UIHandleLoader.GetUIHandleOfType<HalibutUIPanel>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public List<SkillSlot> halibutUISkillSlots = [];
        public List<FishSkill> fishSkills = [];
        public float Sengs;
        public override void Update() {
            halibutUISkillSlots ??= [];
            if (HalibutUILeftSidebar.Instance.Sengs >= 1f && HalibutUIHead.Instance.Open) {//侧边栏完全打开后才开始打开面板
                if (Sengs < 1f) {
                    Sengs += 0.1f;
                }
            }
            else {
                if (Sengs > 0f) {
                    Sengs -= 0.1f;
                }
            }

            Sengs = Math.Clamp(Sengs, 0f, 1f);

            Size = Panel.Size();
            int leftWeith = (int)(20 - 200 * (1f - Sengs));
            int topHeight = (int)Size.Y;
            if (HalibutUILeftSidebar.Instance.Sengs < 1f) {
                topHeight = (int)(Size.Y * HalibutUILeftSidebar.Instance.Sengs);
            }
            DrawPosition = new Vector2(leftWeith, Main.screenHeight - topHeight);
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            StudySlot.Instance.DrawPosition = DrawPosition + new Vector2(80, Size.Y / 2);
            StudySlot.Instance.Update();

            int index = 0;
            foreach (var slot in halibutUISkillSlots.ToList()) {
                slot.DrawPosition = DrawPosition + new Vector2(12, 30);
                slot.DrawPosition.X += index % 5 * (Skillcon.Width + 4);
                slot.DrawPosition.Y += index / 5 * (Skillcon.Height / 5 + 4);
                slot.SkillID = index;
                slot.Update();
                index++;
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(Panel, UIHitBox, Color.White);

            StudySlot.Instance.Draw(spriteBatch);

            foreach (var slot in halibutUISkillSlots.ToList()) {
                slot.Draw(spriteBatch);
            }
        }
    }

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
                researchTimer++;

                //研究完成
                if (researchTimer >= ResearchDuration) {
                    SoundEngine.PlaySound(SoundID.ResearchComplete);
                    isResearching = false;
                    researchTimer = 0;
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

    internal class SkillSlot : UIHandle
    {
        public static SkillSlot Instance => UIHandleLoader.GetUIHandleOfType<SkillSlot>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public int SkillID;//对应的技能ID，也觉得其绘制帧
        public float hoverSengs;
        public override void Update() {
            Size = new Vector2(Skillcon.Width, Skillcon.Height / 5);
            UIHitBox = DrawPosition.GetRectangle((int)Size.X, (int)(Size.Y));
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                if (hoverSengs < 1f) {
                    hoverSengs += 0.1f;
                }
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    HalibutUIHead.Instance.HeldSkillID = SkillID;
                }
            }
            else {
                if (hoverSengs > 0f) {
                    hoverSengs -= 0.1f;
                }
            }
        }
        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(Skillcon, DrawPosition + Size / 2, Skillcon.GetRectangle(SkillID, 5), Color.Gold with { A = 0 } * hoverSengs, 0, Size / 2, 1.2f, SpriteEffects.None, 0);
            spriteBatch.Draw(Skillcon, DrawPosition, Skillcon.GetRectangle(SkillID, 5), Color.White);
        }
    }
}