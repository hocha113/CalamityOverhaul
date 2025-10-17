using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Resurrections;
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

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills
{
    internal static class RestartFish
    {
        public static int ID = 5;
        private const int ToggleCD = 20;
        private const int RestartCooldown = 60 * 60 * 3; //3分钟冷却

        public static void AltUse(Item item, Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.RestartFishToggleCD > 0 || hp.RestartFishCooldown > 0) return;
            Activate(player);
            hp.RestartFishToggleCD = ToggleCD;
            hp.RestartFishCooldown = RestartCooldown;
        }

        public static void Activate(Player player) {
            if (Main.myPlayer == player.whoAmI) {
                SpawnRestartEffect(player);
            }
        }

        internal static void SpawnRestartEffect(Player player) {
            var source = player.GetSource_Misc("RestartFishSkill");
            Projectile.NewProjectile(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<RestartEffectProj>(), 0, 0, player.whoAmI);
        }
    }

    #region 重启鱼群
    internal class RestartFishBoid
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 TargetPosition;
        public float Scale;
        public float Frame;
        public int FishType;
        public Color TintColor;
        public float LifeProgress; //0-1，用于控制生命周期
        public float MaxLife;
        public float Life;
        private float rotationSpeed;
        private float spiralAngle;
        private float spiralRadius;
        public readonly List<Vector2> TrailPositions = new();
        private const int MaxTrailLength = 12;

        public RestartFishBoid(Vector2 spawnPos, Vector2 targetPos) {
            var rand = Main.rand;
            Position = spawnPos;
            TargetPosition = targetPos;
            Velocity = (targetPos - spawnPos).SafeNormalize(Vector2.Zero) * rand.NextFloat(15f, 25f);

            Scale = 0.5f + rand.NextFloat() * 0.4f;
            Frame = rand.NextFloat(10f);
            FishType = rand.Next(3);
            Life = 0f;
            MaxLife = 120f;
            LifeProgress = 0f;

            rotationSpeed = rand.NextFloat(0.05f, 0.1f) * (rand.NextBool() ? 1 : -1);
            spiralAngle = rand.NextFloat(MathHelper.TwoPi);
            spiralRadius = rand.NextFloat(30f, 60f);

            TintColor = new Color(100 + rand.Next(50), 200 + rand.Next(55), 255);
        }

        public void Update(Vector2 playerCenter) {
            Life++;
            LifeProgress = Life / MaxLife;

            //阶段性运动
            if (LifeProgress < 0.6f) {
                //阶段1：向玩家中心高速冲刺 + 螺旋
                spiralAngle += rotationSpeed;
                Vector2 spiralOffset = new Vector2(
                    (float)Math.Cos(spiralAngle) * spiralRadius * (1f - LifeProgress),
                    (float)Math.Sin(spiralAngle) * spiralRadius * (1f - LifeProgress)
                );

                Vector2 toTarget = (playerCenter - Position).SafeNormalize(Vector2.Zero);
                Velocity = Vector2.Lerp(Velocity, (toTarget * 20f) + spiralOffset * 0.3f, 0.15f);
            }
            else {
                //阶段2：环绕玩家快速旋转
                float orbitAngle = LifeProgress * MathHelper.TwoPi * 3f + spiralAngle;
                float orbitRadius = 50f * (1f - (LifeProgress - 0.6f) / 0.4f);
                Vector2 orbitPos = playerCenter + orbitAngle.ToRotationVector2() * orbitRadius;
                Velocity = (orbitPos - Position) * 0.3f;
            }

            Position += Velocity;
            Frame += 0.4f;

            TrailPositions.Insert(0, Position);
            if (TrailPositions.Count > MaxTrailLength) {
                TrailPositions.RemoveAt(TrailPositions.Count - 1);
            }
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void DrawTrail(float globalAlpha) {
            if (TrailPositions.Count < 2) return;
            Texture2D tex = VaultAsset.placeholder2.Value;

            for (int i = 0; i < TrailPositions.Count - 1; i++) {
                float progress = i / (float)TrailPositions.Count;
                float trailAlpha = (1f - progress) * globalAlpha * (1f - LifeProgress) * 0.8f;
                float width = Scale * (5f - progress * 3f);

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

            float fadeAlpha = globalAlpha * (1f - LifeProgress * 0.5f);
            Color c = TintColor * fadeAlpha;

            //发光效果
            for (int i = 0; i < 4; i++) {
                Vector2 offset = (i * MathHelper.PiOver2).ToRotationVector2() * 3f;
                Main.spriteBatch.Draw(fishTex, Position + offset - Main.screenPosition, rect,
                    c * 0.5f, rot, origin, Scale * 0.8f, effects, 0f);
            }

            Main.spriteBatch.Draw(fishTex, Position - Main.screenPosition, rect, c, rot, origin, Scale, effects, 0f);
        }
    }
    #endregion

    internal class RestartPlayer : ModPlayer
    {
        public override bool PreKill(double damage, int hitDirection, bool pvp
            , ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (Player.CountProjectilesOfID<RestartEffectProj>() > 0) {
                return false; //正在重启，阻止死亡
            }
            return true;
        }
    }

    internal class RestartEffectProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private List<RestartFishBoid> fishSwarms;
        private enum RestartState { Gathering, Wrapping, Restarting, Dispersing }
        private RestartState currentState = RestartState.Gathering;
        private int stateTimer = 0;
        private const int GatherDuration = 40;
        private const int WrapDuration = 30;
        private const int RestartDuration = 20;
        private const int DisperseDuration = 30;
        private float effectAlpha = 0f;
        private float restartFlashIntensity = 0f;
        private int particleTimer = 0;

        //特效纹理叠加
        private float shockwaveRadius = 0f;
        private float shockwaveAlpha = 0f;
        private readonly List<EnergyRing> energyRings = new();

        public override void SetDefaults() {
            Projectile.width = 400;
            Projectile.height = 400;
            Projectile.timeLeft = GatherDuration + WrapDuration + RestartDuration + DisperseDuration;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
        }

        public override void AI() {
            if (!Owner.active) { Projectile.Kill(); return; }

            Projectile.Center = Owner.Center;
            stateTimer++;

            switch (currentState) {
                case RestartState.Gathering:
                    UpdateGathering();
                    break;
                case RestartState.Wrapping:
                    UpdateWrapping();
                    break;
                case RestartState.Restarting:
                    UpdateRestarting();
                    break;
                case RestartState.Dispersing:
                    UpdateDispersing();
                    break;
            }

            //更新鱼群
            if (fishSwarms != null) {
                for (int i = fishSwarms.Count - 1; i >= 0; i--) {
                    fishSwarms[i].Update(Owner.Center);
                    if (fishSwarms[i].ShouldRemove()) {
                        fishSwarms.RemoveAt(i);
                    }
                }
            }

            //更新能量环
            for (int i = energyRings.Count - 1; i >= 0; i--) {
                energyRings[i].Update();
                if (energyRings[i].ShouldRemove()) {
                    energyRings.RemoveAt(i);
                }
            }
        }

        private void UpdateGathering() {
            float progress = stateTimer / (float)GatherDuration;
            effectAlpha = MathHelper.Clamp(progress, 0f, 1f);

            if (stateTimer == 1) {
                InitializeFishSwarms();
                SoundEngine.PlaySound(SoundID.Item8, Owner.Center); //召唤音效
            }

            //生成聚集粒子
            particleTimer++;
            if (particleTimer % 3 == 0) {
                SpawnGatherParticle();
            }

            if (stateTimer >= GatherDuration) {
                currentState = RestartState.Wrapping;
                stateTimer = 0;
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse, Owner.Center);
            }
        }

        private void UpdateWrapping() {
            effectAlpha = 1f;

            //生成包裹效果
            particleTimer++;
            if (particleTimer % 2 == 0) {
                SpawnWrapParticle();
            }

            //生成能量环
            if (stateTimer % 8 == 0) {
                energyRings.Add(new EnergyRing(Owner.Center));
            }

            if (stateTimer >= WrapDuration) {
                currentState = RestartState.Restarting;
                stateTimer = 0;
                SoundEngine.PlaySound(SoundID.Item4, Owner.Center); //爆炸音效
                SoundEngine.PlaySound(SoundID.Item29, Owner.Center); //恢复音效
            }
        }

        private void UpdateRestarting() {
            float progress = stateTimer / (float)RestartDuration;

            //闪光效果
            restartFlashIntensity = (float)Math.Sin(progress * MathHelper.Pi) * 2f;

            //冲击波扩散
            shockwaveRadius = progress * 300f;
            shockwaveAlpha = (1f - progress) * 1.5f;

            //执行重启效果
            if (stateTimer == 5) {
                ExecuteRestart();
            }

            //密集粒子爆发
            if (stateTimer < 10) {
                for (int i = 0; i < 3; i++) {
                    SpawnRestartParticle();
                }
            }

            if (stateTimer >= RestartDuration) {
                currentState = RestartState.Dispersing;
                stateTimer = 0;
            }
        }

        private void UpdateDispersing() {
            float progress = stateTimer / (float)DisperseDuration;
            effectAlpha = 1f - MathHelper.Clamp(progress, 0f, 1f);
            restartFlashIntensity *= 0.9f;

            if (stateTimer % 4 == 0) {
                SpawnDisperseParticle();
            }

            if (stateTimer >= DisperseDuration) {
                Projectile.Kill();
            }
        }

        private void InitializeFishSwarms() {
            fishSwarms = new List<RestartFishBoid>();
            int fishCount = 150; //大量鱼群

            for (int i = 0; i < fishCount; i++) {
                //从屏幕四周生成
                Vector2 spawnPos;
                float side = Main.rand.NextFloat(4f);
                if (side < 1f) { //上方
                    spawnPos = Owner.Center + new Vector2(Main.rand.NextFloat(-400, 400), -600);
                }
                else if (side < 2f) { //下方
                    spawnPos = Owner.Center + new Vector2(Main.rand.NextFloat(-400, 400), 600);
                }
                else if (side < 3f) { //左侧
                    spawnPos = Owner.Center + new Vector2(-600, Main.rand.NextFloat(-400, 400));
                }
                else { //右侧
                    spawnPos = Owner.Center + new Vector2(600, Main.rand.NextFloat(-400, 400));
                }

                fishSwarms.Add(new RestartFishBoid(spawnPos, Owner.Center));
            }
        }

        private void ExecuteRestart() {
            //满血
            Owner.Heal(Owner.statLifeMax2);

            //清除所有buff
            for (int i = 0; i < Player.MaxBuffs; i++) {
                Owner.DelBuff(i);
            }

            Owner.SetResurrectionValue(0);//复苏进度归零

            //生成大量恢复粒子
            for (int i = 0; i < 50; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Owner.Center + angle.ToRotationVector2() * Main.rand.NextFloat(100f);
                int dust = Dust.NewDust(pos, 1, 1, DustID.HealingPlus, 0, 0, 0, default, 2f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (Owner.Center - pos).SafeNormalize(Vector2.Zero) * 5f;
            }
        }

        private void SpawnGatherParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Owner.Center + angle.ToRotationVector2() * Main.rand.NextFloat(200f, 400f);
            int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(100, 200, 255), 1.5f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = (Owner.Center - pos).SafeNormalize(Vector2.Zero) * 6f;
        }

        private void SpawnWrapParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float dist = Main.rand.NextFloat(50f, 100f);
            Vector2 pos = Owner.Center + angle.ToRotationVector2() * dist;
            int dust = Dust.NewDust(pos, 1, 1, DustID.DungeonSpirit, 0, 0, 120, new Color(120, 220, 255), 1.2f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 3f;
        }

        private void SpawnRestartParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Owner.Center + Main.rand.NextVector2Circular(30, 30);
            int dustType = Main.rand.NextBool() ? DustID.Electric : DustID.BlueFairy;
            int dust = Dust.NewDust(pos, 1, 1, dustType, 0, 0, 0, default, 2.5f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 15f);
        }

        private void SpawnDisperseParticle() {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Owner.Center + Main.rand.NextVector2Circular(50, 50);
            int dust = Dust.NewDust(pos, 1, 1, DustID.Water, 0, 0, 100, new Color(150, 220, 255), 1.3f);
            Main.dust[dust].noGravity = true;
            Main.dust[dust].velocity = angle.ToRotationVector2() * 5f;
        }

        public override bool PreDraw(ref Color lightColor) {
            //绘制冲击波
            if (shockwaveAlpha > 0f) {
                DrawShockwave();
            }

            //绘制能量环
            foreach (var ring in energyRings) {
                ring.Draw(effectAlpha);
            }

            //绘制重启闪光
            if (restartFlashIntensity > 0f) {
                DrawRestartFlash();
            }

            //绘制鱼群拖尾
            if (fishSwarms != null) {
                foreach (var fish in fishSwarms) {
                    fish.DrawTrail(effectAlpha);
                }
            }

            //绘制鱼群主体
            if (fishSwarms != null) {
                foreach (var fish in fishSwarms) {
                    fish.Draw(effectAlpha);
                }
            }
            return false;
        }

        private void DrawShockwave() {
            Texture2D tex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            int segments = 80;
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++) {
                float angle = i * angleStep;
                Vector2 pos = Owner.Center + angle.ToRotationVector2() * shockwaveRadius;
                float wave = (float)Math.Sin(angle * 4f + Main.GlobalTimeWrappedHourly * 10f) * 10f;
                pos += angle.ToRotationVector2() * wave;

                Color c = new Color(100, 220, 255, 0) * shockwaveAlpha * 0.6f;
                Main.spriteBatch.Draw(tex, pos - Main.screenPosition, null, c, angle,
                    tex.Size() / 2f, 2f, SpriteEffects.None, 0f);
            }
        }

        private void DrawRestartFlash() {
            Texture2D bloomTex = CWRAsset.StarTexture.Value;
            float flashScale = 5f + restartFlashIntensity * 3f;
            Color flashColor = new Color(150, 230, 255, 0) * restartFlashIntensity * 0.5f;

            Main.spriteBatch.Draw(bloomTex, Owner.Center - Main.screenPosition, null, flashColor,
                0f, bloomTex.Size() / 2f, flashScale, SpriteEffects.None, 0f);

            Main.spriteBatch.Draw(bloomTex, Owner.Center - Main.screenPosition, null, flashColor,
                MathHelper.PiOver2, bloomTex.Size() / 2f, flashScale * 0.8f, SpriteEffects.None, 0f);

            //额外的光芒扩散
            Main.spriteBatch.Draw(bloomTex, Owner.Center - Main.screenPosition, null, flashColor * 0.6f,
                MathHelper.PiOver4, bloomTex.Size() / 2f, flashScale * 1.2f, SpriteEffects.None, 0f);
        }
    }

    #region 能量环效果
    internal class EnergyRing
    {
        public Vector2 Center;
        public float Radius;
        public float Life;
        public float MaxLife;
        public float RotationSpeed;
        private float rotation;

        public EnergyRing(Vector2 center) {
            Center = center;
            Radius = 40f;
            Life = 0f;
            MaxLife = 60f;
            RotationSpeed = Main.rand.NextFloat(0.05f, 0.1f) * (Main.rand.NextBool() ? 1 : -1);
            rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public void Update() {
            Life++;
            float progress = Life / MaxLife;
            Radius = 40f + progress * 120f;
            rotation += RotationSpeed;
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public void Draw(float globalAlpha) {
            float progress = Life / MaxLife;
            float alpha = (1f - progress) * globalAlpha * 0.7f;
            if (alpha < 0.01f) return;

            Texture2D tex = CWRAsset.StarTexture.Value;
            int segments = 24;
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++) {
                float angle = i * angleStep + rotation;
                Vector2 pos = Center + angle.ToRotationVector2() * Radius;
                float scale = 0.8f + (float)Math.Sin(angle * 3f + Main.GlobalTimeWrappedHourly * 5f) * 0.3f;
                Color c = new Color(120, 230, 255, 0) * alpha;

                Main.spriteBatch.Draw(tex, pos - Main.screenPosition, null, c, angle,
                    tex.Size() / 2f, scale * 0.5f, SpriteEffects.None, 0f);
            }
        }
    }
    #endregion
}
