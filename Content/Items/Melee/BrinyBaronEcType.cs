using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 海爵剑
    /// </summary>
    internal class BrinyBaronEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "BrinyBaron";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.damage = 110;
            Item.knockBack = 2f;
            Item.useAnimation = Item.useTime = 15;
            Item.DamageType = DamageClass.Melee;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.shootSpeed = 4f;
            Item.shoot = ModContent.ProjectileType<Razorwind>();
            Item.width = 100;
            Item.height = 102;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.value = CalamityGlobalItem.Rarity8BuyPrice;
            Item.rare = ItemRarityID.Yellow;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            player.AddBuff(BuffID.Wet, 180);
            Projectile.NewProjectile(source, position, velocity.RotatedBy(MathHelper.ToRadians(10)), type, damage, knockback, player.whoAmI);
            Projectile.NewProjectile(source, position, velocity.RotatedBy(MathHelper.ToRadians(-10)), type, damage, knockback, player.whoAmI);
            return true;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (player.altFunctionUse == 2) {
                Item.useAnimation = Item.useTime = 20;
                damage = (int)(damage * 0.2f);
                type = ModContent.ProjectileType<Razorwind>();
            }
            else {
                Item.useAnimation = Item.useTime = 15;
                type = 0;
            }
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override bool CanUseItem(Player player) {
            return true;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox) {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(new Vector2(hitbox.X, hitbox.Y), hitbox.Width, hitbox.Height, DustID.Flare_Blue, 0f, 0f, 100, new Color(53, Main.DiscoG, 255));
            }
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers) {
            target.AddBuff(BuffID.Wet, 120);
            int newDef = target.defDefense - 3;
            if (newDef < 0) newDef = 0;
            target.defense = newDef;
            modifiers.CritDamage *= 0.5f;
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (player.whoAmI == Main.myPlayer) {
                Vector2 speed = CWRUtils.RandomBooleanValue(2, 1, true) ? new Vector2(16, 0) : new Vector2(-16, 0);
                if (Main.projectile.Count(n => n.active && n.type == ModContent.ProjectileType<SeaBlueBrinySpout>() && n.ai[1] == 1) <= 2) {
                    int proj = Projectile.NewProjectile(CWRUtils.parent(player), target.Center, speed, ModContent.ProjectileType<SeaBlueBrinySpout>(), Item.damage, Item.knockBack, player.whoAmI);
                    Main.projectile[proj].timeLeft = 60;
                    Main.projectile[proj].localAI[1] = 30;
                    Main.projectile[proj].netUpdate = true;
                }
            }
        }

        public override void OnHitPvp(Player player, Player target, Player.HurtInfo hurtInfo) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<SeaBlueBrinySpout>()] == 0 && player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(CWRUtils.parent(player), target.Center, Vector2.Zero, ModContent.ProjectileType<SeaBlueBrinySpout>(), Item.damage, Item.knockBack, player.whoAmI);
            }
        }

        public override bool? UseItem(Player player) {
            if (player.altFunctionUse == 2) {
                Item.noMelee = true;
            }
            else {
                Item.noMelee = false;
            }
            return null;
        }

        public override void UseAnimation(Player player) {
            Item.noUseGraphic = false;
            Item.UseSound = SoundID.Item1;
            if (player.altFunctionUse == 2) {
                Item.noUseGraphic = true;
                Item.UseSound = SoundID.Item84;
            }
        }
    }
}
