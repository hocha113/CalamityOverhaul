using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.Blessings.Types
{
    /// <summary>拜月教邪教徒·月相护盾：月相定期凝成护盾，完全挡下一发弹幕</summary>
    internal sealed class LunaticCultistBlessing : Blessing
    {
        public override int ProgressOrder => 170;

        public override int[] AnchorNPCTypes => [NPCID.CultistBoss];

        public override string SigilPath =>
            "M60,20 Q32,30 34,52 Q36,76 60,82 Q44,68 44,50 Q44,32 60,20 Z";

        //槽位：0=护盾充能余时（0 即就绪）
        public override int StateSlots => 1;

        public override void PostUpdate(BlessingPlayer bp) {
            float[] state = bp.StateOf(this);
            if (state[0] > 0f) {
                state[0]--;
            }
        }

        public override bool FreeDodge(BlessingPlayer bp, in Player.HurtInfo info) {
            float[] state = bp.StateOf(this);
            if (state[0] > 0f) {
                return false;
            }
            //只挡弹幕：接触与环境伤不吃盾
            if (info.DamageSource is not PlayerDeathReason reason
                || !reason.TryGetCausingEntity(out Entity entity) || entity is not Projectile) {
                return false;
            }
            state[0] = BlessingTuning.CultistShieldCooldown;
            if (!Main.dedServ) {
                CombatText.NewText(bp.Player.getRect(), new Color(140, 196, 232), DisplayName.Value);
            }
            return true;
        }
    }
}
