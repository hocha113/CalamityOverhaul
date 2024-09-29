using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 镀金鸟喙
    /// </summary>
    internal class GildedProboscisEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "GildedProboscis";
        public override void SetStaticDefaults() {
            ItemID.Sets.Spears[Type] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 66;
            Item.damage = 315;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.useTurn = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 19;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 19;
            Item.knockBack = 8.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 66;
            Item.value = CalamityGlobalItem.RarityPurpleBuyPrice;
            Item.rare = ItemRarityID.Purple;
            Item.shoot = ModContent.ProjectileType<RGildedProboscisProj>();
            Item.shootSpeed = 13f;
            Item.CWR().IsShootCountCorlUse = true;
        }

        public override bool AltFunctionUse(Player player) => true;
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int proj = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            if (player.altFunctionUse == 2) {
                SoundStyle sound = CommonCalamitySounds.MeatySlashSound;
                SoundEngine.PlaySound(sound with { Volume = 0.35f }, player.Center);
                Main.projectile[proj].ai[1] = 1;
            }
            return false;
        }
    }
}
