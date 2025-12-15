using CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class DawnshatterAzure : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";

        private int comboCounter;
        private int comboResetTimer;

        public override void SetStaticDefaults() {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 4));
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.height = Item.width = 54;
            Item.damage =11200;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useTime = Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(6, 23, 75, 0);
            Item.rare = CWRID.Rarity_DarkOrange;
            Item.shoot = ModContent.ProjectileType<DawnshatterSpearThrust>();
            Item.shootSpeed = 1f;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_DawnshatterAzure;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 20;

        public override void HoldItem(Player player) {
            //连击计时器递减
            if (comboResetTimer > 0) {
                comboResetTimer--;
                if (comboResetTimer <= 0) {
                    comboCounter = 0;
                }
            }
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<DawnshatterSpearThrust>()] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<DawnshatterChargeDash>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                //右键蓄力突进
                SoundEngine.PlaySound("CalamityMod/Sounds/Custom/Yharon/YharonRoarShort".GetSound() with { Volume = 0.5f, Pitch = -0.1f }, player.Center);
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<DawnshatterChargeDash>()
                    , (int)(damage * 2f), knockback * 1.5f, player.whoAmI);

                //使用大招后重置连击
                comboCounter = 0;
                comboResetTimer = 0;
                return false;
            }

            //左键普通刺击，传递连击阶段
            float thrustPitch = 0.1f + (comboCounter % 3) * 0.15f;
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = thrustPitch }, player.Center);

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, comboCounter);

            //增加连击计数并重置计时器
            comboCounter++;
            comboResetTimer = 90;

            return false;
        }
    }
}
