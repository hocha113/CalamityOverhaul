using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class SwordsplosionEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Swordsplosion";
        public override void SetDefaults() {
            Item.SetCalamitySD<Swordsplosion>();
            Item.SetKnifeHeld<SwordsplosionHeld>();
        }
    }

    internal class RSwordsplosion : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<Swordsplosion>();
        public override int ProtogenesisID => ModContent.ItemType<SwordsplosionEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<SwordsplosionHeld>();
    }

    internal class SwordsplosionHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Swordsplosion>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Swordsplosion_Bar";
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
