using CalamityMod.Items;
using CalamityMod;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.VulcaniteProj;
using Microsoft.Xna.Framework;
using CalamityOverhaul.Content.Items.Melee;
using System.Collections.Generic;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RVulcaniteLance : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.VulcaniteLance>();
        public override int ProtogenesisID => ModContent.ItemType<VulcaniteLanceEcType>();
        public override string TargetToolTipItemName => "VulcaniteLanceEcType";

        public override void SetDefaults(Item item) {
            item.width = 44;
            item.damage = 90;
            item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            item.noMelee = true;
            item.useTurn = true;
            item.noUseGraphic = true;
            item.useAnimation = 22;
            item.useStyle = ItemUseStyleID.Shoot;
            item.useTime = 22;
            item.knockBack = 6.75f;
            item.UseSound = SoundID.Item1;
            item.autoReuse = true;
            item.height = 44;
            item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            item.rare = ItemRarityID.Yellow;
            item.shoot = ModContent.ProjectileType<RVulcaniteLanceProj>();
            item.shootSpeed = 10f;
        }

        public override void HoldItem(Item item, Player player) {
            if (Main.rand.NextBool(13)) {
                Vector2 pos = player.Center + new Vector2(Main.rand.Next(-320, 230), Main.rand.Next(-160, 32));
                Projectile.NewProjectile(player.parent(), pos, new Vector2(0, -1), ModContent.ProjectileType<VulcaniteBall>(), item.damage, 0, player.whoAmI);
            }
        }
    }
}
