using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RPearlGod : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<PearlGod>();
        public override int ProtogenesisID => ModContent.ItemType<PearlGodEcType>();
        public override void SetDefaults(Item item) {
            item.damage = 150;
            item.DamageType = DamageClass.Ranged;
            item.width = 80;
            item.height = 46;
            item.useTime = 20;
            item.useAnimation = 20;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 3f;
            item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.UseSound = SoundID.Item41;
            item.autoReuse = true;
            item.shootSpeed = 12f;
            item.shoot = ModContent.ProjectileType<ShockblastRound>();
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 12;
            item.SetHeldProj<PearlGodHeldProj>();
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
    }
}
