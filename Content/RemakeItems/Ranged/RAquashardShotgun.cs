using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAquashardShotgun : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.AquashardShotgun>();
        public override int ProtogenesisID => ModContent.ItemType<AquashardShotgun>();
        public override void SetDefaults(Item item) {
            item.damage = 9;
            item.DamageType = DamageClass.Ranged;
            item.width = 62;
            item.height = 26;
            item.useTime = 30;
            item.useAnimation = 30;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 5.5f;
            item.value = CalamityGlobalItem.Rarity3BuyPrice;
            item.rare = ItemRarityID.Orange;
            item.UseSound = SoundID.Item61;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<Aquashard>();
            item.shootSpeed = 22f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<AquashardShotgunHeldProj>();
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "AquashardShotgun");
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
