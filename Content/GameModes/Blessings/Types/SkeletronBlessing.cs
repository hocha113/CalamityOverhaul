using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>骷髅王·亡骨守望：亡骨定期格挡，将一次受击减半</summary>
    internal sealed class SkeletronBlessing : Blessing
    {
        public override int ProgressOrder => 60;

        public override int[] AnchorNPCTypes => [NPCID.SkeletronHead];

        public override string SigilPath =>
            "M32,56 Q30,26 50,24 Q70,26 68,56 L62,74 L38,74 Z M40,44 L46,50 M60,44 L54,50 M44,64 L56,64";

        //槽位：0=格挡冷却余时
        public override int StateSlots => 1;

        public override void PostUpdate(BlessingPlayer bp) {
            float[] state = bp.StateOf(this);
            if (state[0] > 0f) {
                state[0]--;
            }
        }

        public override void ModifyHurt(BlessingPlayer bp, ref Player.HurtModifiers modifiers) {
            float[] state = bp.StateOf(this);
            if (state[0] > 0f) {
                return;
            }
            state[0] = BlessingTuning.SkeletronGuardCooldown;
            modifiers.FinalDamage *= BlessingTuning.SkeletronGuardMult;
            if (!Main.dedServ) {
                CombatText.NewText(bp.Player.getRect(), new Color(206, 206, 188), DisplayName.Value);
            }
        }
    }
}
