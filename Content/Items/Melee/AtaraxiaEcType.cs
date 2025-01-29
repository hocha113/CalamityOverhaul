using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class AtaraxiaEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Ataraxia";
        public override void SetDefaults() {
            Item.SetItemCopySD<Ataraxia>();
            Item.damage = 405;
            Item.SetKnifeHeld<AtaraxiaHeld>();
        }
    }

    internal class RAtaraxia : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Ataraxia>();
        public override int ProtogenesisID => ModContent.ItemType<AtaraxiaEcType>();
        public override void SetDefaults(Item item) {
            item.damage = 405;
            item.SetKnifeHeld<AtaraxiaHeld>();
        }
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }
}
