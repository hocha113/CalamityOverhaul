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
    /// 公主鱼技能，召唤公主鱼释放绚丽魔法攻击
    /// </summary>
    internal class FishPrincess : FishSkill
    {
        public override int UnlockFishID => ItemID.PrincessFish;
        public override int DefaultCooldown => 50 - HalibutData.GetDomainLayer() * 2;
        public override int ResearchDuration => 60 * 22;

        //活跃的公主鱼追踪
        private static readonly List<int> ActivePrincessFish = new();
        private static int MaxPrincessFish => 3 + HalibutData.GetDomainLayer() / 3;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();
                CleanupInactiveFish();

                if (ActivePrincessFish.Count < MaxPrincessFish) {
                    //在玩家周围随机位置生成公主鱼
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float distance = Main.rand.NextFloat(200f, 300f);
                    Vector2 spawnPos = player.Center + angle.ToRotationVector2() * distance;

                    //将鱼的索引通过ai2传递
                    int fishProj = Projectile.NewProjectile(
                        source,
                        spawnPos,
                        Vector2.Zero,
                        ModContent.ProjectileType<PrincessFishMinion>(),
                        (int)(damage * (0.2f + HalibutData.GetDomainLayer() * 0.05f)),
                        knockback * 1.5f,
                        player.whoAmI,
                        ai2: ActivePrincessFish.Count //通过ai2传递索引
                    );

                    if (fishProj >= 0 && fishProj < Main.maxProjectiles) {
                        ActivePrincessFish.Add(fishProj);
                        SpawnSummonEffect(spawnPos);

                        //公主鱼召唤音效 - 清脆魔法音
                        SoundEngine.PlaySound(SoundID.Item29 with {
                            Volume = 0.6f,
                            Pitch = 0.4f
                        }, spawnPos);

                        SoundEngine.PlaySound(SoundID.Item82 with {
                            Volume = 0.5f,
                            Pitch = 0.3f
                        }, spawnPos);
                    }
                }
            }

            return null;
        }

        private static void CleanupInactiveFish() {
            ActivePrincessFish.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<PrincessFishMinion>();
            });
        }

        private static void SpawnSummonEffect(Vector2 position) {
            //绚丽彩虹粒子爆发
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 10f);

                //彩虹色粒子
                Color rainbowColor = Main.hslToRgb((i / 30f + Main.GlobalTimeWrappedHourly * 0.3f) % 1f, 1f, 0.6f);

                Dust rainbow = Dust.NewDustPerfect(
                    position,
                    DustID.RainbowMk2,
                    velocity,
                    0,
                    rainbowColor,
                    Main.rand.NextFloat(1.8f, 2.8f)
                );
                rainbow.noGravity = true;
                rainbow.fadeIn = 1.5f;
            }

            //星星粒子
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust star = Dust.NewDustPerfect(
                    position,
                    DustID.PinkFairy,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                star.noGravity = true;
            }

            //魔法光环
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 velocity = angle.ToRotationVector2() * 6f;

                BasePRT sparkle = new PRT_Light(
                    position,
                    velocity,
                    0.8f,
                    Main.hslToRgb((i / 20f) % 1f, 1f, 0.7f),
                    30,
                    1f,
                    hueShift: 0.01f
                );
                PRTLoader.AddParticle(sparkle);
            }
        }
    }

    /// <summary>
    /// 公主鱼召唤物弹幕
    /// </summary>
    internal class PrincessFishMinion : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.PrincessFish;

        //状态
        private enum FishState
        {
            Spawning,    //生成阶段
            Following,   //跟随玩家
            Targeting,   //锁定目标
            Attacking    //攻击阶段
        }

        private FishState State {
            get => (FishState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float AttackCooldown => ref Projectile.ai[1];
        private ref float FishIndex => ref Projectile.ai[2];
        private ref float StateTimer => ref Projectile.localAI[0];

        private int targetNPCID = -1;
        private Vector2 idleOffset = Vector2.Zero;
        private float orbitAngle = 0f;
        private float floatPhase = 0f;

        //视觉效果
        private float glowIntensity = 0f;
        private float rainbowHue = 0f;
        private readonly List<Vector2> trailPositions = new();
        private const int MaxTrailLength = 15;

        //攻击参数
        private const float SearchRange = 1400f;
        private const int AttackInterval = 90;
        private const int SpawningDuration = 20;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = false;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            floatPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            orbitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<FishPrincess>().Active(owner)) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 60;
            StateTimer++;

            //状态机
            switch (State) {
                case FishState.Spawning:
                    SpawningAI();
                    break;
                case FishState.Following:
                    FollowingAI(owner);
                    break;
                case FishState.Targeting:
                    TargetingAI(owner);
                    break;
                case FishState.Attacking:
                    AttackingAI(owner);
                    break;
            }

            //更新拖尾
            UpdateTrail();

            //更新彩虹色相
            rainbowHue += 0.01f;
            if (rainbowHue > 1f) rainbowHue -= 1f;

            //公主鱼的彩虹光照
            Color lightColor = Main.hslToRgb(rainbowHue, 1f, 0.6f);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * 0.8f);

            //生成彩虹粒子
            if (Main.rand.NextBool(8)) {
                SpawnRainbowParticle();
            }

            //攻击冷却
            if (AttackCooldown > 0) AttackCooldown--;
        }

        private void SpawningAI() {
            float progress = StateTimer / SpawningDuration;

            //淡入效果
            Projectile.alpha = (int)((1f - progress) * 255f);
            Projectile.scale = progress;

            //向上浮现
            Projectile.velocity.Y = -2f * (1f - progress);
            Projectile.velocity.X *= 0.9f;

            glowIntensity = progress;

            //生成绚丽粒子
            if (Main.rand.NextBool(3)) {
                SpawnSpawnParticle();
            }

            if (StateTimer >= SpawningDuration) {
                State = FishState.Following;
                StateTimer = 0;
                Projectile.alpha = 0;
                Projectile.scale = 1f;
            }
        }

        private void FollowingAI(Player owner) {
            UpdateIdleOffset();

            //环绕玩家
            orbitAngle += 0.02f;
            Vector2 orbitPos = owner.Center +
                new Vector2(
                    (float)Math.Cos(orbitAngle + FishIndex) * 150f,
                    (float)Math.Sin(orbitAngle + FishIndex * 0.7f) * 100f
                ) + idleOffset;

            //平滑移动
            Vector2 toTarget = orbitPos - Projectile.Center;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toTarget * 0.08f, 0.2f);

            //旋转朝向移动方向
            if (Projectile.velocity.LengthSquared() > 0.5f) {
                Projectile.rotation = MathHelper.Lerp(
                    Projectile.rotation,
                    Projectile.velocity.ToRotation(),
                    0.15f
                );
            }

            glowIntensity = 0.6f + (float)Math.Sin(StateTimer * 0.1f) * 0.2f;

            //搜索敌人
            if (AttackCooldown <= 0) {
                NPC target = owner.Center.FindClosestNPC(SearchRange);
                if (target != null) {
                    targetNPCID = target.whoAmI;
                    State = FishState.Targeting;
                    StateTimer = 0;
                }
            }
        }

        private void TargetingAI(Player owner) {
            if (!IsTargetValid()) {
                State = FishState.Following;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[targetNPCID];

            //移动到目标上方
            Vector2 attackPos = target.Center + new Vector2(0, -200f);
            Vector2 toAttackPos = attackPos - Projectile.Center;

            Projectile.velocity = Vector2.Lerp(
                Projectile.velocity,
                toAttackPos.SafeNormalize(Vector2.Zero) * 14f,
                0.15f
            );

            //旋转朝向目标
            Projectile.rotation = MathHelper.Lerp(
                Projectile.rotation,
                (target.Center - Projectile.Center).ToRotation(),
                0.2f
            );

            glowIntensity = 0.8f + (float)Math.Sin(StateTimer * 0.3f) * 0.2f;

            //到达位置后开始攻击
            if (Vector2.Distance(Projectile.Center, attackPos) < 100f && StateTimer > 25) {
                State = FishState.Attacking;
                StateTimer = 0;
            }
        }

        private void AttackingAI(Player owner) {
            if (!IsTargetValid()) {
                State = FishState.Following;
                StateTimer = 0;
                AttackCooldown = AttackInterval;
                return;
            }

            NPC target = Main.npc[targetNPCID];

            //保持位置并攻击
            Projectile.velocity *= 0.9f;

            glowIntensity = 1f;

            //发射魔法弹幕
            if (StateTimer == 1 && Projectile.IsOwnedByLocalPlayer()) {
                LaunchMagicAttack(target);
            }

            //攻击持续时间
            if (StateTimer >= 45) {
                State = FishState.Following;
                StateTimer = 0;
                AttackCooldown = AttackInterval - HalibutData.GetDomainLayer() * 8;
            }
        }

        private void LaunchMagicAttack(NPC target) {
            //发射多个彩虹魔法球
            int projectileCount = 3 + HalibutData.GetDomainLayer() / 4;

            for (int i = 0; i < projectileCount; i++) {
                //计算预判位置
                Vector2 targetPos = target.Center + target.velocity * 20f;
                Vector2 toTarget = targetPos - Projectile.Center;

                //添加扇形散射
                float spreadAngle = MathHelper.Lerp(-0.3f, 0.3f, i / (float)(projectileCount - 1));
                Vector2 velocity = toTarget.SafeNormalize(Vector2.Zero).RotatedBy(spreadAngle) * 18f;

                //生成魔法球
                int proj = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    ModContent.ProjectileType<PrincessMagicOrb>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    ai0: i / (float)projectileCount //传递颜色偏移
                );

                if (proj >= 0) {
                    Main.projectile[proj].netUpdate = true;
                }
            }

            //攻击特效
            SpawnAttackEffect();

            //攻击音效 - 魔法铃铛音
            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.7f,
                Pitch = 0.3f
            }, Projectile.Center);

            SoundEngine.PlaySound(SoundID.Item43 with {
                Volume = 0.6f,
                Pitch = 0.5f
            }, Projectile.Center);
        }

        private void UpdateIdleOffset() {
            idleOffset.X = (float)Math.Sin(floatPhase * 0.8f) * 30f;
            idleOffset.Y = (float)Math.Cos(floatPhase * 0.6f) * 20f;
            floatPhase += 0.05f;
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
            return target.active && target.CanBeChasedBy();
        }

        //粒子效果
        private void SpawnRainbowParticle() {
            Color rainbowColor = Main.hslToRgb(rainbowHue, 1f, 0.6f);

            Dust rainbow = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                DustID.RainbowMk2,
                Projectile.velocity * -0.3f + Main.rand.NextVector2Circular(1f, 1f),
                0,
                rainbowColor,
                Main.rand.NextFloat(0.8f, 1.3f)
            );
            rainbow.noGravity = true;
            rainbow.fadeIn = 1f;
        }

        private void SpawnSpawnParticle() {
            Color spawnColor = Main.hslToRgb(Main.rand.NextFloat(1f), 1f, 0.7f);

            BasePRT sparkle = new PRT_Light(
                Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                Main.rand.NextVector2Circular(3f, 3f),
                Main.rand.NextFloat(0.6f, 1f),
                spawnColor,
                25,
                1f,
                hueShift: 0.02f
            );
            PRTLoader.AddParticle(sparkle);
        }

        private void SpawnAttackEffect() {
            //攻击爆发环
            for (int i = 0; i < 25; i++) {
                float angle = MathHelper.TwoPi * i / 25f;
                Vector2 velocity = angle.ToRotationVector2() * 8f;
                Color attackColor = Main.hslToRgb((i / 25f + rainbowHue) % 1f, 1f, 0.6f);

                BasePRT burst = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    0.9f,
                    attackColor,
                    35,
                    1f,
                    hueShift: 0.015f
                );
                PRTLoader.AddParticle(burst);
            }

            //星星爆发
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(10f, 10f);
                Dust star = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.PinkFairy,
                    velocity,
                    0,
                    default,
                    Main.rand.NextFloat(2f, 3f)
                );
                star.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //消散效果
            for (int i = 0; i < 30; i++) {
                Color fadeColor = Main.hslToRgb((i / 30f + rainbowHue) % 1f, 1f, 0.6f);

                Dust fade = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.RainbowMk2,
                    Main.rand.NextVector2Circular(8f, 8f),
                    0,
                    fadeColor,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                fade.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = TextureAssets.Item[ItemID.PrincessFish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = fishTex.Size() / 2f;

            float alpha = (255f - Projectile.alpha) / 255f;

            //绘制彩虹拖尾
            DrawRainbowTrail(sb, fishTex, origin, alpha);

            //绘制彩虹辉光层
            for (int i = 0; i < 3; i++) {
                float glowScale = Projectile.scale * (1.2f + i * 0.15f);
                float glowAlpha = glowIntensity * (1f - i * 0.3f) * 0.5f * alpha;
                Color glowColor = Main.hslToRgb((rainbowHue + i * 0.1f) % 1f, 1f, 0.6f) with { A = 0 };

                sb.Draw(
                    fishTex,
                    drawPos,
                    null,
                    glowColor * glowAlpha,
                    Projectile.rotation + MathHelper.PiOver4,
                    origin,
                    glowScale,
                    SpriteEffects.None,
                    0
                );
            }

            //主体绘制 - 应用彩虹色调
            Color mainColor = Color.Lerp(
                lightColor,
                Main.hslToRgb(rainbowHue, 0.8f, 0.7f),
                glowIntensity * 0.6f
            );

            sb.Draw(
                fishTex,
                drawPos,
                null,
                mainColor * alpha,
                Projectile.rotation + MathHelper.PiOver4,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //额外白色高光
            float highlightAlpha = glowIntensity * 0.4f * alpha;
            sb.Draw(
                fishTex,
                drawPos,
                null,
                Color.White * highlightAlpha,
                Projectile.rotation + MathHelper.PiOver4,
                origin,
                Projectile.scale * 0.95f,
                SpriteEffects.None,
                0
            );

            return false;
        }

        private void DrawRainbowTrail(SpriteBatch sb, Texture2D texture, Vector2 origin, float alpha) {
            for (int i = 1; i < trailPositions.Count; i++) {
                if (i >= trailPositions.Count) continue;

                float progress = 1f - i / (float)trailPositions.Count;
                float trailAlpha = progress * alpha * 0.6f;
                float trailScale = Projectile.scale * MathHelper.Lerp(0.7f, 1f, progress);

                Color trailColor = Main.hslToRgb(
                    (rainbowHue + i * 0.05f) % 1f,
                    1f,
                    0.5f
                ) * trailAlpha;

                Vector2 trailPos = trailPositions[i] - Main.screenPosition;

                sb.Draw(
                    texture,
                    trailPos,
                    null,
                    trailColor,
                    Projectile.rotation - i * 0.05f,
                    origin,
                    trailScale,
                    SpriteEffects.None,
                    0
                );
            }
        }
    }

    /// <summary>
    /// 公主鱼的魔法球弹幕
    /// </summary>
    internal class PrincessMagicOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float ColorOffset => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[1];

        private float orbRotation = 0f;
        private float pulsePhase = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Timer++;
            pulsePhase += 0.15f;
            orbRotation += 0.2f;

            //轻微追踪
            if (Timer > 15) {
                NPC target = Projectile.Center.FindClosestNPC(400f);
                if (target != null) {
                    Vector2 toTarget = target.Center - Projectile.Center;
                    Projectile.velocity += toTarget.SafeNormalize(Vector2.Zero) * 0.25f;

                    if (Projectile.velocity.Length() > 20f) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 20f;
                    }
                }
            }

            //旋转
            Projectile.rotation = Projectile.velocity.ToRotation();

            //彩虹光照
            float hue = (ColorOffset + Main.GlobalTimeWrappedHourly * 0.5f) % 1f;
            Color lightColor = Main.hslToRgb(hue, 1f, 0.6f);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * 1.2f);

            //彩虹粒子轨迹
            if (Main.rand.NextBool(3)) {
                SpawnTrailParticle(hue);
            }
        }

        private void SpawnTrailParticle(float hue) {
            Color particleColor = Main.hslToRgb(hue, 1f, 0.6f);

            BasePRT trail = new PRT_Spark(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1f, 1f),
                false,
                15,
                Main.rand.NextFloat(0.8f, 1.2f),
                particleColor
            );
            PRTLoader.AddParticle(trail);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中魔法爆发
            float hue = (ColorOffset + Main.GlobalTimeWrappedHourly * 0.5f) % 1f;

            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * 6f;
                Color hitColor = Main.hslToRgb((hue + i * 0.05f) % 1f, 1f, 0.6f);

                BasePRT hit_effect = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    0.7f,
                    hitColor,
                    25,
                    1f,
                    hueShift: 0.02f
                );
                PRTLoader.AddParticle(hit_effect);
            }

            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.5f,
                Pitch = 0.4f
            }, Projectile.Center);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //反弹
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.7f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
            }

            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                Projectile.Kill();
            }

            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.4f,
                Pitch = 0.2f
            }, Projectile.Center);

            return false;
        }

        public override void OnKill(int timeLeft) {
            float hue = (ColorOffset + Main.GlobalTimeWrappedHourly * 0.5f) % 1f;

            //彩虹爆发
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Color burstColor = Main.hslToRgb((hue + i * 0.03f) % 1f, 1f, 0.6f);

                BasePRT burst = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    Main.rand.NextFloat(0.6f, 1f),
                    burstColor,
                    30,
                    1f,
                    hueShift: 0.02f
                );
                PRTLoader.AddParticle(burst);
            }

            //星星粒子
            for (int i = 0; i < 12; i++) {
                Dust star = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.PinkFairy,
                    Main.rand.NextVector2Circular(7f, 7f),
                    0,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                star.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.6f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;

            float hue = (ColorOffset + Main.GlobalTimeWrappedHourly * 0.5f) % 1f;
            Color orbColor = Main.hslToRgb(hue, 1f, 0.7f);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 1f + (float)Math.Sin(pulsePhase) * 0.15f;

            //绘制拖尾
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / (float)Projectile.oldPos.Length;
                float trailAlpha = progress * 0.6f;
                float trailScale = progress * pulse * 0.8f;

                Color trailColor = Main.hslToRgb((hue + i * 0.02f) % 1f, 1f, 0.6f) with { A = 0 };

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                sb.Draw(
                    glowTex,
                    trailPos,
                    null,
                    trailColor * trailAlpha,
                    orbRotation - i * 0.1f,
                    glowTex.Size() / 2f,
                    trailScale * 0.4f,
                    SpriteEffects.None,
                    0
                );
            }

            //外层光晕
            for (int i = 0; i < 3; i++) {
                float glowScale = pulse * (1.3f + i * 0.2f);
                float glowAlpha = (1f - i * 0.3f) * 0.5f;

                Color glowColor = Main.hslToRgb((hue + i * 0.05f) % 1f, 1f, 0.6f) with { A = 0 };

                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    glowColor * glowAlpha,
                    orbRotation,
                    glowTex.Size() / 2f,
                    glowScale * 0.5f,
                    SpriteEffects.None,
                    0
                );
            }

            //主体魔法球
            sb.Draw(
                glowTex,
                drawPos,
                null,
                orbColor with { A = 0 },
                orbRotation,
                glowTex.Size() / 2f,
                pulse * 0.35f,
                SpriteEffects.None,
                0
            );

            //白色核心
            sb.Draw(
                glowTex,
                drawPos,
                null,
                Color.White with { A = 0 } * 0.8f,
                orbRotation,
                glowTex.Size() / 2f,
                pulse * 0.2f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}
