using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs
{
    internal class ArrowHolderUI : UIHandle
    {
        public override Texture2D Texture => TextureAssets.Item[ChooseAmmo.type].Value;
        public override bool Active => BowActive || IsArrow();
        private static Item handItem => player.GetItem();
        private static Item ChooseAmmo => player.ChooseAmmo(handItem);
        public static bool BowActive => ChooseAmmo != null && handItem != null && (CWRLoad.ItemIsBow[handItem.type] || CWRLoad.ItemIsCrossBow[handItem.type]);
        public static Item targetAmmo;
        private int Weith;
        private int Height;
        public static bool IsArrow() => handItem.ammo == AmmoID.Arrow;
        public override void Update() {
            if (ChooseAmmo != null && ChooseAmmo.type != ItemID.None) {
                Weith = Texture.Width;
                Height = Texture.Height;
            }
            else {
                Weith = 32;
                Height = 32;
            }

            int arrowDrawStackCount = ChooseAmmo.stack;
            if (arrowDrawStackCount > 6) {
                arrowDrawStackCount = 6;
            }

            DrawPosition = new Vector2(20, Main.screenHeight - 100);
            DrawPosition += new Vector2(CWRServerConfig.Instance.CartridgeUI_Offset_X_Value
                , -CWRServerConfig.Instance.CartridgeUI_Offset_Y_Value);

            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, Weith + (arrowDrawStackCount * Weith / 2), Height);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = UIHitBox.Intersects(mouseHit);
            
            if (hoverInMainPage) {
                if (keyLeftPressState == KeyPressState.Pressed && IsArrow()) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    targetAmmo = handItem.Clone();
                }
                if (keyRightPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(SoundID.Grab);
                    targetAmmo = new Item();
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Rectangle rectangle = CWRUtils.GetRec(CWRAsset.Quiver_back_Asset.Value, 0, 4);
            spriteBatch.Draw(CWRAsset.Quiver_back_Asset.Value, DrawPosition, rectangle, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
            if (IsArrow()) {
                if (hoverInMainPage) {
                    Texture2D aim = CWRAsset.AimTarget.Value;
                    spriteBatch.Draw(aim, MousePosition, null, Color.White, 0, aim.Size() / 2, 0.1f, SpriteEffects.None, 0);
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, CWRLocText.GetTextValue("ArrowHolderUI_Text0")
                    , MousePosition.X + 0, MousePosition.Y + 50, Color.Goldenrod, Color.Black, Vector2.Zero, 1f);
                }
            }
            else if (ChooseAmmo.type > ItemID.None) {
                int arrowDrawStackCount = ChooseAmmo.stack;
                if (arrowDrawStackCount > 6) {
                    arrowDrawStackCount = 6;
                }
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

                    if (targetAmmo != null && targetAmmo.type > ItemID.None) {
                        Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, CWRLocText.GetTextValue("ArrowHolderUI_Text1")
                        , MousePosition.X + 0, MousePosition.Y + 50, Color.Goldenrod, Color.Black, Vector2.Zero, 1f);
                    }
                }
            }

            if (targetAmmo != null && targetAmmo.type > ItemID.None) {
                Texture2D aim = CWRAsset.AimTarget.Value;
                spriteBatch.Draw(aim, DrawPosition, null, Color.White, 0, aim.Size() / 2, 0.1f, SpriteEffects.None, 0);
                VaultUtils.SimpleDrawItem(spriteBatch, targetAmmo.type, DrawPosition, 32);
            }
        }
    }
}
