using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using CalamityOverhaul.Content.UIs.CompressorUIs;
using InnoVault.UIHandles;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.GunCustomization.UI.AmmoView
{
    internal class AmmoItemElement : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.None;
        public static int Weith => 32;
        public static int Height => 32;
        private float _sengs;
        public Item Ammo = new Item();
        public override void Update() {
            UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, Weith, Height);
            Rectangle mouseHit = new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1);
            hoverInMainPage = UIHitBox.Intersects(mouseHit);
            if (hoverInMainPage) {
                if (_sengs < 1f) {
                    _sengs += 0.08f;
                }
                player.mouseInterface = true;
                AmmoViewUI.Instance.ItemVsActive = true;

                bool mr2 = true;
                if (player.CWR().TryGetInds_BaseFeederGun(out BaseFeederGun gun)) {
                    if (gun.SafeMousetStart2) {
                        mr2 = false;
                    }
                }

                if (keyRightPressState == KeyPressState.Pressed && mr2) {
                    CWRItems cwrItem = player.GetItem().CWR();
                    SoundEngine.PlaySound(CWRSound.loadTheRounds, player.Center);

                    List<Item> newMagazine = [];
                    foreach (var ammo in cwrItem.MagazineContents) {
                        if (ammo.type == Ammo.type) {
                            continue;
                        }
                        newMagazine.Add(ammo);
                    }
                    cwrItem.MagazineContents = newMagazine.ToArray();

                    if (Ammo.CWR().AmmoProjectileReturn) {
                        player.QuickSpawnItem(player.FromObjectGetParent(), Ammo);
                    }

                    if (cwrItem.MagazineContents.Length > 0) {
                        AmmoViewUI.Instance.LoadAmmos(cwrItem);
                    }
                    else {
                        cwrItem.InitializeMagazine();
                    }
                }
            }
            else {
                if (_sengs > 0f) {
                    _sengs -= 0.08f;
                }
            }

            _sengs = MathHelper.Clamp(_sengs, 0, 1f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (Ammo.type <= ItemID.None) {
                return;
            }

            string text = Ammo.stack.ToString();
            if (hoverInMainPage) {
                text = $"{Ammo.Name} {CWRLocText.GetTextValue("CartridgeHolderUI_Text2")}: {Ammo.stack}";
            }
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text);

            VaultUtils.DrawBorderedRectangle(spriteBatch, CWRAsset.UI_JAR.Value, 2, DrawPosition
                , Weith + (int)(_sengs * textSize.X), Height, Color.AliceBlue * 0.8f, Color.Azure * 0.2f, 1);

            float drawSize = VaultUtils.GetDrawItemSize(Ammo, Weith) * 1;
            Vector2 drawPos = DrawPosition + new Vector2(Weith, Height) / 2;
            Main.instance.LoadItem(Ammo.type);
            VaultUtils.SimpleDrawItem(spriteBatch, Ammo.type, drawPos, drawSize, 0, Color.White);

            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text, drawPos.X + 18, drawPos.Y - 8
                , Ammo.CWR().AmmoProjectileReturn ? Color.White : Color.Goldenrod, Color.Black, new Vector2(0.2f), 0.8f);

            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, CWRLocText.GetTextValue("CartridgeHolderUI_Text4")
                    , MousePosition.X + 0, MousePosition.Y + 50, Color.Goldenrod, Color.Black, Vector2.Zero, 1f);
            }
        }
    }
}
