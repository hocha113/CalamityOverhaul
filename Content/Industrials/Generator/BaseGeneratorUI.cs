using InnoVault.UIHandles;

namespace CalamityOverhaul.Content.Industrials.Generator
{
    public abstract class BaseGeneratorUI : UIHandle
    {
        internal bool IsActive;
        public override bool Active => IsActive;
        internal BaseGeneratorTP GeneratorTP;
        public sealed override void Update() => UpdateElement();

        public virtual void RightClickByTile(bool newTP) {

        }

        public virtual void ByTPCloaseFunc() {

        }

        public virtual void UpdateElement() {

        }
    }
}
