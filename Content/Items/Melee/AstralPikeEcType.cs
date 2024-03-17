using CalamityMod.Items;
using CalamityMod.Projectiles.Melee.Spears;
using CalamityMod;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 幻星长矛
    /// </summary>
    internal class AstralPikeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "AstralPike";
        public new string LocalizationCategory => "Items.Weapons.Melee";
        public const int InTargetProjToLang = 1220;
        public const int ShootPeriod = 2;

        public override void SetStaticDefaults() {
            ItemID.Sets.Spears[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.width = 44;
            Item.damage = 90;
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.noMelee = true;
            Item.useTurn = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 13;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 13;
            Item.knockBack = 8.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 50;
            Item.value = CalamityGlobalItem.Rarity9BuyPrice;
            Item.rare = ItemRarityID.Cyan;
            Item.shoot = ModContent.ProjectileType<RAstralPikeProj>();
            Item.shootSpeed = 13f;
            
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 25;

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;
    }
}
