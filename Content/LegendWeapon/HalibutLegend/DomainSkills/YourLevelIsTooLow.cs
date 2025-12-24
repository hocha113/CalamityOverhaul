using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills
{
    //你的层次太低，永远无法理解我现在的状态
    //我是一个时代孕育出来的唯一，既然舍弃了玩家的身份，自封为神
    //自然是百无禁忌，无所不能
    //现在就让你见识一下，可以终结这个灾厄时代的力量，到底有多么可怕
    internal static class YourLevelIsTooLow
    {
        public static int ID = 8;
        public static void AltUse(Item item, Player player) {
            TryAutoActivate(player);
        }
        public static void TryAutoActivate(Player player) {
            if (!Main.mouseLeft) {
                return;
            }
            if (!player.TryGetOverride<HalibutPlayer>(out var hp)) {
                return;
            }
            if (hp.SeaDomainLayers < 10 || !hp.SeaDomainActive) {
                return;
            }
            if (!HalibutPlayer.TheOnlyBornOfAnEra()) {
                return;
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int projType = ModContent.ProjectileType<YourLevelIsTooLowProj>();
            if (player.ownedProjectileCounts[projType] > 0) {
                return;
            }
            Activate(player);
        }
        public static void Activate(Player player) {
            if (Main.myPlayer == player.whoAmI) {
                SpawnUltimateEffect(player);
            }
        }
        internal static void SpawnUltimateEffect(Player player) {
            var source = player.GetSource_Misc("YourLevelIsTooLowSkill");
            Projectile.NewProjectile(
                source,
                player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<YourLevelIsTooLowProj>(),
                0,
                0,
                player.whoAmI
            );
        }
    }

    /// <summary>
    /// 无限重启叠加效果主控制器
    /// </summary>
    internal class YourLevelIsTooLowProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        //时空克隆体系统
        private List<InfiniteTimeClone> timeClones;
        private int cloneSpawnTimer;
        private const int CloneSpawnInterval = 10; //稍减频率，因演出更复杂

        //重启特效系统
        private List<RestartFlashEffect> restartFlashes;
        private int restartFlashTimer;
        private const int RestartFlashInterval = 18; //稍降频率

        //鱼群系统
        private List<InfiniteFishBoid> fishSwarms;
        private int fishSpawnTimer;
        private const int FishSpawnInterval = 3;

        //法阵符环系统
        private List<InfiniteRuneCircle> runeCircles = new();
        private int runeSpawnTimer;

        //能量环系统
        private List<EnergyRing> energyRings = new();

        //炮阵系统
        private List<int> activeCannons = new();
        private int cannonSpawnTimer;
        private const int CannonInterval = 52; //轻微降低强度

        //特效强度
        private float globalIntensity = 0f;
        private float pulsePhase = 0f;
        private float restartGlowIntensity = 0f;

        //持续控制
        private int skillTimer = 0; //用于内部节奏（不再限定上限）
        private bool endingPhase;
        private int endTimer;
        private const int EndDuration = 60; //结束渐隐时长

        public override void SetDefaults() {
            Projectile.width = 1200;
            Projectile.height = 1200;
            Projectile.timeLeft = 120; //通过刷新 timeLeft 维持
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
        }

        public override void AI() {
            if (!Owner.Alives()) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = Owner.Center;
            skillTimer++;

            if (skillTimer == 1) {
                Initialize();
            }

            bool keepCondition = Main.mouseLeft;
            if (Owner.TryGetOverride<HalibutPlayer>(out var hp)) {
                if (hp.SeaDomainLayers < 10) {
                    keepCondition = false;
                }
            }
            if (!HalibutPlayer.TheOnlyBornOfAnEra()) {
                keepCondition = false;
            }

            if (!endingPhase) {
                if (keepCondition) {
                    Projectile.timeLeft = 120; //刷新生存
                }
                else {
                    endingPhase = true;
                }
            }

            UpdateGlobalIntensity();
            if (endingPhase) {
                float fade = 1f - endTimer / (float)EndDuration;
                globalIntensity *= fade;
                restartGlowIntensity *= fade;
            }

            UpdateTimeClones();
            UpdateRestartFlashes();
            UpdateFishSwarms();
            UpdateRuneCircles();
            UpdateEnergyRings();
            UpdateCannons();
            if (!endingPhase) {
                SpawnNewElements();
                ContinuousHeal();
            }

            if (endingPhase) {
                endTimer++;
                if (endTimer >= EndDuration) {
                    Projectile.Kill();
                }
            }
        }

        private new void Initialize() {
            timeClones = new List<InfiniteTimeClone>();
            restartFlashes = new List<RestartFlashEffect>();
            fishSwarms = new List<InfiniteFishBoid>();

            //播放开场音效
            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 1.0f, Pitch = -0.32f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.9f }, Owner.Center);

            //初始爆发效果
            for (int i = 0; i < 80; i++) { //初始爆发稍微削弱数量
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Owner.Center + angle.ToRotationVector2() * Main.rand.NextFloat(120f);
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.BlueFairy;
                int dust = Dust.NewDust(pos, 1, 1, dustType, 0, 0, 0, default, 2.1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (pos - Owner.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(8f, 16f);
            }
        }

        private void UpdateGlobalIntensity() {
            if (!endingPhase) {
                if (skillTimer < 24) {
                    globalIntensity = MathHelper.Lerp(globalIntensity, 0.95f, 0.18f);
                }
                else {
                    globalIntensity = 0.95f; //稍低基准
                }
            }
            pulsePhase += 0.07f; //减慢脉冲速度
            float pulse = (float)Math.Sin(pulsePhase) * 0.5f + 0.5f;
            restartGlowIntensity = pulse * globalIntensity * 0.9f; //整体削弱
        }

        private void UpdateTimeClones() {
            //更新所有克隆体
            for (int i = timeClones.Count - 1; i >= 0; i--) {
                timeClones[i].Update(Owner.Center);
                if (timeClones[i].ShouldRemove()) {
                    timeClones.RemoveAt(i);
                }
            }
        }

        private void UpdateRestartFlashes() {
            //更新重启闪光
            for (int i = restartFlashes.Count - 1; i >= 0; i--) {
                restartFlashes[i].Center = Owner.Center;
                restartFlashes[i].Update();
                if (restartFlashes[i].ShouldRemove()) {
                    restartFlashes.RemoveAt(i);
                }
            }
        }

        private void UpdateFishSwarms() {
            //更新鱼群
            for (int i = fishSwarms.Count - 1; i >= 0; i--) {
                fishSwarms[i].Update(Owner.Center);
                if (fishSwarms[i].ShouldRemove()) {
                    fishSwarms.RemoveAt(i);
                }
            }
        }

        private void UpdateRuneCircles() {
            //更新符环
            foreach (var rune in runeCircles) {
                rune.Update();
            }
            runeCircles.RemoveAll(r => r.Dead);
        }

        private void UpdateEnergyRings() {
            //更新能量环
            for (int i = energyRings.Count - 1; i >= 0; i--) {
                energyRings[i].Update();
                if (energyRings[i].ShouldRemove()) {
                    energyRings.RemoveAt(i);
                }
            }
        }

        private void UpdateCannons() {
            //更新并清理失效的炮
            activeCannons.RemoveAll(id => id < 0 || id >= Main.maxProjectiles || !Main.projectile[id].active);
        }

        private void SpawnNewElements() {
            //持续生成时空克隆体
            cloneSpawnTimer++;
            if (cloneSpawnTimer >= CloneSpawnInterval && timeClones.Count < 42) {
                SpawnTimeClone();
                cloneSpawnTimer = 0;
            }

            //持续生成重启闪光
            restartFlashTimer++;
            if (restartFlashTimer >= RestartFlashInterval && restartFlashes.Count < 4) {
                SpawnRestartFlash();
                restartFlashTimer = 0;
            }

            //持续生成鱼群
            fishSpawnTimer++;
            if (fishSpawnTimer >= FishSpawnInterval && fishSwarms.Count < 170) {
                SpawnFish();
                fishSpawnTimer = 0;
            }

            //持续生成符环
            runeSpawnTimer++;
            if (runeSpawnTimer >= 10 && runeCircles.Count < 10) {
                SpawnRuneCircle();
                runeSpawnTimer = 0;
            }

            //定期生成能量环
            if (skillTimer % 14 == 0 && energyRings.Count < 12) {
                energyRings.Add(new EnergyRing(Owner.Center));
            }

            //定期生成炮阵
            cannonSpawnTimer++;
            if (cannonSpawnTimer >= CannonInterval && activeCannons.Count < 12) {
                SpawnCannonWave();
                cannonSpawnTimer = 0;
            }
        }

        private void SpawnTimeClone() {
            float edge = Main.rand.NextFloat(4f);
            Vector2 spawn;
            float distant = Main.rand.NextFloat(1100f, 1500f); //更远距离

            //从屏幕四周随机生成
            if (edge < 1f) {
                spawn = Owner.Center + new Vector2(Main.rand.NextFloat(-distant * 0.7f, distant * 0.7f), -distant);
            }
            else if (edge < 2f) {
                spawn = Owner.Center + new Vector2(Main.rand.NextFloat(-distant * 0.7f, distant * 0.7f), distant);
            }
            else if (edge < 3f) {
                spawn = Owner.Center + new Vector2(-distant, Main.rand.NextFloat(-distant * 0.7f, distant * 0.7f));
            }
            else {
                spawn = Owner.Center + new Vector2(distant, Main.rand.NextFloat(-distant * 0.7f, distant * 0.7f));
            }

            timeClones.Add(new InfiniteTimeClone(spawn, new PlayerSnapshot(Owner)));
        }

        private void SpawnRestartFlash() {
            restartFlashes.Add(new RestartFlashEffect(Owner.Center));

            //播放重启音效
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.33f, Pitch = 0.25f }, Owner.Center);
        }

        private void SpawnFish() {
            Vector2 spawnPos;
            float side = Main.rand.NextFloat(4f);
            float far = Main.rand.NextFloat(650f, 900f);

            if (side < 1f) {
                spawnPos = Owner.Center + new Vector2(Main.rand.NextFloat(-far * 0.7f, far * 0.7f), -far);
            }
            else if (side < 2f) {
                spawnPos = Owner.Center + new Vector2(Main.rand.NextFloat(-far * 0.7f, far * 0.7f), far);
            }
            else if (side < 3f) {
                spawnPos = Owner.Center + new Vector2(-far, Main.rand.NextFloat(-far * 0.7f, far * 0.7f));
            }
            else {
                spawnPos = Owner.Center + new Vector2(far, Main.rand.NextFloat(-far * 0.7f, far * 0.7f));
            }

            fishSwarms.Add(new InfiniteFishBoid(spawnPos, Owner.Center));
        }

        private void SpawnRuneCircle() {
            float startR = Main.rand.NextFloat(240f, 380f);
            float endR = Main.rand.NextFloat(120f, 260f);
            bool shrink = Main.rand.NextBool();
            Color colorA = new Color(110 + Main.rand.Next(55), 80 + Main.rand.Next(60), 205 + Main.rand.Next(45));
            Color colorB = new Color(195 + Main.rand.Next(55), 140 + Main.rand.Next(60), 255);

            runeCircles.Add(new InfiniteRuneCircle(startR, endR, 66, shrink, colorA, colorB));
        }

        private void SpawnCannonWave() {
            var source = Owner.GetSource_Misc("UltimateCannons");
            Vector2 direction = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 backDir = -direction;
            Vector2 perp = direction.RotatedBy(MathHelper.PiOver2);

            int cannonCount = 7;
            float arc = MathHelper.ToRadians(64f);

            for (int i = 0; i < cannonCount; i++) {
                float lerpFactor = (cannonCount == 1) ? 0.5f : i / (float)(cannonCount - 1);
                float angleOffset = (lerpFactor - 0.5f) * arc;
                Vector2 offsetDir = backDir.RotatedBy(angleOffset);
                Vector2 position = Owner.Center + backDir * 170f +
                                  perp * (float)Math.Sin(angleOffset) * 34f +
                                  offsetDir * 16f;

                int id = Projectile.NewProjectile(
                    source,
                    position,
                    Vector2.Zero,
                    ModContent.ProjectileType<SuperpositionCannon>(),
                    Owner.HeldItem.damage,
                    4f,
                    Owner.whoAmI,
                    angleOffset,
                    0
                );

                if (id >= 0) {
                    activeCannons.Add(id);
                }
            }

            SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.42f, Pitch = -0.18f }, Owner.Center);
        }

        private void ContinuousHeal() {
            //每36帧恢复一次生命
            if (skillTimer % 36 == 0) { //频率稍降低
                RestartFish.ExecuteRestart(Owner);
            }
        }

        private void DrawTimeClone(InfiniteTimeClone clone) {
            if (clone.Alpha < 0.05f) {
                return;
            }

            Player ghost = new Player();

            ghost.CopyVisuals(Owner);
            ghost.ResetEffects();
            ghost.position = clone.Position - Owner.Size * 0.5f;
            ghost.direction = Owner.direction;
            ghost.bodyFrame = Owner.bodyFrame;
            ghost.legFrame = Owner.legFrame;
            ghost.heldProj = -1;

            Lighting.AddLight(clone.Position, TorchID.Blue);

            Color ghostColor = Color.Blue;
            ghost.skinColor = ghostColor;
            ghost.shirtColor = ghostColor;
            ghost.underShirtColor = ghostColor;
            ghost.pantsColor = ghostColor;
            ghost.shoeColor = ghostColor;
            ghost.hairColor = ghostColor;
            ghost.eyeColor = ghostColor;

            Main.PlayerRenderer.DrawPlayer(
                    Main.Camera,
                    ghost,
                    ghost.position,
                    0f,
                    ghost.fullRotationOrigin
                );
        }

        public override bool PreDraw(ref Color lightColor) {
            //绘制全局重启闪光背景
            DrawGlobalRestartGlow();

            //绘制符环
            foreach (var rune in runeCircles) {
                rune.Draw(Owner.Center, globalIntensity);
            }

            //绘制能量环
            foreach (var ring in energyRings) {
                ring.Draw(globalIntensity);
            }

            //绘制重启闪光
            foreach (var flash in restartFlashes) {
                flash.Draw(globalIntensity);
            }

            //绘制鱼群拖尾
            foreach (var fish in fishSwarms) {
                fish.DrawTrail(globalIntensity);
            }

            //绘制鱼群主体
            foreach (var fish in fishSwarms) {
                fish.Draw(globalIntensity);
            }

            //绘制克隆体拖尾
            foreach (var clone in timeClones) {
                clone.DrawTrail(globalIntensity);
            }

            //绘制克隆体
            foreach (var clone in timeClones) {
                DrawTimeClone(clone);
            }

            return false;
        }

        private void DrawGlobalRestartGlow() {
            if (restartGlowIntensity < 0.05f) {
                return;
            }

            Texture2D bloomTex = CWRAsset.StarTexture.Value;
            float scale = 6.2f + restartGlowIntensity * 3.2f; //整体缩小
            Color glowColor = new Color(140, 220, 250, 0) * restartGlowIntensity * 0.22f; //透明度降低

            //多层光晕
            for (int i = 0; i < 3; i++) {
                float layerScale = scale * (1f + i * 0.28f);
                float layerAlpha = restartGlowIntensity * (0.75f - i * 0.22f);
                Main.spriteBatch.Draw(bloomTex, Owner.Center - Main.screenPosition, null,
                    glowColor * layerAlpha,
                    pulsePhase * (1f - i * 0.15f) + i * MathHelper.PiOver4,
                    bloomTex.Size() / 2f,
                    layerScale,
                    SpriteEffects.None,
                    0f);
            }
        }
    }

    internal class YourLevelIsTooLowPlayer : PlayerOverride
    {
        public override bool? On_PreKill(double damage, int hitDirection, bool pvp
            , ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (Player.CountProjectilesOfID<YourLevelIsTooLowProj>() > 0) {
                return false; //无限重启，不死
            }
            return null;
        }
    }

    #region 无限时空克隆体
    internal class InfiniteTimeClone
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Alpha;
        public float Life;
        public float MaxLife;
        public PlayerSnapshot Snapshot;
        public readonly List<Vector2> TrailPositions = new();

        private const int MaxTrailLength = 42;
        private float spiralAngle;
        private float timeWarpFactor;
        private float orbitRadius;
        private float startOrbit;
        private bool burstSpawned; //进入核心爆裂特效

        public InfiniteTimeClone(Vector2 spawnPos, PlayerSnapshot snapshot) {
            Position = spawnPos;
            Snapshot = snapshot;
            Velocity = Vector2.Zero;
            Life = 0f;
            MaxLife = 240f; //更长旅程
            Alpha = 0f;
            spiralAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            timeWarpFactor = Main.rand.NextFloat(0.85f, 1.35f);
            startOrbit = orbitRadius = Main.rand.NextFloat(620f, 880f);
        }

        public void Update(Vector2 center) {
            Life++;

            float progress = Life / MaxLife;

            //多阶段 0-0.25 远距显形 0.25-0.6 螺旋加速 0.6-0.85 快速逼近 0.85-1 融入消散
            if (progress < 0.25f) {
                float p = progress / 0.25f;
                orbitRadius = MathHelper.Lerp(startOrbit, startOrbit * 0.9f, p);
                spiralAngle += 0.05f * timeWarpFactor;
                Alpha = p * 0.8f;
            }
            else if (progress < 0.6f) {
                float p = (progress - 0.25f) / 0.35f; //0-1
                orbitRadius = MathHelper.Lerp(startOrbit * 0.9f, 340f, MathHelper.SmoothStep(0f, 1f, p));
                spiralAngle += 0.11f * timeWarpFactor * (1f + p * 0.5f);
                Alpha = 0.8f + p * 0.2f;
            }
            else if (progress < 0.85f) {
                float p = (progress - 0.6f) / 0.25f;
                orbitRadius = MathHelper.Lerp(340f, 60f, CWRUtils.EaseOutCubic(p));
                spiralAngle += 0.18f * timeWarpFactor * (1f + p);
                Alpha = 1f;
                if (!burstSpawned && p > 0.3f) { //临近核心喷洒震荡尘粒
                    burstSpawned = true;
                    SpawnConvergeBurst(center);
                }
            }
            else {
                float p = (progress - 0.85f) / 0.15f;
                orbitRadius = MathHelper.Lerp(60f, 0f, p);
                spiralAngle += 0.22f * timeWarpFactor;
                Alpha = 1f - p;
            }

            //计算位置
            Vector2 targetPos = center + spiralAngle.ToRotationVector2() * orbitRadius;
            Vector2 toTarget = targetPos - Position;
            Velocity = Vector2.Lerp(Velocity, toTarget * 0.22f, 0.32f);
            Position += Velocity;

            //记录拖尾
            TrailPositions.Insert(0, Position);
            if (TrailPositions.Count > MaxTrailLength) {
                TrailPositions.RemoveAt(TrailPositions.Count - 1);
            }
        }

        private void SpawnConvergeBurst(Vector2 center) {
            if (!Main.dedServ) {
                for (int i = 0; i < 24; i++) {
                    float ang = MathHelper.TwoPi * i / 24f + spiralAngle;
                    Vector2 p = Position + ang.ToRotationVector2() * Main.rand.NextFloat(6f, 18f);
                    int dust = Dust.NewDust(p, 1, 1, DustID.Electric, 0, 0, 0, default, 1.2f + Main.rand.NextFloat(0.4f));
                    Main.dust[dust].velocity = (p - center).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(4f, 12f);
                    Main.dust[dust].noGravity = true;
                }
            }
        }

        public bool ShouldRemove() => Life >= MaxLife || Alpha < 0.01f;

        public void DrawTrail(float globalAlpha) {
            if (TrailPositions.Count < 3) {
                return;
            }

            Texture2D tex = VaultAsset.placeholder2.Value;

            for (int i = 0; i < TrailPositions.Count - 1; i++) {
                float progress = i / (float)TrailPositions.Count;
                float trailAlpha = (1f - progress) * Alpha * globalAlpha * 0.55f; //稍降亮度

                Vector2 start = TrailPositions[i];
                Vector2 end = TrailPositions[i + 1];
                Vector2 diff = end - start;
                float length = diff.Length();

                if (length < 0.01f) {
                    continue;
                }

                float rotation = diff.ToRotation();
                float width = 6f - progress * 4.5f;
                Color color = new Color(160, 130, 255, 0) * trailAlpha;

                Main.spriteBatch.Draw(tex, start - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, width),
                    SpriteEffects.None,
                    0f
                );

                if (i % 8 == 0 && progress < 0.6f) { //星点闪烁
                    float sparkScale = 1.2f - progress * 0.7f;
                    Color spark = new Color(200, 220, 255) * trailAlpha * 0.8f;
                    Main.spriteBatch.Draw(tex, start - Main.screenPosition, new Rectangle(0, 0, 1, 1), spark, 0f, Vector2.Zero, new Vector2(2f, 2f) * sparkScale, SpriteEffects.None, 0f);
                }
            }
        }
    }
    #endregion

    #region 重启闪光效果
    internal class RestartFlashEffect
    {
        public Vector2 Center;
        public float Life;
        public float MaxLife;
        public float Intensity;
        public float Scale;
        private float rotationSeed;

        public RestartFlashEffect(Vector2 center) {
            Center = center;
            Life = 0f;
            MaxLife = 40f;
            Intensity = 0f;
            Scale = 0f;
            rotationSeed = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public void Update() {
            Life++;
            float progress = Life / MaxLife;

            if (progress < 0.28f) {
                float p = progress / 0.28f;
                Intensity = p;
                Scale = MathHelper.Lerp(0f, 5f, CWRUtils.EaseOutBack(p)); //缩小初始规模
            }
            else {
                float p = (progress - 0.28f) / 0.72f;
                Intensity = (float)Math.Pow(1f - p, 1.35f);
                Scale = 5f + p * 2.2f; //终末尺寸更小
            }
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void Draw(float globalAlpha) {
            if (Intensity < 0.05f) {
                return;
            }

            Texture2D bloomTex = CWRAsset.StarTexture.Value;
            float baseRot = rotationSeed + Main.GlobalTimeWrappedHourly * 0.8f;
            Color flashColor = new Color(150, 225, 255, 0) * Intensity * globalAlpha * 0.55f; //降低亮度

            for (int i = 0; i < 4; i++) {
                float rot = baseRot + i * MathHelper.PiOver2;
                Main.spriteBatch.Draw(bloomTex, Center - Main.screenPosition, null, flashColor,
                    rot, bloomTex.Size() / 2f, Scale * (0.85f + i * 0.08f), SpriteEffects.None, 0f);
            }

            //柔和外环
            Main.spriteBatch.Draw(bloomTex, Center - Main.screenPosition, null,
                new Color(80, 160, 220, 0) * Intensity * globalAlpha * 0.25f,
                0f, bloomTex.Size() / 2f, Scale * 1.55f, SpriteEffects.None, 0f);
        }
    }
    #endregion

    #region 无限鱼群
    internal class InfiniteFishBoid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Scale;
        public int FishType;
        public Color TintColor;
        public float Life;
        public float MaxLife;
        public readonly List<Vector2> TrailPositions = new();

        private const int MaxTrailLength = 15;
        private float spiralAngle;

        public InfiniteFishBoid(Vector2 spawnPos, Vector2 targetPos) {
            Position = spawnPos;
            Velocity = (targetPos - spawnPos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(18f, 28f);
            Scale = 0.5f + Main.rand.NextFloat() * 0.4f;
            FishType = Main.rand.Next(3);
            Life = 0f;
            MaxLife = 150f;
            spiralAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            TintColor = new Color(100 + Main.rand.Next(50), 200 + Main.rand.Next(55), 255);
        }

        public void Update(Vector2 playerCenter) {
            Life++;
            float progress = Life / MaxLife;

            //螺旋运动
            spiralAngle += 0.1f;
            Vector2 spiralOffset = spiralAngle.ToRotationVector2() * (float)Math.Sin(progress * MathHelper.Pi) * 40f;

            Vector2 toTarget = (playerCenter - Position).SafeNormalize(Vector2.Zero);
            Velocity = Vector2.Lerp(Velocity, (toTarget * 22f) + spiralOffset * 0.4f, 0.12f);
            Position += Velocity;

            TrailPositions.Insert(0, Position);
            if (TrailPositions.Count > MaxTrailLength) {
                TrailPositions.RemoveAt(TrailPositions.Count - 1);
            }
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void DrawTrail(float globalAlpha) {
            if (TrailPositions.Count < 2) {
                return;
            }
            Texture2D tex = VaultAsset.placeholder2.Value;

            for (int i = 0; i < TrailPositions.Count - 1; i++) {
                float progress = i / (float)TrailPositions.Count;
                float trailAlpha = (1f - progress) * globalAlpha * 0.7f;
                float width = Scale * (6f - progress * 4f);

                Vector2 start = TrailPositions[i];
                Vector2 end = TrailPositions[i + 1];
                Vector2 diff = end - start;
                float rot = diff.ToRotation();
                float len = diff.Length();

                Color c = TintColor * trailAlpha;
                Main.spriteBatch.Draw(tex, start - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                    c, rot, Vector2.Zero, new Vector2(len, width), SpriteEffects.None, 0f);
            }
        }

        public void Draw(float globalAlpha) {
            int itemType = FishType switch {
                0 => ItemID.Tuna,
                1 => ItemID.Bass,
                2 => ItemID.Trout,
                _ => ItemID.Tuna
            };

            Main.instance.LoadItem(itemType);
            Texture2D fishTex = TextureAssets.Item[itemType].Value;
            Rectangle rect = fishTex.Bounds;
            SpriteEffects effects = Velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float rot = Velocity.ToRotation() + (Velocity.X > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            Vector2 origin = rect.Size() * 0.5f;

            float alpha = globalAlpha * (1f - Life / MaxLife * 0.3f);
            Color c = TintColor * alpha;

            Main.spriteBatch.Draw(fishTex, Position - Main.screenPosition, rect, c, rot, origin, Scale, effects, 0f);
        }
    }
    #endregion

    #region 无限符环
    internal class InfiniteRuneCircle
    {
        public float Life;
        public float MaxLife;
        public float StartRadius;
        public float EndRadius;
        public float Rotation;
        public float RotSpeed;
        public float EllipseFactor;
        public Color ColorA;
        public Color ColorB;
        public bool Shrink;
        public InfiniteRuneCircle(float startR, float endR, int life, bool shrink, Color a, Color b) {
            StartRadius = startR;
            EndRadius = endR;
            MaxLife = life;
            Life = 0;
            Shrink = shrink;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            RotSpeed = Main.rand.NextFloat(-0.055f, 0.055f);
            EllipseFactor = Main.rand.NextFloat(0.75f, 1.25f);
            ColorA = a;
            ColorB = b;
        }
        public void Update() {
            Life++;
            Rotation += RotSpeed;
        }
        public bool Dead => Life >= MaxLife;
        public void Draw(Vector2 center, float alpha) {
            float progress = Life / MaxLife;
            float radius = Shrink ? MathHelper.Lerp(StartRadius, EndRadius, progress) : MathHelper.Lerp(StartRadius, EndRadius, (float)Math.Sin(progress * MathHelper.Pi));
            float fade = (float)Math.Sin(progress * MathHelper.Pi) * alpha * 0.75f;
            if (fade <= 0.01f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segments = 120;
            float angleStep = MathHelper.TwoPi / segments;
            for (int i = 0; i < segments; i++) {
                float angle1 = Rotation + i * angleStep;
                float angle2 = Rotation + (i + 1) * angleStep;
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1) * radius, (float)Math.Sin(angle1) * radius * EllipseFactor);
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2) * radius, (float)Math.Sin(angle2) * radius * EllipseFactor);
                Vector2 diff = p2 - p1;
                float length = diff.Length();
                if (length < 0.0001f) {
                    continue;
                }
                float rotation = diff.ToRotation();
                float wave = (float)Math.Sin(angle1 * 7f + Main.GlobalTimeWrappedHourly * 9f) * 0.5f + 0.5f;
                Color color = Color.Lerp(ColorA, ColorB, wave) * fade;
                Main.spriteBatch.Draw(pixel, p1 - Main.screenPosition, new Rectangle(0, 0, 1, 1), color, rotation, Vector2.Zero, new Vector2(length, 2.2f), SpriteEffects.None, 0f);
            }
        }
    }
    #endregion
}
