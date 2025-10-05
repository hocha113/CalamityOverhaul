﻿using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
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
        public List<HalibutUISkillSlot> halibutUISkillSlots = [];
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

            HalibutUIStudySlot.Instance.DrawPosition = DrawPosition + new Vector2(80, Size.Y / 2);
            HalibutUIStudySlot.Instance.Update();

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

            HalibutUIStudySlot.Instance.Draw(spriteBatch);

            foreach (var slot in halibutUISkillSlots.ToList()) {
                slot.Draw(spriteBatch);
            }
        }
    }

    internal class HalibutUIStudySlot : UIHandle
    {
        public static HalibutUIStudySlot Instance => UIHandleLoader.GetUIHandleOfType<HalibutUIStudySlot>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public Item Item = new Item();
        public override void Update() {
            Item ??= new Item();
            Size = PictureSlot.Size();
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    Item = Main.mouseItem.Clone();
                    Main.mouseItem.TurnToAir();
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(PictureSlot, UIHitBox, Color.White);

            //研究进度条，待完善
            int cur = 1;
            int max = 1;
            float pct = max == 0 ? 0 : cur / (float)max;
            //底部进度条
            int barW = 80;
            int barH = 6;
            Vector2 barTopLeft = DrawPosition + new Vector2(-10, 56);
            //背景
            DrawRect(spriteBatch, barTopLeft, barW, barH, new Color(30, 20, 10, 200) * 1);
            //填充
            DrawRect(spriteBatch, barTopLeft, (int)(barW * pct), barH, Color.Lerp(Color.Peru, Color.Gold, pct) * (0.8f + 0.2f * 1) * 1);

            if (Item.Alives() && Item.type > ItemID.None) {//绘制研究的物品
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, DrawPosition + new Vector2(26, 26), 40, 1f, 0, Color.White);
            }
        }

        private static void DrawRect(SpriteBatch sb, Vector2 pos, int w, int h, Color c) {
            sb.Draw(VaultAsset.placeholder2.Value, new Rectangle((int)pos.X, (int)pos.Y, w, h), c);
        }
    }

    internal class HalibutUISkillSlot : UIHandle
    {
        public static HalibutUISkillSlot Instance => UIHandleLoader.GetUIHandleOfType<HalibutUISkillSlot>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;//不被自动更新，需要手动调用Update和Draw
        public int SkillID;//对应的技能ID，也觉得其绘制帧
        public float hoverSengs;
        public override void Update() {
            Size = new Vector2((int)Skillcon.Width, (int)(Skillcon.Height / 5));
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