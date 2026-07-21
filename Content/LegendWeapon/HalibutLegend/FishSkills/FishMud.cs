using CalamityOverhaul.Common;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishMud : FishSkill
    {
        public override int UnlockFishID => ItemID.Mudfish;
        public override int DefaultCooldown => 180 - HalibutData.GetDomainLayer() * 12;
        public override int ResearchDuration => 60 * 16;
        private static int MaxMudfishSentries => 1 + HalibutData.GetDomainLayer() / 2;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (!Active(player)) {
                return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
            }

            if (Cooldown <= 0) {
                SetCooldown();
                int existingCount = player.CountProjectilesOfID<MudfishSentry>();
                int maxCount = MaxMudfishSentries + HalibutData.GetDomainLayer() * 2;

                if (existingCount < maxCount) {
                    SpawnMudfishSentry(player, source, damage, knockback);
                }
            }

            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        private void SpawnMudfishSentry(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            NPC target = player.Center.FindClosestNPC(1200f);
            if (target == null) return;

            Vector2 spawnPos = FindValidGroundPosition(player, target);
            if (spawnPos == Vector2.Zero) {
                spawnPos = player.Bottom;
            }

            Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<MudfishSentry>(),
                (int)(damage * (1.8f + HalibutData.GetDomainLayer() * 0.45f)),
                knockback * 0.8f,
                player.whoAmI,
                0
            );
        }

        private static Vector2 FindValidGroundPosition(Player player, NPC target) {
            Vector2 targetPos = target.Center;
            Vector2 dirToTarget = (targetPos - player.Center).SafeNormalize(Vector2.Zero);

            for (int attempt = 0; attempt < 10; attempt++) {
                float distance = Main.rand.NextFloat(200f, 400f);
                float angleOffset = Main.rand.NextFloat(-0.8f, 0.8f);
                Vector2 testDir = dirToTarget.RotatedBy(angleOffset);
                Vector2 testPos = player.Center + testDir * distance;

                for (int y = 0; y < 50; y++) {
                    Vector2 checkPos = testPos + new Vector2(0, y * 16);
                    Point tilePos = checkPos.ToTileCoordinates();

                    if (WorldGen.InWorld(tilePos.X, tilePos.Y)) {
                        Tile tile = Main.tile[tilePos.X, tilePos.Y];
                        if (tile.HasSolidTile()) {
                            return new Vector2(checkPos.X, tilePos.Y * 16 - 16);
                        }
                    }
                }
            }

            return Vector2.Zero;
        }
    }

    /// <summary>泥鱼哨兵，鼓包→破土→待机→回吸，根部泥堆shader盖出入场</summary>
    internal class MudfishSentry : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Mudfish;

        private enum SentryState
        {
            Rising,
            Emerging,
            Idle,
            Attacking,
            TurningDown,
            Submerging
        }

        private SentryState State {
            get => (SentryState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float TargetID => ref Projectile.ai[1];
        private ref float StateTimer => ref Projectile.ai[2];

        private float emergingProgress = 0f;
        private float bodyWiggle = 0f;
        private float mouthOpenness = 0f;
        private int attackCooldown = 0;
        private int shotsFired = 0;
        private bool isUnderground = false;
        private float targetRotation = 0f;
        private float recoilKick = 0f;      //射击后坐包络1→0
        private float wrapMud = 0f;         //破土挂泥包裹量1→0，兼做湿身高光强度
        private float burstEnvelope = 0f;   //破土爆发包络1→0
        private float moundEmerge = 0f;     //泥丘隆起量
        private float sinkPhase = 0f;       //塌陷相0..1
        private Vector2 moundAnchor;        //泥堆世界锚点，x柱心y地面线
        private Vector2 submergeStart;      //下潜起始中心，绘制锚点
        private bool anchorSet = false;
        private float vfxSeed = -1f;

        private const int RisingDuration = 20;
        private const int EmergeDuration = 30;
        private const int TelegraphFrames = 9;
        private const int IdleDuration = 40;
        private const int AttackDuration = 120;
        private const int TurningDownDuration = 20;
        private const int SubmergeDuration = 30;
        private const int AttackCooldownMax = 25;
        private const int ShotCount = 4;
        private const float RisingSpeed = 4f;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 20;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 9;
        }

        public override void AI() {
            if (vfxSeed < 0f) {
                vfxSeed = Projectile.whoAmI * 0.617f % 1f;
            }
            //中途入场的客户端补挂锚点，防止泥堆与残迹落在原点
            if (!anchorSet && State != SentryState.Rising) {
                EnsureGroundAnchor();
            }

            if (StateTimer == 0 && State == SentryState.Rising) {
                isUnderground = CheckIfUnderground();

                if (!isUnderground) {
                    State = SentryState.Emerging;
                    StateTimer = 0;
                }
            }

            StateTimer++;
            bodyWiggle += 0.12f;
            recoilKick *= 0.85f;
            burstEnvelope *= 0.88f;
            //破土挂泥缓慢干落
            if (State != SentryState.Rising && State != SentryState.Emerging) {
                wrapMud *= 0.988f;
            }

            if (Framing.GetTileSafely(Projectile.Center.ToTileCoordinates16()).HasTile) {
                Projectile.position.Y -= 8f;
            }

            //寿命将尽时强制走退场链，禁pop-out
            if (Projectile.timeLeft < TurningDownDuration + SubmergeDuration + 5
                && State is SentryState.Idle or SentryState.Attacking) {
                Projectile.timeLeft = TurningDownDuration + SubmergeDuration + 5;
                State = SentryState.TurningDown;
                StateTimer = 0;
            }

            switch (State) {
                case SentryState.Rising:
                    RisingPhase();
                    break;
                case SentryState.Emerging:
                    EmergingPhase();
                    break;
                case SentryState.Idle:
                    IdlePhase();
                    break;
                case SentryState.Attacking:
                    AttackingPhase();
                    break;
                case SentryState.TurningDown:
                    TurningDownPhase();
                    break;
                case SentryState.Submerging:
                    SubmergingPhase();
                    break;
            }

            Projectile.velocity = Vector2.Zero;
        }

        private bool CheckIfUnderground() {
            Point tilePos = Projectile.Center.ToTileCoordinates();

            for (int y = tilePos.Y; y >= tilePos.Y - 20; y--) {
                if (WorldGen.InWorld(tilePos.X, y)) {
                    Tile tile = Main.tile[tilePos.X, y];
                    if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType]) {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool IsOnSurface() {
            Point tilePos = Projectile.Center.ToTileCoordinates();

            for (int y = tilePos.Y; y >= tilePos.Y - 3; y--) {
                if (WorldGen.InWorld(tilePos.X, y)) {
                    Tile tile = Main.tile[tilePos.X, y];
                    if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType]) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>钻升途中把锚点挂到正上方最近的地表，供预告鼓包定位</summary>
        private void UpdateAnchorAboveHead() {
            Point t = Projectile.Center.ToTileCoordinates();
            for (int y = t.Y; y >= t.Y - 40 && y > 10; y--) {
                Tile tile = Framing.GetTileSafely(t.X, y);
                if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType]) {
                    moundAnchor = new Vector2(Projectile.Center.X, y * 16 + 16);
                    anchorSet = true;
                    return;
                }
            }
        }

        /// <summary>把锚点钉在正下方第一格固体的顶面</summary>
        private void EnsureGroundAnchor() {
            Point t = Projectile.Center.ToTileCoordinates();
            for (int y = t.Y; y <= t.Y + 12; y++) {
                Tile tile = Framing.GetTileSafely(t.X, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                    moundAnchor = new Vector2(Projectile.Center.X, y * 16);
                    anchorSet = true;
                    return;
                }
            }
            moundAnchor = Projectile.Bottom;
            anchorSet = true;
        }

        private void RisingPhase() {
            Projectile.Center += new Vector2(0, -RisingSpeed);
            emergingProgress = 0f;

            if (StateTimer % 4 == 0) {
                UpdateAnchorAboveHead();
            }
            //钻升预告，地表微鼓
            moundEmerge = MathHelper.Lerp(moundEmerge, 0.4f, 0.08f);

            if (anchorSet && StateTimer % 5 == 0 && !VaultUtils.isServer) {
                //被顶起的表土小跳
                PRTLoader.NewParticle<PRT_FishMudDroplet>(
                    moundAnchor + new Vector2(Main.rand.NextFloat(-18f, 18f), -2f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-2.2f, -0.8f)),
                    FishMudPalette.Mud(Main.rand.NextFloat(0.2f, 0.6f)), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(12, 20));
            }

            if (IsOnSurface() || StateTimer >= RisingDuration * 3) {
                State = SentryState.Emerging;
                StateTimer = 0;
            }
        }

        private void EmergingPhase() {
            if (StateTimer == 1) {
                EnsureGroundAnchor();
                SoundEngine.PlaySound(SoundID.WormDig with {
                    Volume = 0.6f,
                    Pitch = -0.4f
                }, Projectile.Center);
            }

            //预告拍，地面先隆起翻涌，鱼未露头
            if (StateTimer <= TelegraphFrames) {
                emergingProgress = 0f;
                moundEmerge = MathHelper.Lerp(moundEmerge, 1f, 0.34f);

                if (!VaultUtils.isServer && StateTimer % 3 == 0) {
                    PRTLoader.NewParticle<PRT_FishMudDroplet>(
                        moundAnchor + new Vector2(Main.rand.NextFloat(-22f, 22f), -4f),
                        new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2.6f, -1f)),
                        FishMudPalette.Mud(Main.rand.NextFloat(0.3f, 0.7f)), Main.rand.NextFloat(0.5f, 0.85f))
                        ?.Configure(Main.rand.Next(12, 20));
                }

                if (StateTimer == TelegraphFrames) {
                    BreachBurst();
                }
                return;
            }

            //破土上冒，湿重过冲回落
            float riseT = MathHelper.Clamp((StateTimer - TelegraphFrames) / (float)(EmergeDuration - TelegraphFrames), 0f, 1f);
            emergingProgress = EaseOutBack(riseT);
            //头朝上钻出，出土后回落到待机姿态
            Projectile.rotation = -MathHelper.PiOver2 * (1f - riseT * 0.9f);
            targetRotation = 0f;

            //挂泥滴淌，刚出土的鱼身往下甩泥
            if (!VaultUtils.isServer && StateTimer % 3 == 0) {
                PRTLoader.NewParticle<PRT_FishMudDroplet>(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 14f),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(0.2f, 1.2f)),
                    FishMudPalette.Mud(Main.rand.NextFloat(0.3f, 0.8f)), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(14, 24));
            }

            moundEmerge = MathHelper.Lerp(moundEmerge, 0.55f, 0.1f);

            if (StateTimer >= EmergeDuration) {
                State = SentryState.Idle;
                StateTimer = 0;
                emergingProgress = 1f;
            }
        }

        /// <summary>破土爆发帧，土浪翻涌+泥瓣泥点四溅+定向震屏+三层音效</summary>
        private void BreachBurst() {
            burstEnvelope = 1f;
            wrapMud = 1f;
            moundEmerge = 1f;

            if (CWRServerConfig.Instance.ScreenVibration && !Main.dedServ) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    moundAnchor, -Vector2.UnitY, 4.5f, 5f, 10, 900f, "FishMudBreach"));
            }

            SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.7f, Pitch = 0.1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.65f, Pitch = -0.5f }, Projectile.Center);

            if (VaultUtils.isServer) {
                return;
            }

            //泥瓣扇形上抛
            for (int i = 0; i < 7; i++) {
                float ang = -MathHelper.PiOver2 + MathHelper.Lerp(-0.85f, 0.85f, i / 6f) + Main.rand.NextFloat(-0.12f, 0.12f);
                PRTLoader.NewParticle<PRT_FishMudClod>(
                    moundAnchor + new Vector2(Main.rand.NextFloat(-14f, 14f), -4f),
                    ang.ToRotationVector2() * Main.rand.NextFloat(4.5f, 8.5f),
                    FishMudPalette.Mud(Main.rand.NextFloat(0.25f, 0.75f)), Main.rand.NextFloat(0.8f, 1.3f))
                    ?.Configure(Main.rand.Next(26, 40));
            }
            //泥点四溅
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-4.5f, 4.5f), Main.rand.NextFloat(-7.5f, -2.5f));
                PRTLoader.NewParticle<PRT_FishMudDroplet>(
                    moundAnchor + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f),
                    vel, FishMudPalette.Mud(Main.rand.NextFloat()), Main.rand.NextFloat(0.6f, 1.05f))
                    ?.Configure(Main.rand.Next(18, 30));
            }
            //扬尘底噪
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(
                    moundAnchor + new Vector2(Main.rand.NextFloat(-24f, 24f), -2f),
                    DustID.Mud,
                    new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-4f, -1.5f)),
                    100, new Color(90, 70, 50), Main.rand.NextFloat(1.2f, 2f));
                dust.noGravity = Main.rand.NextBool();
            }
        }

        private void IdlePhase() {
            mouthOpenness = MathHelper.Lerp(mouthOpenness, 0f, 0.15f);
            targetRotation = MathHelper.Lerp(targetRotation, 0f, 0.1f);
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetRotation, 0.15f);
            moundEmerge = MathHelper.Lerp(moundEmerge, 0.5f, 0.06f);

            NPC target = FindTarget();
            if (target != null) {
                State = SentryState.Attacking;
                StateTimer = 0;
                shotsFired = 0;
                TargetID = target.whoAmI;
                return;
            }

            if (StateTimer >= IdleDuration) {
                State = SentryState.TurningDown;
                StateTimer = 0;
            }

            //待机滴淌，湿身析出的泥珠
            if (!VaultUtils.isServer && StateTimer % 15 == 0) {
                PRTLoader.NewParticle<PRT_FishMudDroplet>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-4f, 10f)),
                    new Vector2(0f, Main.rand.NextFloat(0.3f, 0.8f)),
                    FishMudPalette.Mud(Main.rand.NextFloat(0.3f, 0.7f)), Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(14, 22), 0.22f);
            }
            //根部泥浆偶尔鼓个浊泡
            if (StateTimer % 22 == 0) {
                Dust bubble = Dust.NewDustPerfect(
                    moundAnchor + new Vector2(Main.rand.NextFloat(-16f, 16f), -6f),
                    DustID.TintableDust,
                    new Vector2(0, -0.8f), 140,
                    new Color(96, 78, 58, 160), Main.rand.NextFloat(0.9f, 1.3f));
                bubble.noGravity = true;
                bubble.fadeIn = 0.7f;
            }
        }

        private void AttackingPhase() {
            if (Framing.GetTileSafely(Projectile.Center.ToTileCoordinates16()).HasTile) {
                return;
            }

            if (attackCooldown > 0) {
                attackCooldown--;
                mouthOpenness = MathHelper.Lerp(mouthOpenness, 0f, 0.15f);
            }

            NPC target = GetTarget();
            if (target == null || !target.active) {
                State = SentryState.Idle;
                StateTimer = 0;
                return;
            }

            Vector2 toTarget = target.Center - Projectile.Center;
            targetRotation = toTarget.ToRotation();
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetRotation, 0.2f);
            moundEmerge = MathHelper.Lerp(moundEmerge, 0.55f, 0.08f);

            //蓄力拍，发射前泥珠向嘴部聚拢
            if (!VaultUtils.isServer && attackCooldown is > 0 and <= 6 && shotsFired < ShotCount && StateTimer % 2 == 0) {
                Vector2 mouth = Projectile.Center + Projectile.rotation.ToRotationVector2() * 24f;
                Vector2 from = mouth + Main.rand.NextVector2CircularEdge(20f, 20f);
                PRTLoader.NewParticle<PRT_FishMudDroplet>(from, (mouth - from) * 0.16f,
                    FishMudPalette.Mud(Main.rand.NextFloat(0.4f, 0.8f)), Main.rand.NextFloat(0.4f, 0.6f))
                    ?.Configure(8, 0.02f);
            }

            if (attackCooldown == 0 && shotsFired < ShotCount) {
                ShootMudBall(target);
                attackCooldown = AttackCooldownMax;
                shotsFired++;
                mouthOpenness = 1f;
                recoilKick = 1f;
            }

            if (shotsFired >= ShotCount || StateTimer >= AttackDuration) {
                State = SentryState.TurningDown;
                StateTimer = 0;
            }
        }

        private void TurningDownPhase() {
            if (StateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item21 with {
                    Volume = 0.5f,
                    Pitch = -0.6f
                }, Projectile.Center);
            }

            mouthOpenness = MathHelper.Lerp(mouthOpenness, 0f, 0.2f);

            targetRotation = MathHelper.PiOver2;
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetRotation, 0.15f);

            //转体甩泥
            if (!VaultUtils.isServer && StateTimer % 4 == 0) {
                PRTLoader.NewParticle<PRT_FishMudDroplet>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    Projectile.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(1f, 2.2f),
                    FishMudPalette.Mud(Main.rand.NextFloat(0.3f, 0.7f)), Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(12, 18));
            }

            if (StateTimer >= TurningDownDuration) {
                State = SentryState.Submerging;
                StateTimer = 0;
                submergeStart = Projectile.Center;
            }
        }

        private void SubmergingPhase() {
            if (StateTimer == 1) {
                if (submergeStart == Vector2.Zero) {
                    submergeStart = Projectile.Center;
                }
                SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.7f, Pitch = -0.5f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.5f, Pitch = -0.2f }, Projectile.Center);
            }

            Projectile.Center += new Vector2(0, 12.5f);

            float t = StateTimer / (float)SubmergeDuration;
            sinkPhase = t;
            emergingProgress = Math.Max(1f - t, 0f);
            //回吸期泥堆先涨后由uSink压塌
            moundEmerge = MathHelper.Lerp(moundEmerge, 0.85f, 0.15f);

            //泥浆回吸，泥珠被拽向入点
            if (!VaultUtils.isServer && StateTimer % 3 == 0) {
                Vector2 from = moundAnchor + new Vector2(Main.rand.NextFloat(-42f, 42f), Main.rand.NextFloat(-16f, 0f));
                Vector2 pull = (moundAnchor + new Vector2(0f, 8f) - from) * 0.11f;
                PRTLoader.NewParticle<PRT_FishMudDroplet>(from, pull,
                    FishMudPalette.Mud(Main.rand.NextFloat(0.2f, 0.6f)), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(12, 18), 0.05f);
            }

            if (StateTimer >= SubmergeDuration) {
                SpawnEndStains();
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = -0.4f }, moundAnchor);
                Projectile.Kill();
            }
        }

        /// <summary>退场残迹，入点留一滩缓慢干涸的泥渍，活得比哨兵久</summary>
        private void SpawnEndStains() {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_FishMudStain>(
                    moundAnchor + new Vector2(Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(-4f, 2f)),
                    Vector2.Zero, FishMudPalette.Mud(Main.rand.NextFloat(0.2f, 0.55f)),
                    Main.rand.NextFloat(0.9f, 1.4f))
                    ?.Configure(Main.rand.Next(70, 110), 2.4f, 2);
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_FishMudDroplet>(
                    moundAnchor + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f),
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(-2.4f, -0.6f)),
                    FishMudPalette.Mud(Main.rand.NextFloat(0.2f, 0.6f)), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(14, 22));
            }
        }

        private void ShootMudBall(NPC target) {
            if (!Projectile.IsOwnedByLocalPlayer()) return;

            Player owner = Main.player[Projectile.owner];
            float shootAngle = Projectile.rotation;
            Vector2 shootDirection = shootAngle.ToRotationVector2();
            Vector2 shootPos = Projectile.Center + shootDirection * 15f * emergingProgress;

            Vector2 toTarget = target.Center - shootPos;
            float distance = toTarget.Length();

            toTarget = toTarget.SafeNormalize(Vector2.Zero);

            float gravity = 0.25f;
            float speed = Math.Min(distance / 25f, 18f);

            Vector2 velocity = toTarget * speed;

            if (distance > 100f) {
                float time = distance / speed;
                float dropCompensation = 0.5f * gravity * time * time / distance;
                velocity.Y -= dropCompensation * speed;
            }

            velocity = velocity.RotatedByRandom(0.08f);

            Projectile.NewProjectile(
                owner.GetSource_FromThis(),
                shootPos,
                velocity,
                ModContent.ProjectileType<MudBall>(),
                Projectile.damage,
                Projectile.knockBack,
                Projectile.owner
            );

            SoundEngine.PlaySound(SoundID.Item85 with {
                Volume = 0.6f,
                Pitch = -0.2f
            }, shootPos);

            //出膛泥浪锥，拉丝泥珠顺射向甩出
            for (int i = 0; i < 6; i++) {
                Vector2 particleVel = velocity.RotatedByRandom(0.45f) * Main.rand.NextFloat(0.35f, 0.7f);
                PRTLoader.NewParticle<PRT_FishMudDroplet>(shootPos, particleVel,
                    FishMudPalette.Mud(Main.rand.NextFloat(0.3f, 0.8f)), Main.rand.NextFloat(0.5f, 0.85f))
                    ?.Configure(Main.rand.Next(12, 20));
            }
            for (int i = 0; i < 4; i++) {
                Dust shoot = Dust.NewDustPerfect(shootPos, DustID.Mud,
                    velocity.RotatedByRandom(0.6f) * Main.rand.NextFloat(0.3f, 0.6f),
                    100, new Color(100, 80, 60), Main.rand.NextFloat(1.2f, 1.8f));
                shoot.noGravity = Main.rand.NextBool();
            }
        }

        private NPC FindTarget() {
            float range = 700f + HalibutData.GetDomainLayer() * 100f;
            NPC closest = null;
            float closestDist = range;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        private NPC GetTarget() {
            int id = (int)TargetID;
            if (id < 0 || id >= Main.maxNPCs) return null;

            NPC target = Main.npc[id];
            if (!target.active || !target.CanBeChasedBy()) return null;

            return target;
        }

        /// <summary>湿重出土曲线</summary>
        private static float EaseOutBack(float x) {
            const float c1 = 1.30f;
            const float c3 = c1 + 1f;
            float u = x - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public override bool PreDraw(ref Color lightColor) {
            //钻升与破土预告期鱼在地下，只有地表鼓包可见
            if (State == SentryState.Rising
                || (State == SentryState.Emerging && StateTimer <= TelegraphFrames)) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            Main.instance.LoadItem(ItemID.Mudfish);
            Texture2D texture = TextureAssets.Item[ItemID.Mudfish].Value;

            if (texture == null) return false;

            Vector2 fishOrigin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            const float drawScale = 1.2f;

            //出土=从泥堆里向上滑出，下潜=锚定入点向下滑没，都由泥柱遮根部
            float buriedOffset = (1f - emergingProgress) * (texture.Height * drawScale + 18f);
            Vector2 drawPos;
            float sinkFade = 1f;
            if (State == SentryState.Submerging) {
                float t = StateTimer / (float)SubmergeDuration;
                drawPos = submergeStart - Main.screenPosition + new Vector2(0, 80f * t * t);
                //末段没入泥中，在泥柱退散前先隐去鱼体
                sinkFade = MathHelper.Clamp((1f - t) / 0.3f, 0f, 1f);
            }
            else {
                drawPos = Projectile.Center - Main.screenPosition + new Vector2(0, buriedOffset);
            }

            //射击后坐，沿嘴向反向缩一口
            drawPos -= Projectile.rotation.ToRotationVector2() * (recoilKick * 6f);

            float wiggleRotation = (float)Math.Sin(bodyWiggle) * 0.1f * emergingProgress;
            if (State == SentryState.TurningDown || State == SentryState.Submerging) {
                wiggleRotation *= 0.5f;
            }
            float totalRotation = Projectile.rotation + wiggleRotation + MathHelper.PiOver4;

            Color mudColor = lightColor;
            mudColor = Color.Lerp(mudColor, new Color(100, 80, 60), 0.4f);
            //攻击蓄势时体色向湿亮泥偏移，不叠加发光层
            mudColor = Color.Lerp(mudColor, FishMudPalette.Wet, mouthOpenness * 0.3f);

            //泥浆包裹层，画在本体之下的深泥剪影
            if (wrapMud > 0.05f) {
                Color wrapColor = FishMudPalette.Murk * (wrapMud * 0.75f * emergingProgress * sinkFade);
                sb.Draw(texture, drawPos + new Vector2(0f, 2f), null, wrapColor,
                    totalRotation, fishOrigin, drawScale * 1.16f, SpriteEffects.None, 0);
            }

            //摆尾转影
            for (int i = 0; i < 3; i++) {
                float lag = (3 - i) * 0.09f;
                Vector2 shadowPos = drawPos + new Vector2((float)Math.Sin(bodyWiggle + i) * 3f, (3 - i) * 2.4f);
                Color shadowColor = new Color(50, 40, 30, 100) * (1f - i * 0.3f) * emergingProgress * sinkFade;

                sb.Draw(texture, shadowPos, null, shadowColor,
                    totalRotation - wiggleRotation * lag * 10f, fishOrigin, drawScale, SpriteEffects.None, 0);
            }

            sb.Draw(texture, drawPos, null, mudColor * sinkFade, totalRotation, fishOrigin, drawScale, SpriteEffects.None, 0);

            //湿面轮廓高光
            float sheenStrength = 0.1f + wrapMud * 0.14f;
            Color sheen = FishMudPalette.Sheen * (sheenStrength * emergingProgress * sinkFade);
            sb.Draw(texture, drawPos + new Vector2(-1.6f, -2.4f), null, sheen,
                totalRotation, fishOrigin, drawScale, SpriteEffects.None, 0);

            return false;
        }

        /// <summary>根部泥堆quad，画在实体层之后，天然盖住鱼体地下段与地面接缝</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !anchorSet) {
                return;
            }
            Effect fx = FishMudAssets.FishMudMound;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            //出入场时泥柱升起遮蔽鱼体，稳定期只剩浅埋根部
            float plug;
            float fade = 1f;
            switch (State) {
                case SentryState.Rising:
                    plug = 0.5f;
                    break;
                case SentryState.Emerging:
                    plug = StateTimer <= TelegraphFrames ? 0.85f : MathHelper.Lerp(1f, 0.4f, emergingProgress);
                    break;
                case SentryState.Submerging:
                    plug = MathHelper.Lerp(0.4f, 1f, sinkPhase);
                    fade = MathHelper.Clamp((1f - sinkPhase) * 5f, 0f, 1f);
                    break;
                default:
                    plug = 0.35f;
                    break;
            }

            //呼吸起伏叠在隆起量上
            float emerge = moundEmerge * (1f + 0.06f * (float)Math.Sin(bodyWiggle * 0.7f));

            Vector2 c = moundAnchor;
            const float halfX = 68f;
            const float upY = 52f;
            const float downY = 78f;

            var verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((c + new Vector2(-halfX, -upY)).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((c + new Vector2(halfX, -upY)).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((c + new Vector2(-halfX, downY)).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((c + new Vector2(halfX, downY)).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            //泥面吃环境光
            Color light = Lighting.GetColor((moundAnchor - new Vector2(0f, 8f)).ToTileCoordinates());
            Vector3 lightVec = Vector3.Max(light.ToVector3(), new Vector3(0.18f));

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(vfxSeed);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uLight"]?.SetValue(lightVec);
            fx.Parameters["uEmerge"]?.SetValue(emerge);
            fx.Parameters["uBurst"]?.SetValue(burstEnvelope);
            fx.Parameters["uSink"]?.SetValue(sinkPhase);
            fx.Parameters["uPlug"]?.SetValue(plug);
            fx.Parameters["uFade"]?.SetValue(fade);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }
    }

    /// <summary>
    /// 泥球，受重力的软体液团，形状由速度持续改写，飞行甩泥、落点留渍
    /// 四阶段，出膛拉丝→飞行液团演化→命中压扁飞溅→泥渍滴淌残留
    /// </summary>
    internal class MudBall : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float wobblePhase = 0f;
        private float vfxSeed = -1f;
        private bool impactHandled = false;
        private const float Gravity = 0.25f;
        private const int FadeFrames = 10;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 4;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        public override void AI() {
            if (vfxSeed < 0f) {
                vfxSeed = Projectile.whoAmI * 0.733f % 1f;
            }

            Projectile.velocity.Y += Gravity;
            //软体蠕动相位随速度加快
            wobblePhase += 0.24f + Projectile.velocity.Length() * 0.016f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            //飞行甩泥，液团一路掉小滴
            if (!VaultUtils.isServer && Projectile.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_FishMudDroplet>(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    Projectile.velocity * 0.22f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    FishMudPalette.Mud(Main.rand.NextFloat(0.25f, 0.7f)), Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(12, 20), 0.3f);
            }
            if (Projectile.timeLeft % 3 == 0) {
                Dust trail = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    DustID.Mud, -Projectile.velocity * 0.2f, 120,
                    new Color(90, 75, 55), Main.rand.NextFloat(0.9f, 1.3f));
                trail.noGravity = true;
            }
        }

        private float DissolveFade => MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);

        public override bool OnTileCollide(Vector2 oldVelocity) {
            SoundEngine.PlaySound(SoundID.Item50 with {
                Volume = 0.5f,
                Pitch = -0.3f
            }, Projectile.Center);

            bool hitWall = Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.1f;
            SpawnSplat(oldVelocity, hitWall);
            impactHandled = true;

            Projectile.penetrate--;
            if (Projectile.penetrate <= 0) {
                return true;
            }

            if (hitWall) {
                Projectile.velocity.X = -oldVelocity.X * 0.4f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.1f) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.3f;
            }
            //反弹压扁
            wobblePhase = -MathHelper.PiOver2;
            //弹起继续飞行，后续寿终仍要收尾滴
            impactHandled = false;

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Slow, 180);

            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.6f,
                Pitch = -0.4f
            }, Projectile.Center);

            SpawnSplat(Projectile.velocity, true);
            impactHandled = true;
        }

        public override void OnKill(int timeLeft) {
            //空中寿终或穿透耗尽的收尾
            if (impactHandled || VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_FishMudDroplet>(Projectile.Center,
                    Main.rand.NextVector2Circular(2.5f, 2.5f),
                    FishMudPalette.Mud(Main.rand.NextFloat(0.2f, 0.6f)), Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.Next(10, 16));
            }
        }

        /// <summary>命中飞溅，入射向泥珠扇+泥瓣+滴淌泥渍，渍是活得比弹体久的残迹</summary>
        private void SpawnSplat(Vector2 incoming, bool onWall) {
            if (VaultUtils.isServer) {
                return;
            }

            Vector2 dir = -incoming.SafeNormalize(Vector2.UnitY);
            //泥珠反射锥
            for (int i = 0; i < 9; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2f, 5.5f);
                PRTLoader.NewParticle<PRT_FishMudDroplet>(Projectile.Center, vel,
                    FishMudPalette.Mud(Main.rand.NextFloat()), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(16, 26));
            }
            //两块大泥瓣
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishMudClod>(Projectile.Center,
                    dir.RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f)) * Main.rand.NextFloat(2.5f, 4.5f),
                    FishMudPalette.Mud(Main.rand.NextFloat(0.3f, 0.7f)), Main.rand.NextFloat(0.6f, 0.9f))
                    ?.Configure(Main.rand.Next(20, 30));
            }
            //泥渍
            PRTLoader.NewParticle<PRT_FishMudStain>(Projectile.Center, Vector2.Zero,
                FishMudPalette.Mud(Main.rand.NextFloat(0.25f, 0.6f)), Main.rand.NextFloat(0.7f, 1f))
                ?.Configure(Main.rand.Next(55, 85), onWall ? 1.2f : 2.4f, 2);
            //底噪
            for (int i = 0; i < 5; i++) {
                Dust splat = Dust.NewDustPerfect(Projectile.Center, DustID.Mud,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), 100,
                    new Color(85, 70, 50), Main.rand.NextFloat(1.2f, 1.9f));
                splat.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //液团由图元层shader绘制，这里只留shader缺失时的软体后备
            if (FishMudAssets.FishMudGlob != null) {
                return false;
            }

            Texture2D tex = FishMudAssets.DropTex?.Value;
            if (tex == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0f, 1f);
            Vector2 scale = new Vector2(1.1f * (1f + stretch), 0.95f * (1f - stretch * 0.25f));
            float rot = Projectile.rotation + MathHelper.PiOver2;
            float fade = DissolveFade;

            sb.Draw(tex, pos, null, FishMudPalette.Deep * (0.85f * fade), rot, origin, scale * 1.3f, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, FishMudPalette.Base * fade, rot, origin, scale, SpriteEffects.None, 0f);
            sb.Draw(tex, pos, null, FishMudPalette.Wet * (0.7f * fade), rot, origin, scale * 0.55f, SpriteEffects.None, 0f);
            return false;
        }

        /// <summary>液团quad，本体+两枚旧位残影，沿速度轴摆放</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ) {
                return;
            }
            Effect fx = FishMudAssets.FishMudGlob;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            //液团吃环境光
            Color light = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Vector3 lightVec = Vector3.Max(light.ToVector3(), new Vector3(0.18f));

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            fx.Parameters["uLight"]?.SetValue(lightVec);

            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.05f, 0f, 1f);
            float fade = DissolveFade;

            //旧位残影在前
            for (int i = 2; i >= 0; i--) {
                if (i > 0 && Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 center = i == 0 ? Projectile.Center : Projectile.oldPos[i] + Projectile.Size * 0.5f;
                float k = i / 3f;
                float ghostFade = fade * (i == 0 ? 1f : 0.42f - k * 0.3f);
                float sizeMul = 1f - k * 0.28f;

                DrawGlobQuad(device, fx, center, stretch, ghostFade, sizeMul, i * 0.37f);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }

        private void DrawGlobQuad(GraphicsDevice device, Effect fx, Vector2 center, float stretch, float fade, float sizeMul, float seedShift) {
            if (fade <= 0.01f) {
                return;
            }
            Vector2 along = Projectile.rotation.ToRotationVector2();
            Vector2 perp = along.RotatedBy(MathHelper.PiOver2);
            float halfAlong = (15f + stretch * 15f) * sizeMul;
            float halfAcross = 12.5f * (1f - stretch * 0.22f) * sizeMul;

            //uv.x=0头部在速度前方，尾须甩在身后；世界坐标交给transformMatrix换算
            Vector2 head = center + along * halfAlong;
            Vector2 tail = center - along * halfAlong;

            var verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((head - perp * halfAcross).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((tail - perp * halfAcross).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((head + perp * halfAcross).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((tail + perp * halfAcross).ToVector3(), Color.White, new Vector2(1f, 1f));

            fx.Parameters["uSeed"]?.SetValue(vfxSeed + seedShift);
            fx.Parameters["uStretch"]?.SetValue(stretch);
            fx.Parameters["uWobble"]?.SetValue(wobblePhase);
            fx.Parameters["uFade"]?.SetValue(fade);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }
    }
}
