using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Weapons.Rogue;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Melee;
using InnoVault.GameSystem;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ID.ContentSamples.CreativeHelper;

namespace CalamityOverhaul.Content.Items.Rogue
{
    internal class SeasonalKunai : ModItem
    {
        public override string Texture => CWRConstant.Item_Rogue + "SeasonalKunai";
        public override void SetDefaults() {
            Item.CloneDefaults(ModContent.ItemType<LunarKunai>());
            Item.useStyle = ItemUseStyleID.Swing;
            Item.damage = 90;
            Item.UseSound = null;
            Item.DamageType = CWRLoad.RogueDamageClass;
            Item.shoot = ModContent.ProjectileType<SeasonalKunaiThrowable>();
            ItemOverride.ItemMeleePrefixDic[Type] = true;
            ItemOverride.ItemRangedPrefixDic[Type] = false;
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

        public override void ModifyResearchSorting(ref ItemGroup itemGroup) => itemGroup = (ItemGroup)CalamityResearchSorting.RogueWeapon;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe(333).
                AddIngredient<LifeAlloy>().
                AddIngredient<AstralBar>().
                AddIngredient<GalacticaSingularity>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    internal class SeasonalKunaiProj : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Rogue + "SeasonalKunai";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 4;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10 * Projectile.extraUpdates;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.scale -= 0.02f;
                Projectile.alpha += 30;
                if (Projectile.alpha >= 250) {
                    Projectile.alpha = 255;
                    Projectile.localAI[0] = 1f;
                }
            }
            else if (Projectile.localAI[0] == 1f) {
                Projectile.scale += 0.02f;
                Projectile.alpha -= 30;
                if (Projectile.alpha <= 0) {
                    Projectile.alpha = 0;
                    Projectile.localAI[0] = 0f;
                }
            }

            if (Projectile.ai[0] > 0) {
                Projectile.penetrate = -1;
                if (Projectile.scale < 2) {
                    Projectile.scale += 0.01f;
                }
                if (Projectile.timeLeft < 240) {
                    Projectile.velocity *= 0.98f;
                }
            }
            else {
                CalamityUtils.HomeInOnNPC(Projectile, !Projectile.tileCollide, 300f, 6f, 20f);
                Projectile.velocity.Y += 0.01f;
                if (Projectile.timeLeft < 240) {
                    Projectile.tileCollide = true;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(3)) {
                int dustType = SolsticeHomeBeam.GetDustTypeBySeason(CalamityMod.CalamityMod.CurrentSeason);
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType,
                                        Projectile.velocity.X * 0.05f, Projectile.velocity.Y * 0.05f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int buff = Main.dayTime ? BuffID.Daybreak : ModContent.BuffType<Nightwither>();
            target.AddBuff(buff, 180);
        }

        public override void OnKill(int timeLeft) {
            int dustType = CalamityMod.CalamityMod.CurrentSeason switch {
                Season.Spring => Utils.SelectRandom(Main.rand, 245, 157, 107), //春季：绿色系尘埃
                Season.Summer => Utils.SelectRandom(Main.rand, 247, 228, 57),  //夏季：黄色系尘埃
                Season.Fall => Utils.SelectRandom(Main.rand, 6, 259, 158),     //秋季：橙色系尘埃
                Season.Winter => Utils.SelectRandom(Main.rand, 67, 229, 185),  //冬季：蓝色系尘埃
                _ => 0                                                         //默认值：无效尘埃类型
            };

            SoundEngine.PlaySound(SoundID.Item10, Projectile.position);

            for (int i = 1; i <= 27; i++) {
                float factor = 30f / i;
                Vector2 offset = Projectile.oldVelocity * factor;
                Vector2 position = Projectile.oldPosition - offset;

                //创建两种不同缩放和速度的尘埃效果
                CreateDust(position, dustType, 1.8f, 0.5f);  //较大缩放，较低速度
                CreateDust(position, dustType, 1.4f, 0.05f); //较小缩放，非常低速度
            }

            if (Projectile.ai[0] > 0) {
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center
                        , (Projectile.rotation + MathHelper.TwoPi / 3 * i).ToRotationVector2() * 2
                        , ModContent.ProjectileType<SeasonalKunaiProj>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
            }
        }

        private void CreateDust(Vector2 position, int dustType, float scale, float velocityMultiplier) {
            int dustIndex = Dust.NewDust(position, 8, 8, dustType, Projectile.oldVelocity.X, Projectile.oldVelocity.Y, 100, default, scale);
            Dust dust = Main.dust[dustIndex];
            dust.noGravity = true;
            dust.velocity *= velocityMultiplier;
        }

        public override bool PreDraw(ref Color lightColor) {
            CalamityUtils.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }
    }

    internal class SeasonalKunaiThrowable : BaseThrowable
    {
        public override string Texture => CWRConstant.Item_Rogue + "SeasonalKunai";
        public override void SetThrowable() {
            Projectile.DamageType = CWRLoad.RogueDamageClass;
            HandOnTwringMode = -40;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            OffsetRoting = MathHelper.PiOver4;
        }

        public override void ThrowOut() {
            if (stealthStrike) {
                for (int i = 0; i < 6; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center, UnitToMouseV.RotatedByRandom(0.3f) * 8
                        , ModContent.ProjectileType<SeasonalKunaiProj>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 1);
                }
            }
            else {
                for (int i = 0; i < 4; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Owner.Center, UnitToMouseV.RotatedByRandom(0.2f) * 8
                        , ModContent.ProjectileType<SeasonalKunaiProj>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
            }

            SoundEngine.PlaySound(SoundID.Item39, Owner.Center);
            Projectile.soundDelay = 10;
            Projectile.Kill();
        }
    }
}
