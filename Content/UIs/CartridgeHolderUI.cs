using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using CalamityOverhaul.Content.UIs.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs
{
    internal class CartridgeHolderUI : CWRUIPanel
    {
        public static class Date
        {
            public static float JARSengs;
        }

        public static CartridgeHolderUI Instance;
        public static Texture2D TextureValue;
        private Item handItem => player.ActiveItem();
        private int bulletNum => player.ActiveItem().CWR().NumberBullets;
        private Rectangle mainRec;
        private bool onMainP;
        public bool Active {
            get {
                if (handItem.type == ItemID.None) {
                    return false;
                }
                return handItem.CWR().HasCartridgeHolder;
            }
        }

        public bool OnMainP => onMainP;

        public override void Load() => Instance = this;

        public override void Update(GameTime gameTime) {
            CWRItems cwrItem = handItem.CWR();
            if (TextureValue != null) {
                mainRec = new Rectangle((int)DrawPos.X, (int)DrawPos.Y, TextureValue.Width, TextureValue.Height);
                if (cwrItem.CartridgeEnum == CartridgeUIEnum.Magazines)
                    mainRec = new Rectangle((int)DrawPos.X, (int)DrawPos.Y, TextureValue.Width, TextureValue.Height / 6);
                onMainP = mainRec.Intersects(new Rectangle((int)MouPos.X, (int)MouPos.Y, 1, 1));
            }
            if (onMainP) {
                bool mr2 = true;
                if (player.CWR().TryGetInds_BaseFeederGun(out BaseFeederGun gun)) {
                    if (gun.SafeMousetStart2) {
                        mr2 = false;
                    }
                }
                int mr = DownStartR();
                if (mr == 1 && mr2) {
                    SoundEngine.PlaySound(CWRSound.loadTheRounds, player.Center);
                    foreach (Item i in cwrItem.MagazineContents) {
                        if (i.type == ItemID.None || i.stack <= 0) {
                            continue;
                        }
                        if (!i.CWR().AmmoProjectileReturn) {
                            continue;
                        }
                        player.QuickSpawnItem(player.parent(), new Item(i.type), i.stack);
                    }
                    cwrItem.InitializeMagazine();
                    cwrItem.SpecialAmmoState = SpecialAmmoStateEnum.ordinary;
                }
            }
            time++;
        }

        public override void Initialize() {
            CWRItems cwrItem = handItem.CWR();
            if (cwrItem.CartridgeEnum == CartridgeUIEnum.CartridgeHolder) {
                DrawPos = new Vector2(20, Main.screenHeight - 100);
                string key = "BulletCard";
                string key2 = "";
                if (cwrItem.SpecialAmmoState == SpecialAmmoStateEnum.napalmBomb) {
                    key2 = "_Fire";
                }
                if (cwrItem.SpecialAmmoState == SpecialAmmoStateEnum.armourPiercer) {
                    key2 = "_AP";
                }
                if (cwrItem.SpecialAmmoState == SpecialAmmoStateEnum.highExplosive) {
                    key2 = "_SH";
                }
                if (cwrItem.SpecialAmmoState == SpecialAmmoStateEnum.dragonBreath) {
                    key2 = "_DBC";
                }
                if (handItem.useAmmo == AmmoID.Rocket) {
                    key = "GrenadeRound";
                }
                TextureValue = CWRUtils.GetT2DValue($"CalamityOverhaul/Assets/UIs/{key}" + key2);
            }
            if (cwrItem.CartridgeEnum == CartridgeUIEnum.Magazines) {
                DrawPos = new Vector2(60, Main.screenHeight - 100);
                TextureValue = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/Magazines");
            }
            if (cwrItem.CartridgeEnum == CartridgeUIEnum.JAR) {
                DrawPos = new Vector2(60, Main.screenHeight - 100);
                TextureValue = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/JAR");
            }
            DrawPos += new Vector2(CWRServerConfig.Instance.CartridgeUI_Offset_X_Value, -CWRServerConfig.Instance.CartridgeUI_Offset_Y_Value);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Initialize();
            CWRItems cwrItem = handItem.CWR();
            if (cwrItem.CartridgeEnum == CartridgeUIEnum.CartridgeHolder) {
                spriteBatch.Draw(TextureValue, DrawPos, null, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, bulletNum.ToString()
                    , DrawPos.X + 50, DrawPos.Y + 0, Color.AliceBlue, Color.Black, Vector2.Zero, 1.3f);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, "Max"
                    , DrawPos.X + 50, DrawPos.Y + 22, Color.Gold, Color.Black, Vector2.Zero, 1f);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, cwrItem.AmmoCapacity.ToString()
                    , DrawPos.X + 85, DrawPos.Y + 22, Color.Gold, Color.Black, Vector2.Zero, 1.05f);
            }
            if (cwrItem.CartridgeEnum == CartridgeUIEnum.Magazines) {
                spriteBatch.Draw(TextureValue, DrawPos, CWRUtils.GetRec(TextureValue, 6 - bulletNum, 7), Color.White
                    , 0f, CWRUtils.GetOrig(TextureValue, 7), 2, SpriteEffects.None, 0);
            }
            if (cwrItem.CartridgeEnum == CartridgeUIEnum.JAR) {
                Texture2D jar2 = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/JAR_Full");
                Date.JARSengs = MathHelper.Lerp(Date.JARSengs, bulletNum / (float)cwrItem.AmmoCapacity, 0.05f);
                float sengs = jar2.Height * (1 - Date.JARSengs);
                Rectangle rectangle = new(0, (int)sengs, jar2.Width, (int)(jar2.Height - sengs));
                spriteBatch.Draw(TextureValue, DrawPos, null, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
                spriteBatch.Draw(jar2, DrawPos + new Vector2(29, sengs + 27), rectangle, Color.White, 0f, Vector2.Zero, 1, SpriteEffects.None, 0);
            }
            if (onMainP) {
                string text = $"{CWRLocText.GetTextValue("CartridgeHolderUI_Text1")}\n";
                int value = 0;
                if (cwrItem.MagazineContents != null && cwrItem.MagazineContents.Length > 0) {
                    foreach (Item i in cwrItem.MagazineContents) {
                        if (i == null) {
                            continue;
                        }
                        if (i.type != ItemID.None && i.ammo != AmmoID.None) {
                            text += $"{i.Name} {CWRLocText.GetTextValue("CartridgeHolderUI_Text2")}: {i.stack}\n";
                            value++;
                        }
                    }
                }
                if (value == 0) {
                    text += CWRLocText.GetTextValue("CartridgeHolderUI_Text3");
                    value = 1;
                }

                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, text
                    , MouPos.X + 0, MouPos.Y - 30 - value * 30, Color.AliceBlue, Color.Black, Vector2.Zero, 1f);
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.ItemStack.Value, CWRLocText.GetTextValue("CartridgeHolderUI_Text4")
                    , MouPos.X + 0, MouPos.Y + 50, Color.Goldenrod, Color.Black, Vector2.Zero, 1.1f);
            }
        }
    }
}
