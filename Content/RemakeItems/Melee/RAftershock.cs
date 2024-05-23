using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Items;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.EarthenProj;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAftershock : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.Aftershock>();
        public override int ProtogenesisID => ModContent.ItemType<AftershockEcType>();
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }
        public override string TargetToolTipItemName => "AftershockEcType";

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override void SetDefaults(Item item) {
            item.damage = 65;
            item.DamageType = DamageClass.Melee;
            item.width = 54;
            item.height = 58;
            item.useTime = 28;
            item.useAnimation = 25;
            item.useTurn = true;
            item.useStyle = ItemUseStyleID.Swing;
            item.knockBack = 7.5f;
            item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            item.rare = ItemRarityID.Pink;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.shoot = ModContent.ProjectileType<MeleeFossilShard>();
            item.shootSpeed = 12f;
            CWRUtils.EasySetLocalTextNameOverride(item, "AftershockEcType");
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            item.useTime = item.useAnimation = 25;
            item.scale = 1;
            if (player.altFunctionUse == 2) {
                item.useTime = item.useAnimation = 28;
                item.scale = 1.5f;
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                for (int i = 0; i < 3; i++) {
                    Vector2 pos = position;
                    pos.X += Main.rand.Next(-120, 120);
                    pos.Y += Main.rand.Next(-330, -303);
                    Vector2 vr = new Vector2(Main.rand.NextFloat(-0.1f, 0.1f) + player.velocity.X * 0.1f, Main.rand.Next(-13, 7));
                    int proj = Projectile.NewProjectile(source, pos, vr, type, damage / 2, knockback);
                    Main.projectile[proj].scale = Main.rand.NextFloat(1, 1.5f);
                }
                return false;
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vr = velocity + new Vector2(Math.Sign(velocity.X) * Main.rand.NextFloat(1, 7.2f), Main.rand.NextFloat(-6.3f, 0));
                int proj = Projectile.NewProjectile(source, position, vr, type, damage / 3, knockback);
                Main.projectile[proj].scale = Main.rand.NextFloat(1, 1.25f);
            }
            return false;
        }

        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (player.altFunctionUse == 2) {
                target.AddBuff(ModContent.BuffType<ArmorCrunch>(), 300);
                AftershockEcType.OnHitSpanProjFunc(item, player, hit.Knockback);
            }
            return false;
        }

        public override bool? On_OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) {
            if (player.altFunctionUse == 2) {
                target.AddBuff(ModContent.BuffType<ArmorCrunch>(), 100);
                AftershockEcType.OnHitSpanProjFunc(item, player, hurtInfo.Knockback);
            }
            return false;
        }
    }
}
