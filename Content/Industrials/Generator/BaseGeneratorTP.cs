namespace CalamityOverhaul.Content.Industrials.Generator
{
    public abstract class BaseGeneratorTP : MachineTP
    {
        public virtual int MaxFindMode => 300;
        public BaseGeneratorUI GeneratorUI;
        public sealed override void SetMachine() {
            SetGenerator();
        }

        public virtual void SetGenerator() {

        }

        public sealed override void UpdateMachine() {
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
            GeneratorUI?.ByTPCloaseFunc();
        }

        public virtual void GeneratorKill() {

        }

        public virtual void RightClickByTile(bool newTP) {

        }
    }
}
