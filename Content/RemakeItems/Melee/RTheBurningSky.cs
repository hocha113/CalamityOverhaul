using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RTheBurningSky : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.width = 74;
            item.height = 74;
            item.useAnimation = 12;
            item.useTime = 12;
            item.damage = 1650;
            item.crit = 16;
            item.knockBack = 7.5f;
            item.UseSound = null;
            item.autoReuse = true;
            item.noUseGraphic = true;
            item.DamageType = DamageClass.Melee;
            item.noMelee = true;
            item.channel = true;
            item.shootSpeed = 10f;
            item.value = Item.sellPrice(gold: 75);
            item.useStyle = ItemUseStyleID.Shoot;
            item.SetKnifeHeld<TheBurningSkyHeld>();
        }
    }
}
