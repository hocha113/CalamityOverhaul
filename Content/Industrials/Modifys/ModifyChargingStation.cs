using CalamityMod;
using CalamityMod.Items.DraedonMisc;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Placeables.DraedonStructures;
using CalamityMod.Tiles.DraedonStructures;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.RemakeItems;
using CalamityOverhaul.Content.TileModify.Core;
using InnoVault.TileProcessors;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.Modifys
{
    internal class ModifyChargingStationItem : CWRItemOverride
    {
        public override int TargetID => ModContent.ItemType<ChargingStationItem>();
        public override bool DrawingInfo => false;
        public override void SetDefaults(Item item) {
            item.CWR().StorageUE = true;
            item.CWR().ConsumeUseUE = 1000;
        }

        public override void AddRecipe() {
            CreateRecipe().
                AddIngredient<DubiousPlating>(10).
                AddIngredient<MysteriousCircuitry>(10).
                AddRecipeGroup(CWRRecipes.GoldBarGroup, 4).
                AddRecipeGroup(CWRRecipes.TinBarGroup, 4).
                AddTile(TileID.Anvils).
                Register();
        }
    }

    internal class ModifyChargingStation : TileOverride
    {
        public override int TargetID => ModContent.TileType<ChargingStation>();
        public override bool? CanDrop(int i, int j, int type) => false;

        public override bool? RightClick(int i, int j, Tile tile) {
            if (!TileProcessorLoader.AutoPositionGetTP<ChargingStationTP>(i, j, out var tp)) {
                return false;
            }
            if (tp is ChargingStationTP chargingStation) {
                chargingStation.RightEvent();
                chargingStation.SendData();
            }
            return false;
        }

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<ChargingStationItem>());
    }

    internal class HandlerChargingStationUI : UIHandle
    {
        //更新所有充能站的UI逻辑
        public override bool Active => TileProcessorLoader.TP_ID_To_InWorld_Count[ChargingStationTP.StaticID] > 0;
        public override void Update() {
            if (!TileProcessorLoader.LoadenTP) {
                return;
            }
            if (!TileProcessorNetWork.LoadenTPByNetWork) {
                return;
            }
            foreach (var tp in TileProcessorLoader.TP_InWorld) {
                if (!tp.Active || tp.ID != ChargingStationTP.StaticID) {
                    continue;
                }
                if (tp is ChargingStationTP charging && charging.OpenUI && charging.sengs >= 1f) {
                    charging.UpdateUI(charging.DrawPos);
                }
            }
        }
    }

    internal class ChargingStationTP : BaseBattery//是的，把这个东西当成是一个电池会更好写
    {
        public override int TargetTileID => ModContent.TileType<ChargingStation>();
        public static int StaticID { get; private set; }
        [VaultLoaden(CWRConstant.UI + "Generator/GeneratorPanel")]
        internal static Asset<Texture2D> Panel { get; private set; }
        [VaultLoaden("@CalamityMod/UI/DraedonsArsenal/ChargerWeaponSlot")]
        internal static Asset<Texture2D> SlotTex { get; private set; }
        [VaultLoaden("@CalamityMod/UI/DraedonsArsenal/ChargeMeterBorder")]
        internal static Asset<Texture2D> BarTop { get; private set; }
        [VaultLoaden("@CalamityMod/UI/DraedonsArsenal/ChargeMeter")]
        internal static Asset<Texture2D> BarFull { get; private set; }
        [VaultLoaden("@CalamityMod/UI/DraedonsArsenal/PowerCellSlot_Empty")]
        internal static Asset<Texture2D> EmptySlot { get; private set; }
        internal bool OpenUI;
        internal float sengs;
        private bool hoverBar;
        private bool hoverChargeBar;
        private bool hoverSlot;
        private bool hoverEmptySlot;
        private bool boverPanel;
        internal Vector2 DrawPos;
        internal static Vector2 MousePos;
        internal Item Item = new Item();
        internal Item Empty = new Item();
        public override bool CanDrop => false;
        public override bool ReceivedEnergy => true;
        public override float MaxUEValue => 1000;
        public override int TargetItem => ModContent.ItemType<ChargingStationItem>();
        public override void SendData(ModPacket data) {
            base.SendData(data);
            ItemIO.Send(Item, data, true);
            ItemIO.Send(Empty, data, true);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            Item = ItemIO.Receive(reader, true);
            Empty = ItemIO.Receive(reader, true);
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            tag["Item"] = Item;
            tag["Empty"] = Empty;
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (!tag.TryGet("Item", out Item)) {
                Item = new Item();
            }
            if (!tag.TryGet("Empty", out Empty)) {
                Empty = new Item();
            }
        }

        public override void SetStaticProperty() => StaticID = ID;

        public void RightEvent() {
            Item item = Main.LocalPlayer.GetItem();

            if (Main.keyState.PressingShift()) {
                if (!Item.IsAir) {
                    Main.LocalPlayer.QuickSpawnItem(new EntitySource_WorldEvent(), Item, Item.stack);
                    Item.TurnToAir();
                }
                SoundEngine.PlaySound(SoundID.Grab);
                return;
            }

            if (ItemIsCharge(item, out _, out _)) {
                if (!Item.IsAir) {
                    Main.LocalPlayer.QuickSpawnItem(new EntitySource_WorldEvent(), Item, Item.stack);
                    Item.TurnToAir();
                }
                Item = item.Clone();
                item.TurnToAir();
                SoundEngine.PlaySound(SoundID.Grab);
            }
            else {
                //SoundEngine.PlaySound(SoundID.MenuClose);
                //CombatText.NewText(HitBox, new Color(111, 247, 200), CWRLocText.Instance.ChargingStation_Text3.Value, false);
                OpenUI = !OpenUI;
                if (OpenUI) {
                    foreach (var tp2 in TileProcessorLoader.TP_InWorld) {
                        if (tp2.ID != ID || tp2.WhoAmI == WhoAmI) {
                            continue;
                        }
                        if (tp2 is ChargingStationTP chargingStation) {
                            chargingStation.OpenUI = false;
                        }
                    }
                }
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
        }

        public override void UpdateMachine() {
            MousePos = Main.MouseWorld;
            DrawPos = CenterInWorld + new Vector2(0, -120) * sengs;
            if (OpenUI) {
                if (sengs < 1f) {
                    sengs += 0.1f;
                }
                else if (Main.LocalPlayer.Distance(CenterInWorld) > 400) {
                    OpenUI = false;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
            else if (sengs > 0f) {
                sengs -= 0.1f;
            }

            if (!Item.IsAir) {
                ChargeWeapon();
            }

            if (!Empty.IsAir) {
                if (MachineData.UEvalue < MaxUEValue - 50) {
                    Empty.stack--;
                    MachineData.UEvalue += 50;
                    MachineData.UEvalue = MathHelper.Clamp(MachineData.UEvalue, 0, MaxUEValue);
                }
            }
        }

        public static bool ItemIsCharge(Item item, out float ueValue, out float maxUEValue) {
            ueValue = 0;
            maxUEValue = 1;//无论如何不要返回0，因为这个数很可能被拿去做除数
            if (item.IsAir || item.type <= ItemID.None) {
                return false;
            }

            if (item.CWR().StorageUE) {
                ueValue = item.CWR().UEValue;
                maxUEValue = item.CWR().MaxUEValue * item.stack;
                return true;
            }

            if (item.Calamity().UsesCharge) {
                ueValue = item.Calamity().Charge;
                maxUEValue = item.Calamity().MaxCharge;
                return true;
            }

            return false;
        }

        internal void UpdateUI(Vector2 drawPos) {
            Rectangle mouseRec = MousePos.GetRectangle(1);
            hoverSlot = (drawPos - SlotTex.Size() / 2).GetRectangle(SlotTex.Size()).Intersects(mouseRec);
            hoverEmptySlot = (drawPos - SlotTex.Size() / 2 + new Vector2(-56, 0)).GetRectangle(EmptySlot.Size()).Intersects(mouseRec);
            boverPanel = (drawPos - Panel.Size() / 2 * 0.75f).GetRectangle(Panel.Size() * 0.75f).Intersects(mouseRec);
            Vector2 barChargePos = drawPos + new Vector2(40, -30);
            drawPos += new Vector2(-30, 30);
            hoverBar = drawPos.GetRectangle(60, 22).Intersects(mouseRec);
            hoverChargeBar = barChargePos.GetRectangle(30, 62).Intersects(mouseRec);

            if (boverPanel) {
                Main.LocalPlayer.mouseInterface = true;
                Main.LocalPlayer.CWR().DontUseItemTime = 2;
            }

            bool justDown = UIHandleLoader.keyLeftPressState == KeyPressState.Pressed;

            if (hoverSlot) {
                if (justDown) {
                    if (ItemIsCharge(Main.mouseItem, out _, out _) || Main.mouseItem.IsAir) {
                        HandlerSlotItem(ref Item);
                        SendData();
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.MenuClose);
                        CombatText.NewText(HitBox, new Color(111, 247, 200), CWRLocText.Instance.ChargingStation_Text3.Value, false);
                    }
                }

                if (!Item.IsAir) {
                    Main.HoverItem = Item.Clone();
                    Main.hoverItemName = Item.Name;
                }
            }

            if (hoverEmptySlot) {
                if (justDown) {
                    if (Main.mouseItem.type == ModContent.ItemType<DraedonPowerCell>() || Main.mouseItem.IsAir) {
                        HandlerSlotItem(ref Empty);
                        SendData();
                    }
                    else {
                        SoundEngine.PlaySound(SoundID.MenuClose);
                        CombatText.NewText(HitBox, new Color(111, 247, 200), CWRLocText.Instance.ChargingStation_Text4.Value, false);
                    }
                }

                if (!Empty.IsAir) {
                    Main.HoverItem = Empty.Clone();
                    Main.hoverItemName = Empty.Name;
                }
            }
        }

        private static void HandlerSlotItem(ref Item setItem) {
            if (setItem.IsAir && Main.mouseItem.IsAir) {
                return;//不要进行空气交互
            }

            SoundEngine.PlaySound(SoundID.Grab);

            if (setItem.type == ItemID.None) {
                setItem = Main.mouseItem.Clone();
                Main.mouseItem.TurnToAir();
            }
            else {
                if (setItem.type == Main.mouseItem.type && setItem.stack < setItem.maxStack) {
                    setItem.stack += Main.mouseItem.stack;
                    Main.mouseItem.TurnToAir();
                }
                else {
                    if (Main.mouseItem.IsAir && Main.keyState.PressingShift()) {//空手Shft能直接放到背包里面
                        Main.LocalPlayer.QuickSpawnItem(new EntitySource_WorldEvent(), setItem, setItem.stack);
                        setItem.TurnToAir();
                        return;
                    }
                    Item swopItem = setItem.Clone();
                    setItem = Main.mouseItem.Clone();
                    Main.mouseItem = swopItem;
                }
            }
        }

        private void ChargeWeapon() {
            if (MachineData.UEvalue < 0.1f) {
                return;
            }
            if (ItemIsCharge(Item, out float ueValue, out float maxValue)) {
                float value = 0;
                ref float setUE = ref value;
                if (Item.CWR().StorageUE) {
                    setUE = ref Item.CWR().UEValue;
                }
                else {
                    setUE = ref Item.Calamity().Charge;
                }

                if (ueValue < maxValue) {
                    setUE += 0.1f;
                    MachineData.UEvalue -= 0.1f;
                    SpawnDust();
                }

                setUE = MathHelper.Clamp(setUE, 0, maxValue);
            }
        }

        private void SpawnDust() {
            int dustID = 182;
            int numDust = 8;
            for (int i = 0; i < numDust; i += 2) {
                float pairSpeed = Main.rand.NextFloat(0.5f, 7f);
                float pairScale = 1f;

                Dust d = Dust.NewDustDirect(CenterInWorld, 0, 0, dustID);
                d.velocity = Vector2.UnitX * pairSpeed;
                d.scale = pairScale;
                d.noGravity = true;

                d = Dust.NewDustDirect(CenterInWorld, 0, 0, dustID);
                d.velocity = Vector2.UnitX * -pairSpeed;
                d.scale = pairScale;
                d.noGravity = true;
            }
        }

        public override void MachineKill() {
            if (VaultUtils.isClient) {
                return;
            }

            int type;
            if (!Item.IsAir) {
                type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, Item);
                Item.TurnToAir();
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
            }

            if (!Empty.IsAir) {
                type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, Empty);
                Empty.TurnToAir();
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
            }

            Item chargingStation = new Item(ModContent.ItemType<ChargingStationItem>());
            chargingStation.CWR().UEValue = MachineData.UEvalue;
            type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, chargingStation);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
            }
        }

        private void DrawChargeBar(SpriteBatch spriteBatch, Vector2 drawPos, float ueRatio) {
            Texture2D electricPower = CWRAsset.ElectricPower.Value;
            Texture2D electricPowerFull = CWRAsset.ElectricPowerFull.Value;
            Texture2D electricPowerGlow = CWRAsset.ElectricPowerGlow.Value;

            float uiRatio = 1 - ueRatio;
            Rectangle full = new Rectangle(0, (int)(electricPowerFull.Height * uiRatio), electricPowerFull.Width, (int)(electricPowerFull.Height * ueRatio));

            drawPos += new Vector2(40, -30) * sengs;
            Vector2 position = drawPos + new Vector2(8, 36 + full.Y) / 2;

            Main.spriteBatch.Draw(electricPower, drawPos, null, Color.White * sengs, 0, Vector2.Zero, 0.5f * sengs, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(electricPowerFull, position, full, Color.White * sengs, 0, Vector2.Zero, 0.5f * sengs, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(electricPowerGlow, drawPos, null, Color.White * sengs, 0, Vector2.Zero, 0.5f * sengs, SpriteEffects.None, 0);
        }

        public void DrawUI(SpriteBatch spriteBatch, Vector2 drawPos, Vector2 MousePos) {
            if (!OpenUI && sengs <= 0) {
                return;
            }

            Vector2 emptyPos = drawPos + new Vector2(-56, 0) * sengs;
            spriteBatch.Draw(Panel.Value, drawPos, null, Color.White, 0, Panel.Size() / 2, 0.75f * sengs, SpriteEffects.None, 0);
            spriteBatch.Draw(SlotTex.Value, drawPos, null, Color.White, 0, SlotTex.Size() / 2, sengs, SpriteEffects.None, 0);
            spriteBatch.Draw(EmptySlot.Value, emptyPos, null, Color.White, 0, SlotTex.Size() / 2, sengs, SpriteEffects.None, 0);

            Vector2 origDrawPos = drawPos;

            drawPos += (new Vector2(-30, 30) * sengs);
            int uiBarByWidthSengs = (int)(BarFull.Value.Width * (MachineData.UEvalue / MaxUEValue));
            // 绘制温度相关的图像
            Rectangle fullRec = new Rectangle(0, 0, uiBarByWidthSengs, BarFull.Value.Height);
            Main.spriteBatch.Draw(BarTop.Value, drawPos, null, Color.White * sengs, 0, Vector2.Zero, sengs, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(BarFull.Value, drawPos + new Vector2(10, 0) * sengs, fullRec, Color.White * sengs, 0, Vector2.Zero, sengs, SpriteEffects.None, 0);

            string textContent;
            Vector2 textSize;
            float textScale;

            if (!Item.IsAir) {
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, origDrawPos, 34, color: Color.White * sengs);
                if (Item.stack > 1) {
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, Item.stack.ToString()
                    , origDrawPos.X - 10, origDrawPos.Y + 12, Color.White, Color.Black, new Vector2(0.3f), 0.6f);
                }

                if (ItemIsCharge(Item, out float ueValue, out float maxValue)) {
                    DrawChargeBar(spriteBatch, origDrawPos, ueValue / maxValue);
                    // 如果鼠标在主页面中，显示信息
                    if (hoverChargeBar) {
                        textContent = (((int)ueValue) + "/" + ((int)maxValue) + "UE").ToString();
                        textScale = 0.8f;
                        textSize = FontAssets.MouseText.Value.MeasureString(textContent) * textScale;
                        Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, textContent
                            , MousePos.X - textSize.X / 2, MousePos.Y + 20, Color.Goldenrod, Color.Black, new Vector2(0.3f), textScale);
                    }
                }
            }
            else {
                DrawChargeBar(spriteBatch, origDrawPos, 0f);
            }

            if (!Empty.IsAir) {
                VaultUtils.SimpleDrawItem(spriteBatch, Empty.type, emptyPos + new Vector2(-1, 1), 34, color: Color.White * sengs);
                if (Empty.stack > 1) {
                    Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, Empty.stack.ToString()
                    , emptyPos.X - 10, emptyPos.Y + 12, Color.White, Color.Black, new Vector2(0.3f), 0.8f);
                }
            }

            if (hoverBar) {
                textContent = (((int)MachineData.UEvalue) + "/" + ((int)MaxUEValue) + "UE").ToString();
                textScale = 0.8f;
                textSize = FontAssets.MouseText.Value.MeasureString(textContent) * textScale;
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, textContent
                    , MousePos.X - textSize.X / 2, MousePos.Y + 20, Color.Goldenrod, Color.Black, new Vector2(0.3f), textScale);
            }

            if (hoverSlot && Item.type == ItemID.None && Main.mouseItem.type == ItemID.None) {
                textContent = CWRLocText.Instance.ChargingStation_Text1.Value;
                textScale = 0.8f;
                textSize = FontAssets.MouseText.Value.MeasureString(textContent) * textScale;
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, textContent
                    , MousePos.X - textSize.X / 2, MousePos.Y + 20, Color.OrangeRed, Color.Black, new Vector2(0.3f), textScale);
            }

            if (hoverEmptySlot && Empty.type == ItemID.None && Main.mouseItem.type == ItemID.None) {
                textContent = CWRLocText.Instance.ChargingStation_Text2.Value;
                textScale = 0.8f;
                textSize = FontAssets.MouseText.Value.MeasureString(textContent) * textScale;
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, textContent
                    , MousePos.X - textSize.X / 2, MousePos.Y + 20, Color.OrangeRed, Color.Black, new Vector2(0.3f), textScale);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Vector2 drawPos = CenterInWorld + new Vector2(0, -24);
            if (Item.type > ItemID.None) {
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, drawPos - Main.screenPosition
                    , 1f, 0, Lighting.GetColor((int)(drawPos.X / 16), (int)(drawPos.Y / 16)));
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawUI(spriteBatch, DrawPos - Main.screenPosition, MousePos - Main.screenPosition);
            DrawChargeBar();
        }
    }
}
