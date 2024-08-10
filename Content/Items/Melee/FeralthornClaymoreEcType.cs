using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class FeralthornClaymoreEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "FeralthornClaymore";
        public override void SetDefaults() {
            Item.SetCalamitySD<FeralthornClaymore>();
            Item.SetKnifeHeld<FeralthornClaymoreHeld>();
        }
    }

    internal class RFeralthornClaymore : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<FeralthornClaymore>();
        public override int ProtogenesisID => ModContent.ItemType<FeralthornClaymoreEcType>();
        public override void SetDefaults(Item item) => item.SetKnifeHeld<FeralthornClaymoreHeld>();
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    internal class FeralthornClaymoreHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<FeralthornClaymore>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "FeralthornClaymore_Bar";
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 64;
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 4f;
            distanceToOwner = 28;
            drawTrailTopWidth = 30;
            Length = 80;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitNPC(target, hit, damageDone);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            base.OnHitPlayer(target, info);
        }
    }
}
