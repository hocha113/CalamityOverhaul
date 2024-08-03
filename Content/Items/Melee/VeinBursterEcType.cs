using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class VeinBursterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "VeinBurster";
        public override void SetDefaults() {
            Item.SetCalamitySD<VeinBurster>();
            Item.SetKnifeHeld<VeinBursterHeld>();
        }
    }

    internal class RVeinBurster : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<VeinBurster>();
        public override int ProtogenesisID => ModContent.ItemType<VeinBursterEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<VoidEdgeHeld>();
    }

    internal class VeinBursterHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<VeinBurster>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "BloodRed_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 36;
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5f;
            drawTrailBtommWidth = 20;
            distanceToOwner = 8;
            drawTrailTopWidth = 18;
            Length = 40;
        }

        public override void Shoot() {

        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {

        }
    }
}
