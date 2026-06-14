using CalamityOverhaul.Content.Projectiles.Weapons.Magic.NeutronWandProjs;
using CalamityOverhaul.Content.RangedModify.Core;
using CalamityOverhaul.Content.UIs.SupertableUIs;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class NeutronWand : ModItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Item_Magic + "NeutronWand";
        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 12));
        }
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 355;
            Item.DamageType = DamageClass.Magic;
            Item.useTime = Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(15, 3, 5, 0);
            Item.rare = ItemRarityID.Red;
            Item.shoot = ModContent.ProjectileType<NeutronMagchStar>();
            Item.shootSpeed = 15;
            Item.mana = 15;
            Item.crit = 6;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.CWR().OmigaSnyContent = SupertableRecipeData.FullItems_NeutronWand;
        }

        //右键：蓄力中子湮灭阵列
        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<NeutronWandHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<NeutronWandHeld>(player, source);
    }
}
