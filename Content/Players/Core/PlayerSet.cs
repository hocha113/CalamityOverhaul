using Terraria;

namespace CalamityOverhaul.Content.Players.Core
{
    internal abstract class PlayerSet
    {
        public virtual void Load() {

        }

        public virtual bool On_ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers) {
            return true;
        }

        public virtual bool On_ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            return true;
        }

        public virtual bool? CanSwitchWeapon(Player player) {
            return null;
        }
    }
}
