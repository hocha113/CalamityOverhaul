using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.SupCalDisplayTexts;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 刻心者
    /// </summary>
    internal class Heartcarver : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "Heartcarver";

        public override void SetStaticDefaults() {
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults() {
            Item.width = 52;
            Item.height = 52;
            Item.damage = 1666;
            Item.DamageType = DamageClass.Generic;
            Item.useAnimation = 14;
            Item.useTime = 14;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Rapier;
            Item.knockBack = 5.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<HeartcarverHeld>();
            Item.shootSpeed = 2.4f;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.buyPrice(0, 30, 0, 0);
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
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

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            if (Main.LocalPlayer.TryGetADVSave(out ADVSave save) && save.SupCalDoGQuestReward) {
                TooltipLine line = new(Mod, "Story", SupCalDisplayText.Story2.Value);
                line.OverrideColor = Color.OrangeRed;
                tooltips.Add(line);
            }
        }

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                if (player.CountProjectilesOfID<HeartcarverDash>() > 0 || player.CountProjectilesOfID<HeartcarverAlt>() > 0) {
                    return false;
                }
            }
            return true;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, position, velocity.SafeNormalize(Vector2.Zero),
                    ModContent.ProjectileType<HeartcarverDash>(), (int)(damage * 1.5f), knockback * 2f, player.whoAmI);
                return false;
            }

            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    /// <summary>
    /// 刻心者短剑的手持弹幕 — 三连刺+终结斩连击系统
    /// </summary>
    internal class HeartcarverHeld : BaseKnife
    {
        public override string Texture => CWRConstant.Item_Melee + "Heartcarver";
        public override int TargetID => ModContent.ItemType<Heartcarver>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Red_Bar";
        private static int stabCounter = 0;
        private static int comboStep = 0;

        public override void SetKnifeProperty() {
            Projectile.DamageType = DamageClass.Generic;
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = false;
            SwingData.baseSwingSpeed = 5.5f;
            Length = 45;
            drawTrailTopWidth = 30;
            drawTrailBtommWidth = 10;
            Projectile.ArmorPenetration = 32767;
        }

        public override void KnifeInitialize() {
            if (++comboStep > 3) {
                comboStep = 0;
            }
        }

        public override bool PreSwingAI() {
            //右键冲刺突击行为，这样防止右键被左键硬控
            if (DownRight && Owner.CountProjectilesOfID<HeartcarverDash>() == 0
                && Owner.CountProjectilesOfID<HeartcarverAlt>() == 0 && Projectile.IsOwnedByLocalPlayer()) {
                ShootState shootState = Owner.GetShootState();
                Projectile.NewProjectile(shootState.Source, Owner.Center, ShootVelocity.SafeNormalize(Vector2.Zero),
                    ModContent.ProjectileType<HeartcarverDash>(), (int)(shootState.WeaponDamage * 1.5f)
                    , shootState.WeaponKnockback * 2f, Owner.whoAmI);
                Projectile.Kill();
                return false;
            }

            if (comboStep == 3) {
                //终结斩蓄力阶段的血色能量粒子
                if (Time < maxSwingTime * 0.3f && Time % 2 == 0) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float dist = Main.rand.NextFloat(60f, 120f);
                    Vector2 spawnPos = Owner.Center + angle.ToRotationVector2() * dist;
                    Vector2 vel = (Owner.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 8f);
                    PRT_SparkAlpha spark = new PRT_SparkAlpha(
                        spawnPos, vel, false, Main.rand.Next(10, 18),
                        Main.rand.NextFloat(1.5f, 2.5f),
                        Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat())
                    );
                    PRTLoader.AddParticle(spark);
                }
            }

            //普通三连刺 — 逐刺加速加距
            float speedBonus = 1f + comboStep * 0.15f;
            float lengthBonus = comboStep * 8f;

            StabBehavior(
                initialLength: 35,
                lifetime: (int)(maxSwingTime / speedBonus),
                scaleFactorDenominator: 380f - comboStep * 30f,
                minLength: (int)(25 + lengthBonus),
                maxLength: (int)(50 + lengthBonus),
                canDrawSlashTrail: false
            );

            if (Time == 1) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Pitch = comboStep * 0.15f,
                    Volume = 0.9f + comboStep * 0.1f
                }, Projectile.Center);
            }

            //连刺加速时的拖尾粒子
            if (comboStep >= 1 && Time % 3 == 0) {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(20f, 20f);
                Dust trail = Dust.NewDustPerfect(dustPos, DustID.Blood,
                    -ShootVelocity * 0.3f, 100, default, 1.2f + comboStep * 0.3f);
                trail.noGravity = true;
            }

            return false;
        }

        public override void Shoot() {
            int daggerType = ModContent.ProjectileType<HeartcarverDagger>();
            if (stabCounter > 3) {
                stabCounter = 0;
            }

            //终结斩：生成2把匕首 + 屏幕微震
            if (comboStep == 3) {
                int spawnCount = Math.Min(2, 3 - Owner.ownedProjectileCounts[daggerType]);
                for (int i = 0; i < spawnCount; i++) {
                    Projectile.NewProjectile(
                        Source, ShootSpanPos, Vector2.Zero,
                        daggerType, (int)(Projectile.damage * 1.2f), Projectile.knockBack,
                        Owner.whoAmI, ai0: stabCounter
                    );
                    stabCounter++;
                }
                return;
            }

            //普通刺击
            if (Owner.ownedProjectileCounts[daggerType] < 3) {
                Projectile.NewProjectile(
                    Source, ShootSpanPos, Vector2.Zero,
                    daggerType, Projectile.damage, Projectile.knockBack,
                    Owner.whoAmI, ai0: stabCounter
                );
                stabCounter++;
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Bleeding, 180 + comboStep * 60);

            //终结斩命中：强力打击反馈
            if (comboStep == 3 && Projectile.numHits <= 1) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with {
                    Volume = 0.8f,
                    Pitch = 0.4f
                }, target.Center);

                for (int i = 0; i < 20; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(10f, 10f);
                    Dust hitDust = Dust.NewDustPerfect(
                        target.Center, DustID.Blood, vel, 100, default,
                        Main.rand.NextFloat(1.8f, 2.8f)
                    );
                    hitDust.noGravity = true;
                    hitDust.fadeIn = 1.3f;
                }

                for (int i = 0; i < 6; i++) {
                    PRT_SparkAlpha spark = new PRT_SparkAlpha(
                        target.Center, Main.rand.NextVector2Circular(10f, 10f),
                        false, Main.rand.Next(12, 20), Main.rand.NextFloat(1.5f, 2.5f),
                        Color.Lerp(Color.Red, Color.Crimson, Main.rand.NextFloat())
                    );
                    PRTLoader.AddParticle(spark);
                }
            }
        }
    }

    internal class HeartcarverAlt : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<Heartcarver>()).DisplayName;
        private const int CooldownDuration = 120;

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Generic;
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = CooldownDuration;
        }

        public override void AI() {
            Projectile.Center = Owner.Center;
            float progress = 1f - Projectile.timeLeft / (float)CooldownDuration;

            //冷却期间粒子逐渐减少，最后几帧爆发充能完成提示
            int dustCount = (int)MathHelper.Lerp(4f, 1f, progress);
            for (int i = 0; i < dustCount; i++) {
                Dust d = Dust.NewDustPerfect(
                    Owner.Center + Main.rand.NextVector2Circular(Owner.width * 0.5f, Owner.height * 0.5f),
                    DustID.Blood, Vector2.Zero, 100, default,
                    MathHelper.Lerp(1.2f, 0.6f, progress)
                );
                d.noGravity = true;
                d.fadeIn = 0.8f;
            }

            //充能接近完成时的脉冲提示
            if (Projectile.timeLeft == 15) {
                SoundEngine.PlaySound(SoundID.Item28 with {
                    Volume = 0.4f,
                    Pitch = 0.5f
                }, Owner.Center);

                for (int i = 0; i < 16; i++) {
                    float angle = MathHelper.TwoPi * i / 16f;
                    PRT_SparkAlpha spark = new PRT_SparkAlpha(
                        Owner.Center,
                        angle.ToRotationVector2() * 5f,
                        false, 15, Main.rand.NextFloat(1.5f, 2.5f),
                        Color.Lerp(Color.Red, Color.Crimson, Main.rand.NextFloat())
                    );
                    PRTLoader.AddParticle(spark);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.1f, Volume = 0.8f }, Owner.Center);

            //充能完成爆发
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Dust burst = Dust.NewDustPerfect(
                    Owner.Center, DustID.Blood,
                    angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f),
                    100, default, Main.rand.NextFloat(1.5f, 2f)
                );
                burst.noGravity = true;
                burst.fadeIn = 1.2f;
            }
        }
    }

    /// <summary>
    /// 刻心者冲刺突击弹幕
    /// </summary>
    internal class HeartcarverDash : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "Heartcarver";

        //冲刺分为两阶段：前冲 + 回刺
        private const int ForwardDuration = 18;
        private const int ReturnDuration = 10;
        private const int TotalDuration = ForwardDuration + ReturnDuration;
        private const float DashSpeed = 38f;

        private Vector2 dashDirection;
        private Vector2 dashStartPos;
        private Vector2 dashPeakPos;
        private Vector2 dashPerpendicularDir;
        private int hitCount;
        private int dashTimer;
        private bool isReturning;
        private float arcIntensity;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 18;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Generic;
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
            Projectile.ArmorPenetration = 32767;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            //初始化冲刺
            if (Projectile.localAI[0] == 0f) {
                InitializeDash();
                Projectile.localAI[0] = 1f;
            }

            dashTimer++;
            Owner.direction = Projectile.direction = Math.Sign(dashDirection.X);

            if (!isReturning && dashTimer >= ForwardDuration) {
                //切换到回刺阶段
                isReturning = true;
                dashPeakPos = Owner.Center;
                dashTimer = 0;

                //回刺瞬间的强力音效
                SoundEngine.PlaySound(SoundID.Item71 with {
                    Volume = 0.8f,
                    Pitch = 0.5f
                }, Owner.Center);

                //方向反转粒子爆发
                for (int i = 0; i < 20; i++) {
                    float angle = MathHelper.TwoPi * i / 20f;
                    Dust flip = Dust.NewDustPerfect(
                        Owner.Center, DustID.Blood,
                        angle.ToRotationVector2() * Main.rand.NextFloat(6f, 12f),
                        100, default, Main.rand.NextFloat(1.5f, 2.2f)
                    );
                    flip.noGravity = true;
                    flip.fadeIn = 1.2f;
                }
            }

            if (isReturning) {
                UpdateReturnMovement();
            }
            else {
                UpdateForwardMovement();
            }

            UpdateVisualEffects();
            Owner.GivePlayerImmuneState(36);
            Projectile.Center = Owner.Center;
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        private void InitializeDash() {
            dashDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            dashStartPos = Owner.Center;
            dashPerpendicularDir = new Vector2(-dashDirection.Y, dashDirection.X);
            arcIntensity = Main.rand.NextBool() ? 1f : -1f;

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                Volume = 0.8f,
                Pitch = -0.2f
            }, Owner.Center);

            SoundEngine.PlaySound(SoundID.Item71 with {
                Volume = 0.6f,
                Pitch = 0.1f
            }, Owner.Center);

            if (CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new PunchCameraModifier(
                    Owner.Center, dashDirection, 5f, 6f, 10, 600f, FullName
                );
                Main.instance.CameraModifiers.Add(modifier);
            }

            SpawnDashStartEffect();
        }

        private void UpdateForwardMovement() {
            float t = dashTimer / (float)ForwardDuration;
            //使用三次贝塞尔曲线实现弧形冲刺轨迹
            float easedT = t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;

            //贝塞尔控制点：起点 → 弧形偏移控制点 → 终点
            Vector2 endPos = dashStartPos + dashDirection * DashSpeed * ForwardDuration * 0.6f;
            Vector2 controlPoint = dashStartPos + dashDirection * DashSpeed * ForwardDuration * 0.3f
                + dashPerpendicularDir * 80f * arcIntensity;

            //二阶贝塞尔曲线
            Vector2 targetPos = Vector2.Lerp(
                Vector2.Lerp(dashStartPos, controlPoint, easedT),
                Vector2.Lerp(controlPoint, endPos, easedT),
                easedT
            );

            Projectile.velocity = targetPos - Owner.Center;
            Owner.Center = targetPos;

            //匕首始终朝向运动方向
            if (Projectile.velocity.LengthSquared() > 0.01f) {
                float targetRot = Projectile.velocity.ToRotation();
                Projectile.rotation = targetRot + (Projectile.direction > 0 ? MathHelper.PiOver4 : -MathHelper.Pi - MathHelper.PiOver4);
            }
        }

        private void UpdateReturnMovement() {
            float t = dashTimer / (float)ReturnDuration;
            //回刺使用强力的缓入缓出
            float easedT = t * t * (3f - 2f * t);

            Vector2 returnTarget = dashStartPos;
            Owner.Center = Vector2.Lerp(dashPeakPos, returnTarget, easedT);
            Projectile.velocity = Owner.Center - (dashPeakPos + (returnTarget - dashPeakPos) * (easedT - 0.05f));

            //回刺方向旋转
            Vector2 returnDir = (returnTarget - dashPeakPos).SafeNormalize(Vector2.UnitX);
            Projectile.rotation = returnDir.ToRotation() + (Projectile.direction > 0 ? MathHelper.PiOver4 : -MathHelper.Pi - MathHelper.PiOver4);

            if (dashTimer >= ReturnDuration) {
                Projectile.Kill();
            }
        }

        private void UpdateVisualEffects() {
            //密集血色拖尾
            for (int i = 0; i < 2; i++) {
                Vector2 spawnPos = Owner.Center + Main.rand.NextVector2Circular(25f, 25f);
                Dust trail = Dust.NewDustPerfect(
                    spawnPos, DustID.Blood,
                    -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 7f),
                    100, default, Main.rand.NextFloat(1.3f, 2f)
                );
                trail.noGravity = true;
                trail.fadeIn = 1.2f;
            }

            //高速运动时的Spark粒子
            if (dashTimer % 3 == 0) {
                PRT_SparkAlpha spark = new PRT_SparkAlpha(
                    Owner.Center + Main.rand.NextVector2Circular(15f, 15f),
                    -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 5f),
                    false, Main.rand.Next(8, 15), Main.rand.NextFloat(1.2f, 2f),
                    Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat())
                );
                PRTLoader.AddParticle(spark);
            }

            Lighting.AddLight(Owner.Center, 1.1f, 0.25f, 0.25f);
        }

        private void SpawnDashStartEffect() {
            //定向锥形爆发（朝冲刺方向散开）
            for (int i = 0; i < 25; i++) {
                float spread = Main.rand.NextFloat(-0.8f, 0.8f);
                Vector2 velocity = dashDirection.RotatedBy(spread) * Main.rand.NextFloat(8f, 18f);

                Dust burst = Dust.NewDustPerfect(
                    Owner.Center, DustID.Blood, velocity,
                    100, default, Main.rand.NextFloat(1.5f, 2.5f)
                );
                burst.noGravity = true;
                burst.fadeIn = 1.3f;
            }

            //反方向气流粒子
            for (int i = 0; i < 10; i++) {
                float spread = Main.rand.NextFloat(-0.5f, 0.5f);
                Vector2 velocity = (-dashDirection).RotatedBy(spread) * Main.rand.NextFloat(5f, 10f);

                Dust wake = Dust.NewDustPerfect(
                    Owner.Center, DustID.Smoke, velocity,
                    150, new Color(120, 40, 40), Main.rand.NextFloat(1.2f, 2f)
                );
                wake.noGravity = true;
            }

            //环形Spark冲击波
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                PRT_SparkAlpha spark = new PRT_SparkAlpha(
                    Owner.Center,
                    angle.ToRotationVector2() * 10f,
                    false, 18, Main.rand.NextFloat(2f, 3.5f),
                    Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat())
                );
                PRTLoader.AddParticle(spark);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit18 with {
                Volume = 0.7f,
                Pitch = 0.3f + (isReturning ? 0.2f : 0f)
            }, target.Center);

            target.AddBuff(BuffID.Bleeding, 300);

            //定向血液飞溅（从冲刺方向扩散）
            Vector2 hitDir = (target.Center - Owner.Center).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 18; i++) {
                float spread = Main.rand.NextFloat(-1f, 1f);
                Vector2 vel = hitDir.RotatedBy(spread) * Main.rand.NextFloat(4f, 12f);
                Dust hitDust = Dust.NewDustPerfect(
                    target.Center, DustID.Blood, vel,
                    100, default, Main.rand.NextFloat(1.5f, 2.5f)
                );
                hitDust.noGravity = true;
                hitDust.fadeIn = 1.2f;
            }

            hitCount++;

            //回刺命中时额外屏幕震动
            if (isReturning && CWRServerConfig.Instance.ScreenVibration) {
                PunchCameraModifier modifier = new PunchCameraModifier(
                    target.Center, hitDir, 4f, 5f, 8, 500f, FullName
                );
                Main.instance.CameraModifiers.Add(modifier);
            }

            if (hitCount % 3 == 0) {
                SpawnDashStartEffect();
            }
        }

        public override void OnKill(int timeLeft) {
            //回刺终结时的收束粒子效果
            for (int i = 0; i < 24; i++) {
                float angle = MathHelper.TwoPi * i / 24f;
                float dist = Main.rand.NextFloat(40f, 80f);
                Vector2 startPos = Owner.Center + angle.ToRotationVector2() * dist;
                Vector2 vel = (Owner.Center - startPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 8f);

                Dust killDust = Dust.NewDustPerfect(
                    startPos, DustID.Blood, vel,
                    100, default, Main.rand.NextFloat(1.3f, 2f)
                );
                killDust.noGravity = true;
                killDust.fadeIn = 1.1f;
            }

            Owner.velocity *= 0.3f;

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Owner.FromObjectGetParent(), Owner.Center, Vector2.Zero
                    , ModContent.ProjectileType<HeartcarverAlt>(), 0, 0, Owner.whoAmI);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Owner.GetPlayerStabilityCenter() - Main.screenPosition;
            Rectangle sourceRect = texture.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;
            SpriteEffects spriteEffects = Projectile.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //绘制残影拖尾 — 颜色随阶段变化
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                float alpha = progress * 0.85f;
                float scale = Projectile.scale * MathHelper.Lerp(0.65f, 1.05f, progress);

                Color trailColor;
                if (isReturning) {
                    trailColor = Color.Lerp(new Color(255, 80, 80), new Color(180, 30, 30), 1f - progress) * alpha;
                }
                else {
                    trailColor = Color.Lerp(new Color(200, 50, 50), new Color(255, 120, 120), progress) * alpha;
                }

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                Main.spriteBatch.Draw(
                    texture, trailPos, sourceRect, trailColor,
                    Projectile.oldRot.Length > i ? Projectile.oldRot[i] : Projectile.rotation,
                    origin, scale, spriteEffects, 0
                );
            }

            //绘制主体
            Main.spriteBatch.Draw(
                texture, drawPos, sourceRect, lightColor,
                Projectile.rotation, origin, Projectile.scale, spriteEffects, 0
            );

            //多层发光效果
            float glowPulse = 0.5f + MathF.Sin(dashTimer * 0.4f) * 0.15f;
            Color glowColor = new Color(220, 50, 50) * glowPulse;
            Main.spriteBatch.Draw(
                texture, drawPos, sourceRect, glowColor,
                Projectile.rotation, origin, Projectile.scale * 1.08f, spriteEffects, 0
            );

            Color glowColor2 = new Color(255, 100, 100) * (glowPulse * 0.4f);
            Main.spriteBatch.Draw(
                texture, drawPos, sourceRect, glowColor2,
                Projectile.rotation, origin, Projectile.scale * 1.18f, spriteEffects, 0
            );

            return false;
        }
    }

    /// <summary>
    /// 刻心者环绕匕首
    /// </summary>
    internal class HeartcarverDagger : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "Heartcarver";

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        private enum DaggerState
        {
            Gathering,
            Orbiting,
            Charging,
            Launching
        }

        private DaggerState State {
            get => (DaggerState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }

        private ref float DaggerIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[2];

        //环绕参数 — 椭圆轨道
        private float orbitRadiusX = 100f;
        private float orbitRadiusY = 55f;
        private float orbitAngle = 0f;
        private float orbitSpeed = 0.05f;
        private float orbitTiltAngle = 0f;
        private const float MaxOrbitSpeed = 0.45f;

        //缩短各阶段时间，节奏更紧凑
        private const int GatherDuration = 12;
        private const int OrbitDuration = 35;
        private const int ChargeDuration = 25;
        private const float LaunchSpeed = 32f;

        //视觉效果
        private float glowIntensity = 0f;
        private float trailIntensity = 0f;
        private float daggerScale = 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Generic;
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.ArmorPenetration = 32767;
        }

        public override bool? CanDamage() {
            return State == DaggerState.Launching ? null : false;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            StateTimer++;

            switch (State) {
                case DaggerState.Gathering:
                    GatheringPhaseAI(Owner);
                    break;
                case DaggerState.Orbiting:
                    OrbitingPhaseAI(Owner);
                    break;
                case DaggerState.Charging:
                    ChargingPhaseAI(Owner);
                    break;
                case DaggerState.Launching:
                    LaunchingPhaseAI(Owner);
                    break;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            float lightIntensity = glowIntensity * 0.8f;
            Lighting.AddLight(Projectile.Center,
                0.9f * lightIntensity, 0.2f * lightIntensity, 0.2f * lightIntensity);
        }

        //椭圆轨道位置计算（带倾斜面）
        private Vector2 GetEllipseOrbitPos(Vector2 center, float angle, float radiusX, float radiusY, float tilt) {
            float x = MathF.Cos(angle) * radiusX;
            float y = MathF.Sin(angle) * radiusY;
            //倾斜面旋转：绕X轴倾斜制造伪3D效果
            float tiltedY = y * MathF.Cos(tilt);
            return center + new Vector2(x, tiltedY).RotatedBy(tilt * 0.3f);
        }

        //基于椭圆位置计算伪深度缩放
        private float GetDepthScale(float angle, float tilt) {
            float depth = MathF.Sin(angle) * MathF.Sin(tilt);
            return MathHelper.Lerp(0.7f, 1.3f, (depth + 1f) * 0.5f);
        }

        private void GatheringPhaseAI(Player owner) {
            float progress = StateTimer / GatherDuration;

            //初始化每把匕首的轨道倾斜角（错开120度均匀分布）
            orbitTiltAngle = MathHelper.PiOver4 + DaggerIndex * 0.4f;
            float targetAngle = MathHelper.TwoPi * DaggerIndex / 3f;
            Vector2 targetPos = GetEllipseOrbitPos(owner.Center, targetAngle, orbitRadiusX, orbitRadiusY, orbitTiltAngle);

            //弹性缓出：快速逼近后减速
            float easeProgress = 1f - MathF.Pow(1f - progress, 3f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, easeProgress * 0.5f);

            orbitAngle = targetAngle;
            glowIntensity = MathHelper.Lerp(0f, 0.6f, progress);
            daggerScale = MathHelper.Lerp(0.5f, 1f, easeProgress);

            if (Main.rand.NextBool(3)) {
                SpawnGatherParticle();
            }

            if (StateTimer >= GatherDuration) {
                State = DaggerState.Orbiting;
                StateTimer = 0;

                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.5f,
                    Pitch = 0.3f + DaggerIndex * 0.1f
                }, Projectile.Center);
            }
        }

        private void OrbitingPhaseAI(Player owner) {
            float progress = StateTimer / OrbitDuration;

            //加速旋转 — 使用缓入
            float speedProgress = progress * progress;
            orbitSpeed = MathHelper.Lerp(0.05f, MaxOrbitSpeed * 0.55f, speedProgress);

            //椭圆半径呼吸效果
            float breathe = MathF.Sin(StateTimer * 0.2f) * 8f * progress;
            float currentRadiusX = orbitRadiusX + breathe;
            float currentRadiusY = orbitRadiusY + breathe * 0.5f;

            orbitAngle += orbitSpeed;

            Vector2 targetPos = GetEllipseOrbitPos(owner.Center, orbitAngle, currentRadiusX, currentRadiusY, orbitTiltAngle);
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.35f);

            //伪3D深度缩放
            daggerScale = GetDepthScale(orbitAngle, orbitTiltAngle);

            glowIntensity = MathHelper.Lerp(0.5f, 0.85f, progress);
            trailIntensity = progress;

            if (Main.rand.NextBool(4)) {
                SpawnOrbitParticle(owner.Center, progress);
            }

            if (StateTimer >= OrbitDuration) {
                State = DaggerState.Charging;
                StateTimer = 0;

                SoundEngine.PlaySound(SoundID.Item28 with {
                    Volume = 0.6f,
                    Pitch = -0.2f
                }, Projectile.Center);
            }
        }

        private void ChargingPhaseAI(Player owner) {
            float progress = StateTimer / ChargeDuration;

            //最高旋转速度
            orbitSpeed = MathHelper.Lerp(MaxOrbitSpeed * 0.55f, MaxOrbitSpeed, CWRUtils.EaseInOutQuad(progress));

            //半径急剧收缩 — 匕首向玩家集中
            float shrinkFactor = 1f - progress * 0.6f;
            float oscillation = MathF.Sin(StateTimer * 0.7f) * 12f * (1f - progress);
            float currentRadiusX = orbitRadiusX * shrinkFactor + oscillation;
            float currentRadiusY = orbitRadiusY * shrinkFactor + oscillation * 0.5f;

            orbitAngle += orbitSpeed;
            Vector2 targetPos = GetEllipseOrbitPos(owner.Center, orbitAngle, currentRadiusX, currentRadiusY, orbitTiltAngle);
            Projectile.velocity = Projectile.Center.To(targetPos) * 0.45f;

            daggerScale = GetDepthScale(orbitAngle, orbitTiltAngle) * MathHelper.Lerp(1f, 1.2f, progress);

            //辉光脉冲加剧
            glowIntensity = 0.9f + MathF.Sin(StateTimer * 1f) * 0.1f;
            trailIntensity = 1f;

            //密集能量聚合粒子
            if (Main.rand.NextBool(2)) {
                SpawnChargeParticle(owner.Center, progress);
            }

            if (StateTimer % 8 == 0) {
                SpawnChargePulse();
            }

            //所有匕首同时发射（不再错开）
            if (StateTimer >= ChargeDuration) {
                LaunchToTarget(owner);
            }
        }

        private void LaunchToTarget(Player owner) {
            NPC target = owner.Center.FindClosestNPC(1500);

            Vector2 launchDir;
            if (target != null) {
                //预判目标位置
                float travelTime = Vector2.Distance(Projectile.Center, target.Center) / LaunchSpeed;
                Vector2 predictedPos = target.Center + target.velocity * travelTime * 0.5f;
                launchDir = (predictedPos - Projectile.Center).SafeNormalize(Vector2.Zero);
            }
            else {
                launchDir = (InMousePos - Projectile.Center).SafeNormalize(
                    (orbitAngle - MathHelper.PiOver2).ToRotationVector2()
                );
            }

            float momentumBonus = orbitSpeed / MaxOrbitSpeed;
            float finalSpeed = LaunchSpeed * (1f + momentumBonus * 0.3f);

            Projectile.velocity = launchDir * finalSpeed;
            Projectile.tileCollide = false;

            State = DaggerState.Launching;
            StateTimer = 0;

            SpawnLaunchBurst();

            SoundEngine.PlaySound(SoundID.Item71 with {
                Volume = 0.8f,
                Pitch = 0.4f
            }, Projectile.Center);
        }

        private void LaunchingPhaseAI(Player owner) {
            //更激进的追踪
            NPC target = Projectile.Center.FindClosestNPC(1200);
            if (target != null && Projectile.numHits == 0) {
                Projectile.SmoothHomingBehavior(target.Center, 1.03f, 0.12f);
            }
            else if (target != null) {
                //击中后弱追踪，不会死板飞行
                Projectile.SmoothHomingBehavior(target.Center, 1.005f, 0.03f);
            }

            //速度维持（几乎不衰减）
            float speed = Projectile.velocity.Length();
            if (speed < LaunchSpeed * 0.7f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * LaunchSpeed * 0.7f;
            }

            trailIntensity = 1f;
            glowIntensity = 0.95f;
            daggerScale = 1.1f;

            //匕首朝向运动方向
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            if (Main.rand.NextBool(2)) {
                SpawnLaunchTrailParticle();
            }

            if (StateTimer > 150) {
                Projectile.Kill();
            }
        }

        private void SpawnGatherParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float dist = Main.rand.NextFloat(30f, 60f);
            Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * dist;
            Vector2 vel = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(3f, 6f);

            PRT_SparkAlpha spark = new PRT_SparkAlpha(
                spawnPos, vel, false, Main.rand.Next(8, 14),
                Main.rand.NextFloat(1f, 1.8f),
                Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat())
            );
            PRTLoader.AddParticle(spark);
        }

        private void SpawnOrbitParticle(Vector2 ownerCenter, float progress) {
            Vector2 tangent = new Vector2(-MathF.Sin(orbitAngle), MathF.Cos(orbitAngle));
            Vector2 velocity = tangent * orbitSpeed * 30f * progress;

            Dust orbit = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.Blood, velocity * 0.3f,
                100, default, Main.rand.NextFloat(0.9f, 1.4f)
            );
            orbit.noGravity = true;
            orbit.fadeIn = 1.1f;
        }

        private void SpawnChargeParticle(Vector2 ownerCenter, float progress) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float dist = Main.rand.NextFloat(20f, 60f) * (1f - progress * 0.5f);
            Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * dist;
            Vector2 vel = (Projectile.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 9f) * progress;

            PRT_SparkAlpha spark = new PRT_SparkAlpha(
                spawnPos, vel, false, Main.rand.Next(8, 16),
                Main.rand.NextFloat(1.2f, 2.2f),
                Color.Lerp(Color.Red, Color.Crimson, Main.rand.NextFloat())
            );
            PRTLoader.AddParticle(spark);
        }

        private void SpawnChargePulse() {
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Dust pulse = Dust.NewDustPerfect(
                    Projectile.Center, DustID.Blood,
                    angle.ToRotationVector2() * 3f,
                    100, default, Main.rand.NextFloat(1.3f, 1.8f)
                );
                pulse.noGravity = true;
                pulse.fadeIn = 1.2f;
            }
        }

        private void SpawnLaunchBurst() {
            //定向锥形爆发（朝发射方向）
            Vector2 launchDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 18; i++) {
                float spread = Main.rand.NextFloat(-0.6f, 0.6f);
                Vector2 vel = launchDir.RotatedBy(spread) * Main.rand.NextFloat(6f, 14f);

                Dust burst = Dust.NewDustPerfect(
                    Projectile.Center, DustID.Blood, vel,
                    100, default, Main.rand.NextFloat(1.4f, 2.2f)
                );
                burst.noGravity = true;
                burst.fadeIn = 1.3f;
            }

            for (int i = 0; i < 6; i++) {
                PRT_SparkAlpha spark = new PRT_SparkAlpha(
                    Projectile.Center,
                    launchDir.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(4f, 10f),
                    false, Main.rand.Next(12, 22), Main.rand.NextFloat(1.5f, 2.5f),
                    Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat())
                );
                PRTLoader.AddParticle(spark);
            }
        }

        private void SpawnLaunchTrailParticle() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                DustID.Blood,
                -Projectile.velocity * Main.rand.NextFloat(0.08f, 0.2f),
                100, default, Main.rand.NextFloat(1f, 1.5f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1.1f;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //穿透地形，匕首不应被地形阻挡
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //定向血液飞溅
            Vector2 hitDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 15; i++) {
                float spread = Main.rand.NextFloat(-0.8f, 0.8f);
                Vector2 vel = hitDir.RotatedBy(spread) * Main.rand.NextFloat(3f, 10f);
                Dust hitDust = Dust.NewDustPerfect(
                    target.Center, DustID.Blood, vel,
                    100, default, Main.rand.NextFloat(1.3f, 2f)
                );
                hitDust.noGravity = true;
                hitDust.fadeIn = 1.2f;
            }

            SoundEngine.PlaySound(SoundID.NPCHit18 with {
                Volume = 0.6f,
                Pitch = 0.3f
            }, Projectile.Center);

            target.AddBuff(BuffID.Bleeding, 240);

            //命中Spark爆发
            for (int i = 0; i < 5; i++) {
                PRT_SparkAlpha spark = new PRT_SparkAlpha(
                    target.Center, Main.rand.NextVector2Circular(8f, 8f),
                    false, Main.rand.Next(10, 18), Main.rand.NextFloat(1.2f, 2f),
                    Color.Lerp(Color.Red, Color.Crimson, Main.rand.NextFloat())
                );
                PRTLoader.AddParticle(spark);
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust killDust = Dust.NewDustPerfect(
                    Projectile.Center, DustID.Blood, vel,
                    100, default, Main.rand.NextFloat(1.2f, 1.8f)
                );
                killDust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D daggerTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = daggerTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;

            SpriteEffects spriteEffects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float rot = Projectile.rotation + (Projectile.velocity.X > 0 ? 0 : MathHelper.PiOver2);

            Color baseColor = lightColor;
            float alpha = (255f - Projectile.alpha) / 255f;
            float drawScale = Projectile.scale * daggerScale;

            //残影拖尾
            if (State == DaggerState.Orbiting || State == DaggerState.Charging || State == DaggerState.Launching) {
                DrawDaggerAfterimages(sb, daggerTex, sourceRect, origin, baseColor, alpha, drawScale);
            }

            //光晕
            if (glowIntensity > 0.3f && SoftGlow?.Value != null) {
                Texture2D glow = SoftGlow.Value;
                float glowScale = drawScale * (0.8f + glowIntensity * 0.6f);
                float glowAlpha = (glowIntensity - 0.3f) * alpha * 0.5f;

                if (State == DaggerState.Charging) {
                    glowAlpha *= 1.8f;
                    glowScale *= 1f + MathF.Sin(StateTimer * 0.8f) * 0.1f;
                }

                sb.Draw(
                    glow, drawPos, null,
                    new Color(255, 80, 80, 0) * glowAlpha,
                    rot, glow.Size() / 2f,
                    glowScale, spriteEffects, 0f
                );
            }

            //主体
            sb.Draw(
                daggerTex, drawPos, sourceRect,
                baseColor * alpha, rot, origin,
                drawScale, spriteEffects, 0
            );

            //血红辉光覆盖层（蓄力/发射时）
            if ((State == DaggerState.Charging || State == DaggerState.Launching) && glowIntensity > 0.5f) {
                float lightAlpha = (glowIntensity - 0.5f) * 2f * alpha * 0.6f;
                sb.Draw(
                    daggerTex, drawPos, sourceRect,
                    new Color(220, 50, 50) * lightAlpha,
                    rot, origin,
                    drawScale * 1.06f, spriteEffects, 0
                );
            }

            return false;
        }

        private void DrawDaggerAfterimages(SpriteBatch sb, Texture2D daggerTex, Rectangle sourceRect,
            Vector2 origin, Color baseColor, float alpha, float drawScale) {
            SpriteEffects spriteEffects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            int count = State == DaggerState.Launching ? 14 : (State == DaggerState.Charging ? 10 : 6);

            for (int i = 0; i < count; i++) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) continue;

                float prog = 1f - i / (float)count;
                float afterAlpha = prog * trailIntensity * alpha;

                Color afterColor;
                if (State == DaggerState.Launching) {
                    afterColor = Color.Lerp(new Color(180, 40, 40), new Color(255, 110, 110), prog) * (afterAlpha * 0.75f);
                }
                else if (State == DaggerState.Charging) {
                    afterColor = Color.Lerp(new Color(200, 50, 50), new Color(240, 80, 80), prog) * (afterAlpha * 0.65f);
                }
                else {
                    afterColor = baseColor * (afterAlpha * 0.45f);
                }

                Vector2 afterPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterScale = drawScale * MathHelper.Lerp(0.8f, 1f, prog);
                float rot = (Projectile.oldRot.Length > i ? Projectile.oldRot[i] : Projectile.rotation) + (Projectile.velocity.X > 0 ? 0 : MathHelper.PiOver2);
                sb.Draw(
                    daggerTex, afterPos, sourceRect, afterColor,
                    rot,
                    origin, afterScale, spriteEffects, 0
                );
            }
        }
    }
}
