using CalamityMod.Items;
using CalamityMod.Projectiles.Melee.Yoyos;
using CalamityMod.Rarities;
using CalamityMod;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 圣旨
    /// </summary>
    internal class Oracle : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Oracle";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public const int YoyoBaseDamage = 170;
        public const int AuraBaseDamage = 100;
        public const int AuraMaxDamage = 220;

        public override void SetStaticDefaults() {
            ItemID.Sets.Yoyo[Item.type] = true;
            ItemID.Sets.GamepadExtraRange[Item.type] = 15;
            ItemID.Sets.GamepadSmartQuickReach[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.width = 58;
            Item.height = 50;
            Item.DamageType = DamageClass.MeleeNoSpeed;
            Item.damage = YoyoBaseDamage;
            Item.knockBack = 4f;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.autoReuse = true;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.UseSound = SoundID.Item1;
            Item.channel = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;

            Item.shoot = ModContent.ProjectileType<ROracleYoyo>();
            Item.shootSpeed = 16f;

            Item.value = CalamityGlobalItem.Rarity15BuyPrice;
            Item.rare = ModContent.RarityType<Violet>();
            Item.Calamity().donorItem = true;
            
        }
    }
}
