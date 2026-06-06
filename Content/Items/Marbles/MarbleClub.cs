using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Marbles
{
    /// <summary>
    /// 大理石巨棍：缓慢的大力挥击，命中有几率石化减速，首击落点迸发冲击波 + 尘土 + 屏震
    /// </summary>
    internal class MarbleClub : ModItem
    {
        private static int swingCounter;

        public override void SetDefaults() {
            Item.width = Item.height = 56;
            Item.damage = 22;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 33;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 7.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.useTurn = true;
            Item.shoot = ModContent.ProjectileType<MarbleClubHeld>();
            Item.shootSpeed = 6f;
            Item.value = Item.sellPrice(0, 0, 80, 0);
            Item.rare = ItemRarityID.Orange;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<MarbleClubHeld>()] <= 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            swingCounter++;
            float dir = swingCounter % 2 == 0 ? 1f : -1f;
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, dir);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.MarbleBlock, 25)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 12)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class MarbleClubHeld : BaseSwingHeld
    {
        protected override string TexturePath => GraniteMarbleVFX.MarbleTex + "MarbleClub";
        protected override float SwingArc => 2.9f;
        protected override float HoldDistance => 42f;
        protected override float BladeLength => 96f;
        protected override float BladeWidth => 46f;
        protected override float TrailWidthMax => 74f;
        protected override int TrailLength => 24;
        protected override float DrawScale => 1.05f;

        protected override void OnSwingStart() {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.4f, Volume = 0.7f }, Projectile.Center);
            }
        }

        protected override void OnSwingUpdate(float p, float ang) {
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleGold.ToVector3() * 0.45f);
            if (p > 0.3f && p < 0.85f && !VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 along = Owner.GetPlayerStabilityCenter()
                    + ang.ToRotationVector2() * Main.rand.NextFloat(BladeLength * 0.45f, BladeLength);
                PRTLoader.NewParticle<PRT_Sparkle>(along, Vector2.Zero, GraniteMarbleVFX.MarbleGold, 0.6f)
                    .Configure(GraniteMarbleVFX.MarbleGold, 14, 0.2f, Main.rand.NextFloat(0.4f, 0.7f));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.rand.NextBool(2)) {
                target.AddBuff(BuffID.Slow, 120);
                if (!target.boss) {
                    target.velocity *= 0.4f;
                }
            }

            if (Projectile.numHits != 0) {
                return;
            }

            Vector2 impact = Owner.GetPlayerStabilityCenter() + CurrentAngle.ToRotationVector2() * BladeLength;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.35f }, impact);
                for (int i = 0; i < 12; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(impact, Main.rand.NextVector2Circular(5f, 5f)
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.4f, 0.7f)).Configure(26, 0.7f, 0.05f);
                }
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(impact, Main.rand.NextVector2Circular(3f, 3f)
                        , GraniteMarbleVFX.MarbleGold, 0.7f).Configure(GraniteMarbleVFX.MarbleGold, 18, 0.2f, Main.rand.NextFloat(0.5f, 0.9f));
                }
            }

            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(impact, Main.rand.NextVector2Unit()
                    , 6.5f, 6f, 15, 800f, FullName));
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), impact, Vector2.Zero
                    , ModContent.ProjectileType<MarbleShockwave>(), (int)(Projectile.damage * 0.55f)
                    , Projectile.knockBack * 0.5f, Projectile.owner, 0f, 145f);
            }
        }
    }
}
