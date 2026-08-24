using CalamityOverhaul.Content.GameModes;
using InnoVault.GameSystem;

namespace CalamityOverhaul.Content.NPCs
{
    /// <summary>
    /// BrutalNPCs 的 AI 覆盖基类：残酷模式的世界旗标（<see cref="GameModeSystem.BrutalActive"/>）是唯一门控。
    /// 覆盖器在 NPC 生成时绑定，模式切换只影响此后生成的个体。
    /// 子类可用 <see cref="CanBrutalOverride"/> 越过该门（返回非 null 时以其为准）
    /// </summary>
    internal abstract class BrutalNPCOverride : NPCOverride
    {
        public sealed override bool CanOverride() {
            bool? result = CanBrutalOverride();
            if (result.HasValue) {
                return result.Value;
            }
            if (!GameModeSystem.BrutalActive) {
                return false;
            }
            return true;
        }

        public virtual bool? CanBrutalOverride() {
            return null;
        }
    }
}
