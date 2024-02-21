using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityMod;
using System.Collections.Generic;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RHellborn : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.Hellborn>();
        public override int ProtogenesisID => ModContent.ItemType<Hellborn>();
        public override void SetDefaults(Item item) {
            item.damage = 20;
            item.DamageType = DamageClass.Ranged;
            item.width = 62;
            item.height = 34;
            item.useAnimation = item.useTime = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.knockBack = 2f;
            item.value = CalamityGlobalItem.Rarity5BuyPrice;
            item.rare = ItemRarityID.Pink;
            item.UseSound = SoundID.Item11;
            item.autoReuse = true;
            item.noMelee = true;
            item.shoot = ProjectileID.PurificationPowder;
            item.shootSpeed = 12f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<HellbornHeldProj>();
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "Hellborn");
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
