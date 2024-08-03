using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class VoidEdgeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "VoidEdge";
        public override void SetDefaults() {
            Item.SetCalamitySD<VoidEdge>();
            Item.SetKnifeHeld<VoidEdgeHeld>();
        }
    }

    internal class RVoidEdge : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<VoidEdge>();
        public override int ProtogenesisID => ModContent.ItemType<VoidEdgeEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<VoidEdgeHeld>();
    }

    internal class VoidEdgeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<VoidEdge>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "VoidEdge_Bar";
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
