using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Extras
{
    internal class GraniteSpear : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "GraniteSpear";
        public override void SetDefaults() {
            Item.width = Item.height = 35;
            Item.damage = 8;
            Item.DamageType = DamageClass.Melee;
            Item.rare = ItemRarityID.Cyan;
            Item.shoot = ModContent.ProjectileType<GraniteSpearHeld>();
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

    internal class GraniteSpearHeld : BaseRapiers
    {
        public override string Texture => CWRConstant.Item_Melee + "GraniteSpear";
        public override void SetRapiers() {
            overHitModeing = 93;
            SkialithVarSpeedMode = 3;
            ShurikenOut = SoundID.Item1 with { Pitch = 0.24f };
        }
    }
}
