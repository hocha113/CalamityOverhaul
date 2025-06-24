using CalamityOverhaul.Common;
using InnoVault.GameSystem;

namespace CalamityOverhaul.Content.NPCs
{
    internal abstract class CWRNPCOverride : NPCOverride
    {
        public override bool CanOverride() {
            bool? result = CanCWROverride();
            if (result.HasValue) {
                return result.Value;
            }
            return CWRServerConfig.Instance.BiologyOverhaul;
        }

        public virtual bool? CanCWROverride() {
            return null;
        }
    }
}
