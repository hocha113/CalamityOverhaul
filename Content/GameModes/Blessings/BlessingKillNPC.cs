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

        /// <summary>
        /// 锁血死亡演出的真死补登记：演出入场那帧的假死已经烧掉 DeathTrackingNPC 的去重闩，
        /// 多部件判定的祝福（世吞/双子）在假死帧因残部未清不入档，真死帧又被闩挡住，两头落空（反馈 #44）。
        /// 各锁血演出的真死收尾处调用本口补账；<see cref="BlessingWorld.AuthorityRecord"/> 自幂等，重复调用无害
        /// </summary>
        internal static void RecordPerformanceKill(NPC npc) {
            if (npc == null || VaultUtils.isClient || !GameModeSystem.AsuraActive) {
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
