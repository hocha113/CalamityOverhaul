using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 猫鱼技能类，右键丢出活泼跳跃的猫鱼
    /// </summary>
    internal class FishCat : FishSkill
    {
        public static SoundStyle Sound => CWRSound.Hajm with {
            MaxInstances = 3,
        };
        public override int UnlockFishID => ItemID.Catfish;
        public override int DefaultCooldown => 60 * (10 - HalibutData.GetDomainLayer() / 2);
        public override int ResearchDuration => 60 * 22;
        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse != 2) {
                return null;
            }

            if (Cooldown > 0) {
                return false;
            }

            item.UseSound = null;
            Vector2 velocity = player.To(Main.MouseWorld).UnitVector() * 12f;
            Vector2 position = player.Center;
            ShootState shootState = player.GetShootState();
            var source = shootState.Source;
            int damage = shootState.WeaponDamage;
            float knockback = shootState.WeaponKnockback;

            SetCooldown();

            int catCount = 3 + HalibutData.GetDomainLayer();

            for (int i = 0; i < catCount; i++) {
                float throwAngle = velocity.ToRotation() + Main.rand.NextFloat(-0.5f, 0.5f);
                float throwSpeed = Main.rand.NextFloat(12f, 18f);
                Vector2 throwVelocity = throwAngle.ToRotationVector2() * throwSpeed;
                throwVelocity.Y -= Main.rand.NextFloat(4f, 7f);

                Projectile.NewProjectile(
                    source,
                    position,
                    throwVelocity,
                    ModContent.ProjectileType<CatfishLeaper>(),
                    (int)(damage * (1.5f + HalibutData.GetDomainLayer() * 0.3f)),
                    knockback * 2.2f,
                    player.whoAmI
                );
            }

            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.7f,
                Pitch = 0.4f
            }, position);

            SoundEngine.PlaySound(SoundID.Meowmere with {
                Volume = 0.5f,
                Pitch = 0.6f
            }, position);

            SpawnThrowEffect(position, velocity);

            return false;
        }

        private static void SpawnThrowEffect(Vector2 position, Vector2 direction) {
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = direction.SafeNormalize(Vector2.Zero).RotatedByRandom(0.6f) * Main.rand.NextFloat(4f, 10f);
                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(200, 200, 220),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                dust.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 猫鱼跳跃弹幕，模拟猫咪的灵活动作
    /// </summary>
    internal class CatfishLeaper : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Catfish;

        private enum CatState
        {
            Airborne,
            OnGround,
            Hunting,
            Spinning,
            Exploding
        }

        private CatState State {
            get => (CatState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float CatLife => ref Projectile.ai[1];
        private ref float TargetNPCID => ref Projectile.ai[2];

        private int groundTime = 0;
        private const int MinGroundTime = 5;
        private const int MaxGroundTime = 18;
        private const float JumpForce = 14f;
        private const float HuntJumpForce = 17f;

        private const float Gravity = 0.45f;
        private const float GroundFriction = 0.85f;
        private const float AirResistance = 0.985f;
        private const float MaxFallSpeed = 20f;

        private const float DetectionRange = 700f;
        private const float HuntRange = 450f;

        private float bodyRotation = 0f;
        private float spinRotation = 0f;
        private float spinSpeed = 0f;
        private float squashStretch = 1f;
        private int idleAnimTimer = 0;
        private bool isSpinning = false;

        private const int MaxLifeTime = 540;
        private const int ExplosionRadius = 155;

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage /= 2f;
            }
        }

        public override void AI() {
            CatLife++;

            switch (State) {
                case CatState.Airborne:
                    AirbornePhaseAI();
                    break;
                case CatState.OnGround:
                    OnGroundPhaseAI();
                    break;
                case CatState.Hunting:
                    HuntingPhaseAI();
                    break;
                case CatState.Spinning:
                    SpinningPhaseAI();
                    break;
                case CatState.Exploding:
                    ExplodingPhaseAI();
                    break;
            }

            UpdateCatAnimation();

            float lightIntensity = 0.65f;
            Lighting.AddLight(Projectile.Center,
                1.0f * lightIntensity,
                0.8f * lightIntensity,
                0.6f * lightIntensity);

            if (Projectile.timeLeft <= 35 && State != CatState.Exploding) {
                State = CatState.Exploding;
            }
        }

        private void AirbornePhaseAI() {
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }

            Projectile.velocity.X *= AirResistance;

            if (Projectile.velocity.LengthSquared() > 9f) {
                bodyRotation = MathHelper.Lerp(bodyRotation, Projectile.velocity.Y * 0.04f, 0.25f);
            }

            if (isSpinning) {
                spinRotation += spinSpeed;
                spinSpeed *= 0.96f;

                if (Math.Abs(spinSpeed) < 0.1f) {
                    isSpinning = false;
                    spinRotation = 0f;
                }
            }

            if (CatLife % 4 == 0) {
                SpawnAirborneParticle();
            }
        }

        private void OnGroundPhaseAI() {
            groundTime++;

            Projectile.velocity.X *= GroundFriction;
            Projectile.velocity.Y = 0;

            bodyRotation = MathHelper.Lerp(bodyRotation, 0, 0.35f);

            NPC target = FindNearestEnemy();

            if (target != null) {
                TargetNPCID = target.whoAmI;
                State = CatState.Hunting;
                groundTime = 0;
                return;
            }

            int jumpTime = Main.rand.Next(MinGroundTime, MaxGroundTime);
            if (groundTime >= jumpTime) {
                PerformJump(false);
                groundTime = 0;

                if (Main.rand.NextBool(3)) {
                    InitiateSpin();
                }

                SoundEngine.PlaySound(FishCat.Sound with {
                    Volume = 0.35f,
                    Pitch = 0.6f
                }, Projectile.Center);
            }

            if (CatLife % 18 == 0) {
                SpawnIdleParticle();
            }
        }

        private void HuntingPhaseAI() {
            groundTime++;

            Projectile.velocity.X *= GroundFriction;
            Projectile.velocity.Y = 0;

            if (!IsTargetValid()) {
                State = CatState.OnGround;
                groundTime = 0;
                return;
            }

            NPC target = Main.npc[(int)TargetNPCID];

            float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);

            if (distanceToTarget > HuntRange) {
                State = CatState.OnGround;
                groundTime = 0;
                return;
            }

            Vector2 toTarget = target.Center - Projectile.Center;

            int huntJumpTime = Main.rand.Next(3, 12);
            if (groundTime >= huntJumpTime) {
                float horizontalSpeed = Math.Abs(toTarget.X) < 80f ? 7f : 11f;
                Projectile.velocity.X = Math.Sign(toTarget.X) * horizontalSpeed;
                Projectile.velocity.Y = -HuntJumpForce;

                if (toTarget.Y < -120f) {
                    Projectile.velocity.Y -= 3f;
                }

                State = CatState.Airborne;
                groundTime = 0;

                if (Main.rand.NextBool(2)) {
                    InitiateSpin();
                }

                SoundEngine.PlaySound(FishCat.Sound with {
                    Volume = 0.45f,
                    Pitch = 0.8f
                }, Projectile.Center);

                SpawnJumpParticle();
            }
        }

        private void SpinningPhaseAI() {
            if (State == CatState.Airborne) {
                AirbornePhaseAI();
            }
        }

        private void ExplodingPhaseAI() {
            Projectile.velocity *= 0.88f;

            squashStretch = 1f + (float)Math.Sin(CatLife * 1.2f) * 0.35f;

            if (Projectile.timeLeft % 2 == 0) {
                SpawnExplosionWarning();
            }
        }

        private void PerformJump(bool isHunt) {
            float jumpPower = isHunt ? HuntJumpForce : JumpForce;

            float horizontalSpeed = Main.rand.NextFloat(4f, 9f) * (Main.rand.NextBool() ? 1 : -1);

            Projectile.velocity.X = horizontalSpeed;
            Projectile.velocity.Y = -jumpPower;

            State = CatState.Airborne;

            SpawnJumpParticle();
        }

        private void InitiateSpin() {
            isSpinning = true;
            spinSpeed = Main.rand.NextFloat(0.4f, 0.8f) * (Main.rand.NextBool() ? 1 : -1);
            State = CatState.Spinning;
        }

        private NPC FindNearestEnemy() {
            NPC closest = null;
            float closestDist = DetectionRange;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        private bool IsTargetValid() {
            int targetID = (int)TargetNPCID;
            if (targetID < 0 || targetID >= Main.maxNPCs) return false;

            NPC target = Main.npc[targetID];
            return target.active && target.CanBeChasedBy();
        }

        private void UpdateCatAnimation() {
            idleAnimTimer++;

            if (State == CatState.Airborne || State == CatState.Spinning) {
                float speedRatio = Math.Abs(Projectile.velocity.Y) / MaxFallSpeed;
                float targetSquash = MathHelper.Lerp(1f, 1.4f, speedRatio);
                squashStretch = MathHelper.Lerp(squashStretch, targetSquash, 0.25f);
            }
            else if (State == CatState.OnGround) {
                if (groundTime < 4) {
                    squashStretch = MathHelper.Lerp(squashStretch, 0.65f, 0.35f);
                }
                else {
                    float breathe = (float)Math.Sin(idleAnimTimer * 0.12f) * 0.06f;
                    squashStretch = MathHelper.Lerp(squashStretch, 1f + breathe, 0.12f);
                }
            }
            else if (State == CatState.Hunting) {
                float tension = (float)Math.Sin(idleAnimTimer * 0.2f) * 0.1f;
                squashStretch = MathHelper.Lerp(squashStretch, 1f + tension, 0.18f);
            }
        }

        private void SpawnAirborneParticle() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.Smoke,
                -Projectile.velocity * 0.25f,
                100,
                new Color(220, 200, 180),
                Main.rand.NextFloat(0.9f, 1.4f)
            );
            trail.noGravity = true;
        }

        private void SpawnIdleParticle() {
            Dust idle = Dust.NewDustPerfect(
                Projectile.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), 12f),
                DustID.Smoke,
                new Vector2(0, -0.6f),
                100,
                new Color(230, 210, 190),
                Main.rand.NextFloat(0.7f, 1.1f)
            );
            idle.noGravity = true;
            idle.fadeIn = 0.9f;
        }

        private void SpawnJumpParticle() {
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(1.5f, 5f));
                Dust jump = Dust.NewDustPerfect(
                    Projectile.Bottom,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(200, 180, 160),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                jump.noGravity = false;
            }
        }

        private void SpawnExplosionWarning() {
            for (int i = 0; i < 4; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                Dust warning = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 18f),
                    DustID.Torch,
                    velocity,
                    100,
                    new Color(255, 200, 100),
                    Main.rand.NextFloat(1.4f, 2.3f)
                );
                warning.noGravity = true;
                warning.fadeIn = 1.4f;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == CatState.Airborne && Projectile.velocity.Y == 0) {
                State = CatState.OnGround;
                groundTime = 0;

                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.35f,
                    Pitch = 0.6f
                }, Projectile.Center);

                for (int i = 0; i < 6; i++) {
                    Dust land = Dust.NewDustDirect(
                        Projectile.Bottom - new Vector2(0, 6),
                        Projectile.width,
                        6,
                        DustID.Smoke,
                        Scale: Main.rand.NextFloat(1.2f, 1.8f)
                    );
                    land.velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-1.5f, 1.5f));
                }

                return false;
            }

            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.65f;
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 3);
            State = CatState.Exploding;
        }

        public override void OnKill(int timeLeft) {
            CreateCatExplosion();

            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.75f,
                Pitch = 0.25f
            }, Projectile.Center);

            SoundEngine.PlaySound(SoundID.Meowmere with {
                Volume = 0.6f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        private void CreateCatExplosion() {
            Projectile.Explode(ExplosionRadius, default, false);
            int particleCount = 50 + HalibutData.GetDomainLayer() * 6;
            for (int i = 0; i < particleCount; i++) {
                float angle = MathHelper.TwoPi * i / particleCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(12f, 22f);

                Dust explosion = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    velocity,
                    100,
                    new Color(255, 220, 150),
                    Main.rand.NextFloat(2.2f, 3.8f)
                );
                explosion.noGravity = Main.rand.NextBool();
                explosion.fadeIn = 1.5f;
            }

            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 22; j++) {
                    float angle = MathHelper.TwoPi * j / 22f;
                    float radius = 30f + i * 35f;
                    Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * radius;

                    Dust ring = Dust.NewDustPerfect(
                        spawnPos,
                        DustID.Smoke,
                        Vector2.Zero,
                        100,
                        new Color(160, 140, 120),
                        Main.rand.NextFloat(2f, 3.5f)
                    );
                    ring.velocity = angle.ToRotationVector2() * 7f;
                    ring.noGravity = true;
                }
            }

            for (int i = 0; i < 18; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(16f, 16f);
                Dust chunk = Dust.NewDustDirect(
                    Projectile.Center,
                    5, 5,
                    DustID.Torch,
                    0, 0, 100,
                    new Color(255, 200, 100),
                    Main.rand.NextFloat(1.8f, 2.8f)
                );
                chunk.velocity = velocity;
                chunk.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D catTex = TextureAssets.Item[ItemID.Catfish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            SpriteEffects effects = SpriteEffects.None;
            if (Projectile.velocity.X < 0) {
                effects = SpriteEffects.FlipHorizontally;
            }

            Vector2 scale = new Vector2(Projectile.scale / squashStretch, Projectile.scale * squashStretch);

            Color drawColor = Projectile.GetAlpha(lightColor);

            if (State == CatState.Exploding) {
                float flash = (float)Math.Sin(CatLife * 1.8f) * 0.5f + 0.5f;
                drawColor = Color.Lerp(drawColor, new Color(255, 150, 50), flash * 0.75f);
            }

            for (int i = 0; i < 3; i++) {
                float shadowOffset = (3 - i) * 2.5f;
                Vector2 shadowPos = drawPos + new Vector2(0, shadowOffset);
                Color shadowColor = new Color(0, 0, 0, 90) * (1f - i * 0.3f);

                sb.Draw(
                    catTex,
                    shadowPos,
                    null,
                    shadowColor,
                    bodyRotation + spinRotation,
                    catTex.Size() / 2f,
                    scale * 0.96f,
                    effects,
                    0
                );
            }

            sb.Draw(
                catTex,
                drawPos,
                null,
                drawColor,
                bodyRotation + spinRotation,
                catTex.Size() / 2f,
                scale,
                effects,
                0
            );

            if (State == CatState.Hunting || State == CatState.Exploding) {
                Color glowColor = State == CatState.Exploding
                    ? new Color(255, 100, 20) * 0.85f
                    : new Color(255, 220, 180) * 0.65f;

                sb.Draw(
                    catTex,
                    drawPos,
                    null,
                    glowColor,
                    bodyRotation + spinRotation,
                    catTex.Size() / 2f,
                    scale * 1.06f,
                    effects,
                    0
                );
            }

            return false;
        }
    }
}
