using CalamityMod.Rarities;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    /// <summary>
    /// 万魔殿
    /// </summary>
    internal class Pandemonium : ModItem
    {
        public override string Texture => CWRConstant.Item_Magic + "Pandemonium";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 320;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 25;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.noMelee = true;
            Item.knockBack = 5;
            Item.value = Item.sellPrice(platinum: 10);
            Item.rare = ModContent.RarityType<Violet>();
            Item.UseSound = SoundID.Item113;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PandemoniumChannel>();
            Item.shootSpeed = 10f;
            Item.channel = true;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_Pandemonium;
        }

        private bool oldQ;
        public override void HoldItem(Player player) {
            bool isQ = Keyboard.GetState().IsKeyDown(Keys.Q);
            if (isQ && !oldQ && player.CountProjectilesOfID<PandemoniumQSkill>() == 0) {
                ShootState shootState = player.GetShootState();
                Projectile.NewProjectile(shootState.Source, player.Center
                    , Vector2.Zero, ModContent.ProjectileType<PandemoniumQSkill>()
                    , shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI);
            }
            oldQ = isQ;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                //右键：鼠标法阵
                Item.mana = 40;
                Item.useTime = Item.useAnimation = 35;
                Item.channel = false;
                Item.shoot = ModContent.ProjectileType<PandemoniumCircle>();
                return player.ownedProjectileCounts[ModContent.ProjectileType<PandemoniumCircle>()] < 13; //最多13个法阵
            }
            else {
                //左键：原本的引导法阵
                Item.mana = 25;
                Item.useTime = Item.useAnimation = 20;
                Item.channel = true;
                Item.shoot = ModContent.ProjectileType<PandemoniumChannel>();
                return player.ownedProjectileCounts[ModContent.ProjectileType<PandemoniumChannel>()] == 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                //在鼠标位置生成法阵
                Vector2 targetPos = Main.MouseWorld;
                Projectile.NewProjectile(
                    source,
                    targetPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<PandemoniumCircle>(),
                    (int)(damage * 0.8f), //右键伤害为左键的80%
                    knockback,
                    player.whoAmI
                );
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }
    }
}
