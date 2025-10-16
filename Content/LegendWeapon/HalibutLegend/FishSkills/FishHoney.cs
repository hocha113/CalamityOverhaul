using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 蜂蜜鱼技能
    /// </summary>
    internal class FishHoney : FishSkill
    {
        public override int UnlockFishID => ItemID.Honeyfin;
        public override int DefaultCooldown => 60 * (15 - HalibutData.GetDomainLayer());
        public override int ResearchDuration => 60 * 16;
        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse != 2) {
                return null;
            }

            if (Cooldown > 0) {
                return false;
            }

            SummonBeeSwarm(player, item);
            SetCooldown();
            return false;
        }

        private static void SummonBeeSwarm(Player player, Item item) {
            if (Main.myPlayer != player.whoAmI) return;

            var source = player.GetSource_FromThis();

            //生成蜂巢核心
            int swarmCore = Projectile.NewProjectile(
                source,
                player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<HoneyBeeSwarmCore>(),
                player.GetShootState().WeaponDamage,
                2f,
                player.whoAmI
            );

            if (swarmCore >= 0) {
                //召唤音效
                SoundEngine.PlaySound(SoundID.Item97 with {
                    Volume = 0.85f,
                    Pitch = -0.2f
                }, player.Center);

                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.6f,
                    Pitch = 0.3f
                }, player.Center);

                //召唤特效
                SpawnSummonEffect(player.Center);
            }
        }

        private static void SpawnSummonEffect(Vector2 position) {
            //蜂蜜爆发粒子
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);

                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.Honey,
                    velocity,
                    140,
                    Color.Gold,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.3f;
            }

            //环形冲击波
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 20; j++) {
                    float angle = MathHelper.TwoPi * j / 20f;
                    float radius = 20f + i * 15f;
                    Vector2 spawnPos = position + angle.ToRotationVector2() * radius;

                    Dust dust = Dust.NewDustPerfect(
                        spawnPos,
                        DustID.Honey,
                        Vector2.Zero,
                        100,
                        new Color(255, 220, 100),
                        1.5f
                    );
                    dust.noGravity = true;
                    dust.velocity = angle.ToRotationVector2() * 2f;
                }
            }
        }
    }

    #region 蜂群核心控制器
    /// <summary>
    /// 蜂群核心，管理所有蜜蜂的生成和行为
    /// </summary>
    internal class HoneyBeeSwarmCore : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private readonly List<int> activeBees = new();
        private const int MaxBees = 12;
        private const int SwarmLifetime = 600; //10秒
        private const int SpawnInterval = 8;
        private int spawnTimer = 0;
        private int beesSpawned = 0;

        private const float orbitRadius = 80f;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = SwarmLifetime;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //跟随玩家
            Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center, 0.15f);

            //周期性生成蜜蜂
            if (beesSpawned < MaxBees) {
                spawnTimer++;
                if (spawnTimer >= SpawnInterval) {
                    spawnTimer = 0;
                    SpawnBee(owner);
                }
            }

            //清理失效蜜蜂
            CleanupInactiveBees();

            //环境效果
            if (Main.rand.NextBool(4)) {
                SpawnAmbientEffect();
            }

            //发光
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.6f * pulse, 0.5f * pulse, 0.2f * pulse);
        }

        private void SpawnBee(Player owner) {
            if (Main.myPlayer != owner.whoAmI) return;

            //在圆周上均匀分布生成位置
            float angle = MathHelper.TwoPi * beesSpawned / MaxBees;
            Vector2 spawnOffset = angle.ToRotationVector2() * orbitRadius;
            Vector2 spawnPos = Projectile.Center + spawnOffset;

            Vector2 initialVel = Main.rand.NextVector2Circular(4f, 4f);

            int bee = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                spawnPos,
                initialVel,
                ModContent.ProjectileType<HoneyBeeMinion>(),
                Projectile.damage,
                2f,
                owner.whoAmI,
                Projectile.whoAmI, //传递核心ID
                beesSpawned //传递蜜蜂索引
            );

            if (bee >= 0) {
                activeBees.Add(bee);
                beesSpawned++;

                //生成特效
                SpawnBeeEffect(spawnPos);

                //音效
                SoundEngine.PlaySound(SoundID.Item97 with {
                    Volume = 0.3f,
                    Pitch = 0.2f + beesSpawned * 0.05f
                }, spawnPos);
            }
        }

        private void SpawnBeeEffect(Vector2 position) {
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(3f, 3f);
                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.Honey,
                    velocity,
                    140,
                    Color.Gold,
                    Main.rand.NextFloat(0.8f, 1.4f)
                );
                dust.noGravity = true;
            }
        }

        private void SpawnAmbientEffect() {
            Vector2 offset = Main.rand.NextVector2Circular(orbitRadius, orbitRadius);
            Dust dust = Dust.NewDustPerfect(
                Projectile.Center + offset,
                DustID.Honey,
                Vector2.Zero,
                100,
                new Color(255, 220, 100, 150),
                Main.rand.NextFloat(0.8f, 1.3f)
            );
            dust.noGravity = true;
            dust.velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
        }

        private void CleanupInactiveBees() {
            activeBees.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<HoneyBeeMinion>();
            });
        }

        public override void OnKill(int timeLeft) {
            //召回所有蜜蜂
            foreach (int beeID in activeBees) {
                if (beeID >= 0 && beeID < Main.maxProjectiles) {
                    Projectile bee = Main.projectile[beeID];
                    if (bee.active && bee.ModProjectile is HoneyBeeMinion minion) {
                        minion.BeginReturn();
                    }
                }
            }

            //消散特效
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Honey,
                    velocity,
                    140,
                    Color.Gold,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //绘制发光核心
            Texture2D glowTex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.4f + 0.6f;

            for (int i = 0; i < 3; i++) {
                float scale = (1f + i * 0.3f) * pulse;
                float alpha = (1f - i * 0.3f) * 0.4f;

                Main.EntitySpriteDraw(
                    glowTex,
                    Projectile.Center - Main.screenPosition,
                    null,
                    new Color(255, 220, 100, 0) * alpha,
                    0,
                    glowTex.Size() / 2f,
                    scale * 0.8f,
                    SpriteEffects.None
                );
            }

            return false;
        }
    }
    #endregion

    #region 蜜蜂仆从
    /// <summary>
    /// 蜂蜜鱼召唤的蜜蜂仆从
    /// </summary>
    internal class HoneyBeeMinion : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bee;

        private enum BeeState
        {
            Orbiting,   //环绕核心待命
            Seeking,    //搜索敌人
            Attacking,  //攻击敌人
            Returning   //返回核心
        }

        private ref float CoreID => ref Projectile.ai[0];
        private ref float BeeIndex => ref Projectile.ai[1];
        private ref float StateRaw => ref Projectile.localAI[0];
        private ref float StateTimer => ref Projectile.localAI[1];

        private BeeState State {
            get => (BeeState)StateRaw;
            set => StateRaw = (float)value;
        }

        private int targetNPCID = -1;
        private int stingTimer = 0;
        private Vector2 latchOffset = Vector2.Zero;

        private const float MaxSpeed = 14f;
        private const float Acceleration = 0.6f;
        private const float OrbitRadius = 80f;
        private const float AttackRange = 800f;
        private const float LatchDistance = 20f;
        private const int MaxAttackTime = 120;
        private const int StingInterval = 18;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
            ProjectileID.Sets.CultistIsResistantTo[Projectile.type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool? CanDamage() => true; //使用自定义伤害系统

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            //检查核心是否存在
            if (!IsCoreLive()) {
                State = BeeState.Returning;
            }

            StateTimer++;

            //动画帧
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            //状态机
            switch (State) {
                case BeeState.Orbiting:
                    OrbitingBehavior();
                    break;

                case BeeState.Seeking:
                    SeekingBehavior();
                    break;

                case BeeState.Attacking:
                    AttackingBehavior();
                    break;

                case BeeState.Returning:
                    ReturningBehavior();
                    break;
            }

            //分离力避免重叠
            ApplySeparation();

            //旋转朝向速度方向
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }

            //发光
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.2f + 0.8f;
            Lighting.AddLight(Projectile.Center, 0.5f * pulse, 0.4f * pulse, 0.15f * pulse);

            //轨迹粒子
            if (Main.rand.NextBool(8)) {
                SpawnTrailDust();
            }
        }

        private void OrbitingBehavior() {
            Projectile coreProj = GetCoreProjectile();
            if (coreProj == null) {
                BeginReturn();
                return;
            }

            //环绕核心运动
            float angle = Main.GlobalTimeWrappedHourly * 2f + BeeIndex * MathHelper.TwoPi / 12f;
            Vector2 targetPos = coreProj.Center + angle.ToRotationVector2() * OrbitRadius;

            //添加轻微的上下波动
            targetPos.Y += (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + BeeIndex) * 10f;

            MoveTowards(targetPos, 0.8f);

            //搜索敌人
            if (StateTimer > 30) {
                NPC target = Projectile.Center.FindClosestNPC(AttackRange, false, true);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                    State = BeeState.Seeking;
                    StateTimer = 0;

                    SoundEngine.PlaySound(SoundID.Item97 with {
                        Volume = 0.25f,
                        Pitch = 0.4f
                    }, Projectile.Center);
                }
            }
        }

        private void SeekingBehavior() {
            if (!IsTargetValid()) {
                State = BeeState.Orbiting;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[targetNPCID];

            //追踪目标
            MoveTowards(target.Center, 1.2f);

            //检查是否接近目标
            if (Vector2.DistanceSquared(Projectile.Center, target.Center) < LatchDistance * LatchDistance) {
                BeginAttack();
            }

            //超时返回
            if (StateTimer > 180) {
                State = BeeState.Orbiting;
                StateTimer = 0;
            }
        }

        private void AttackingBehavior() {
            if (!IsTargetValid()) {
                State = BeeState.Orbiting;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[targetNPCID];

            //附着在目标上
            Projectile.Center = target.Center + latchOffset;
            Projectile.velocity = target.velocity;

            //周期性攻击
            stingTimer++;
            if (stingTimer >= StingInterval) {
                stingTimer = 0;
                DealDamage(target);
            }

            //攻击时间结束或目标死亡
            if (StateTimer > MaxAttackTime || target.life <= 0) {
                State = BeeState.Orbiting;
                StateTimer = 0;

                //生成脱离特效
                for (int i = 0; i < 5; i++) {
                    Dust dust = Dust.NewDustDirect(
                        Projectile.position,
                        Projectile.width,
                        Projectile.height,
                        DustID.Honey,
                        Scale: Main.rand.NextFloat(1f, 1.5f)
                    );
                    dust.velocity = Main.rand.NextVector2Circular(2f, 2f);
                    dust.noGravity = true;
                }
            }
        }

        private void ReturningBehavior() {
            Player owner = Main.player[Projectile.owner];

            MoveTowards(owner.Center, 0.9f);

            if (Vector2.DistanceSquared(Projectile.Center, owner.Center) < 30f * 30f) {
                Projectile.Kill();
            }
        }

        private void MoveTowards(Vector2 target, float speedMultiplier) {
            Vector2 direction = target - Projectile.Center;
            float distance = direction.Length();

            if (distance > 5f) {
                direction.Normalize();
                Vector2 desiredVelocity = direction * MaxSpeed * speedMultiplier;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, Acceleration / 10f);
            }

            //添加细微随机扰动
            Projectile.velocity += Main.rand.NextVector2Circular(0.3f, 0.3f);

            //限制最大速度
            if (Projectile.velocity.Length() > MaxSpeed * speedMultiplier) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * MaxSpeed * speedMultiplier;
            }
        }

        private void ApplySeparation() {
            Vector2 separation = Vector2.Zero;
            int count = 0;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (other.active &&
                    other.type == Projectile.type &&
                    other.whoAmI != Projectile.whoAmI &&
                    other.owner == Projectile.owner) {

                    float distance = Vector2.Distance(Projectile.Center, other.Center);
                    if (distance < 25f && distance > 0.1f) {
                        separation += (Projectile.Center - other.Center) / distance;
                        count++;
                    }
                }
            }

            if (count > 0) {
                separation /= count;
                Projectile.velocity += separation * 0.5f;
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) return false;
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        private bool IsCoreLive() {
            int coreID = (int)CoreID;
            if (coreID < 0 || coreID >= Main.maxProjectiles) return false;
            Projectile core = Main.projectile[coreID];
            return core.active && core.type == ModContent.ProjectileType<HoneyBeeSwarmCore>();
        }

        private Projectile GetCoreProjectile() {
            int coreID = (int)CoreID;
            if (coreID < 0 || coreID >= Main.maxProjectiles) return null;
            Projectile core = Main.projectile[coreID];
            if (!core.active || core.type != ModContent.ProjectileType<HoneyBeeSwarmCore>()) return null;
            return core;
        }

        private void BeginAttack() {
            State = BeeState.Attacking;
            StateTimer = 0;
            stingTimer = 0;

            //计算附着偏移
            NPC target = Main.npc[targetNPCID];
            latchOffset = Projectile.Center - target.Center;
            if (latchOffset.LengthSquared() < 1f) {
                latchOffset = new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-10f, 10f));
            }

            //音效
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.4f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        public void BeginReturn() {
            State = BeeState.Returning;
            StateTimer = 0;
        }

        private void DealDamage(NPC target) {
            //蜇刺特效
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Honey,
                    Main.rand.NextVector2Circular(2f, 2f),
                    140,
                    Color.Gold,
                    Main.rand.NextFloat(0.8f, 1.3f)
                );
                dust.noGravity = true;
            }

            for (int i = 0; i < 1; i++) {
                Dust dust = Dust.NewDustPerfect(
                    target.Center,
                    DustID.Honey,
                    Main.rand.NextVector2Circular(2f, 2f),
                    140,
                    Color.Gold,
                    Main.rand.NextFloat(0.8f, 1.3f)
                );
                dust.noGravity = true;
            }

            //音效
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.3f,
                Pitch = 0.5f
            }, Projectile.Center);
        }

        private void SpawnTrailDust() {
            Dust dust = Dust.NewDustDirect(
                Projectile.position,
                Projectile.width,
                Projectile.height,
                DustID.Honey,
                0, 0, 140,
                new Color(255, 220, 100),
                Main.rand.NextFloat(0.6f, 1f)
            );
            dust.velocity = -Projectile.velocity * 0.2f;
            dust.noGravity = true;
        }

        public override void OnKill(int timeLeft) {
            //死亡特效
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Honey,
                    velocity,
                    140,
                    Color.Gold,
                    Main.rand.NextFloat(1f, 1.6f)
                );
                dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.3f,
                Pitch = 0.4f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle sourceRect = new Rectangle(0, frameHeight * Projectile.frame, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);

            //飞行时的轻微波动
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.whoAmI * 0.5f) * 0.15f;
            float scaleX = 1f + wave * 0.1f;
            float scaleY = 1f - wave * 0.08f;

            //根据速度方向决定翻转
            SpriteEffects effects = Projectile.velocity.X < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //攻击状态时根据目标位置决定朝向
            if (State == BeeState.Attacking && IsTargetValid()) {
                NPC target = Main.npc[targetNPCID];
                effects = target.Center.X < Projectile.Center.X ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            }

            //发光效果
            Color glowColor = Color.Lerp(lightColor, new Color(255, 220, 100), 0.5f);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                sourceRect,
                glowColor,
                Projectile.rotation,
                origin,
                new Vector2(scaleX, scaleY) * Projectile.scale,
                effects,
                0
            );

            return false;
        }
    }
    #endregion
}
