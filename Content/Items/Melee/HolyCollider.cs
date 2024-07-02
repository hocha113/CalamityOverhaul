using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 圣火巨刃
    /// </summary>
    internal class HolyCollider : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "HolyCollider";
        public override void SetDefaults() {
            Item.width = 94;
            Item.height = 80;
            Item.scale = 1f;
            Item.damage = 230;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 16;
            Item.useTurn = true;
            Item.knockBack = 3.75f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<HolyColliderHolyFires>();
            Item.shootSpeed = 10f;
            Item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            Item.rare = ModContent.RarityType<Turquoise>();

        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            for (int i = 0; i < Main.rand.Next(3, 5); i++) {
                Projectile.NewProjectile(source, player.Center + Main.rand.NextVector2Unit() * Main.rand.Next(342, 468), velocity / 3, ModContent.ProjectileType<HolyColliderHolyFires>(), (int)(damage * 0.5f), knockback, player.whoAmI);
                for (int j = 0; j < 3; j++) {
                    Vector2 pos = player.Center + Main.rand.NextVector2Unit() * Main.rand.Next(342, 468);
                    Vector2 particleSpeed = pos.To(player.Center).UnitVector() * 7;
                    CWRParticle energyLeak = new HolyColliderLightParticle(pos, particleSpeed
                        , Main.rand.NextFloat(0.5f, 0.7f), Color.Gold, 90, 1, 1.5f, hueShift: 0.0f);
                    CWRParticleHandler.AddParticle(energyLeak);
                }
            }
            return false;
        }
    }
}
