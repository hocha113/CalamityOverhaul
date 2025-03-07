using InnoVault.TileProcessors;
using System.IO;
using Terraria.ID;
using Terraria.ModLoader.IO;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria;

namespace CalamityOverhaul.Content.Industrials
{
    public abstract class MachineTP : TileProcessor
    {
        public MachineData MachineData { get; set; }
        public virtual float MaxUEValue => 1000;
        public virtual int TargetItem => ItemID.None;
        public virtual bool CanDrop => true;
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
    }
}
