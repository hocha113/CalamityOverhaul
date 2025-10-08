using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills
{
    //你的层次太低，永远无法理解我现在的状态
    //我是一个时代孕育出来的唯一，既然舍弃了玩家的身份，自封为神
    //自然是百无禁忌，无所不能
    //现在就让你见识一下，可以终结这个灾厄时代的力量，到底有多么可怕
    internal static class YourLevelIsTooLow
    {
        public static int ID = 8;
        private const int ToggleCD = 30;
        private const int UltimateCooldown = 3600; //60秒终极冷却

        public static void AltUse(Item item, Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.YourLevelIsTooLowToggleCD > 0 || hp.YourLevelIsTooLowCooldown > 0) {
                return;
            }

            Activate(player);
            hp.YourLevelIsTooLowToggleCD = ToggleCD;
            hp.YourLevelIsTooLowCooldown = 600; //调试用10秒
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
    /// 终极技能弹幕 - 无限重启叠加效果主控制器
    /// </summary>
    internal class YourLevelIsTooLowProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        //时空克隆体系统
        private List<InfiniteTimeClone> timeClones;
        private int cloneSpawnTimer;
        private const int CloneSpawnInterval = 8; //更快的克隆体生成速度

        //重启特效系统
        private List<RestartFlashEffect> restartFlashes;
        private int restartFlashTimer;
        private const int RestartFlashInterval = 15; //重启闪光间隔

        //鱼群系统
        private List<InfiniteFishBoid> fishSwarms;
        private int fishSpawnTimer;
        private const int FishSpawnInterval = 3; //持续不断的鱼群

        //法阵符环系统
        private List<InfiniteRuneCircle> runeCircles = new();
        private int runeSpawnTimer;

        //能量环系统
        private List<EnergyRing> energyRings = new();

        //炮阵系统
        private List<int> activeCannons = new();
        private int cannonSpawnTimer;
        private const int CannonInterval = 45; //每隔45帧生成一轮炮阵

        //特效强度
        private float globalIntensity = 0f;
        private float pulsePhase = 0f;
        private float restartGlowIntensity = 0f;

        //技能持续时间
        private const int TotalDuration = 600; //10秒的绝对统治时间
        private int skillTimer = 0;

        public override void SetDefaults() {
            Projectile.width = 1200;
            Projectile.height = 1200;
            Projectile.timeLeft = TotalDuration + 60;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
        }

        public override void AI() {
            if (!Owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = Owner.Center;
            skillTimer++;

            //初始化
            if (skillTimer == 1) {
                Initialize();
            }

            //更新全局强度
            UpdateGlobalIntensity();

            //更新各个系统
            UpdateTimeClones();
            UpdateRestartFlashes();
            UpdateFishSwarms();
            UpdateRuneCircles();
            UpdateEnergyRings();
            UpdateCannons();

            //生成新元素
            SpawnNewElements();

            //持续治疗和状态恢复
            ContinuousHeal();

            //结束阶段
            if (skillTimer >= TotalDuration) {
                HandleEnding();
            }
        }

        private new void Initialize() {
            timeClones = new List<InfiniteTimeClone>();
            restartFlashes = new List<RestartFlashEffect>();
            fishSwarms = new List<InfiniteFishBoid>();
            
            //播放开场音效
            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 1.2f, Pitch = -0.3f }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1.0f }, Owner.Center);

            //初始爆发效果
            for (int i = 0; i < 100; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Owner.Center + angle.ToRotationVector2() * Main.rand.NextFloat(150f);
                int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.BlueFairy;
                int dust = Dust.NewDust(pos, 1, 1, dustType, 0, 0, 0, default, 2.5f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (pos - Owner.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(10f, 20f);
            }
        }

        private void UpdateGlobalIntensity() {
            //强度快速上升后保持
            if (skillTimer < 30) {
                globalIntensity = MathHelper.Lerp(globalIntensity, 1f, 0.15f);
            }
            else if (skillTimer > TotalDuration - 60) {
                //结束阶段逐渐减弱
                float endProgress = (skillTimer - (TotalDuration - 60)) / 60f;
                globalIntensity = 1f - endProgress;
            }
            else {
                globalIntensity = 1f;
            }

            pulsePhase += 0.08f;
            float pulse = (float)Math.Sin(pulsePhase) * 0.5f + 0.5f;
            restartGlowIntensity = pulse * globalIntensity;
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
            if (cloneSpawnTimer >= CloneSpawnInterval && timeClones.Count < 50) {
                SpawnTimeClone();
                cloneSpawnTimer = 0;
            }

            //持续生成重启闪光
            restartFlashTimer++;
            if (restartFlashTimer >= RestartFlashInterval && restartFlashes.Count < 5) {
                SpawnRestartFlash();
                restartFlashTimer = 0;
            }

            //持续生成鱼群
            fishSpawnTimer++;
            if (fishSpawnTimer >= FishSpawnInterval && fishSwarms.Count < 200) {
                SpawnFish();
                fishSpawnTimer = 0;
            }

            //持续生成符环
            runeSpawnTimer++;
            if (runeSpawnTimer >= 8 && runeCircles.Count < 12) {
                SpawnRuneCircle();
                runeSpawnTimer = 0;
            }

            //定期生成能量环
            if (skillTimer % 12 == 0 && energyRings.Count < 15) {
                energyRings.Add(new EnergyRing(Owner.Center));
            }

            //定期生成炮阵
            cannonSpawnTimer++;
            if (cannonSpawnTimer >= CannonInterval && activeCannons.Count < 14) {
                SpawnCannonWave();
                cannonSpawnTimer = 0;
            }
        }

        private void SpawnTimeClone() {
            float edge = Main.rand.NextFloat(4f);
            Vector2 spawn;

            //从屏幕四周随机生成
            if (edge < 1f) {
                spawn = Owner.Center + new Vector2(Main.rand.NextFloat(-700, 700), -900);
            }
            else if (edge < 2f) {
                spawn = Owner.Center + new Vector2(Main.rand.NextFloat(-700, 700), 900);
            }
            else if (edge < 3f) {
                spawn = Owner.Center + new Vector2(-900, Main.rand.NextFloat(-700, 700));
            }
            else {
                spawn = Owner.Center + new Vector2(900, Main.rand.NextFloat(-700, 700));
            }

            timeClones.Add(new InfiniteTimeClone(spawn, new PlayerSnapshot(Owner)));
        }

        private void SpawnRestartFlash() {
            restartFlashes.Add(new RestartFlashEffect(Owner.Center));
            
            //播放重启音效
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f, Pitch = 0.3f }, Owner.Center);
        }

        private void SpawnFish() {
            Vector2 spawnPos;
            float side = Main.rand.NextFloat(4f);
            
            if (side < 1f) {
                spawnPos = Owner.Center + new Vector2(Main.rand.NextFloat(-500, 500), -700);
            }
            else if (side < 2f) {
                spawnPos = Owner.Center + new Vector2(Main.rand.NextFloat(-500, 500), 700);
            }
            else if (side < 3f) {
                spawnPos = Owner.Center + new Vector2(-700, Main.rand.NextFloat(-500, 500));
            }
            else {
                spawnPos = Owner.Center + new Vector2(700, Main.rand.NextFloat(-500, 500));
            }

            fishSwarms.Add(new InfiniteFishBoid(spawnPos, Owner.Center));
        }

        private void SpawnRuneCircle() {
            float startR = Main.rand.NextFloat(200f, 350f);
            float endR = Main.rand.NextFloat(100f, 250f);
            bool shrink = Main.rand.NextBool();
            Color colorA = new Color(
                120 + Main.rand.Next(50),
                90 + Main.rand.Next(60),
                210 + Main.rand.Next(45)
            );
            Color colorB = new Color(
                200 + Main.rand.Next(55),
                150 + Main.rand.Next(60),
                255
            );

            runeCircles.Add(new InfiniteRuneCircle(startR, endR, 60, shrink, colorA, colorB));
        }

        private void SpawnCannonWave() {
            var source = Owner.GetSource_Misc("UltimateCannons");
            Vector2 direction = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 backDir = -direction;
            Vector2 perp = direction.RotatedBy(MathHelper.PiOver2);
            
            int cannonCount = 7;
            float arc = MathHelper.ToRadians(70f);

            for (int i = 0; i < cannonCount; i++) {
                float lerpFactor = (cannonCount == 1) ? 0.5f : i / (float)(cannonCount - 1);
                float angleOffset = (lerpFactor - 0.5f) * arc;
                Vector2 offsetDir = backDir.RotatedBy(angleOffset);
                Vector2 position = Owner.Center + backDir * 180f +
                                  perp * (float)Math.Sin(angleOffset) * 40f +
                                  offsetDir * 20f;

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

            SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.5f, Pitch = -0.2f }, Owner.Center);
        }

        private void ContinuousHeal() {
            //每30帧恢复一次生命
            if (skillTimer % 30 == 0) {
                int healAmount = (int)(Owner.statLifeMax2 * 0.05f); //每次恢复5%最大生命
                Owner.statLife = Math.Min(Owner.statLife + healAmount, Owner.statLifeMax2);
                Owner.HealEffect(healAmount);
            }

            //持续清除debuff
            if (skillTimer % 10 == 0) {
                for (int i = 0; i < Player.MaxBuffs; i++) {
                    int buffType = Owner.buffType[i];
                    if (buffType > 0 && Main.debuff[buffType]) {
                        Owner.DelBuff(i);
                    }
                }
            }
        }

        private void HandleEnding() {
            if (skillTimer == TotalDuration) {
                //最终爆发
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.0f }, Owner.Center);
                
                for (int i = 0; i < 150; i++) {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 pos = Owner.Center + angle.ToRotationVector2() * Main.rand.NextFloat(200f);
                    int dustType = Main.rand.Next(new int[] { DustID.Electric, DustID.BlueFairy, DustID.Water });
                    int dust = Dust.NewDust(pos, 1, 1, dustType, 0, 0, 0, default, 3f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = angle.ToRotationVector2() * Main.rand.NextFloat(15f, 30f);
                }
            }

            if (skillTimer > TotalDuration + 60) {
                Projectile.Kill();
            }
        }

        private void DrawTimeClone(InfiniteTimeClone clone) {
            if (clone.Alpha < 0.05f) {
                return;
            }

            Player ghost = new Player();
            ghost.ResetEffects();
            ghost.CopyVisuals(Owner);
            ghost.position = clone.Position - Owner.Size * 0.5f;
            ghost.direction = Owner.direction;
            ghost.bodyFrame = Owner.bodyFrame;
            ghost.legFrame = Owner.legFrame;

            Color ghostColor = new Color(170, 130, 255) * clone.Alpha * 0.85f;
            ghost.skinColor = ghostColor;
            ghost.shirtColor = ghostColor;
            ghost.underShirtColor = ghostColor;
            ghost.pantsColor = ghostColor;
            ghost.shoeColor = ghostColor;
            ghost.hairColor = ghostColor;
            ghost.eyeColor = ghostColor;

            try {
                Main.PlayerRenderer.DrawPlayer(
                    Main.Camera,
                    ghost,
                    ghost.position,
                    0f,
                    ghost.fullRotationOrigin
                );
            } catch {
                //忽略渲染异常
            }
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
            if (restartGlowIntensity < 0.05f) return;

            Texture2D bloomTex = CWRAsset.StarTexture.Value;
            float scale = 8f + restartGlowIntensity * 4f;
            Color glowColor = new Color(150, 230, 255, 0) * restartGlowIntensity * 0.3f;

            //多层光晕
            for (int i = 0; i < 3; i++) {
                float layerScale = scale * (1f + i * 0.3f);
                float layerAlpha = restartGlowIntensity * (1f - i * 0.3f);
                Main.spriteBatch.Draw(bloomTex, Owner.Center - Main.screenPosition, null, 
                    glowColor * layerAlpha,
                    pulsePhase + i * MathHelper.PiOver2, bloomTex.Size() / 2f, layerScale, 
                    SpriteEffects.None, 0f);
            }
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
        
        private const int MaxTrailLength = 35;
        private float spiralAngle;
        private float timeWarpFactor;
        private float orbitRadius;
        private int convergePhase;

        public InfiniteTimeClone(Vector2 spawnPos, PlayerSnapshot snapshot) {
            Position = spawnPos;
            Snapshot = snapshot;
            Velocity = Vector2.Zero;
            Life = 0f;
            MaxLife = 180f;
            Alpha = 0f;
            spiralAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            timeWarpFactor = Main.rand.NextFloat(0.9f, 1.4f);
            orbitRadius = 450f;
            convergePhase = 0;
        }

        public void Update(Vector2 center) {
            Life++;
            
            float progress = Life / MaxLife;

            //三阶段运动
            if (progress < 0.4f) {
                //阶段1：螺旋接近
                float approachProgress = progress / 0.4f;
                orbitRadius = MathHelper.Lerp(450f, 280f, approachProgress);
                spiralAngle += 0.09f * timeWarpFactor;
                Alpha = MathHelper.Clamp(approachProgress * 2f, 0f, 1f);
            }
            else if (progress < 0.8f) {
                //阶段2：快速收拢
                convergePhase = 1;
                float convergeProgress = (progress - 0.4f) / 0.4f;
                orbitRadius = MathHelper.Lerp(280f, 50f, MathHelper.SmoothStep(0f, 1f, convergeProgress));
                spiralAngle += 0.15f * timeWarpFactor;
                Alpha = 1f;
            }
            else {
                //阶段3：融合消失
                convergePhase = 2;
                float fadeProgress = (progress - 0.8f) / 0.2f;
                orbitRadius = MathHelper.Lerp(50f, 0f, fadeProgress);
                spiralAngle += 0.2f * timeWarpFactor;
                Alpha = 1f - fadeProgress;
            }

            //计算位置
            Vector2 targetPos = center + spiralAngle.ToRotationVector2() * orbitRadius;
            Vector2 toTarget = targetPos - Position;
            Velocity = Vector2.Lerp(Velocity, toTarget * 0.25f, 0.35f);
            Position += Velocity;

            //记录拖尾
            TrailPositions.Insert(0, Position);
            if (TrailPositions.Count > MaxTrailLength) {
                TrailPositions.RemoveAt(TrailPositions.Count - 1);
            }
        }

        public bool ShouldRemove() => Life >= MaxLife || Alpha < 0.01f;

        public void DrawTrail(float globalAlpha) {
            if (TrailPositions.Count < 3) return;

            Texture2D tex = TextureAssets.MagicPixel.Value;

            for (int i = 0; i < TrailPositions.Count - 1; i++) {
                float progress = i / (float)TrailPositions.Count;
                float trailAlpha = (1f - progress) * Alpha * globalAlpha * 0.6f;

                Vector2 start = TrailPositions[i];
                Vector2 end = TrailPositions[i + 1];
                Vector2 diff = end - start;
                float length = diff.Length();

                if (length < 0.01f) continue;

                float rotation = diff.ToRotation();
                Color color = new Color(180, 140, 255, 0) * trailAlpha;

                Main.spriteBatch.Draw(
                    tex,
                    start - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, 7f - progress * 5f),
                    SpriteEffects.None,
                    0f
                );
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

        public RestartFlashEffect(Vector2 center) {
            Center = center;
            Life = 0f;
            MaxLife = 40f;
            Intensity = 0f;
            Scale = 0f;
        }

        public void Update() {
            Life++;
            float progress = Life / MaxLife;

            //快速上升再缓慢下降
            if (progress < 0.3f) {
                Intensity = progress / 0.3f;
                Scale = Intensity * 6f;
            }
            else {
                Intensity = (float)Math.Pow(1f - (progress - 0.3f) / 0.7f, 1.5f);
                Scale = 6f + (progress - 0.3f) * 3f;
            }
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void Draw(float globalAlpha) {
            if (Intensity < 0.05f) return;

            Texture2D bloomTex = CWRAsset.StarTexture.Value;
            Color flashColor = new Color(150, 230, 255, 0) * Intensity * globalAlpha * 0.7f;

            //十字光芒
            for (int i = 0; i < 4; i++) {
                float rot = i * MathHelper.PiOver2;
                Main.spriteBatch.Draw(bloomTex, Center - Main.screenPosition, null, flashColor,
                    rot, bloomTex.Size() / 2f, Scale * (1f + i * 0.1f), SpriteEffects.None, 0f);
            }
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
            if (TrailPositions.Count < 2) return;
            Texture2D tex = TextureAssets.MagicPixel.Value;

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
            RotSpeed = Main.rand.NextFloat(-0.06f, 0.06f);
            EllipseFactor = Main.rand.NextFloat(0.7f, 1.3f);
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
            float radius = Shrink
                ? MathHelper.Lerp(StartRadius, EndRadius, progress)
                : MathHelper.Lerp(StartRadius, EndRadius, (float)Math.Sin(progress * MathHelper.Pi));
            float fade = (float)Math.Sin(progress * MathHelper.Pi) * alpha * 0.8f;

            if (fade <= 0.01f) return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segments = 140;
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++) {
                float angle1 = Rotation + i * angleStep;
                float angle2 = Rotation + (i + 1) * angleStep;

                Vector2 p1 = center + new Vector2(
                    (float)Math.Cos(angle1) * radius,
                    (float)Math.Sin(angle1) * radius * EllipseFactor
                );
                Vector2 p2 = center + new Vector2(
                    (float)Math.Cos(angle2) * radius,
                    (float)Math.Sin(angle2) * radius * EllipseFactor
                );

                Vector2 diff = p2 - p1;
                float length = diff.Length();
                if (length < 0.0001f) continue;

                float rotation = diff.ToRotation();
                float wave = (float)Math.Sin(angle1 * 8f + Main.GlobalTimeWrappedHourly * 10f) * 0.5f + 0.5f;
                Color color = Color.Lerp(ColorA, ColorB, wave) * fade;

                Main.spriteBatch.Draw(
                    pixel,
                    p1 - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, 2.5f),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
    #endregion
}
