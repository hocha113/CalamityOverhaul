using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.CalPlayer;
using CalamityMod.Cooldowns;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Particles;
using CalamityMod.Projectiles;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Ranged
{
    internal class RSuperradiantSlaughterer : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<SuperradiantSlaughterer>();
        public override bool DrawingInfo => false;
        public override void SetDefaults(Item item) => item.shoot = ModContent.ProjectileType<SuperradiantSlaughtererHeld>();
        public override void ModifyRecipe(Recipe recipe) => recipe.RemoveIngredient(ModContent.ItemType<SpeedBlaster>());
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {//重新补上修改，因为描述替换会让原本的修改操作失效
            tooltips.ReplaceTooltip("[MAIN]"
                , VaultUtils.FormatColorTextMultiLine(this.GetLocalizedValue("MainInfo")
                , new Color(180, 255, 0)), "");
            tooltips.ReplaceTooltip("[ALT]"
                , VaultUtils.FormatColorTextMultiLine(this.GetLocalization("AltInfo")
                .Format(SuperradiantSlaughterer.DashCooldown / 60)
                , new Color(120, 255, 120)), "");
        }
    }

    internal class SuperradiantSlaughtererHeld : BaseHeldProj
    {
        [VaultLoaden("@CalamityMod/Projectiles/Ranged/SuperradiantSlaughtererHoldoutMiniSaw")]
        private static Asset<Texture2D> MiniSaw { get; set; }
        [VaultLoaden("@CalamityMod/Projectiles/Ranged/SuperradiantSlaughtererHoldout")]
        private static Asset<Texture2D> Holdout { get; set; }
        [VaultLoaden("@CalamityMod/Projectiles/Ranged/SuperradiantSlaughtererHoldoutGlow")]
        private static Asset<Texture2D> HoldoutGlow { get; set; }
        [VaultLoaden("@CalamityMod/Projectiles/Ranged/SuperradiantSawLargeSlash")]
        private static Asset<Texture2D> LargeSlash { get; set; }
        [VaultLoaden("@CalamityMod/Projectiles/Ranged/SuperradiantSawSmallSlash")]
        private static Asset<Texture2D> SmallSlash { get; set; }
        public override string Texture => "CalamityMod/Projectiles/Ranged/SuperradiantSlaughtererHoldout";
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<SuperradiantSlaughterer>();
        public bool NoSawByHeld;
        public Particle TargetSmallSlashInstance;
        public Particle TargetLargeSlashInstance;
        public const float MaxFromArmLength = 36f;
        public const float MaxChargeTime = 120f;
        public float FromArmLength { get; set; }
        public bool FreezingLife { get; set; } = true;
        public ref float Time => ref Projectile.ai[0];
        public SlotId TargetSound;
        public Vector2 TruePosition => Projectile.Center + Vector2.UnitX.RotatedBy(Projectile.rotation) * Projectile.width * 0.25f;
        public override bool? CanDamage() => !NoSawByHeld;
        public override bool ShouldUpdatePosition() => false;
        public override void SetStaticDefaults() => Main.projFrames[Type] = 5;
        public override void SetDefaults() {
            Projectile.width = 84;
            Projectile.height = 46;
            Projectile.tileCollide = false;
            Projectile.netImportant = true;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }
        public override void Initialize() => FromArmLength = MaxFromArmLength;
        public override void AI() {
            if (!Owner.Alives() || Item.type != ModContent.ItemType<SuperradiantSlaughterer>()) {
                Projectile.Kill();
                Projectile.netUpdate = true;
                return;
            }

            Time++;

            Vector2 armPosition = Owner.RotatedRelativePoint(Owner.MountedCenter, true);

            Vector2 ownerToMouse = InMousePos - armPosition;

            float holdoutDirection = Projectile.velocity.ToRotation();

            float proximityLookingUpwards = Vector2.Dot(ownerToMouse.SafeNormalize(Vector2.Zero), -Vector2.UnitY * Owner.gravDir);

            int direction = MathF.Sign(ownerToMouse.X);

            Vector2 lengthOffset = holdoutDirection.ToRotationVector2() * FromArmLength;
            Vector2 armOffset = new Vector2(Utils.Remap(MathF.Abs(proximityLookingUpwards), 0f, 1f, 0f, 0) * direction, -5f * Owner.gravDir + Utils.Remap(MathF.Abs(proximityLookingUpwards), 0f, 1f, 0f, proximityLookingUpwards > 0f ? -5 : 5) * Owner.gravDir);
            Projectile.Center = armPosition + lengthOffset + armOffset;
            Projectile.velocity = holdoutDirection.AngleTowards(ownerToMouse.ToRotation(), 0.2f).ToRotationVector2();
            Projectile.rotation = holdoutDirection;

            Projectile.spriteDirection = direction;
            Owner.ChangeDir(direction);

            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = (Projectile.velocity * Projectile.direction).ToRotation();

            float armRotation = (Projectile.rotation - MathHelper.PiOver2) * Owner.gravDir + (Owner.gravDir == -1 ? MathHelper.Pi : 0f);
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation + MathHelper.ToRadians(15f) * direction);

            if (FreezingLife) {
                Projectile.timeLeft = 2;
            }

            if (FromArmLength != MaxFromArmLength) {
                FromArmLength = MathHelper.Lerp(FromArmLength, MaxFromArmLength, 0.05f);
            }

            float SawPower = MathHelper.Clamp(Time / MaxChargeTime, 0f, 1f);

            if (SoundEngine.TryGetActiveSound(TargetSound, out var Idle) && Idle.IsPlaying) {
                Idle.Position = TruePosition;
            }

            if (DownRight && !Owner.HasCooldown(SuperradiantSawBoost.ID)) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Owner.AddCooldown(SuperradiantSawBoost.ID, SuperradiantSlaughterer.DashCooldown);
                }
                
                Owner.Calamity().sBlasterDashActivated = true;
                Owner.velocity += UnitToMouseV * 22;
                SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Custom/MeatySlash"), TruePosition);

                float clampedMouseDist = MathHelper.Clamp(Vector2.Distance(TruePosition, Owner.Calamity().mouseWorld), 0f, 960f);
                float adjustedMouseDist = clampedMouseDist / 21f;

                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), TruePosition, Projectile.velocity.SafeNormalize(Vector2.UnitY) * adjustedMouseDist
                    , ModContent.ProjectileType<SuperradiantSawLingering>(), (int)(Projectile.damage * 1.5f), Projectile.knockBack, Projectile.owner);
                }

                if (Projectile.ai[1] >= 2f) {
                    NoSawByHeld = true;
                    FromArmLength -= 16f;
                    Projectile.timeLeft = Item.useAnimation;
                    FreezingLife = false;
                    Idle?.Stop();
                }

                if (Owner.velocity != Vector2.Zero) {
                    int particleAmt = 7;

                    //主火花粒子：偏亮金橘调（近似火星）
                    for (int c = 0; c < particleAmt; c++) {
                        float lerpFactor = c / (float)(particleAmt - 1);
                        Color sparkColor = Color.Lerp(new Color(255, 211, 120), new Color(255, 128, 0), lerpFactor); //金 -> 橙
                        Particle spark = new CritSpark(
                            Owner.Center,
                            Owner.velocity.RotatedByRandom(MathHelper.ToRadians(13f)) * Main.rand.NextFloat(-2.1f, -4.5f),
                            Color.White, sparkColor, 2f, 45, 2.25f, 2f);
                        GeneralParticleHandler.SpawnParticle(spark);
                    }

                    //辅助火星粒子：更小、更亮、更有“飞散感”
                    for (int e = 0; e < particleAmt * 2; e++) {
                        float lerpFactor = e / (float)(particleAmt * 2 - 1);
                        Color sparkColor2 = Color.Lerp(new Color(255, 238, 185), new Color(255, 140, 40), lerpFactor); //柔金 -> 橘棕
                        Particle spark2 = new NanoParticle(
                            Owner.Center,
                            Owner.velocity.RotatedByRandom(MathHelper.PiOver4) * Main.rand.NextFloat(2.5f, 4.5f),
                            sparkColor2, 1f, 45, Main.rand.NextBool(3));
                        GeneralParticleHandler.SpawnParticle(spark2);
                    }
                }
            }
            else if (Owner.CantUseHoldout() && Projectile.ai[1] < 1f) {
                FreezingLife = false;
                Idle?.Stop();

                Projectile.ai[1] = 1f;
                Projectile.timeLeft = Item.useAnimation;
                SoundStyle ShootSound = new("CalamityMod/Sounds/Item/SawShot", 2) { PitchVariance = 0.1f, Volume = 0.4f + SawPower * 0.5f };
                SoundEngine.PlaySound(ShootSound, TruePosition);

                float sawDamageMult = MathHelper.Lerp(1f, 5f, SawPower) / 2f;
                int sawPierce = (int)MathHelper.Lerp(2f, 7f, SawPower);
                int sawLevel = (SawPower >= 1f).ToInt() + (SawPower >= 0.25f).ToInt();

                Projectile.NewProjectile(Projectile.GetSource_FromThis(), TruePosition, Projectile.velocity.SafeNormalize(Vector2.UnitY) * SuperradiantSlaughterer.ShootSpeed, ModContent.ProjectileType<SuperradiantSawOverhaul>(), (int)(Projectile.damage * sawDamageMult), Projectile.knockBack, Projectile.owner, sawLevel, 0f, sawPierce);

                NoSawByHeld = true;
                FromArmLength -= 4f + 12f * SawPower;

                int sparkPairCount = 3 + 2 * sawLevel;
                for (int s = 0; s < sparkPairCount; s++) {
                    float velocityMult = Main.rand.NextFloat(5f, 8f) + Main.rand.NextFloat(4f, 7f) * sawLevel;
                    float scale = Main.rand.NextFloat(0.6f, 0.8f) + Main.rand.NextFloat(0.3f, 0.5f) * sawLevel;

                    //生成偏金黄至橘红色的火花颜色
                    float hue = Main.rand.NextFloat(0.05f, 0.12f); //金色至橙色
                    float saturation = Main.rand.NextFloat(0.85f, 1f); //高饱和
                    float lightness = Main.rand.NextFloat(0.6f, 0.8f); //中高亮度
                    Color color = Main.hslToRgb(hue, saturation, lightness);

                    Vector2 sparkVelocity = Projectile.velocity.RotatedByRandom(MathHelper.PiOver4) * velocityMult;
                    Particle weaponShootSparks = new AltLineParticle(TruePosition, sparkVelocity, false, 40, scale, color);
                    GeneralParticleHandler.SpawnParticle(weaponShootSparks);

                    sparkVelocity = Projectile.velocity.RotatedByRandom(MathHelper.PiOver4) * velocityMult;
                    Particle weaponShootSparks2 = new AltSparkParticle(TruePosition, sparkVelocity, false, 40, scale, color);
                    GeneralParticleHandler.SpawnParticle(weaponShootSparks2);
                }
            }

            if (NoSawByHeld) {
                Projectile.frame = 4;
                return;
            }
            else {
                Projectile.frameCounter++;
                if (Projectile.frameCounter >= 3) {
                    Projectile.frameCounter = 0;
                    Projectile.frame++;
                    if (Projectile.frame > 3)
                        Projectile.frame = 0;
                }
            }

            if (SawPower >= 1f) {
                if (TargetLargeSlashInstance == null) {
                    TargetLargeSlashInstance = new CircularSmearVFX(TruePosition, Color.Black, Time * -MathHelper.ToRadians(42f), 1.35f);
                    GeneralParticleHandler.SpawnParticle(TargetLargeSlashInstance);
                }
                else {
                    TargetLargeSlashInstance.Rotation = Time * -MathHelper.ToRadians(42f);
                    TargetLargeSlashInstance.Time = 0;
                    TargetLargeSlashInstance.Position = TruePosition;
                    TargetLargeSlashInstance.Scale = 1.35f;

                    //设置为金橘色火花效果（动态但统一色系）
                    float hue = Main.rand.NextFloat(0.06f, 0.10f); //金色-橙色之间
                    TargetLargeSlashInstance.Color = Main.hslToRgb(hue, 1f, 0.6f) * 0.8f;
                }
            }

            if (SawPower >= 0.25f) {
                if (TargetSmallSlashInstance == null) {
                    TargetSmallSlashInstance = new CircularSmearVFX(TruePosition, Color.Black, Time * MathHelper.ToRadians(42f), 0.8f);
                    GeneralParticleHandler.SpawnParticle(TargetSmallSlashInstance);
                }
                else {
                    TargetSmallSlashInstance.Rotation = Time * MathHelper.ToRadians(42f);
                    TargetSmallSlashInstance.Time = 0;
                    TargetSmallSlashInstance.Position = TruePosition;
                    TargetSmallSlashInstance.Scale = 0.8f;

                    //更柔和的金色火光
                    float hue = Main.rand.NextFloat(0.055f, 0.09f);
                    TargetSmallSlashInstance.Color = Main.hslToRgb(hue, 0.95f, 0.5f) * 0.65f;
                }
            }

            if (Time < MaxChargeTime) {
                if (Time == 30f && !NoSawByHeld) {
                    TargetSound = SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Custom/BuzzsawCharge") { Volume = 0.3f }, TruePosition);
                }
            }
            else {
                if ((Time + 240) % 360 == 0) {
                    TargetSound = SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Custom/BuzzsawIdle"), TruePosition);
                }

                if (Time % 3 == 0) {
                    Vector2 smokeVelocity = Vector2.UnitY * Main.rand.NextFloat(-7f, -12f);
                    smokeVelocity = smokeVelocity.RotatedByRandom(MathHelper.Pi / 8f);

                    //使用暗金+炽橘色调模拟热烟雾
                    float hue = Main.rand.NextFloat(0.07f, 0.11f); //金橘色区间
                    float lightness = Main.rand.NextFloat(0.3f, 0.5f);
                    Color smokeColor = Main.hslToRgb(hue, 0.9f, lightness);

                    Particle fullChargeSmoke = new HeavySmokeParticle(TruePosition + Main.rand.NextVector2CircularEdge(3f, 3f)
                        , smokeVelocity, smokeColor, 30, 0.65f, 0.5f, Main.rand.NextFloat(-0.2f, 0.2f), true);

                    GeneralParticleHandler.SpawnParticle(fullChargeSmoke);
                }
            }

        }

        public override void ModifyDamageHitbox(ref Rectangle hitbox) {
            hitbox = new Rectangle((int)TruePosition.X - 23, (int)TruePosition.Y - 23, 46, 46);
            if (Time / MaxChargeTime >= 1f) {
                hitbox.Inflate(72, 72);
            }
            else if (Time / MaxChargeTime >= 0.25f) {
                hitbox.Inflate(32, 32);
            }
        }

        public override void OnKill(int timeLeft) {
            if (SoundEngine.TryGetActiveSound(TargetSound, out var Idle)) {
                Idle?.Stop();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Laceration>(), 300);
            target.AddBuff(ModContent.BuffType<ElementalMix>(), 150);
            SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Custom/SwiftSlice") { Volume = 0.7f }, TruePosition);

            int SawLevel = (Time / MaxChargeTime >= 1f).ToInt() + (Time / MaxChargeTime >= 0.25f).ToInt();
            int onHitSparkAmount = 4 + 4 * SawLevel;

            for (int s = 0; s < onHitSparkAmount; s++) {
                Vector2 sparkVel = Main.rand.NextVector2CircularEdge(1f, 1f) * (Main.rand.NextFloat(6f, 10f) + 5f * SawLevel);
                float sparkSize = 0.4f + Main.rand.NextFloat(0.3f, 0.6f) * SawLevel;

                //火花颜色：色相范围约为橙到黄 (H 约 0.05f~0.12f)，高亮度
                float hue = Main.rand.NextFloat(0.05f, 0.12f);
                float saturation = Main.rand.NextFloat(0.8f, 1f);
                float lightness = Main.rand.NextFloat(0.6f, 0.85f);
                Color sparkColor = Main.hslToRgb(hue, saturation, lightness);

                Particle sparked = new AltLineParticle(target.Center, sparkVel, false, 20, sparkSize, sparkColor);
                GeneralParticleHandler.SpawnParticle(sparked);
            }

            for (int sq = 0; sq < 5; sq++) {
                Vector2 squareVel = Main.rand.NextVector2CircularEdge(1f, 1f) * (Main.rand.NextFloat(6f, 10f) + 5f * SawLevel);
                float squareSize = 1.6f + Main.rand.NextFloat(1f, 1.6f) * SawLevel;

                //方块火星颜色略暗些，偏红橙
                float hue = Main.rand.NextFloat(0.03f, 0.09f);
                float saturation = Main.rand.NextFloat(0.7f, 0.95f);
                float lightness = Main.rand.NextFloat(0.5f, 0.75f);
                Color squareColor = Main.hslToRgb(hue, saturation, lightness);

                Particle squared = new SquareParticle(target.Center, squareVel, true, 20, squareSize, squareColor);
                GeneralParticleHandler.SpawnParticle(squared);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D holdoutTexture = Holdout.Value;
            Texture2D largeSlashTexture = LargeSlash.Value;
            Texture2D smallSlashTexture = SmallSlash.Value;
            Color slashColor = new Color(200, 200, 200, 100);

            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle frame = holdoutTexture.Frame(verticalFrames: Main.projFrames[Type], frameY: Projectile.frame);
            float drawRotation = Projectile.rotation + (Projectile.spriteDirection == -1 ? MathHelper.Pi : 0f);
            Vector2 rotationPoint = frame.Size() * 0.5f;
            SpriteEffects flipSprite = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            if (!NoSawByHeld) {
                float shake = Utils.Remap(Time, 0f, MaxChargeTime, 0f, 3f);
                drawPosition += Main.rand.NextVector2Circular(shake, shake);
            }

            Texture2D mini = MiniSaw.Value;
            Vector2 verticalOffset = Vector2.UnitY.RotatedBy(Projectile.rotation);
            if (Math.Cos(Projectile.rotation) < 0f) {
                verticalOffset *= -1f;
            }

            Vector2 miniSawPosition = drawPosition - Vector2.UnitX.RotatedBy(Projectile.rotation) * Projectile.width * 0.125f + verticalOffset * 6f;
            Main.EntitySpriteDraw(mini, miniSawPosition, null, Color.White, Time * MathHelper.ToRadians(24f), mini.Size() * 0.5f, Projectile.scale, flipSprite);

            Main.EntitySpriteDraw(holdoutTexture, drawPosition, frame, Projectile.GetAlpha(lightColor), drawRotation, rotationPoint, Projectile.scale, flipSprite);

            Texture2D glow = HoldoutGlow.Value;
            Main.EntitySpriteDraw(glow, drawPosition, frame, Color.White, drawRotation, rotationPoint, Projectile.scale, flipSprite);

            if (NoSawByHeld) {
                return false;
            }

            if (Time <= 30f) {
                return false;
            }

            if (Time / MaxChargeTime >= 1f) {
                Main.EntitySpriteDraw(largeSlashTexture, TruePosition - Main.screenPosition, null
                    , slashColor, Time * -MathHelper.ToRadians(42f), largeSlashTexture.Size() * 0.5f, 1f, SpriteEffects.None);
            }

            if (Time / MaxChargeTime >= 0.25f) {
                Main.EntitySpriteDraw(smallSlashTexture, TruePosition - Main.screenPosition, null
                    , slashColor, Time * MathHelper.ToRadians(42f), smallSlashTexture.Size() * 0.5f, 1f, SpriteEffects.None);
            }

            if (!CalamityConfig.Instance.Afterimages) {
                return false;
            }

            for (int i = 1; i < 3; i++) {
                float intensity = MathHelper.Lerp(0.05f, 0.25f, 1f - i / 3f);
                if (Time / MaxChargeTime >= 1f) {
                    Main.EntitySpriteDraw(largeSlashTexture, TruePosition - Main.screenPosition, null
                        , slashColor * intensity, (Time - i) * -MathHelper.ToRadians(42f), largeSlashTexture.Size() * 0.5f, 1f, SpriteEffects.None);
                }
                if (Time / MaxChargeTime >= 0.25f) {
                    Main.EntitySpriteDraw(smallSlashTexture, TruePosition - Main.screenPosition, null
                        , slashColor * intensity, (Time - i) * MathHelper.ToRadians(42f), smallSlashTexture.Size() * 0.5f, 1f, SpriteEffects.None);
                }
            }

            return false;
        }
    }

    internal class SuperradiantSawOverhaul : BaseHeldProj
    {
        [VaultLoaden("@CalamityMod/Projectiles/Ranged/SuperradiantSawOutline")]
        public static Asset<Texture2D> SawOutline = null;
        [VaultLoaden("@CalamityMod/Projectiles/Ranged/SuperradiantSawSmallSlash")]
        public static Asset<Texture2D> SmallSlash = null;
        [VaultLoaden("@CalamityMod/Projectiles/Ranged/SuperradiantSawLargeSlash")]
        public static Asset<Texture2D> LargeSlash = null;
        public override string Texture => "CalamityMod/Projectiles/Ranged/SuperradiantSaw";
        public override LocalizedText DisplayName => ProjectileLoader.GetProjectile(ModContent.ProjectileType<SuperradiantSaw>()).DisplayName;
        public ref float SawPowerLevel => ref Projectile.ai[0];
        public ref float Time => ref Projectile.ai[1];
        public ref float PierceThreshold => ref Projectile.ai[2];
        public int HitLagTimer;
        public int HomingReturnTimer;
        public bool HasPowerBoost;
        public bool IsRecalling;
        public const int RecallWaitTime = 90;
        public const int MaxBoltPairCount = 7;
        public Particle TargetSmallSlashInstance;
        public Particle TargetLargeSlashInstance;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 600;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.Calamity().pointBlankShotDuration = CalamityGlobalProjectile.DefaultPointBlankDuration;
        }

        public override void AI() {
            if (Projectile.MaxUpdates > 1) {
                Projectile.MaxUpdates = 1;
            }

            Time++;
            Projectile.rotation += MathHelper.ToRadians(6f + 18f * SawPowerLevel);

            if (HitLagTimer > 0) {
                HitLagTimer--;
                if (HitLagTimer == 0)
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * SuperradiantSlaughterer.ShootSpeed;
            }

            HasPowerBoost = Owner.HasCooldown(SuperradiantSawBoost.ID);

            if (HasPowerBoost && !IsRecalling && Time > 30) {
                float homingTurnSpeed = 0.2f;
                Vector2 mouseDir = Projectile.SafeDirectionTo(InMousePos);
                Projectile.velocity = Projectile.velocity.ToRotation().AngleTowards(mouseDir.ToRotation(), homingTurnSpeed)
                    .ToRotationVector2() * SuperradiantSlaughterer.ShootSpeed;
            }

            if (HomingReturnTimer > 0 && HomingReturnTimer < RecallWaitTime) {
                HomingReturnTimer++;
            }

            if (IsRecalling) {
                HandleReturningBehavior();
            }
            else {
                if (Main.rand.NextBool()) {
                    Color trailColor = HasPowerBoost ? Color.LightGoldenrodYellow : new Color(110, 255, 60);
                    Dust trail = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.RainbowTorch,
                        Projectile.velocity.X * 0.05f, Projectile.velocity.Y * 0.05f, 150, trailColor, 1.2f);
                    trail.noGravity = true;
                }
            }

            if (HasPowerBoost) {
                UpdateEmpoweredEffects();
            }
        }

        private void HandleReturningBehavior() {
            Projectile.tileCollide = false;

            if (HomingReturnTimer < RecallWaitTime) {
                HomingReturnTimer = RecallWaitTime;
            }

            HomingReturnTimer++;

            if (HomingReturnTimer < RecallWaitTime + 30) {
                Projectile.velocity *= 0.95f;
            }
            else {
                if (HomingReturnTimer == RecallWaitTime + 30) {
                    int boltCount = Math.Min(Projectile.numHits, MaxBoltPairCount) * 2;
                    for (int b = 0; b < boltCount; b++) {
                        Vector2 boltVel = Main.rand.NextVector2Unit() * 9f;
                        if (Projectile.IsOwnedByLocalPlayer()) {
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, boltVel
                                , ModContent.ProjectileType<SuperradiantBolt>(), (int)(Projectile.damage * 0.5f), 0f, Main.myPlayer);
                        }
                    }

                    float sparkCount = 6f + 5f * SawPowerLevel;
                    for (int i = 0; i < sparkCount; i++) {
                        Vector2 sparkVel = Main.rand.NextVector2Unit() * (12f + 10f * SawPowerLevel);
                        float sparkScale = 1f + 0.25f * SawPowerLevel;
                        Particle sparkle = new CritSpark(Projectile.Center, sparkVel, Color.White, Color.Lime
                            , sparkScale, 30, 0.1f, sparkScale, Main.rand.NextFloat(0f, 0.01f));
                        GeneralParticleHandler.SpawnParticle(sparkle);
                    }
                }

                if (HomingReturnTimer % 9 == 0 && HasPowerBoost) {
                    Vector2 randVelocity = -Projectile.velocity.RotatedByRandom(MathHelper.Pi / 3f) * 0.5f;
                    if (Projectile.IsOwnedByLocalPlayer()) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, randVelocity
                            , ModContent.ProjectileType<SuperradiantBolt>(), (int)(Projectile.damage * 0.5f), 0f, Main.myPlayer);
                    }
                }

                Vector2 direction = Owner.Center - Projectile.Center;
                if (direction.Length() > 3000f) {
                    Projectile.Kill();
                    return;
                }

                direction.Normalize();
                float returnSpeed = (SuperradiantSlaughterer.ShootSpeed * 0.6f) + (0.05f * (HomingReturnTimer - 120));
                Vector2 targetVelocity = direction * returnSpeed;

                Projectile.velocity.X = targetVelocity.X;
                Projectile.velocity.Y = targetVelocity.Y;

                if (Projectile.IsOwnedByLocalPlayer() && Projectile.Hitbox.Intersects(Owner.Hitbox)) {
                    Projectile.Kill();
                }
            }
        }

        private void UpdateEmpoweredEffects() {
            if (SawPowerLevel >= 2f) {
                if (TargetLargeSlashInstance == null) {
                    TargetLargeSlashInstance = new CircularSmearVFX(Projectile.Center, Color.Black, Time * -Projectile.rotation, 1.35f);
                    GeneralParticleHandler.SpawnParticle(TargetLargeSlashInstance);
                }
                else {
                    TargetLargeSlashInstance.Rotation = -Projectile.rotation;
                    TargetLargeSlashInstance.Time = 0;
                    TargetLargeSlashInstance.Position = Projectile.Center;

                    float hue = Main.rand.NextFloat(0.06f, 0.10f); // 金橘色火花
                    TargetLargeSlashInstance.Color = Main.hslToRgb(hue, 1f, 0.6f) * 0.8f;
                }
            }

            if (SawPowerLevel >= 1f) {
                if (TargetSmallSlashInstance == null) {
                    TargetSmallSlashInstance = new CircularSmearVFX(Projectile.Center, Color.Black, Projectile.rotation, 0.8f);
                    GeneralParticleHandler.SpawnParticle(TargetSmallSlashInstance);
                }
                else {
                    TargetSmallSlashInstance.Rotation = Projectile.rotation;
                    TargetSmallSlashInstance.Time = 0;
                    TargetSmallSlashInstance.Position = Projectile.Center;

                    float hue = Main.rand.NextFloat(0.055f, 0.09f);
                    TargetSmallSlashInstance.Color = Main.hslToRgb(hue, 0.95f, 0.5f) * 0.65f;
                }
            }
        }

        public override void OnKill(int timeLeft) => SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Custom/CeramicImpact", 2), Projectile.Center);

        public override bool OnTileCollide(Vector2 oldVelocity) {
            int sparkCount = 6 + 5 * (int)SawPowerLevel;

            Vector2 collisionNormal = Vector2.Zero;
            if (Projectile.velocity.X != oldVelocity.X) {
                collisionNormal.X = oldVelocity.X < 0 ? 1 : -1;
            }

            if (Projectile.velocity.Y != oldVelocity.Y) {
                collisionNormal.Y = oldVelocity.Y < 0 ? 1 : -1;
            }

            Vector2 baseVelocity = collisionNormal * 6.5f;

            Vector2 sparkLocation = collisionNormal switch {
                { X: > 0 } => Projectile.Left,
                { X: < 0 } => Projectile.Right,
                { Y: > 0 } => Projectile.Top,
                { Y: < 0 } => Projectile.Bottom,
                _ => Projectile.Center
            };

            for (int s = 0; s < sparkCount; s++) {
                Vector2 randomVelocity = baseVelocity.RotatedByRandom(MathHelper.PiOver2);
                float velocityScale = Main.rand.NextFloat(0.8f, 1.2f) + Main.rand.NextFloat(0.2f, 0.6f) * SawPowerLevel;
                Vector2 sparkVelocity = randomVelocity * velocityScale;

                float scale = Main.rand.NextFloat(0.5f, 0.8f) + Main.rand.NextFloat(0.2f, 0.6f) * SawPowerLevel;
                Color sparkColor = Color.Lerp(new Color(255, 215, 100), new Color(255, 140, 20), Main.rand.NextFloat());

                Particle spark = new AltLineParticle(sparkLocation, sparkVelocity, false, 30, scale, sparkColor);
                GeneralParticleHandler.SpawnParticle(spark);
            }

            var sound = Main.zenithWorld ? SuperradiantSaw.TileCollideGFB : SoundID.Item178 with { Pitch = 0.1f * Projectile.numHits };
            SoundEngine.PlaySound(sound, Projectile.Center);

            if (collisionNormal.X != 0) {
                Projectile.velocity.X = -oldVelocity.X;
            }

            if (collisionNormal.Y != 0) {
                Projectile.velocity.Y = -oldVelocity.Y;
            }

            if (PierceThreshold > 0) {
                PierceThreshold--;
                Projectile.numHits++;
                if (PierceThreshold <= 0)
                    IsRecalling = true;
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Laceration>(), 180);
            target.AddBuff(ModContent.BuffType<ElementalMix>(), 90);
            SoundEngine.PlaySound(new SoundStyle("CalamityMod/Sounds/Custom/SwiftSlice") with { Pitch = 0.1f * Projectile.numHits }, Projectile.Center);

            //金色火花粒子（击中金属感）
            int onHitSparkAmount = 7 + 10 * (int)SawPowerLevel;
            for (int s = 0; s < onHitSparkAmount; s++) {
                Vector2 sparkVel = Projectile.velocity.RotatedByRandom(MathHelper.ToRadians(30f)) *
                                   (Main.rand.NextFloat(0.4f, 0.8f) + (Main.rand.NextFloat(0.4f, 0.6f) * SawPowerLevel));
                float sparkSize = 0.4f + Main.rand.NextFloat(0.3f, 0.6f) * SawPowerLevel;

                //使用暖金火花色
                Color sparkColor = Color.Lerp(new Color(255, 200, 80), new Color(255, 140, 40), Main.rand.NextFloat());

                Particle sparked = new AltLineParticle(target.Center, sparkVel, false, 30, sparkSize, sparkColor);
                GeneralParticleHandler.SpawnParticle(sparked);
            }

            //暗红色四方血溅粒子
            for (int sq = 0; sq < 7; sq++) {
                Vector2 squareVel = Main.rand.NextVector2CircularEdge(1f, 1f) *
                                    (Main.rand.NextFloat(10f, 16f) + 5f * SawPowerLevel);
                float squareSize = 1.6f + Main.rand.NextFloat(1f, 1.6f) * SawPowerLevel;

                //深血红色调
                Color squareColor = Color.Lerp(new Color(110, 0, 0), new Color(180, 30, 20), Main.rand.NextFloat());

                Particle squared = new SquareParticle(target.Center, squareVel, true, 30, squareSize, squareColor);
                GeneralParticleHandler.SpawnParticle(squared);
            }

            if (!IsRecalling && HitLagTimer == 0) {
                HitLagTimer = 5;
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) / SuperradiantSlaughterer.ShootSpeed;
            }

            if (Projectile.numHits < 1) {
                HomingReturnTimer = 1;
            }

            if (PierceThreshold > 0) {
                PierceThreshold--;
                if (PierceThreshold <= 0) {
                    IsRecalling = true;
                }
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (PierceThreshold <= 0) {
                modifiers.SourceDamage *= 0.33f;
            }
        }

        public override void ModifyDamageHitbox(ref Rectangle hitbox) {
            if (SawPowerLevel >= 2f) {
                hitbox.Inflate(72, 72);
            }
            else if (SawPowerLevel >= 1f) {
                hitbox.Inflate(32, 32);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D largeSlash = LargeSlash.Value;
            Texture2D smallSlash = SmallSlash.Value;
            Color slashColor = new Color(200, 200, 200, 100);

            if (SawPowerLevel >= 2f) {
                Main.EntitySpriteDraw(largeSlash, Projectile.Center - Main.screenPosition, null
                    , slashColor, -Projectile.rotation, largeSlash.Size() * 0.5f, 1f, SpriteEffects.None);

                if (Time % 4 == 0) {
                    Vector2 randomParticleOffset = new Vector2(Main.rand.NextFloat(-Projectile.width * 1.75f
                        , Projectile.width * 1.75f), Main.rand.NextFloat(-Projectile.width * 1.75f, Projectile.width * 1.75f));
                    float randomParticleScale = Main.rand.NextFloat(0.65f, 0.95f);

                    //金黄与火橙色混合（偏热火花）
                    Color bloomColor = Color.Lerp(new Color(255, 215, 100), new Color(255, 140, 40), MathF.Abs(MathF.Sin(Time * 0.1f)));

                    Particle bloomCircle = new BloomParticle(
                        Projectile.Center + randomParticleOffset, Projectile.velocity,
                        Main.rand.NextBool(3) ? Color.White : bloomColor,
                        randomParticleScale, randomParticleScale, 4, false);
                    GeneralParticleHandler.SpawnParticle(bloomCircle);
                }
            }

            if (SawPowerLevel >= 1f) {
                Main.EntitySpriteDraw(smallSlash, Projectile.Center - Main.screenPosition, null
                    , slashColor, Projectile.rotation, smallSlash.Size() * 0.5f, 1f, SpriteEffects.None);

                if (Time % 4 == 0) {
                    Vector2 randomParticleOffset = new Vector2(Main.rand.NextFloat(-Projectile.width, Projectile.width)
                        , Main.rand.NextFloat(-Projectile.width, Projectile.width));
                    float randomParticleScale = Main.rand.NextFloat(0.35f, 0.65f);

                    //同样使用金火色调，但较暗，表现出渐弱的余热
                    Color bloomColor = Color.Lerp(new Color(220, 170, 80), new Color(255, 110, 30), MathF.Abs(MathF.Cos(Time * 0.1f)));

                    Particle bloomCircle = new BloomParticle(
                        Projectile.Center + randomParticleOffset, Projectile.velocity * 0.6f,
                        Main.rand.NextBool(4) ? Color.White : bloomColor,
                        randomParticleScale, randomParticleScale, 4, false);
                    GeneralParticleHandler.SpawnParticle(bloomCircle);
                }
            }

            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, null
                , Color.White, Projectile.rotation, mainValue.Size() * 0.5f, 1f, SpriteEffects.None);

            if (HasPowerBoost) {
                Texture2D outline = SawOutline.Value;
                Main.EntitySpriteDraw(outline, Projectile.Center - Main.screenPosition, null
                    , Color.LightGoldenrodYellow, Projectile.rotation, outline.Size() * 0.5f, 1f, SpriteEffects.None);
            }

            if (!CalamityConfig.Instance.Afterimages) {
                return false;
            }

            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                float afterimageRot = Projectile.oldRot[i];

                Vector2 drawPos = Projectile.oldPos[i] + mainValue.Size() * 0.5f - Main.screenPosition;
                float intensity = MathHelper.Lerp(0.1f, 0.6f, 1f - i / (float)Projectile.oldPos.Length);

                Main.EntitySpriteDraw(mainValue, drawPos, null, lightColor * intensity
                    , afterimageRot, mainValue.Size() * 0.5f, 1f, SpriteEffects.None);

                if (SawPowerLevel >= 2f)
                    Main.EntitySpriteDraw(largeSlash, drawPos, null, slashColor * intensity
                        , -afterimageRot, largeSlash.Size() * 0.5f, 1f, SpriteEffects.None);
                if (SawPowerLevel >= 1f)
                    Main.EntitySpriteDraw(smallSlash, drawPos, null, slashColor * intensity
                        , afterimageRot, smallSlash.Size() * 0.5f, 1f, SpriteEffects.None);
            }
            return false;
        }
    }
}
