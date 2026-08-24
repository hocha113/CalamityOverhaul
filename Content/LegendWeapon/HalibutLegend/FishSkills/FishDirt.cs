using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
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
            int fishCount = player.CountProjectilesOfID<DirtFishFollower>();
            int requiredFish = 5 + HalibutData.GetDomainLayer();

            if (fishCount >= requiredFish && !HasActiveDirtBall(player) && !HasGatheringFish(player)) {
                GatherAndShootDirtBall(player, source, damage, knockback, velocity);
            }

            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (Active(player)) {
                spawnTimer++;

                int currentCount = player.CountProjectilesOfID<DirtFishFollower>();
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

            //出生特效走 follower 首帧,让所有客户端都能看到成形
            Projectile.NewProjectile(
                player.GetSource_FromThis(),
                spawnPos,
                velocity,
                ModContent.ProjectileType<DirtFishFollower>(),
                0,
                0f,
                player.whoAmI
            );
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

    /// <summary>土鱼跟随弹幕</summary>
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

        /// <summary>高领域层鱼群可到 40+,掉渣概率按"等效 12 条"封顶,防全队刷屏</summary>
        private bool ShedGate() {
            int flock = Main.player[Projectile.owner].ownedProjectileCounts[Projectile.type];
            return flock <= 12 || Main.rand.Next(flock) < 12;
        }

        private Vector2 boidVelocity = Vector2.Zero;
        private float wigglePhase = 0f;
        private float orbitAngle = 0f;
        private float orbitRadius = 0f;
        private int shedTimer = 0;
        /// <summary>14 帧从尘中成形,禁 pop-in;传送后 LifeTimer 归零复用同一包络</summary>
        private float MaterializeFade => Math.Min(LifeTimer / 14f, 1f);
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

            //成形首帧:一团干尘裹住淡入的鱼,土屑同步散落
            if (LifeTimer == 1f) {
                SoundEngine.PlaySound(SoundID.Item8 with {
                    Volume = 0.3f,
                    Pitch = 0.2f
                }, Projectile.Center);
                FishDirtVFX.Puff(Projectile.Center, Projectile.velocity * 0.15f, Main.rand.NextFloat(0.24f, 0.3f));
                for (int i = 0; i < 3; i++) {
                    FishDirtVFX.Crumb(Projectile.Center + Main.rand.NextVector2Circular(10f, 8f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.2f, 1f)),
                        Main.rand.NextFloat(0.5f, 0.75f));
                }
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

            //蓄力期躁动加剧,摆尾频率上抬
            wigglePhase += State == FishState.Gathering ? 0.28f : 0.15f;
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
                //旧位置留一撮散尘,新位置靠 LifeTimer 归零重走成形包络
                FishDirtVFX.Puff(Projectile.Center, Vector2.Zero, 0.24f, 20);
                orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                orbitRadius = Main.rand.NextFloat(MinOrbitRadius, MaxOrbitRadius);
                Projectile.Center = owner.Center + Main.rand.NextVector2Circular(200f, 200f);
                Projectile.velocity = owner.velocity + Main.rand.NextVector2Circular(3f, 3f);
                LifeTimer = 0f;
            }

            //存在即掉渣:速度越快土屑抖落越勤,受重力坠离鱼身
            shedTimer++;
            int shedInterval = (int)MathHelper.Clamp(34f - Projectile.velocity.Length() * 1.6f, 12f, 34f);
            if (shedTimer >= shedInterval && ShedGate()) {
                shedTimer = 0;
                FishDirtVFX.Crumb(Projectile.Center + Main.rand.NextVector2Circular(8f, 6f),
                    new Vector2(Projectile.velocity.X * 0.15f, Main.rand.NextFloat(0.2f, 0.8f)),
                    Main.rand.NextFloat(0.5f, 0.8f));
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

            //赶路预备拍:急转掉渣更勤
            if (GatherTimer % 5 == 0 && ShedGate()) {
                FishDirtVFX.Crumb(Projectile.Center + Main.rand.NextVector2Circular(8f, 6f),
                    -Projectile.velocity * 0.1f + new Vector2(0f, 0.6f),
                    Main.rand.NextFloat(0.45f, 0.7f));
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

            //蓄力拍:攥不住土,越临近收束渣掉得越急
            int shedCadence = GatherTimer > 45 ? 4 : 6;
            if (GatherTimer % shedCadence == 0 && ShedGate()) {
                FishDirtVFX.Crumb(Projectile.Center + Main.rand.NextVector2Circular(9f, 7f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 1.5f)),
                    Main.rand.NextFloat(0.4f, 0.65f), Main.rand.Next(14, 22));
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
                                (int)(storedDamage * (1.7f + HalibutData.GetDomainLayer() * 0.45f)),
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

            //收束冲刺:身后甩落短命土屑,与拉伸残影同向
            if (GatherTimer % 3 == 0 && Main.rand.NextBool() && ShedGate()) {
                FishDirtVFX.Crumb(Projectile.Center, -Projectile.velocity * 0.25f,
                    Main.rand.NextFloat(0.5f, 0.8f), Main.rand.Next(12, 18));
            }
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

        private static Vector2 LimitVector(Vector2 vec, float max) {
            if (vec.LengthSquared() > max * max) {
                return vec.SafeNormalize(Vector2.Zero) * max;
            }
            return vec;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //化入土球或寿终:散成一撮土屑与干尘,禁 pop-out
            //收束吸收是全队同帧齐灭,单鱼配额压低+概率闸防刷屏
            bool absorbed = State == FishState.Converging;
            int crumbs = absorbed ? (ShedGate() ? 2 : 0) : 3;
            for (int i = 0; i < crumbs; i++) {
                FishDirtVFX.Crumb(Projectile.Center + Main.rand.NextVector2Circular(8f, 6f),
                    Projectile.velocity * 0.2f + new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-0.5f, 1f)),
                    Main.rand.NextFloat(0.5f, 0.8f), Main.rand.Next(14, 24));
            }
            if (!absorbed || (Main.rand.NextBool() && ShedGate())) {
                FishDirtVFX.Puff(Projectile.Center, Projectile.velocity * 0.15f,
                    Main.rand.NextFloat(0.2f, 0.28f), Main.rand.Next(18, 28));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Main.instance.LoadItem(ItemID.Dirtfish);
            Texture2D texture = TextureAssets.Item[ItemID.Dirtfish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            SpriteEffects effects = Projectile.velocity.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            float wiggleOffset = (float)Math.Sin(wigglePhase) * 3f;
            Vector2 wigglePos = drawPos + new Vector2(wiggleOffset, 0);

            //成形包络:缓出放大+淡入,从出生尘团里长出来
            float fade = MaterializeFade;
            float matGrow = 1f - (1f - fade) * (1f - fade);
            float scaleModifier = (1f + (float)Math.Sin(LifeTimer * 0.1f) * 0.08f) * MathHelper.Lerp(0.55f, 1f, matGrow);

            //游速拉伸:横向撑长纵向收窄,游得越急鱼身越绷
            float hSpeed = Math.Abs(Projectile.velocity.X);
            float bodyStretch = Math.Min(hSpeed * 0.02f, 0.24f);
            Vector2 bodyScale = new Vector2(1f + bodyStretch, 1f - bodyStretch * 0.45f) * Projectile.scale * scaleModifier;

            Color drawColor = Projectile.GetAlpha(lightColor) * fade;

            if (State == FishState.Gathering) {
                //蓄力:土不发光,紧张感走压暗+颤抖+掉渣
                float strain = Math.Min(GatherTimer / 45f, 1f);
                drawColor = drawColor.MultiplyRGB(Color.Lerp(Color.White, new Color(185, 175, 165), strain));
                wigglePos += Main.rand.NextVector2Circular(1.7f * strain, 1.3f * strain);
            }

            //收束/赶路高速段:速度拉伸残影链,方向由残影编码
            float speed = Projectile.velocity.Length();
            if ((State == FishState.Converging || State == FishState.MovingToGather) && speed > 7f) {
                for (int g = 2; g >= 1; g--) {
                    sb.Draw(texture, wigglePos - Projectile.velocity * (g * 1.35f), null,
                        drawColor * (0.32f / g), Projectile.rotation, texture.Size() / 2f,
                        bodyScale * (1f - g * 0.08f), effects, 0);
                }
            }

            sb.Draw(
                texture,
                wigglePos,
                null,
                drawColor,
                Projectile.rotation,
                texture.Size() / 2f,
                bodyScale,
                effects,
                0
            );

            return false;
        }
    }

    /// <summary>土球弹幕，土鱼聚合产物</summary>
    internal class DirtBall : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float FishCount => ref Projectile.ai[0];
        private ref float BounceCount => ref Projectile.ai[1];

        private bool isRolling = false;
        private float ballRotation = 0f;
        private float spinVel = 0f;      //自转角速度,滚动期锁定为线速度换算值
        private float squash = 0f;       //落地压缩量 0-1,指数回弹
        private int spawnPop = 0;        //出场过冲剩余帧
        private int rollSoundTimer = 0;
        private int trackTimer = 0;
        private int shedTimer = 0;
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
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                spawnPop = 8;
                //出手即带角动量,飞行读作翻滚
                spinVel = MathHelper.Clamp(Projectile.velocity.X * 0.02f, -0.16f, 0.16f);
                if (Math.Abs(spinVel) < 0.05f) {
                    spinVel = 0.05f * (Projectile.velocity.X >= 0f ? 1f : -1f);
                }
                LaunchBurst();
            }
            if (spawnPop > 0) {
                spawnPop--;
            }

            if (!isRolling) {
                Projectile.velocity.Y += Gravity;
                Projectile.velocity *= 0.99f;
                spinVel *= 0.997f;

                //飞行期持续掉渣拖微尘,量随飞行演化
                shedTimer++;
                if (shedTimer % 4 == 0) {
                    FishDirtVFX.Crumb(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f),
                        -Projectile.velocity * 0.12f + new Vector2(0f, 0.5f), Main.rand.NextFloat(0.5f, 0.85f));
                }
                if (shedTimer % 7 == 0) {
                    FishDirtVFX.Puff(Projectile.Center - Projectile.velocity * 1.2f,
                        -Projectile.velocity * 0.06f, Main.rand.NextFloat(0.16f, 0.24f), Main.rand.Next(18, 26));
                }
            }
            else {
                Projectile.velocity.X *= 0.97f;

                if (Math.Abs(Projectile.velocity.X) < 0.5f) {
                    if (Projectile.velocity.X != 0f) {
                        //落定:最后一口沉降尘,此后静置
                        FishDirtVFX.Puff(Projectile.Bottom + new Vector2(0f, -6f),
                            new Vector2(0f, -0.4f), 0.34f, 34, 0.5f);
                        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.35f, Pitch = -0.5f }, Projectile.Center);
                    }
                    Projectile.velocity.X = 0;
                }

                //贴地滚动:角速度锁定线速度换算,滚动感来自转速与位移咬合
                spinVel = Projectile.velocity.X * 0.024f;
                RollingFX();

                //静置微演化:偶尔从团顶剥落一粒
                if (Projectile.velocity.X == 0f && Main.rand.NextBool(40)) {
                    FishDirtVFX.Crumb(Projectile.Top + new Vector2(Main.rand.NextFloat(-16f, 16f), 10f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), 0.3f), Main.rand.NextFloat(0.4f, 0.6f));
                }
            }

            ballRotation += spinVel;
            squash *= 0.82f;
        }

        private void LaunchBurst() {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 8; i++) {
                FishDirtVFX.Crumb(Projectile.Center,
                    dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(3f, 8f),
                    Main.rand.NextFloat(0.6f, 0.9f), Main.rand.Next(16, 26));
            }
            for (int i = 0; i < 2; i++) {
                FishDirtVFX.Puff(Projectile.Center - dir * 12f,
                    -dir * Main.rand.NextFloat(0.8f, 1.6f) + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Main.rand.NextFloat(0.3f, 0.42f));
            }
        }

        private void RollingFX() {
            float speed = Math.Abs(Projectile.velocity.X);
            if (speed < 0.7f) {
                return;
            }
            //只有真踩着地才踢尘,滚动期 tileCollide 已关闭需自查
            if (!Collision.SolidCollision(Projectile.Bottom + new Vector2(-18f, -2f), 36, 10)) {
                return;
            }

            rollSoundTimer++;
            if (rollSoundTimer >= 24) {
                rollSoundTimer = 0;
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.18f + speed * 0.03f,
                    Pitch = -0.6f + Main.rand.NextFloat(0.25f),
                    MaxInstances = 2
                }, Projectile.Center);
            }

            //接触面向后踢尘+偶发碎屑碎石抛落
            Vector2 contact = Projectile.Bottom + new Vector2(-Math.Sign(Projectile.velocity.X) * Projectile.width * 0.25f, -2f);
            if (Main.rand.NextBool(2)) {
                FishDirtVFX.Puff(contact + new Vector2(Main.rand.NextFloat(-8f, 8f), 0f),
                    new Vector2(-Projectile.velocity.X * 0.28f, -Main.rand.NextFloat(0.4f, 1.1f) - speed * 0.06f),
                    0.16f + speed * 0.02f, 0, 0.3f);
            }
            if (Main.rand.NextBool(4)) {
                FishDirtVFX.Crumb(contact,
                    new Vector2(-Projectile.velocity.X * Main.rand.NextFloat(0.2f, 0.45f), -Main.rand.NextFloat(1.5f, 3f)),
                    Main.rand.NextFloat(0.5f, 0.8f));
            }
            if (Main.rand.NextBool(10)) {
                FishDirtVFX.Pebble(contact,
                    new Vector2(-Projectile.velocity.X * 0.3f + Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(2.5f, 4.5f)),
                    Main.rand.NextFloat(0.7f, 1f));
            }
            //滚痕:每隔一小段铺一枚贴地压痕 decal
            trackTimer++;
            if (trackTimer >= 11 && speed > 1.2f) {
                trackTimer = 0;
                FishDirtVFX.Track(Projectile.Bottom + new Vector2(0f, -3f), Main.rand.NextFloat(0.65f, 0.85f));
            }
        }

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac) {
            width = height = 60;
            return base.TileCollideStyle(ref width, ref height, ref fallThrough, ref hitboxCenterFrac);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            bool hitGround = Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.1f && oldVelocity.Y > 0;

            if (hitGround) {
                BounceCount++;
                //落地压缩:砸得越狠压得越扁,后续帧指数回弹
                squash = MathHelper.Clamp(Math.Abs(oldVelocity.Y) * 0.09f, 0.35f, 1f);
                //抓地扭矩:自旋向滚动方向收拢
                spinVel = MathHelper.Clamp(Projectile.velocity.X * 0.03f, -0.2f, 0.2f);
                BounceImpactFX(oldVelocity);

                if (BounceCount == 1 && Math.Abs(oldVelocity.Y) > 8f && !Main.dedServ
                    && CWRClientConfig.Instance.ScreenVibration) {
                    //仅首次重着地给一记克制的竖向震
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center,
                        Vector2.UnitY, 3f, 5f, 8, 900f, FullName));
                }

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
                    //闷土底层音对齐弹跳拍
                    SoundEngine.PlaySound(SoundID.Dig with {
                        Volume = 0.5f,
                        Pitch = -0.4f + Main.rand.NextFloat(0.2f)
                    }, Projectile.Center);
                }
            }

            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.1f) {
                Projectile.velocity.X = -oldVelocity.X * 0.5f;

                SoundEngine.PlaySound(SoundID.Tink with {
                    Volume = 0.5f
                }, Projectile.Center);

                //撞墙:沿反弹向刮落一排土屑
                Vector2 away = new Vector2(Projectile.velocity.X >= 0f ? 1f : -1f, 0f);
                for (int i = 0; i < 4; i++) {
                    FishDirtVFX.Crumb(Projectile.Center + new Vector2(-away.X * Projectile.width * 0.4f, Main.rand.NextFloat(-20f, 20f)),
                        away * Main.rand.NextFloat(1.5f, 4f) + new Vector2(0f, -Main.rand.NextFloat(0.5f, 2f)),
                        Main.rand.NextFloat(0.5f, 0.8f));
                }
            }

            return false;
        }

        private void BounceImpactFX(Vector2 oldVelocity) {
            if (Main.dedServ) {
                return;
            }
            float power = MathHelper.Clamp(Math.Abs(oldVelocity.Y) / 12f, 0.3f, 1.4f);
            Vector2 basePos = Projectile.Bottom - new Vector2(0f, 4f);

            //落点擦痕 decal:砸得越狠痕越大
            FishDirtVFX.Track(basePos + new Vector2(0f, 1f), 0.6f + power * 0.4f);

            //压缩尘饼:贴地横向铺开,中间一片加两侧外推
            for (int i = 0; i < 3; i++) {
                float dir = i == 0 ? 0f : (i == 1 ? -1f : 1f);
                FishDirtVFX.Puff(basePos + new Vector2(dir * 18f, 0f),
                    new Vector2(dir * (1.6f + power * 1.6f), -0.35f),
                    (0.3f + power * 0.28f) * Main.rand.NextFloat(0.85f, 1.15f),
                    (int)(26 + power * 12), 0.55f);
            }
            //碎屑弧线抛落
            int crumbs = (int)(7 + power * 5);
            for (int i = 0; i < crumbs; i++) {
                FishDirtVFX.Crumb(basePos + new Vector2(Main.rand.NextFloat(-24f, 24f), 0f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f) * (2.5f + power * 3f), -Main.rand.NextFloat(2f, 5.5f) * power - 1f),
                    Main.rand.NextFloat(0.55f, 0.9f));
            }
            for (int i = 0; i < 3; i++) {
                FishDirtVFX.Pebble(basePos,
                    new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(3f, 6f) * power),
                    Main.rand.NextFloat(0.8f, 1.1f));
            }
            //原版土尘作底噪填充
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.Bottom - new Vector2(Projectile.width / 2, 12),
                    Projectile.width, 12,
                    DustID.Dirt,
                    Scale: Main.rand.NextFloat(1.1f, 1.8f)
                );
                dust.velocity = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, -1.5f)) * power;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Confused, 60 * 3);

            if (Main.dedServ) {
                return;
            }
            //撞击点土屑沿撞向外溅
            Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 3; i++) {
                FishDirtVFX.Crumb(target.Center - dir * 10f,
                    dir.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(2f, 5f) - Vector2.UnitY * 1.5f,
                    Main.rand.NextFloat(0.5f, 0.8f));
            }
            FishDirtVFX.Puff(target.Center, dir * 1.2f, Main.rand.NextFloat(0.2f, 0.3f), Main.rand.Next(16, 24));
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(
                    target.position,
                    target.width,
                    target.height,
                    DustID.Dirt,
                    Scale: Main.rand.NextFloat(1.2f, 1.8f)
                );
                dust.velocity = Main.rand.NextVector2Circular(4f, 4f);
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Dig with {
                Volume = 0.8f,
                Pitch = -0.35f
            }, Projectile.Center);

            if (Main.dedServ) {
                return;
            }
            //崩解:整团散架成碎屑与碎石,沉降尘活得比球久
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4.5f, 3f);
                vel.Y -= Main.rand.NextFloat(1f, 3f);
                FishDirtVFX.Crumb(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f), vel,
                    Main.rand.NextFloat(0.6f, 1f), Main.rand.Next(26, 40));
            }
            for (int i = 0; i < 4; i++) {
                FishDirtVFX.Pebble(Projectile.Center,
                    new Vector2(Main.rand.NextFloat(-3.5f, 3.5f), -Main.rand.NextFloat(2f, 5f)),
                    Main.rand.NextFloat(0.8f, 1.2f));
            }
            for (int i = 0; i < 2; i++) {
                FishDirtVFX.Puff(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -0.3f),
                    Main.rand.NextFloat(0.4f, 0.55f), Main.rand.Next(32, 46), 0.5f);
                FishDirtVFX.Puff(Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                    Main.rand.NextVector2Circular(1f, 0.7f),
                    Main.rand.NextFloat(0.3f, 0.4f), Main.rand.Next(30, 42));
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position, Projectile.width, Projectile.height,
                    DustID.Dirt,
                    Scale: Main.rand.NextFloat(1.2f, 2f)
                );
                dust.velocity = Main.rand.NextVector2Circular(4f, 3f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //高层数鱼量可到 40+,视觉尺寸封顶防糊满屏(判定箱不变)
            float ballSize = MathHelper.Clamp(0.8f + FishCount * 0.05f, 0.8f, 1.9f);
            float popT = spawnPop / 8f;
            float pop = 1f + 0.28f * popT * popT;
            //出场过冲+落地压缩:横向撑宽纵向压扁,作用在构图偏移与土体缩放上
            Vector2 deform = new Vector2(1f + squash * 0.2f, 1f - squash * 0.17f) * pop;

            //自旋拖影:按上一两帧的真实角度回拨重画整团,位置残影表达不了滚动;
            //回拨量压在鱼环相邻间距(24°)之下,防拖影与邻位鱼混叠失效
            float smear = Math.Abs(spinVel);
            if (smear > 0.045f) {
                float back = MathHelper.Clamp(spinVel * 1.25f, -0.16f, 0.16f);
                DrawBallBody(sb, drawPos, lightColor, ballSize, deform, ballRotation - back * 2f, 0.15f);
                DrawBallBody(sb, drawPos, lightColor, ballSize, deform, ballRotation - back, 0.32f);
            }
            DrawBallBody(sb, drawPos, lightColor, ballSize, deform, ballRotation, 1f);

            return false;
        }

        private void DrawBallBody(SpriteBatch sb, Vector2 drawPos, Color lightColor
            , float ballSize, Vector2 deform, float rot, float alpha) {
            Main.instance.LoadItem(ItemID.Dirtfish);
            Texture2D fishTex = TextureAssets.Item[ItemID.Dirtfish].Value;
            Texture2D lumpTex = CWRAsset.Fog?.Value;
            Texture2D chipTex = CWRAsset.Extra_98?.Value;
            int fishToDraw = (int)Math.Min(FishCount, 15);

            //底层土体:三片噪形烟团随球转动拼出哑光团块剪影,画在鱼层之下(夹心)
            if (lumpTex != null) {
                Vector2 origin = lumpTex.Size() * 0.5f;
                for (int k = 0; k < 3; k++) {
                    //三片各自镜像，免得同一张烟团盖出三个同形块
                    SpriteEffects flip = k switch {
                        0 => SpriteEffects.None,
                        1 => SpriteEffects.FlipHorizontally,
                        _ => SpriteEffects.FlipVertically,
                    };
                    float phase = k * 2.1f;
                    Vector2 off = (rot * 0.9f + phase).ToRotationVector2() * (k * 6f * ballSize) * deform;
                    Color c = (k == 0 ? FishDirtVFX.SoilDark : FishDirtVFX.SoilDeep).MultiplyRGB(lightColor);
                    float s = (0.096f - k * 0.0132f) * ballSize;
                    sb.Draw(lumpTex, drawPos + off, null, c * ((k == 0 ? 0.92f : 0.78f) * alpha),
                        rot + phase, origin, new Vector2(s) * deform, flip, 0);
                }
            }

            //鱼层:半埋在土体里绕转,顶亮底沉读出体积
            for (int i = 0; i < fishToDraw; i++) {
                float angleOffset = MathHelper.TwoPi * i / fishToDraw;
                float currentAngle = rot + angleOffset;
                float radius = Projectile.width * 0.18f * ballSize;

                Vector2 fishPos = drawPos + currentAngle.ToRotationVector2() * radius * deform;
                float fishRotation = currentAngle + MathHelper.PiOver2;

                float depthShade = 0.55f + 0.45f * (0.5f - 0.5f * (float)Math.Sin(currentAngle));
                Color fishColor = Projectile.GetAlpha(lightColor).MultiplyRGB(new Color(210, 190, 168));
                fishColor = Color.Lerp(FishDirtVFX.SoilDeep.MultiplyRGB(lightColor), fishColor, depthShade);

                sb.Draw(
                    fishTex,
                    fishPos,
                    null,
                    fishColor * alpha,
                    fishRotation,
                    fishTex.Size() / 2f,
                    0.7f * ballSize,
                    SpriteEffects.None,
                    0
                );
            }

            //表面碎石:随转动的固定锚点,让转速肉眼可读
            if (chipTex != null) {
                for (int i = 0; i < 4; i++) {
                    float a = rot + MathHelper.TwoPi * i / 4f + 0.7f;
                    Vector2 p = drawPos + a.ToRotationVector2() * (24f * ballSize) * deform;
                    Color c = FishDirtVFX.PebbleGray.MultiplyRGB(lightColor);
                    sb.Draw(chipTex, p, null, c * (0.9f * alpha), a * 1.7f, chipTex.Size() / 2f,
                        new Vector2(0.16f, 0.26f) * ballSize, SpriteEffects.None, 0);
                }
            }
        }
    }
}
