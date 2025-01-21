using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GunCustomization.UI.AmmoView;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs
{
    internal class CartridgeHolderUI : UIHandle
    {
        public static CartridgeHolderUI Instance => UIHandleLoader.GetUIHandleOfType<CartridgeHolderUI>();
        public static Texture2D TextureValue;
        public static float JARSengs;
        public static CWRItems cwrWeapon;
        public static Item targetWeapon;
        private Item heldItem => player.GetItem();
        private float otherPotData;
        internal int Weith;
        internal int Height;
        private int rightHeldTime;
        public override bool Active => CWRServerConfig.Instance.MagazineSystem && (CWRLoad.ItemHasCartridgeHolder[heldItem.type] || IsAmmo());
        public bool IsAmmo() => heldItem.ammo != AmmoID.None;
        public override void Update() {
            if (CWRLoad.ItemHasCartridgeHolder[heldItem.type]) {
                targetWeapon = heldItem;
                cwrWeapon = targetWeapon.CWR();
            }
            //如果是最开始，没有手持枪械但是拿着子弹时，手动搜索玩家背包里面的枪
            if (targetWeapon == null || cwrWeapon == null || IsAmmo()) {
                foreach (var item in player.inventory) {
                    if (!CWRLoad.ItemHasCartridgeHolder[heldItem.type]) {
                        continue;
                    }
                    targetWeapon = item;
                    cwrWeapon = targetWeapon.CWR();
                }
            }

            if (cwrWeapon == null) {
                return;
            }

            if (TextureValue != null) {
                UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, TextureValue.Width, TextureValue.Height);
                if (cwrWeapon.CartridgeType == CartridgeUIEnum.Magazines) {
                    UIHitBox = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, TextureValue.Width, TextureValue.Height / 6);
                }
                hoverInMainPage = UIHitBox.Intersects(new Rectangle((int)MousePosition.X, (int)MousePosition.Y, 1, 1));
            }

            if (hoverInMainPage) {
                if (keyRightPressState == KeyPressState.Held) {
                    rightHeldTime++;
                }
                else {
                    rightHeldTime = 0;
                }

                if (IsAmmo()) {
                    player.mouseInterface = true;
                    bool canContrl = false;
                    int addStact = 1;
                    if (keyRightPressState == KeyPressState.Pressed || rightHeldTime > 20) {
                        canContrl = true;
                    }
                    else if (keyLeftPressState == KeyPressState.Pressed) {
                        canContrl = true;
                        addStact = 0;
                    }

                    if (canContrl) {
                        if (cwrWeapon.NumberBullets < cwrWeapon.AmmoCapacity) {
                            SoundEngine.PlaySound(CWRSound.Gun_Clipin);
                            cwrWeapon.LoadenMagazine(heldItem, addStact);
                        }
                        else {
                            SoundEngine.PlaySound(CWRSound.Gun_ClipinLocked);
                        }
                    }
                }
                else {
                    bool leisure = true;
                    if (player.CWR().TryGetInds_BaseFeederGun(out BaseFeederGun gun) && gun.SafeMousetStart2) {
                        leisure = false;
                    }

                    if (keyRightPressState == KeyPressState.Pressed && leisure) {
                        SoundEngine.PlaySound(CWRSound.loadTheRounds, player.Center);
                        foreach (Item ammo in cwrWeapon.MagazineContents) {
                            if (ammo.type == ItemID.None || ammo.stack <= 0) {
                                continue;
                            }
                            if (!ammo.CWR().AmmoProjectileReturn) {
                                continue;
                            }
                            player.QuickSpawnItem(player.FromObjectGetParent(), new Item(ammo.type), ammo.stack);
                        }
                        cwrWeapon.InitializeMagazine();
                    }
                }
            }

            if (player.CWR().PlayerIsKreLoadTime > 0) {
                otherPotData -= 0.8f;
            }
            else {
                otherPotData = 0;
            }

            AmmoViewUI.Instance.Update();
        }

        public void Initialize() {
            if (cwrWeapon.CartridgeType == CartridgeUIEnum.CartridgeHolder) {
                DrawPosition = new Vector2(20, Main.screenHeight - 100);
                string key = "BulletCard";
                string key2 = "";
                if (cwrWeapon.SpecialAmmoState == SpecialAmmoStateEnum.napalmBomb) {
                    key2 = "_napalmBomb";
                }
                if (cwrWeapon.SpecialAmmoState == SpecialAmmoStateEnum.armourPiercer) {
                    key2 = "_armourPiercer";
                }
                if (cwrWeapon.SpecialAmmoState == SpecialAmmoStateEnum.highExplosive) {
                    key2 = "_highExplosive";
                }
                if (cwrWeapon.SpecialAmmoState == SpecialAmmoStateEnum.dragonBreath) {
                    key2 = "_dragonBreath";
                }
                if (heldItem.useAmmo == AmmoID.Rocket) {
                    key = "GrenadeRound";
                }
                TextureValue = CWRUtils.GetT2DValue($"CalamityOverhaul/Assets/UIs/{key}" + key2);
            }
            if (cwrWeapon.CartridgeType == CartridgeUIEnum.Magazines) {
                DrawPosition = new Vector2(60, Main.screenHeight - 100);
                TextureValue = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/Magazines");
            }
            if (cwrWeapon.CartridgeType == CartridgeUIEnum.JAR) {
                DrawPosition = new Vector2(60, Main.screenHeight - 100);
                TextureValue = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/JAR");
            }
            Weith = TextureValue.Width;
            Height = TextureValue.Height;
            DrawPosition += new Vector2(CWRServerConfig.Instance.CartridgeUI_Offset_X_Value
                , -CWRServerConfig.Instance.CartridgeUI_Offset_Y_Value);
        }

        private void DrawToolp(SpriteBatch spriteBatch, CWRItems cwrItem) {
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, cwrWeapon.NumberBullets.ToString()
                , DrawPosition.X + Weith + 2, DrawPosition.Y, Color.AliceBlue, Color.Black, Vector2.Zero, 1.3f);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, "Max"
                , DrawPosition.X + Weith + 2, DrawPosition.Y + 22, Color.Gold, Color.Black, Vector2.Zero, 1f);
            Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, cwrItem.AmmoCapacity.ToString()
                , DrawPosition.X + Weith + 32 + 2, DrawPosition.Y + 22, Color.Gold, Color.Black, Vector2.Zero, 1.05f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (cwrWeapon == null || targetWeapon == null) {
                return;
            }

            Initialize();

            if (AmmoViewUI.Instance.Active) {
                foreach (AmmoItemElement ammoItem in AmmoViewUI.Instance.ammoItemElements) {
                    ammoItem.Draw(spriteBatch);
                }
            }

            if (cwrWeapon.CartridgeType == CartridgeUIEnum.CartridgeHolder) {
                spriteBatch.Draw(TextureValue, DrawPosition, null, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
                DrawToolp(spriteBatch, cwrWeapon);
            }

            if (cwrWeapon.CartridgeType == CartridgeUIEnum.Magazines) {
                Rectangle rectangle = CWRUtils.GetRec(TextureValue, 6 - cwrWeapon.NumberBullets, 7);
                spriteBatch.Draw(TextureValue, DrawPosition + rectangle.Size() / 2, rectangle, Color.White
                    , otherPotData, rectangle.Size() / 2, 1, SpriteEffects.None, 0);
                DrawToolp(spriteBatch, cwrWeapon);
            }

            if (cwrWeapon.CartridgeType == CartridgeUIEnum.JAR) {
                Texture2D jar2 = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/JAR_Full");
                Texture2D ctb = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/JAR_CTB");
                JARSengs = MathHelper.Lerp(JARSengs, cwrWeapon.NumberBullets / (float)cwrWeapon.AmmoCapacity, 0.05f);
                float sengs = jar2.Height * (1 - JARSengs);
                Rectangle rectangle = new(0, (int)sengs, jar2.Width, (int)(jar2.Height - sengs) + 1);
                spriteBatch.Draw(TextureValue, DrawPosition, null, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
                spriteBatch.Draw(jar2, DrawPosition + new Vector2(4, sengs + 6), rectangle, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
                spriteBatch.Draw(ctb, DrawPosition + new Vector2(4, 6), null, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
            }

            //如果是拿着弹药进行选择性装填，这里就绘制出目标枪体
            if (IsAmmo()) {
                float slp = VaultUtils.GetDrawItemSize(targetWeapon, 64);
                Vector2 drawPos = DrawPosition + new Vector2(0, Height);
                VaultUtils.SimpleDrawItem(spriteBatch, targetWeapon.type, drawPos, slp, 0, Color.White, new Vector2(0.001f));
            }

            if (hoverInMainPage) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, CWRLocText.GetTextValue("CartridgeHolderUI_Text4")
                    , MousePosition.X + 0, MousePosition.Y + 50, Color.Goldenrod, Color.Black, Vector2.Zero, 1f);
            }

            if (AmmoViewUI.Instance.Active) {
                foreach (AmmoItemElement ammoItem in AmmoViewUI.Instance.ammoItemElements) {
                    ammoItem.PostDraw(spriteBatch);
                }
            }
        }
    }
}
