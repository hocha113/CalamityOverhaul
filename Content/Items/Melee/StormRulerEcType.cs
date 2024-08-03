using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class StormRulerEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "StormRuler";
        public override void SetDefaults() {
            Item.SetCalamitySD<StormRuler>();
            Item.SetKnifeHeld<StormRulerHeld>();
        }
    }

    internal class RStormRuler : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<StormRuler>();
        public override int ProtogenesisID => ModContent.ItemType<StormRulerEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<StormRulerHeld>();
    }

    internal class StormRulerHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<StormRuler>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "AbsoluteZero_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 6;
            Length = 82;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5;
            ShootSpeed = 11;
        }

        public override void Shoot() {

        }

        public override bool PreInOwnerUpdate() {
            return base.PreInOwnerUpdate();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {

        }
    }
}
