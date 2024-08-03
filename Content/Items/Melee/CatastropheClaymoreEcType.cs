using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class CatastropheClaymoreEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "CatastropheClaymore";
        public override void SetDefaults() {
            Item.SetCalamitySD<CatastropheClaymore>();
            Item.SetKnifeHeld<CatastropheClaymoreHeld>();
        }
    }

    internal class RCatastropheClaymore : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CatastropheClaymore>();
        public override int ProtogenesisID => ModContent.ItemType<CatastropheClaymoreEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<CatastropheClaymoreHeld>();
    }

    internal class CatastropheClaymoreHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<CatastropheClaymore>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "CatastropheClaymore_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 60;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
            Length = 56;
        }

        public override void Shoot() {

        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {

        }
    }
}
