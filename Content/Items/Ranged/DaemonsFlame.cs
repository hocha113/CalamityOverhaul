using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    /// <summary>
    /// 鬼火弓
    /// </summary>
    internal class DaemonsFlame : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DaemonsFlame";
        public override void SetDefaults() {
            Item.damage = 150;
            Item.width = 62;
            Item.height = 128;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 4f;
            Item.UseSound = SoundID.Item5;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.DamageType = DamageClass.Ranged;
            Item.channel = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<DaemonsFlameHeldProj>();
            Item.shootSpeed = 20f;
            Item.useAmmo = AmmoID.Arrow;
            Item.value = CalamityGlobalItem.Rarity13BuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();
            Item.Calamity().canFirePointBlankShots = true;
            Item.SetHeldProj<DaemonsFlameHeldProj>();
        }

        public override bool CanConsumeAmmo(Item ammo, Player player) => Main.rand.NextBool(3) && player.ownedProjectileCounts[Item.shoot] > 0;

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation, ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Ranged/DaemonsFlameGlow").Value);
        }
    }
}
