using CalamityOverhaul.Content.Narrative.Common;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Shepel
{
    /// <summary>客户端Boss死亡写 <see cref="ShepelReactiveEvent.BossDefeated"/>，仅npc.boss</summary>
    internal class ShepelBossDeathTracker : DeathTrackingNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.boss;

        public override void OnNPCDeath(NPC npc) {
            if (Main.dedServ) return;
            ShepelReactiveEvents.EnqueueBossDefeated(Main.LocalPlayer, npc.type);
        }
    }
}
