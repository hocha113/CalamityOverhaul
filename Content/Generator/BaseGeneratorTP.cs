using InnoVault.TileProcessors;
using System;
using System.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Generator
{ 
    internal abstract class BaseGeneratorTP : TileProcessor
    {
        internal virtual int maxFindMode => 300;
        internal GeneratorData GeneratorData;
        internal BaseGeneratorUI GeneratorUI;
        public sealed override void SetProperty() {
            GeneratorData ??= GetGeneratorDataInds();
            SetGenerator();
        }

        public virtual void SetGenerator() {

        }

        public virtual GeneratorData GetGeneratorDataInds() => new GeneratorData();

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
            GeneratorKill();
            GeneratorUI.ByTPCloaseFunc();
        }

        public virtual void GeneratorKill() {

        }
    }
}
