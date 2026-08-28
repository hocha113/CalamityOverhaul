using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>毁灭者·探针回路：击杀回魔，探针偶尔替你上膛</summary>
    internal sealed class DestroyerBlessing : Blessing
    {
        public override int ProgressOrder => 110;

        public override int[] AnchorNPCTypes => [NPCID.TheDestroyer];

        public override string SigilPath =>
            "M20,72 L40,72 L40,46 L60,46 L60,72 L80,72 M50,46 L50,32 M50,32 L44,22 L56,22 Z";

        public override void OnHitNPC(BlessingPlayer bp, NPC target, in NPC.HitInfo hit, int damageDone) {
            if (target.life > 0) {
                return;
            }
            Player player = bp.Player;
            int restore = Math.Min(BlessingTuning.DestroyerManaOnKill, player.statManaMax2 - player.statMana);
            if (restore > 0) {
                player.statMana += restore;
                player.ManaEffect(restore);
            }
        }

        public override bool CanConsumeAmmo(BlessingPlayer bp, Item weapon, Item ammo)
            => !Main.rand.NextBool(BlessingTuning.DestroyerAmmoSaveDenominator);
    }
}
