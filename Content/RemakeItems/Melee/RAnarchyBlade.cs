using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.CalPlayer;
using CalamityMod.Items;
using CalamityMod.NPCs;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.VulcaniteProj;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAnarchyBlade : BaseRItem
    {
        private static int BaseDamage = 150;
        public const int ShootPeriod = 3;
        public int ShootCount;
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.AnarchyBlade>();
        public override int ProtogenesisID => ModContent.ItemType<AnarchyBladeEcType>();
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        }
        public override string TargetToolTipItemName => "AnarchyBladeEcType";

        public override void SetDefaults(Item item) {
            item.width = 114;
            item.damage = BaseDamage;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 19;
            item.useTime = 19;
            item.useTurn = true;
            item.useStyle = ItemUseStyleID.Swing;
            item.knockBack = 7.5f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 122;
            item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.shoot = ModContent.ProjectileType<AnarchyBeam>();
            item.shootSpeed = 15;
        }

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            item.scale = 1;
            item.useTime = item.useAnimation = 17;
            if (player.altFunctionUse == 2) {
                item.scale = 1.2f;
                item.useTime = item.useAnimation = 19;
            }
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                return false;
            }
            ShootCount++;
            if (ShootCount < ShootPeriod) {//这里使用简单的流程控制语句进行周期发射
                return false;
            }
            ShootCount = 0;
            SoundEngine.PlaySound(SoundID.Item20, position);
            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            if (player.altFunctionUse == 2) {
                var source = player.GetSource_ItemUse(item);
                Projectile.NewProjectile(source, target.Center, Vector2.Zero, ModContent.ProjectileType<BrimstoneBoom>(), item.damage, item.knockBack, Main.myPlayer);
                target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 300);

                if (player.statLife <= (player.statLifeMax2 * 0.5f) && Main.rand.NextBool(5)) {
                    if (!CalamityPlayer.areThereAnyDamnBosses && CalamityGlobalNPC.ShouldAffectNPC(target)) {
                        target.life = 0;
                        target.HitEffect(0, 10.0);
                        target.active = false;
                        target.NPCLoot();
                    }
                }
            }
            return false;
        }

        public override bool? On_OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) {
            if (player.altFunctionUse == 2) {
                var source = player.GetSource_ItemUse(item);
                Projectile.NewProjectile(source, target.Center, Vector2.Zero, ModContent.ProjectileType<BrimstoneBoom>(), item.damage, item.knockBack, Main.myPlayer);
                target.AddBuff(ModContent.BuffType<BrimstoneFlames>(), 300);
            }
            return false;
        }
    }
}
