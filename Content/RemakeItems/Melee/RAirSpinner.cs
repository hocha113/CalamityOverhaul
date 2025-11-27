using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RAirSpinner : CWRItemOverride
    {
        public override void SetDefaults(Item item) {
            item.width = 28;
            item.height = 28;
            item.DamageType = DamageClass.MeleeNoSpeed;
            item.damage = 26;
            item.knockBack = 4f;
            item.useTime = 22;
            item.useAnimation = 22;
            item.autoReuse = true;
            item.useStyle = ItemUseStyleID.Shoot;
            item.UseSound = SoundID.Item1;
            item.channel = true;
            item.noUseGraphic = true;
            item.noMelee = true;
            item.shoot = ModContent.ProjectileType<RAirSpinnerYoyo>();
            item.shootSpeed = 14f;
            item.rare = ItemRarityID.Orange;
        }
    }
}
