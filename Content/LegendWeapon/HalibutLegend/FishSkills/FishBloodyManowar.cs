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
    internal class FishBloodyManowar : FishSkill
    {
        public override int UnlockFishID => ItemID.BloodyManowar;
        public override int DefaultCooldown => 300 - HalibutData.GetDomainLayer() * 24;
        public override int ResearchDuration => 60 * 18;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (Cooldown <= 0) {
                Use(item, player);
            }
            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        public override void Use(Item item, Player player) {
            SetCooldown();

            Vector2 targetPos = Main.MouseWorld;
            ShootState shootState = player.GetShootState();

            //生成水母群控制器
            Projectile.NewProjectile(
                player.GetSource_ItemUse(item),
                targetPos,
                Vector2.Zero,
                ModContent.ProjectileType<BloodySwarmController>(),
                (int)(shootState.WeaponDamage * (2f + HalibutData.GetDomainLayer() * 0.5f)),
                shootState.WeaponKnockback * 2.5f,
                player.whoAmI
            );

            //召唤音效
            SoundEngine.PlaySound(SoundID.NPCDeath19 with { Volume = 0.7f, Pitch = -0.3f }, targetPos);
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = -0.2f }, targetPos);
        }
    }

    /// <summary>
    /// 血腥水母群控制器
    /// </summary>
    internal class BloodySwarmController : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        public enum SwarmPhase
        {
            Spawning,//生成扩散
            Hovering,//悬浮等待
            Converging,//聚拢冲击
            Exploding//爆炸消散
        }

        public SwarmPhase Phase {
            get => (SwarmPhase)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        public ref float PhaseTimer => ref Projectile.ai[1];
        public ref float ConvergenceProgress => ref Projectile.ai[2];
        public Player Owner => Main.player[Projectile.owner];

        public List<int> jellyfishList = new List<int>();
        public Vector2 centerPoint;//聚集中心点
        public bool hasCausedDamage = false;

        private const int SpawnDuration = 25;//生成扩散阶段
        private const int HoverDuration = 35;//悬浮等待阶段
        private const int ConvergeDuration = 20;//聚拢冲击阶段
        private const int ExplodeDuration = 30;//爆炸消散阶段

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 400;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = SpawnDuration + HoverDuration + ConvergeDuration + ExplodeDuration + 10;
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            PhaseTimer++;

            //初始化中心点
            if (PhaseTimer == 1) {
                centerPoint = Projectile.Center;
            }

            switch (Phase) {
                case SwarmPhase.Spawning:
                    SpawningPhaseAI();
                    break;
                case SwarmPhase.Hovering:
                    HoveringPhaseAI();
                    break;
                case SwarmPhase.Converging:
                    ConvergingPhaseAI();
                    break;
                case SwarmPhase.Exploding:
                    ExplodingPhaseAI();
                    break;
            }

            Projectile.Center = centerPoint;
        }

        private void SpawningPhaseAI() {
            if (PhaseTimer == 1) {
                int layer = HalibutData.GetDomainLayer(Owner);
                int jellyfishCount = 25 + layer * 6;//水母数量随层数增长

                //环形扩散生成水母
                for (int i = 0; i < jellyfishCount; i++) {
                    float angle = MathHelper.TwoPi * i / jellyfishCount;
                    float distance = Main.rand.NextFloat(180f, 280f);
                    Vector2 offset = angle.ToRotationVector2() * distance;

                    int proj = Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        centerPoint,
                        Vector2.Zero,
                        ModContent.ProjectileType<BloodyJellyfishUnit>(),
                        Projectile.damage,
                        Projectile.knockBack,
                        Projectile.owner,
                        Projectile.identity,
                        i
                    );

                    if (proj >= 0) {
                        jellyfishList.Add(proj);
                        if (Main.projectile[proj].ModProjectile is BloodyJellyfishUnit unit) {
                            unit.targetOffset = offset;
                            unit.hoverHeight = Main.rand.NextFloat(-40f, 40f);
                        }
                    }
                }

                //生成血雾扩散特效
                SpawnBloodMist(centerPoint, 40);
            }

            //扩散粒子效果
            if (PhaseTimer % 2 == 0) {
                float progress = PhaseTimer / SpawnDuration;
                float radius = MathHelper.Lerp(0f, 300f, progress);
                for (int i = 0; i < 3; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = centerPoint + angle.ToRotationVector2() * radius;
                    Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                        Vector2.Zero, 100, Color.DarkRed, Main.rand.NextFloat(1.2f, 1.8f));
                    d.noGravity = true;
                }
            }

            if (PhaseTimer >= SpawnDuration) {
                Phase = SwarmPhase.Hovering;
                PhaseTimer = 0;
                SoundEngine.PlaySound(SoundID.NPCHit19 with { Volume = 0.5f, Pitch = 0.2f }, centerPoint);
            }
        }

        private void HoveringPhaseAI() {
            //环境血雾
            if (PhaseTimer % 5 == 0) {
                Vector2 pos = centerPoint + Main.rand.NextVector2Circular(250f, 250f);
                Dust mist = Dust.NewDustPerfect(pos, DustID.Blood,
                    Main.rand.NextVector2Circular(1f, 1f), 120,
                    new Color(180, 0, 0, 100), Main.rand.NextFloat(1.5f, 2.2f));
                mist.noGravity = true;
            }

            //脉动光效
            if (PhaseTimer % 20 == 0) {
                Lighting.AddLight(centerPoint, 0.8f, 0.1f, 0.1f);
            }

            if (PhaseTimer >= HoverDuration) {
                Phase = SwarmPhase.Converging;
                PhaseTimer = 0;
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.7f, Pitch = -0.4f }, centerPoint);
            }
        }

        private void ConvergingPhaseAI() {
            ConvergenceProgress = PhaseTimer / ConvergeDuration;

            //聚拢过程粒子轨迹
            if (PhaseTimer % 1 == 0) {
                for (int i = 0; i < 5; i++) {
                    float distance = MathHelper.Lerp(300f, 50f, ConvergenceProgress);
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = centerPoint + angle.ToRotationVector2() * distance;
                    Vector2 vel = (centerPoint - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(8f, 15f);

                    Dust d = Dust.NewDustPerfect(pos, DustID.Blood, vel,
                        100, Color.Red, Main.rand.NextFloat(1.5f, 2.5f));
                    d.noGravity = true;
                }
            }

            //聚拢冲击音效
            if (PhaseTimer == 5) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.8f, Pitch = -0.5f }, centerPoint);
            }

            //产生冲击波伤害（聚拢完成瞬间）
            if (PhaseTimer == ConvergeDuration - 3 && !hasCausedDamage) {
                CreateImpactWave();
                hasCausedDamage = true;
            }

            if (PhaseTimer >= ConvergeDuration) {
                Phase = SwarmPhase.Exploding;
                PhaseTimer = 0;
            }
        }

        private void ExplodingPhaseAI() {
            //触发所有水母消散
            if (PhaseTimer == 1) {
                foreach (int projIndex in jellyfishList) {
                    if (Main.projectile.IndexInRange(projIndex) && Main.projectile[projIndex].active) {
                        Main.projectile[projIndex].ai[2] = 1f;//消散标记
                    }
                }

                //爆炸特效
                SpawnExplosionEffect(centerPoint);
            }

            if (PhaseTimer >= ExplodeDuration) {
                Projectile.Kill();
            }
        }

        private void CreateImpactWave() {
            //生成多层冲击波
            int waveCount = 1 + HalibutData.GetDomainLayer(Owner) / 3;
            for (int i = 0; i < waveCount; i++) {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    centerPoint,
                    Vector2.Zero,
                    ModContent.ProjectileType<BloodyStrikeWave>(),
                    Projectile.damage * 2,
                    Projectile.knockBack * 2f,
                    Projectile.owner,
                    ai0: i * 0.15f//延迟错开
                );
            }

            //冲击音效叠加
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.4f }, centerPoint);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.7f, Pitch = -0.5f }, centerPoint);

            //冲击环形血雾爆发
            for (int ring = 0; ring < 3; ring++) {
                int count = 20 + ring * 10;
                float radius = 80f + ring * 60f;

                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(12f, 20f);
                    Dust impact = Dust.NewDustPerfect(centerPoint, DustID.Blood, vel,
                        100, Color.DarkRed, Main.rand.NextFloat(2f, 3f));
                    impact.noGravity = ring > 0;
                }
            }

            //中心血液爆炸
            for (int i = 0; i < 40; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(15f, 15f);
                Dust blood = Dust.NewDustPerfect(centerPoint, DustID.Blood, vel,
                    100, Color.Red, Main.rand.NextFloat(2f, 3.5f));
                blood.noGravity = Main.rand.NextBool();
            }
        }

        private static void SpawnBloodMist(Vector2 center, int count) {
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust mist = Dust.NewDustPerfect(center, DustID.Blood, vel,
                    100, new Color(160, 0, 0, 150), Main.rand.NextFloat(1.5f, 2.5f));
                mist.noGravity = true;
            }
        }

        private static void SpawnExplosionEffect(Vector2 center) {
            //爆炸血雾扩散
            for (int i = 0; i < 60; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(18f, 18f);
                Dust explosion = Dust.NewDustPerfect(center, DustID.Blood, vel,
                    100, Color.DarkRed, Main.rand.NextFloat(2f, 3.5f));
                explosion.noGravity = Main.rand.NextBool();
            }

            //血液飞溅
            for (int i = 0; i < 30; i++) {
                Dust splash = Dust.NewDustDirect(center - new Vector2(40), 80, 80,
                    DustID.Blood, Scale: Main.rand.NextFloat(2f, 3f));
                splash.velocity = Main.rand.NextVector2Circular(12f, 12f);
            }

            SoundEngine.PlaySound(SoundID.NPCDeath19 with { Volume = 0.6f, Pitch = -0.2f }, center);
        }

        public override void OnKill(int timeLeft) {
            //清理所有水母
            foreach (int projIndex in jellyfishList) {
                if (Main.projectile.IndexInRange(projIndex) && Main.projectile[projIndex].active) {
                    Main.projectile[projIndex].Kill();
                }
            }
        }
    }

    /// <summary>
    /// 血腥水母单元
    /// </summary>
    internal class BloodyJellyfishUnit : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.BloodyManowar;

        private ref float ControllerID => ref Projectile.ai[0];
        private ref float UnitIndex => ref Projectile.ai[1];
        private ref float IsDissipating => ref Projectile.ai[2];

        public Vector2 targetOffset;//目标偏移位置
        public float hoverHeight;//悬浮高度偏移
        private float rotation;
        private float pulsePhase;
        private float hoverPhase;
        private float dissipateAlpha = 1f;
        private Vector2 currentPos;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 200;
        }

        public override void AI() {
            if (!ControllerID.TryGetProjectile(out var controller)) {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 200;
            pulsePhase += 0.12f;
            hoverPhase += 0.08f;

            if (controller.ModProjectile is BloodySwarmController swarmCtrl) {
                Vector2 centerPoint = swarmCtrl.centerPoint;

                if (IsDissipating == 0) {
                    float phase = (float)swarmCtrl.Phase;

                    if (phase == 0) {//Spawning扩散阶段
                        float spawnProgress = swarmCtrl.PhaseTimer / 25f;
                        currentPos = Vector2.Lerp(centerPoint, centerPoint + targetOffset,
                            CWRUtils.EaseOutCubic(spawnProgress));
                    }
                    else if (phase == 1) {//Hovering悬浮阶段
                        Vector2 basePos = centerPoint + targetOffset;
                        float hoverOffset = MathF.Sin(hoverPhase + UnitIndex * 0.3f) * hoverHeight;
                        currentPos = basePos + new Vector2(0, hoverOffset);
                    }
                    else if (phase == 2) {//Converging聚拢阶段
                        float convergeProgress = swarmCtrl.ConvergenceProgress;
                        Vector2 startPos = centerPoint + targetOffset;
                        currentPos = Vector2.Lerp(startPos, centerPoint,
                            CWRUtils.EaseInCubic(convergeProgress));
                    }
                    else {//Exploding爆炸阶段
                        currentPos = centerPoint;
                    }

                    Projectile.Center = currentPos;

                    //朝向中心点
                    Vector2 toCenter = centerPoint - Projectile.Center;
                    if (toCenter.LengthSquared() > 1f) {
                        Projectile.rotation = toCenter.ToRotation() + MathHelper.PiOver2;
                    }
                }
                else {
                    //消散状态向外飞散
                    dissipateAlpha -= 0.04f;
                    Projectile.velocity = Main.rand.NextVector2Circular(8f, 8f);
                    Projectile.rotation += 0.2f;

                    if (dissipateAlpha <= 0) {
                        Projectile.Kill();
                    }
                }
            }

            //旋转动画
            rotation += 0.08f * (UnitIndex % 2 == 0 ? 1 : -1);

            //脉动缩放
            float pulse = MathF.Sin(pulsePhase + UnitIndex * 0.3f);
            Projectile.scale = 0.8f + pulse * 0.15f;

            //血雾粒子
            if (Main.rand.NextBool(12) && IsDissipating == 0) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Blood,
                    Main.rand.NextVector2Circular(0.5f, 0.5f),
                    120,
                    new Color(180, 0, 0, 100),
                    Main.rand.NextFloat(0.6f, 1f)
                );
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            //死亡血雾
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    Main.rand.NextVector2Circular(4f, 4f), 100,
                    Color.DarkRed, Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Item[ItemID.BloodyManowar].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;

            Color drawColor = lightColor * dissipateAlpha;

            //多层阴影增强深度感
            for (int i = 0; i < 3; i++) {
                Vector2 shadowOffset = new Vector2(i * 2f, i * 2f);
                Main.EntitySpriteDraw(texture, drawPos + shadowOffset, null,
                    Color.Black * 0.2f * dissipateAlpha,
                    Projectile.rotation + rotation, origin,
                    Projectile.scale * 0.95f, SpriteEffects.None, 0);
            }

            //主体
            Main.EntitySpriteDraw(texture, drawPos, null, drawColor,
                Projectile.rotation + rotation, origin,
                Projectile.scale, SpriteEffects.None, 0);

            //血红发光
            if (IsDissipating == 0) {
                Color glowColor = new Color(255, 0, 0, 0) * 0.4f * dissipateAlpha;
                Main.EntitySpriteDraw(texture, drawPos, null, glowColor,
                    Projectile.rotation + rotation, origin,
                    Projectile.scale * 1.15f, SpriteEffects.None, 0);
            }

            return false;
        }
    }

    /// <summary>
    /// 血腥冲击波
    /// </summary>
    internal class BloodyStrikeWave : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private ref float DelayOffset => ref Projectile.ai[0];
        private float delayTimer = 0f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 360;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 45;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.75f;
            }
            if (target.type == CWRLoad.DevourerofGodsHead || target.type == CWRLoad.DevourerofGodsTail) {
                modifiers.FinalDamage *= 1.33f;
            }
        }

        public override void AI() {
            //延迟启动
            if (delayTimer < DelayOffset * 60f) {
                delayTimer++;
                Projectile.scale = 0.1f;
                Projectile.alpha = 255;
                return;
            }

            Projectile.scale += 0.2f;
            Projectile.alpha += 7;
            Projectile.rotation += 0.06f;

            //血雾扩散
            if (Main.rand.NextBool(2)) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(
                    Projectile.width / 2 * Projectile.scale,
                    Projectile.height / 2 * Projectile.scale);
                Dust mist = Dust.NewDustPerfect(pos, DustID.Blood,
                    Main.rand.NextVector2Circular(4f, 4f), 100,
                    Color.DarkRed, Main.rand.NextFloat(1.8f, 2.8f));
                mist.noGravity = true;
            }

            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //强力减速
            target.velocity *= 0.3f;

            //血液爆溅
            for (int i = 0; i < 15; i++) {
                Dust blood = Dust.NewDustDirect(target.position, target.width, target.height,
                    DustID.Blood, Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-6f, 6f),
                    100, default, Main.rand.NextFloat(1.8f, 2.8f));
                blood.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D warpTex = CWRUtils.GetT2DValue(CWRConstant.Masking + "DiffusionCircle");
            float fadeAlpha = 1f - Projectile.alpha / 255f;
            Color warpColor = new Color(120, 0, 0, 0) * fadeAlpha;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = warpTex.Size() / 2f;

            //多层旋转增强冲击力
            for (int i = 0; i < 5; i++) {
                float layerAlpha = fadeAlpha * (1f - i * 0.15f);
                float layerScale = Projectile.scale * (1f + i * 0.12f);
                float layerRotation = Projectile.rotation + i * MathHelper.PiOver2 * 0.8f;

                Main.spriteBatch.Draw(warpTex, drawPos, null,
                    warpColor * layerAlpha * 0.65f, layerRotation, origin,
                    layerScale, SpriteEffects.None, 0f);
            }

            //中心增强层
            Main.spriteBatch.Draw(warpTex, drawPos, null,
                new Color(180, 0, 0, 0) * fadeAlpha * 0.8f,
                Projectile.rotation * 0.5f, origin,
                Projectile.scale * 0.7f, SpriteEffects.None, 0f);

            return false;
        }
    }
}
