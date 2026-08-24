using CalamityOverhaul.Content.GameModes;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs
{
    /// <summary>
    /// BrutalNPCs 的 AI 覆盖基类：残酷模式世界旗标（<see cref="GameModeSystem.BrutalActive"/>）是默认门控。
    /// <see cref="DisabledReworkTypes"/> 里的类型恒不接管（重制未完成），残酷模式下走原版 AI + <c>GameModeNPC</c> 通用增强。
    /// 覆盖器在 NPC 生成时绑定，模式切换只影响此后生成的个体。
    /// 子类可用 <see cref="CanBrutalOverride"/> 越过旗标门（返回非 null 时以其为准；拒绝名单仍优先）
    /// </summary>
    internal abstract class BrutalNPCOverride : NPCOverride
    {
        /// <summary>
        /// 重制未完成、默认不接管的 NPC 类型。加 ID 即禁用，从集合移除即重新启用。
        /// </summary>
        internal static readonly HashSet<int> DisabledReworkTypes = [
            NPCID.CultistBoss,
            NPCID.CultistBossClone,
            NPCID.HallowBoss,
        ];

        public sealed override bool CanOverride() {
            if (DisabledReworkTypes.Contains(TargetID)) {
                return false;
            }
            bool? result = CanBrutalOverride();
            if (result.HasValue) {
                return result.Value;
            }
            return GameModeSystem.BrutalActive;
        }

        public virtual bool? CanBrutalOverride() {
            return null;
        }
    }
}
