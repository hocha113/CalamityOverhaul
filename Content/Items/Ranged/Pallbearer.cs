using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Particles;
using CalamityMod.Projectiles;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    ///<summary>
    ///抬棺人
    ///</summary>
    internal class Pallbearer : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "Pallbearer";
        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 32;
            Item.damage = 666;
            Item.DamageType = DamageClass.Generic;
            Item.useTime = 45;
            Item.useAnimation = 45;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 6.5f;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = null;
            Item.autoReuse = false;
            Item.shoot = ModContent.ProjectileType<PallbearerHeld>();
            Item.shootSpeed = 15f;
            Item.useAmmo = AmmoID.Arrow;
            Item.channel = true; //允许持续按住
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            if (InWorldBossPhase.Level9) {
                damage *= 1.25f;
            }
            if (InWorldBossPhase.Level10) {
                damage *= 1.25f;
            }
            if (InWorldBossPhase.Level11) {
                damage *= 1.25f;
            }
            if (InWorldBossPhase.Level12) {
                damage *= 1.25f;
            }
        }

        public override bool CanUseItem(Player player) {
            //确保同时只有一个手持弹幕存在
            return player.ownedProjectileCounts[Item.shoot] == 0
                && player.ownedProjectileCounts[ModContent.ProjectileType<PallbearerBoomerang>()] == 0;
        }

        public override bool CanConsumeAmmo(Item ammo, Player player) {
            if (player.ownedProjectileCounts[Item.shoot] == 0) {
                return false;//是0说明正在发射手持弹幕本身，也不消耗弹药
            }
            return player.altFunctionUse != 2;//右键不消耗弹药
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //生成手持弹幕而不是直接射出箭矢
            Projectile.NewProjectile(source, position, velocity, Item.shoot, damage, knockback, player.whoAmI);
            return false;
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity
            , ref int type, ref int damage, ref float knockback) {
            //修正射击起始位置到玩家中心
            position = player.GetPlayerStabilityCenter();
        }
    }

    /// <summary>
    /// 抬棺人弩的手持弹幕，负责蓄力、装填和射击的动画逻辑
    /// </summary>
    internal class PallbearerHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Ranged + "PallbearerHeld";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<Pallbearer>()).DisplayName;
        //弩的状态机
        private enum CrossbowState
        {
            Idle,           //待机
            Loading,        //装填箭矢
            Charged,        //蓄力完成(正在蓄力)
            Firing,         //发射
            Throwing        //投掷弩本身 (未使用保留)
        }

        private CrossbowState State {
            get => (CrossbowState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StateTimer => ref Projectile.ai[1];
        private ref float ChargeLevel => ref Projectile.localAI[0]; //蓄力等级 0-1
        private ref float ThrowCooldown => ref Projectile.localAI[1]; //投掷冷却(实例内生效)

        //动画帧控制 (使用Projectile.frame)
        private float armRotation = 0f;

        //常量配置
        private const int LoadDuration = 35;        //装填时长
        private const int MaxChargeDuration = 60;   //最大蓄力时长
        private const int FireDuration = 15;        //射击动画时长
        private const int ThrowCooldownTime = 120;  //投掷后的冷却(仅右键)

        //弩弦相关
        private float bowstringPullback = 0f; //弓弦拉动进度

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = 4; //4帧动画：0待机 1加载过渡 2满弦 3射击回弹
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
            Projectile.hide = false;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (DownLeft) {
                Projectile.timeLeft = 60;
            }
            SetHeld();

            switch (State) {
                case CrossbowState.Idle:
                    HandleIdle();
                    break;
                case CrossbowState.Loading:
                    HandleLoading();
                    break;
                case CrossbowState.Charged:
                    HandleCharged();
                    break;
                case CrossbowState.Firing:
                    HandleFiring();
                    break;
            }

            UpdateOwnerArms();
            UpdatePositionAndRotation();
            StateTimer++;
        }

        private void HandleIdle() {
            Projectile.frame = 0;
            bowstringPullback = 0f;
            ChargeLevel = 0f;

            if (DownLeft && Owner.HasAmmo(Owner.ActiveItem())) {
                State = CrossbowState.Loading;
                StateTimer = 0;
                SoundEngine.PlaySound(SoundID.Item149, Owner.Center);
            }

            if (DownRight && ThrowCooldown <= 0) {
                ThrowCrossbow();
            }
        }

        private void HandleLoading() {
            float loadProgress = StateTimer / LoadDuration;
            Projectile.frame = loadProgress < 0.5f ? 0 : 1;
            bowstringPullback = MathHelper.SmoothStep(0f, 1f, loadProgress);

            if (StateTimer % 8 == 0 && !Main.dedServ) {
                Vector2 dustPos = Projectile.Center + Projectile.velocity * 20f;
                for (int i = 0; i < 3; i++) {
                    Dust dust = Dust.NewDustPerfect(dustPos, DustID.Smoke,
                        Main.rand.NextVector2Circular(2f, 2f), 100, default, 1.2f);
                    dust.noGravity = true;
                }
            }

            if (StateTimer >= LoadDuration) {
                State = CrossbowState.Charged;
                StateTimer = 0;
                ChargeLevel = 0f;
                Projectile.frame = 2;
                SoundEngine.PlaySound(SoundID.Item102 with { Pitch = -0.3f }, Owner.Center);
                SpawnLoadCompleteEffect();
            }

            if (!DownLeft) { //取消
                State = CrossbowState.Idle;
                StateTimer = 0;
            }
        }

        private void HandleCharged() {
            Projectile.frame = 2;
            bowstringPullback = 1f;

            if (DownLeft && StateTimer < MaxChargeDuration) {
                ChargeLevel = StateTimer / MaxChargeDuration;
                if (StateTimer % 5 == 0) {
                    SpawnChargeParticle();
                }
                if (StateTimer % 15 == 0) {
                    SoundEngine.PlaySound(SoundID.Item149 with {
                        Volume = 0.3f,
                        Pitch = ChargeLevel * 0.5f
                    }, Owner.Center);
                }
            }

            if (!DownLeft || StateTimer >= MaxChargeDuration) {
                State = CrossbowState.Firing;
                StateTimer = 0;
                FireArrow();
            }
        }

        private void HandleFiring() {
            Projectile.frame = 3;
            float fireProgress = StateTimer / FireDuration;
            bowstringPullback = 1f - fireProgress;

            if (StateTimer >= FireDuration) {
                //直接回到 Idle，保证循环顺滑（移除随机投掷导致的不稳定节奏）
                State = CrossbowState.Idle;
                StateTimer = 0;
                ChargeLevel = 0f;
            }
        }

        private void FireArrow() {
            if (!Projectile.IsOwnedByLocalPlayer())
                return;

            Owner.PickAmmo(Owner.ActiveItem(), out int projToShoot, out float speed,
                out int damage, out float knockback, out int usedAmmoItemId, false);

            int mult = Owner.GetModPlayer<CWRPlayer>().PallbearerNextArrowDamageMult;
            float damageMultiplier = (1f + ChargeLevel * 1.5f) * mult; //最高250% * 触发加成
            int finalDamage = (int)(damage * damageMultiplier);

            Vector2 shootVelocity = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction) * (speed + ChargeLevel * 5f);
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center + Projectile.velocity * 30f,
                shootVelocity,
                ModContent.ProjectileType<PallbearerArrow>(),
                finalDamage,
                knockback * (1f + ChargeLevel * 0.5f),
                Owner.whoAmI,
                ChargeLevel
            );

            //Reset multiplier after consumption
            if (mult > 1) {
                Owner.GetModPlayer<CWRPlayer>().PallbearerNextArrowDamageMult = 1;
                //反馈特效
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 16; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, Main.rand.NextVector2Circular(4f, 4f), 150, Color.OrangeRed, 1.4f);
                        d.noGravity = true;
                    }
                }
            }

            SoundEngine.PlaySound(SoundID.DD2_BallistaTowerShot with {
                Volume = 0.8f + ChargeLevel * 0.4f,
                Pitch = -0.1f + ChargeLevel * 0.2f
            }, Projectile.Center);

            SpawnFireEffect();
        }

        private void ThrowCrossbow() {
            if (!Projectile.IsOwnedByLocalPlayer())
                return;

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction) * 14f,
                ModContent.ProjectileType<PallbearerBoomerang>(),
                (int)(Projectile.damage * 0.8f),
                Projectile.knockBack * 1.2f,
                Owner.whoAmI
            );

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f }, Projectile.Center);
            ThrowCooldown = ThrowCooldownTime;
            Projectile.Kill();
        }

        private void UpdateOwnerArms() {
            int dir = Owner.direction;
            float targetArmRot = Projectile.rotation;
            if (dir < 0) {
                targetArmRot -= MathHelper.PiOver2;
            }
            else {
                targetArmRot -= MathHelper.ToRadians(60);
            }

            switch (State) {
                case CrossbowState.Loading:
                    armRotation = MathHelper.Lerp(armRotation, targetArmRot - 0.5f * dir, 0.15f);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, targetArmRot);
                    break;
                case CrossbowState.Charged:
                    float vibration = (float)Math.Sin(StateTimer * 0.3f) * 0.03f;
                    armRotation = targetArmRot - 0.6f * dir + vibration;
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, targetArmRot);
                    break;
                case CrossbowState.Firing:
                    armRotation = MathHelper.Lerp(armRotation, targetArmRot, 0.4f);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, targetArmRot);
                    break;
                default:
                    armRotation = MathHelper.Lerp(armRotation, targetArmRot, 0.2f);
                    Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Quarter, armRotation);
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, targetArmRot);
                    break;
            }
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        private void UpdatePositionAndRotation() {
            Vector2 ownerCenter = Owner.GetPlayerStabilityCenter();
            Vector2 aimDir = (Main.MouseWorld - ownerCenter).SafeNormalize(Vector2.UnitX * Owner.direction);
            Projectile.velocity = aimDir; //稳定的方向向量

            float holdDistance = 20f + ((State == CrossbowState.Loading || State == CrossbowState.Charged) ? bowstringPullback * 8f : 0f);
            Projectile.Center = ownerCenter + aimDir * holdDistance;
            Projectile.rotation = aimDir.ToRotation();

            Owner.ChangeDir(aimDir.X > 0 ? 1 : -1);
            Owner.itemRotation = Projectile.rotation * Owner.direction;

            if (ThrowCooldown > 0) ThrowCooldown--;
        }

        private void SpawnLoadCompleteEffect() {
            if (Main.dedServ) return;
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Projectile.rotation.ToRotationVector2() * 32, DustID.RedTorch, velocity, 100, Color.Cyan, 1.5f);
                dust.noGravity = true;
            }
        }

        private void SpawnChargeParticle() {
            if (Main.dedServ) return;
            Color chargeColor = Color.Lerp(Color.Yellow, Color.OrangeRed, ChargeLevel);
            Vector2 particlePos = Projectile.Center + Projectile.rotation.ToRotationVector2() * 32 + Main.rand.NextVector2Circular(15f, 15f);
            Vector2 particleVel = (Projectile.Center - particlePos).SafeNormalize(Vector2.Zero) * 2f;
            Dust charge = Dust.NewDustPerfect(particlePos, DustID.RedTorch, particleVel, 100, chargeColor, 1.2f);
            charge.noGravity = true;
            charge.fadeIn = 1.2f;
        }

        private void SpawnFireEffect() {
            if (Main.dedServ) return;
            Vector2 muzzlePos = Projectile.Center + Projectile.velocity * 30f;
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Projectile.velocity.RotatedByRandom(0.4f) * Main.rand.NextFloat(2f, 8f);
                Dust spark = Dust.NewDustPerfect(muzzlePos, DustID.Torch, velocity, 100, Color.OrangeRed, 1.8f);
                spark.noGravity = true;
            }
            for (int i = 0; i < 8; i++) {
                Dust smoke = Dust.NewDustPerfect(muzzlePos, DustID.Smoke,
                    Projectile.velocity.RotatedByRandom(0.2f) * Main.rand.NextFloat(1f, 3f), 100, default, 2f);
                smoke.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle frame = texture.Frame(1, Main.projFrames[Type], 0, Projectile.frame);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = frame.Size() / 2f;
            SpriteEffects fx = Owner.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            Main.EntitySpriteDraw(texture, drawPos, frame, lightColor, Projectile.rotation, origin, Projectile.scale, fx, 0);

            if (State == CrossbowState.Charged && ChargeLevel > 0.3f) {
                Color glowColor = Color.Lerp(Color.Yellow, Color.Red, ChargeLevel) * (0.4f + ChargeLevel * 0.6f);
                Main.EntitySpriteDraw(texture, drawPos, frame, glowColor * 0.5f,
                        Projectile.rotation, origin, Projectile.scale, fx, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 抬棺人弩被投掷后的回旋镖形态，会旋转攻击敌人后返回玩家手中
    /// </summary>
    internal class PallbearerBoomerang : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Ranged + "Pallbearer";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<Pallbearer>()).DisplayName;
        private enum BoomerangState { Throwing, Returning }
        private BoomerangState State { get => (BoomerangState)Projectile.ai[0]; set { if (Projectile.ai[0] != (float)value) { Projectile.ai[0] = (float)value; Projectile.netUpdate = true; } } }
        private ref float Time => ref Projectile.ai[1];
        private ref float ReturnProgress => ref Projectile.ai[2]; //修正：使用 ai[2] 而不是越界的 localAI[2]
        private ref float SpinSpeed => ref Projectile.localAI[0];
        private ref float ThrowProgress => ref Projectile.localAI[1]; //0-1 飞出阶段进度

        //基础参数
        private const int LaunchPhaseFrames = 14;      //初始爆发加速帧数
        private const int TotalThrowFrames = 48;       //前向阶段总帧数（含爆发）
        private const float MaxDistance = 1000f;       //最大距离
        private const float BaseSpeed = 32f;           //基础巡航速度
        private const float PeakLaunchSpeed = 52f;     //初始爆发峰值
        private const float ReturnMaxSpeed = 58f;      //回程最大速度
        private const float ArcAmplitude = 120f;       //外抛弧线幅度
        private bool PlayedMidWhoosh => (Projectile.miscText?.Length ?? 0) > 0; //复用一个简单标记

        //缓动函数
        private static float EaseOutExpo(float t) => t >= 1f ? 1f : 1f - (float)Math.Pow(2, -10 * t);
        private static float EaseOutCubic(float t) { t = MathHelper.Clamp(t, 0, 1); t = 1 - (float)Math.Pow(1 - t, 3); return t; }
        private static float EaseInQuad(float t) => t * t;
        private static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        private static float EaseInOutBack(float t) {
            const float c1 = 1.70158f; const float c2 = c1 * 1.525f; t = MathHelper.Clamp(t, 0, 1);
            return t < 0.5f ? (float)(Math.Pow(2 * t, 2) * ((c2 + 1) * 2 * t - c2)) / 2f
                             : (float)(Math.Pow(2 * t - 2, 2) * ((c2 + 1) * (t * 2 - 2) + c2) + 2) / 2f;
        }
        private static float EaseOvershootSnap(float t) { //回程末端吸附 + 轻微回拉
            t = MathHelper.Clamp(t, 0, 1);
            float overshoot = 1.08f - (float)Math.Cos(t * MathHelper.Pi) * 0.08f; //前段轻超出
            return overshoot * (0.85f + 0.15f * EaseOutQuad(t));
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) { Projectile.Kill(); return; }

            Time++;
            Vector2 playerCenter = owner.GetPlayerStabilityCenter();
            Vector2 toMouseDir = (Main.MouseWorld - playerCenter).SafeNormalize(Vector2.UnitX * owner.direction);
            Vector2 centerToPlayer = playerCenter - Projectile.Center;

            if (State == BoomerangState.Throwing) {
                //进度（整体）
                ThrowProgress = MathHelper.Clamp(ThrowProgress + 1f / TotalThrowFrames, 0f, 1f);

                //阶段划分：爆发 -> 滑行延伸 -> 减速准备回程
                float launchT = MathHelper.Clamp(Time / (float)LaunchPhaseFrames, 0f, 1f);
                float launchSpeedFactor = EaseOutCubic(launchT); //爆发上升
                float currentLaunchSpeed = MathHelper.Lerp(BaseSpeed, PeakLaunchSpeed, launchSpeedFactor);

                float cruisePhase = MathHelper.Clamp((Time - LaunchPhaseFrames) / (TotalThrowFrames - LaunchPhaseFrames), 0f, 1f);
                float cruiseEase = EaseOutQuad(cruisePhase);
                float distanceFactor = EaseOutExpo(ThrowProgress);

                //弧线偏移：以到鼠标方向法线做侧向偏移（力量感：外抛弧）
                Vector2 lateral = toMouseDir.RotatedBy(MathHelper.PiOver2 * owner.direction);
                float arc = (float)Math.Sin(distanceFactor * MathHelper.Pi) * ArcAmplitude * (1f - cruisePhase * 0.65f);
                Vector2 targetPos = playerCenter + toMouseDir * (distanceFactor * MaxDistance) + lateral * arc;

                Vector2 desiredVel = (targetPos - Projectile.Center).SafeNormalize(toMouseDir) * currentLaunchSpeed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.18f + 0.1f * (1f - cruisePhase));

                //到顶或时间结束进入回程
                if (ThrowProgress >= 1f || Vector2.Distance(playerCenter, Projectile.Center) > MaxDistance * 0.97f) {
                    State = BoomerangState.Returning;
                    ReturnProgress = 0f;
                    Time = 0f;
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.35f, Volume = 1f }, Projectile.Center);
                }
            }
            else { //Returning
                ReturnProgress = MathHelper.Clamp(ReturnProgress + 0.022f, 0f, 1f);
                float ease = EaseInQuad(ReturnProgress) * 0.35f + EaseInOutBack(ReturnProgress) * 0.65f;
                float speed = MathHelper.Lerp(BaseSpeed * 0.5f, ReturnMaxSpeed, ease);
                Vector2 desiredVel = centerToPlayer.SafeNormalize(Vector2.Zero) * speed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.30f + 0.25f * (1f - ReturnProgress));

                //吸附末端加速收手
                if (ReturnProgress > 0.85f) {
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, centerToPlayer.SafeNormalize(Vector2.Zero) * ReturnMaxSpeed, 0.45f);
                }

                if (centerToPlayer.Length() < 54f) {
                    owner.GetModPlayer<CWRPlayer>().GetScreenShake(4f);
                    SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.9f, Pitch = 0.15f }, owner.Center);
                    Projectile.Kill();
                    return;
                }
            }

            //旋转与屏幕震动基于速度
            float velLen = Projectile.velocity.Length();
            float targetSpin = 0.15f + velLen / 90f; //更高速更快旋转
            SpinSpeed = MathHelper.Lerp(SpinSpeed, targetSpin, 0.15f);
            Projectile.rotation += SpinSpeed * Math.Sign(Projectile.velocity.X == 0 ? owner.direction : Projectile.velocity.X);

            //中程呼啸音效
            if (!PlayedMidWhoosh && ThrowProgress > 0.55f && State == BoomerangState.Throwing) {
                Projectile.miscText = "x"; //标记已播放
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.55f, Pitch = 0.6f }, Projectile.Center);
            }

            //回程尾声掠过音效
            if (State == BoomerangState.Returning && ReturnProgress > 0.6f && (Time % 18 == 0)) {
                SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.35f, Pitch = 0.4f }, Projectile.Center);
            }

            //装饰性高速离心粒子
            if (!Main.dedServ && Time % 2 == 0) {
                SpawnSpinParticle();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) { //corrected signature
            Player owner = Main.player[Projectile.owner];
            owner.GetModPlayer<CWRPlayer>().PallbearerNextArrowDamageMult = 2;
            if (State == BoomerangState.Throwing) {
                State = BoomerangState.Returning;
                ReturnProgress = 0f;
                Time = 0f;
                Projectile.velocity *= 0.75f;
            }
            else {
                Projectile.velocity *= 1.15f;
            }
            SpawnHitEffect(target);
        }

        private void SpawnHitEffect(NPC target) {
            if (Main.dedServ)
                return;

            //旋转斩击效果
            int sparkCount = 12;
            for (int i = 0; i < sparkCount; i++) {
                float angle = MathHelper.TwoPi * i / sparkCount + Projectile.rotation;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);

                Dust spark = Dust.NewDustPerfect(target.Center, DustID.Electric,
                    velocity, 100, Color.Cyan, 1.8f);
                spark.noGravity = true;
            }

            //冲击波
            for (int i = 0; i < 3; i++) {
                Dust shockwave = Dust.NewDustPerfect(target.Center, DustID.Cloud,
                    Main.rand.NextVector2Circular(5f, 5f), 100, Color.White, 2f);
                shockwave.noGravity = true;
            }
        }

        private void SpawnSpinParticle() {
            if (Main.dedServ)
                return;

            //旋转产生的风暴粒子
            for (int i = 0; i < 2; i++) {
                float angle = Projectile.rotation + MathHelper.PiOver2 * i;
                Vector2 offset = angle.ToRotationVector2() * 30f;
                Vector2 velocity = offset.RotatedBy(MathHelper.PiOver2) * 0.5f;

                Dust wind = Dust.NewDustPerfect(Projectile.Center + offset, DustID.Cloud,
                    velocity, 100, Color.LightCyan, 1.5f);
                wind.noGravity = true;
                wind.fadeIn = 1.1f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Item[ModContent.ItemType<Items.Ranged.Pallbearer>()].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            float scale = Projectile.scale;
            float speedFactor = Math.Clamp(Projectile.velocity.Length() / ReturnMaxSpeed, 0f, 1f);

            //Trail（速度越快 & 越靠近回程末端越亮）
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = i / (float)Projectile.oldPos.Length;
                float fade = (1f - progress) * (0.35f + 0.65f * speedFactor);
                if (State == BoomerangState.Returning) fade *= 1.1f;
                Color trailColor = Color.Cyan * fade;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailRot = Projectile.oldRot[i];
                Main.EntitySpriteDraw(texture, trailPos, null, trailColor, trailRot, origin, scale * (1f - progress * 0.12f), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0);

            //能量环（速度驱动 + 回程加强）
            float pulse = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f) * 0.08f;
            float ringScale = scale * (1.12f + 0.25f * speedFactor) * pulse;
            Color ringColor = (State == BoomerangState.Returning ? Color.Cyan : Color.LightCyan) * (0.4f + 0.5f * speedFactor);
            Main.EntitySpriteDraw(texture, drawPos, null, ringColor, Projectile.rotation + MathHelper.PiOver4, origin, ringScale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 抬棺人弩发射的强力箭矢
    /// </summary>
    internal class PallbearerArrow : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Ranged + "PallbearerArrow";

        private ref float ChargeLevel => ref Projectile.ai[0];  //蓄力等级
        private ref float Time => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 15;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 5 + (int)(ChargeLevel * 5); //最多10次穿透
            Projectile.timeLeft = 600;
            Projectile.arrow = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = (int)(1 + ChargeLevel);
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.Calamity().pointBlankShotDuration = CalamityGlobalProjectile.DefaultPointBlankDuration;
        }

        public override void AI() {
            Time++;

            Projectile.penetrate = 5 + (int)(ChargeLevel * 5); //最多10次穿透
            Projectile.extraUpdates = (int)(0 + ChargeLevel * 5);
            //旋转
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //轻微的追踪效果（蓄力越高追踪越强）
            if (ChargeLevel > 0.5f) {
                NPC target = Projectile.Center.FindClosestNPC(600f + ChargeLevel * 400f);
                if (target != null && Projectile.velocity.Length() > 2f) {
                    float homingStrength = 0.02f + ChargeLevel * 0.05f;
                    Projectile.velocity = Vector2.Lerp(
                        Projectile.velocity,
                        Projectile.DirectionTo(target.Center) * Projectile.velocity.Length(),
                        homingStrength
                    );
                }
            }

            //缩放脉冲效果
            Projectile.scale = 1f + (float)Math.Sin(Time * 0.2f) * 0.1f * ChargeLevel;

            //光照
            float lightIntensity = 0.5f + ChargeLevel * 0.8f;
            Color lightColor = Color.Lerp(Color.Yellow, Color.OrangeRed, ChargeLevel);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * lightIntensity);

            //粒子拖尾
            if (Time % 3 == 0) {
                SpawnTrailParticle();
            }

            //满蓄力时每隔一段时间释放能量环
            if (ChargeLevel >= 0.9f && Time % 20 == 0) {
                SpawnEnergyRing();
            }
        }

        private void SpawnTrailParticle() {
            if (Main.dedServ)
                return;

            Color particleColor = Color.Lerp(Color.Yellow, Color.Red, ChargeLevel);
            Vector2 particlePos = Projectile.Center - Projectile.velocity * 0.5f;

            Dust trail = Dust.NewDustPerfect(particlePos, DustID.Torch,
                -Projectile.velocity * 0.3f, 100, particleColor, 1.5f + ChargeLevel * 0.5f);
            trail.noGravity = true;
            trail.fadeIn = 1.2f;
        }

        private void SpawnEnergyRing() {
            if (Main.dedServ)
                return;

            int dustCount = 12;
            for (int i = 0; i < dustCount; i++) {
                float angle = MathHelper.TwoPi * i / dustCount;
                Vector2 offset = angle.ToRotationVector2() * 15f;
                Vector2 velocity = offset * 0.3f;

                Dust energy = Dust.NewDustPerfect(Projectile.Center + offset, DustID.Electric,
                    velocity, 100, Color.OrangeRed, 1.8f);
                energy.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //播放击中音效
            SoundEngine.PlaySound(SoundID.NPCHit53 with {
                Volume = 0.6f + ChargeLevel * 0.4f,
                Pitch = -0.2f + ChargeLevel * 0.3f
            }, Projectile.Center);

            //击中特效
            SpawnHitEffect(target);

            //蓄力越高，特殊效果越强
            if (ChargeLevel > 0.6f) {
                //高蓄力造成额外的小范围AOE
                if (Main.rand.NextBool(2)) {
                    CreateMiniExplosion(target);
                }
            }

            //满蓄力时有几率生成追踪箭矢
            if (ChargeLevel >= 0.9f && Main.rand.NextBool(3) && Projectile.IsOwnedByLocalPlayer()) {
                SpawnHomingArrow(target);
            }
        }

        private void SpawnHitEffect(NPC target) {
            if (Main.dedServ)
                return;

            //冲击火花
            int sparkCount = 8 + (int)(ChargeLevel * 12);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Color sparkColor = Color.Lerp(Color.Yellow, Color.Red, Main.rand.NextFloat());

                Dust spark = Dust.NewDustPerfect(target.Center, DustID.Torch, velocity,
                    100, sparkColor, 1.5f + ChargeLevel);
                spark.noGravity = true;
            }

            //能量波纹
            if (ChargeLevel > 0.5f) {
                for (int i = 0; i < 3; i++) {
                    LineParticle line = new LineParticle(
                        target.Center,
                        Main.rand.NextVector2Circular(8f, 8f),
                        false,
                        (int)(15 + ChargeLevel * 10),
                        1.2f + ChargeLevel * 0.8f,
                        Color.OrangeRed
                    );
                    GeneralParticleHandler.SpawnParticle(line);
                }
            }
        }

        private void CreateMiniExplosion(NPC target) {
            if (Main.dedServ)
                return;

            //小型AOE伤害
            float explosionRadius = 80f + ChargeLevel * 60f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == target.whoAmI || !npc.CanBeChasedBy())
                    continue;

                float distance = Vector2.Distance(npc.Center, target.Center);
                if (distance < explosionRadius) {
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        int aoeGamage = (int)(Projectile.damage * 0.4f * (1f - distance / explosionRadius));
                        npc.SimpleStrikeNPC(aoeGamage, Math.Sign(npc.Center.X - target.Center.X),
                            false, 0f, null, false, 0f, true);
                    }
                }
            }

            //爆炸特效
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust explosion = Dust.NewDustPerfect(target.Center, DustID.Torch,
                    velocity, 100, Color.OrangeRed, 2f);
                explosion.noGravity = true;
            }

            //爆炸音效
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, target.Center);
        }

        private void SpawnHomingArrow(NPC target) {
            //在周围生成1-2支小型追踪箭
            int arrowCount = Main.rand.Next(1, 3);
            for (int i = 0; i < arrowCount; i++) {
                Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(100f, 100f);
                Vector2 velocity = (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * 12f;

                int arrow = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    velocity,
                    Type,
                    Projectile.damage / 3,
                    Projectile.knockBack * 0.5f,
                    Projectile.owner,
                    0.7f //中等蓄力等级
                );

                if (arrow >= 0 && arrow < Main.maxProjectiles) {
                    Main.projectile[arrow].timeLeft = 180;
                    Main.projectile[arrow].scale = 0.8f;
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //死亡时的烟雾效果
            if (Main.dedServ)
                return;

            for (int i = 0; i < 10; i++) {
                Dust smoke = Dust.NewDustDirect(Projectile.position, Projectile.width,
                    Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 1.5f);
                smoke.velocity = Main.rand.NextVector2Circular(2f, 2f);
                smoke.noGravity = true;
            }

            //高蓄力箭矢死亡时额外的能量释放
            if (ChargeLevel > 0.7f) {
                for (int i = 0; i < 15; i++) {
                    Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                    Dust energy = Dust.NewDustPerfect(Projectile.Center, DustID.Electric,
                        velocity, 100, Color.OrangeRed, 2f);
                    energy.noGravity = true;
                }
            }
        }

        public override Color? GetAlpha(Color lightColor) {
            //根据蓄力等级调整颜色
            Color baseColor = Color.Lerp(Color.White, Color.Orange, ChargeLevel * 0.6f);
            return baseColor * Projectile.Opacity;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Time <= 2)
                return false;

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;

            //绘制残影
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = i / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Lerp(Color.Yellow, Color.Red, ChargeLevel) * (1f - progress) * 0.5f;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailRot = Projectile.oldRot[i];

                Main.EntitySpriteDraw(texture, trailPos, null, trailColor,
                    trailRot, origin, Projectile.scale * (1f - progress * 0.3f),
                    SpriteEffects.None, 0);
            }

            //绘制主体
            Main.EntitySpriteDraw(texture, drawPos, null, Projectile.GetAlpha(lightColor),
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            //满蓄力时绘制额外光晕
            if (ChargeLevel >= 0.9f) {
                float glowScale = Projectile.scale * (1.2f + (float)Math.Sin(Time * 0.3f) * 0.2f);
                Color glowColor = Color.OrangeRed * 0.4f;
                Main.EntitySpriteDraw(texture, drawPos, null, glowColor,
                    Projectile.rotation, origin, glowScale, SpriteEffects.None, 0);
            }

            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Projectile.RotatingHitboxCollision(targetHitbox.TopLeft(), targetHitbox.Size(), null, ChargeLevel + 1f);
        }
    }
}
