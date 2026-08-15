using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets
{
    /// <summary>
    /// 重启持有的 NPC：AI 不走、速度钳零；
    /// 位置由 <see cref="KikasaReset"/> 在 PostUpdateEverything 统一按历史写回
    /// </summary>
    internal sealed class KikasaResetGlobalNPC : GlobalNPC
    {
        public override bool PreAI(NPC npc) {
            if (!KikasaReset.IsNpcHeld(npc.whoAmI)) {
                return true;
            }
            npc.velocity = Vector2.Zero;
            return false;
        }
    }
}
