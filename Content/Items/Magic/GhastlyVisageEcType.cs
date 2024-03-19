using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    /// <summary>
    /// 鬼之形
    /// </summary>
    internal class GhastlyVisageEcType : EctypeItem
    {
        public new string LocalizationCategory => "Items.Weapons.Magic";

        public override string Texture => CWRConstant.Cay_Wap_Magic + "GhastlyVisage";

        public override void SetDefaults() {
            Item.damage = 69;
            Item.DamageType = DamageClass.Magic;
            Item.noUseGraphic = true;
            Item.channel = true;
            Item.mana = 20;
            Item.width = 32;
            Item.height = 36;
            Item.useTime = 27;
            Item.useAnimation = 27;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 5f;
            Item.shootSpeed = 9f;
            Item.shoot = ModContent.ProjectileType<GhastlyBlasts>();
            Item.value = CalamityGlobalItem.Rarity13BuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();
            Item.SetHeldProj<GhastlyVisageHeldProj>();
            
        }

        public override void OnConsumeMana(Player player, int manaConsumed) {
            player.statMana += manaConsumed;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation, ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Magic/GhastlyVisageGlow").Value);
        }
    }
}
