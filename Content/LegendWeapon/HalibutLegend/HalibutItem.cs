using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    internal class HalibutItem : ModItem
    {
        //以HalibutOverride修改，这里写上属性是为了兼容性
        public override void SetDefaults() {
            Item.damage = 4;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 118;
            Item.height = 56;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.rare = CWRID.Rarity_HotPink > 0 ? CWRID.Rarity_HotPink : ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 2, 50, 0);
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 1f;
            Item.UseSound = SoundID.Item38 with { Volume = 0.6f };
            Item.autoReuse = true;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 12f;
            Item.useAmmo = AmmoID.Bullet;
        }

        /// <summary>返回 false 接管 tooltip 全绘制(行数据仍来自 ModifyTooltips 管线)</summary>
        public override bool PreDrawTooltip(ReadOnlyCollection<TooltipLine> lines, ref int x, ref int y)
            => HalibutItemTooltipPanel.Draw(Item, lines, x, y);
    }
}
