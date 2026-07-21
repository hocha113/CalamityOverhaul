using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class HyperionBarrage : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "HyperionBarrage";
        [VaultLoaden(CWRConstant.Item_Ranged + "HyperionBarrageGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 94;
            Item.height = 34;
            Item.damage = 300;
            Item.useTime = 40;
            Item.useAnimation = 40;
            Item.useAmmo = AmmoID.Bullet;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 15;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.buyPrice(0, 2, 60, 10);
            Item.CWR().DeathModeItem = true;
        }

        //物品使用本身不消耗子弹，由手持弹幕在实际开火时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<HyperionBarrageHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<HyperionBarrageHeld>(player, source);

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }
    }

    internal class HyperionBarrageEX : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "HyperionBarrageEX";
        [VaultLoaden(CWRConstant.Item_Ranged + "HyperionBarrageEXGlow")]
        public static Asset<Texture2D> Glow = null;
        public override void SetDefaults() {
            Item.DamageType = DamageClass.Ranged;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.width = 124;
            Item.height = 46;
            Item.damage = 890;
            Item.useTime = 48;
            Item.useAnimation = 48;
            Item.useAmmo = AmmoID.Bullet;
            Item.shoot = ProjectileID.Bullet;
            Item.shootSpeed = 15;
            Item.UseSound = null;//开火音效在HeldProj
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 8, 60, 10);
        }

        //物品使用本身不消耗子弹，由手持弹幕在实际开火时自行拾取
        public override bool CanConsumeAmmo(Item ammo, Player player) => BaseHeldGun.AmmoConsumeContext;

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<HyperionBarrageEXHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<HyperionBarrageEXHeld>(player, source);

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor
            , Color alphaColor, float rotation, float scale, int whoAmI) {
            spriteBatch.Draw(Glow.Value, Item.Center - Main.screenPosition
                , null, Color.White, rotation, Glow.Value.Size() / 2, scale, SpriteEffects.None, 0);
        }

        public override void AddRecipes() {
            CreateRecipe().
                AddIngredient<HyperionBarrage>().
                AddIngredient<SoulofFrightEX>().
                AddTile(TileID.LunarCraftingStation).
                Register();
        }
    }

    /// <summary>
    /// 色盘，基础琥珀/EX猩红
    /// </summary>
    internal static class HyperionTheme
    {
        public static readonly Vector3 AmberCore = new(1.35f, 1.22f, 1.00f);
        public static readonly Vector3 AmberSheath = new(1.05f, 0.55f, 0.18f);
        public static readonly Vector3 AmberEmber = new(0.62f, 0.16f, 0.04f);
        public static readonly Color AmberGlow = new(255, 150, 50);

        public static readonly Vector3 CrimsonCore = new(1.40f, 1.10f, 0.95f);
        public static readonly Vector3 CrimsonSheath = new(1.10f, 0.26f, 0.12f);
        public static readonly Vector3 CrimsonEmber = new(0.52f, 0.06f, 0.04f);
        public static readonly Color CrimsonGlow = new(255, 66, 40);

        /// <summary>黄金比散列，种子→[0,1)</summary>
        public static float Hash01(float seed) => seed * 0.6180339887f % 1f;

        /// <summary>写入Exhaust/Blast三色</summary>
        public static void ApplyPalette(Effect effect, bool crimson) {
            effect.Parameters["coreColor"]?.SetValue(crimson ? CrimsonCore : AmberCore);
            effect.Parameters["sheathColor"]?.SetValue(crimson ? CrimsonSheath : AmberSheath);
            effect.Parameters["emberColor"]?.SetValue(crimson ? CrimsonEmber : AmberEmber);
        }

        /// <summary>世界空间quad画Blast technique，实体后层</summary>
        public static void DrawBlastQuad(string technique, Vector2 worldCenter, float size
            , float progress, float intensity, float seed, Vector2 direction, bool crimson) {
            Effect effect = EffectLoader.HyperionBlast?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            float half = size * 0.5f;
            var quad = new VertexPositionColorTexture[4];
            quad[0] = new VertexPositionColorTexture((worldCenter + new Vector2(-half, -half)).ToVector3(), Color.White, new Vector2(0, 0));
            quad[1] = new VertexPositionColorTexture((worldCenter + new Vector2(half, -half)).ToVector3(), Color.White, new Vector2(1, 0));
            quad[2] = new VertexPositionColorTexture((worldCenter + new Vector2(-half, half)).ToVector3(), Color.White, new Vector2(0, 1));
            quad[3] = new VertexPositionColorTexture((worldCenter + new Vector2(half, half)).ToVector3(), Color.White, new Vector2(1, 1));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique = effect.Techniques[technique];
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uProgress"]?.SetValue(progress);
            effect.Parameters["uIntensity"]?.SetValue(intensity);
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uDirection"]?.SetValue(direction);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            ApplyPalette(effect, crimson);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, quad, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }

    /// <summary>
    /// 持握骨架，开火反馈与枪口炬在此，
    /// 每次扣动扳机发射什么由子类的 <see cref="LaunchOrdnance"/> 决定
    /// </summary>
    internal abstract class BaseHyperionHeld : BaseHeldGun, IPrimitiveDrawable
    {
        private const int MuzzleFlashTime = 8;
        private int muzzleFlash;
        private float muzzleSeed;
        /// <summary>炮管积热0-1</summary>
        private float heat;

        protected abstract bool CrimsonTheme { get; }

        /// <summary>枪口炬面片边长（像素）</summary>
        protected virtual float MuzzleFlashSize => 120f;
        protected virtual float FireShake => 1.2f;
        protected Color ThemeGlow => CrimsonTheme ? HyperionTheme.CrimsonGlow : HyperionTheme.AmberGlow;

        public override void SetGunProperty() {
            GunPressure = 0.14f;
            ControlForce = 0.025f;
            RecoilRetroForceMagnitude = 7;
            RecoilOffsetRecoverValue = 0.75f;
            HandIdleDistanceX = 26;
            HandIdleDistanceY = 2;
            HandFireDistanceX = 26;
            HandFireDistanceY = -2;
            MuzzleForwardOffset = 22;
            MuzzleNormalOffset = -2;
            AlwaysAimPose = true;
        }

        public override void AI() {
            UpdateHeldPose(WantsFireLeft);

            if (WantsFireLeft && FireCooldown <= 0 && HasAmmo) {
                Fire();
                SetFireCooldown();
            }

            if (muzzleFlash > 0) {
                muzzleFlash--;
            }
            heat = MathHelper.Clamp(heat - 0.006f, 0f, 1f);
            if (heat > 0.3f) {
                Lighting.AddLight(ShootPos, ThemeGlow.ToVector3() * heat * 0.35f);
            }
            Time++;
        }

        private void Fire() {
            SnapToAimPose();
            PlayShootSound();
            CreateFireLight();
            CreateRecoil();
            Owner.CWR().GetScreenShake(FireShake);

            muzzleFlash = MuzzleFlashTime;
            muzzleSeed = Main.rand.NextFloat();
            heat = MathHelper.Clamp(heat + 0.3f, 0f, 1f);
            SpawnMuzzleParticles();

            if (Projectile.IsOwnedByLocalPlayer()) {
                LaunchOrdnance();
            }

            fireIndex++;
            ConsumeAmmo();
        }

        /// <summary>发射本次弹药，仅在弹幕主人端调用</summary>
        protected abstract void LaunchOrdnance();

        private void SpawnMuzzleParticles() {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 5; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.22f, 0.22f)) * Main.rand.NextFloat(4f, 11f);
                PRTLoader.NewParticle<PRT_Spark>(ShootPos, vel
                    , Color.Lerp(ThemeGlow, Color.White, Main.rand.NextFloat(0.4f)), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(true, Main.rand.Next(8, 14));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(ShootPos + dir * 6f
                    , dir.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f)) * Main.rand.NextFloat(1.5f, 3f)
                    , Color.DimGray, 0.08f)?.Configure(Main.rand.Next(18, 26), 0.35f, 0.02f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!OnHandheldDisplayBool) {
                return false;
            }
            GunDraw(Projectile.Center - Main.screenPosition + SpecialDrawPositionOffset, ref lightColor);
            DrawHeatGlow();
            return false;
        }

        //枪口炬走实体后层，Held内不宜批三明治
        void IPrimitiveDrawable.DrawPrimitives() {
            if (muzzleFlash <= 0 || !OnHandheldDisplayBool) {
                return;
            }
            float progress = 1f - muzzleFlash / (float)MuzzleFlashTime;
            HyperionTheme.DrawBlastQuad("MuzzleTech", ShootPos, MuzzleFlashSize
                , progress, 1f, muzzleSeed, Projectile.rotation.ToRotationVector2(), CrimsonTheme);
        }

        /// <summary>炮口余温，A=0加色</summary>
        private void DrawHeatGlow() {
            if (heat < 0.1f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float pulse = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f);
            Main.EntitySpriteDraw(glow, ShootPos - Main.screenPosition, null
                , ThemeGlow with { A = 0 } * (heat * 0.55f * pulse), 0f
                , glow.Size() / 2f, 0.42f + heat * 0.2f, SpriteEffects.None, 0);
        }
    }

    internal class HyperionBarrageHeld : BaseHyperionHeld
    {
        public override string Texture => CWRConstant.Item_Ranged + "HyperionBarrage";
        public override Asset<Texture2D> GlowAsset => HyperionBarrage.Glow;
        public override int TargetID => ModContent.ItemType<HyperionBarrage>();
        public override SoundStyle? ShootSound => SoundID.Item61 with { Volume = 0.6f, Pitch = 0.15f, PitchVariance = 0.1f };
        protected override bool CrimsonTheme => false;

        //每第四次扣动扳机改为垂直齐射，抬升开火节奏的段落感
        private bool SalvoShot => fireIndex % 4 == 3;
        protected override float FireShake => SalvoShot ? 2.2f : 1.2f;

        protected override void LaunchOrdnance() {
            if (!SalvoShot) {
                Projectile.NewProjectile(Source, ShootPos, Projectile.rotation.ToRotationVector2() * 6f
                    , ModContent.ProjectileType<HyperionCruiseMissile>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI, (int)HyperionCruiseMissile.LaunchMode.Direct);
                return;
            }

            //三枚跃升导弹扇形
            for (int i = 0; i < 3; i++) {
                Vector2 eject = (-Vector2.UnitY).RotatedBy((i - 1) * 0.36f) * 7.2f + Owner.velocity * 0.3f;
                Vector2 target = InMousePos + new Vector2((i - 1) * 30f, 0f);
                Projectile.NewProjectile(Source, Projectile.Center - Vector2.UnitY * 8f, eject
                    , ModContent.ProjectileType<HyperionCruiseMissile>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI
                    , (int)HyperionCruiseMissile.LaunchMode.Vertical, target.X, target.Y);
            }
        }
    }

    internal class HyperionBarrageEXHeld : BaseHyperionHeld
    {
        public override string Texture => CWRConstant.Item_Ranged + "HyperionBarrageEX";
        public override Asset<Texture2D> GlowAsset => HyperionBarrageEX.Glow;
        public override int TargetID => ModContent.ItemType<HyperionBarrageEX>();
        public override SoundStyle? ShootSound => SoundID.Item61 with { Volume = 0.75f, Pitch = -0.35f, PitchVariance = 0.08f };
        protected override bool CrimsonTheme => true;
        protected override float MuzzleFlashSize => 185f;
        protected override float FireShake => 3.2f;

        protected override void LaunchOrdnance() {
            Projectile.NewProjectile(Source, ShootPos, Projectile.rotation.ToRotationVector2() * 5f
                , ModContent.ProjectileType<HyperionCruiseMissile>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, (int)HyperionCruiseMissile.LaunchMode.Heavy);
        }
    }

    /// <summary>
    /// 弹出→点火→巡航，尾焰见<see cref="EffectLoader.HyperionExhaust"/>
    /// <para>ai[0]=发射模式（<see cref="LaunchMode"/>）；ai[1]/ai[2]=垂发模式的俯冲目标点</para>
    /// </summary>
    internal class HyperionCruiseMissile : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "DestroyerGrenade";

        internal enum LaunchMode
        {
            /// <summary>直射巡航</summary>
            Direct,
            /// <summary>垂直跃升后俯冲</summary>
            Vertical,
            /// <summary>集束子弹</summary>
            Cluster,
            /// <summary>EX重型，殉爆分裂</summary>
            Heavy
        }

        private const int TrailLen = 24;
        private Trail trail;

        private LaunchMode Mode => (LaunchMode)(int)Projectile.ai[0];
        private Vector2 DiveTarget => new(Projectile.ai[1], Projectile.ai[2]);
        private ref float Tick => ref Projectile.localAI[0];
        /// <summary>localAI1，集束=NPC索引+1，垂发1=熄锁</summary>
        private ref float TargetCache => ref Projectile.localAI[1];

        private bool IsHeavy => Mode == LaunchMode.Heavy;
        private bool Crimson => Mode is LaunchMode.Cluster or LaunchMode.Heavy;
        /// <summary>点火时刻（extraUpdates=1下×2），垂发错相</summary>
        private int IgniteTick => Mode switch {
            LaunchMode.Vertical => 30 + (int)(HyperionTheme.Hash01(Projectile.identity) * 20f),
            LaunchMode.Cluster => 8,
            LaunchMode.Heavy => 6,
            _ => 4,
        };
        private bool Ignited => Tick >= IgniteTick;
        private float MaxSpeed => Mode switch {
            LaunchMode.Heavy => 17f,
            LaunchMode.Cluster => 19f,
            _ => 23f,
        };
        private Color ThemeGlow => Crimson ? HyperionTheme.CrimsonGlow : HyperionTheme.AmberGlow;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //齐射与集束多弹并存，各弹独立命中互不占用免疫帧
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            float seedPhase = HyperionTheme.Hash01(Projectile.identity) * MathHelper.TwoPi;

            if (Tick == 0 && IsHeavy) {
                Projectile.scale = 1.3f;
                Projectile.Resize(22, 22);
            }

            if (!Ignited) {
                UpdateEjectPhase();
            }
            else {
                if (Tick == IgniteTick) {
                    IgnitionEffect();
                }
                UpdateCruisePhase(seedPhase);
            }

            //出膛/跃升免碰，离墙后恢复
            if (!Projectile.tileCollide && Ignited
                && !Framing.GetTileSafely(Projectile.Center).HasSolidTile()) {
                Projectile.tileCollide = true;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Ignited) {
                Lighting.AddLight(Projectile.Center, ThemeGlow.ToVector3() * (IsHeavy ? 0.75f : 0.5f));
            }
            SpawnFlightParticles();
            Tick++;
        }

        private void UpdateEjectPhase() {
            if (Mode == LaunchMode.Vertical) {
                //跃升滞空
                Projectile.velocity *= 0.955f;
                Projectile.velocity.Y += 0.12f;
            }
            else if (Mode == LaunchMode.Cluster) {
                Projectile.velocity *= 0.88f;
            }
            else {
                Projectile.velocity *= 0.985f;
                Projectile.velocity.Y += 0.05f;
            }
        }

        private void UpdateCruisePhase(float seedPhase) {
            int cruiseTick = (int)Tick - IgniteTick;

            if (Mode == LaunchMode.Vertical) {
                float distSQ = Projectile.DistanceSQ(DiveTarget);
                //俯冲制导，掠过熄锁
                if (distSQ < 40f * 40f) {
                    Projectile.Kill();
                    return;
                }
                if (distSQ < 90f * 90f) {
                    TargetCache = 1;
                }
                else if (TargetCache == 0) {
                    float turn = MathHelper.Lerp(0.17f, 0.04f, MathHelper.Clamp(cruiseTick / 40f, 0f, 1f));
                    Projectile.SmoothHomingBehavior(DiveTarget, 1f, turn);
                }
            }
            else if (Mode == LaunchMode.Cluster) {
                UpdateClusterHoming();
            }
            else {
                //微幅蛇行
                float weave = IsHeavy ? 0.006f : 0.011f;
                Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(Tick * 0.09f + seedPhase) * weave);
            }

            float speed = MathF.Min(Projectile.velocity.Length() * 1.045f + 0.18f, MaxSpeed);
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * speed;
        }

        private void UpdateClusterHoming() {
            //每6跳搜目标
            if ((int)Tick % 6 == 0) {
                NPC cached = TargetCache > 0 ? Main.npc[(int)TargetCache - 1] : null;
                if (!cached.Alives() || !cached.CanBeChasedBy(Projectile)) {
                    NPC found = Projectile.Center.FindClosestNPC(560f);
                    TargetCache = found == null ? 0 : found.whoAmI + 1;
                }
            }
            if (TargetCache > 0) {
                NPC target = Main.npc[(int)TargetCache - 1];
                if (target.Alives()) {
                    Projectile.SmoothHomingBehavior(target.Center, 1f, 0.09f);
                }
            }
        }

        private void IgnitionEffect() {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.3f, Pitch = 0.45f, PitchVariance = 0.15f }, Projectile.Center);
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + back * 10f
                    , back.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(3f, 7f)
                    , ThemeGlow, Main.rand.NextFloat(0.5f, 0.85f))?.Configure(false, Main.rand.Next(8, 13));
            }
        }

        private void SpawnFlightParticles() {
            if (Main.dedServ) {
                return;
            }
            Vector2 tail = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitY) * 14f * Projectile.scale;
            if (!Ignited) {
                if ((int)Tick % 4 == 0) {
                    PRTLoader.NewParticle<PRT_Smoke>(tail, -Projectile.velocity * 0.1f
                        , Color.DimGray, 0.06f)?.Configure(16, 0.25f, 0.015f);
                }
                return;
            }
            if ((int)Tick % 5 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(tail, -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.3f, 0.3f)
                    , Color.DimGray, IsHeavy ? 0.1f : 0.07f)?.Configure(Main.rand.Next(18, 26), 0.3f, 0.018f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(ModContent.BuffType<HellburnBuff>(), IsHeavy ? 120 : 60);

        public override void OnKill(int timeLeft) {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float boom = IsHeavy ? 1.9f : 1f;

            if (!Main.dedServ) {
                SoundEngine.PlaySound((IsHeavy ? SoundID.Item62 : SoundID.Item14)
                    with { Volume = IsHeavy ? 0.7f : 0.4f, Pitch = IsHeavy ? -0.1f : 0.3f, PitchVariance = 0.1f }, Projectile.Center);

                PRTLoader.NewParticle<PRT_MechExplosion>(Projectile.Center, Vector2.Zero, ThemeGlow, boom)
                    ?.Configure(IsHeavy ? 40 : 26, ThemeGlow);
                int sparkCount = IsHeavy ? 16 : 9;
                for (int i = 0; i < sparkCount; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(1f, 1f).SafeNormalize(Vector2.UnitX)
                        * Main.rand.NextFloat(3f, 9f) * boom - dir * 2f;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel
                        , Color.Lerp(ThemeGlow, Color.White, Main.rand.NextFloat(0.5f)), Main.rand.NextFloat(0.6f, 1.1f) * boom)
                        ?.Configure(true, Main.rand.Next(12, 20));
                }
                for (int i = 0; i < (IsHeavy ? 5 : 3); i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                        , Main.rand.NextVector2Circular(2f, 2f) - dir * 1.5f
                        , Color.DarkGray, 0.1f * boom)?.Configure(Main.rand.Next(24, 36), 0.4f, 0.02f);
                }
                if (Main.LocalPlayer.Alives() && Main.LocalPlayer.DistanceSQ(Projectile.Center) < 600f * 600f) {
                    Main.LocalPlayer.CWR().GetScreenShake(IsHeavy ? 3f : 1f);
                }
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            //殉爆AoE，直击不二次伤
            Projectile.penetrate = -1;
            Projectile.position = Projectile.Center;
            Projectile.width = Projectile.height = IsHeavy ? 150 : 80;
            Projectile.Center = Projectile.position;
            Projectile.Damage();

            //殉爆面片，ai0规模，负号猩红
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero
                , ModContent.ProjectileType<HyperionBlastProj>(), 0, 0, Owner: Projectile.owner
                , ai0: Crimson ? -boom : boom, ai1: dir.X, ai2: dir.Y);

            if (IsHeavy) {
                //六枚子弹环形
                int childDamage = (int)(Projectile.damage * 0.35f);
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = (MathHelper.TwoPi / 6f * i + 0.3f).ToRotationVector2() * 6.5f + Projectile.velocity * 0.15f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel
                        , Type, childDamage, Projectile.knockBack * 0.5f, Projectile.owner
                        , (int)LaunchMode.Cluster);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            //引擎喷口辉光，点火后随机闪烁
            if (Ignited) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Vector2 nozzle = drawPos - Projectile.velocity.SafeNormalize(Vector2.UnitY) * 12f * Projectile.scale;
                float flicker = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 40f + Projectile.whoAmI);
                Main.EntitySpriteDraw(glow, nozzle, null, ThemeGlow with { A = 0 } * (0.85f * flicker)
                    , 0f, glow.Size() / 2f, 0.3f * Projectile.scale * flicker, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, nozzle, null, Color.White with { A = 0 } * (0.55f * flicker)
                    , 0f, glow.Size() / 2f, 0.14f * Projectile.scale, SpriteEffects.None, 0);
            }

            Color bodyColor = Color.Lerp(lightColor, Color.White, 0.35f);
            Main.EntitySpriteDraw(tex, drawPos, null, bodyColor, Projectile.rotation
                , origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Tick < 2) {
                return;
            }
            Effect effect = EffectLoader.HyperionExhaust?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            //尾迹用oldPos，[^1]最新
            Vector2[] pts = new Vector2[TrailLen];
            trail ??= new Trail(pts, WidthFunc, ColorFunc);
            Vector2 lastValid = NozzlePos(Projectile.Center, Projectile.rotation);
            for (int i = TrailLen - 1; i >= 0; i--) {
                int k = TrailLen - 1 - i;
                if (k == 0) {
                    pts[i] = lastValid;
                    continue;
                }
                if (Projectile.oldPos[k] == Vector2.Zero) {
                    pts[i] = lastValid;
                    continue;
                }
                lastValid = NozzlePos(Projectile.oldPos[k] + Projectile.Size / 2f, Projectile.oldRot[k]);
                pts[i] = lastValid;
            }
            trail.TrailPositions = pts;

            float thrust = Ignited ? 1f : 0.15f;
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + HyperionTheme.Hash01(Projectile.identity) * 7f);
            effect.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(Tick / 8f, 0f, 1f));
            effect.Parameters["thrust"]?.SetValue(thrust);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            HyperionTheme.ApplyPalette(effect, Crimson);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(effect);
            device.BlendState = BlendState.AlphaBlend;
        }

        private Vector2 NozzlePos(Vector2 center, float rotation)
            => center - (rotation - MathHelper.PiOver2).ToRotationVector2() * 13f * Projectile.scale;

        private float WidthFunc(float factor)
            => (IsHeavy ? 11f : 7.5f) * Projectile.scale * (0.15f + 0.85f * MathF.Pow(factor, 1.4f));

        private Color ColorFunc(Vector2 uv) => Color.White;
    }

    /// <summary>
    /// <see cref="EffectLoader.HyperionBlast"/>面片，无伤害
    /// <para>ai[0]=规模倍率（负值切换猩红色盘）；ai[1]/ai[2]=入射方向单位向量</para>
    /// </summary>
    internal class HyperionBlastProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private const int LifeTime = 24;
        private ref float Timer => ref Projectile.localAI[0];
        private float BoomScale => MathF.Abs(Projectile.ai[0]);
        private bool Crimson => Projectile.ai[0] < 0;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
        }

        public override void AI() {
            Timer++;
            float inv = 1f - Timer / LifeTime;
            Vector3 light = (Crimson ? HyperionTheme.CrimsonSheath : HyperionTheme.AmberSheath) * inv * BoomScale;
            Lighting.AddLight(Projectile.Center, light);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            float progress = MathHelper.Clamp(Timer / LifeTime, 0f, 1f);
            float drawSize = (150f + progress * 70f) * BoomScale;
            HyperionTheme.DrawBlastQuad("BlastTech", Projectile.Center, drawSize
                , progress, 1f - progress * progress * 0.4f, HyperionTheme.Hash01(Projectile.whoAmI + 17f)
                , new Vector2(Projectile.ai[1], Projectile.ai[2]), Crimson);
        }
    }
}
