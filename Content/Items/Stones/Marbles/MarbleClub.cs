using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.GameSystem;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Stones.Marbles
{
    /// <summary>大理石巨棍：蓄-砸-震三拍重锤，棍头探地生成冲击波，命中必定短石化</summary>
    internal class MarbleClub : ModItem
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleClub";

        public override void SetDefaults() {
            Item.width = Item.height = 56;
            Item.damage = 22;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = Item.useTime = 33;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTurn = false;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MarbleClubHeld>();
            Item.shootSpeed = 6f;
            Item.value = Item.sellPrice(0, 0, 80, 0);
            Item.rare = ItemRarityID.Orange;
            //noMelee 武器需要手动允许近战词缀，否则攻速词缀永远刷不出来
            ItemOverride.ItemMeleePrefixDic[Type] = true;
        }

        //场上只允许一柄巨棍，避免连点重复进入挥击
        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);
            return false;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Marble, 25)
                .AddRecipeGroup(CWRCrafted.TinBarGroup, 12)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    /// <summary>
    /// 巨棍HeldProj，三拍节奏：抬手蓄势（身体后倾+上飘石尘）→ ease-in-quart 猛砸（MarbleSlash弧光带）
    /// → 落地/命中顿帧 → 收力。砸落末端从棍头沿重力方向探实际地表：
    /// 砸中地面生成全额 <see cref="MarbleShockwave"/> 与沿地奔跑的尘土波，悬空只放半径减半的空气震荡
    /// </summary>
    internal class MarbleClubHeld : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => GraniteMarbleVFX.MarbleTex + "MarbleClub";

        //三拍时长（逻辑帧，受攻速缩放）；顿帧独立计时不占 elapsed
        private const float WindupTime = 13f;
        private const float SlamTime = 12f;
        private const float RecoverTime = 11f;
        private const float TotalTime = WindupTime + SlamTime + RecoverTime;
        private const int HitstopFrames = 3;

        //朝右基准角：预备 → 过顶后拉（蓄势终点略过 LiftRel，作 anticipation 过冲）→ 砸落
        private const float ReadyRel = -MathHelper.Pi * 0.26f;
        private const float OverLiftRel = -MathHelper.Pi * 0.78f;
        private const float EndRel = MathHelper.Pi * 0.5f;
        //纹理在无旋转时棍头指向约 -63.5°（右上偏陡，依据贴图像素主轴实测），绘制时补偿到实际指向
        private const float TextureBladeAngle = -1.108f;

        private const float HoldDistance = 40f;
        private const float BladeLength = 96f;
        //棍头沿重力探地表的最大距离（tile）
        private const int GroundProbeTiles = 6;

        private float elapsed;
        private int hitstopTimer;
        private int lockedDirection = 1;
        private float currentRotation;
        private float lastRotation;
        private Vector2 pivot;
        private float bodyLean;
        private bool slamSoundPlayed;

        //落点冲击状态
        private bool impactDone;
        private bool groundedImpact;
        private Vector2 impactPoint;
        private float recoverStartRot;

        //沿地表双向奔跑的尘土波
        private const int DustWaveSteps = 8;
        private const float DustWaveStride = 22f;
        private int dustWaveStep;
        private int surfaceTileYLeft, surfaceTileYRight;
        private bool waveBlockedLeft, waveBlockedRight;

        //弧光轨迹缓存：每逻辑帧细分采样保证 TriangleStrip 平滑
        private const int TrailMax = 64;
        private const int TrailSubdiv = 4;
        private readonly float[] trailRot = new float[TrailMax];
        private int trailCount;
        private float trailFade;

        public float CurrentAngle => currentRotation;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 130;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.ownerHitCheck = true;
            Projectile.timeLeft = 120;
        }

        public override bool ShouldUpdatePosition() => false;

        //仅在砸落阶段（含顿帧与一点收尾余量）参与伤害
        public override bool? CanDamage() => elapsed >= WindupTime && elapsed <= WindupTime + SlamTime + 2f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            Vector2 tip = pivot + currentRotation.ToRotationVector2() * BladeLength;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , pivot, tip, 42f, ref collisionPoint);
        }

        public override void Initialize() {
            //朝向锁定到光标所在的左右侧，整个挥击过程不再随玩家转身而抖动
            lockedDirection = Math.Sign(ToMouse.X);
            if (lockedDirection == 0) {
                lockedDirection = Owner.direction;
            }
            Owner.direction = lockedDirection;
            currentRotation = MirrorAngle(ReadyRel);
            lastRotation = currentRotation;
            recoverStartRot = MirrorAngle(EndRel);
            if (!VaultUtils.isServer) {
                //起手：沉重的抡起 + 石棍离地的磨擦
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.55f, Volume = 0.8f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.5f, Volume = 0.4f }, Owner.Center);
            }
        }

        public override void AI() {
            if (Item.type != ModContent.ItemType<MarbleClub>()) {
                Projectile.Kill();
                return;
            }
            if (elapsed >= TotalTime) {
                Projectile.Kill();
                return;
            }

            //命中/落地顿帧：elapsed 暂停，保持砸落姿势，弧光停驻
            if (hitstopTimer > 0) {
                hitstopTimer--;
                PushTrailSamples();
                UpdateDustWave();
                UpdatePlayerPose();
                Lighting.AddLight(pivot, GraniteMarbleVFX.MarbleGold.ToVector3() * 0.6f);
                return;
            }

            lastRotation = currentRotation;

            if (elapsed < WindupTime) {
                //抬手蓄势：smoothstep 缓入缓出，末段减速地拉到过顶后方（anticipation）
                float t = elapsed / WindupTime;
                float w = t * t * (3f - 2f * t);
                currentRotation = MathHelper.Lerp(MirrorAngle(ReadyRel), MirrorAngle(OverLiftRel), w);
                bodyLean = -0.065f * w;
                trailFade = 0f;
            }
            else if (elapsed < WindupTime + SlamTime) {
                //猛砸：ease-in-quart 前慢后快
                float s = (elapsed - WindupTime) / SlamTime;
                float eased = s * s * s * s;
                currentRotation = MathHelper.Lerp(MirrorAngle(OverLiftRel), MirrorAngle(EndRel), eased);
                bodyLean = MathHelper.Lerp(-0.065f, 0.045f, eased);
                trailFade = 1f;

                if (!slamSoundPlayed) {
                    slamSoundPlayed = true;
                    if (!VaultUtils.isServer) {
                        //破空分层：低鸣 + 沉重杖挥
                        SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.72f, Volume = 1.05f }, Owner.Center);
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.35f, Volume = 0.85f }, Owner.Center);
                    }
                }

                PushTrailSamples();
                SpawnSwingParticles();

                //砸落中后段开始探测棍头触地：提前砸中地形立即触发冲击（不再等固定帧）
                pivot = GetHandPos() + currentRotation.ToRotationVector2() * HoldDistance;
                Vector2 tip = pivot + currentRotation.ToRotationVector2() * BladeLength;
                if (!impactDone && (eased >= 0.985f || (eased > 0.55f && TipTouchingGround(tip)))) {
                    TriggerImpact(tip);
                    return;
                }
            }
            else {
                //收力：从砸落姿势缓出地回抬一小段，弧光收缩渐隐
                if (!impactDone) {
                    //高攻速下 elapsed 可能整帧跨过砸落末端，进收尾前补触发
                    currentRotation = MirrorAngle(EndRel);
                    pivot = GetHandPos() + currentRotation.ToRotationVector2() * HoldDistance;
                    TriggerImpact(pivot + currentRotation.ToRotationVector2() * BladeLength);
                    return;
                }
                float r = (elapsed - WindupTime - SlamTime) / RecoverTime;
                float er = 1f - (1f - r) * (1f - r);
                currentRotation = recoverStartRot - 0.38f * lockedDirection * er;
                bodyLean = MathHelper.Lerp(0.045f, 0f, er);
                trailFade = 1f - r;
                PushTrailSamples();
            }

            pivot = GetHandPos() + currentRotation.ToRotationVector2() * HoldDistance;

            if (elapsed < WindupTime) {
                SpawnWindupParticles();
            }

            UpdateDustWave();
            UpdatePlayerPose();
            Lighting.AddLight(pivot, GraniteMarbleVFX.MarbleGold.ToVector3() * 0.5f);

            //吃攻速：近战词缀/装备加成直接加快三拍节奏
            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            elapsed += speed;
        }

        /// <summary>砸落末端一次性冲击：探地表定落点，分派贴地全额/空中减半两种形态</summary>
        private void TriggerImpact(Vector2 tip) {
            impactDone = true;
            hitstopTimer = HitstopFrames;
            recoverStartRot = currentRotation;
            //顿帧结束后直接进入收尾拍
            elapsed = WindupTime + SlamTime;

            groundedImpact = TryFindGroundAlongGravity(tip, GroundProbeTiles, out impactPoint);
            if (!groundedImpact) {
                impactPoint = tip;
            }

            if (!VaultUtils.isServer) {
                if (groundedImpact) {
                    //重响分层：低频砸击 + 轰底 + 石裂
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 1.15f, Pitch = -0.45f }, impactPoint);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.7f }, impactPoint);
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.9f, Pitch = -0.25f }, impactPoint);
                    SpawnGroundImpactParticles();
                }
                else {
                    //悬空挥空：沉重破空 + 闷响
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.9f, Pitch = -0.55f }, impactPoint);
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = -0.35f }, impactPoint);
                    SpawnAerialImpactParticles();
                }
            }

            if (CWRServerConfig.Instance.ScreenVibration) {
                if (groundedImpact) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(impactPoint
                        , Vector2.UnitY * Owner.gravDir, 9f, 7f, 16, 800f, FullName));
                    Owner.CWR().GetScreenShake(4.5f);
                }
                else {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(impactPoint
                        , currentRotation.ToRotationVector2(), 4.5f, 6f, 9, 800f, FullName));
                }
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                //贴地全额半径，悬空减半
                float radius = groundedImpact ? 150f : 75f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), impactPoint, Vector2.Zero
                    , ModContent.ProjectileType<MarbleShockwave>(), (int)(Projectile.damage * 0.55f)
                    , Projectile.knockBack * 0.5f, Projectile.owner, 0f, radius);
            }

            if (groundedImpact) {
                //启动沿地表双向奔跑的尘土波
                dustWaveStep = 1;
                surfaceTileYLeft = surfaceTileYRight = (int)((impactPoint.Y + 8f) / 16f);
                waveBlockedLeft = waveBlockedRight = false;
            }

            UpdatePlayerPose();
        }

        private void SpawnGroundImpactParticles() {
            for (int i = 0; i < 16; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-4.5f, 4.5f), -Main.rand.NextFloat(1f, 5f));
                PRTLoader.NewParticle<PRT_Smoke>(impactPoint, vel, GraniteMarbleVFX.MarbleDust
                    , Main.rand.NextFloat(0.45f, 0.8f)).Configure(28, 0.7f, 0.05f);
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(impactPoint
                    , new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(3f, 8f))
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.5f, 0.9f)).Configure(Main.rand.Next(24, 36));
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(impactPoint + Main.rand.NextVector2Circular(20f, 8f)
                    , Main.rand.NextVector2Circular(2f, 2f), GraniteMarbleVFX.MarbleGold, 0.7f)
                    .Configure(GraniteMarbleVFX.MarbleGold, 18, 0.2f, Main.rand.NextFloat(0.5f, 0.9f));
            }
            //落点白闪
            PRTLoader.NewParticle<PRT_Sparkle>(impactPoint, Vector2.Zero, GraniteMarbleVFX.MarbleCore, 1.1f)
                .Configure(GraniteMarbleVFX.MarbleCore, 12, 0f, 1.6f);
        }

        private void SpawnAerialImpactParticles() {
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(impactPoint, Main.rand.NextVector2Circular(3.5f, 3.5f)
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.35f, 0.6f)).Configure(22, 0.6f, 0.05f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_MarbleChip>(impactPoint, Main.rand.NextVector2Circular(3f, 3f)
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.65f)).Configure(Main.rand.Next(18, 26));
            }
        }

        /// <summary>沿地表双向奔跑的尘土波：每帧波前外推一步，贴着地形高度连续扬尘</summary>
        private void UpdateDustWave() {
            if (!groundedImpact || dustWaveStep <= 0 || dustWaveStep > DustWaveSteps || VaultUtils.isServer) {
                return;
            }
            float front = dustWaveStep * DustWaveStride;
            float strength = 1f - dustWaveStep / (float)(DustWaveSteps + 1);
            SpawnDustWaveSide(-1, front, strength, ref surfaceTileYLeft, ref waveBlockedLeft);
            SpawnDustWaveSide(1, front, strength, ref surfaceTileYRight, ref waveBlockedRight);
            dustWaveStep++;
        }

        private void SpawnDustWaveSide(int side, float front, float strength, ref int surfaceTileY, ref bool blocked) {
            if (blocked) {
                return;
            }
            int tileX = (int)((impactPoint.X + side * front) / 16f);
            surfaceTileY = FollowSurface(tileX, surfaceTileY);
            //撞墙或悬崖：波前被地形吃掉
            if (!IsSolidTile(tileX, surfaceTileY) || IsSolidTile(tileX, surfaceTileY - 1)) {
                blocked = true;
                return;
            }
            Vector2 pos = new Vector2(tileX * 16f + 8f, surfaceTileY * 16f - 6f);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(pos + Main.rand.NextVector2Circular(6f, 4f)
                    , new Vector2(side * Main.rand.NextFloat(1.8f, 3.2f), -Main.rand.NextFloat(0.6f, 1.6f))
                    , GraniteMarbleVFX.MarbleDust, strength * Main.rand.NextFloat(0.5f, 0.75f)).Configure(24, 0.6f, 0.04f);
            }
            PRTLoader.NewParticle<PRT_MarbleChip>(pos
                , new Vector2(side * Main.rand.NextFloat(1.5f, 3.5f), -Main.rand.NextFloat(2f, 4.5f))
                , GraniteMarbleVFX.MarbleGold, strength * Main.rand.NextFloat(0.45f, 0.75f)).Configure(Main.rand.Next(18, 28));
        }

        /// <summary>尘土波沿地表高度跟踪：地形升高向上让、下降向下贴，±4 格内</summary>
        private static int FollowSurface(int tileX, int lastSurfaceY) {
            int y = lastSurfaceY;
            int guard = 0;
            if (IsSolidTile(tileX, y)) {
                while (guard++ < 4 && IsSolidTile(tileX, y - 1)) {
                    y--;
                }
            }
            else {
                while (guard++ < 4 && !IsSolidTile(tileX, y)) {
                    y++;
                }
            }
            return y;
        }

        private static bool IsSolidTile(int x, int y) {
            if (!WorldGen.InWorld(x, y)) {
                return false;
            }
            Tile t = Framing.GetTileSafely(x, y);
            return t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType];
        }

        private bool TipTouchingGround(Vector2 tip) {
            Vector2 probe = tip + Vector2.UnitY * Owner.gravDir * 8f;
            return IsSolidTile((int)(tip.X / 16f), (int)(tip.Y / 16f))
                || IsSolidTile((int)(probe.X / 16f), (int)(probe.Y / 16f));
        }

        /// <summary>
        /// 从起点沿重力方向逐格探实心地表，命中返回贴地表面点；
        /// 末速极快时棍头可能帧间直接扎进地里，此时反向上溯找回真实表面
        /// </summary>
        private bool TryFindGroundAlongGravity(Vector2 from, int maxTiles, out Vector2 surface) {
            int tx = (int)(from.X / 16f);
            int ty = (int)(from.Y / 16f);
            int step = Owner.gravDir >= 0f ? 1 : -1;

            if (IsSolidTile(tx, ty)) {
                int y = ty;
                int guard = 0;
                while (guard++ < maxTiles && IsSolidTile(tx, y - step)) {
                    y -= step;
                }
                surface = new Vector2(from.X, step > 0 ? y * 16f - 4f : y * 16f + 20f);
                return true;
            }

            for (int i = 1; i <= maxTiles; i++) {
                int y = ty + i * step;
                if (!WorldGen.InWorld(tx, y)) {
                    break;
                }
                if (IsSolidTile(tx, y)) {
                    surface = new Vector2(from.X, step > 0 ? y * 16f - 4f : y * 16f + 20f);
                    return true;
                }
            }
            surface = from;
            return false;
        }

        private void PushTrailSamples() {
            for (int s = TrailSubdiv - 1; s >= 0; s--) {
                float rot = MathHelper.Lerp(currentRotation, lastRotation, s / (float)TrailSubdiv);
                for (int i = Math.Min(trailCount, TrailMax - 1); i > 0; i--) {
                    trailRot[i] = trailRot[i - 1];
                }
                trailRot[0] = rot;
                if (trailCount < TrailMax) {
                    trailCount++;
                }
            }
        }

        /// <summary>蓄势期上飘石尘：棍头掉渣与浮尘，暗示即将到来的重击</summary>
        private void SpawnWindupParticles() {
            if (VaultUtils.isServer || !Main.rand.NextBool(2)) {
                return;
            }
            Vector2 head = pivot + currentRotation.ToRotationVector2()
                * Main.rand.NextFloat(BladeLength * 0.55f, BladeLength);
            PRTLoader.NewParticle<PRT_Smoke>(head, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.4f))
                , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.3f, 0.5f)).Configure(20, 0.5f, 0.03f);
            if (Main.rand.NextBool(3)) {
                //低重力石屑：从棍身上飘剥落
                PRTLoader.NewParticle<PRT_MarbleChip>(head, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.5f, 1.5f))
                    , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.35f, 0.5f)).Configure(16, 0.12f);
            }
        }

        /// <summary>砸落期沿棍身甩出的石屑与金闪</summary>
        private void SpawnSwingParticles() {
            if (VaultUtils.isServer || !Main.rand.NextBool(2)) {
                return;
            }
            Vector2 along = GetHandPos() + currentRotation.ToRotationVector2()
                * Main.rand.NextFloat(BladeLength * 0.4f, BladeLength + HoldDistance);
            int swingSign = Math.Sign(currentRotation - lastRotation);
            if (swingSign == 0) {
                swingSign = lockedDirection;
            }
            Vector2 tangent = currentRotation.ToRotationVector2().RotatedBy(swingSign * MathHelper.PiOver2);
            PRTLoader.NewParticle<PRT_MarbleChip>(along, tangent * Main.rand.NextFloat(3f, 6f)
                , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.4f, 0.65f)).Configure(Main.rand.Next(16, 24));
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Sparkle>(along, Vector2.Zero, GraniteMarbleVFX.MarbleGold, 0.6f)
                    .Configure(GraniteMarbleVFX.MarbleGold, 12, 0.2f, Main.rand.NextFloat(0.4f, 0.7f));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //必定短石化：Boss 只吃减速且持续减半，杂兵额外吃当场急停
            target.AddBuff(ModContent.BuffType<MarblePetrify>(), target.boss ? 60 : 120);
            if (!target.boss) {
                target.velocity *= 0.35f;
            }

            //命中顿帧：砸中目标也顿一拍（落地冲击已顿则不叠加）
            if (hitstopTimer <= 0 && !impactDone) {
                hitstopTimer = HitstopFrames;
            }

            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f)
                        , new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(2f, 5f))
                        , GraniteMarbleVFX.MarbleGold, Main.rand.NextFloat(0.45f, 0.75f)).Configure(Main.rand.Next(20, 30));
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Smoke>(target.Center, Main.rand.NextVector2Circular(2.5f, 2.5f)
                        , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.35f, 0.55f)).Configure(20, 0.55f, 0.05f);
                }
            }

            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(target.Center
                    , currentRotation.ToRotationVector2(), 3.5f, 5f, 7, 800f, FullName));
            }
        }

        public override void OnKill(int timeLeft) {
            //归还蓄势期借用的身体后倾
            Owner.fullRotation = 0f;
        }

        /// <summary>双臂跟随棍体朝向；蓄-砸-收期间给身体一个后倾→前压的重心细节</summary>
        private void UpdatePlayerPose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = lockedDirection;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = currentRotation;

            Owner.fullRotation = bodyLean * lockedDirection * Owner.gravDir;
            Owner.fullRotationOrigin = Owner.Size / 2f;

            float armAngle = currentRotation - MathHelper.PiOver2;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armAngle + 0.1f * lockedDirection);

            Projectile.Center = pivot;
            Projectile.timeLeft = 120;
        }

        private Vector2 GetHandPos() {
            Vector2 p = Owner.GetPlayerStabilityCenter();
            p.Y -= 6f * Owner.gravDir;
            return p;
        }

        //朝右直接返回，朝左绕 Y 轴镜像（π - θ），保证斜向姿势在两个朝向都正确
        private float MirrorAngle(float rightFacingAngle)
            => lockedDirection > 0 ? rightFacingAngle : MathHelper.Pi - rightFacingAngle;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureValue;
            Vector2 origin = tex.Size() / 2f;
            //朝左时竖直镜像，并按 +TextureBladeAngle 补偿（FlipVertically 下的通用解，使棍头始终指向 currentRotation）
            SpriteEffects effect = lockedDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float drawRot = lockedDirection == -1 ? currentRotation + TextureBladeAngle : currentRotation - TextureBladeAngle;

            //砸落阶段保留两道轻残影垫在弧光带下，强化棍体本身的重量感
            if (elapsed >= WindupTime && elapsed <= WindupTime + SlamTime + 2f) {
                for (int i = 1; i <= 2; i++) {
                    float rot = MathHelper.Lerp(currentRotation, lastRotation, i / 3f);
                    Vector2 pos = GetHandPos() + rot.ToRotationVector2() * HoldDistance - Main.screenPosition;
                    float trailDrawRot = lockedDirection == -1 ? rot + TextureBladeAngle : rot - TextureBladeAngle;
                    Color trailColor = GraniteMarbleVFX.MarbleGold * (0.35f * (1f - i / 3f));
                    trailColor.A = 0;
                    Main.EntitySpriteDraw(tex, pos, null, trailColor, trailDrawRot, origin, Projectile.scale * 1.02f, effect, 0);
                }
            }

            Main.EntitySpriteDraw(tex, pivot - Main.screenPosition, null, lightColor, drawRot, origin
                , Projectile.scale * 1.05f, effect, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailCount < 3 || trailFade <= 0.02f) {
                return;
            }
            Effect effect = EffectLoader.MarbleSlash?.Value;
            if (effect == null) {
                return;
            }

            //TriangleStrip 弧光带：uv.x=1 最新挥砍缘，uv.y=0 外缘（棍头侧）
            var bars = new VertexPositionColorTexture[trailCount * 2];
            Vector2 center = GetHandPos();
            float outer = HoldDistance + BladeLength + 14f;
            float inner = (HoldDistance + BladeLength) * 0.34f;
            for (int i = 0; i < trailCount; i++) {
                float factor = 1f - i / (float)trailCount;
                Vector2 dir = trailRot[i].ToRotationVector2();
                bars[i * 2] = new VertexPositionColorTexture((center + dir * outer).ToVector3()
                    , Color.White, new Vector2(factor, 0f));
                bars[i * 2 + 1] = new VertexPositionColorTexture((center + dir * inner).ToVector3()
                    , Color.White, new Vector2(factor, 1f));
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            //重砸 heat 拉满：金边与白芯全亮
            GraniteMarbleVFX.ApplyMarbleSlash(effect, trailFade, 1f);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars, 0, bars.Length - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }
    }
}
