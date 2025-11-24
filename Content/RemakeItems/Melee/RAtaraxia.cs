using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAtaraxia : CWRItemOverride
    {
        public override bool DrawingInfo => false;
        public override void SetDefaults(Item item) => item.SetKnifeHeld<AtaraxiaHeld>();
    }
}
