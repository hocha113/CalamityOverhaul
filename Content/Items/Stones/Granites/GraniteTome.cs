using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Granites
{
    /// <summary>花岗魔典，充能三点齐射三发轻追踪球，命中碎晶</summary>
    internal class GraniteTome : ModItem
    {
        public override void SetDefaults() {
            Item.width = Item.height = 34;
            Item.damage = 20;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 7;
            Item.useTime = Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 2.5f;
            //翻页起手音
            Item.UseSound = SoundID.Item43 with { Volume = 0.6f, Pitch = -0.12f };
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<GraniteTomeHeld>();
            Item.shootSpeed = 10f;
            Item.value = Item.sellPrice(0, 0, 60, 0);
            Item.rare = ItemRarityID.Orange;
        }

        //场上仅一本法书
        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<GraniteTomeHeld>()] <= 0;

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Granite, 18)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 8)
                .AddIngredient(ItemID.FallenStar, 3)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>浮空法书，三点汇聚后扇形齐射+后坐闪光</summary>
    internal class GraniteTomeHeld : BaseHeldProj, IAdditiveDrawable
    {
        public override string Texture => GraniteMarbleVFX.GraniteTex + "GraniteTome";

        //汇聚节点 24%/51%/78%，第三颗齐射
        private const float FirstCharge = 0.24f;
        private const float ChargeStep = 0.27f;
        private const int ChargeCount = 3;

        private Vector2 aim = Vector2.UnitX;
        private int banked;      //已汇聚点数，边沿检测
        private int chantTime;   //吟咏视觉计时
        private float recoil;    //齐射后坐，置1后指数衰减
        private float fireFlash; //齐射闪光

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 40;
            Projectile.friendly = false;
        }

        //书体锚定，禁速度积分
        public override bool ShouldUpdatePosition() => false;

        //Initialize 远端速度清零时退回同步鼠标方向
        public override void Initialize() {
            Vector2 dir = Projectile.velocity != Vector2.Zero ? Projectile.velocity : ToMouse;
            aim = dir.SafeNormalize(Vector2.UnitX);
            Projectile.velocity = Vector2.Zero;
        }

        private int Duration => Owner.itemAnimationMax > 0 ? Owner.itemAnimationMax : 26;

        private float Progress => 1f - Projectile.timeLeft / (float)Duration;

        //光点槽位，书前垂于瞄准
        private Vector2 GatherSlot(int index) {
            Vector2 perp = aim.RotatedBy(MathHelper.PiOver2);
            Vector2 rest = Projectile.Center + aim * 34f + perp * ((index - 1) * 13f);
            float phase = chantTime * 0.16f + index * 2.1f;
            return rest + perp * MathF.Sin(phase) * 2.6f + aim * MathF.Cos(phase * 0.7f) * 1.8f;
        }

        public override void AI() {
            SetHeld();
            chantTime++;
            //首帧对齐寿命，其后 timeLeft=进度
            if (chantTime == 1) {
                Projectile.timeLeft = Duration;
            }

            //跟鼠标，限转速
            aim = aim.ToRotation().AngleTowards(ToMouseA, 0.22f).ToRotationVector2();
            SetDirection();

            recoil *= 0.82f;
            fireFlash *= 0.86f;

            //进度推点数，第三颗齐射
            float progress = Progress;
            int charges = 0;
            for (int i = 0; i < ChargeCount; i++) {
                if (progress >= FirstCharge + ChargeStep * i) {
                    charges++;
                }
            }
            while (banked < charges) {
                OnMoteBanked(banked);
                banked++;
                if (banked == ChargeCount) {
                    FireVolley();
                }
            }

            //浮动呼吸+后坐
            float bob = MathF.Sin(progress * MathHelper.Pi) * 6f + MathF.Sin(chantTime * 0.11f) * 1.6f;
            Projectile.Center = Owner.GetPlayerStabilityCenter() + aim * (26f - recoil * 10f)
                + aim.RotatedBy(MathHelper.PiOver2) * bob + new Vector2(0f, -4f);
            Projectile.rotation = aim.ToRotation();

            if (banked < ChargeCount) {
                UpdateChantDust(charges);
            }

            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3()
                * (0.45f + 0.18f * banked + fireFlash * 0.8f));
        }

        //光点到位音阶
        private void OnMoteBanked(int index) {
            Vector2 slot = GatherSlot(index);
            SoundEngine.PlaySound(SoundID.MaxMana with {
                Volume = 0.5f,
                Pitch = -0.18f + 0.2f * index
            }, slot);

            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Light>(slot + Main.rand.NextVector2Circular(10f, 10f)
                    , Main.rand.NextVector2Circular(1.2f, 1.2f), GraniteMarbleVFX.GraniteSpark
                    , Main.rand.NextFloat(0.2f, 0.34f)).Configure(12, 1f, 1.2f);
            }
            PRTLoader.NewParticle<PRT_GraniteVolt>(slot, Vector2.Zero, GraniteMarbleVFX.GraniteCore
                , Main.rand.NextFloat(0.2f, 0.3f)).Configure(Main.rand.Next(3, 6));
        }

        //三点齐射
        private void FireVolley() {
            Vector2 muzzle = Projectile.Center + aim * 26f;
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.9f, Pitch = 0.25f }, muzzle);
            SoundEngine.PlaySound(SoundID.Item43 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            recoil = 1f;
            fireFlash = 1f;

            if (Projectile.IsOwnedByLocalPlayer()) {
                //单发0.75x 控总伤
                int damage = (int)(Projectile.damage * 0.75f);
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = aim.RotatedBy(i * 0.16f) * 11f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), muzzle, vel
                        , ModContent.ProjectileType<GraniteEnergyOrb>()
                        , damage, Projectile.knockBack, Projectile.owner);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 14; i++) {
                PRTLoader.NewParticle<PRT_Light>(muzzle
                    , aim.RotatedByRandom(0.45f) * Main.rand.NextFloat(1.5f, 5.5f)
                    , Main.rand.NextBool() ? GraniteMarbleVFX.GraniteSpark : GraniteMarbleVFX.GraniteCore
                    , Main.rand.NextFloat(0.28f, 0.5f)).Configure(16, 1f, 1.4f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(muzzle + Main.rand.NextVector2Circular(8f, 8f)
                    , aim.RotatedByRandom(0.5f) * 3f, GraniteMarbleVFX.GraniteCore
                    , Main.rand.NextFloat(0.26f, 0.42f)).Configure(Main.rand.Next(3, 6));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(muzzle, Vector2.Zero
                , GraniteMarbleVFX.GraniteDeep, 0).Configure(0.04f, 0.42f, 14);

            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    muzzle, aim, 2f, 4f, 5, 400f, FullName));
            }
        }

        //吟咏氛围粒子
        private void UpdateChantDust(int charges) {
            if (VaultUtils.isServer) {
                return;
            }
            if (chantTime % 2 == 0) {
                Vector2 slot = GatherSlot(Math.Min(charges, ChargeCount - 1));
                Vector2 from = slot + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(30f, 54f);
                PRTLoader.NewParticle<PRT_Light>(from, from.To(slot) * 0.07f
                    , Main.rand.NextBool(3) ? GraniteMarbleVFX.GraniteDeep : GraniteMarbleVFX.GraniteCore
                    , Main.rand.NextFloat(0.18f, 0.3f)).Configure(14, 0.9f, 1.1f);
            }
            if (Main.rand.NextBool(7)) {
                Vector2 edge = Projectile.Center + Main.rand.NextVector2CircularEdge(18f, 14f);
                Vector2 drift = aim.RotatedBy(MathHelper.PiOver2)
                    * Main.rand.NextFloat(0.4f, 0.9f) * (Main.rand.NextBool() ? 1f : -1f);
                PRTLoader.NewParticle<PRT_Light>(edge, drift, GraniteMarbleVFX.GraniteSpark
                    , Main.rand.NextFloat(0.14f, 0.24f)).Configure(12, 0.8f, 1f);
            }
            if (Main.rand.NextBool(24)) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(Projectile.Center + Main.rand.NextVector2Circular(14f, 12f)
                    , Vector2.Zero, GraniteMarbleVFX.GraniteCore
                    , Main.rand.NextFloat(0.18f, 0.28f)).Configure(Main.rand.Next(2, 5));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            SpriteEffects fx = Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            //近竖直微倾+呼吸缩放
            float tilt = aim.Y * 0.3f * Owner.direction;
            float breath = 1f + MathF.Sin(chantTime * 0.09f) * 0.035f + fireFlash * 0.08f;
            Main.EntitySpriteDraw(tex, pos, null, Projectile.GetAlpha(lightColor), tilt
                , tex.Size() / 2f, Projectile.scale * breath, fx, 0);
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Color deep = GraniteMarbleVFX.GraniteDeep; deep.A = 0;
            Color core = GraniteMarbleVFX.GraniteCore; core.A = 0;
            Color spark = GraniteMarbleVFX.GraniteSpark; spark.A = 0;

            //书身底辉
            Vector2 bookPos = Projectile.Center - Main.screenPosition;
            spriteBatch.Draw(glow, bookPos, null, deep * (0.3f + 0.1f * banked), 0f
                , glow.Size() / 2f, 0.52f, SpriteEffects.None, 0f);

            //充能光点
            float progress = Progress;
            if (banked < ChargeCount) {
                for (int i = 0; i < ChargeCount; i++) {
                    float grow;
                    if (i < banked) {
                        grow = 1f;
                    }
                    else if (i == banked) {
                        float begin = FirstCharge + ChargeStep * (i - 1);
                        grow = MathHelper.Clamp((progress - begin) / ChargeStep, 0f, 1f);
                    }
                    else {
                        continue;
                    }
                    if (grow <= 0f) {
                        continue;
                    }
                    Vector2 slot = GatherSlot(i) - Main.screenPosition;
                    float pulse = 1f + 0.15f * MathF.Sin(chantTime * 0.24f + i * 1.7f);
                    float s = (0.16f + 0.1f * grow) * pulse;
                    spriteBatch.Draw(glow, slot, null, deep * (0.55f * grow), 0f
                        , glow.Size() / 2f, s * 1.6f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(glow, slot, null, core * (0.85f * grow), 0f
                        , glow.Size() / 2f, s, SpriteEffects.None, 0f);
                    spriteBatch.Draw(star, slot, null, spark * (0.7f * grow), chantTime * 0.05f + i
                        , star.Size() / 2f, 0.05f + 0.035f * grow * pulse, SpriteEffects.None, 0f);
                }
            }

            //齐射闪光
            if (fireFlash > 0.05f) {
                Vector2 muzzle = Projectile.Center + aim * 26f - Main.screenPosition;
                Color flashWhite = Color.White; flashWhite.A = 0;
                spriteBatch.Draw(glow, muzzle, null, core * (0.9f * fireFlash), 0f
                    , glow.Size() / 2f, 0.25f + 0.9f * fireFlash, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, muzzle, null, spark * (0.75f * fireFlash), 0f
                    , glow.Size() / 2f, 0.15f + 0.5f * fireFlash, SpriteEffects.None, 0f);
                spriteBatch.Draw(star, muzzle, null, flashWhite * (0.8f * fireFlash), aim.ToRotation()
                    , star.Size() / 2f, new Vector2(0.3f, 0.12f) * fireFlash, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>能量球，轻追踪，命中/撞地裂两枚追击晶</summary>
    internal class GraniteEnergyOrb : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private Trail Trail;
        //延迟锁定，避三发拧成一股
        private const int HomingDelay = 9;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.ai[0]++;
            const float maxSpeed = 12.5f;
            if (Projectile.ai[0] > HomingDelay) {
                NPC target = Projectile.Center.FindClosestNPC(780f);
                if (target != null) {
                    Vector2 desired = Projectile.Center.To(target.Center).UnitVector() * 11.5f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.03f);
                }
            }
            if (Projectile.velocity.Length() > maxSpeed) {
                Projectile.velocity = Projectile.velocity.UnitVector() * maxSpeed;
            }

            Projectile.rotation += 0.18f;
            Projectile.scale = 1f + MathF.Sin(Projectile.ai[0] * 0.24f) * 0.07f;
            Lighting.AddLight(Projectile.Center, GraniteMarbleVFX.GraniteCore.ToVector3() * 0.85f);

            if (VaultUtils.isServer) {
                return;
            }
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.06f
                    , GraniteMarbleVFX.GraniteCore, 0.26f).Configure(14, 1f, 1.15f);
            }
            if (Main.rand.NextBool(10)) {
                PRTLoader.NewParticle<PRT_GraniteVolt>(Projectile.Center + Main.rand.NextVector2Circular(7f, 7f)
                    , Projectile.velocity * 0.1f, GraniteMarbleVFX.GraniteSpark
                    , Main.rand.NextFloat(0.2f, 0.32f)).Configure(Main.rand.Next(2, 5));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                //碎裂分层音
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.35f, Volume = 0.7f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.32f, Pitch = 0.5f }, Projectile.Center);

                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_GraniteShard>(Projectile.Center
                        , Main.rand.NextVector2Circular(3f, 2.6f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f)
                        , GraniteMarbleVFX.GraniteSpark, Main.rand.NextFloat(0.5f, 0.85f))
                        .Configure(Main.rand.Next(26, 40));
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_GraniteVolt>(Projectile.Center + Main.rand.NextVector2Circular(9f, 9f)
                        , Main.rand.NextVector2Unit() * 2.5f, GraniteMarbleVFX.GraniteCore
                        , Main.rand.NextFloat(0.25f, 0.42f)).Configure(Main.rand.Next(3, 6));
                }
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Main.rand.NextVector2Circular(4.5f, 4.5f)
                        , GraniteMarbleVFX.GraniteCore, Main.rand.NextFloat(0.26f, 0.5f)).Configure(16, 1f, 1.3f);
                }
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero
                    , GraniteMarbleVFX.GraniteDeep, 0).Configure(0.05f, 0.5f, 16);
            }

            //单发裂两枚晶
            if (Projectile.IsOwnedByLocalPlayer()) {
                float baseRot = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < 2; i++) {
                    Vector2 v = (baseRot + MathHelper.Pi * i + Main.rand.NextFloat(-0.4f, 0.4f)).ToRotationVector2()
                        * Main.rand.NextFloat(6f, 9f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, v
                        , ModContent.ProjectileType<GraniteCrystalShard>()
                        , (int)(Projectile.damage * 0.5f), Projectile.knockBack * 0.4f, Projectile.owner);
                }
            }
        }

        //半宽10px，贴18px核
        public float GetWidthFunc(float completionRatio)
            => MathF.Pow(1f - completionRatio, 0.75f) * 10f * Projectile.scale;

        public Color GetColorFunc(Vector2 completionRatio) => Color.White * Projectile.Opacity;

        void IPrimitiveDrawable.DrawPrimitives() {
            //出膛淡入/濒死淡出
            float fade = MathHelper.Clamp(Projectile.ai[0] / 8f, 0f, 1f)
                * MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            GraniteMarbleVFX.DrawGraniteArcTrailFromOldPos(Projectile, ref Trail
                , GetWidthFunc, GetColorFunc, fade);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Texture2D sliver = CWRAsset.Line.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float s = Projectile.scale;
            float pulse = 1f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + Projectile.whoAmI);

            Color deep = GraniteMarbleVFX.GraniteDeep; deep.A = 0;
            Color core = GraniteMarbleVFX.GraniteCore; core.A = 0;
            Color spark = GraniteMarbleVFX.GraniteSpark; spark.A = 0;

            spriteBatch.Draw(glow, pos, null, deep * 0.7f, 0f, glow.Size() / 2f
                , s * 1.05f * pulse, SpriteEffects.None, 0f);
            //三片切向晶棱，Line+PiOver2
            for (int i = 0; i < 3; i++) {
                float a = Projectile.rotation + MathHelper.TwoPi / 3f * i;
                Vector2 p = pos + a.ToRotationVector2() * 10f * s;
                spriteBatch.Draw(sliver, p, null, core * 0.75f, a + MathHelper.Pi
                    , sliver.Size() / 2f, new Vector2(0.05f, 0.085f) * s, SpriteEffects.None, 0f);
            }
            //核心亮球
            spriteBatch.Draw(glow, pos, null, core * 0.95f, 0f, glow.Size() / 2f
                , s * 0.55f * pulse, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, pos, null, spark * 0.85f, Projectile.rotation * 1.5f
                , star.Size() / 2f, s * 0.12f * pulse, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, pos, null, Color.White * 0.75f, 0f, glow.Size() / 2f
                , s * 0.22f, SpriteEffects.None, 0f);
        }
    }
}
