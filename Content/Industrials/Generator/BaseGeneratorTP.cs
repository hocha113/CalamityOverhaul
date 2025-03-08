using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.Industrials.Generator
{
    internal abstract class BaseGeneratorTP : MachineTP
    {
        public virtual int MaxFindMode => 300;
        public BaseGeneratorUI GeneratorUI;
        public sealed override void SetMachine() {
            MachineData ??= GetGeneratorDataInds();
            if (TrackItem != null && TrackItem.type == TargetItem) {
                MachineData.UEvalue = TrackItem.CWR().UEValue;
                if (MachineData.UEvalue > MaxUEValue) {
                    MachineData.UEvalue = MaxUEValue;
                }
            }
            SetGenerator();
        }

        public virtual void SetGenerator() {

        }

        public sealed override void Update() {
            if (PreGeneratorUpdate()) {
                MachineData?.Update();
                GeneratorUpdate();
            }
        }

        public virtual bool PreGeneratorUpdate() {
            return true;
        }

        public virtual void GeneratorUpdate() {

        }

        public override void MachineKill() {
            GeneratorKill();
            GeneratorUI?.ByTPCloaseFunc();
        }

        public virtual void GeneratorKill() {

        }

        public virtual void RightClickByTile(bool newTP) {

        }
    }
}
