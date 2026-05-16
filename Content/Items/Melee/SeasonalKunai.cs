using CalamityOverhaul.Common;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 时令飞刃 —— 战士的日月消耗投掷品
    /// 每次掷出 4 把扇形苦无，命中后日间触发破晓灼烧、夜间触发夜衰
    /// 飞刃命中或寿命终结时会迸裂出 3 把次级飞刃做扇形扫荡
    /// </summary>
    internal class SeasonalKunai : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "SeasonalKunai";

        public override void SetDefaults() {
            Item.width = 38;
            Item.height = 38;
            Item.damage = 90;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 10;
            Item.knockBack = 3.5f;
            Item.UseSound = SoundID.Item39 with { Pitch = 0.05f, Volume = 0.7f };
            Item.autoReuse = true;
            Item.value = Item.sellPrice(copper: 24);
            Item.rare = ItemRarityID.Purple;
            Item.DamageType = DamageClass.Melee;
            Item.shoot = ModContent.ProjectileType<SeasonalKunaiProj>();
            Item.shootSpeed = 18f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //战士齐射 4 把扇形飞刃，每把都启用次级分裂效果 (ai0 = 1)
            for (int i = 0; i < 4; i++) {
                Vector2 vel = velocity.RotatedBy(MathHelper.ToRadians(-15 + 10 * i));
                Projectile.NewProjectile(source, position, vel, type, damage, knockback, player.whoAmI, 1f);
            }
            return false;
        }

        public override void AddRecipes() {
            if (!CWRRef.Has) {
                CreateRecipe(333)
                .AddIngredient(ItemID.LunarBar, 30)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
                return;
            }
            CreateRecipe(333).
                AddIngredient(CWRID.Item_LifeAlloy).
                AddIngredient(CWRID.Item_AstralBar).
                AddIngredient(CWRID.Item_GalacticaSingularity).
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    /// <summary>
    /// 时令飞刃实体
    /// 主弹幕 (ai0 = 1): 高伤强穿透，命中或寿命终结时分裂为 3 把次级飞刃
    /// 次级弹幕 (ai0 = 0): 单次穿透 + 短距追踪
    /// </summary>
    internal class SeasonalKunaiProj : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Melee + "SeasonalKunai";

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
            Projectile.DamageType = DamageClass.Melee;
            Projectile.extraUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10 * Projectile.extraUpdates;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            //闪烁动画
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
                //主弹幕: 强穿透 + 渐变扩大
                Projectile.penetrate = -1;
                if (Projectile.scale < 2) {
                    Projectile.scale += 0.01f;
                }
                if (Projectile.timeLeft < 240) {
                    Projectile.velocity *= 0.98f;
                }
            }
            else {
                //次级弹幕: 短距追踪 + 重力
                CWRRef.HomeInOnNPC(Projectile, !Projectile.tileCollide, 300f, 6f, 20f);
                Projectile.velocity.Y += 0.01f;
                if (Projectile.timeLeft < 240) {
                    Projectile.tileCollide = true;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            int buff = Main.dayTime ? BuffID.Daybreak : CWRID.Buff_Nightwither;
            target.AddBuff(buff, 180);

            //主弹幕命中时屏震 + 火星
            if (Projectile.ai[0] > 0 && Projectile.numHits <= 1 && CWRServerConfig.Instance.ScreenVibration) {
                Vector2 hitDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, hitDir, 2f, 3.5f, 5, 350f, FullName));
            }
        }

        public override void OnKill(int timeLeft) {
            int dustType = Utils.SelectRandom(Main.rand, 245, 157, 107);

            SoundEngine.PlaySound(SoundID.Item10, Projectile.position);

            for (int i = 1; i <= 27; i++) {
                float factor = 30f / i;
                Vector2 offset = Projectile.oldVelocity * factor;
                Vector2 position = Projectile.oldPosition - offset;

                CreateDust(position, dustType, 1.8f, 0.5f);
                CreateDust(position, dustType, 1.4f, 0.05f);
            }

            //主弹幕分裂为 3 把次级飞刃做扇形扫荡
            if (Projectile.ai[0] > 0 && Projectile.IsOwnedByLocalPlayer()) {
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center
                        , (Projectile.rotation + MathHelper.TwoPi / 3 * i).ToRotationVector2() * 2
                        , ModContent.ProjectileType<SeasonalKunaiProj>(), Projectile.damage,
                        Projectile.knockBack, Projectile.owner);
                }
            }
        }

        private void CreateDust(Vector2 position, int dustType, float scale, float velocityMultiplier) {
            int dustIndex = Dust.NewDust(position, 8, 8, dustType,
                Projectile.oldVelocity.X, Projectile.oldVelocity.Y, 100, default, scale);
            Dust dust = Main.dust[dustIndex];
            dust.noGravity = true;
            dust.velocity *= velocityMultiplier;
        }

        public override bool PreDraw(ref Color lightColor) {
            CWRRef.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            return false;
        }
    }
}
