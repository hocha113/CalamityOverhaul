using CalamityMod.Buffs.DamageOverTime;
using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.AstralProj;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAstralBlade : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.AstralBlade>();
        public override int ProtogenesisID => ModContent.ItemType<AstralBladeEcType>();
        public override string TargetToolTipItemName => "AstralBladeEcType";

        public override void SetDefaults(Item item) {
            item.damage = 85;
            item.DamageType = DamageClass.Melee;
            item.width = 80;
            item.height = 80;
            item.scale = 1;
            item.useTime = 12;
            item.useAnimation = 12;
            item.useTurn = true;
            item.useStyle = ItemUseStyleID.Swing;
            item.knockBack = 4f;
            item.value = CalamityGlobalItem.RarityCyanBuyPrice;
            item.rare = ItemRarityID.Cyan;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<AstralBall>();
            item.shootSpeed = 11;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity.RotatedBy(Main.rand.NextFloat(-0.15f, 0.15f)), type, (int)(damage * 0.75f), knockback, player.whoAmI);
            return false;
        }

        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<AstralInfectionDebuff>(), 300);
            if (hit.Crit) {
                var source = item.GetSource_FromThis();
                for (int i = 0; i < 3; i++) {
                    Projectile star = CalamityUtils.ProjectileBarrage(source, player.Center, target.Center
                        , Main.rand.NextBool(), 800f, 800f, 800f, 800f, 10f, ModContent.ProjectileType<AstralStar>(), (int)(hit.Damage * 0.4), 1f, player.whoAmI, true);
                    if (star.whoAmI.WithinBounds(Main.maxProjectiles)) {
                        star.DamageType = DamageClass.Melee;
                        star.ai[0] = 3f;
                    }
                }
            }
            return false;
        }

        public override bool? On_ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers) {
            return false;
        }
    }
}
