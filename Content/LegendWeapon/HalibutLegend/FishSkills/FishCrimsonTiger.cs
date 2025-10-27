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
    /// <summary>
    /// 猩红虎鱼技能，右键召唤凶猛虎鱼群撕咬敌人
    /// </summary>
    internal class FishCrimsonTiger : FishSkill
    {
        public override int UnlockFishID => ItemID.CrimsonTigerfish;
        public override int DefaultCooldown => 60; //无冷却，但需要右键
        public override int ResearchDuration => 60 * 18;

        //活跃的虎鱼追踪
        private static readonly List<int> ActiveTigerFish = new();
        private static int MaxTigerFish => 8 + HalibutData.GetDomainLayer();

        public override bool? AltFunctionUse(Item item, Player player) {
            return Cooldown == 0;
        }

        public override bool? ShootAlt(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            //右键召唤虎鱼群
            if (player.altFunctionUse == 2) {
                CleanupInactiveFish();
                SetCooldown();

                if (ActiveTigerFish.Count < MaxTigerFish) {
                    //在鼠标方向生成一群虎鱼
                    Vector2 mouseDir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero);
                    int spawnCount = Math.Min(3 + HalibutData.GetDomainLayer() / 3, MaxTigerFish - ActiveTigerFish.Count);

                    for (int i = 0; i < spawnCount; i++) {
                        float angleOffset = MathHelper.Lerp(-0.4f, 0.4f, i / (float)(spawnCount - 1));
                        Vector2 spawnDir = mouseDir.RotatedBy(angleOffset);
                        Vector2 spawnPos = player.Center + spawnDir * Main.rand.NextFloat(60f, 120f);

                        int tigerProj = Projectile.NewProjectile(
                            source,
                            spawnPos,
                            spawnDir * Main.rand.NextFloat(12f, 18f),
                            ModContent.ProjectileType<CrimsonTigerFishMinion>(),
                            (int)(damage * (1.8f + HalibutData.GetDomainLayer() * 0.4f)),
                            knockback * 2f,
                            player.whoAmI,
                            ai2: ActiveTigerFish.Count
                        );

                        if (tigerProj >= 0 && tigerProj < Main.maxProjectiles) {
                            ActiveTigerFish.Add(tigerProj);
                        }
                    }

                    SpawnSummonEffect(position, mouseDir);

                    //虎鱼召唤音效 - 凶猛嘶吼
                    SoundEngine.PlaySound(SoundID.NPCHit1 with {
                        Volume = 0.8f,
                        Pitch = -0.5f
                    }, position);

                    SoundEngine.PlaySound(SoundID.NPCHit9 with {
                        Volume = 0.7f,
                        Pitch = -0.6f
                    }, position);
                }

                return false; //阻止默认射击
            }

            return null;
        }

        private static void CleanupInactiveFish() {
            ActiveTigerFish.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<CrimsonTigerFishMinion>();
            });
        }

        private static void SpawnSummonEffect(Vector2 position, Vector2 direction) {
            //血色爆发粒子
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = direction.RotatedByRandom(0.6f) * Main.rand.NextFloat(6f, 14f);

                Dust blood = Dust.NewDustPerfect(
                    position,
                    DustID.Blood,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.8f, 2.8f)
                );
                blood.noGravity = true;
                blood.fadeIn = 1.5f;
            }

            //猩红粒子环
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * 8f;

                BasePRT crimson = new PRT_Light(
                    position,
                    velocity,
                    0.9f,
                    Color.Crimson,
                    30,
                    1f,
                    hueShift: 0.01f
                );
                PRTLoader.AddParticle(crimson);
            }
        }
    }

    /// <summary>
    /// 猩红虎鱼召唤物
    /// </summary>
    internal class CrimsonTigerFishMinion : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.CrimsonTigerfish;

        //状态
        private enum TigerState
        {
            Spawning,    //生成
            Hunting,     //狩猎
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
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 12;

        //狩猎参数
        private const float SearchRange = 1200f;
        private const float MaxSpeed = 24f;
        private const float Acceleration = 0.8f;
        private const int BiteDuration = 45;
        private const int SpawningDuration = 15;

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
            StateTimer++;
            AttackTimer++;

            //状态机
            switch (State) {
                case TigerState.Spawning:
                    SpawningAI(owner);
                    break;
                case TigerState.Hunting:
                    HuntingAI(owner);
                    break;
                case TigerState.Biting:
                    BitingAI();
                    break;
                case TigerState.Returning:
                    ReturningAI(owner);
                    break;
            }

            //更新拖尾
            UpdateTrail();

            //更新游动
            swimPhase += 0.3f;

            //血色光照
            Lighting.AddLight(Projectile.Center, 0.8f, 0.2f, 0.2f);

            //嗜血状态衰减
            if (bloodLust > 0f) bloodLust *= 0.95f;

            //生成血迹粒子
            if (Main.rand.NextBool(6)) {
                SpawnBloodParticle();
            }
        }

        private void SpawningAI(Player owner) {
            float progress = StateTimer / SpawningDuration;

            //淡入
            Projectile.alpha = (int)((1f - progress) * 255f);
            Projectile.scale = progress;

            //向前冲刺
            Projectile.velocity *= 1.05f;

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
                ChaseTarget(target.Center);

                //接近目标时进入撕咬状态
                if (Vector2.Distance(Projectile.Center, target.Center) < 60f) {
                    State = TigerState.Biting;
                    StateTimer = 0;
                    bloodLust = 1f;

                    //咬击音效
                    SoundEngine.PlaySound(SoundID.NPCHit9 with {
                        Volume = 0.6f,
                        Pitch = 0.2f
                    }, Projectile.Center);
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
                    //环绕玩家
                    Vector2 orbitPos = owner.Center + 
                        (swimPhase + FishIndex).ToRotationVector2() * 200f;
                    ChaseTarget(orbitPos);
                }
            }

            //旋转朝向速度
            if (Projectile.velocity.LengthSquared() > 1f) {
                Projectile.rotation = MathHelper.Lerp(
                    Projectile.rotation,
                    Projectile.velocity.ToRotation(),
                    0.2f
                );
            }
        }

        private void BitingAI() {
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

            //旋转撕咬动画
            Projectile.rotation += 0.4f;

            //减速
            Projectile.velocity *= 0.8f;

            //持续造成伤害
            if (StateTimer % 8 == 0) {
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

            //返回后切换回狩猎状态
            if (distance < 100f) {
                State = TigerState.Hunting;
                StateTimer = 0;
            }

            //旋转
            if (Projectile.velocity.LengthSquared() > 1f) {
                Projectile.rotation = MathHelper.Lerp(
                    Projectile.rotation,
                    Projectile.velocity.ToRotation(),
                    0.2f
                );
            }
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

        private void UpdateTrail() {
            trailPositions.Insert(0, Projectile.Center);
            if (trailPositions.Count > MaxTrailLength) {
                trailPositions.RemoveAt(trailPositions.Count - 1);
            }
        }

        private bool IsTargetValid() {
            if (targetNPCID < 0 || targetNPCID >= Main.maxNPCs) return false;
            NPC target = Main.npc[targetNPCID];
            return target.active && target.CanBeChasedBy() && !target.friendly;
        }

        private void SpawnBloodParticle() {
            Dust blood = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                DustID.Blood,
                Projectile.velocity * -0.3f + Main.rand.NextVector2Circular(2f, 2f),
                0,
                default,
                Main.rand.NextFloat(0.8f, 1.4f)
            );
            blood.noGravity = true;
            blood.fadeIn = 0.8f;
        }

        private void SpawnBiteEffect(NPC target) {
            //咬击血花
            for (int i = 0; i < 5; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust blood = Dust.NewDustPerfect(
                    target.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Blood,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                blood.noGravity = true;
            }

            //猩红能量爆发
            BasePRT crimson = new PRT_Spark(
                target.Center,
                Main.rand.NextVector2Circular(4f, 4f),
                false,
                15,
                Main.rand.NextFloat(1f, 1.5f),
                Color.Crimson
            );
            PRTLoader.AddParticle(crimson);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //造成流血debuff
            target.AddBuff(BuffID.Bleeding, 180 + HalibutData.GetDomainLayer() * 15);

            //击中特效
            for (int i = 0; i < 8; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust blood = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Blood,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.8f, 2.5f)
                );
                blood.noGravity = true;
            }

            //增强嗜血状态
            bloodLust = Math.Min(bloodLust + 0.3f, 1.5f);

            SoundEngine.PlaySound(SoundID.NPCHit9 with {
                Volume = 0.5f,
                Pitch = 0.1f
            }, Projectile.Center);
        }

        public override void OnKill(int timeLeft) {
            //消散血雾
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust blood = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Blood,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                blood.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.4f,
                Pitch = -0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = TextureAssets.Item[ItemID.CrimsonTigerfish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;
            SpriteEffects spriteEffects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = Projectile.rotation + (Projectile.velocity.X > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);

            float alpha = (255f - Projectile.alpha) / 255f;

            //绘制血色拖尾
            DrawBloodTrail(sb, fishTex, origin, alpha);

            //嗜血发光
            if (bloodLust > 0.3f) {
                for (int i = 0; i < 3; i++) {
                    float glowScale = Projectile.scale * (1.2f + i * 0.15f);
                    float glowAlpha = bloodLust * (1f - i * 0.3f) * 0.6f * alpha;

                    sb.Draw(
                        fishTex,
                        drawPos,
                        null,
                        Color.Red with { A = 0 } * glowAlpha,
                        drawRot,
                        origin,
                        glowScale,
                        spriteEffects,
                        0
                    );
                }
            }

            //主体绘制 - 猩红色调
            Color mainColor = Color.Lerp(
                lightColor,
                Color.Crimson,
                bloodLust * 0.7f
            );

            sb.Draw(
                fishTex,
                drawPos,
                null,
                mainColor * alpha,
                drawRot,
                origin,
                Projectile.scale,
                spriteEffects,
                0
            );

            //眼睛发光（嗜血时）
            if (bloodLust > 0.5f) {
                sb.Draw(
                    fishTex,
                    drawPos,
                    null,
                    Color.OrangeRed * bloodLust * alpha,
                    drawRot,
                    origin,
                    Projectile.scale * 0.9f,
                    spriteEffects,
                    0
                );
            }

            return false;
        }

        private void DrawBloodTrail(SpriteBatch sb, Texture2D texture, Vector2 origin, float alpha) {
            SpriteEffects spriteEffects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float drawRot = Projectile.rotation + (Projectile.velocity.X > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            for (int i = 1; i < trailPositions.Count; i++) {
                if (i >= trailPositions.Count) continue;

                float progress = 1f - i / (float)trailPositions.Count;
                float trailAlpha = progress * alpha * 0.7f;
                float trailScale = Projectile.scale * MathHelper.Lerp(0.6f, 1f, progress);

                Color trailColor = Color.Lerp(
                    Color.DarkRed,
                    Color.Crimson,
                    progress
                ) * trailAlpha;

                Vector2 trailPos = trailPositions[i] - Main.screenPosition;

                sb.Draw(
                    texture,
                    trailPos,
                    null,
                    trailColor,
                    drawRot - i * 0.08f,
                    origin,
                    trailScale,
                    spriteEffects,
                    0
                );
            }
        }
    }
}
