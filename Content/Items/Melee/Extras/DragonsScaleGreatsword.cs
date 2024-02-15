using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.DragonsScaleGreatswordProj;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class DragonsScaleGreatsword : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "DragonsScaleGreatsword";

        public Texture2D Value => CWRUtils.GetT2DValue(Texture);

        public override void SetDefaults() {
            Item.height = 54;
            Item.width = 54;
            Item.damage = 232;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 18;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 2.5f;
            Item.UseSound = SoundID.Item60;
            Item.autoReuse = true;
            Item.value = CalamityGlobalItem.Rarity1BuyPrice;
            Item.rare = ModContent.RarityType<Violet>();
            Item.shoot = ModContent.ProjectileType<DragonsScaleGreatswordBeam>();
            Item.shootSpeed = 7f;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 3;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (!Item.CWR().closeCombat) {
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback);
            }
            Item.CWR().closeCombat = false;
            return false;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            Item.CWR().closeCombat = true;
            target.AddBuff(BuffID.Poisoned, 1200);
            for (int i = 0; i < 6; i++) {
                Vector2 spanPos = target.Center + new Vector2(Main.rand.Next(-523, 524), Main.rand.Next(-353, 0));
                int proj = Projectile.NewProjectile(player.GetSource_FromThis(), spanPos, spanPos.To(target.Center).UnitVector() * Main.rand.Next(3, 5)
                    , ModContent.ProjectileType<SporeCloud>(), Item.damage / 2, 0, player.whoAmI);
                Main.projectile[proj].timeLeft = 120;
                Main.projectile[proj].scale = 1.2f + Main.rand.NextFloat(0.3f);
            }
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            Item.CWR().closeCombat = true;
            target.AddBuff(BuffID.Poisoned, 600);
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            for (int i = 0; i < 6; i++) {
                int dust = Dust.NewDust(hitbox.TopLeft(), hitbox.Width, hitbox.Height, DustID.JungleSpore);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].scale = Main.rand.NextFloat(0.5f, 2.2f);
            }
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<PerennialBar>(15).
                AddIngredient<UelibloomBar>(15).
                AddIngredient(ItemID.ChlorophyteBar, 15).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }
}
