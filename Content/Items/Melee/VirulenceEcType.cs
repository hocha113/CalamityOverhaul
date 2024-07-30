using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class VirulenceEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Virulence";
        public override void SetDefaults() {
            Item.SetCalamitySD<Virulence>();
            Item.SetKnifeHeld<VirulenceHeld>();
        }
    }
}
