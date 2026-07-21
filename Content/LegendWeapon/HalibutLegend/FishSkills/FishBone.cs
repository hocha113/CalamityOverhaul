using InnoVault.GameContent.BaseEntity;
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
    internal class FishBone : FishSkill
    {
        public override int UnlockFishID => ItemID.Bonefish;

        public override int DefaultCooldown => 100 - HalibutData.GetDomainLayer() * 4;
        public override int ResearchDuration => 60 * 12;
        //活跃骨头索引表
        private static readonly List<int> ActiveBones = new();
        private static int MaxBones => 3 + HalibutData.GetDomainLayer() / 2; //最多3-8根骨头

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();
                //检查当前活跃的骨头数量
                CleanupInactiveBones();

                if (ActiveBones.Count < MaxBones) {
                    //生成新的骨头弹幕
                    int boneProj = Projectile.NewProjectile(
                        source,
                        player.Center,
                        Vector2.Zero, //初始速度为0
                        ModContent.ProjectileType<BonefishOrbit>(),
                        (int)(damage * (2.4f + HalibutData.GetDomainLayer() * 0.9f)),
                        knockback * 0.25f,
                        player.whoAmI,
                        ai0: ActiveBones.Count //错开动画索引
                    );

                    if (boneProj >= 0 && boneProj < Main.maxProjectiles) {
                        ActiveBones.Add(boneProj);

                        SpawnSummonEffect(player.Center);

                        //骨质召唤音效
                        SoundEngine.PlaySound(SoundID.Item1 with {
                            Volume = 0.5f,
                            Pitch = -0.4f + ActiveBones.Count * 0.05f
                        }, player.Center);
                    }
                }
            }

            return null;
        }


        private static void CleanupInactiveBones() {
            ActiveBones.RemoveAll(id => id < 0 || id >= Main.maxProjectiles || !Main.projectile[id].active);
        }

        /// <summary>召唤演出</summary>
        private static void SpawnSummonEffect(Vector2 position) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), Main.rand.NextFloat(-4.2f, -1.6f));
                PRTLoader.NewParticle<PRT_FishBoneShard>(position + Main.rand.NextVector2Circular(10f, 10f)
                    , vel, FishBonePalette.Chip(), Main.rand.NextFloat(0.6f, 0.95f))?.Configure(Main.rand.Next(18, 28));
            }
            PRTLoader.NewParticle<PRT_FishBoneDust>(position, new Vector2(0f, -0.7f)
                , FishBonePalette.Chalk, Main.rand.NextFloat(0.15f, 0.20f))?.Configure(26);
            for (int i = 0; i < 5; i++) {
                Dust.NewDustPerfect(position + Main.rand.NextVector2Circular(12f, 12f), DustID.Bone
                    , Main.rand.NextVector2Circular(2f, 2f), 120, default, Main.rand.NextFloat(0.8f, 1.3f));
            }
        }

        //受伤判定，借免疫帧窗口
        public static bool IsPlayerHurt(Player player) => player.immuneTime > 0;
    }

    internal class BonefishOrbit : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bone;

        //状态机
        private enum BoneState
        {
            Gathering,    //聚集阶段，骨头飞向玩家
            Orbiting,     //环绕阶段，环绕玩家加速旋转
            Charging,     //蓄力阶段，继续加速，准备发射
            Launching,    //发射阶段，回旋镖外程，加速弯弧
            Scattering,   //碎裂阶段
            Returning,    //折返阶段
            Dissolving    //消散阶段，技能切换的退场收尾
        }

        private BoneState State {
            get => (BoneState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }

        private ref float BoneIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[2];

        //环绕参数
        private float orbitRadius = 120f;
        private float orbitAngle = 0f;
        private float orbitSpeed = 0.05f;
        private const float MaxOrbitSpeed = 0.5f;

        //蓄力参数
        private static int GatherDuration => 20 - HalibutData.GetDomainLayer();      //聚集时间
        private static int OrbitDuration => 60 - HalibutData.GetDomainLayer() * 3;       //环绕时间
        private static int ChargeDuration => 40 - HalibutData.GetDomainLayer() * 2;      //蓄力时间
        private const float LaunchSpeed = 28f;      //发射速度

        //外程运动学
        private const float LaunchMul = 0.6f;           //出手初速占比，余量给外程挤压
        private const float FlightAccelMul = 1.045f;    //外程每帧复合加速，16帧约把初速翻倍
        private const float FlightBrakeMul = 0.78f;     //加速窗关闭后每帧硬刹
        private const float FlightCurveRad = 0.014f;    //外程每帧弯弧弧度，弯向随骨序号交替
        private const int OutboundAccelFrames = 16;     //外程加速窗口帧数
        private const int MaxOutboundFrames = 46;       //外程超时上限
        private const int ApexFrames = 5;               //顶点悬滞帧数
        private const float ReturnMaxSpeed = 30f;       //折返吸附峰值

        //视觉量（各端本地，不参与同步）
        private float trailIntensity = 0f;  //残影链强度
        private float spinRate = 0.15f;     //当前自旋角速度，旋转拖影按它回溯
        private float flightTopSpeed = LaunchSpeed; //本次外程速度上限，出手时按角动量算定
        private float catchPulse = 0f;      //接骨顿挫包络，归位时半径下沉再弹回
        private float shatterGap = 0f;      //碎裂定帧两半分离像素

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10086;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 2f;
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            //入场从半尺寸长起，防 pop-in
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                Projectile.scale = 0.55f;
            }

            //受伤当场碎裂，技能切换走消散退场，两个终态不可再打断
            if (State != BoneState.Scattering && State != BoneState.Dissolving) {
                if (!FishSkill.GetT<FishBone>().Active(Owner)) {
                    EnterDissolveState();
                }
                else if (FishBone.IsPlayerHurt(Owner)) {
                    EnterScatterState();
                }
            }

            StateTimer++;

            if (Projectile.scale < 1.5f && State != BoneState.Dissolving) {
                Projectile.scale = MathF.Min(1.5f, Projectile.scale + 0.022f);
            }

            //状态机
            switch (State) {
                case BoneState.Gathering:
                    GatheringPhaseAI(Owner);
                    break;

                case BoneState.Orbiting:
                    OrbitingPhaseAI(Owner);
                    break;

                case BoneState.Charging:
                    ChargingPhaseAI(Owner);
                    break;

                case BoneState.Launching:
                    LaunchingPhaseAI();
                    break;

                case BoneState.Returning:
                    ReturningPhaseAI(Owner);
                    break;

                case BoneState.Scattering:
                    ScatteringPhaseAI();
                    break;

                case BoneState.Dissolving:
                    DissolvingPhaseAI();
                    break;
            }

            //自旋角速度随运动能量
            float targetSpin = State switch {
                BoneState.Launching => 0.85f,
                BoneState.Returning => 0.95f,
                BoneState.Charging => MathHelper.Lerp(0.30f, 0.80f, orbitSpeed / MaxOrbitSpeed),
                BoneState.Scattering => 0.02f,
                BoneState.Dissolving => 0.06f,
                _ => MathHelper.Lerp(0.15f, 0.60f, orbitSpeed / MaxOrbitSpeed),
            };
            spinRate = MathHelper.Lerp(spinRate, targetSpin, 0.2f);
            Projectile.rotation += spinRate;

            catchPulse *= 0.88f;
        }

        /// <summary>待机沉浮</summary>
        private Vector2 BobOffset() {
            float phase = BoneIndex * 2.399f;
            return new Vector2(0f, MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f + phase) * 5f);
        }

        /// <summary>聚集阶段</summary>
        private void GatheringPhaseAI(Player owner) {
            float progress = StateTimer / GatherDuration;

            //计算初始环绕位置
            float targetAngle = MathHelper.TwoPi * BoneIndex / 8f;
            Vector2 targetPos = owner.Center + targetAngle.ToRotationVector2() * orbitRadius;

            //EaseOutCubic 冲刺
            float easeProgress = VaultUtils.EaseOutCubic(progress);
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, easeProgress * 0.4f);

            //提前开始旋转
            orbitAngle = targetAngle;

            //成形途中零星掉钙屑
            if (!Main.dedServ && Main.rand.NextBool(6)) {
                Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.Bone
                    , Main.rand.NextVector2Circular(0.8f, 0.5f), 130, default, Main.rand.NextFloat(0.6f, 0.9f));
            }

            //转入环绕阶段
            if (StateTimer >= GatherDuration) {
                State = BoneState.Orbiting;
                StateTimer = 0;
                catchPulse = 0.7f; //入位半径下沉再弹回

                //骨头碰撞音效
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.4f,
                    Pitch = 0.3f
                }, Projectile.Center);
            }
        }

        /// <summary>环绕阶段</summary>
        private void OrbitingPhaseAI(Player owner) {
            float progress = StateTimer / OrbitDuration;

            //加速旋转（使用EaseInQuad）
            float speedProgress = VaultUtils.EaseInQuad(progress);
            orbitSpeed = MathHelper.Lerp(0.05f, MaxOrbitSpeed * 0.6f, speedProgress);

            //半径脉冲，接骨顿挫时半径先下沉再弹回
            float radiusPulse = MathF.Sin(StateTimer * 0.3f) * 10f;
            float currentRadius = orbitRadius + radiusPulse * progress - 16f * catchPulse * catchPulse;

            //更新环绕角度
            orbitAngle += orbitSpeed;

            //计算环绕位置，叠上错相位浮动
            Vector2 orbitOffset = orbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = owner.Center + orbitOffset + BobOffset();

            //平滑跟随
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.3f);

            trailIntensity = progress;

            //低频钙屑剥落
            if (!Main.dedServ && Main.rand.NextBool(14)) {
                Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.Bone
                    , Main.rand.NextVector2Circular(0.6f, 0.4f), 130, default, Main.rand.NextFloat(0.6f, 0.9f));
            }

            //周期性骨头摩擦音效
            if (StateTimer % (int)MathHelper.Lerp(25, 8, progress) == 0) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.25f * progress,
                    Pitch = -0.5f + progress * 0.3f
                }, Projectile.Center);
            }

            //转入蓄力阶段
            if (StateTimer >= OrbitDuration) {
                State = BoneState.Charging;
                StateTimer = 0;

                //骨质蓄力音效
                SoundEngine.PlaySound(SoundID.Item67 with {
                    Volume = 0.6f,
                    Pitch = -0.4f
                }, Projectile.Center);
            }
        }

        /// <summary>蓄力阶段</summary>
        private void ChargingPhaseAI(Player owner) {
            float progress = StateTimer / ChargeDuration;

            //达到最高旋转速度
            orbitSpeed = MathHelper.Lerp(MaxOrbitSpeed * 0.6f, MaxOrbitSpeed, VaultUtils.EaseInOutQuad(progress));

            //半径震荡收紧
            float radiusOscillation = MathF.Sin(StateTimer * 0.5f) * 15f * progress;
            float currentRadius = orbitRadius - 20f * progress + radiusOscillation;

            //更新环绕
            orbitAngle += orbitSpeed;
            Vector2 orbitOffset = orbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = owner.Center + orbitOffset + BobOffset() * (1f - progress);

            //末4帧朝掷出反方向拉弓，出手前的反向预压
            int framesLeft = ChargeDuration - (int)StateTimer;
            if (framesLeft <= 4 && framesLeft >= 0) {
                targetPos -= UnitToMouseV * ((5 - framesLeft) * 2.5f);
            }
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.4f);

            trailIntensity = 1f;

            //高速摩擦剥落升频
            if (!Main.dedServ && Main.rand.NextBool(6)) {
                Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.Bone
                    , Main.rand.NextVector2Circular(1.2f, 0.8f), 130, default, Main.rand.NextFloat(0.7f, 1f));
            }

            //高频骨头碰撞音效
            if (StateTimer % 6 == 0) {
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.15f + progress * 0.25f,
                    Pitch = 0.3f + progress * 0.4f
                }, Projectile.Center);
            }

            //转入发射阶段
            if (StateTimer >= ChargeDuration) {
                State = BoneState.Launching;
                StateTimer = 0;
                LaunchToTarget();
            }
        }

        /// <summary>向锁定目标发射</summary>
        private void LaunchToTarget() {
            //速度上限叠当前角速度，出手只给六成
            float momentumBonus = orbitSpeed / MaxOrbitSpeed;
            flightTopSpeed = LaunchSpeed * (1f + momentumBonus * 0.5f);

            Projectile.velocity = UnitToMouseV * (flightTopSpeed * LaunchMul);
            if (!Framing.GetTileSafely(Projectile.Center.ToTileCoordinates16()).HasTile) {
                Projectile.tileCollide = true;
            }

            //出手崩屑
            if (!Main.dedServ) {
                Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_FishBoneShard>(Projectile.Center
                        , back.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 6f) + Projectile.velocity * 0.08f
                        , FishBonePalette.Chip(), Main.rand.NextFloat(0.7f, 1.1f))?.Configure(Main.rand.Next(18, 28));
                }
                PRTLoader.NewParticle<PRT_FishBoneDust>(Projectile.Center, back * 1.4f
                    , FishBonePalette.Chalk, Main.rand.NextFloat(0.16f, 0.22f))?.Configure(26);
            }

            //轻骨破空双层
            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.75f,
                Pitch = 0.45f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.5f,
                Pitch = -0.2f
            }, Projectile.Center);
        }

        /// <summary>外程阶段</summary>
        private void LaunchingPhaseAI() {
            bool accelWindow = StateTimer <= OutboundAccelFrames;
            Projectile.velocity *= accelWindow ? FlightAccelMul : FlightBrakeMul;

            //弯弧方向随骨序号交替，多骨齐飞时左右弧线交织
            float curveDir = (int)BoneIndex % 2 == 0 ? 1f : -1f;
            Projectile.velocity = Projectile.velocity.RotatedBy(FlightCurveRad * curveDir);

            trailIntensity = 1f;
            FlightShedAndWhoosh();

            //刹到残速或超时
            if ((!accelWindow && Projectile.velocity.Length() < 7f) || StateTimer > MaxOutboundFrames) {
                State = BoneState.Returning;
                StateTimer = 0;
                Projectile.tileCollide = false;
                Projectile.velocity *= 0.4f;
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.3f,
                    Pitch = 0.5f
                }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 2; i++) {
                        PRTLoader.NewParticle<PRT_FishBoneDust>(Projectile.Center
                            , Main.rand.NextVector2Circular(1.2f, 1.2f), FishBonePalette.Chalk
                            , Main.rand.NextFloat(0.13f, 0.18f))?.Configure(Main.rand.Next(20, 28));
                    }
                }
            }
        }

        /// <summary>折返阶段，顶点悬滞几帧后复利加速拽回，贴近环绕半径无缝接回相位</summary>
        private void ReturningPhaseAI(Player owner) {
            Vector2 toOwner = owner.Center - Projectile.Center;

            if (StateTimer <= ApexFrames) {
                //悬帧，残速继续泄掉
                Projectile.velocity *= 0.9f;
            }
            else {
                float returnSpeed = MathF.Min(8f * MathF.Pow(1.09f, StateTimer - ApexFrames), ReturnMaxSpeed);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity
                    , toOwner.SafeNormalize(Vector2.Zero) * returnSpeed, 0.42f);
            }

            trailIntensity = 1f;
            FlightShedAndWhoosh();

            //回到环绕半径
            if (toOwner.Length() < orbitRadius + 30f && StateTimer > ApexFrames) {
                State = BoneState.Orbiting;
                StateTimer = 0;
                orbitAngle = (Projectile.Center - owner.Center).ToRotation();
                orbitSpeed = MaxOrbitSpeed * 0.35f;
                catchPulse = 1f;
                //清掉线速度残留
                Projectile.velocity = Vector2.Zero;

                //归位轻响 + 两粒接骨尘
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.3f,
                    Pitch = 0.45f
                }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int i = 0; i < 2; i++) {
                        Dust.NewDustPerfect(Projectile.Center, DustID.Bone
                            , Main.rand.NextVector2Circular(1.8f, 1.8f), 120, default, Main.rand.NextFloat(0.7f, 1f));
                    }
                }
            }
        }

        /// <summary>飞行途中的剥落与呼啸</summary>
        private void FlightShedAndWhoosh() {
            float speed = Projectile.velocity.Length();

            if (!Main.dedServ && speed > 6f) {
                int cadence = speed > 20f ? 2 : 4;
                if (StateTimer % cadence == 0) {
                    Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.4f, DustID.Bone
                        , -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.5f, 0.5f)
                        , 140, default, Main.rand.NextFloat(0.6f, 1f));
                }
            }

            //回旋镖破空呼啸
            if (StateTimer % 7 == 0 && speed > 10f) {
                float speedT = MathHelper.Clamp((speed - 10f) / 24f, 0f, 1f);
                SoundEngine.PlaySound(SoundID.Item32 with {
                    Volume = 0.2f + 0.2f * speedT,
                    Pitch = -0.35f + speedT * 0.75f,
                    MaxInstances = 3
                }, Projectile.Center);
            }
        }

        /// <summary>碎裂阶段，先裂开定帧（两半撑缝悬滞），第4帧崩成骨屑与钙尘</summary>
        private void ScatteringPhaseAI() {
            Projectile.velocity *= 0.82f;
            shatterGap += 1.6f;

            //裂缝漏干粉
            if (!Main.dedServ) {
                Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f), DustID.Bone
                    , Main.rand.NextVector2Circular(0.8f, 0.8f), 140, default, Main.rand.NextFloat(0.6f, 0.9f));
            }

            if (StateTimer >= 4) {
                SpawnShatterBurst();
                Projectile.Kill();
            }
        }

        /// <summary>切入碎裂，受伤瞬间进入裂开定帧，多骨齐碎按数量压音量</summary>
        private void EnterScatterState() {
            State = BoneState.Scattering;
            StateTimer = 0;
            shatterGap = 0f;
            Projectile.tileCollide = false;
            Projectile.friendly = false;
            Projectile.velocity *= 0.25f;

            //骨裂双层，脆断 + 闷崩
            int count = Math.Max(1, Owner.ownedProjectileCounts[Projectile.type]);
            float volumeScale = 1f / MathF.Sqrt(count);
            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.5f * volumeScale,
                Pitch = 0.2f
            }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Dig with {
                Volume = 0.4f * volumeScale,
                Pitch = 0.5f
            }, Projectile.Center);
        }

        /// <summary>切入消散</summary>
        private void EnterDissolveState() {
            State = BoneState.Dissolving;
            StateTimer = 0;
            Projectile.tileCollide = false;
            Projectile.friendly = false;
            Projectile.velocity = Vector2.Zero;
        }

        /// <summary>消散阶段</summary>
        private void DissolvingPhaseAI() {
            Projectile.scale *= 0.9f;
            Projectile.alpha = Math.Min(255, Projectile.alpha + 42);
            trailIntensity *= 0.8f;

            if (!Main.dedServ && (StateTimer == 1 || StateTimer == 3)) {
                Dust.NewDustPerfect(Projectile.Center, DustID.Bone
                    , Main.rand.NextVector2Circular(0.9f, 0.9f), 150, default, Main.rand.NextFloat(0.6f, 0.9f));
            }

            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }
        }

        /// <summary>碎裂英雄时刻</summary>
        private void SpawnShatterBurst() {
            if (Main.dedServ) {
                return;
            }
            int count = Math.Max(1, Owner.ownedProjectileCounts[Projectile.type]);
            int shardN = count >= 6 ? 3 : count >= 4 ? 4 : 5;
            int dustN = count >= 5 ? 1 : 2;

            Vector2 inherit = Projectile.velocity * 0.45f;
            for (int i = 0; i < shardN; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 7f) + inherit;
                vel.Y -= Main.rand.NextFloat(1.5f); //上抛偏置，落回时读出重量
                PRTLoader.NewParticle<PRT_FishBoneShard>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                    , vel, FishBonePalette.Chip(), Main.rand.NextFloat(0.8f, 1.25f))?.Configure(Main.rand.Next(22, 34));
            }
            for (int i = 0; i < dustN; i++) {
                PRTLoader.NewParticle<PRT_FishBoneDust>(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f)
                    , Main.rand.NextVector2Circular(1.2f, 0.9f) + inherit * 0.2f
                    , FishBonePalette.Chalk, Main.rand.NextFloat(0.15f, 0.24f))?.Configure(Main.rand.Next(30, 44));
            }
            //几粒环境骨尘补底噪
            for (int i = 0; i < 4; i++) {
                Dust.NewDustPerfect(Projectile.Center, DustID.Bone
                    , Main.rand.NextVector2Circular(3f, 3f) + inherit * 0.3f, 100, default, Main.rand.NextFloat(0.9f, 1.4f));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰撞后反弹
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.8f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.8f;
            }

            //骨头碰撞音效
            SoundEngine.PlaySound(SoundID.Dig with {
                Volume = 0.5f,
                Pitch = 0.4f
            }, Projectile.Center);

            //撞墙崩屑，沿反弹方向啃下几片
            if (!Main.dedServ) {
                Vector2 outDir = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_FishBoneShard>(Projectile.Center
                        , outDir.RotatedByRandom(0.6f) * Main.rand.NextFloat(1.5f, 4f)
                        , FishBonePalette.Chip(), Main.rand.NextFloat(0.55f, 0.85f))?.Configure(Main.rand.Next(16, 24));
                }
                for (int i = 0; i < 3; i++) {
                    Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Bone
                        , Scale: Main.rand.NextFloat(0.8f, 1.2f));
                }
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中崩屑
            if (!Main.dedServ) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Main.rand.NextVector2Unit());
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_FishBoneShard>(Projectile.Center
                        , dir.RotatedByRandom(0.8f) * Main.rand.NextFloat(2f, 5.5f)
                        , FishBonePalette.Chip(), Main.rand.NextFloat(0.6f, 0.95f))?.Configure(Main.rand.Next(16, 26));
                }
                PRTLoader.NewParticle<PRT_FishBoneDust>(Projectile.Center, dir * 0.8f
                    , FishBonePalette.Chalk, Main.rand.NextFloat(0.13f, 0.18f))?.Configure(22);
                for (int i = 0; i < 3; i++) {
                    Dust.NewDustPerfect(Projectile.Center, DustID.Bone
                        , Main.rand.NextVector2Circular(3f, 3f), 120, default, Main.rand.NextFloat(0.8f, 1.2f));
                }
            }

            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit2 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D boneTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = boneTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;

            float alpha = (255f - Projectile.alpha) / 255f;
            if (alpha <= 0.01f) {
                return false;
            }

            //哑光基色
            Color baseColor = Color.Lerp(lightColor, lightColor.MultiplyRGB(FishBonePalette.Aged), 0.35f) * alpha;

            //碎裂定帧，本体换成撑缝的两半
            if (State == BoneState.Scattering) {
                DrawShatterHalves(sb, boneTex, drawPos, baseColor);
                return false;
            }

            DrawMotionGhosts(sb, boneTex, sourceRect, origin, baseColor);
            DrawSpinSmear(sb, boneTex, sourceRect, origin, baseColor);

            sb.Draw(
                boneTex,
                drawPos,
                sourceRect,
                baseColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }

        /// <summary>碎裂定帧，骨身沿长轴劈成两半，缝隙渐开并各自微偏转</summary>
        private void DrawShatterHalves(SpriteBatch sb, Texture2D tex, Vector2 drawPos, Color baseColor) {
            int w = tex.Width, h = tex.Height;
            Rectangle topRect = new(0, 0, w, h / 2);
            Rectangle bottomRect = new(0, h / 2, w, h - h / 2);
            //两半原点都取骨心，绕同一轴撑开
            Vector2 topOrigin = new(w / 2f, h / 2f);
            Vector2 bottomOrigin = new(w / 2f, 0f);

            Vector2 apart = (-Vector2.UnitY * shatterGap).RotatedBy(Projectile.rotation);
            float splitTwist = StateTimer * 0.06f;

            sb.Draw(tex, drawPos + apart, topRect, baseColor, Projectile.rotation - splitTwist
                , topOrigin, Projectile.scale, SpriteEffects.None, 0);
            sb.Draw(tex, drawPos - apart, bottomRect, baseColor, Projectile.rotation + splitTwist
                , bottomOrigin, Projectile.scale, SpriteEffects.None, 0);
        }

        /// <summary>位移残影链</summary>
        private void DrawMotionGhosts(SpriteBatch sb, Texture2D tex, Rectangle frame, Vector2 origin, Color baseColor) {
            float speedT = MathHelper.Clamp(Projectile.velocity.Length() / 24f, 0f, 1f);
            //环绕时速度场为零，用轨迹强度顶上（线速度由角速度产生）
            float strength = MathF.Max(speedT, trailIntensity * 0.5f);
            if (strength <= 0.04f) {
                return;
            }

            int count = State == BoneState.Launching || State == BoneState.Returning ? 8 : 5;
            for (int i = 1; i <= count; i++) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float k = 1f - i / (float)(count + 1);
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float rot = i < Projectile.oldRot.Length ? Projectile.oldRot[i] : Projectile.rotation;
                sb.Draw(tex, pos, frame, baseColor * (MathF.Pow(k, 1.6f) * 0.38f * strength), rot, origin
                    , Projectile.scale * MathHelper.Lerp(0.80f, 0.98f, k), SpriteEffects.None, 0);
            }
        }

        /// <summary>旋转拖影</summary>
        private void DrawSpinSmear(SpriteBatch sb, Texture2D tex, Rectangle frame, Vector2 origin, Color baseColor) {
            float spinT = MathHelper.Clamp(spinRate / 0.9f, 0f, 1f);
            if (spinT <= 0.12f) {
                return;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            for (int i = 1; i <= 4; i++) {
                float fade = (0.30f - i * 0.06f) * spinT;
                if (fade <= 0.01f) {
                    continue;
                }
                sb.Draw(tex, drawPos, frame, baseColor * fade, Projectile.rotation - spinRate * i * 1.6f
                    , origin, Projectile.scale * (1f - i * 0.015f), SpriteEffects.None, 0);
            }
        }
    }
}
