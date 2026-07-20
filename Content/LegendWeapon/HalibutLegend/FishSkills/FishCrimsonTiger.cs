using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
    /// <summary>猩红虎鱼专属 shader 资源（域内加载器，不动 EffectLoader）</summary>
    internal class FishCrimsonTigerAssets
    {
        /// <summary>冲刺尾波绸带（预乘 alpha，配 AlphaBlend）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishCrimsonTigerWake { get; private set; }
    }

    /// <summary>猩红虎鱼技能，右键召唤虎鱼群撕咬</summary>
    internal class FishCrimsonTiger : FishSkill
    {
        public override int UnlockFishID => ItemID.CrimsonTigerfish;
        public override int DefaultCooldown => 60 * (20 - HalibutData.GetDomainLayer());
        public override int ResearchDuration => 60 * 18;

        internal static int MaxTigerFish => 5 + HalibutData.GetDomainLayer();

        //猩红语系调色板：暗红外缘 → 饱和猩红，禁冷白
        internal static readonly Color BloodDeep = new(112, 14, 24);
        internal static readonly Color BloodCrim = new(205, 36, 46);
        internal static Color BloodShade(float t) => Color.Lerp(BloodDeep, BloodCrim, t);

        public override bool? AltFunctionUse(Item item, Player player) {
            return Cooldown == 0;
        }

        public override bool? ShootAlt(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            //右键召唤虎鱼群
            if (player.altFunctionUse == 2) {
                SetCooldown();

                int alive = CountOwnedTigers(player);
                if (alive < MaxTigerFish) {
                    //在鼠标方向生成一群虎鱼
                    Vector2 mouseDir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero);
                    int spawnCount = Math.Min(3 + HalibutData.GetDomainLayer() / 3, MaxTigerFish - alive);

                    for (int i = 0; i < spawnCount; i++) {
                        float angleOffset = spawnCount > 1
                            ? MathHelper.Lerp(-0.4f, 0.4f, i / (float)(spawnCount - 1)) : 0f;
                        Vector2 spawnDir = mouseDir.RotatedBy(angleOffset);
                        Vector2 spawnPos = player.Center + spawnDir * Main.rand.NextFloat(60f, 120f);

                        Projectile.NewProjectile(
                            source,
                            spawnPos,
                            spawnDir * Main.rand.NextFloat(12f, 18f),
                            ModContent.ProjectileType<CrimsonTigerFishMinion>(),
                            (int)(damage * (0.6f + HalibutData.GetDomainLayer() * 0.15f)),
                            knockback * 2f,
                            player.whoAmI,
                            ai2: alive + i
                        );
                    }

                    SpawnSummonEffect(player.Center, mouseDir, spawnCount);

                    //虎鱼召唤音效：低吼 + 咬合 + 破水
                    SoundEngine.PlaySound(SoundID.NPCHit1 with {
                        Volume = 0.8f,
                        Pitch = -0.5f
                    }, position);

                    SoundEngine.PlaySound(SoundID.NPCHit9 with {
                        Volume = 0.7f,
                        Pitch = -0.6f
                    }, position);

                    SoundEngine.PlaySound(SoundID.SplashWeak with {
                        Volume = 0.9f,
                        Pitch = -0.2f
                    }, position);
                }

                return false; //阻止默认射击
            }

            return null;
        }

        /// <summary>直接清点在场虎鱼，替代旧 static 列表（每客户端一份的列表在 MP 语义上易漂移）</summary>
        internal static int CountOwnedTigers(Player player) {
            int fishType = ModContent.ProjectileType<CrimsonTigerFishMinion>();
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == fishType) {
                    count++;
                }
            }
            return count;
        }

        private static void SpawnSummonEffect(Vector2 position, Vector2 direction, int spawnCount) {
            if (VaultUtils.isServer) {
                return;
            }

            //定向血珠扇：沿召唤方向撕开的水面，受重力坠落
            int drops = 8 + spawnCount * 2;
            for (int i = 0; i < drops; i++) {
                Vector2 vel = direction.RotatedBy(Main.rand.NextFloat(-0.55f, 0.55f))
                    * Main.rand.NextFloat(5f, 13f);
                vel.Y -= 1.2f;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(position + direction * 30f, vel,
                    BloodShade(Main.rand.NextFloat()), Main.rand.NextFloat(0.9f, 1.5f))
                    ?.Configure(Main.rand.Next(18, 30), 0.26f);
            }

            //血色底噪
            for (int i = 0; i < 14; i++) {
                Vector2 vel = direction.RotatedByRandom(0.6f) * Main.rand.NextFloat(4f, 11f);
                Dust blood = Dust.NewDustPerfect(position + direction * 20f, DustID.Blood, vel,
                    0, default, Main.rand.NextFloat(1.4f, 2.2f));
                blood.noGravity = true;
                blood.fadeIn = 1.2f;
            }
        }
    }

    /// <summary>
    /// 猩红虎鱼召唤物：巡弋编队随行，锁敌后蓄势弹射突击。<br/>
    /// 表现三件套：速度拉伸残影链 + FishCrimsonTigerWake.fx 尾波绸带（宽度∝速度）
    /// + 咬合定帧与血珠撕裂飞沫（PRT_HeartcarverDroplet）
    /// </summary>
    internal class CrimsonTigerFishMinion : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.CrimsonTigerfish;

        //状态
        private enum TigerState
        {
            Spawning,    //破水入场
            Hunting,     //巡弋/追击
            Pouncing,    //蓄势后坐
            Dashing,     //弹射冲刺
            Biting,      //撕咬
            Returning    //返回
        }

        private TigerState State {
            get => (TigerState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float AttackTimer => ref Projectile.ai[1];
        private ref float FishIndex => ref Projectile.ai[2];
        private ref float StateTimer => ref Projectile.localAI[0];

        private int targetNPCID = -1;
        private float swimPhase = 0f;
        private float bloodLust = 0f; //嗜血状态
        private float wakeStrength = 0f;  //尾波强度，随速度平滑起落
        private float biteFlash = 0f;     //咬合过曝白闪，≤2 帧衰尽
        private int freezeFrames = 0;     //咬合定帧
        private int pounceCooldown = 0;   //弹射再蓄势间隔
        private int facingSign = 1;       //缓存朝向，防低速抖动翻面
        private float biteBaseRot = 0f;   //咬合甩头基准角
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 14;

        //狩猎参数
        private const float SearchRange = 1200f;
        private const float MaxSpeed = 24f;
        private const float Acceleration = 0.8f;
        private const int BiteDuration = 45;
        private const int SpawningDuration = 15;
        private const int PounceWindup = 9;
        private const float PounceSpeed = 27f;
        private const float PounceMinDist = 130f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 60;

            swimPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        private static float EaseOutBack(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<FishCrimsonTiger>().Active(owner)) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 60;
            AttackTimer++;
            if (pounceCooldown > 0) {
                pounceCooldown--;
            }
            biteFlash *= 0.35f;

            //咬合定帧：撕开的一瞬全画面停驻
            if (freezeFrames > 0) {
                freezeFrames--;
                Projectile.velocity = Vector2.Zero;
            }
            else {
                StateTimer++;

                //状态机
                switch (State) {
                    case TigerState.Spawning:
                        SpawningAI();
                        break;
                    case TigerState.Hunting:
                        HuntingAI(owner);
                        break;
                    case TigerState.Pouncing:
                        PouncingAI();
                        break;
                    case TigerState.Dashing:
                        DashingAI();
                        break;
                    case TigerState.Biting:
                        BitingAI();
                        break;
                    case TigerState.Returning:
                        ReturningAI(owner);
                        break;
                }
            }

            //更新拖尾锚点（取尾鳍位置，尾波从尾根撕开）
            UpdateTrail();

            //摆尾相位：速度越快摆越急
            float speed = Projectile.velocity.Length();
            swimPhase += 0.16f + speed * 0.010f;

            //尾波强度随速度平滑起落，慢游时几乎无痕
            float wakeTarget = MathHelper.Clamp((speed - 5f) / 15f, 0f, 1f);
            wakeStrength = MathHelper.Lerp(wakeStrength, wakeTarget, 0.18f);

            //朝向缓存：仅在明确横速下改判，防低速抖动翻面
            if (MathF.Abs(Projectile.velocity.X) > 0.8f) {
                facingSign = Projectile.velocity.X > 0 ? 1 : -1;
            }

            //血色光照：压暗基底，嗜血时增亮
            Lighting.AddLight(Projectile.Center, 0.30f + bloodLust * 0.28f, 0.05f, 0.07f);

            //嗜血状态衰减
            if (bloodLust > 0f) {
                bloodLust *= 0.95f;
            }

            //高速尾流：偶发血珠向后甩落，慢游不喷
            if (!VaultUtils.isServer && speed > 11f && Main.rand.NextBool(5)) {
                Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.Zero);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    TailAnchor() + Main.rand.NextVector2Circular(4f, 4f),
                    back * Main.rand.NextFloat(1.5f, 3.5f) + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    FishCrimsonTiger.BloodShade(Main.rand.NextFloat(0.7f)),
                    Main.rand.NextFloat(0.6f, 1.0f))?.Configure(Main.rand.Next(12, 20), 0.22f);
            }
        }

        private Vector2 TailAnchor()
            => Projectile.Center - Projectile.rotation.ToRotationVector2() * 16f * Projectile.scale;

        /// <summary>破水入场：快冲缓滑，easeOutBack 尺度过冲回稳，禁 pop-in</summary>
        private void SpawningAI() {
            float progress = StateTimer / SpawningDuration;

            Projectile.alpha = (int)((1f - progress) * 220f);
            Projectile.scale = 0.4f + 0.6f * EaseOutBack(progress);

            //先冲后滑：入场初速逐帧泄掉
            Projectile.velocity *= 0.965f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            //破水血珠：首帧集中甩出
            if (!VaultUtils.isServer && StateTimer <= 1f) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                        dir.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2f, 6f),
                        FishCrimsonTiger.BloodShade(Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.7f, 1.1f))?.Configure(Main.rand.Next(14, 22), 0.24f);
                }
            }

            if (StateTimer >= SpawningDuration) {
                State = TigerState.Hunting;
                StateTimer = 0;
                Projectile.alpha = 0;
                Projectile.scale = 1f;
            }
        }

        private void HuntingAI(Player owner) {
            //搜索敌人
            if (targetNPCID <= 0 || !IsTargetValid()) {
                NPC target = owner.Center.FindClosestNPC(SearchRange);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                }
            }

            if (IsTargetValid()) {
                NPC target = Main.npc[targetNPCID];
                float dist = Vector2.Distance(Projectile.Center, target.Center);

                //有冲刺空间且蓄势就绪：进入预告拍
                if (dist > PounceMinDist && dist < 900f && pounceCooldown <= 0) {
                    State = TigerState.Pouncing;
                    StateTimer = 0;
                    return;
                }

                ChaseTarget(target.Center);

                //接近目标时进入撕咬状态
                if (dist < 60f) {
                    EnterBiting(target);
                }
            }
            else {
                //无目标时返回玩家身边
                float distToOwner = Vector2.Distance(Projectile.Center, owner.Center);
                if (distToOwner > 600f) {
                    State = TigerState.Returning;
                    StateTimer = 0;
                }
                else {
                    //松散巡弋编队：慢旋椭圆轨道 + 每鱼半径错层 + 呼吸涨落
                    float orbitAngle = Main.GlobalTimeWrappedHourly * 1.1f + FishIndex * 2.4f;
                    float radius = 165f + (FishIndex % 3f) * 22f
                        + MathF.Sin(Main.GlobalTimeWrappedHourly * 0.8f + FishIndex) * 14f;
                    Vector2 orbitPos = owner.Center
                        + new Vector2(MathF.Cos(orbitAngle), MathF.Sin(orbitAngle) * 0.72f) * radius;
                    ChaseTarget(orbitPos, speedMult: 0.55f);
                    ApplySwimSway();
                }
            }

            FaceVelocity();
        }

        /// <summary>预告拍：后坐蓄势，速度泄空 + 反向缓退 + 微缩绷紧，尾波收拢</summary>
        private void PouncingAI() {
            if (!IsTargetValid()) {
                State = TigerState.Hunting;
                StateTimer = 0;
                Projectile.scale = 1f;
                return;
            }

            NPC target = Main.npc[targetNPCID];
            Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);

            float t = StateTimer / PounceWindup;
            //反向缓退：pow 迟滞，蓄势末段几乎定住
            Projectile.velocity = Projectile.velocity * 0.72f - toTarget * 1.4f * (1f - MathF.Pow(t, 3f));
            //绷紧微缩
            Projectile.scale = 1f - 0.08f * MathF.Sin(t * MathHelper.Pi);
            Projectile.rotation = toTarget.ToRotation();
            if (MathF.Abs(toTarget.X) > 0.1f) {
                facingSign = toTarget.X > 0 ? 1 : -1;
            }

            //张力泄漏：蓄势中血珠往后滴
            if (!VaultUtils.isServer && (int)StateTimer % 3 == 0) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(TailAnchor(),
                    -toTarget * Main.rand.NextFloat(1f, 2.5f) + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    FishCrimsonTiger.BloodShade(Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(10, 16), 0.20f);
            }

            //过冲拍：一帧全速弹射，尺度过冲回落接续绷紧微缩
            if (StateTimer >= PounceWindup) {
                State = TigerState.Dashing;
                StateTimer = 0;
                Projectile.scale = 1.09f;
                Projectile.velocity = toTarget * PounceSpeed;
                pounceCooldown = 45;

                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_JavelinThrowersAttack with {
                        Volume = 0.42f,
                        Pitch = 0.32f
                    }, Projectile.Center);

                    //弹射反冲血珠：向后喷出的加速度可视化
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_HeartcarverDroplet>(TailAnchor(),
                            -toTarget.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(4f, 8f),
                            FishCrimsonTiger.BloodShade(Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.8f, 1.2f))?.Configure(Main.rand.Next(14, 22), 0.26f);
                    }
                }
            }
        }

        /// <summary>释放拍：直线全速冲刺，仅微量转向修正保持锐利线条</summary>
        private void DashingAI() {
            Projectile.scale = MathHelper.Lerp(Projectile.scale, 1f, 0.15f);

            if (!IsTargetValid()) {
                State = TigerState.Hunting;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[targetNPCID];
            float dist = Vector2.Distance(Projectile.Center, target.Center);

            //有限转向：每帧至多 0.06 rad，冲刺读作一条直线（定帧清零后用朝向兜底）
            Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            Vector2 current = Projectile.velocity.SafeNormalize(Projectile.rotation.ToRotationVector2());
            float turn = MathHelper.Clamp(
                MathHelper.WrapAngle(desired.ToRotation() - current.ToRotation()), -0.06f, 0.06f);
            Projectile.velocity = current.RotatedBy(turn) * PounceSpeed;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (dist < 64f) {
                EnterBiting(target);
                return;
            }

            //冲刺超时：目标已脱离弹道，回巡航追击（36 帧足够覆盖 900px 弹道）
            if (StateTimer > 36f) {
                State = TigerState.Hunting;
                StateTimer = 0;
            }
        }

        private void EnterBiting(NPC target, bool playSound = true) {
            State = TigerState.Biting;
            StateTimer = 0;
            bloodLust = 1f;
            biteBaseRot = (target.Center - Projectile.Center).ToRotation();

            //咬击音效（OnHitNPC 路径已有自己的分层音，跳过防同帧堆叠）
            if (playSound) {
                SoundEngine.PlaySound(SoundID.NPCHit9 with {
                    Volume = 0.6f,
                    Pitch = 0.2f
                }, Projectile.Center);
            }
        }

        private void BitingAI() {
            Projectile.scale = MathHelper.Lerp(Projectile.scale, 1f, 0.25f);

            if (!IsTargetValid()) {
                State = TigerState.Hunting;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[targetNPCID];

            //附着在目标身上疯狂撕咬
            Projectile.Center = Vector2.Lerp(
                Projectile.Center,
                target.Center + Main.rand.NextVector2Circular(target.width / 3f, target.height / 3f),
                0.3f
            );

            //甩头撕扯：绕咬合角往复摆动，非连续自旋
            Projectile.rotation = biteBaseRot + MathF.Sin(StateTimer * 0.9f) * 0.5f;

            //减速
            Projectile.velocity *= 0.8f;

            //撕裂飞沫节拍
            if ((int)StateTimer % 8 == 0) {
                SpawnBiteEffect(target);
            }

            //撕咬结束
            if (StateTimer >= BiteDuration) {
                State = TigerState.Hunting;
                StateTimer = 0;
                targetNPCID = -1;

                //弹开
                Vector2 bounceDir = (Projectile.Center - target.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = bounceDir * 16f;
            }
        }

        private void ReturningAI(Player owner) {
            Vector2 toOwner = owner.Center - Projectile.Center;
            float distance = toOwner.Length();

            //快速返回
            ChaseTarget(owner.Center, speedMult: 1.5f);
            ApplySwimSway();

            //返回后切换回狩猎状态
            if (distance < 100f) {
                State = TigerState.Hunting;
                StateTimer = 0;
            }

            FaceVelocity();
        }

        private void ChaseTarget(Vector2 targetPos, float speedMult = 1f) {
            Vector2 toTarget = targetPos - Projectile.Center;
            float distance = toTarget.Length();

            if (distance > 5f) {
                Vector2 desiredVelocity = toTarget.SafeNormalize(Vector2.Zero) *
                    Math.Min(MaxSpeed * speedMult, distance * 0.1f);

                Projectile.velocity += (desiredVelocity - Projectile.velocity) * (Acceleration * 0.1f);

                if (Projectile.velocity.Length() > MaxSpeed * speedMult) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * MaxSpeed * speedMult;
                }
            }
        }

        /// <summary>游动横摆：垂直速度方向的正弦摆动力，每鱼相位错开</summary>
        private void ApplySwimSway() {
            if (Projectile.velocity.LengthSquared() < 1f) {
                return;
            }
            Vector2 side = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity += side * MathF.Sin(swimPhase) * 0.32f;
        }

        private void FaceVelocity() {
            if (Projectile.velocity.LengthSquared() > 1f) {
                Projectile.rotation = MathHelper.Lerp(
                    Projectile.rotation,
                    Projectile.velocity.ToRotation(),
                    0.2f
                );
            }
        }

        private void UpdateTrail() {
            Vector2 anchor = TailAnchor();
            if (trailPositions.Count == 0
                || Vector2.DistanceSquared(anchor, trailPositions[0]) > 4f) {
                trailPositions.Insert(0, anchor);
            }
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) {
                return false;
            }
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy() && !target.friendly;
        }

        private void SpawnBiteEffect(NPC target) {
            if (VaultUtils.isServer) {
                return;
            }

            //撕裂飞沫：从咬合点沿甩头方向锥形甩出，受重力
            Vector2 fling = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    Projectile.Center + fling * 12f,
                    fling.RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(3f, 7f),
                    FishCrimsonTiger.BloodShade(Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.3f))?.Configure(Main.rand.Next(16, 26), 0.28f);
            }

            for (int i = 0; i < 2; i++) {
                Dust blood = Dust.NewDustPerfect(
                    target.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Blood,
                    Main.rand.NextVector2Circular(5f, 5f),
                    0, default, Main.rand.NextFloat(1.3f, 2f));
                blood.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //造成流血debuff
            target.AddBuff(BuffID.Bleeding, 180 + HalibutData.GetDomainLayer() * 15);

            //咬合定帧 + 过曝白闪（≤2 帧衰尽）
            freezeFrames = 2;
            biteFlash = 1f;

            //冲刺/蓄势中撞上目标：命中即咬合达成，防止定帧清速后方向漂移
            if (State == TigerState.Dashing || State == TigerState.Pouncing) {
                targetNPCID = target.whoAmI;
                EnterBiting(target, playSound: false);
            }

            //增强嗜血状态
            bloodLust = Math.Min(bloodLust + 0.3f, 1.5f);

            SoundEngine.PlaySound(SoundID.NPCHit9 with {
                Volume = 0.5f,
                Pitch = 0.1f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.35f,
                Pitch = 0.55f
            }, Projectile.Center);

            if (VaultUtils.isServer) {
                return;
            }

            //击中撕裂喷溅：沿入射向的血珠锥
            Vector2 inDir = Projectile.velocity.SafeNormalize(
                (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX));
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center,
                    inDir.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(4f, 10f),
                    FishCrimsonTiger.BloodShade(Main.rand.NextFloat()),
                    Main.rand.NextFloat(1f, 1.7f))?.Configure(Main.rand.Next(18, 30), 0.30f);
            }
            for (int i = 0; i < 4; i++) {
                Dust blood = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    Main.rand.NextVector2Circular(7f, 7f), 0, default, Main.rand.NextFloat(1.6f, 2.4f));
                blood.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                //化血消散
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                        Main.rand.NextVector2Circular(4.5f, 4.5f),
                        FishCrimsonTiger.BloodShade(Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.9f, 1.6f))?.Configure(Main.rand.Next(16, 26), 0.26f);
                }

                //尾波余韵：血珠沿轨迹排布，尾端寿命更短先蚀掉
                for (int i = 0; i < trailPositions.Count; i += 2) {
                    Vector2 drift = i + 1 < trailPositions.Count
                        ? (trailPositions[i] - trailPositions[i + 1]) * 0.25f : Vector2.Zero;
                    int life = Math.Max(6, 20 - i);
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(trailPositions[i],
                        drift + Main.rand.NextVector2Circular(0.8f, 0.8f),
                        FishCrimsonTiger.BloodShade(Main.rand.NextFloat(0.6f)),
                        Main.rand.NextFloat(0.6f, 1.0f))?.Configure(life, 0.20f);
                }

                for (int i = 0; i < 6; i++) {
                    Dust blood = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                        Main.rand.NextVector2Circular(5f, 5f), 0, default, Main.rand.NextFloat(1.4f, 2.2f));
                    blood.noGravity = true;
                }
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.4f,
                Pitch = -0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            if (!TextureAssets.Item[ItemID.CrimsonTigerfish].IsLoaded) {
                Main.instance.LoadItem(ItemID.CrimsonTigerfish);
            }
            Texture2D fishTex = TextureAssets.Item[ItemID.CrimsonTigerfish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;
            //贴图斜置：右向 +PiOver4 校正，左向垂直翻转取 -PiOver4
            SpriteEffects spriteEffects = facingSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = Projectile.rotation + (facingSign > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);

            float alpha = (255f - Projectile.alpha) / 255f;

            //速度残影链：位置链编码运动方向，速度低时消隐
            float speedFactor = MathHelper.Clamp((Projectile.velocity.Length() - 6f) / 16f, 0f, 1f);
            float ghostBoost = MathHelper.Clamp(speedFactor + bloodLust * 0.3f, 0f, 1f);
            if (ghostBoost > 0.05f) {
                int[] ghostIdx = { 1, 3, 5 };
                for (int g = 0; g < ghostIdx.Length; g++) {
                    int idx = ghostIdx[g];
                    if (idx >= trailPositions.Count) {
                        break;
                    }
                    float fade = (1f - g / 3f) * 0.42f * ghostBoost * alpha;
                    //残影锚回尾迹点，从尾锚折回体心
                    Vector2 ghostPos = trailPositions[idx]
                        + Projectile.rotation.ToRotationVector2() * 16f * Projectile.scale
                        - Main.screenPosition;
                    Color ghostColor = FishCrimsonTiger.BloodShade(0.35f + 0.3f * (1f - g / 3f)) * fade;
                    sb.Draw(fishTex, ghostPos, null, ghostColor, drawRot, origin,
                        Projectile.scale * (0.95f - g * 0.06f), spriteEffects, 0);
                }
            }

            //嗜血辉光：单层加色描边，不再三层同贴图堆叠
            if (bloodLust > 0.3f) {
                sb.Draw(fishTex, drawPos, null,
                    new Color(205, 40, 46, 0) * (bloodLust * 0.38f * alpha),
                    drawRot, origin, Projectile.scale * 1.10f, spriteEffects, 0);
            }

            //主体绘制，嗜血时染猩红
            Color mainColor = Color.Lerp(lightColor, FishCrimsonTiger.BloodCrim, bloodLust * 0.55f);
            sb.Draw(fishTex, drawPos, null, mainColor * alpha, drawRot, origin,
                Projectile.scale, spriteEffects, 0);

            //咬合白闪：≤2 帧过曝爆点
            if (biteFlash > 0.08f) {
                sb.Draw(fishTex, drawPos, null,
                    new Color(255, 235, 225, 0) * (biteFlash * 0.85f * alpha),
                    drawRot, origin, Projectile.scale * 1.04f, spriteEffects, 0);
            }

            return false;
        }

        /// <summary>尾波绸带：沿尾锚轨迹的 TriangleStrip，宽度∝速度，慢游近乎无痕</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || wakeStrength < 0.06f) {
                return;
            }
            Effect fx = FishCrimsonTigerAssets.FishCrimsonTigerWake;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            //采样点：尾锚打头，历史轨迹向尾（去过近点）
            Span<Vector2> pts = stackalloc Vector2[1 + MaxTrailLength];
            int count = 0;
            pts[count++] = TailAnchor();
            for (int k = 0; k < trailPositions.Count; k++) {
                Vector2 p = trailPositions[k];
                if (Vector2.DistanceSquared(p, pts[count - 1]) < 9f) {
                    continue;
                }
                pts[count++] = p;
            }
            if (count < 3) {
                return;
            }

            //条带顶点：头段快速铺满宽度，向尾收成撕开的尖
            float maxWidth = (3.5f + 8.5f * wakeStrength) * Projectile.scale;
            var verts = new VertexPositionColorTexture[count * 2];
            for (int i = 0; i < count; i++) {
                float t = i / (float)(count - 1);
                Vector2 tangent = i < count - 1
                    ? (pts[i] - pts[i + 1]).SafeNormalize(Vector2.UnitX)
                    : (pts[i - 1] - pts[i]).SafeNormalize(Vector2.UnitX);
                Vector2 normal = new(-tangent.Y, tangent.X);
                float width = maxWidth * (0.5f + 0.5f * MathHelper.Clamp(t / 0.18f, 0f, 1f))
                    * MathF.Pow(1f - t, 0.85f);
                verts[i * 2] = new VertexPositionColorTexture((pts[i] + normal * width).ToVector3()
                    , Color.White, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pts[i] - normal * width).ToVector3()
                    , Color.White, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            float alpha = (255f - Projectile.alpha) / 255f;
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.61f % 1f);
            fx.Parameters["uFade"]?.SetValue(wakeStrength * alpha);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, verts.Length - 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }
    }
}
