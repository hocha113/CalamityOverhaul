using CalamityMod.Items;
using CalamityMod.Projectiles.Melee.Spears;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.EarthenProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 地炎长矛
    /// </summary>
    internal class EarthenPikeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "EarthenPike";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        
        public override void SetStaticDefaults() {
            ItemID.Sets.Spears[Item.type] = true;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 60;
            Item.damage = 90;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.useTurn = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 25;
            Item.knockBack = 7f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 60;
            Item.value = CalamityGlobalItem.Rarity5BuyPrice;
            Item.rare = ItemRarityID.Pink;
            Item.shoot = ModContent.ProjectileType<REarthenPikeSpear>();
            Item.shootSpeed = 8f;
            
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile proj = Projectile.NewProjectileDirect(source, position, velocity, ModContent.ProjectileType<EarthenPikeThrowProj>(), damage * 2, knockback);
                EarthenPikeThrowProj earthenPikeThrowProj = (EarthenPikeThrowProj)proj.ModProjectile;
                if(earthenPikeThrowProj != null) {
                    earthenPikeThrowProj.earthenPike = Item.Clone();
                    Item.TurnToAir();
                } 
                else {
                    proj.Kill();
                }
                return false;
            }
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;// && player.ownedProjectileCounts[ModContent.ProjectileType<EarthenPikeThrowProj>()] <= 0;一般来讲是不需要判断投掷弹幕的数量的，因为这从使用情景上来讲没必要
    }
}
