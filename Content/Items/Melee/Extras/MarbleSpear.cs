using CalamityOverhaul.Common;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class MarbleSpear : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "MarbleSpear";
        public override void SetDefaults() {
            Item.width = Item.height = 35;
            Item.damage = 8;
            Item.DamageType = DamageClass.Melee;
            Item.rare = ItemRarityID.Cyan;
            Item.shoot = ModContent.ProjectileType<MarbleSpearHeld>();
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.autoReuse = true;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 3.5f;
            Item.shootSpeed = 5f;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.channel = true;
        }
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;
    }

    internal class MarbleSpearHeld : BaseRapiers
    {
        public override string Texture => CWRConstant.Item_Melee + "MarbleSpear";
        public override void SetRapiers() {
            overHitModeing = 93;
            SkialithVarSpeedMode = 3;
            ShurikenOut = SoundID.Item1 with { Pitch = 0.24f };
        }
    }
}
