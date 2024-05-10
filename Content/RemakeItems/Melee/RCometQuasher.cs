using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RCometQuasher : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.CometQuasher>();
        public override int ProtogenesisID => ModContent.ItemType<CometQuasherEcType>();
        public override string TargetToolTipItemName => "CometQuasherEcType";
        public override void SetDefaults(Item item) {
            item.width = 46;
            item.height = 62;
            item.scale = 1.5f;
            item.damage = 80;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 15;
            item.useStyle = ItemUseStyleID.Swing;
            item.useTime = 15;
            item.useTurn = true;
            item.knockBack = 2.75f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.shoot = ModContent.ProjectileType<CometQuasherMeteor>();
            item.shootSpeed = 9f;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int proj = Projectile.NewProjectile(source, position, velocity
                    , ModContent.ProjectileType<CometQuasherMeteor>(), (int)(item.damage * 0.5f), item.knockBack, player.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
            Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
            return false;
        }

        public override bool? On_OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            Vector2 offsetVr = CWRUtils.GetRandomVevtor(-75, -105, Main.rand.Next(500, 600));
            Vector2 spanPos = target.Center + offsetVr;
            Vector2 vr = offsetVr.UnitVector() * -17;
            int proj = Projectile.NewProjectile(CWRUtils.parent(player), spanPos, vr
                , ModContent.ProjectileType<CometQuasherMeteor>(), item.damage, item.knockBack, player.whoAmI, 0f, 0.5f + (float)Main.rand.NextDouble() * 0.3f);
            Main.projectile[proj].Calamity().lineColor = Main.rand.Next(3);
            return false;
        }
    }
}
