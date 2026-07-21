using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow;
using CalamityOverhaul.Content.Scenarios.SupCal.SupCalDisplayTexts;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Pandemoniums
{
    /// <summary>万魔殿</summary>
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
            Item.rare = CWRID.Rarity_BurnishedAuric;
            Item.UseSound = SoundID.Item113;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PandemoniumChannel>();
            Item.shootSpeed = 10f;
            Item.channel = true;
        }

        public override void AddRecipes() {
            if (!CWRID.AllValid(CWRID.Item_AshesofAnnihilation, CWRID.Item_Heresy
                , CWRID.Item_Vehemence, CWRID.Item_Rock)) {
                return;
            }
            CreateRecipe()
                .AddIngredient(CWRID.Item_Heresy)
                .AddIngredient(CWRID.Item_Vehemence)
                .AddIngredient(CWRID.Item_AshesofAnnihilation, 38)
                .AddIngredient(CWRID.Item_Rock)
                .AddEndgameStation()
                .DisableDecraft()
                .Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            tooltips.InsertHotkeyBinding(CWRKeySystem.WeponSkill_Q, "PandemoniumQSkill", CWRKeySystem.Notbound.Value);
            tooltips.InsertHotkeyBinding(CWRKeySystem.WeponSkill_R, "PandemoniumRSkill", CWRKeySystem.Notbound.Value);

            if (EbnState.OnEbn(Main.LocalPlayer)) {
                TooltipLine line = new(Mod, "Story", SupCalDisplayText.Story4.Value);
                line.OverrideColor = Color.OrangeRed;
                tooltips.Add(line);
            }
        }

        public override void HoldItem(Player player) {
            if (CWRKeySystem.WeponSkill_Q.JustPressed && player.CountProjectilesOfID<PandemoniumQSkill>() == 0) {
                ShootState shootState = player.GetShootState();
                Projectile.NewProjectile(shootState.Source, player.Center
                    , Vector2.Zero, ModContent.ProjectileType<PandemoniumQSkill>()
                    , shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI);
            }
            if (CWRKeySystem.WeponSkill_R.JustPressed && player.CountProjectilesOfID<PandemoniumRSkill>() == 0) {
                ShootState shootState = player.GetShootState();
                Projectile.NewProjectile(shootState.Source, player.Center
                    , Vector2.Zero, ModContent.ProjectileType<PandemoniumRSkill>()
                    , shootState.WeaponDamage, shootState.WeaponKnockback, player.whoAmI);
            }
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                Item.mana = 40;
                Item.useTime = Item.useAnimation = 35;
                Item.channel = false;
                Item.shoot = ModContent.ProjectileType<PandemoniumCircle>();
                return player.ownedProjectileCounts[ModContent.ProjectileType<PandemoniumCircle>()] < 13; //最多13个法阵
            }
            else {
                Item.mana = 25;
                Item.useTime = Item.useAnimation = 20;
                Item.channel = true;
                Item.shoot = ModContent.ProjectileType<PandemoniumChannel>();
                return player.ownedProjectileCounts[ModContent.ProjectileType<PandemoniumChannel>()] == 0;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                
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
