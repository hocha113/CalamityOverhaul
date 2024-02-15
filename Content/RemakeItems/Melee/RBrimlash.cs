using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using CalamityOverhaul.Content.Items.Melee;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using CalamityMod.Buffs.DamageOverTime;
using Terraria.Audio;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBrimlash : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.Brimlash>();
        public override int ProtogenesisID => ModContent.ItemType<Brimlash>();
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override void SetDefaults(Item item) {
            item.width = item.height = 72;
            item.damage = 70;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 30;
            item.useTime = 30;
            item.useTurn = true;
            item.useStyle = ItemUseStyleID.Swing;
            item.knockBack = 6f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.value = CalamityGlobalItem.Rarity7BuyPrice;
            item.rare = ItemRarityID.Lime;
            item.shoot = ModContent.ProjectileType<BrimlashProj>();
            item.shootSpeed = 10f;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, "Brimlash");
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            item.useTime = item.useAnimation = 30;
            item.noUseGraphic = item.noMelee = false;
            if (player.altFunctionUse == 2) {
                item.useTime = item.useAnimation = 22;
                item.noUseGraphic = item.noMelee = true;
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                SoundEngine.PlaySound(SoundID.Item84, position);
                Lighting.AddLight(position, Color.Red.ToVector3());

                if (Main.rand.NextBool(16))
                    player.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 20);

                float randRot = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 5; i++) {
                    Vector2 vr = (MathHelper.TwoPi / 5f * i + randRot).ToRotationVector2() * 15;
                    Projectile.NewProjectile(source, position, vr, ModContent.ProjectileType<Brimlash2>(), damage, knockback, player.whoAmI);
                }

                return false;
            }
            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }
    }
}
