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
            //安装了Calamity时限制仅在复仇/死亡/Boss急速模式下启用，防止与其他mod的AI覆盖冲突
            if (CWRRef.Has) {
                return CWRRef.GetRevengeMode() || CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            }
            return true;
        }

        public virtual bool? CanCWROverride() {
            return null;
        }
    }
}
