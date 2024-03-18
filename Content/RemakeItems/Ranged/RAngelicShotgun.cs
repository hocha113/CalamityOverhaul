using CalamityMod.Items;
using CalamityMod.Projectiles.Ranged;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityMod;
using System.Collections.Generic;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RAngelicShotgun : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.AngelicShotgun>();
        public override int ProtogenesisID => ModContent.ItemType<AngelicShotgunEcType>();
        public override string TargetToolTipItemName => "AngelicShotgunEcType";
        public override void SetDefaults(Item item) {
            item.damage = 136;
            item.knockBack = 3f;
            item.DamageType = DamageClass.Ranged;
            item.noMelee = true;
            item.autoReuse = true;
            item.width = 86;
            item.height = 38;
            item.useTime = 24;
            item.useAnimation = 24;
            item.UseSound = SoundID.Item38;
            item.useStyle = ItemUseStyleID.Shoot;
            item.value = CalamityGlobalItem.Rarity12BuyPrice;
            item.rare = ModContent.RarityType<Turquoise>();
            item.Calamity().donorItem = true;
            item.shootSpeed = 12;
            item.shoot = ModContent.ProjectileType<IlluminatedBullet>();
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.SetHeldProj<AngelicShotgunHeldProj>();
            item.EasySetLocalTextNameOverride("AngelicShotgun");
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "AngelicShotgun");
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
