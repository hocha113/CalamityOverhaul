using CalamityOverhaul.Common;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 寰宇灾厄长矛
    /// </summary>
    internal class CosmicCalamity : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "CosmicCalamity";

        /// <summary>
        /// 连击索引：在三段刺击之间循环，越靠后能量越强
        /// </summary>
        private static int comboIndex = 0;
        /// <summary>
        /// 连击重置计时器，超过这个时间没有再次攻击就重置连击
        /// </summary>
        private int comboResetTimer;

        public override void SetStaticDefaults() {
            ItemID.Sets.AnimatesAsSoul[Type] = true;
            Main.RegisterItemAnimation(Type, new DrawAnimationVertical(5, 1));
        }

        public override void SetDefaults() {
            Item.width = 96;
            Item.height = 96;
            Item.damage = 920;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useTurn = true;
            Item.channel = false;
            Item.useAnimation = 22;
            Item.useTime = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 9f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.value = Item.buyPrice(8, 25, 0, 0);
            Item.shoot = ModContent.ProjectileType<CosmicCalamityHeld>();
            Item.shootSpeed = 18f;
            Item.rare = CWRID.Rarity_CosmicPurple;
            Item.crit = 12;
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 10;

        public override bool CanUseItem(Player player) {
            //同时最多挂三段刺击，让连击丝滑而不卡帧
            return player.ownedProjectileCounts[Item.shoot] <= 2;
        }

        public override void HoldItem(Player player) {
            if (comboResetTimer > 0) {
                comboResetTimer--;
                if (comboResetTimer == 0) {
                    comboIndex = 0;
                }
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            //不同阶段不同音色，增强打击节奏感
            float pitch = -0.15f + comboIndex * 0.18f;
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.55f, Pitch = pitch }, player.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                Volume = 0.45f,
                Pitch = 0.1f + comboIndex * 0.12f
            }, player.Center);

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback
                , player.whoAmI, ai0: comboIndex);

            comboIndex = (comboIndex + 1) % 3;
            comboResetTimer = 75;
            return false;
        }

        public override void AddRecipes() {
            if (CWRRef.Has)
                CreateRecipe().AddIngredient(CWRID.Item_CosmiliteBar, 12).AddTile(CWRID.Tile_CosmicAnvil).Register();
        }
    }

    /// <summary>
    /// 寰宇灾厄长矛的持握弹幕
    /// </summary>
    internal class CosmicCalamityHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<CosmicCalamity>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "AstralBlade_Bar";

        /// <summary>
        /// 当前连击阶段：0=轻刺，1=重刺，2=终结刺
        /// </summary>
        private int comboStage;
        /// <summary>
        /// 是否已经生成过本次刺击对应的月牙冲击波
        /// </summary>
        private bool waveSpawned;

        /// <summary>
        /// 获取本次连击对应的冲击波威力倍率
        /// </summary>
        private float WaveDamageMul => comboStage switch {
            0 => 0.55f,
            1 => 0.75f,
            2 => 1.10f,
            _ => 0.55f
        };

        /// <summary>
        /// 三段连击共用的宇宙紫蓝色调，作为月牙能量的主色
        /// </summary>
        private static readonly Color BaseCore = new(220, 230, 255);
        private static readonly Color BaseMid = new(140, 90, 230);
        private static readonly Color BaseEdge = new(40, 14, 90);
        private static readonly Color BaseAccent = new(255, 90, 200);

        public override void SetKnifeProperty() {
            AnimationMaxFrme = 1;
            Projectile.width = Projectile.height = 96;
            canDrawSlashTrail = true;
            drawTrailHighlight = true;
            distanceToOwner = 26;
            drawTrailBtommWidth = 30;
            drawTrailTopWidth = 90;
            drawTrailCount = 12;
            Length = 78;
            Projectile.scale = 1f;
            ShootSpeed = 22f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8 * UpdateRate;
        }

        public override void KnifeInitialize() {
            comboStage = (int)Projectile.ai[0] % 3;
            waveSpawned = false;
        }

        public override bool PreSwingAI() {
            //不同阶段刺击参数：阶段越深、距离越长、生命越长
            float initialLen = 90f + comboStage * 18f;
            int lifetime = 22 + comboStage * 4;
            int maxLen = 220 + comboStage * 36;
            int minLen = 80 + comboStage * 10;
            float denom = 460f - comboStage * 50f;

            StabBehavior(
                initialLength: initialLen,
                lifetime: lifetime,
                scaleFactorDenominator: denom,
                minLength: minLen,
                maxLength: maxLen,
                canDrawSlashTrail: true
            );

            if (Time == 1) {
                SpawnChargeUpParticles();
            }

            //刺击中段持续吐出能量粒子，像星屑撒下
            if (Time % UpdateRate == 0 && Time < lifetime * UpdateRate * 0.7f && !VaultUtils.isServer) {
                SpawnContinuousParticles();
            }

            return false;
        }

        public override void Shoot() {
            if (waveSpawned) {
                return;
            }
            waveSpawned = true;

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            int waveType = ModContent.ProjectileType<CosmicCrescentWave>();
            Vector2 dir = ShootVelocity.SafeNormalize(Vector2.UnitX);
            Vector2 spawnPos = ShootSpanPos + dir * 14f;
            //冲击波本身飞行较慢，让"月牙"成为可被看到的表演而非一闪即逝
            float waveSpeed = 6.5f + comboStage * 1.4f;
            int waveDamage = (int)(Projectile.damage * WaveDamageMul);

            Projectile.NewProjectile(
                Source,
                spawnPos,
                dir * waveSpeed,
                waveType,
                waveDamage,
                Projectile.knockBack * 0.8f,
                Owner.whoAmI,
                ai0: comboStage,
                ai1: Main.rand.NextFloat(1000f)
            );

            //终结刺额外播放一次重音 + 屏幕震动反馈
            if (comboStage == 2) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.55f, Pitch = -0.25f }, ShootSpanPos);
                if (CWRServerConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        Owner.Center, dir, 4.5f, 6f, 8, 500f, FullName));
                }
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中粒子：颗粒大小受连击阶段影响
            if (VaultUtils.isServer) {
                return;
            }

            int sparkCount = 6 + comboStage * 3;
            for (int i = 0; i < sparkCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(7f, 7f);
                Color sparkColor = Color.Lerp(BaseCore, BaseAccent, Main.rand.NextFloat());
                BasePRT spark = new PRT_Spark(
                    target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f),
                    vel, false, Main.rand.Next(10, 18),
                    Main.rand.NextFloat(1.0f, 1.8f),
                    sparkColor * 0.9f, Owner);
                PRTLoader.AddParticle(spark);
            }

            //中圈光环 + 暗紫扩散
            for (int i = 0; i < 4; i++) {
                BasePRT halo = new PRT_Light(
                    target.Center, Main.rand.NextVector2Circular(3f, 3f),
                    Main.rand.NextFloat(0.7f, 1.2f),
                    Color.Lerp(BaseMid, BaseCore, Main.rand.NextFloat()),
                    20, 0.4f, 1.2f);
                PRTLoader.AddParticle(halo);
            }

            if (Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.NPCHit57 with { Volume = 0.35f, Pitch = 0.3f }, target.Center);
            }
        }

        public override void MeleeEffect() {
            //每两帧在矛尖位置喷出星屑
            if (Time % (2 * UpdateRate) == 0 && !VaultUtils.isServer) {
                Vector2 tip = Projectile.Center + safeInSwingUnit * Length * 0.95f;
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f) + safeInSwingUnit * Main.rand.NextFloat(0.5f, 1.8f);
                Color c = Color.Lerp(BaseCore, BaseMid, Main.rand.NextFloat());
                BasePRT dot = new PRT_Light(tip + Main.rand.NextVector2Circular(4f, 4f), vel,
                    Main.rand.NextFloat(0.5f, 1f), c, Main.rand.Next(10, 18), 0.4f, 1.3f, _entity: Owner, _followingRateRatio: 0.6f);
                PRTLoader.AddParticle(dot);
            }

            //矛尖光照（轻柔的紫光）
            Vector2 lightAt = Projectile.Center + safeInSwingUnit * Length * 0.8f;
            Lighting.AddLight(lightAt, 0.45f, 0.25f, 0.85f);
        }

        private void SpawnChargeUpParticles() {
            if (VaultUtils.isServer) {
                return;
            }

            Vector2 origin = Owner.Center + safeInSwingUnit * 30f;
            int count = 8 + comboStage * 4;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.1f, 0.1f);
                float radius = Main.rand.NextFloat(34f, 56f);
                Vector2 startPos = origin + angle.ToRotationVector2() * radius;
                Vector2 inVel = (origin - startPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2.5f, 4.5f);

                Color c = Color.Lerp(BaseCore, BaseMid, Main.rand.NextFloat());
                BasePRT spark = new PRT_Spark(startPos, inVel, false, Main.rand.Next(10, 16),
                    Main.rand.NextFloat(0.9f, 1.4f), c, Owner);
                PRTLoader.AddParticle(spark);
            }
        }

        private void SpawnContinuousParticles() {
            Vector2 tip = Projectile.Center + safeInSwingUnit * Length * 0.85f;
            //深紫到亮蓝的两个粒子流
            if (Main.rand.NextBool(2)) {
                Vector2 vel = -safeInSwingUnit * Main.rand.NextFloat(1.2f, 3.4f)
                              + Main.rand.NextVector2Circular(1.6f, 1.6f);
                BasePRT dust = new PRT_Light(tip + Main.rand.NextVector2Circular(6f, 6f), vel,
                    Main.rand.NextFloat(0.6f, 1.2f),
                    Color.Lerp(BaseMid, BaseEdge, Main.rand.NextFloat()) * 0.95f,
                    Main.rand.Next(15, 25), 0.35f, 1.4f);
                PRTLoader.AddParticle(dust);
            }
            if (Main.rand.NextBool(3)) {
                BasePRT halo = new PRT_Light(tip + Main.rand.NextVector2Circular(3f, 3f),
                    -safeInSwingUnit * 1.2f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Main.rand.NextFloat(0.5f, 0.9f),
                    Color.Lerp(BaseCore, BaseAccent, Main.rand.NextFloat()) * 0.85f,
                    Main.rand.Next(10, 18), 0.35f, 1.2f);
                PRTLoader.AddParticle(halo);
            }
        }

        public override void PostDrawSwing(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPos
            , Rectangle rectangle, Color color, float roting, Vector2 drawOrigin, float scale, SpriteEffects spriteEffects) {
            //多层宇宙紫光晕，营造"能量饱和"的厚重感
            float pulse = 0.5f + 0.5f * MathF.Sin(Time * 0.35f + comboStage);
            for (int i = 0; i < 4; i++) {
                float alpha = 0.30f - i * 0.06f;
                if (alpha <= 0f) {
                    continue;
                }
                float layerScale = scale * (1f + i * 0.025f);
                Color glow = i switch {
                    0 => new Color(180, 110, 255, 0),
                    1 => new Color(120, 180, 255, 0),
                    2 => new Color(255, 90, 200, 0),
                    _ => new Color(220, 220, 255, 0)
                } * (alpha * (0.55f + pulse * 0.45f));
                spriteBatch.Draw(texture, drawPos, rectangle, glow, roting, drawOrigin, layerScale, spriteEffects, 0);
            }
        }
    }

    /// <summary>
    /// 寰宇灾厄长矛的月牙能量冲击波
    /// </summary>
    internal class CosmicCrescentWave : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<CosmicCalamity>();

        /// <summary>
        /// 三档阶段：0=轻刺月牙、1=重刺月牙、2=终结月牙（更大更耀眼）
        /// </summary>
        private int Stage => (int)Projectile.ai[0] % 3;
        /// <summary>
        /// 为每个冲击波生成的独立噪声种子，避免视觉上完全重复
        /// </summary>
        private float Seed => Projectile.ai[1];

        private const int MaxLife = 60;
        /// <summary>
        /// 冲击波"基础半径"，会乘以阶段倍率 + 时间生长曲线
        /// </summary>
        private static readonly float[] StageBaseRadius = { 110f, 140f, 180f };
        /// <summary>
        /// 控制贴图深度（即 X 轴半宽 / Y 轴半宽 的比例）
        /// </summary>
        private const float DepthRatio = 1.05f;

        private float fadeAlpha;
        private float growProgress;
        private float energyPulse;

        public override void SetDefaults() {
            Projectile.width = 264;
            Projectile.height = 264;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        public override void AI() {
            float t = 1f - Projectile.timeLeft / (float)MaxLife;

            //生长 → 巡航 → 消散 三段曲线
            if (t < 0.25f) {
                //快速膨胀：EaseOutExpo
                float p = t / 0.25f;
                growProgress = 1f - MathF.Pow(2f, -8f * p);
                fadeAlpha = MathHelper.Lerp(0.4f, 1f, p);
            }
            else if (t < 0.7f) {
                growProgress = 1f;
                fadeAlpha = 1f;
            }
            else {
                float p = (t - 0.7f) / 0.3f;
                growProgress = 1f + p * 0.18f;
                //尾收非线性，越靠后越淡
                fadeAlpha = 1f - MathF.Pow(p, 1.5f);
            }

            //能量脉冲：用于让贴图本体随时间呼吸
            energyPulse = 0.5f + 0.5f * MathF.Sin(t * MathF.PI * (2.4f + Stage * 0.4f) + Seed * 0.13f);

            //飞行减速：随时间逐渐失去推力，让月牙"停下来再散开"
            Projectile.velocity *= MathHelper.Lerp(0.985f, 0.94f, t);

            //朝向：根据速度方向修正，避免反向飞行带来的画面错乱
            if (Projectile.velocity.LengthSquared() > 0.01f) {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            //光照效果
            float lightIntensity = fadeAlpha * (0.55f + energyPulse * 0.45f);
            Lighting.AddLight(Projectile.Center,
                0.55f * lightIntensity,
                0.30f * lightIntensity,
                0.95f * lightIntensity);

            //月牙生成时炸开一圈粒子，给观众一个"诞生"信号
            if (Projectile.timeLeft == MaxLife - 1 && !VaultUtils.isServer) {
                SpawnBirthParticles();
            }

            //飞行过程中持续吐出残影粒子
            if (Main.rand.NextBool(2 + Stage) && !VaultUtils.isServer) {
                SpawnTrailParticles();
            }
        }

        private float CurrentRadius => StageBaseRadius[Stage] * growProgress;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (fadeAlpha < 0.25f) {
                return false;
            }

            //用一个加宽的椭圆近似月牙的命中区域：横向更宽，纵向（沿飞行方向）较窄
            float radius = CurrentRadius;
            //horns 在前部，整体命中区域略向前突出
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 sampleCenter = Projectile.Center + forward * radius * 0.15f;

            Vector2 perp = new(-forward.Y, forward.X);
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            Vector2 toTarget = targetCenter - sampleCenter;

            //转换到月牙本地坐标
            float localX = Vector2.Dot(toTarget, forward); //沿前向
            float localY = Vector2.Dot(toTarget, perp);    //沿侧向

            float halfLen = radius * 0.55f;
            float halfWid = radius * 0.95f;
            //椭圆判定（命中域要稍大于绘制范围）
            float test = localX * localX / (halfLen * halfLen) + localY * localY / (halfWid * halfWid);
            return test < 1f;
        }

        private void SpawnBirthParticles() {
            //生成方向沿飞行轴向、稍微扇形展开的粒子爆发
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            int birthCount = 14 + Stage * 6;
            for (int i = 0; i < birthCount; i++) {
                float arc = MathHelper.Lerp(-1.05f, 1.05f, i / (float)(birthCount - 1));
                Vector2 dir = forward.RotatedBy(arc);
                Vector2 vel = dir * Main.rand.NextFloat(2.5f, 5.5f);
                Color c = i % 2 == 0
                    ? Color.Lerp(new Color(200, 230, 255), new Color(150, 100, 255), Main.rand.NextFloat())
                    : Color.Lerp(new Color(255, 100, 220), new Color(180, 80, 255), Main.rand.NextFloat());

                BasePRT spark = new PRT_Spark(
                    Projectile.Center + dir * 12f, vel, false,
                    Main.rand.Next(14, 22),
                    Main.rand.NextFloat(0.9f, 1.6f),
                    c * 0.95f);
                PRTLoader.AddParticle(spark);
            }
        }

        private void SpawnTrailParticles() {
            //在月牙的"horns"位置喷出星屑
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = new(-forward.Y, forward.X);
            float radius = CurrentRadius;
            float hornY = radius * 0.7f * (Main.rand.NextBool() ? 1f : -1f);
            Vector2 hornPos = Projectile.Center + forward * radius * 0.35f + perp * hornY;

            Vector2 vel = perp * Math.Sign(hornY) * Main.rand.NextFloat(0.6f, 2.2f);
            Color c = Color.Lerp(new Color(180, 140, 255), new Color(255, 110, 210), Main.rand.NextFloat());

            BasePRT dot = new PRT_Light(hornPos, vel,
                Main.rand.NextFloat(0.55f, 1.1f),
                c * (0.8f + energyPulse * 0.2f),
                Main.rand.Next(14, 24),
                0.35f, 1.4f);
            PRTLoader.AddParticle(dot);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Stage == 2 && Projectile.numHits <= 1 && CWRServerConfig.Instance.ScreenVibration) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, dir, 3.5f, 4.5f, 6, 400f, FullName));
            }

            if (VaultUtils.isServer) {
                return;
            }

            //命中粒子：向后炸开（与冲击方向相反），强化"被打飞"的视觉
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 8 + Stage * 4; i++) {
                Vector2 vel = back.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(3f, 8f);
                Color c = Color.Lerp(new Color(220, 230, 255), new Color(255, 110, 210), Main.rand.NextFloat());

                BasePRT spark = new PRT_Spark(target.Center, vel, false,
                    Main.rand.Next(12, 22), Main.rand.NextFloat(0.9f, 1.5f), c);
                PRTLoader.AddParticle(spark);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (fadeAlpha < 0.01f) {
                return;
            }

            Effect shader = EffectLoader.CosmicCrescent?.Value;
            if (shader == null) {
                return;
            }
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) {
                return;
            }

            //四边形片元：以飞行方向为 X 轴，正前方为 UV.x = 1
            //注意：顶点位置直接使用世界坐标，由 transformMatrix 转换至裁剪空间
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 side = new(-forward.Y, forward.X);
            float radius = CurrentRadius;
            float halfWidth = radius * DepthRatio; //沿飞行方向的半宽
            float halfHeight = radius * 1.15f;      //垂直方向的半宽（略大，给月牙"horns"留出空间）

            Vector2 center = Projectile.Center;

            //quad 四角：UV (0,0) 后上、(1,0) 前上、(0,1) 后下、(1,1) 前下
            //"后下/后上" 是远离飞行方向那侧
            Vector2 backTop = center - forward * halfWidth - side * halfHeight;
            Vector2 frontTop = center + forward * halfWidth - side * halfHeight;
            Vector2 backBottom = center - forward * halfWidth + side * halfHeight;
            Vector2 frontBottom = center + forward * halfWidth + side * halfHeight;

            //TriangleStrip 需要 4 个顶点：TL, TR, BL, BR
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(backTop, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(frontTop, 0f), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(backBottom, 0f), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(frontBottom, 0f), Color.White, new Vector2(1f, 1f));

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.025f);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(fadeAlpha, 0f, 1f));
            shader.Parameters["growProgress"]?.SetValue(MathHelper.Clamp(growProgress, 0f, 1.3f));
            shader.Parameters["energyPulse"]?.SetValue(energyPulse);
            shader.Parameters["seed"]?.SetValue(Seed);
            shader.Parameters["stage"]?.SetValue((float)Stage);
            shader.Parameters["coreColor"]?.SetValue(new Vector3(0.95f, 0.90f, 1.0f));
            shader.Parameters["midColor"]?.SetValue(new Vector3(0.55f, 0.40f, 1.00f));
            shader.Parameters["edgeColor"]?.SetValue(new Vector3(0.18f, 0.06f, 0.45f));
            shader.Parameters["accentColor"]?.SetValue(new Vector3(1.00f, 0.45f, 0.85f));
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            device.BlendState = BlendState.Additive;

            foreach (EffectPass pass in shader.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
        }
    }
}
