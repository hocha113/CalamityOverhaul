using CalamityOverhaul.Common;
using InnoVault.GameSystem;

namespace CalamityOverhaul.Content.NPCs
{
    internal abstract class CWRNPCOverride : NPCOverride
    {
        public sealed override bool CanOverride() {
            bool? result = CanCWROverride();
            if (result.HasValue) {
                return result.Value;
            }
            if (!CWRServerConfig.Instance.BiologyOverhaul) {
                return false;
            }
            return true;
        }

        public virtual bool? CanCWROverride() {
            return null;
        }
    }
}
