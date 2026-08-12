using CalamityOverhaul.Content.Industrials.MachineModules;
using System.IO;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Industrials.Generator
{
    public abstract class BaseGeneratorTP : MachineTP
    {
        public virtual int MaxFindMode => 300;
        public BaseGeneratorUI GeneratorUI;

        #region 模块架
        /// <summary>本机作为模块宿主的种类;None 表示不开放槽位</summary>
        public virtual MachineModuleTarget ModuleHostKind => MachineModuleTarget.None;
        /// <summary>模块槽数,0 表示无槽(荒野敌对结构等)</summary>
        public virtual int ModuleSlotCount => 0;

        private MachineModuleRack moduleRack;
        public MachineModuleRack ModuleRack => moduleRack ??= new MachineModuleRack(ModuleHostKind);
        #endregion

        public sealed override void SetMachine() {
            SetGenerator();
        }

        public virtual void SetGenerator() {

        }

        public sealed override void UpdateMachine() {
            if (ModuleSlotCount > 0) {
                ModuleRack.EnsureSlots(ModuleSlotCount);
                ModuleRack.Refresh();
            }

            if (PreGeneratorUpdate()) {
                MachineData?.Update();
                GeneratorUpdate();
            }

            if (MachineData != null) {
                MachineData.UEvalue = MathHelper.Clamp(MachineData.UEvalue, 0, MaxUEValue);
            }
        }

        public virtual bool PreGeneratorUpdate() {
            return true;
        }

        public virtual void GeneratorUpdate() {

        }

        public override void MachineKill() {
            GeneratorKill();
            //模块随拆机倒出(权威端)
            if (ModuleSlotCount > 0 && !VaultUtils.isClient) {
                ModuleRack.DropAll(item => DropItem(item));
            }
            GeneratorUI?.ByTPCloaseFunc();
        }

        public virtual void GeneratorKill() {

        }

        public virtual void RightClickByTile(bool newTP) {

        }

        #region 存档与同步:模块架一律追加在既有字段之后,槽数按类型固定故两端对称
        public override void SendData(ModPacket data) {
            base.SendData(data);
            if (ModuleSlotCount > 0) {
                ModuleRack.Send(data, ModuleSlotCount);
            }
        }

        public override void ReceiveData(BinaryReader reader, int whoAmI) {
            base.ReceiveData(reader, whoAmI);
            if (ModuleSlotCount > 0) {
                ModuleRack.Receive(reader, ModuleSlotCount);
            }
        }

        public override void SaveData(TagCompound tag) {
            base.SaveData(tag);
            if (ModuleSlotCount > 0) {
                ModuleRack.Save(tag, ModuleSlotCount);
            }
        }

        public override void LoadData(TagCompound tag) {
            base.LoadData(tag);
            if (ModuleSlotCount > 0) {
                ModuleRack.Load(tag, ModuleSlotCount, GetType().Name);
            }
        }
        #endregion
    }
}
