using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>星云烈焰重铸：命中积「烈焰」，满层右键在光标目标上引爆「新星引爆」灾变</summary>
    internal class GsNebulaBlaze : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.NebulaBlaze;

        protected override string GsDescFallback =>
            "Reforged: hits build Blaze; at full charge, right click to mark a nova on the target near your cursor\n" +
            "It detonates in three staggered blast rings around a gravity well";

        public override int ChargePerHit => 4;

        public override int CataclysmManaCost => 55;

        protected override float PassiveDamageBonus => 0.10f;

        protected override int DirectorType => ModContent.ProjectileType<GsNovaDetonationDirector>();

        protected override Color AccentColor => new(255, 120, 210);

        protected override SoundStyle TriggerSound => SoundID.Item103;

        protected override void ModifyTriggerParams(Item item, Player player, ref Vector2 anchor, ref float ai1, ref float ai2) {
            //光标 300px 内最近可追踪敌作为新星锚点，无则锚定触发点
            ai1 = -1f;
            float best = 300f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || !npc.CanBeChasedBy() || npc.type == NPCID.TargetDummy) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, anchor);
                if (dist < best) {
                    best = dist;
                    ai1 = i;
                }
            }
        }
    }
}
