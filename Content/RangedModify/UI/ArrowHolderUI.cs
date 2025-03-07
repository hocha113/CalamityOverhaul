using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.RangedModify.UI
{
    internal class ArrowHolderUI : UIHandle
    {
        public override Texture2D Texture => TextureAssets.Item[ChooseAmmo.type].Value;
        public override bool Active => GlobalBow.IsBow || GlobalBow.IsArrow;
        private static Item HeldWeapon;
        private static Item ChooseAmmo;
        private int Weith;
        private int Height;
        public override void Update() {
            bool weaponIsHand = false;
            if (GlobalBow.IsBow) {//如果拿着的是弓，就赋值
                HeldWeapon = player.GetItem();
                weaponIsHand = true;
            }

            if (!weaponIsHand) {//如果武器不在手上
                bool weaponIsBackpack = false;
                foreach (var item in player.inventory) {
                    if (item.Equals(HeldWeapon)) {
                        weaponIsBackpack = true;
                    }
                }
                if (!weaponIsBackpack) {//并且也不在背包里面
                    HeldWeapon = null;//那么就设置回默认值
                }
            }

            ChooseAmmo = null;

            //如果到这里没有找到有效的弓，就手动遍历
            if (HeldWeapon == null || HeldWeapon.type == ItemID.None) {
                for (int i = player.inventory.Length - 1; i >= 0; i--) {
                    Item item = player.inventory[i];
                    if (item.useAmmo != AmmoID.Arrow) {
                        continue;
                    }
                    HeldWeapon = item;
                }
            }

            if (HeldWeapon == null || HeldWeapon.type == ItemID.None) {
                return;
            }

            ChooseAmmo = player.ChooseAmmo(HeldWeapon);
            if (ChooseAmmo == null) {
                if (GlobalBow.IsArrow) {
                    ChooseAmmo = player.GetItem();
                }
                if (ChooseAmmo == null) {
                    return;
                }
            }

            Weith = Texture.Width;
            Height = Texture.Height;

            int arrowDrawStackCount = ChooseAmmo.stack;
            if (arrowDrawStackCount > 6) {
                arrowDrawStackCount = 6;
            }

            DrawPosition = new Vector2(20, Main.screenHeight - 100);
            DrawPosition += new Vector2(CWRServerConfig.Instance.CartridgeUI_Offset_X_Value
                , -CWRServerConfig.Instance.CartridgeUI_Offset_Y_Value);

            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, Weith + arrowDrawStackCount * Weith / 2, Height);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = UIHitBox.Intersects(mouseHit);

            if (hoverInMainPage) {
                if (Main.playerInventory) {
                    player.mouseInterface = true;
                }

                if (keyLeftPressState == KeyPressState.Pressed && GlobalBow.IsArrow) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    HeldWeapon.CWR().TargetLockAmmo = player.GetItem().Clone();
                }
                if (keyRightPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    HeldWeapon.CWR().TargetLockAmmo = new Item();
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (ChooseAmmo == null || HeldWeapon == null || HeldWeapon.type == ItemID.None || ChooseAmmo.type == ItemID.None) {
                return;
            }

            int arrowDrawStackCount = ChooseAmmo.stack;
            if (arrowDrawStackCount > 6) {
                arrowDrawStackCount = 6;
            }

            Rectangle rectangle = CWRUtils.GetRec(CWRAsset.Quiver_back_Asset.Value, 0, 4);
            spriteBatch.Draw(CWRAsset.Quiver_back_Asset.Value, DrawPosition, rectangle, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
            if (GlobalBow.IsArrow) {
                Vector2 drawPos = DrawPosition + new Vector2(CWRAsset.Quiver_back_Asset.Width() + arrowDrawStackCount * Weith / 4, Height);
                //drawPos += TextureAssets.Item[HeldWeapon.type].Value.Size() / 2;
                float rotOffset = MathHelper.PiOver2;
                if (CWRLoad.ItemIsCrossBow[HeldWeapon.type]) {
                    rotOffset = 0;
                    drawPos += new Vector2(-16, 16);
                }
                VaultUtils.SimpleDrawItem(spriteBatch, HeldWeapon.type, drawPos, 40, rotation: rotOffset, orig: new Vector2(0.001f, 0.001f));
                if (hoverInMainPage) {
                    Texture2D aim = CWRAsset.AimTarget.Value;
                    spriteBatch.Draw(aim, MousePosition, null, Color.White, 0, aim.Size() / 2, 0.1f, SpriteEffects.None, 0);
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, CWRLocText.Instance.ArrowHolderUI_Text0.Value
                    , MousePosition.X + 0, MousePosition.Y + 50, Color.Goldenrod, Color.Black, Vector2.Zero, 1f);
                }
            }

            Item targetLockAmmo = HeldWeapon.CWR().TargetLockAmmo;

            if (ChooseAmmo.type > ItemID.None) {
                for (int i = 0; i < arrowDrawStackCount; i++) {
                    Vector2 drawPos = DrawPosition + new Vector2(CWRAsset.Quiver_back_Asset.Width() + i * Weith / 2, 0);
                    VaultUtils.SimpleDrawItem(spriteBatch, ChooseAmmo.type, drawPos, 40, orig: new Vector2(0.001f, 0.001f));
                    if (i == arrowDrawStackCount - 1) {
                        Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, ChooseAmmo.stack.ToString()
                        , drawPos.X - Weith, drawPos.Y + Height, Color.White, Color.Black, new Vector2(0.2f), 0.8f);
                    }
                }

                if (hoverInMainPage) {
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, ChooseAmmo.Name
                    , MousePosition.X, MousePosition.Y + 30, Color.Goldenrod, Color.Black, Vector2.Zero, 1f);

                    if (targetLockAmmo != null && targetLockAmmo.type > ItemID.None) {
                        Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, CWRLocText.Instance.ArrowHolderUI_Text1.Value
                        , MousePosition.X + 0, MousePosition.Y + 50, Color.Goldenrod, Color.Black, Vector2.Zero, 1f);
                    }
                }
            }

            if (targetLockAmmo != null && targetLockAmmo.type > ItemID.None) {
                Texture2D aim = CWRAsset.AimTarget.Value;
                spriteBatch.Draw(aim, DrawPosition, null, Color.White, 0, aim.Size() / 2, 0.1f, SpriteEffects.None, 0);
                VaultUtils.SimpleDrawItem(spriteBatch, targetLockAmmo.type, DrawPosition, 32);
            }
        }
    }
}
