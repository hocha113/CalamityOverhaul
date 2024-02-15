using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Magic
{
    internal class RFatesReveal : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Magic.FatesReveal>();
        public override int ProtogenesisID => ModContent.ItemType<FatesReveal>();
        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetDefaults(Item item) {
            item.damage = 56;
            item.DamageType = DamageClass.Magic;
            item.mana = 20;
            item.width = 80;
            item.height = 86;
            item.useTime = 16;
            item.useAnimation = 16;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.noUseGraphic = true;
            item.knockBack = 5.5f;
            item.UseSound = SoundID.Item20;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<FatesRevealHeldProj>();
            item.shootSpeed = 1f;
            item.value = CalamityGlobalItem.Rarity13BuyPrice;
            item.rare = ModContent.RarityType<PureGreen>();
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "FatesReveal");
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<FatesRevealHeldProj>()] <= 0)
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<FatesRevealHeldProj>(), damage, knockback, player.whoAmI);
            return false;
        }
    }
}
