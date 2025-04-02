using CalamityOverhaul.Content.Industrials.Modifys;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials
{
    public abstract class MachineTP : TileProcessor
    {
        public MachineData MachineData { get; set; }
        public virtual float MaxUEValue => 1000;
        public virtual int TargetItem => ItemID.None;
        public virtual bool CanDrop => true;
        public bool Spawn;
        public virtual MachineData GetGeneratorDataInds() => new MachineData();
        public sealed override void SetProperty() {
            MachineData ??= GetGeneratorDataInds();
            if (TrackItem != null && TrackItem.type == TargetItem) {
                MachineData.UEvalue = TrackItem.CWR().UEValue;
                if (MachineData.UEvalue > MaxUEValue) {
                    MachineData.UEvalue = MaxUEValue;
                }
            }
            SetMachine();
        }

        public virtual void SetMachine() {
            
        }

        public sealed override void Update() {
            if (!Spawn && TrackItem != null && TrackItem.type == TargetItem) {
                SendData();//发送一次数据，因为放置时设置的UEValue不会自动同步
                Spawn = true;
            }
            UpdateMachine();
        }

        public virtual void UpdateMachine() {

        }

        public override void SendData(ModPacket data) {
            MachineData?.SendData(data);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            MachineData?.ReceiveData(reader, whoAmI);
        }

        public override void SaveData(TagCompound tag) {
            MachineData?.SaveData(tag);
        }

        public override void LoadData(TagCompound tag) {
            MachineData?.LoadData(tag);
        }

        public void DropItem(int id) => DropItem(new Item(id));

        public void DropItem(Item item) {
            int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, item);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
            }
        }

        public sealed override void OnKill() {
            if (!VaultUtils.isClient && CanDrop && TargetItem > ItemID.None) {
                Item item = new Item(TargetItem);
                item.CWR().UEValue = MachineData.UEvalue;
                DropItem(item);
            }

            MachineKill();
        }

        public virtual void MachineKill() {

        }

        /// <summary>
        /// 不会自动调用，需要在子类中手动调用
        /// </summary>
        public virtual void DrawChargeBar() {
            if (!HoverTP) {
                return;
            }

            Vector2 drawPos = CenterInWorld + new Vector2(0, Height / 2 + 20) - Main.screenPosition;
            int uiBarByWidthSengs = (int)(ChargingStationTP.BarFull.Value.Width * (MachineData.UEvalue / MaxUEValue));
            // 绘制温度相关的图像
            Rectangle fullRec = new Rectangle(0, 0, uiBarByWidthSengs, ChargingStationTP.BarFull.Value.Height);
            Main.spriteBatch.Draw(ChargingStationTP.BarTop.Value, drawPos, null, Color.White, 0, ChargingStationTP.BarTop.Size() / 2, 1, SpriteEffects.None, 0);
            Main.spriteBatch.Draw(ChargingStationTP.BarFull.Value, drawPos + new Vector2(10, 0), fullRec, Color.White, 0, ChargingStationTP.BarTop.Size() / 2, 1, SpriteEffects.None, 0);

            if (Main.keyState.PressingShift()) {
                string textContent = (((int)MachineData.UEvalue) + "/" + ((int)MaxUEValue) + "UE").ToString();
                Vector2 textSize = FontAssets.MouseText.Value.MeasureString(textContent);
                Utils.DrawBorderStringFourWay(Main.spriteBatch, FontAssets.MouseText.Value, textContent
                            , drawPos.X - textSize.X / 2 + 18, drawPos.Y, Color.White, Color.Black, new Vector2(0.3f), 0.6f);
            }
        }
    }
}
