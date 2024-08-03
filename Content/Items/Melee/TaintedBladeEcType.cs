using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class TaintedBladeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TaintedBlade";
        public override void SetDefaults() {
            Item.SetCalamitySD<TaintedBlade>();
            Item.SetKnifeHeld<TaintedBladeHeld>();
        }
    }

    internal class RTaintedBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<TaintedBlade>();
        public override int ProtogenesisID => ModContent.ItemType<TaintedBladeEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<TaintedBladeHeld>();
    }

    internal class TaintedBladeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<TaintedBlade>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "BloodRed_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 40;
            canDrawSlashTrail = true;
            SwingData.starArg = 74;
            SwingData.baseSwingSpeed = 5f;
            drawTrailBtommWidth = 30;
            distanceToOwner = 14;
            drawTrailTopWidth = 20;
            Length = 50;
        }

        public override void PostInOwnerUpdare() {

        }

        public override void Shoot() {

        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {

        }
    }
}
