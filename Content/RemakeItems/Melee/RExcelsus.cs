using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RExcelsus : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.Excelsus>();
        public override int ProtogenesisID => ModContent.ItemType<ExcelsusEcType>();
        public override string TargetToolTipItemName => "ExcelsusEcType";

        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[ModContent.ItemType<CalamityMod.Items.Weapons.Melee.Excelsus>()] = true;
        }

        public override void SetDefaults(Item item) {
            item.width = 78;
            item.damage = 220;
            item.DamageType = DamageClass.Melee;
            item.useTime = item.useAnimation = 14;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTurn = true;
            item.knockBack = 8f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 94;
            item.value = CalamityGlobalItem.RarityDarkBlueBuyPrice;
            item.rare = ModContent.RarityType<DarkBlue>();
            item.shoot = ModContent.ProjectileType<ExcelsusMain>();
            item.shootSpeed = 12f;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                item.useTime = 10;
                Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<ExcelsusBomb>(), damage * 2, knockback, player.whoAmI);
            }
            else {
                item.useTime = 14;
                for (int i = 0; i < 3; i++) {
                    float speedX = velocity.X + Main.rand.NextFloat(-1.5f, 1.5f);
                    float speedY = velocity.Y + Main.rand.NextFloat(-1.5f, 1.5f);
                    switch (i) {
                        case 0:
                            type = ModContent.ProjectileType<ExcelsusMain>();
                            break;
                        case 1:
                            type = ModContent.ProjectileType<ExcelsusBlue>();
                            break;
                        case 2:
                            type = ModContent.ProjectileType<ExcelsusPink>();
                            break;
                    }

                    Projectile.NewProjectile(source, position.X, position.Y, speedX, speedY, type, damage, knockback, player.whoAmI);
                }
            }
            return false;
        }

        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.NewProjectile(player.GetSource_ItemUse(item), target.Center, Vector2.Zero, ModContent.ProjectileType<LaserFountains>()
                    , item.damage, 0f, player.whoAmI, target.whoAmI);
            return false;
        }

        public override bool? On_OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) {
            Projectile.NewProjectile(player.GetSource_ItemUse(item), target.Center, Vector2.Zero, ModContent.ProjectileType<LaserFountains>()
                    , item.damage, 0f, player.whoAmI, target.whoAmI);
            return false;
        }
    }
}
