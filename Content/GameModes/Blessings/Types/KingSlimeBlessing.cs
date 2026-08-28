using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>史莱姆王·御胶之冕：凝胶裹身，接触伤害承伤降低</summary>
    internal sealed class KingSlimeBlessing : Blessing
    {
        public override int ProgressOrder => 10;

        public override int[] AnchorNPCTypes => [NPCID.KingSlime];

        public override string SigilPath =>
            "M20,64 L20,38 L35,50 L50,28 L65,50 L80,38 L80,64 Z M42,72 Q50,86 58,72";

        public override void ModifyHitByNPC(BlessingPlayer bp, NPC npc, ref Player.HurtModifiers modifiers)
            => modifiers.FinalDamage *= BlessingTuning.KingSlimeContactMult;
    }
}
