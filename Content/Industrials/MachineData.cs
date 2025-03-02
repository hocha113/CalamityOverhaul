using System.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials
{
    public class MachineData
    {
        internal float UEvalue;
        internal string Name => GetType().Name;
        public virtual void SendData(ModPacket data) {
            data.Write(UEvalue);
        }

        public virtual void ReceiveData(BinaryReader reader, int whoAmI) {
            UEvalue = reader.ReadSingle();
        }

        public virtual void SaveData(TagCompound tag) {
            tag[$"{Name}_UEvalue"] = UEvalue;
        }

        public virtual void LoadData(TagCompound tag) {
            if (!tag.TryGet($"{Name}_UEvalue", out UEvalue)) {
                UEvalue = 0;
            }
        }

        public virtual void Update() {

        }
    }
}
