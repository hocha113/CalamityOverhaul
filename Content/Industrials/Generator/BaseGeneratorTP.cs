using InnoVault.TileProcessors;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.Generator
{ 
    internal abstract class BaseGeneratorTP : TileProcessor
    {
        public virtual int MaxFindMode => 300;
        public virtual float MaxUEValue => 1000;
        public MachineData GeneratorData;
        public BaseGeneratorUI GeneratorUI;
        public virtual int TargetItem => ItemID.None;
        public virtual bool CanDrop => true;
        public sealed override void SetProperty() {
            GeneratorData ??= GetGeneratorDataInds();
            if (TrackItem != null && TrackItem.type == TargetItem) {
                GeneratorData.UEvalue = TrackItem.CWR().UEValue;
                if (GeneratorData.UEvalue > MaxUEValue) {
                    GeneratorData.UEvalue = MaxUEValue;
                }
            }
            SetGenerator();
        }

        public virtual void SetGenerator() {

        }

        public virtual MachineData GetGeneratorDataInds() => new MachineData();

        public override void SendData(ModPacket data) {
            GeneratorData?.SendData(data);
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            GeneratorData?.ReceiveData(reader, whoAmI);
        }

        public override void SaveData(TagCompound tag) {
            GeneratorData?.SaveData(tag);
        }

        public override void LoadData(TagCompound tag) {
            GeneratorData?.LoadData(tag);
        }

        public sealed override void Update() {
            if (PreGeneratorUpdate()) {
                GeneratorData?.Update();
                GeneratorUpdate();
            }
        }

        public virtual bool PreGeneratorUpdate() {
            return true;
        }

        public virtual void GeneratorUpdate() {

        }

        public sealed override void OnKill() {
            if (!VaultUtils.isClient && CanDrop) {
                Item item = new Item(TargetItem);
                item.CWR().UEValue = GeneratorData.UEvalue;
                int type = Item.NewItem(new EntitySource_WorldEvent(), HitBox, item);
                if (!VaultUtils.isSinglePlayer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, type, 0f, 0f, 0f, 0, 0, 0);
                }
            }

            GeneratorKill();
            GeneratorUI?.ByTPCloaseFunc();
        }

        public virtual void GeneratorKill() {

        }

        public virtual void RightClickByTile(bool newTP) {

        }
    }
}
