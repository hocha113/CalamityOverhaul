using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 泰拉巨刃
    /// </summary>
    internal class TerratomereEcType : EctypeItem
    {
        public const int SwingTime = 83;

        public const int SlashLifetime = 27;

        public const int SmallSlashCreationRate = 9;

        public const int TrueMeleeHitHeal = 4;

        public const int TrueMeleeGlacialStateTime = 30;

        public const float SmallSlashDamageFactor = 0.4f;

        public const float ExplosionExpandFactor = 1.013f;

        public const float TrailOffsetCompletionRatio = 0.2f;

        public static readonly Color TerraColor1 = new Color(141, 203, 50);

        public static readonly Color TerraColor2 = new Color(83, 163, 136);

        public static readonly SoundStyle SwingSound = new SoundStyle("CalamityMod/Sounds/Item/TerratomereSwing");

        public override string Texture => CWRConstant.Cay_Wap_Melee + "Terratomere";

        public override void SetDefaults() {
            Item.width = 60;
            Item.height = 66;
            Item.damage = 185;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 21;
            Item.useTime = 21;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.knockBack = 7f;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            Item.rare = ModContent.RarityType<Turquoise>();
            Item.shoot = ModContent.ProjectileType<RTerratomereHoldoutProj>();
            Item.shootSpeed = 60f;

        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            return base.Shoot(player, source, position, velocity, type, damage, knockback);
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[Item.shoot] <= 0;
        }
    }
}
