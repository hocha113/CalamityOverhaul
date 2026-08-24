using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// 寰宇灾厄长矛
    internal class CosmicCalamity : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "CosmicCalamity";

        /// 连击索引(后段更强)
        private int comboIndex;
        /// 连击重置计时
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
            if (CWRID.Item_CosmiliteBar > 0 && CWRID.Tile_CosmicAnvil > 0)
                CreateRecipe().AddIngredient(CWRID.Item_CosmiliteBar, 12).AddTile(CWRID.Tile_CosmicAnvil).Register();
        }
    }

    /// 寰宇灾厄手持弹幕，三段渐强刺击+顶点月牙冲击波
    internal class CosmicCalamityHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "CosmicCalamity";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<CosmicCalamity>();

        /// 连击阶段 0轻 1重 2终结
        private int ComboStage => (int)Projectile.ai[0] % 3;

        private bool IsFinisher => ComboStage == 2;

        //阶段时长（逻辑帧，受攻速缩放）
        private float WindupTime => 4f + ComboStage * 1.5f;
        private float StabTime => 7f + ComboStage;
        private float RecoverTime => 8f;
        private float TotalTime => WindupTime + StabTime + RecoverTime;
        //刺击顶点的突出距离，阶段越深扎得越远
        private float StabReach => 96f + ComboStage * 26f;
        //矛刃判定长度（从持握点向矛尖延伸）
        private const float BladeLength = 130f;

        /// 本次连击冲击波威力倍率
        private float WaveDamageMul => ComboStage switch {
            0 => 0.55f,
            1 => 0.75f,
            2 => 1.10f,
            _ => 0.55f
        };

        /// 宇宙紫蓝主色
        private static readonly Color BaseCore = new(220, 230, 255);
        private static readonly Color BaseMid = new(140, 90, 230);
        private static readonly Color BaseEdge = new(40, 14, 90);
        private static readonly Color BaseAccent = new(255, 90, 200);

        private float elapsed;
        private float speedMul = 1f;
        private Vector2 stabUnit;
        /// 当前持距
        private float holdout;
        private bool waveSpawned;
        private readonly HashSet<int> hitNPCs = [];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 60;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
            Projectile.CWR().PierceResist = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => elapsed >= WindupTime && elapsed <= WindupTime + StabTime + 1f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            Vector2 tip = hand + stabUnit * (holdout + BladeLength);
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , hand, tip, 34f, ref collisionPoint);
        }

        public override void Initialize() {
            stabUnit = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            Owner.direction = Math.Sign(stabUnit.X) == 0 ? Owner.direction : Math.Sign(stabUnit.X);

            speedMul = Owner.GetWeaponAttackSpeed(Item);
            if (speedMul <= 0f) {
                speedMul = 1f;
            }

            if (!VaultUtils.isServer) {
                SpawnChargeUpParticles();
            }
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<CosmicCalamity>()) {
                Projectile.Kill();
                return;
            }
            if (elapsed >= TotalTime) {
                Projectile.Kill();
                return;
            }

            float stabEnd = WindupTime + StabTime;

            if (elapsed < WindupTime) {
                //回拉蓄力
                float t = elapsed / WindupTime;
                holdout = MathHelper.Lerp(16f, -22f, MathF.Sin(t * MathHelper.PiOver2));
            }
            else if (elapsed < stabEnd) {
                //渐强突刺
                float t = (elapsed - WindupTime) / StabTime;
                float eased = 1f - MathF.Pow(1f - t, 3.8f + ComboStage * 0.3f);
                holdout = MathHelper.Lerp(-22f, StabReach, eased);

                if (!waveSpawned && t >= 0.45f) {
                    waveSpawned = true;
                    FireCrescentWave();
                }

                //刺击中段持续吐出能量粒子，像星屑撒下
                if (!VaultUtils.isServer && t < 0.75f) {
                    SpawnContinuousParticles();
                }
            }
            else {
                //收矛
                float t = (elapsed - stabEnd) / RecoverTime;
                holdout = MathHelper.Lerp(StabReach, 10f, t * t * (3f - 2f * t));
            }

            UpdatePlayerPose();

            //矛尖光照（轻柔的紫光）
            Vector2 lightAt = Owner.GetPlayerStabilityCenter() + stabUnit * (holdout + BladeLength * 0.85f);
            Lighting.AddLight(lightAt, 0.45f, 0.25f, 0.85f);

            //每两帧在矛尖位置喷出星屑
            if (!VaultUtils.isServer && elapsed % 2f < speedMul) {
                Vector2 tip = Owner.GetPlayerStabilityCenter() + stabUnit * (holdout + BladeLength * 0.9f);
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f) + stabUnit * Main.rand.NextFloat(0.5f, 1.8f);
                Color c = Color.Lerp(BaseCore, BaseMid, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Light>(tip + Main.rand.NextVector2Circular(4f, 4f), vel,
                    c, Main.rand.NextFloat(0.5f, 1f)).Configure(Main.rand.Next(10, 18), opacity: 0.4f, squishStrenght: 1.3f, _entity: Owner, _followingRateRatio: 0.6f);
            }

            elapsed += speedMul;
        }

        private void FireCrescentWave() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            int waveType = ModContent.ProjectileType<CosmicCrescentWave>();
            Vector2 spawnPos = Owner.GetPlayerStabilityCenter() + stabUnit * (holdout + BladeLength * 0.7f);
            //冲击波慢飞，月牙可读的演出时长
            float waveSpeed = 6.5f + ComboStage * 1.4f;
            int waveDamage = (int)(Projectile.damage * WaveDamageMul);

            Projectile.NewProjectile(
                Owner.GetSource_ItemUse(Item),
                spawnPos,
                stabUnit * waveSpeed,
                waveType,
                waveDamage,
                Projectile.knockBack * 0.8f,
                Owner.whoAmI,
                ai0: ComboStage,
                ai1: Main.rand.NextFloat(1000f)
            );

            //终结刺额外播放一次重音 + 屏幕震动反馈
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Volume = 0.55f, Pitch = -0.25f }, spawnPos);
                if (CWRClientConfig.Instance.ScreenVibration) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                        Owner.Center, stabUnit, 4.5f, 6f, 8, 500f, FullName));
                }
            }
        }

        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (stabUnit * Owner.direction).ToRotation();
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, stabUnit.ToRotation() - MathHelper.PiOver2);
            Projectile.Center = Owner.GetPlayerStabilityCenter() + stabUnit * (holdout + BladeLength * 0.5f);
            Projectile.rotation = stabUnit.ToRotation();
            Projectile.timeLeft = 60;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = stabUnit.X > 0 ? 1 : -1;
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.425f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //转发物品命中钩子，维持装备与饰品的近战联动
            if (hitNPCs.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }

            //命中粒子随连击
            if (VaultUtils.isServer) {
                return;
            }

            int sparkCount = 6 + ComboStage * 3;
            for (int i = 0; i < sparkCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(7f, 7f);
                Color sparkColor = Color.Lerp(BaseCore, BaseAccent, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f), vel, sparkColor * 0.9f, Main.rand.NextFloat(1.0f, 1.8f)).Configure(false, Main.rand.Next(10, 18), Owner);
            }

            //中圈光环 + 暗紫扩散
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    target.Center, Main.rand.NextVector2Circular(3f, 3f),
                    Color.Lerp(BaseMid, BaseCore, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.7f, 1.2f)).Configure(20, opacity: 0.4f, squishStrenght: 1.2f);
            }

            if (Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.NPCHit57 with { Volume = 0.35f, Pitch = 0.3f }, target.Center);
            }
        }

        private void SpawnChargeUpParticles() {
            Vector2 origin = Owner.Center + stabUnit * 30f;
            int count = 8 + ComboStage * 4;
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.1f, 0.1f);
                float radius = Main.rand.NextFloat(34f, 56f);
                Vector2 startPos = origin + angle.ToRotationVector2() * radius;
                Vector2 inVel = (origin - startPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2.5f, 4.5f);

                Color c = Color.Lerp(BaseCore, BaseMid, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_Spark>(startPos, inVel, c, Main.rand.NextFloat(0.9f, 1.4f)).Configure(false, Main.rand.Next(10, 16), Owner);
            }
        }

        private void SpawnContinuousParticles() {
            Vector2 tip = Owner.GetPlayerStabilityCenter() + stabUnit * (holdout + BladeLength * 0.85f);
            //深紫到亮蓝的两个粒子流
            if (Main.rand.NextBool(2)) {
                Vector2 vel = -stabUnit * Main.rand.NextFloat(1.2f, 3.4f)
                              + Main.rand.NextVector2Circular(1.6f, 1.6f);
                PRTLoader.NewParticle<PRT_Light>(tip + Main.rand.NextVector2Circular(6f, 6f), vel,
                    Color.Lerp(BaseMid, BaseEdge, Main.rand.NextFloat()) * 0.95f,
                    Main.rand.NextFloat(0.6f, 1.2f)).Configure(Main.rand.Next(15, 25), opacity: 0.35f, squishStrenght: 1.4f);
            }
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Light>(tip + Main.rand.NextVector2Circular(3f, 3f),
                    -stabUnit * 1.2f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Color.Lerp(BaseCore, BaseAccent, Main.rand.NextFloat()) * 0.85f,
                    Main.rand.NextFloat(0.5f, 0.9f)).Configure(Main.rand.Next(10, 18), opacity: 0.35f, squishStrenght: 1.2f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            Vector2 hand = Owner.GetPlayerStabilityCenter();
            //贴图矛尖指向右上，刺击时沿刺击方向旋转
            float rot = Projectile.rotation + MathHelper.PiOver4;
            SpriteEffects effect = SpriteEffects.None;
            if (Owner.direction < 0) {
                rot += MathHelper.PiOver2;
                effect = SpriteEffects.FlipHorizontally;
            }

            //突刺阶段的残影
            if (CanDamage() == true) {
                for (int i = 1; i <= 3; i++) {
                    float ghostHoldout = holdout - i * 16f;
                    if (ghostHoldout < -20f) {
                        continue;
                    }
                    Vector2 pos = hand + stabUnit * (ghostHoldout + BladeLength * 0.5f) - Main.screenPosition;
                    Color trailColor = BaseMid * (0.32f * (1f - i / 4f));
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(tex, pos, null, trailColor, rot, origin, Projectile.scale, effect, 0);
                }
            }

            //矛体本体
            Vector2 drawPos = hand + stabUnit * (holdout + BladeLength * 0.5f) - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor, rot, origin, Projectile.scale, effect, 0);

            //多层宇宙紫光晕，营造"能量饱和"的厚重感
            float pulse = 0.5f + 0.5f * MathF.Sin(elapsed * 0.35f + ComboStage);
            for (int i = 0; i < 4; i++) {
                float alpha = 0.30f - i * 0.06f;
                if (alpha <= 0f) {
                    continue;
                }
                float layerScale = Projectile.scale * (1f + i * 0.025f);
                Color glow = i switch {
                    0 => new Color(180, 110, 255, 0),
                    1 => new Color(120, 180, 255, 0),
                    2 => new Color(255, 90, 200, 0),
                    _ => new Color(220, 220, 255, 0)
                } * (alpha * (0.55f + pulse * 0.45f));
                Main.EntitySpriteDraw(tex, drawPos, null, glow, rot, origin, layerScale, effect, 0);
            }
            return false;
        }
    }

    /// 寰宇灾厄月牙冲击波
    internal class CosmicCrescentWave : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<CosmicCalamity>();

        /// 阶段 0轻 1重 2终结
        private int Stage => (int)Projectile.ai[0] % 3;
        /// 噪声种子
        private float Seed => Projectile.ai[1];

        private const int MaxLife = 60;
        /// 基础半径(×阶段倍率+生长曲线)
        private static readonly float[] StageBaseRadius = { 110f, 140f, 180f };
        /// 深度比(X半宽/Y半宽)
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
                //快速膨胀 EaseOutExpo
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

            //能量脉冲，贴图呼吸
            energyPulse = 0.5f + 0.5f * MathF.Sin(t * MathF.PI * (2.4f + Stage * 0.4f) + Seed * 0.13f);

            //飞行减速
            Projectile.velocity *= MathHelper.Lerp(0.985f, 0.94f, t);

            //朝向随速度
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

            //月牙命中椭圆近似
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

                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + dir * 12f, vel, c * 0.95f, Main.rand.NextFloat(0.9f, 1.6f)).Configure(false, Main.rand.Next(14, 22));
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

            PRTLoader.NewParticle<PRT_Light>(hornPos, vel,
                c * (0.8f + energyPulse * 0.2f),
                Main.rand.NextFloat(0.55f, 1.1f)).Configure(Main.rand.Next(14, 24), opacity: 0.35f, squishStrenght: 1.4f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Stage == 2 && Projectile.numHits <= 1 && CWRClientConfig.Instance.ScreenVibration) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    target.Center, dir, 3.5f, 4.5f, 6, 400f, FullName));
            }

            if (VaultUtils.isServer) {
                return;
            }

            //命中粒子向后炸
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 8 + Stage * 4; i++) {
                Vector2 vel = back.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(3f, 8f);
                Color c = Color.Lerp(new Color(220, 230, 255), new Color(255, 110, 210), Main.rand.NextFloat());

                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.9f, 1.5f)).Configure(false, Main.rand.Next(12, 22));
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

            //四边形片元，飞行为 X
            //顶点世界坐标，transformMatrix进裁剪空间
            Vector2 forward = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 side = new(-forward.Y, forward.X);
            float radius = CurrentRadius;
            float halfWidth = radius * DepthRatio; //沿飞行方向的半宽
            float halfHeight = radius * 1.15f;      //垂直方向的半宽（略大，给月牙"horns"留出空间）

            Vector2 center = Projectile.Center;

            //quad 四角 UV
            //"后下/后上" 是远离飞行方向那侧
            Vector2 backTop = center - forward * halfWidth - side * halfHeight;
            Vector2 frontTop = center + forward * halfWidth - side * halfHeight;
            Vector2 backBottom = center - forward * halfWidth + side * halfHeight;
            Vector2 frontBottom = center + forward * halfWidth + side * halfHeight;

            //TriangleStrip 四顶点
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
