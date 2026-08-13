using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>大理石卵石，快投石，高血上限加伤，撞地弹一次</summary>
    internal class MarblePebble : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 22;
            Item.damage = 11;
            Item.DamageType = DamageClass.Melee;
            Item.useTime = Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 2f;
            Item.UseSound = SoundID.Item1 with { Pitch = 0.25f, Volume = 0.85f };
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MarblePebbleProj>();
            Item.shootSpeed = 14f;
            Item.value = Item.sellPrice(0, 0, 30, 0);
            Item.rare = ItemRarityID.Green;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 8)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 3)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    internal class MarblePebbleProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarblePebble";
        private Trail Trail;

        //HP加成超此值走金色重击反馈
        private const float HeavyBonusThreshold = 0.25f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 220;
            Projectile.tileCollide = true;
            Projectile.MaxUpdates = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        //4000血封顶 +85%
        private static float DavidBonus(NPC target) => MathHelper.Clamp(target.lifeMax / 4000f, 0f, 1f) * 0.85f;

        public override void AI() {
            Projectile.ai[0]++;
            if (Projectile.ai[0] > 26) {
                Projectile.velocity.Y += 0.18f;
            }
            if (Projectile.velocity.Y > 17f) {
                Projectile.velocity.Y = 17f;
            }
            Projectile.rotation += 0.3f * Math.Sign(Projectile.velocity.X == 0 ? 1 : Projectile.velocity.X);
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.MarbleCore.ToVector3() * 0.3f);

            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center - Projectile.velocity * 0.5f
                    , -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.3f, 0.3f)
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.16f, 0.28f))?.Configure(16, 0.32f, 0.03f);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.FinalDamage *= 1f + DavidBonus(target);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.5f) {
                    Projectile.velocity.X = -oldVelocity.X * 0.7f;
                }
                if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.5f) {
                    Projectile.velocity.Y = -oldVelocity.Y * 0.55f;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Tink with { Pitch = 0.35f, Volume = 0.5f }, Projectile.Center);
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center
                            , Projectile.velocity * 0.2f + new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-3.2f, -0.8f))
                            , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(16, 24));
                    }
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Main.rand.NextVector2Circular(1.6f, 1.2f)
                            , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.22f, 0.36f))?.Configure(18, 0.5f, 0.04f);
                    }
                }
                return false;
            }
            Projectile.Kill();
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = 0.1f, Volume = 0.35f }, Projectile.Center);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(Projectile.Center, Main.rand.NextVector2Circular(2f, 2f)
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 20));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center, Main.rand.NextVector2Circular(1.2f, 1.2f)
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.2f, 0.3f))?.Configure(16, 0.45f, 0.04f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            float bonus = DavidBonus(target);
            float heat = bonus / 0.85f;   //0~1 庞然度
            Vector2 impact = Projectile.Center;
            Vector2 recoil = -Projectile.velocity.SafeNormalize(Vector2.UnitX);

            int chips = 3 + (int)(heat * 4f);
            for (int i = 0; i < chips; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(impact, recoil.RotatedByRandom(0.85f) * Main.rand.NextFloat(2f, 4.5f)
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.45f, 0.75f))?.Configure(Main.rand.Next(18, 26));
            }
            PRTLoader.NewParticle<PRT_Smoke>(impact, recoil * 1.2f, GraniteMarbleVFX.MarbleDust
                , Main.rand.NextFloat(0.25f, 0.38f))?.Configure(18, 0.5f, 0.04f);

            if (bonus > HeavyBonusThreshold) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(Vector2.Lerp(impact, target.Center, 0.35f), Vector2.Zero
                    , GraniteMarbleVFX.MarbleGold, 0.05f)?.Configure(0.05f, 0.22f + 0.34f * heat, 16);
                int stars = 3 + (int)(heat * 4f);
                for (int i = 0; i < stars; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(impact, Main.rand.NextVector2Circular(2.6f, 2.6f)
                        , GraniteMarbleVFX.MarbleGold, 0.5f)?.Configure(GraniteMarbleVFX.MarbleGold, 14, 0.25f, Main.rand.NextFloat(0.5f, 0.85f));
                }
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                    Pitch = -0.2f - heat * 0.15f + Main.rand.NextFloat(0.06f),
                    Volume = 0.55f + 0.3f * heat
                }, impact);
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.3f, Volume = 0.5f }, impact);
                //约2400+血才震屏
                if (heat > 0.6f && CWRServerConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(impact, -recoil, 3f, 5f, 8, 600f, FullName));
                }
            }
            else {
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.3f + Main.rand.NextFloat(-0.05f, 0.05f), Volume = 0.4f }, impact);
            }
        }

        public float GetWidthFunc(float c) {
            //半宽≤3.2px，慢速再收
            float speedFade = MathHelper.Clamp(Projectile.velocity.Length() / 14f, 0f, 1f);
            return (1f - c) * Projectile.scale * 3.2f * (0.35f + 0.65f * speedFade);
        }

        //RGB 由 MarbleBar 定，此处只压透明度
        public Color GetColorFunc(Vector2 coord) =>
            GraniteMarbleVFX.MarbleDust * (0.32f * (1f - coord.X * 0.45f) * Projectile.Opacity);

        void IPrimitiveDrawable.DrawPrimitives() =>
            GraniteMarbleVFX.DrawGradientTrailFromOldPos(Projectile, ref Trail, GetWidthFunc, GetColorFunc
                , GraniteMarbleVFX.MarbleBar, CWRAsset.Line.Value);
    }
}
