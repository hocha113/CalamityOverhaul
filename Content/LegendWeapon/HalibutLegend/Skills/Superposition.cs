using CalamityMod.Items.Weapons.Ranged; // 引用大比目鱼炮贴图
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
    internal static class Superposition
    {
        public static int ID = 6;
        private const int ToggleCD = 30;
        private const int SuperpositionCooldown = 1800; // 30秒终极技能冷却

        public static void AltUse(Item item, Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.SuperpositionToggleCD > 0 || hp.SuperpositionCooldown > 0) return;
            Activate(player);
            hp.SuperpositionToggleCD = ToggleCD;
            hp.SuperpositionCooldown = 60;//调试用
        }

        public static void Activate(Player player) {
            if (Main.myPlayer == player.whoAmI) {
                SpawnSuperpositionEffect(player);
            }
        }

        internal static void SpawnSuperpositionEffect(Player player) {
            var source = player.GetSource_Misc("SuperpositionSkill");
            Projectile.NewProjectile(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<SuperpositionProj>(), 0, 0, player.whoAmI);
        }
    }

    #region 时空克隆体
    internal class TimeClone
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 SpawnPos;
        public float Alpha;
        public float Life;
        public float MaxLife;
        public float Scale;
        public PlayerSnapshot Snapshot;
        public readonly List<Vector2> TrailPositions = new();
        private const int MaxTrailLength = 28;
        private float spiralAngle;
        private readonly float timeWarpFactor;
        private float orbitRadius;
        private bool converging;

        public TimeClone(Vector2 spawnPos, PlayerSnapshot snapshot, float startOrbitRadius) {
            Position = spawnPos;
            SpawnPos = spawnPos;
            Snapshot = snapshot;
            Velocity = Vector2.Zero;
            Life = 0f;
            MaxLife = 140f; // 更长展示期
            Alpha = 0f;
            Scale = 0.75f;
            spiralAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            timeWarpFactor = Main.rand.NextFloat(0.8f, 1.25f);
            orbitRadius = startOrbitRadius; // 初始环绕半径
        }

        public void SetConverging() => converging = true;

        public void Update(Vector2 center, float gatherProgress, float convergeProgress) {
            Life++;
            float targetRadius = converging
                ? MathHelper.Lerp(orbitRadius, 0f, MathHelper.SmoothStep(0f, 1f, convergeProgress))
                : MathHelper.Lerp(orbitRadius * 1.15f, orbitRadius * 0.85f, (float)Math.Sin(gatherProgress * MathHelper.Pi));
            orbitRadius = MathHelper.Lerp(orbitRadius, targetRadius, converging ? 0.18f : 0.05f);
            spiralAngle += 0.07f * timeWarpFactor + (converging ? 0.12f : 0f);
            Vector2 targetPos = center + spiralAngle.ToRotationVector2() * orbitRadius;
            Vector2 toTarget = (targetPos - Position);
            Velocity = Vector2.Lerp(Velocity, toTarget * (converging ? 0.25f : 0.18f), 0.4f);
            Position += Velocity;
            if (!converging) Alpha = MathHelper.Clamp(gatherProgress * 1.6f, 0f, 1f); else Alpha = (float)Math.Pow(1f - convergeProgress, 0.6f);
            TrailPositions.Insert(0, Position);
            if (TrailPositions.Count > MaxTrailLength) TrailPositions.RemoveAt(TrailPositions.Count - 1);
        }

        public bool ShouldRemove() => Life >= MaxLife || (converging && orbitRadius < 4f && Alpha < 0.05f);

        public void DrawTrail(float globalAlpha) {
            if (TrailPositions.Count < 3) return;
            Texture2D tex = TextureAssets.MagicPixel.Value;
            for (int i = 0; i < TrailPositions.Count - 1; i++) {
                float p = i / (float)TrailPositions.Count;
                float trailAlpha = (1f - p) * Alpha * globalAlpha * 0.55f;
                Vector2 a = TrailPositions[i];
                Vector2 b = TrailPositions[i + 1];
                Vector2 d = b - a;
                float len = d.Length();
                if (len < 0.01f) continue;
                float rot = d.ToRotation();
                Color c = new Color(170, 120, 255, 0) * trailAlpha;
                Main.spriteBatch.Draw(tex, a - Main.screenPosition, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, 6f - p * 4f), SpriteEffects.None, 0f);
            }
        }
    }
    #endregion

    #region 法阵符环
    internal class RuneCircle
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
        public RuneCircle(float startR, float endR, int life, bool shrink, Color a, Color b) {
            StartRadius = startR; EndRadius = endR; MaxLife = life; Life = 0; Shrink = shrink; Rotation = Main.rand.NextFloat(MathHelper.TwoPi); RotSpeed = Main.rand.NextFloat(-0.05f, 0.05f); EllipseFactor = Main.rand.NextFloat(0.6f, 1.15f); ColorA = a; ColorB = b;
        }
        public void Update() { Life++; Rotation += RotSpeed; }
        public bool Dead => Life >= MaxLife;
        public void Draw(Vector2 center, float alpha) {
            float p = Life / MaxLife;
            float radius = Shrink ? MathHelper.Lerp(StartRadius, EndRadius, p) : MathHelper.Lerp(StartRadius, EndRadius, (float)Math.Sin(p * MathHelper.Pi));
            float fade = (float)Math.Sin(p * MathHelper.Pi) * alpha;
            if (fade <= 0.01f) return;
            Texture2D pix = TextureAssets.MagicPixel.Value;
            int seg = 120; float step = MathHelper.TwoPi / seg;
            for (int i = 0; i < seg; i++) {
                float ang1 = Rotation + i * step; float ang2 = Rotation + (i + 1) * step;
                Vector2 p1 = center + new Vector2((float)Math.Cos(ang1) * radius, (float)Math.Sin(ang1) * radius * EllipseFactor);
                Vector2 p2 = center + new Vector2((float)Math.Cos(ang2) * radius, (float)Math.Sin((ang2)) * radius * EllipseFactor);
                Vector2 d = p2 - p1; float len = d.Length(); if (len < 0.0001f) continue; float rot = d.ToRotation();
                float wave = (float)Math.Sin(ang1 * 6f + Main.GlobalTimeWrappedHourly * 8f) * 0.5f + 0.5f;
                Color c = Color.Lerp(ColorA, ColorB, wave) * fade * 0.6f;
                Main.spriteBatch.Draw(pix, p1 - Main.screenPosition, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, 2f), SpriteEffects.None, 0f);
            }
        }
    }
    #endregion

    internal class SuperpositionProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private List<TimeClone> timeClones;
        private List<RuneCircle> runeCircles = new();
        private List<int> cannonProjIds = new(); // 记录炮阵弹幕ID
        private bool cannonsSpawned;
        private enum SuperpositionState { Gathering, Converging, Charging, Launching, Exploding }
        private SuperpositionState currentState = SuperpositionState.Gathering;
        private int stateTimer = 0;
        private const int GatherDuration = 60;
        private const int ConvergeDuration = 45;
        private const int ChargeDuration = 36;
        private const int LaunchDuration = 180; // 发射阶段上限（若炮阵提前完成会提前进入Explode）
        private const int ExplodeDuration = 40;
        private float effectAlpha = 0f;
        private float chargeIntensity = 0f; // 作为进度控制
        private Vector2 attackDirection = Vector2.UnitX;

        public override void SetDefaults() {
            Projectile.width = 900;
            Projectile.height = 900;
            Projectile.timeLeft = GatherDuration + ConvergeDuration + ChargeDuration + LaunchDuration + ExplodeDuration + 30;
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
                case SuperpositionState.Gathering: UpdateGathering(); break;
                case SuperpositionState.Converging: UpdateConverging(); break;
                case SuperpositionState.Charging: UpdateCharging(); break;
                case SuperpositionState.Launching: UpdateLaunching(); break;
                case SuperpositionState.Exploding: UpdateExploding(); break;
            }
            UpdateLists();
        }

        private void UpdateLists() {
            if (timeClones != null) {
                float gatherProgress = currentState == SuperpositionState.Gathering ? stateTimer / (float)GatherDuration : 1f;
                float convergeProgress = currentState == SuperpositionState.Converging ? stateTimer / (float)ConvergeDuration : (currentState > SuperpositionState.Converging ? 1f : 0f);
                foreach (var c in timeClones) { if (currentState == SuperpositionState.Converging) c.SetConverging(); c.Update(Owner.Center, gatherProgress, convergeProgress); }
                timeClones.RemoveAll(c => c.ShouldRemove());
            }
            foreach (var r in runeCircles) r.Update(); runeCircles.RemoveAll(r => r.Dead);
        }

        private void UpdateGathering() {
            float p = stateTimer / (float)GatherDuration; effectAlpha = MathHelper.Clamp(p * 1.3f, 0f, 1f);
            if (stateTimer == 1) { InitializeTimeClones(); SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen, Owner.Center); }
            if (stateTimer % 12 == 0) runeCircles.Add(new RuneCircle(260, 300, 50, false, new Color(120, 90, 210), new Color(200, 150, 255)));
            if (stateTimer >= GatherDuration) { currentState = SuperpositionState.Converging; stateTimer = 0; }
        }
        private void UpdateConverging() {
            if (stateTimer % 10 == 0) runeCircles.Add(new RuneCircle(220, 120, 40, true, new Color(160, 110, 240), new Color(230, 200, 255)));
            if (stateTimer >= ConvergeDuration) { currentState = SuperpositionState.Charging; stateTimer = 0; attackDirection = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX); }
        }
        private void UpdateCharging() {
            float p = stateTimer / (float)ChargeDuration; chargeIntensity = p; effectAlpha = 1f;
            if (stateTimer == 1) { SoundEngine.PlaySound(SoundID.Item72 with { Volume = 1.1f }, Owner.Center); }
            if (stateTimer % 6 == 0) runeCircles.Add(new RuneCircle(140, 210, 32, false, new Color(180, 130, 255), new Color(255, 255, 255)));
            if (stateTimer >= ChargeDuration) { currentState = SuperpositionState.Launching; stateTimer = 0; SpawnCannons(); }
        }
        private void UpdateLaunching() {
            // 若炮阵尚未生成，立即生成（安全）
            if (!cannonsSpawned) SpawnCannons();
            // 监控炮阵状态
            bool allDone = true;
            for (int i = cannonProjIds.Count - 1; i >= 0; i--) {
                int id = cannonProjIds[i];
                if (id < 0 || id >= Main.maxProjectiles || !Main.projectile[id].active) continue;
                if (Main.projectile[id].ModProjectile is SuperpositionCannon c) {
                    if (!c.Completed) { allDone = false; }
                }
            }
            if (allDone || stateTimer >= LaunchDuration) { currentState = SuperpositionState.Exploding; stateTimer = 0; SoundEngine.PlaySound(SoundID.Item14, Owner.Center); }
        }
        private void UpdateExploding() {
            float p = stateTimer / (float)ExplodeDuration; effectAlpha = 1f - p;
            if (stateTimer == 1) runeCircles.Add(new RuneCircle(200, 30, 40, true, new Color(200, 150, 255), new Color(255, 255, 255)));
            if (stateTimer >= ExplodeDuration) Projectile.Kill();
        }

        private void InitializeTimeClones() {
            timeClones = new List<TimeClone>();
            int cloneCount = 26; float outerRing = 420f;
            for (int i = 0; i < cloneCount; i++) {
                float edge = Main.rand.NextFloat(4f); Vector2 spawn;
                if (edge < 1f) spawn = Owner.Center + new Vector2(Main.rand.NextFloat(-600, 600), -800);
                else if (edge < 2f) spawn = Owner.Center + new Vector2(Main.rand.NextFloat(-600, 600), 800);
                else if (edge < 3f) spawn = Owner.Center + new Vector2(-800, Main.rand.NextFloat(-600, 600));
                else spawn = Owner.Center + new Vector2(800, Main.rand.NextFloat(-600, 600));
                timeClones.Add(new TimeClone(spawn, new PlayerSnapshot(Owner), outerRing));
            }
        }

        private void SpawnCannons() {
            cannonsSpawned = true;
            cannonProjIds.Clear();
            var source = Owner.GetSource_Misc("SuperpositionCannons");
            int cannonCount = 7; // 中心 + 3/3 扇形
            Vector2 backDir = -attackDirection;
            Vector2 perp = attackDirection.RotatedBy(MathHelper.PiOver2);
            float arc = MathHelper.ToRadians(70f);
            for (int i = 0; i < cannonCount; i++) {
                float lerp = (cannonCount == 1) ? 0.5f : i / (float)(cannonCount - 1);
                float angleOff = (lerp - 0.5f) * arc;
                Vector2 offsetDir = backDir.RotatedBy(angleOff);
                Vector2 pos = Owner.Center + backDir * 180f + perp * (float)Math.Sin(angleOff) * 40f + offsetDir * 20f;
                int id = Projectile.NewProjectile(source, pos, Vector2.Zero, ModContent.ProjectileType<SuperpositionCannon>(), Owner.HeldItem.damage, 4f, Owner.whoAmI, angleOff, 0);
                if (id >= 0) cannonProjIds.Add(id);
            }
        }

        private void DrawTimeClone(TimeClone clone) {
            if (clone.Alpha < 0.05f) return;
            Player ghost = new Player(); ghost.ResetEffects(); ghost.CopyVisuals(Owner);
            ghost.position = clone.Position - Owner.Size * 0.5f; ghost.direction = Owner.direction;
            ghost.bodyFrame = Owner.bodyFrame; ghost.legFrame = Owner.legFrame;
            Color ghostColor = new Color(170, 130, 255) * clone.Alpha * 0.9f;
            ghost.skinColor = ghostColor; ghost.shirtColor = ghostColor; ghost.underShirtColor = ghostColor; ghost.pantsColor = ghostColor; ghost.shoeColor = ghostColor; ghost.hairColor = ghostColor; ghost.eyeColor = ghostColor;
            try { Main.PlayerRenderer.DrawPlayer(Main.Camera, ghost, ghost.position, 0f, ghost.fullRotationOrigin); } catch { }
        }

        public override bool PreDraw(ref Color lightColor) {
            foreach (var rc in runeCircles) rc.Draw(Owner.Center, effectAlpha);
            if (timeClones != null) foreach (var c in timeClones) c.DrawTrail(effectAlpha);
            if (timeClones != null) { foreach (var c in timeClones) { DrawTimeClone(c); } }
            return false;
        }
    }

    #region 齐射炮弹幕
    internal class SuperpositionCannon : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private Player Owner => Main.player[Projectile.owner];
        private enum CannonState { Deploy, Charge, Volley, Finish }
        private CannonState state = CannonState.Deploy;
        private int timer;
        private int volleyIndex;
        private const int DeployTime = 20;
        private const int ChargeTime = 30;
        private const int VolleyCount = 4;
        private const int VolleySpacing = 12;
        private float angleOffset; // 存放在 ai[0]
        private float pulse;
        public bool Completed => state == CannonState.Finish;

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 400;
        }

        public override void AI() {
            if (!Owner.active) { Projectile.Kill(); return; }
            if (timer == 0) { angleOffset = Projectile.ai[0]; }
            timer++;
            Vector2 dir = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 backDir = -dir;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            // 位置跟随玩家（保持相对队形）
            Vector2 basePos = Owner.Center + backDir * -80f; // 基准
            Vector2 offset = backDir.RotatedBy(angleOffset) * 20f + perp * (float)Math.Sin(angleOffset) * 40f;
            Projectile.Center = Vector2.Lerp(Projectile.Center, basePos + offset, 0.15f);

            Owner.direction = Math.Sign(dir.X);

            switch (state) {
                case CannonState.Deploy:
                    pulse = timer / (float)DeployTime;
                    if (timer >= DeployTime) { state = CannonState.Charge; timer = 0; SoundEngine.PlaySound(SoundID.Item34 with { Pitch = -0.5f }, Projectile.Center); }
                    break;
                case CannonState.Charge:
                    pulse = (float)Math.Sin(timer / (float)ChargeTime * MathHelper.Pi);
                    if (timer % 6 == 0) { // 充能粒子
                        int d = Dust.NewDust(Projectile.Center - new Vector2(8, 8), 16, 16, DustID.Water, 0, 0, 150, new Color(160, 200, 255), 1.3f);
                        Main.dust[d].noGravity = true; Main.dust[d].velocity = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * -2f + Main.rand.NextVector2Circular(1f, 1f);
                    }
                    if (timer >= ChargeTime) { state = CannonState.Volley; timer = 0; volleyIndex = 0; SoundEngine.PlaySound(SoundID.Item82, Projectile.Center); }
                    break;
                case CannonState.Volley:
                    pulse = 1f;
                    if (timer == 1) { FireVolley(dir); volleyIndex++; }
                    if (volleyIndex >= VolleyCount) { state = CannonState.Finish; timer = 0; }
                    else if (timer >= VolleySpacing) { timer = 0; }
                    break;
                case CannonState.Finish:
                    pulse *= 0.92f;
                    if (pulse < 0.05f) Projectile.Kill();
                    break;
            }
        }

        private void FireVolley(Vector2 forward) {
            var source = Projectile.GetSource_FromThis();
            int fishPerVolley = 10;
            float spread = MathHelper.ToRadians(18f);
            ShootState shootState = Owner.GetShootState();
            for (int i = 0; i < fishPerVolley; i++) {
                float lerp = (i + 0.5f) / fishPerVolley;
                float angle = spread * (lerp - 0.5f);
                Vector2 vel = forward.RotatedBy(angle) * Main.rand.NextFloat(14f, 18f);
                int id = Projectile.NewProjectile(source, Projectile.Center + forward * 30f, vel
                    , ModContent.ProjectileType<CannonFishShot>(), shootState.WeaponDamage, shootState.WeaponKnockback, Owner.whoAmI, Main.rand.Next(9999));
                if (id >= 0) Main.projectile[id].friendly = true;
            }
            // 环形符咒粒子
            for (int k = 0; k < 14; k++) {
                float ang = MathHelper.TwoPi * k / 14f;
                Vector2 v = ang.ToRotationVector2() * Main.rand.NextFloat(2f, 5f) + forward * 6f;
                int d = Dust.NewDust(Projectile.Center, 1, 1, DustID.Water, 0, 0, 100, new Color(180, 220, 255), 1.4f);
                Main.dust[d].noGravity = true; Main.dust[d].velocity = v;
            }
            SoundEngine.PlaySound(SoundID.Item11 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ModContent.ItemType<HalibutCannon>());
            Texture2D tex = TextureAssets.Item[ModContent.ItemType<HalibutCannon>()].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 dir = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
            SpriteEffects spriteEffects = dir.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float rot = dir.ToRotation() + (dir.X > 0 ? 0 : MathHelper.Pi);
            float recoil = (state == CannonState.Volley && timer < 4) ? -6f : 0f;
            drawPos += dir * recoil;
            float scale = 0.8f + pulse * 0.25f;
            // 发光层
            Color glow = new Color(180, 200, 255, 0) * pulse * 0.6f;
            for (int i = 0; i < 3; i++)
                Main.spriteBatch.Draw(tex, drawPos + (i * MathHelper.TwoPi / 3f).ToRotationVector2() * pulse * 4f, null, glow, rot, origin, scale, spriteEffects, 0f);
            Main.spriteBatch.Draw(tex, drawPos, null, Color.White, rot, origin, scale, spriteEffects, 0f);
            return false;
        }
    }

    internal class CannonFishShot : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        private float seed;
        private bool init;
        private float alpha;
        private int fishType; // 主鱼类型
        private float fishScale;
        private float fishRotation;
        private int fishDirection;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 120;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            if (!init) {
                init = true;
                seed = Projectile.ai[0];
                alpha = 0f;
                fishType = Main.rand.Next(3); // 0=Tuna, 1=Bass, 2=Trout
                fishScale = 0.6f + Main.rand.NextFloat() * 0.3f;
                fishDirection = Projectile.velocity.X > 0 ? 1 : -1;
            }
            alpha = Math.Min(1f, alpha + 0.1f);

            // 轻微波动+群聚趋向
            Projectile.velocity *= 0.998f;
            Vector2 waveOffset = new Vector2(0, (float)Math.Sin((Projectile.timeLeft + seed) * 0.2f)) * 0.06f;
            Projectile.velocity += waveOffset;

            // 简化群聚：向最近的同类射弹靠拢
            Vector2 cohesion = Vector2.Zero;
            int nearCount = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (i == Projectile.whoAmI) continue;
                Projectile other = Main.projectile[i];
                if (!other.active || other.type != Projectile.type || other.owner != Projectile.owner) continue;
                float dist = Vector2.Distance(Projectile.Center, other.Center);
                if (dist < 100f && dist > 0.1f) {
                    cohesion += (other.Center - Projectile.Center).SafeNormalize(Vector2.Zero) / dist;
                    nearCount++;
                }
            }
            if (nearCount > 0) {
                cohesion /= nearCount;
                Projectile.velocity += cohesion * 0.15f;
            }

            // 更新朝向
            if (Math.Abs(Projectile.velocity.X) > 0.5f) {
                fishDirection = Projectile.velocity.X > 0 ? 1 : -1;
            }
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                fishRotation = Projectile.velocity.ToRotation();
            }

            // 轨迹粒子
            if (Main.rand.NextBool(4)) {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0, 0, 150, new Color(150, 210, 255), 1.0f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = -Projectile.velocity * 0.3f;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 击中粒子
            for (int i = 0; i < 3; i++) {
                int d = Dust.NewDust(target.position, target.width, target.height, DustID.Water, 0, 0, 150, new Color(140, 210, 255), 1.2f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.4f;
            }
        }

        public override void OnKill(int timeLeft) {
            // 死亡爆散
            for (int i = 0; i < 8; i++) {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0, 0, 120, new Color(160, 220, 255), 1.4f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = Main.rand.NextVector2Circular(3f, 3f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int itemType = fishType switch { 0 => ItemID.Tuna, 1 => ItemID.Bass, 2 => ItemID.Trout, _ => ItemID.Tuna };
            Main.instance.LoadItem(itemType);
            Texture2D fishTex = TextureAssets.Item[itemType].Value;

            // 拖尾
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float trailProgress = i / (float)Projectile.oldPos.Length;
                float trailAlpha = alpha * (1f - trailProgress) * 0.5f;
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                Rectangle rect = fishTex.Bounds;
                Vector2 origin = rect.Size() * 0.5f;
                SpriteEffects effects = fishDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
                float rot = fishRotation + (fishDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);

                Main.spriteBatch.Draw(fishTex, trailPos, rect, new Color(140, 200, 255) * trailAlpha, rot, origin, fishScale * 0.8f * (1f - trailProgress * 0.3f), effects, 0f);
            }

            // 主体鱼
            {
                Rectangle rect = fishTex.Bounds;
                Vector2 origin = rect.Size() * 0.5f;
                SpriteEffects effects = fishDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
                float rot = fishRotation + (fishDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
                Vector2 drawPos = Projectile.Center - Main.screenPosition;
                Main.spriteBatch.Draw(fishTex, drawPos, rect, Color.White * alpha, rot, origin, fishScale, effects, 0f);
            }
            return false;
        }
    }
    #endregion

    #region 时空裂隙 (精简视觉占比)
    internal class TimeRift
    {
        public Vector2 Position; public float Life; public float MaxLife; public float Rotation; public float Scale;
        public TimeRift(Vector2 pos) { Position = pos; Life = 0; MaxLife = Main.rand.NextFloat(50f, 90f); Rotation = Main.rand.NextFloat(MathHelper.TwoPi); Scale = Main.rand.NextFloat(0.6f, 1.3f); }
        public void Update() { Life++; Rotation += 0.04f; }
        public bool ShouldRemove() => Life >= MaxLife;
        public void Draw(Vector2 c) { } // 旧绘制弃用
    }
    #endregion

    #region 能量球 (保留简化)
    internal class EnergyOrb
    {
        public Vector2 Position; public Vector2 Velocity; public float Life; public float MaxLife; public float Scale;
        public EnergyOrb(Vector2 pos) { Position = pos; Velocity = Vector2.Zero; Life = 0; MaxLife = 70f; Scale = Main.rand.NextFloat(0.4f, 0.9f); }
        public void Update(Vector2 target) { Life++; Vector2 to = (target - Position).SafeNormalize(Vector2.Zero); Velocity = Vector2.Lerp(Velocity, to * 14f, 0.12f); Position += Velocity; }
        public bool ShouldRemove() => Life >= MaxLife;
    }
    #endregion
}
