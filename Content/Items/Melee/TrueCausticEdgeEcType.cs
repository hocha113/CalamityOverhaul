using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class TrueCausticEdgeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TrueCausticEdge";
        public override void SetDefaults() {
            Item.SetCalamitySD<TrueCausticEdge>();
            Item.SetKnifeHeld<TrueCausticEdgeHeld>();
        }
    }

    internal class RTrueCausticEdge : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<TrueCausticEdge>();
        public override int ProtogenesisID => ModContent.ItemType<TrueCausticEdgeEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<TrueCausticEdgeHeld>();
    }

    internal class TrueCausticEdgeHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<TrueCausticEdge>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "BloodRed_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 46;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = 20;
            drawTrailBtommWidth = 50;
            drawTrailTopWidth = 36;
            Length = 70;
            unitOffsetDrawZkMode = -8;
            overOffsetCachesRoting = MathHelper.ToRadians(8);
            SwingData.starArg = 80;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 80;
            SwingData.maxClampLength = 90;
            SwingData.ler1_UpSizeSengs = 0.056f;
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
