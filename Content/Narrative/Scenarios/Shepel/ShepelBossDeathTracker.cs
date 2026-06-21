using CalamityOverhaul.Content.Narrative.Common;
using Terraria;

namespace CalamityOverhaul.Content.Narrative.Scenarios.Shepel
{
    /// <summary>
    /// 客户端 Boss 死亡时写入 <see cref="ShepelReactiveEvent.BossDefeated"/>，仅 <c>npc.boss</c>
    /// </summary>
    internal class ShepelBossDeathTracker : DeathTrackingNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.boss;

        public override void OnNPCDeath(NPC npc) {
            if (Main.dedServ) return;
            ShepelReactiveEvents.EnqueueBossDefeated(Main.LocalPlayer, npc.type);
        }
    }
}
