using CalamityMod.Items;
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
    internal class REntropicClaymore : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.EntropicClaymore>();
        public override int ProtogenesisID => ModContent.ItemType<EntropicClaymoreEcType>();
        public override string TargetToolTipItemName => "EntropicClaymoreEcType";
        public override void Load() {
            SetReadonlyTargetID = TargetID;
        }
        public override void SetDefaults(Item item) {
            item.damage = 152;
            item.DamageType = DamageClass.Melee;
            item.useAnimation = 78;
            item.useTime = 78;
            item.knockBack = 5.25f;
            item.useStyle = ItemUseStyleID.Swing;
            item.UseSound = SoundID.Item1;
            item.useTurn = true;
            item.autoReuse = true;
            item.noUseGraphic = true;
            item.noMelee = true;
            item.value = CalamityGlobalItem.Rarity9BuyPrice;
            item.rare = ItemRarityID.Cyan;
            item.shoot = ModContent.ProjectileType<EntropicClaymoreHoldoutProj>();
            item.shootSpeed = 12f;
            CWRUtils.EasySetLocalTextNameOverride(item, "EntropicClaymoreEcType");
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            item.initialize();
            item.CWR().ai[0]++;
            if (item.CWR().ai[0] > 2)
                item.CWR().ai[0] = 0;
            Projectile proj = Projectile.NewProjectileDirect(
                source,
                position,
                velocity,
                type,
                damage,
                knockback,
                player.whoAmI,
                ai2: item.useTime
                );
            proj.timeLeft = item.useTime;
            proj.localAI[0] = item.CWR().ai[0];
            return false;
        }
    }
}
