using CalamityMod.Tiles.DraedonStructures;
using CalamityOverhaul.Content.Industrials.Generator.Thermal;
using CalamityOverhaul.Content.Industrials.Generator;
using CalamityOverhaul.Content.Industrials.MaterialFlow;
using CalamityOverhaul.Content.Tiles.Core;
using InnoVault;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.Modifys
{
    internal class ModifyChargingStation : TileOverride
    {
        public override int TargetID => ModContent.TileType<ChargingStation>();
        public override bool? RightClick(int i, int j, Tile tile) {
            if (!TileProcessorLoader.AutoPositionGetTP<ChargingStationTP>(i, j, out var tp)) {
                return false;
            }
            //在没打开UI的情况下，如果拿着东西右键可以直接放上去
            if ((!tp.OpenUI && !Main.LocalPlayer.GetItem().IsAir) || Main.keyState.PressingShift()) {
                if (tp is ChargingStationTP chargingStation) {
                    chargingStation.RightEvent();
                }
                return false;
            }

            tp.OpenUI = !tp.OpenUI;
            //如果是开启，就先关掉其他所有的同类实体的UI
            if (tp.OpenUI) {
                foreach (var tp2 in TileProcessorLoader.TP_InWorld) {
                    if (tp2.ID != tp.ID || tp2.WhoAmI == tp.WhoAmI) {
                        continue;
                    }
                    if (tp2 is ChargingStationTP chargingStation) {
                        chargingStation.OpenUI = false;
                    }
                }
            }

            SoundEngine.PlaySound(SoundID.MenuTick);
            return false;
        }
    }

    internal class ChargingStationTP : BaseBattery, ICWRLoader//是的，把这个东西当成是一个电池会更好写
    {
        public override int TargetTileID => ModContent.TileType<ChargingStation>();
        internal static Asset<Texture2D> Panel { get; private set; }
        internal static Asset<Texture2D> SlotTex { get; private set; }
        internal static Asset<Texture2D> BarTop { get; private set; }
        internal static Asset<Texture2D> BarFull { get; private set; }
        internal bool OpenUI;
        internal float sengs;
        private bool hoverBar;
        private bool hoverSlot;
        private bool boverPanel;
        private bool oldLeftDown;
        private Vector2 MousePos;
        internal Item Item = new Item();
        public override float MaxUEValue => 1000;
        void ICWRLoader.LoadAsset() {
            Panel = CWRUtils.GetT2DAsset(CWRConstant.UI + "Generator/GeneratorPanel");
            SlotTex = CWRUtils.GetT2DAsset("CalamityMod/UI/DraedonsArsenal/ChargerWeaponSlot");
            BarTop = CWRUtils.GetT2DAsset("CalamityMod/UI/DraedonsArsenal/ChargeMeterBorder");
            BarFull = CWRUtils.GetT2DAsset("CalamityMod/UI/DraedonsArsenal/ChargeMeter");
        }
        void ICWRLoader.UnLoadData() {
            Panel = null;
            SlotTex = null;
            BarTop = null;
            BarFull = null;
        }

        public override void SetProperty() => MachineData = new MachineData();

        public void RightEvent() {
            Item item = Main.LocalPlayer.GetItem();

            if (Main.keyState.PressingShift()) {
                if (!Item.IsAir && !VaultUtils.isClient) {
                    int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, Item.Clone());
                    if (!VaultUtils.isSinglePlayer) {
                        NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                    }
                }
                Item.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            if (item.IsAir) {
                return;
            }

            if (!Item.IsAir && !VaultUtils.isClient) {
                int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, Item.Clone());
                if (!VaultUtils.isSinglePlayer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
                Item.TurnToAir();
            }

            Item = item.Clone();
            item.TurnToAir();
            SoundEngine.PlaySound(SoundID.Grab);
        }

        public override void Update() {
            if (OpenUI) {
                if (sengs < 1f) {
                    sengs += 0.1f;
                }
                else {
                    Vector2 drawPos = CenterInWorld + new Vector2(0, -120) * sengs;
                    Rectangle mouseRec = Main.MouseWorld.GetRectangle(1);
                    hoverSlot = (drawPos - SlotTex.Size() / 2).GetRectangle(SlotTex.Size()).Intersects(mouseRec);
                    boverPanel = (drawPos - Panel.Size() / 2 * 0.75f).GetRectangle(Panel.Size() * 0.75f).Intersects(mouseRec);
                    drawPos += new Vector2(-30, 30);
                    hoverBar = drawPos.GetRectangle(60, 22).Intersects(mouseRec);
                    MousePos = Main.MouseScreen;

                    if (boverPanel) {
                        Main.LocalPlayer.mouseInterface = true;
                        Main.LocalPlayer.CWR().DontUseItemTime = 2;
                    }

                    if (hoverSlot) {
                        bool justDown = !oldLeftDown && Main.mouseLeft;
                        oldLeftDown = Main.mouseLeft;
                        if (justDown) {
                            SoundEngine.PlaySound(SoundID.Grab);

                            if (Item.type == ItemID.None) {
                                Item = Main.mouseItem.Clone();
                                Main.mouseItem.TurnToAir();
                            }
                            else {
                                if (Item.type == Main.mouseItem.type) {
                                    Item.stack += Main.mouseItem.stack;
                                    Main.mouseItem.TurnToAir();
                                }
                                else {
                                    Item swopItem = Item.Clone();
                                    Item = Main.mouseItem.Clone();
                                    Main.mouseItem = swopItem;
                                }
                            }
                        }
                    }
                }
            }
            else {
                if (sengs > 0f) {
                    sengs -= 0.1f;
                }
            }
        }

        public void DrawUI(SpriteBatch spriteBatch) {
            Vector2 drawPos = CenterInWorld - Main.screenPosition + new Vector2(0, -120) * sengs;
            spriteBatch.Draw(Panel.Value, drawPos, null, Color.White, 0, Panel.Size() / 2, 0.75f * sengs, SpriteEffects.None, 0);
            spriteBatch.Draw(SlotTex.Value, drawPos, null, Color.White, 0, SlotTex.Size() / 2, sengs, SpriteEffects.None, 0);
            Vector2 origDrawPos = drawPos;
            drawPos += new Vector2(-30, 30);
            int uiBarByWidthSengs = (int)(BarFull.Value.Width * (MachineData.UEvalue / MaxUEValue));
            // 绘制温度相关的图像
            Rectangle fullRec = new Rectangle(0, 0, uiBarByWidthSengs, BarFull.Value.Height);
            Main.spriteBatch.Draw(BarTop.Value, drawPos, null, Color.White * sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(BarFull.Value, drawPos + new Vector2(10, 0), fullRec, Color.White * sengs, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            if (hoverBar) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value
                    , (MachineData.UEvalue + "/" + MaxUEValue + "UE").ToString()
                    , MousePos.X - 10, MousePos.Y + 20, Color.White, Color.Black, new Vector2(0.3f), 0.6f);
            }

            if (hoverSlot && Item.type == ItemID.None && Main.mouseItem.type == ItemID.None) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value
                    , "放入充能物品"
                    , MousePos.X - 10, MousePos.Y + 20, Color.White, Color.Black, new Vector2(0.3f), 0.6f);
            }

            if (Item.type > ItemID.None) {
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, origDrawPos, 34);
                if (Item.stack > 1) {
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value
                    , Item.stack.ToString()
                    , origDrawPos.X - 10, origDrawPos.Y + 12, Color.White, Color.Black, new Vector2(0.3f), 0.6f);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (OpenUI || sengs > 0) {
                DrawUI(spriteBatch);
            }

            Vector2 drawPos = CenterInWorld - Main.screenPosition + new Vector2(0, -24);
            if (Item.type > ItemID.None) {
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, drawPos, 54);
            }
        }
    }
}
