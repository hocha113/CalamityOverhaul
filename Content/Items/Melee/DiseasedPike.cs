using CalamityMod.Items;
using CalamityMod.Projectiles.Melee.Spears;
using CalamityMod;
using CalamityOverhaul.Common;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using CalamityMod.Items.Armor.Bloodflare;
using CalamityMod.Sounds;
using Terraria.Audio;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 瘟疫长枪
    /// </summary>
    internal class DiseasedPike : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "DiseasedPike";

        public new string LocalizationCategory => "Items.Weapons.Melee";
        public override void SetStaticDefaults() {
            ItemID.Sets.Spears[Item.type] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 62;
            Item.damage = 65;
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.noMelee = true;
            Item.useTurn = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 20;
            Item.knockBack = 8.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 58;
            Item.value = CalamityGlobalItem.Rarity8BuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.shoot = ModContent.ProjectileType<RDiseasedPikeSpear>();
            Item.shootSpeed = 10f;
            
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int proj = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            if (player.altFunctionUse == 2) {
                SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, player.Center);
                Item.CWR().MeleeCharge = 0;
                Main.projectile[proj].ai[1] = 1;
            }
            return false;
        }
    }
}
