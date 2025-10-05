using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
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
        public float Sengs;
        public override void Update() {
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
        }
        public override void Draw(SpriteBatch spriteBatch) {
            spriteBatch.Draw(Panel, UIHitBox, Color.White);
        }
    }
}
