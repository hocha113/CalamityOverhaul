using CalamityOverhaul.Content.Narrative.Common;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.Blessings
{
    /// <summary>
    /// 祝福讨伐检测：修罗（含死神永生态）开启状态下 Boss 之死由权威端入档。
    /// 死亡回调已按 realLife 归并到头节点；客户端不记档，等 <see cref="BlessingUnlockNet"/> 回执
    /// </summary>
    internal class BlessingKillNPC : DeathTrackingNPC
    {
        public override void OnNPCDeath(NPC npc) {
            if (VaultUtils.isClient) {
                return;
            }
            if (!GameModeSystem.AsuraActive) {
                return;
            }
            Blessing blessing = BlessingRegistry.FindByAnchor(npc.type);
            if (blessing == null || !blessing.IsBossFullyDown(npc)) {
                return;
            }
            BlessingWorld.AuthorityRecord(blessing);
        }
    }
}
