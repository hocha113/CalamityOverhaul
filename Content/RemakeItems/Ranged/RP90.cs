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
    internal class RP90 : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<P90>();
        public override int ProtogenesisID => ModContent.ItemType<P90EcType>();
        public override void SetDefaults(Item item) {
            item.damage = 5;
            item.DamageType = DamageClass.Ranged;
            item.width = 60;
            item.height = 28;
            item.useTime = item.useAnimation = 2;
            item.useStyle = ItemUseStyleID.Shoot;
            item.noMelee = true;
            item.knockBack = 1.5f;
            item.value = CalamityGlobalItem.RarityLightRedBuyPrice;
            item.rare = ItemRarityID.LightRed;
            item.UseSound = SoundID.Item11 with { Volume = 0.6f };
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<P90Round>();
            item.shootSpeed = 9f;
            item.useAmmo = AmmoID.Bullet;
            item.Calamity().canFirePointBlankShots = true;
            item.CWR().HasCartridgeHolder = true;
            item.CWR().AmmoCapacity = 380;
            item.SetHeldProj<P90HeldProj>();
        }

        public override bool? On_Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return false;
        }
        public override bool? On_CanConsumeAmmo(Item weapon, Item ammo, Player player) => Main.rand.NextFloat() > 0.35f;
    }
}
