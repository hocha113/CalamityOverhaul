using CalamityMod;
using CalamityMod.Items.DraedonMisc;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Placeables.DraedonStructures;
using CalamityMod.Tiles.DraedonStructures;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.RemakeItems;
using InnoVault.GameSystem;
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

    internal class ChargingStationTP : BaseBattery
    {
        public override int TargetTileID => ModContent.TileType<ChargingStation>();
        public static int StaticID { get; private set; }

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
            Item ??= new();
            tag["_Item"] = ItemIO.Save(Item);
            Empty ??= new();
            tag["_Empty"] = ItemIO.Save(Empty);
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            try {
                if (!tag.TryGet("Item", out Item)) {
                    Item = new Item();
                }
                if (!tag.TryGet("Empty", out Empty)) {
                    Empty = new Item();
                }
            } catch { }

            if (tag.TryGet<TagCompound>("_Item", out var itemTag)) {
                Item = ItemIO.Load(itemTag);
            }
            else {
                Item = new Item();
            }

            if (tag.TryGet<TagCompound>("_Empty", out var emptyTag)) {
                Empty = ItemIO.Load(emptyTag);
            }
            else {
                Empty = new Item();
            }
        }

        public override void SetStaticDefaults() => StaticID = ID;

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
                ChargingStationUI.Instance.Initialize(this);
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.3f, Pitch = -0.3f });
            }
        }

        public override void UpdateMachine() {
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
            maxUEValue = 1;
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
            int dustID = 226; //电流粒子
            int numDust = 6;
            for (int i = 0; i < numDust; i += 2) {
                float pairSpeed = Main.rand.NextFloat(0.5f, 5f);
                float pairScale = 0.9f;

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

        public override void Draw(SpriteBatch spriteBatch) {
            Vector2 drawPos = CenterInWorld + new Vector2(0, -24);
            if (Item.type > ItemID.None) {
                VaultUtils.SimpleDrawItem(spriteBatch, Item.type, drawPos - Main.screenPosition,
                    1f, 0, Lighting.GetColor((int)(drawPos.X / 16), (int)(drawPos.Y / 16)));
            }
        }

        public override void FrontDraw(SpriteBatch spriteBatch) {
            DrawChargeBar();
        }
    }
}
