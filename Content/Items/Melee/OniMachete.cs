using CalamityMod.Dusts;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    [VaultLoaden(CWRConstant.Item_Melee)]
    internal class OniMachete : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "OniMachete";
        public static Texture2D OniArm = null;
        public static Texture2D OniHand = null;
        private static readonly List<int> ActiveHands = new();
        public override void SetDefaults() {
            Item.width = Item.height = 45;
            Item.damage = 2666;
            Item.scale = 3.2f;
            Item.DamageType = DamageClass.Generic;
            Item.useTime = Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            if (InWorldBossPhase.Level11) {
                damage *= 1.25f;
            }
            if (InWorldBossPhase.Level12) {
                damage *= 1.25f;
            }
        }

        public override void HoldItem(Player player) {
            if (ActiveHands.Count < 6) {
                SpawnHand(player, player.FromObjectGetParent(), player.GetWeaponDamage(Item, true), 2f);
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            SpawnSummonEffect(target.Center);
        }

        public override void UpdateInventory(Player player) {
            CleanupInactiveHands();
        }

        private static void SpawnHand(Player player, IEntitySource source, int damage, float knockback) {
            //手臂直接从玩家中心生成
            Vector2 spawnPos = player.Center;

            int handProj = Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<OniHandMinion>(),
                2666,//给定一个不变的基础伤害
                knockback * 2f,
                player.whoAmI,
                ActiveHands.Count
            );

            if (handProj >= 0) {
                ActiveHands.Add(handProj);
                SpawnSummonEffect(spawnPos);

                //硫磺火召唤音效 - 参考SCal的音效
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.7f,
                    Pitch = -0.4f
                }, spawnPos);

                //低沉的地狱火焰音
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                    Volume = 0.6f,
                    Pitch = -0.5f
                }, spawnPos);
            }
        }

        private static void CleanupInactiveHands() {
            ActiveHands.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<OniHandMinion>();
            });
        }

        private static void SpawnSummonEffect(Vector2 position) {
            //硫磺火粒子爆发，使用Brimstone粒子
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);

                Dust dust = Dust.NewDustPerfect(
                    position,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.8f, 3f)
                );
                dust.noGravity = true;
                dust.fadeIn = 1.4f;
            }

            //红色火焰核心
            for (int i = 0; i < 20; i++) {
                Dust fire = Dust.NewDustPerfect(
                    position,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(8f, 8f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                fire.noGravity = true;
            }

            //地狱火环
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 18; j++) {
                    float angle = MathHelper.TwoPi * j / 18f;
                    float radius = 35f + i * 25f;
                    Vector2 spawnPos = position + angle.ToRotationVector2() * radius;

                    Dust ring = Dust.NewDustPerfect(
                        spawnPos,
                        (int)CalamityDusts.Brimstone,
                        angle.ToRotationVector2() * 4f,
                        0,
                        default,
                        Main.rand.NextFloat(1.5f, 2.5f)
                    );
                    ring.noGravity = true;
                }
            }
        }
    }

    internal class OniHandExplode : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 4;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
        }
    }

    internal class OniFireBall : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Melee + "OniMachete";

        private ref float Timer => ref Projectile.ai[0];
        private ref float TargetNPCID => ref Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.timeLeft = 180;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = true;
            Projectile.maxPenetrate = Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            Projectile.damage = (int)(Projectile.damage * 0.60f);
        }

        public override void AI() {
            Timer++;

            //火球旋转效果
            Projectile.rotation += Projectile.velocity.X * 0.13f;

            //硫磺火球发光
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.whoAmI) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.9f * pulse, 0.3f * pulse, 0.1f * pulse);

            //初期加速
            if (Timer < 15f) {
                Projectile.velocity *= 1.02f;
            }

            //中期追踪
            if (Timer > 15f && Timer < 120f) {
                HomeInOnTarget();
            }

            //后期减速
            if (Timer > 120f) {
                Projectile.velocity *= 0.98f;
            }

            //硫磺火粒子轨迹
            SpawnBrimstoneTrail();

            //火球膨胀脉动效果
            Projectile.scale = 1f + (float)Math.Sin(Timer * 0.02f) * 0.1f;
        }

        private void HomeInOnTarget() {
            //寻找最近的敌人
            NPC target = null;
            float maxDistance = 600f;

            if (TargetNPCID >= 0 && TargetNPCID < Main.maxNPCs) {
                NPC potentialTarget = Main.npc[(int)TargetNPCID];
                if (potentialTarget.active && potentialTarget.CanBeChasedBy() &&
                    Vector2.Distance(Projectile.Center, potentialTarget.Center) < maxDistance) {
                    target = potentialTarget;
                }
            }

            if (target == null) {
                target = Projectile.Center.FindClosestNPC(maxDistance);
                if (target != null) {
                    TargetNPCID = target.whoAmI;
                }
            }

            if (target != null) {
                //平滑追踪
                Vector2 targetDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float currentSpeed = Projectile.velocity.Length();
                float turnSpeed = 0.08f;

                Projectile.velocity = Vector2.Lerp(
                    Projectile.velocity.SafeNormalize(Vector2.Zero),
                    targetDirection,
                    turnSpeed
                ) * currentSpeed;
            }
        }

        private void SpawnBrimstoneTrail() {
            //每帧生成硫磺火轨迹粒子
            if (Main.rand.NextBool(2)) {
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    (int)CalamityDusts.Brimstone,
                    -Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(1f, 1f),
                    0,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.3f;
            }

            //火焰尾迹
            if (Main.rand.NextBool(3)) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    -Projectile.velocity * 0.2f,
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1f, 1.8f)
                );
                fire.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 180);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire3, 180);

            //击中特效
            CreateHitEffect(Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            //火球爆炸效果
            CreateExplosionEffect(Projectile.Center);

            //爆炸音效
            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.6f,
                Pitch = 0.2f
            }, Projectile.Center);
        }

        private void CreateHitEffect(Vector2 position) {
            //击中硫磺火爆发
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust brimstone = Dust.NewDustPerfect(
                    position,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                brimstone.noGravity = true;
            }

            //火焰飞溅
            for (int i = 0; i < 8; i++) {
                Dust fire = Dust.NewDustPerfect(
                    position,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(6f, 6f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                fire.noGravity = true;
            }
        }

        private void CreateExplosionEffect(Vector2 position) {
            //硫磺火爆炸波
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 10f);

                Dust brimstone = Dust.NewDustPerfect(
                    position,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.6f;
            }

            //火焰爆炸核心
            for (int i = 0; i < 20; i++) {
                Dust fire = Dust.NewDustPerfect(
                    position,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(8f, 8f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.8f, 3f)
                );
                fire.noGravity = true;
            }

            //冲击环
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * 6f;

                Dust ring = Dust.NewDustPerfect(
                    position,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                ring.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰撞时爆炸
            Projectile.Kill();
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle();
            Vector2 origin = rectangle.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //绘制硫磺火后发光层
            for (int i = 0; i < 3; i++) {
                float glowScale = Projectile.scale * (1.3f + i * 0.2f);
                float glowAlpha = 0.4f * (1f - i * 0.3f);

                sb.Draw(
                    mainValue,
                    drawPos,
                    rectangle,
                    new Color(255, 100, 50, 0) * glowAlpha,
                    Projectile.rotation,
                    origin,
                    glowScale,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制核心白色亮光
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.whoAmI) * 0.3f + 0.7f;
            sb.Draw(
                mainValue,
                drawPos,
                rectangle,
                new Color(255, 255, 255, 0) * (0.5f * pulse),
                Projectile.rotation,
                origin,
                Projectile.scale * 0.8f,
                SpriteEffects.None,
                0
            );

            //绘制火球主体 - 红橙色调
            sb.Draw(
                mainValue,
                drawPos,
                rectangle,
                new Color(255, 180, 100, 200),
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //绘制外层炽热边缘
            sb.Draw(
                mainValue,
                drawPos,
                rectangle,
                new Color(255, 80, 40, 0) * 0.6f,
                Projectile.rotation,
                origin,
                Projectile.scale * 1.15f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }

    internal class OniHandMinion : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "OniHand";

        private enum HandState
        {
            Idle,           //待机漂浮
            Targeting,      //锁定目标
            WindingUp,      //蓄力后拉
            Swinging,       //挥击
            Slamming,       //下砸
            Sweeping,       //横扫
            Throwing,       //投掷骨头
            Recovering      //攻击后恢复
        }

        private ref float HandIndex => ref Projectile.ai[0];
        private ref float StateRaw => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.ai[2];
        private ref float StateTimer => ref Projectile.localAI[0];
        private ref float AttackType => ref Projectile.localAI[1];

        private HandState State {
            get => (HandState)StateRaw;
            set => StateRaw = (float)value;
        }

        private int targetNPCID = -1;
        private Vector2 idleOffset = Vector2.Zero;
        private Vector2 attackStartPos = Vector2.Zero;
        private Vector2 attackTargetPos = Vector2.Zero;

        //IK手臂参数
        private readonly List<Vector2> armSegments = new();
        private const int ArmSegmentCount = 6;
        private const float SegmentLength = 45f;
        private Vector2 shoulderPos = Vector2.Zero;
        private Vector2 handPos = Vector2.Zero;
        private float armTension = 0f; //手臂张力,用于IK自然度
        private int ownerDirection = 1; //玩家朝向 (-1左, 1右)

        //攻击参数
        private const float SearchRange = 1800f;
        private const int IdleDuration = 40;
        private const int WindUpDuration = 30;
        private const int SwingDuration = 18;
        private const int SlamDuration = 22;
        private const int SweepDuration = 35;
        private const int ThrowDuration = 50;
        private const int RecoverDuration = 35;

        //视觉效果
        private float glowIntensity = 0f;
        private float impactShake = 0f;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 20;
        private float handScale = 1f;
        private float attackWindUpIntensity = 0f;

        //投掷动作相关
        private bool throwActionActive = false;
        private Vector2 throwStartPos = Vector2.Zero;
        private Vector2 throwEndPos = Vector2.Zero;

        /// <summary>
        /// 每只手的独特时间偏移（0-1），用于错开动作时机
        /// </summary>
        private float personalityTimeOffset = 0f;

        /// <summary>
        /// 个性化速度倍率（0.85-1.15），让每只手的动作速度略有不同
        /// </summary>
        private float personalitySpeedMultiplier = 1f;

        /// <summary>
        /// 攻击偏好权重：[挥击, 下砸, 横扫, 投掷]
        /// </summary>
        private float[] attackPreference = new float[4];

        /// <summary>
        /// 额外的待机延迟（帧数），让搜索目标的时机错开
        /// </summary>
        private int personalityIdleDelay = 0;

        /// <summary>
        /// 是否初始化了个性
        /// </summary>
        private bool personalityInitialized = false;

        /// <summary>
        /// 个性化的待机角度偏移，让漂浮位置更分散
        /// </summary>
        private float personalityAngleOffset = 0f;

        /// <summary>
        /// 个性化的攻击距离偏好（0.8-1.2）
        /// </summary>
        private float personalityRangePreference = 1f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
        }

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 80;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;

            //初始化IK手臂段
            for (int i = 0; i < ArmSegmentCount; i++) {
                armSegments.Add(Vector2.Zero);
            }
        }

        /// <summary>
        /// 初始化每只手的个性化参数
        /// </summary>
        private void InitializePersonality() {
            if (personalityInitialized) return;
            personalityInitialized = true;

            //基于HandIndex生成一致的随机种子
            int seed = (int)(HandIndex * 1000) + Projectile.owner * 10000;
            Random personalRand = new Random(seed);

            //1. 时间偏移：让每只手的动作时机错开（0-60帧的随机延迟）
            personalityTimeOffset = (float)personalRand.NextDouble();
            personalityIdleDelay = personalRand.Next(0, 60);

            //2. 速度差异：每只手的动作速度略有不同（±15%）
            personalitySpeedMultiplier = 0.85f + (float)personalRand.NextDouble() * 0.3f;

            //3. 攻击偏好：某些手更偏好某种攻击方式
            for (int i = 0; i < 4; i++) {
                attackPreference[i] = 0.5f + (float)personalRand.NextDouble() * 0.5f;
            }
            //归一化权重
            float totalWeight = attackPreference[0] + attackPreference[1] + attackPreference[2] + attackPreference[3];
            for (int i = 0; i < 4; i++) {
                attackPreference[i] /= totalWeight;
            }

            //4. 角度偏移：让待机位置更分散（±30度，即±Pi/6）
            personalityAngleOffset = ((float)personalRand.NextDouble() - 0.5f) * MathHelper.Pi / 3f;

            //5. 距离偏好：某些手更喜欢远程/近战（±20%）
            personalityRangePreference = 0.8f + (float)personalRand.NextDouble() * 0.4f;
        }

        /// <summary>
        /// 获取经过个性化调整的持续时间
        /// </summary>
        private int GetPersonalizedDuration(int baseDuration) {
            return (int)(baseDuration * personalitySpeedMultiplier);
        }

        /// <summary>
        /// 获取带有个性化偏移的全局时间
        /// </summary>
        private float GetPersonalizedTime() {
            return Main.GlobalTimeWrappedHourly + personalityTimeOffset * MathHelper.TwoPi;
        }

        private void UpdateIdleOffset() {
            //更加明显的漂浮效果
            idleOffset.X = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + HandIndex) * 50f;
            idleOffset.Y = (float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.5f + HandIndex) * 30f;
        }

        private void UpdateShoulderPosition(Player owner) {
            if (State == HandState.Idle) {
                //更新玩家朝向
                ownerDirection = owner.direction;
            }
            //肩膀位置需要根据玩家朝向偏移
            Vector2 shoulderOffset = new Vector2(8f * ownerDirection, -4f);
            shoulderPos = owner.GetPlayerStabilityCenter() + shoulderOffset;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Owner.GetItem().type != ModContent.ItemType<OniMachete>()) {
                Projectile.Kill();
                return;
            }

            if (Timer < 30) {
                Projectile.damage = 0;
            }
            else {
                Projectile.damage = (int)(Item.damage * Owner.GetDamage(DamageClass.Generic).Additive);
            }

            //初始化个性
            InitializePersonality();

            Projectile.timeLeft = 60;

            Timer++;
            StateTimer++;
            UpdateIdleOffset();
            UpdateShoulderPosition(Owner);

            //状态机
            switch (State) {
                case HandState.Idle:
                    IdleBehavior(Owner);
                    break;
                case HandState.Targeting:
                    TargetingBehavior();
                    break;
                case HandState.WindingUp:
                    WindUpBehavior();
                    break;
                case HandState.Swinging:
                    SwingingBehavior();
                    break;
                case HandState.Slamming:
                    SlammingBehavior();
                    break;
                case HandState.Sweeping:
                    SweepingBehavior();
                    break;
                case HandState.Throwing:
                    ThrowingBehavior();
                    break;
                case HandState.Recovering:
                    RecoveringBehavior(Owner);
                    break;
            }

            //更新IK手臂
            UpdateArmIK();

            //更新拖尾
            UpdateTrail();

            //硫磺火发光效果（使用个性化时间）
            float pulse = (float)Math.Sin(GetPersonalizedTime() * 6f) * 0.3f + 0.7f;
            Lighting.AddLight(Projectile.Center, 0.8f * pulse, 0.2f * pulse, 0.1f * pulse);

            //冲击震动衰减
            impactShake *= 0.85f;

            //手部缩放回归
            handScale = MathHelper.Lerp(handScale, 1f, 0.1f);

            //蓄力强度衰减
            attackWindUpIntensity *= 0.95f;

            //旋转朝向
            UpdateRotation();

            //硫磺火环境粒子
            SpawnBrimstoneAmbient();
        }

        private void SpawnBrimstoneAmbient() {
            //待机和攻击时持续产生硫磺火粒子
            if (Main.rand.NextBool(5)) {
                Dust brimstoneFire = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                    DustID.Torch,
                    Vector2.UnitY * -Main.rand.NextFloat(1f, 3f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(0.8f, 1.3f)
                );
                brimstoneFire.noGravity = true;
                brimstoneFire.fadeIn = 1.1f;
            }

            //高强度状态下的额外硫磺火效果
            if (glowIntensity > 0.7f && Main.rand.NextBool(3)) {
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(40f, 40f),
                    (int)CalamityDusts.Brimstone,
                    Main.rand.NextVector2Circular(2f, 2f),
                    0,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                brimstone.noGravity = true;
            }
        }

        private void IdleBehavior(Player owner) {
            //应用个性化的待机延迟
            int adjustedIdleDuration = GetPersonalizedDuration(IdleDuration) + personalityIdleDelay;

            //在玩家周围较远距离漂浮，根据玩家朝向调整位置
            //使用个性化角度偏移让每只手的位置更分散
            float angle = HandIndex * MathHelper.TwoPi / 3f + GetPersonalizedTime() * 0.5f;

            //根据玩家朝向镜像X偏移
            Vector2 circleOffset = angle.ToRotationVector2() * 150f;
            circleOffset.X *= ownerDirection;

            Vector2 targetPos = shoulderPos + circleOffset + idleOffset + new Vector2(0, -80f);
            //应用个性化速度
            MoveToPosition(targetPos, 0.15f * personalitySpeedMultiplier);

            glowIntensity = 0.4f;
            armTension = 0.3f;
            throwActionActive = false;

            //搜索敌人（考虑个性化延迟）
            if (StateTimer > adjustedIdleDuration) {
                NPC target = owner.Center.FindClosestNPC(SearchRange);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                    State = HandState.Targeting;
                    StateTimer = 0;
                }
            }

            //硫磺火待机粒子
            if (Main.rand.NextBool(8)) {
                SpawnIdleDust();
            }
        }

        private void TargetingBehavior() {
            if (!IsTargetValid()) {
                State = HandState.Idle;
                StateTimer = 0;
                //重新随机化待机延迟，避免同时返回待机
                personalityIdleDelay = Main.rand.Next(0, 40);
                return;
            }

            NPC target = Main.npc[targetNPCID];
            float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);

            //应用个性化的距离判断偏好
            float adjustedThrowRange = 400f * personalityRangePreference;

            //根据距离决定行为
            if (distanceToTarget > adjustedThrowRange) {
                //距离较远-直接进入投掷模式
                AttackType = 3;
                State = HandState.WindingUp;
                StateTimer = 0;
                attackStartPos = Projectile.Center;
                attackTargetPos = target.Center;
                attackWindUpIntensity = 1f;

                //硫磺火蓄力音效
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                    Volume = 0.5f,
                    Pitch = -0.4f
                }, Projectile.Center);
            }
            else {
                //距离适中，移动到目标附近
                Vector2 approachPos = target.Center + new Vector2(0, -180f);
                MoveToPosition(approachPos, 0.2f * personalitySpeedMultiplier);

                glowIntensity = 0.6f;
                armTension = 0.6f;

                //到达位置后选择近战攻击方式
                if (Vector2.Distance(Projectile.Center, approachPos) < 120f) {
                    ChooseAttackType(target);
                    State = HandState.WindingUp;
                    StateTimer = 0;
                    attackStartPos = Projectile.Center;
                    attackTargetPos = target.Center;
                    attackWindUpIntensity = 1f;
                }

                //锁定火焰效果
                if (StateTimer == 1) {
                    SoundEngine.PlaySound(SoundID.Item74 with {
                        Volume = 0.4f,
                        Pitch = -0.3f
                    }, Projectile.Center);
                }
            }

            //如果无法接近目标,切换到投掷（考虑个性化速度）
            int timeoutDuration = (int)(30 * personalitySpeedMultiplier);
            if (StateTimer > timeoutDuration && distanceToTarget >= 120) {
                AttackType = 3;
                State = HandState.WindingUp;
                StateTimer = 0;
                attackStartPos = Projectile.Center;
                attackTargetPos = target.Center;
                attackWindUpIntensity = 1f;
            }
        }

        private void ChooseAttackType(NPC target) {
            //基于攻击偏好权重和相对位置选择攻击方式
            Vector2 toTarget = target.Center - Projectile.Center;

            //根据位置给不同攻击方式打分
            float swingScore = attackPreference[0] * 1.0f;
            float slamScore = attackPreference[1] * (Math.Abs(toTarget.Y) > Math.Abs(toTarget.X) * 1.2f && toTarget.Y > 0 ? 2.0f : 0.5f);
            float sweepScore = attackPreference[2] * 1.0f;

            //添加随机性
            swingScore *= 0.8f + Main.rand.NextFloat(0.4f);
            slamScore *= 0.8f + Main.rand.NextFloat(0.4f);
            sweepScore *= 0.8f + Main.rand.NextFloat(0.4f);

            //选择得分最高的攻击方式
            if (slamScore > swingScore && slamScore > sweepScore) {
                AttackType = 1; //下砸
            }
            else if (sweepScore > swingScore) {
                AttackType = 2; //横扫
            }
            else {
                AttackType = 0; //挥击
            }
        }

        private void WindUpBehavior() {
            if (!IsTargetValid()) {
                State = HandState.Idle;
                StateTimer = 0;
                return;
            }

            //应用个性化速度
            int adjustedWindUpDuration = GetPersonalizedDuration(WindUpDuration);
            float progress = StateTimer / adjustedWindUpDuration;
            glowIntensity = 0.6f + progress * 0.4f;
            armTension = 0.9f;

            //根据攻击类型后拉-增大幅度，并考虑玩家朝向
            Vector2 windUpOffset = AttackType switch {
                0 => new Vector2(-200f * ownerDirection, -100f),  //挥击-向后上方拉
                1 => new Vector2(0, -250f),                        //下砸-向上拉
                2 => new Vector2(-220f * ownerDirection, 0),       //横扫-向侧后方拉
                3 => new Vector2(-180f * ownerDirection, -120f),   //投掷-向后上方
                _ => Vector2.Zero
            };

            Vector2 targetPos = attackStartPos + windUpOffset;
            MoveToPosition(targetPos, 0.3f * personalitySpeedMultiplier);

            //蓄力时手部放大
            handScale = 1f + progress * 0.3f;

            //硫磺火蓄力粒子
            if (Main.rand.NextBool(2)) {
                SpawnWindUpDust();
            }

            //蓄力音效 - 地狱火焰蓄力（使用个性化pitch）
            if (StateTimer % 8 == 0) {
                float personalPitch = -0.6f + progress * 0.4f + (personalityTimeOffset - 0.5f) * 0.2f;
                SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with {
                    Volume = 0.3f * progress,
                    Pitch = personalPitch
                }, Projectile.Center);
            }

            if (StateTimer >= adjustedWindUpDuration) {
                //切换到对应攻击状态
                State = AttackType switch {
                    0 => HandState.Swinging,
                    1 => HandState.Slamming,
                    2 => HandState.Sweeping,
                    3 => HandState.Throwing,
                    _ => HandState.Idle
                };
                StateTimer = 0;

                //投掷状态特殊处理
                if (State == HandState.Throwing) {
                    throwActionActive = true;
                    throwStartPos = Projectile.Center;

                    //计算投掷目标点在目标敌人前方,考虑预判
                    if (IsTargetValid()) {
                        NPC target = Main.npc[targetNPCID];
                        //个性化的预判时间
                        float predictionTime = 20f * personalitySpeedMultiplier;
                        Vector2 predictedPos = target.Center + target.velocity * predictionTime;
                        throwEndPos = predictedPos;
                    }
                }

                //硫磺火爆发音效
                SoundEngine.PlaySound(SoundID.Item74 with {
                    Volume = 0.9f,
                    Pitch = -0.3f
                }, Projectile.Center);
            }
        }

        private void SwingingBehavior() {
            int adjustedDuration = GetPersonalizedDuration(SwingDuration);
            float progress = StateTimer / adjustedDuration;
            glowIntensity = 1f;
            armTension = 1f;

            //快速挥击弧线-增大范围，考虑玩家朝向
            //朝右时：从右上挥到左下
            //朝左时：从左上挥到右下
            float startAngle = MathHelper.PiOver2 * 1.2f;
            float endAngle = -MathHelper.PiOver4 * 1.5f;

            //根据玩家朝向镜像角度
            if (ownerDirection == -1) {
                startAngle = MathHelper.Pi - startAngle;
                endAngle = MathHelper.Pi - endAngle;
            }

            float swingAngle = MathHelper.Lerp(startAngle, endAngle, CWRUtils.EaseInOutCubic(progress));

            Vector2 swingOffset = new Vector2(
                (float)Math.Cos(swingAngle) * 250f,
                (float)Math.Sin(swingAngle) * 180f
            );

            Projectile.Center = attackTargetPos + swingOffset;
            Projectile.velocity = (attackTargetPos - Projectile.Center) * 1.2f * personalitySpeedMultiplier;

            //挥击时手部缩放效果
            handScale = 1f + (float)Math.Sin(progress * MathHelper.Pi) * 0.4f;

            //挥击特效
            SpawnSwingEffect();

            if (StateTimer >= adjustedDuration) {
                State = HandState.Recovering;
                StateTimer = 0;
                CreateImpactEffect(attackTargetPos);
            }
        }

        private void SlammingBehavior() {
            int adjustedDuration = GetPersonalizedDuration(SlamDuration);
            float progress = StateTimer / adjustedDuration;
            glowIntensity = 1f;
            armTension = 1f;

            //强力下砸-增强速度曲线
            Vector2 slamStart = attackTargetPos + new Vector2(0, -250f);
            Vector2 slamEnd = attackTargetPos + new Vector2(0, 50f);

            float easeProgress = CWRUtils.EaseInCubic(progress);
            Projectile.Center = Vector2.Lerp(slamStart, slamEnd, easeProgress);

            Projectile.velocity = Vector2.Lerp(
                Vector2.Zero,
                new Vector2(0, 60f * personalitySpeedMultiplier),
                easeProgress
            );

            //下砸时手部逐渐握紧效果
            handScale = 1f + (1f - progress) * 0.5f;

            //下砸轨迹特效
            SpawnSlamTrail();

            if (StateTimer >= adjustedDuration) {
                State = HandState.Recovering;
                StateTimer = 0;
                CreateSlamImpact(slamEnd);
            }
        }

        private void SweepingBehavior() {
            int adjustedDuration = GetPersonalizedDuration(SweepDuration);
            float progress = StateTimer / adjustedDuration;
            glowIntensity = 1f;
            armTension = 1f;

            //横扫弧线-增大范围，考虑玩家朝向
            float startAngle = -MathHelper.Pi * 1.1f;
            float endAngle = MathHelper.Pi * 1.1f;

            //根据玩家朝向调整横扫方向
            if (ownerDirection == -1) {
                (startAngle, endAngle) = (MathHelper.Pi - endAngle, MathHelper.Pi - startAngle);
            }

            float sweepAngle = MathHelper.Lerp(startAngle, endAngle, CWRUtils.EaseInOutQuad(progress));

            float radius = 220f;
            Vector2 sweepOffset = new Vector2(
                (float)Math.Cos(sweepAngle) * radius,
                (float)Math.Sin(sweepAngle) * radius * 0.4f
            );

            Projectile.Center = attackTargetPos + sweepOffset;
            Projectile.velocity = Vector2.Zero;

            //横扫时手部扩张
            handScale = 1f + (float)Math.Sin(progress * MathHelper.Pi) * 0.35f;

            //横扫特效
            SpawnSweepEffect();

            if (StateTimer >= adjustedDuration) {
                State = HandState.Recovering;
                StateTimer = 0;
                CreateImpactEffect(Projectile.Center);
            }
        }

        private void ThrowingBehavior() {
            int adjustedDuration = GetPersonalizedDuration(ThrowDuration);
            glowIntensity = 1f;
            armTension = 0.8f;

            if (StateTimer < adjustedDuration * 0.3f) {
                //前30%，保持蓄力姿态
                float holdProgress = StateTimer / (adjustedDuration * 0.3f);
                Vector2 windUpPos = throwStartPos;
                MoveToPosition(windUpPos, 0.2f * personalitySpeedMultiplier);
                handScale = 1f + 0.4f;

                //蓄力粒子持续生成
                if (Main.rand.NextBool(2)) {
                    SpawnWindUpDust();
                }
            }
            else if (StateTimer < adjustedDuration * 0.7f) {
                //中40%，快速前冲投掷动作
                float throwProgress = (StateTimer - adjustedDuration * 0.3f) / (adjustedDuration * 0.4f);
                float easeProgress = CWRUtils.EaseOutCubic(throwProgress);

                //手臂快速向前冲
                Vector2 currentPos = Vector2.Lerp(throwStartPos, throwEndPos, easeProgress);
                Projectile.Center = currentPos;
                Projectile.velocity = (throwEndPos - throwStartPos).SafeNormalize(Vector2.Zero) * 35f * personalitySpeedMultiplier * (1f - easeProgress);

                handScale = 1f + 0.4f * (1f - throwProgress);

                //在投掷动作中段释放骨头
                if (StateTimer == (int)(adjustedDuration * 0.5f)) {
                    ThrowFires();
                }

                //投掷动作轨迹特效
                if (Main.rand.NextBool()) {
                    SpawnThrowTrailEffect();
                }
            }
            else {
                //后30%，收手减速
                float recoverProgress = (StateTimer - adjustedDuration * 0.7f) / (adjustedDuration * 0.3f);
                Projectile.velocity *= 0.85f;
                handScale = 1f + 0.2f * (1f - recoverProgress);
            }

            if (StateTimer >= adjustedDuration) {
                State = HandState.Recovering;
                StateTimer = 0;
                throwActionActive = false;
            }
        }

        private void ThrowFires() {
            if (!IsTargetValid() || Main.myPlayer != Projectile.owner) return;

            NPC target = Main.npc[targetNPCID];

            //计算从手掌中心到目标的方向
            Vector2 throwOrigin = Projectile.Center;
            Vector2 toTarget = (target.Center - throwOrigin).SafeNormalize(Vector2.Zero);

            //投掷5-8根骨头
            int boneCount = 5 + Main.rand.Next(4);
            for (int i = 0; i < boneCount; i++) {
                //扇形散射角度
                float spreadAngle = MathHelper.Lerp(-0.35f, 0.35f, i / (float)(boneCount - 1));
                Vector2 velocity = toTarget.RotatedBy(spreadAngle) * Main.rand.NextFloat(10f, 12f);

                //从手掌位置生成骨头,添加轻微随机偏移
                Vector2 spawnOffset = Main.rand.NextVector2Circular(8f, 8f);

                int proj = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    throwOrigin + spawnOffset,
                    velocity,
                    ModContent.ProjectileType<OniFireBall>(),
                    (int)(Projectile.damage * 0.12),
                    2f,
                    Projectile.owner
                );
                Main.projectile[proj].friendly = true;
            }

            //硫磺火投掷爆发特效
            for (int i = 0; i < 35; i++) {
                Vector2 velocity = toTarget.RotatedByRandom(0.7f) * Main.rand.NextFloat(8f, 18f);
                Dust brimstone = Dust.NewDustPerfect(
                    throwOrigin,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.6f;
            }

            //火焰爆发
            for (int i = 0; i < 25; i++) {
                Vector2 velocity = toTarget.RotatedByRandom(0.6f) * Main.rand.NextFloat(6f, 14f);
                Dust fire = Dust.NewDustPerfect(
                    throwOrigin,
                    DustID.Torch,
                    velocity,
                    0,
                    Color.Red,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                fire.noGravity = true;
            }

            //地狱火冲击环
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 velocity = angle.ToRotationVector2() * 10f;
                Dust ring = Dust.NewDustPerfect(
                    throwOrigin,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                ring.noGravity = true;
            }

            //投掷音效，地狱火焰爆发
            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.95f,
                Pitch = 0.3f
            }, throwOrigin);

            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                Volume = 0.85f,
                Pitch = 0.4f
            }, throwOrigin);
        }

        private void RecoveringBehavior(Player owner) {
            int adjustedDuration = GetPersonalizedDuration(RecoverDuration);
            float progress = StateTimer / adjustedDuration;
            glowIntensity = 1f - progress * 0.7f;
            armTension = 0.5f;

            //返回待机位置，考虑玩家朝向和个性化角度
            float angle = HandIndex * MathHelper.TwoPi / 3f + GetPersonalizedTime() * 0.5f;
            Vector2 circleOffset = angle.ToRotationVector2() * 150f;
            circleOffset.X *= ownerDirection;

            Vector2 recoverPos = shoulderPos + circleOffset + idleOffset + new Vector2(0, -80f);
            MoveToPosition(recoverPos, 0.2f * personalitySpeedMultiplier);

            if (StateTimer >= adjustedDuration) {
                State = HandState.Idle;
                StateTimer = 0;
                //重新随机化待机延迟，确保下次攻击不同步
                personalityIdleDelay = Main.rand.Next(0, 50);
            }
        }

        private void MoveToPosition(Vector2 target, float speed) {
            Vector2 direction = target - Projectile.Center;
            float distance = direction.Length();

            if (distance > 5f) {
                direction.Normalize();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * distance * speed, 0.3f);
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            Projectile.Center += Projectile.velocity;
        }

        private void UpdateArmIK() {
            handPos = Projectile.Center;

            //增强的FABRIK算法,考虑手臂张力
            float targetDistance = Vector2.Distance(shoulderPos, handPos);
            float maxReach = SegmentLength * ArmSegmentCount;

            //如果超出最大伸展范围,限制手的位置
            if (targetDistance > maxReach * 0.98f) {
                Vector2 direction = (handPos - shoulderPos).SafeNormalize(Vector2.Zero);
                handPos = shoulderPos + direction * maxReach * 0.98f;
                Projectile.Center = handPos;
            }

            //前向遍历-从手到肩
            armSegments[0] = handPos;
            for (int i = 1; i < ArmSegmentCount; i++) {
                Vector2 direction = (armSegments[i - 1] - (i == ArmSegmentCount - 1 ? shoulderPos : armSegments[i])).SafeNormalize(Vector2.Zero);

                //根据张力调整关节位置,增加自然弯曲
                //关键修复：根据玩家朝向调整弯曲方向
                float bendFactor = (float)Math.Sin((i / (float)ArmSegmentCount) * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 15f * ownerDirection;

                armSegments[i] = armSegments[i - 1] - direction * SegmentLength + perpendicular;
            }

            //反向遍历-从肩到手
            armSegments[ArmSegmentCount - 1] = shoulderPos;
            for (int i = ArmSegmentCount - 2; i >= 0; i--) {
                Vector2 direction = (armSegments[i] - armSegments[i + 1]).SafeNormalize(Vector2.Zero);

                //同样应用弯曲，考虑玩家朝向
                float bendFactor = (float)Math.Sin((i / (float)ArmSegmentCount) * MathHelper.Pi) * armTension;
                Vector2 perpendicular = new Vector2(-direction.Y, direction.X) * bendFactor * 15f * ownerDirection;

                armSegments[i] = armSegments[i + 1] + direction * SegmentLength + perpendicular;
            }

            //最终调整手的位置
            Projectile.Center = armSegments[0];
        }

        private void UpdateTrail() {
            trailPositions.Insert(0, Projectile.Center);
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private void UpdateRotation() {
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                Projectile.rotation = MathHelper.Lerp(
                    Projectile.rotation,
                    Projectile.velocity.ToRotation() + MathHelper.PiOver2,
                    0.2f
                );
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) return false;
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy();
        }

        //特效方法
        private void SpawnIdleDust() {
            Dust brimstone = Dust.NewDustDirect(
                Projectile.position,
                Projectile.width,
                Projectile.height,
                (int)CalamityDusts.Brimstone,
                Scale: Main.rand.NextFloat(1f, 1.5f)
            );
            brimstone.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
            brimstone.noGravity = true;
        }

        private void SpawnWindUpDust() {
            for (int i = 0; i < 3; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(50f, 50f),
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                brimstone.noGravity = true;
            }

            //红色火焰核心
            if (Main.rand.NextBool()) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                    DustID.Torch,
                    Main.rand.NextVector2Circular(3f, 3f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                fire.noGravity = true;
            }
        }

        private void SpawnSwingEffect() {
            if (Main.rand.NextBool(2)) {
                Dust brimstone = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    (int)CalamityDusts.Brimstone,
                    Scale: Main.rand.NextFloat(2f, 3f)
                );
                brimstone.velocity = Projectile.velocity * 0.4f;
                brimstone.noGravity = true;
            }

            //火焰轨迹
            if (Main.rand.NextBool()) {
                Dust fire = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Torch,
                    Scale: Main.rand.NextFloat(1.5f, 2.5f)
                );
                fire.velocity = Projectile.velocity * 0.3f;
                fire.noGravity = true;
                fire.color = Color.Red;
            }
        }

        private void SpawnSlamTrail() {
            for (int i = 0; i < 4; i++) {
                Dust brimstone = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    (int)CalamityDusts.Brimstone,
                    0, 0, 0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                brimstone.velocity = -Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(3f, 3f);
                brimstone.noGravity = true;
            }

            //火焰尾迹
            if (Main.rand.NextBool()) {
                Dust fire = Dust.NewDustDirect(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Torch,
                    0, 0, 0,
                    Color.Red,
                    Main.rand.NextFloat(1.8f, 2.8f)
                );
                fire.velocity = -Projectile.velocity * 0.2f;
                fire.noGravity = true;
            }
        }

        private void SpawnSweepEffect() {
            if (Main.rand.NextBool()) {
                Vector2 velocity = Projectile.velocity.RotatedByRandom(0.6f) * Main.rand.NextFloat(0.6f, 1.8f);
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                brimstone.noGravity = true;
            }

            //火焰弧线
            if (Main.rand.NextBool(2)) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    Projectile.velocity * 0.4f,
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                fire.noGravity = true;
            }
        }

        private void SpawnThrowTrailEffect() {
            //投掷动作的硫磺火轨迹
            for (int i = 0; i < 3; i++) {
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(25f, 25f),
                    (int)CalamityDusts.Brimstone,
                    -Projectile.velocity * 0.4f + Main.rand.NextVector2Circular(4f, 4f),
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.5f;
            }

            //火焰尾迹
            if (Main.rand.NextBool(2)) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    -Projectile.velocity * 0.3f,
                    0,
                    Color.Red,
                    Main.rand.NextFloat(2f, 3f)
                );
                fire.noGravity = true;
            }
        }

        private void CreateImpactEffect(Vector2 position) {
            impactShake = 12f;

            //硫磺火冲击波
            for (int i = 0; i < 50; i++) {
                float angle = MathHelper.TwoPi * i / 50f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 15f);

                Dust brimstone = Dust.NewDustPerfect(
                    position,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.5f;
            }

            //红色火焰爆发
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(10f, 10f);
                Dust fire = Dust.NewDustPerfect(
                    position,
                    DustID.Torch,
                    velocity,
                    0,
                    Color.Red,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                fire.noGravity = true;
            }

            //地狱火环
            for (int i = 0; i < 25; i++) {
                float angle = MathHelper.TwoPi * i / 25f;
                Vector2 velocity = angle.ToRotationVector2() * 10f;
                Dust ring = Dust.NewDustPerfect(
                    position,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                ring.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.9f,
                Pitch = -0.5f
            }, position);

            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with {
                Volume = 0.8f
            }, position);
        }

        private void CreateSlamImpact(Vector2 position) {
            impactShake = 18f;

            //强力硫磺火冲击波
            for (int i = 0; i < 80; i++) {
                float angle = MathHelper.TwoPi * i / 80f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 22f);

                Dust brimstone = Dust.NewDustPerfect(
                    position,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                brimstone.noGravity = true;
                brimstone.fadeIn = 1.8f;
            }

            //地狱火焰柱
            for (int i = 0; i < 40; i++) {
                Dust fire = Dust.NewDustPerfect(
                    position + Main.rand.NextVector2Circular(60f, 20f),
                    DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-20f, -8f)),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                fire.noGravity = true;
            }

            //燃烧的余烬
            for (int i = 0; i < 50; i++) {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-18f, -6f));
                Dust ember = Dust.NewDustPerfect(
                    position,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                ember.noGravity = false;
            }

            //硫磺火环爆发
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 20; j++) {
                    float angle = MathHelper.TwoPi * j / 20f;
                    float radius = 50f + i * 35f;
                    Vector2 spawnPos = position + angle.ToRotationVector2() * radius;

                    Dust ring = Dust.NewDustPerfect(
                        spawnPos,
                        (int)CalamityDusts.Brimstone,
                        angle.ToRotationVector2() * 5f,
                        0,
                        default,
                        Main.rand.NextFloat(2.5f, 4f)
                    );
                    ring.noGravity = true;
                }
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 1.2f,
                Pitch = -0.6f
            }, position);

            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballImpact with {
                Volume = 1f,
                Pitch = -0.3f
            }, position);

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<OniHandExplode>(), Projectile.damage * 3, Projectile.knockBack, Projectile.owner);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //硫磺火击中特效
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(10f, 10f);
                Dust brimstone = Dust.NewDustPerfect(
                    target.Center,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                brimstone.noGravity = true;
            }

            //火焰爆发
            for (int i = 0; i < 10; i++) {
                Dust fire = Dust.NewDustPerfect(
                    target.Center,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(8f, 8f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                fire.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.7f,
                Pitch = 0.3f
            }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //硫磺火消散特效
            for (int i = 0; i < 40; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust brimstone = Dust.NewDustPerfect(
                    Projectile.Center,
                    (int)CalamityDusts.Brimstone,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                brimstone.noGravity = true;
            }

            //火焰余烬
            for (int i = 0; i < 20; i++) {
                Dust fire = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    Main.rand.NextVector2Circular(6f, 6f),
                    0,
                    Color.Red,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                fire.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item74 with {
                Volume = 0.6f,
                Pitch = -0.4f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D handTexture = OniMachete.OniHand;
            Vector2 origin = handTexture.Size() / 2f;

            //绘制IK手臂链-使用骨头纹理
            DrawArmChain(sb, lightColor);

            //绘制攻击拖尾
            if (State == HandState.Swinging || State == HandState.Slamming || State == HandState.Sweeping) {
                DrawAttackTrail(sb, handTexture, origin);
            }

            //绘制投掷动作残影
            if (throwActionActive && State == HandState.Throwing) {
                DrawThrowActionTrail(sb, handTexture, origin, lightColor);
            }

            //添加冲击震动偏移
            Vector2 shakeOffset = Main.rand.NextVector2Circular(impactShake, impactShake);
            Vector2 drawPos = Projectile.Center - Main.screenPosition + shakeOffset;

            //硫磺火蓄力辉光效果 - 红色能量
            if (attackWindUpIntensity > 0.3f) {
                for (int i = 0; i < 4; i++) {
                    float windUpScale = handScale * (1.2f + i * 0.15f);
                    float windUpAlpha = (attackWindUpIntensity - 0.3f) * (1f - i * 0.25f) * 0.6f;

                    sb.Draw(
                        handTexture,
                        drawPos,
                        null,
                        new Color(255, 80, 40, 0) * windUpAlpha,
                        Projectile.rotation + MathHelper.Pi,
                        origin,
                        Projectile.scale * windUpScale,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //硫磺火发光层 - 红橙色辉光
            if (glowIntensity > 0.5f) {
                for (int i = 0; i < 4; i++) {
                    float glowScale = handScale * (1.15f + i * 0.12f);
                    float glowAlpha = (glowIntensity - 0.5f) * (1f - i * 0.25f) * 0.8f;

                    sb.Draw(
                        handTexture,
                        drawPos,
                        null,
                        new Color(255, 100, 50, 0) * glowAlpha,
                        Projectile.rotation + MathHelper.Pi,
                        origin,
                        Projectile.scale * glowScale,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //主体绘制
            sb.Draw(
                handTexture,
                drawPos,
                null,
                lightColor,
                Projectile.rotation + MathHelper.Pi,
                origin,
                Projectile.scale * handScale,
                SpriteEffects.None,
                0
            );

            return false;
        }

        private void DrawArmChain(SpriteBatch sb, Color lightColor) {
            //使用骨头弹幕纹理作为链条
            Texture2D boneTexture = OniMachete.OniArm;

            for (int i = 0; i < armSegments.Count - 1; i++) {
                Vector2 start = armSegments[i + 1];
                Vector2 end = armSegments[i];
                Vector2 diff = end - start;
                float length = diff.Length();
                float rotation = diff.ToRotation() - MathHelper.ToRadians(80) * ownerDirection;

                //计算需要多少骨头来填充这段
                int boneCount = Math.Max(1, (int)(length / boneTexture.Height));

                for (int j = 0; j < boneCount; j++) {
                    float progress = j / (float)boneCount;
                    Vector2 bonePos = Vector2.Lerp(start, end, progress);

                    //根据位置添加轻微的骨头大小变化
                    float boneScale = Projectile.scale * MathHelper.Lerp(0.6f, 0.8f, (float)Math.Sin(progress * MathHelper.Pi));

                    //绘制骨头主体
                    sb.Draw(
                        boneTexture,
                        bonePos - Main.screenPosition,
                        null,
                        lightColor * 0.9f,
                        rotation + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + i + j) * 0.1f,
                        boneTexture.Size() / 2f,
                        boneScale * 2f,
                        SpriteEffects.None,
                        0
                    );

                    //硫磺火辉光层
                    if (glowIntensity > 0.4f) {
                        sb.Draw(
                            boneTexture,
                            bonePos - Main.screenPosition,
                            null,
                            new Color(255, 100, 50, 0) * glowIntensity * 0.5f,
                            rotation + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + i + j) * 0.1f,
                            boneTexture.Size() / 2f,
                            boneScale * 2.1f,
                            SpriteEffects.None,
                            0
                        );
                    }
                }
            }
        }

        private void DrawAttackTrail(SpriteBatch sb, Texture2D texture, Vector2 origin) {
            for (int i = 0; i < trailPositions.Count; i++) {
                if (i >= trailPositions.Count - 1) continue;

                float fade = (trailPositions.Count - i) / (float)trailPositions.Count;
                //硫磺火轨迹 - 红橙色
                Color trailColor = new Color(255, 120, 60, 0) * fade * 0.7f;

                sb.Draw(
                    texture,
                    trailPositions[i] - Main.screenPosition,
                    null,
                    trailColor,
                    Projectile.rotation,
                    origin,
                    Projectile.scale * handScale * (0.7f + fade * 0.3f),
                    SpriteEffects.None,
                    0
                );
            }
        }

        private void DrawThrowActionTrail(SpriteBatch sb, Texture2D texture, Vector2 origin, Color lightColor) {
            //投掷动作的强力残影效果
            int trailCount = 8;
            float throwProgress = StateTimer / ThrowDuration;

            //只在投掷动作的主要阶段(30%-70%)显示残影
            if (throwProgress >= 0.3f && throwProgress <= 0.7f) {
                float actionProgress = (throwProgress - 0.3f) / 0.4f;

                for (int i = 0; i < trailCount; i++) {
                    float trailProgress = i / (float)trailCount;

                    //计算残影位置-从起点到当前位置的插值
                    Vector2 trailPos = Vector2.Lerp(throwStartPos, Projectile.Center, 1f - trailProgress * 0.6f);

                    //残影透明度随距离衰减 - 硫磺火色彩
                    float alpha = (1f - trailProgress) * 0.6f * actionProgress;
                    Color trailColor = new Color(255, 140, 70, 0) * alpha;

                    //残影尺寸略小
                    float trailScale = handScale * (0.85f + trailProgress * 0.15f);

                    //添加轻微的旋转变化
                    float trailRotation = Projectile.rotation + (trailProgress - 0.5f) * 0.3f;

                    sb.Draw(
                        texture,
                        trailPos - Main.screenPosition,
                        null,
                        trailColor,
                        trailRotation + MathHelper.Pi,
                        origin,
                        Projectile.scale * trailScale,
                        SpriteEffects.None,
                        0
                    );
                }

                //额外的硫磺火发光层
                for (int i = 0; i < 3; i++) {
                    float glowProgress = i / 3f;
                    Vector2 glowPos = Vector2.Lerp(throwStartPos, Projectile.Center, 1f - glowProgress * 0.3f);
                    float glowAlpha = (1f - glowProgress) * 0.4f * actionProgress;

                    sb.Draw(
                        texture,
                        glowPos - Main.screenPosition,
                        null,
                        new Color(255, 100, 50, 0) * glowAlpha,
                        Projectile.rotation + MathHelper.Pi,
                        origin,
                        Projectile.scale * handScale * (1.3f + glowProgress * 0.2f),
                        SpriteEffects.None,
                        0
                    );
                }
            }
        }
    }
}
