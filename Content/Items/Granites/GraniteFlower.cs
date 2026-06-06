using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Granites
{
    /// <summary>
    /// 花岗之花：射出水晶种子，飞行一段后绽放成停留的能量花，数次脉冲向四周喷射花瓣碎片
    /// </summary>
    internal class GraniteFlower : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 38;
            Item.damage = 20;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 12;
            Item.useTime = Item.useAnimation = 32;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 3f;
            Item.UseSound = SoundID.Item43;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<GraniteFlowerHeld>();
            Item.shootSpeed = 11f;
            Item.value = Item.sellPrice(0, 0, 75, 0);
            Item.rare = ItemRarityID.Orange;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<GraniteFlowerHeld>()] <= 0;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.GraniteBlock, 22)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 10)
                .AddIngredient(ItemID.FallenStar, 5)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>
    /// 持杖施法体：脚手架式持握 + 抛射水晶种子
    /// </summary>
    internal class GraniteFlowerHeld : BaseHeldProj
    {
        public override string Texture => GraniteMarbleVFX.GraniteTex + "GraniteFlower";
        private Vector2 aim = Vector2.UnitX;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 44;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 40;
            Projectile.friendly = false;
        }

        public override void OnSpawn(IEntitySource source) {
            aim = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.velocity = Vector2.Zero;
        }

        public override void AI() {
            SetHeld();
            int duration = Owner.itemAnimationMax;
            if (duration < 1) {
                duration = 32;
            }
            if (Projectile.timeLeft > duration) {
                Projectile.timeLeft = duration;
            }

            float life = 1f - Projectile.timeLeft / (float)duration;
            float thrust = MathF.Sin(life * MathHelper.Pi) * 10f;

            Projectile.velocity = aim;
            Projectile.Center = Owner.GetPlayerStabilityCenter() + aim * (24f + thrust);
            Projectile.rotation = aim.ToRotation();
            SetDirection();

            if (Projectile.ai[0] == 0f && life >= 0.4f) {
                Projectile.ai[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item43 with { Pitch = -0.1f }, Projectile.Center);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + aim * 30f
                        , aim * 11f, ModContent.ProjectileType<GraniteFlowerSeed>()
                        , Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 8; i++) {
                        PRTLoader.NewParticle<PRT_Light>(Projectile.Center + aim * 26f
                            , aim.RotatedByRandom(0.4f) * Main.rand.NextFloat(1f, 3f)
                            , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.3f, 0.5f)).Configure(16, 1f, 1.2f);
                    }
                }
            }

            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation + MathHelper.PiOver4;
            SpriteEffects fx = Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Main.EntitySpriteDraw(tex, pos, null, Projectile.GetAlpha(lightColor), rot
                , tex.Size() / 2f, Projectile.scale, fx, 0);
            return false;
        }
    }

    /// <summary>
    /// 水晶种子：直线飞行，命中 / 撞地 / 超时后绽放成能量花
    /// </summary>
    internal class GraniteFlowerSeed : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private Trail Trail;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation += 0.3f;
            Projectile.velocity *= 0.992f;
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.8f);
            if (Main.rand.NextBool(2) && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.1f
                    , GraniteMarbleVFX.GraniteCore, 0.3f).Configure(14, 1f, 1.2f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.Kill();
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = 0.3f }, Projectile.Center);
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<GraniteBloom>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
        }

        public float GetWidthFunc(float c) {
            float p = c > 0.5f ? 1f - c : c;
            return p * 2f * Projectile.scale * Projectile.width * 1.2f;
        }

        public Color GetColorFunc(Vector2 _) => GraniteMarbleVFX.GraniteCore * Projectile.Opacity;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.oldPos == null || Projectile.oldPos.Length == 0) {
                return;
            }
            Vector2[] positions = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < positions.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    Projectile.oldPos[i] = Projectile.Center;
                }
                positions[i] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }
            Trail ??= new Trail(positions, GetWidthFunc, GetColorFunc);
            Trail.TrailPositions = positions;

            Effect effect = EffectLoader.GradientTrail.Value;
            GraniteMarbleVFX.ApplyGradientTrail(effect, GraniteMarbleVFX.GraniteBar, CWRConstant.Masking + "StarTexture");
            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color core = GraniteMarbleVFX.GraniteCore; core.A = 0;
            spriteBatch.Draw(glow, pos, null, core * 0.9f, 0f, glow.Size() / 2f, Projectile.scale * 0.8f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 停留的能量花：绽放后数次脉冲，每次喷射水晶花瓣碎片，并对范围内敌人持续造成伤害
    /// </summary>
    internal class GraniteBloom : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int Life = 190;
        private const int PulseInterval = 48;
        private const int MaxPulses = 3;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
        }

        private float Open => MathHelper.Clamp((Life - Projectile.timeLeft) / 16f, 0f, 1f)
            * MathHelper.Clamp(Projectile.timeLeft / 26f, 0f, 1f);

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            Projectile.ai[0]++;

            if (Projectile.ai[0] >= PulseInterval && Projectile.ai[1] < MaxPulses) {
                Projectile.ai[0] = 0f;
                Projectile.ai[1]++;
                Pulse();
            }

            float light = Open * 1.1f;
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * light);
        }

        private void Pulse() {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.2f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero
                    , GraniteMarbleVFX.GraniteCore, 0).Configure(0.1f, 0.9f, 26);
                for (int i = 0; i < 14; i++) {
                    Vector2 v = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(2f, 6f);
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, v
                        , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.3f, 0.6f)).Configure(20, 1f, 1.4f);
                }
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                int petals = 8;
                float baseRot = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < petals; i++) {
                    Vector2 v = (baseRot + MathHelper.TwoPi / petals * i).ToRotationVector2() * Main.rand.NextFloat(7f, 10f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, v
                        , ModContent.ProjectileType<GraniteCrystalShard>()
                        , (int)(Projectile.damage * 0.4f), Projectile.knockBack * 0.3f, Projectile.owner);
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => VaultUtils.CircleIntersectsRectangle(Projectile.Center, 56f * Open, targetHitbox);

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float open = Open;
            if (open <= 0.01f) {
                return;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Texture2D ring = CWRAsset.DiffusionCircle.Value;

            Color deep = GraniteMarbleVFX.GraniteDeep; deep.A = 0;
            Color core = GraniteMarbleVFX.GraniteCore; core.A = 0;
            float pulse = 1f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);

            spriteBatch.Draw(ring, pos, null, deep * 0.55f * open, Main.GlobalTimeWrappedHourly, ring.Size() / 2f, open * 0.45f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, pos, null, deep * 0.8f * open, 0f, glow.Size() / 2f, open * 1.7f * pulse, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, pos, null, core * 0.9f * open, 0f, glow.Size() / 2f, open * 0.85f * pulse, SpriteEffects.None, 0f);

            int petals = 6;
            float spin = Main.GlobalTimeWrappedHourly * 1.4f;
            for (int i = 0; i < petals; i++) {
                float a = spin + MathHelper.TwoPi / petals * i;
                Vector2 off = a.ToRotationVector2() * 28f * open;
                spriteBatch.Draw(star, pos + off, null, core * 0.85f * open, a, star.Size() / 2f, open * 0.16f * pulse, SpriteEffects.None, 0f);
            }
        }
    }
}
