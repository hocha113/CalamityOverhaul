using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishDirt : FishSkill
    {
        public override int UnlockFishID => ItemID.Dirtfish;
        public override int DefaultCooldown => 60;
        public override int ResearchDuration => 60 * 20;
        private static int MaxDirtFish => 5 + HalibutData.GetDomainLayer();
        private static int FishPerDomainLayer => 1 + HalibutData.GetDomainLayer() / 5;
        private int spawnTimer = 0;
        private const int SpawnInterval = 20;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int fishCount = CountActiveDirtFish(player);
            int requiredFish = 5 + HalibutData.GetDomainLayer();

            if (fishCount >= requiredFish && !HasActiveDirtBall(player) && !HasGatheringFish(player)) {
                GatherAndShootDirtBall(player, source, damage, knockback, velocity);
            }

            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (Active(player)) {
                spawnTimer++;

                int currentCount = CountActiveDirtFish(player);
                int maxCount = MaxDirtFish + HalibutData.GetDomainLayer() * FishPerDomainLayer;

                if (spawnTimer >= SpawnInterval && currentCount < maxCount && player.velocity.LengthSquared() > 1f) {
                    SpawnDirtFish(player);
                    spawnTimer = 0;
                }
            }
            return true;
        }

        private static void SpawnDirtFish(Player player) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(250f, 400f);
            Vector2 spawnPos = player.Center + angle.ToRotationVector2() * distance;

            Vector2 velocity = (player.Center - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 7f);

            Projectile.NewProjectile(
                player.GetSource_FromThis(),
                spawnPos,
                velocity,
                ModContent.ProjectileType<DirtFishFollower>(),
                0,
                0f,
                player.whoAmI
            );

            SoundEngine.PlaySound(SoundID.Item8 with {
                Volume = 0.3f,
                Pitch = 0.2f
            }, spawnPos);

            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(
                    spawnPos - new Vector2(10, 10),
                    20, 20,
                    DustID.Dirt,
                    Scale: Main.rand.NextFloat(1.2f, 1.8f)
                );
                dust.velocity = Main.rand.NextVector2Circular(3f, 3f);
                dust.noGravity = Main.rand.NextBool();
            }
        }

        private static int CountActiveDirtFish(Player player) {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI &&
                    proj.type == ModContent.ProjectileType<DirtFishFollower>()) {
                    count++;
                }
            }
            return count;
        }

        private static bool HasActiveDirtBall(Player player) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI &&
                    proj.type == ModContent.ProjectileType<DirtBall>()) {
                    return true;
                }
            }
            return false;
        }

        private static bool HasGatheringFish(Player player) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI &&
                    proj.type == ModContent.ProjectileType<DirtFishFollower>() &&
                    proj.ModProjectile is DirtFishFollower follower) {
                    if (follower.State != DirtFishFollower.FishState.Following) {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void GatherAndShootDirtBall(Player player, EntitySource_ItemUse_WithAmmo source,
            int damage, float knockback, Vector2 targetVelocity) {
            List<int> fishIndices = new List<int>();

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI &&
                    proj.type == ModContent.ProjectileType<DirtFishFollower>() &&
                    proj.ModProjectile is DirtFishFollower follower &&
                    follower.State == DirtFishFollower.FishState.Following) {
                    fishIndices.Add(i);
                }
            }

            if (fishIndices.Count == 0) return;

            foreach (int index in fishIndices) {
                if (Main.projectile[index].ModProjectile is DirtFishFollower follower) {
                    follower.StartGathering(targetVelocity, damage, knockback, source);
                }
            }

            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.6f,
                Pitch = -0.3f
            }, player.Center);
        }
    }

    /// <summary>
    /// 土鱼跟随者弹幕
    /// </summary>
    internal class DirtFishFollower : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Dirtfish;

        public enum FishState
        {
            Following,
            Gathering,
            MovingToGather,
            Converging
        }

        public FishState State {
            get => (FishState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float LifeTimer => ref Projectile.ai[1];
        private ref float GatherTimer => ref Projectile.ai[2];

        private Vector2 boidVelocity = Vector2.Zero;
        private float wigglePhase = 0f;
        private float orbitAngle = 0f;
        private float orbitRadius = 0f;
        private Vector2 storedShootVelocity = Vector2.Zero;
        private int storedDamage = 0;
        private float storedKnockback = 0f;
        private EntitySource_ItemUse_WithAmmo storedSource = null;

        private const float SeparationRadius = 80f;
        private const float AlignmentRadius = 140f;
        private const float CohesionRadius = 180f;
        private const float MaxSpeed = 11f;
        private const float MaxForce = 0.5f;

        private const float PlayerFollowWeight = 1.8f;
        private const float SeparationWeight = 2.4f;
        private const float AlignmentWeight = 0.9f;
        private const float CohesionWeight = 0.8f;
        private const float OrbitWeight = 1.5f;

        private const float MinOrbitRadius = 120f;
        private const float MaxOrbitRadius = 220f;

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 60 * 5;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            wigglePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            orbitRadius = Main.rand.NextFloat(MinOrbitRadius, MaxOrbitRadius);
        }

        public void StartGathering(Vector2 shootVelocity, int damage, float knockback, EntitySource_ItemUse_WithAmmo source) {
            State = FishState.MovingToGather;
            GatherTimer = 0;
            storedShootVelocity = shootVelocity;
            storedDamage = damage;
            storedKnockback = knockback;
            storedSource = source;
        }

        public override void AI() {
            LifeTimer++;
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            switch (State) {
                case FishState.Following:
                    FollowingAI(owner);
                    break;
                case FishState.MovingToGather:
                    MovingToGatherAI(owner);
                    break;
                case FishState.Gathering:
                    GatheringAI(owner);
                    break;
                case FishState.Converging:
                    ConvergingAI(owner);
                    break;
            }

            Projectile.rotation = Projectile.velocity.X * 0.05f;

            wigglePhase += 0.15f;

            Lighting.AddLight(Projectile.Center, 0.3f, 0.25f, 0.2f);
        }

        private void FollowingAI(Player owner) {
            Vector2 steeringForce = Vector2.Zero;

            Vector2 playerRelativeVel = owner.velocity;
            float anticipationFactor = Math.Min(playerRelativeVel.Length() / 10f, 2.5f);

            orbitAngle += 0.012f + anticipationFactor * 0.008f;
            Vector2 orbitOffset = new Vector2(
                (float)Math.Cos(orbitAngle) * orbitRadius,
                (float)Math.Sin(orbitAngle) * orbitRadius * 0.7f
            );

            Vector2 targetPos = owner.Center + playerRelativeVel * anticipationFactor * 6f + orbitOffset;
            Vector2 toTarget = targetPos - Projectile.Center;
            float distanceToPlayer = Vector2.Distance(Projectile.Center, owner.Center);

            Vector2 orbitForce = Seek(targetPos, distanceToPlayer > 300f ? 1.8f : 1.2f);
            steeringForce += orbitForce * OrbitWeight;

            Vector2 playerForce = Seek(owner.Center + playerRelativeVel * 10f, distanceToPlayer > 350f ? 1.5f : 0.6f);
            steeringForce += playerForce * PlayerFollowWeight;

            Vector2 separation = CalculateSeparation();
            steeringForce += separation * SeparationWeight;

            Vector2 alignment = CalculateAlignment();
            steeringForce += alignment * AlignmentWeight;

            Vector2 cohesion = CalculateCohesion();
            steeringForce += cohesion * CohesionWeight;

            ApplySteering(steeringForce);

            if (distanceToPlayer > 900f) {
                orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                orbitRadius = Main.rand.NextFloat(MinOrbitRadius, MaxOrbitRadius);
                Projectile.Center = owner.Center + Main.rand.NextVector2Circular(200f, 200f);
                Projectile.velocity = owner.velocity + Main.rand.NextVector2Circular(3f, 3f);
            }

            if (LifeTimer % 60 == 0) {
                SpawnFollowDust();
            }
        }

        private void MovingToGatherAI(Player owner) {
            GatherTimer++;

            Vector2 gatherPoint = owner.Center + new Vector2(0, -120f);
            Vector2 toGatherPoint = gatherPoint - Projectile.Center;
            float distance = toGatherPoint.Length();

            if (distance > 10f) {
                Vector2 desired = toGatherPoint.SafeNormalize(Vector2.Zero) * MaxSpeed * 2.2f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.15f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            if (GatherTimer > 40 && distance < 60f) {
                State = FishState.Gathering;
                GatherTimer = 0;
            }

            if (GatherTimer % 3 == 0) {
                SpawnGatherDust();
            }
        }

        private void GatheringAI(Player owner) {
            GatherTimer++;

            Vector2 gatherCenter = owner.Center + new Vector2(0, -120f);
            Vector2 toCenter = gatherCenter - Projectile.Center;
            float distance = toCenter.Length();

            if (GatherTimer < 30) {
                Vector2 repel = -toCenter.SafeNormalize(Vector2.Zero) * 10f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, repel, 0.18f);
            }
            else if (GatherTimer < 75) {
                Projectile.velocity *= 0.85f;

                Vector2 offset = new Vector2(
                    (float)Math.Cos(wigglePhase + Projectile.whoAmI) * 18f,
                    (float)Math.Sin(wigglePhase * 0.7f + Projectile.whoAmI) * 18f
                );
                Vector2 orbitPos = gatherCenter + offset;
                Vector2 toOrbit = orbitPos - Projectile.Center;
                Projectile.velocity += toOrbit * 0.025f;
            }
            else {
                State = FishState.Converging;
                GatherTimer = 0;
            }

            if (GatherTimer % 2 == 0) {
                SpawnGatherDust();
            }
        }

        private void ConvergingAI(Player owner) {
            GatherTimer++;

            Vector2 ballCenter = owner.Center + new Vector2(0, -80f);
            Vector2 toCenter = ballCenter - Projectile.Center;
            float distance = toCenter.Length();

            if (distance > 13f) {
                Vector2 desired = toCenter.SafeNormalize(Vector2.Zero) * MaxSpeed * 2.5f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.3f);
            }
            else {
                Projectile.velocity *= 0.85f;

                if (GatherTimer > 20 && Projectile.IsOwnedByLocalPlayer()) {
                    int convergingCount = 0;
                    int arrivedCount = 0;

                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        Projectile proj = Main.projectile[i];
                        if (proj.active && proj.owner == owner.whoAmI &&
                            proj.type == Projectile.type &&
                            proj.ModProjectile is DirtFishFollower follower &&
                            follower.State == FishState.Converging) {
                            convergingCount++;
                            Vector2 toBall = ballCenter - proj.Center;
                            if (toBall.Length() < 15f) {
                                arrivedCount++;
                            }
                        }
                    }

                    if (convergingCount > 0 && arrivedCount >= convergingCount) {
                        int existingBalls = 0;
                        for (int i = 0; i < Main.maxProjectiles; i++) {
                            if (Main.projectile[i].active &&
                                Main.projectile[i].owner == owner.whoAmI &&
                                Main.projectile[i].type == ModContent.ProjectileType<DirtBall>()) {
                                existingBalls++;
                            }
                        }

                        if (existingBalls == 0 && storedSource != null) {
                            Vector2 shootDirection = storedShootVelocity.SafeNormalize(Vector2.Zero);

                            Projectile.NewProjectile(
                                storedSource,
                                ballCenter,
                                shootDirection * 16f,
                                ModContent.ProjectileType<DirtBall>(),
                                (int)(storedDamage * (2.7f + HalibutData.GetDomainLayer() * 0.55f)),
                                storedKnockback * 1.8f,
                                owner.whoAmI,
                                convergingCount
                            );

                            SoundEngine.PlaySound(SoundID.Item92 with {
                                Volume = 0.8f,
                                Pitch = -0.4f
                            }, ballCenter);

                            for (int i = 0; i < Main.maxProjectiles; i++) {
                                Projectile proj = Main.projectile[i];
                                if (proj.active && proj.owner == owner.whoAmI &&
                                    proj.type == Projectile.type &&
                                    proj.ModProjectile is DirtFishFollower follower &&
                                    follower.State == FishState.Converging) {
                                    proj.Kill();
                                }
                            }
                            return;
                        }
                    }
                }
            }

            SpawnConvergeDust();
        }

        private Vector2 Seek(Vector2 target, float speedMultiplier = 1f) {
            Vector2 desired = (target - Projectile.Center).SafeNormalize(Vector2.Zero) * MaxSpeed * speedMultiplier;
            Vector2 steer = desired - Projectile.velocity;
            return LimitVector(steer, MaxForce);
        }

        private Vector2 CalculateSeparation() {
            Vector2 steer = Vector2.Zero;
            int count = 0;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (i != Projectile.whoAmI && other.active &&
                    other.type == Projectile.type && other.owner == Projectile.owner) {

                    float distance = Vector2.Distance(Projectile.Center, other.Center);

                    if (distance > 0 && distance < SeparationRadius) {
                        Vector2 diff = (Projectile.Center - other.Center).SafeNormalize(Vector2.Zero);
                        diff /= distance;
                        steer += diff;
                        count++;
                    }
                }
            }

            if (count > 0) {
                steer /= count;
                steer = steer.SafeNormalize(Vector2.Zero) * MaxSpeed;
                steer -= Projectile.velocity;
                steer = LimitVector(steer, MaxForce);
            }

            return steer;
        }

        private Vector2 CalculateAlignment() {
            Vector2 sum = Vector2.Zero;
            int count = 0;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (i != Projectile.whoAmI && other.active &&
                    other.type == Projectile.type && other.owner == Projectile.owner) {

                    float distance = Vector2.Distance(Projectile.Center, other.Center);

                    if (distance > 0 && distance < AlignmentRadius) {
                        sum += other.velocity;
                        count++;
                    }
                }
            }

            if (count > 0) {
                sum /= count;
                sum = sum.SafeNormalize(Vector2.Zero) * MaxSpeed;
                Vector2 steer = sum - Projectile.velocity;
                return LimitVector(steer, MaxForce);
            }

            return Vector2.Zero;
        }

        private Vector2 CalculateCohesion() {
            Vector2 sum = Vector2.Zero;
            int count = 0;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (i != Projectile.whoAmI && other.active &&
                    other.type == Projectile.type && other.owner == Projectile.owner) {

                    float distance = Vector2.Distance(Projectile.Center, other.Center);

                    if (distance > 0 && distance < CohesionRadius) {
                        sum += other.Center;
                        count++;
                    }
                }
            }

            if (count > 0) {
                sum /= count;
                return Seek(sum, 0.7f);
            }

            return Vector2.Zero;
        }

        private void ApplySteering(Vector2 force) {
            boidVelocity += force;
            boidVelocity = LimitVector(boidVelocity, MaxSpeed);

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, boidVelocity, 0.25f);
            Projectile.velocity = LimitVector(Projectile.velocity, MaxSpeed);
        }

        private Vector2 LimitVector(Vector2 vec, float max) {
            if (vec.LengthSquared() > max * max) {
                return vec.SafeNormalize(Vector2.Zero) * max;
            }
            return vec;
        }

        private void SpawnFollowDust() {
            Dust dust = Dust.NewDustDirect(
                Projectile.position,
                Projectile.width,
                Projectile.height,
                DustID.Dirt,
                Scale: Main.rand.NextFloat(0.8f, 1.2f)
            );
            dust.velocity = -Projectile.velocity * 0.3f;
            dust.noGravity = true;
            dust.fadeIn = 0.8f;
        }

        private void SpawnGatherDust() {
            for (int i = 0; i < 2; i++) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.Center - new Vector2(5, 5),
                    10, 10,
                    DustID.Dirt,
                    Scale: Main.rand.NextFloat(1.2f, 1.6f)
                );
                dust.velocity = Main.rand.NextVector2Circular(2f, 2f);
                dust.noGravity = Main.rand.NextBool();
            }
        }

        private void SpawnConvergeDust() {
            Dust dust = Dust.NewDustDirect(
                Projectile.Center - new Vector2(8, 8),
                16, 16,
                DustID.Dirt,
                Scale: Main.rand.NextFloat(1.4f, 2f)
            );
            dust.velocity = -Projectile.velocity * 0.5f;
            dust.noGravity = true;
            dust.fadeIn = 1.2f;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Item[ItemID.Dirtfish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            SpriteEffects effects = Projectile.velocity.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            float wiggleOffset = (float)Math.Sin(wigglePhase) * 3f;
            Vector2 wigglePos = drawPos + new Vector2(wiggleOffset, 0);

            float scaleModifier = 1f + (float)Math.Sin(LifeTimer * 0.1f) * 0.08f;

            Color drawColor = Projectile.GetAlpha(lightColor);

            if (State == FishState.Gathering || State == FishState.MovingToGather || State == FishState.Converging) {
                float glowPulse = 0.5f + (float)Math.Sin(GatherTimer * 0.3f) * 0.5f;
                drawColor = Color.Lerp(drawColor, new Color(160, 140, 100), glowPulse * 0.6f);
            }

            sb.Draw(
                texture,
                wigglePos,
                null,
                drawColor,
                Projectile.rotation,
                texture.Size() / 2f,
                Projectile.scale * scaleModifier,
                effects,
                0
            );

            return false;
        }
    }

    /// <summary>
    /// 土球弹幕，由土鱼聚合而成
    /// </summary>
    internal class DirtBall : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float FishCount => ref Projectile.ai[0];
        private ref float BounceCount => ref Projectile.ai[1];

        private bool isRolling = false;
        private float ballRotation = 0f;
        private const float Gravity = 0.4f;
        private const float BounceDecay = 0.65f;
        private const float MinBounceVelocity = 3f;
        private const int MaxBounces = 5;

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 16;
            Projectile.tileCollide = true;

            if (FishCount == 0) FishCount = 10;
        }

        public override void AI() {
            if (!isRolling) {
                Projectile.velocity.Y += Gravity;
                Projectile.velocity *= 0.99f;
            }
            else {
                Projectile.velocity.X *= 0.97f;

                if (Math.Abs(Projectile.velocity.X) < 0.5f) {
                    Projectile.velocity.X = 0;
                }
            }

            if (Projectile.velocity.LengthSquared() > 0.1f) {
                ballRotation += Projectile.velocity.X * 0.05f;
            }

            if (isRolling && Main.rand.NextBool(3)) {
                SpawnRollingDust();
            }

            Lighting.AddLight(Projectile.Center, 0.4f, 0.35f, 0.25f);
        }

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac) {
            width = height = 60;
            return base.TileCollideStyle(ref width, ref height, ref fallThrough, ref hitboxCenterFrac);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            bool hitGround = Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.1f && oldVelocity.Y > 0;

            if (hitGround) {
                BounceCount++;

                if (BounceCount >= MaxBounces || Math.Abs(oldVelocity.Y) < MinBounceVelocity) {
                    isRolling = true;
                    Projectile.velocity.Y = 0;
                    Projectile.tileCollide = false;

                    SoundEngine.PlaySound(SoundID.Dig with {
                        Volume = 0.7f,
                        Pitch = -0.2f
                    }, Projectile.Center);
                }
                else {
                    Projectile.velocity.Y = -oldVelocity.Y * BounceDecay;

                    SoundEngine.PlaySound(SoundID.Item14 with {
                        Volume = 0.4f,
                        Pitch = 0.2f
                    }, Projectile.Center);

                    SpawnBounceDust();
                }
            }

            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.1f) {
                Projectile.velocity.X = -oldVelocity.X * 0.5f;

                SoundEngine.PlaySound(SoundID.Tink with {
                    Volume = 0.5f
                }, Projectile.Center);
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Confused, 60 * 3);

            for (int i = 0; i < 5; i++) {
                Dust dust = Dust.NewDustDirect(
                    target.position,
                    target.width,
                    target.height,
                    DustID.Dirt,
                    Scale: Main.rand.NextFloat(1.5f, 2.2f)
                );
                dust.velocity = Main.rand.NextVector2Circular(5f, 5f);
                dust.noGravity = Main.rand.NextBool();
            }
        }

        private void SpawnBounceDust() {
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = new Vector2(
                    Main.rand.NextFloat(-6f, 6f),
                    Main.rand.NextFloat(-8f, -3f)
                );

                Dust dust = Dust.NewDustDirect(
                    Projectile.Bottom - new Vector2(Projectile.width / 2, 10),
                    Projectile.width,
                    10,
                    DustID.Dirt,
                    Scale: Main.rand.NextFloat(1.5f, 2.5f)
                );
                dust.velocity = velocity;
                dust.noGravity = Main.rand.NextBool();
            }
        }

        private void SpawnRollingDust() {
            Vector2 spawnPos = Projectile.Bottom + new Vector2(
                Main.rand.NextFloat(-Projectile.width / 2, Projectile.width / 2),
                Main.rand.NextFloat(-5f, 5f)
            );

            Dust dust = Dust.NewDustDirect(
                spawnPos - new Vector2(4, 4),
                8, 8,
                DustID.Dirt,
                Scale: Main.rand.NextFloat(1.2f, 1.8f)
            );
            dust.velocity = new Vector2(-Projectile.velocity.X * 0.5f, Main.rand.NextFloat(-2f, -0.5f));
            dust.noGravity = Main.rand.NextBool(3);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = TextureAssets.Item[ItemID.Dirtfish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float ballSize = 0.8f + FishCount * 0.05f;
            int fishToDraw = (int)Math.Min(FishCount, 15);

            for (int i = 0; i < fishToDraw; i++) {
                float angleOffset = MathHelper.TwoPi * i / fishToDraw;
                float currentAngle = ballRotation + angleOffset;
                float radius = Projectile.width * 0.15f * ballSize;

                Vector2 fishPos = drawPos + currentAngle.ToRotationVector2() * radius;

                float fishRotation = currentAngle + MathHelper.PiOver2;

                Color fishColor = Projectile.GetAlpha(lightColor);
                fishColor *= 0.9f + (float)Math.Sin(currentAngle * 3f) * 0.1f;

                sb.Draw(
                    fishTex,
                    fishPos,
                    null,
                    fishColor,
                    fishRotation,
                    fishTex.Size() / 2f,
                    0.7f * ballSize,
                    SpriteEffects.None,
                    0
                );
            }

            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + ballRotation * 0.5f;
                Vector2 offset = angle.ToRotationVector2() * 3f;

                for (int j = 0; j < fishToDraw; j++) {
                    float angleOffset = MathHelper.TwoPi * j / fishToDraw;
                    float currentAngle = ballRotation + angleOffset;
                    float radius = Projectile.width * 0.15f * ballSize;

                    Vector2 fishPos = drawPos + offset + currentAngle.ToRotationVector2() * radius;
                    float fishRotation = currentAngle + MathHelper.PiOver2;

                    sb.Draw(
                        fishTex,
                        fishPos,
                        null,
                        new Color(100, 80, 60, 60),
                        fishRotation,
                        fishTex.Size() / 2f,
                        0.7f * ballSize,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            return false;
        }
    }
}
