using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishPeng : FishSkill
    {
        public override int UnlockFishID => ItemID.Pengfish;
        public override int DefaultCooldown => 60 * (8 - HalibutData.GetDomainLayer() / 2);
        public override int ResearchDuration => 60 * 18;
        private int spawnTimer = 0;
        private static int MaxActivePenguins => 3 + HalibutData.GetDomainLayer() / 2;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (!Active(player) || Cooldown > 0) {
                return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
            }

            if (++spawnTimer <= 3 + HalibutData.GetDomainLayer() / 2) {
                int existingCount = player.CountProjectilesOfID<FallingPenguin>();
                int maxCount = MaxActivePenguins + HalibutData.GetDomainLayer() * 2;

                if (existingCount < maxCount) {
                    NPC target = player.Center.FindClosestNPC(1000f);
                    if (target != null) {
                        SpawnFallingPenguin(player, source, target, damage, knockback);
                    }
                }
            }
            else {
                spawnTimer = 0;
                SetCooldown();
            }

            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        private static void SpawnFallingPenguin(Player player, EntitySource_ItemUse_WithAmmo source, NPC target, int damage, float knockback) {
            Vector2 targetPos = target.Center;
            Vector2 spawnPos = targetPos + new Vector2(Main.rand.NextFloat(-100f, 100f), -600f);

            Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<FallingPenguin>(),
                (int)(damage * (2.25f + HalibutData.GetDomainLayer() * 0.55f)),
                knockback * 1.5f,
                player.whoAmI,
                0
            );

            SoundEngine.PlaySound(SoundID.Item30 with {
                Volume = 0.4f,
                Pitch = 0.3f
            }, spawnPos);
        }
    }

    internal class FallingPenguin : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Penguin;

        private enum PenguinState
        {
            Falling,
            Descending,
            Impact,
            Bouncing,
            Waddling,
            Disappearing
        }

        private PenguinState State {
            get => (PenguinState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float TargetID => ref Projectile.ai[1];
        private ref float StateTimer => ref Projectile.ai[2];

        private float rotation = 0f;
        private float rotationSpeed = 0f;
        private Vector2 targetPosition = Vector2.Zero;
        private int bounceCount = 0;
        //落点阴影预告的地面缓存，-1 为未命中地面
        private float groundY = -1f;
        private int groundScanCd = 0;
        private int diveSoundStage = 0;
        private const int MaxBounces = 2;
        private const float Gravity = 0.6f;
        private const float MaxFallSpeed = 24f;
        private const float TargetingStrength = 0.15f;
        private const int ImpactRadius = 120;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 10;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 6;
        }

        public override void AI() {
            StateTimer++;

            switch (State) {
                case PenguinState.Falling:
                    FallingPhase();
                    break;
                case PenguinState.Descending:
                    DescendingPhase();
                    break;
                case PenguinState.Impact:
                    ImpactPhase();
                    break;
                case PenguinState.Bouncing:
                    BouncingPhase();
                    break;
                case PenguinState.Waddling:
                    WaddlingPhase();
                    break;
                case PenguinState.Disappearing:
                    DisappearingPhase();
                    break;
            }

            rotation += rotationSpeed;

            if (State == PenguinState.Falling || State == PenguinState.Descending) {
                UpdateGroundCache();
                //冷空气动力微光，只随速度亮
                float speedT = MathHelper.Clamp(Projectile.velocity.Length() / MaxFallSpeed, 0f, 1f);
                Lighting.AddLight(Projectile.Center, 0.16f * speedT, 0.20f * speedT, 0.28f * speedT);
            }
            else if (State == PenguinState.Impact && StateTimer <= 2) {
                //着陆 2 帧过曝闪光配套的光照脉冲
                Lighting.AddLight(Projectile.Center, 0.6f, 0.7f, 0.85f);
            }
        }

        private void FallingPhase() {
            if (StateTimer == 1) {
                //出仓：小幅翻滚 + 高空微尘，渐显入场
                rotationSpeed = Main.rand.NextFloat(-0.22f, 0.22f);
                Projectile.alpha = 200;
                FishPengVFX.SnowBurst(Projectile.Center, 2, 1.6f);

                NPC target = GetTarget();
                if (target != null) {
                    targetPosition = target.Center;
                }
            }

            if (Projectile.alpha > 0) {
                Projectile.alpha = Math.Max(0, Projectile.alpha - 26);
            }

            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }

            NPC currentTarget = GetTarget();
            if (currentTarget != null) {
                targetPosition = currentTarget.Center;
                Vector2 toTarget = targetPosition - Projectile.Center;
                toTarget.Y = 0;

                if (toTarget.LengthSquared() > 0) {
                    Vector2 targetVelocity = toTarget.SafeNormalize(Vector2.Zero) * TargetingStrength;
                    Projectile.velocity.X += targetVelocity.X;
                    Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X, -8f, 8f);
                }
            }

            rotationSpeed += Main.rand.NextFloat(-0.02f, 0.02f);
            rotationSpeed = MathHelper.Clamp(rotationSpeed, -0.5f, 0.5f);

            if (StateTimer > 20) {
                State = PenguinState.Descending;
                StateTimer = 0;
            }
        }

        private void DescendingPhase() {
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }

            NPC currentTarget = GetTarget();
            if (currentTarget != null) {
                targetPosition = currentTarget.Center;
                Vector2 toTarget = targetPosition - Projectile.Center;
                toTarget.Y = 0;

                if (toTarget.LengthSquared() > 0) {
                    Vector2 targetVelocity = toTarget.SafeNormalize(Vector2.Zero) * TargetingStrength;
                    Projectile.velocity.X += targetVelocity.X;
                    Projectile.velocity.X = MathHelper.Clamp(Projectile.velocity.X, -8f, 8f);
                }
            }

            //神风锁定：翻滚衰减，姿态笔直转向头朝下
            rotationSpeed *= 0.88f;
            float aim = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            rotation = rotation.AngleLerp(aim, 0.16f);

            float speed = Projectile.velocity.Length();
            if (diveSoundStage == 0 && StateTimer == 2) {
                diveSoundStage = 1;
                FishPengVFX.DiveWhoosh(Projectile.Center, 0);
            }
            else if (diveSoundStage == 1 && speed > 21f) {
                diveSoundStage = 2;
                FishPengVFX.DiveWhoosh(Projectile.Center, 1);
            }

            //风切线：高速段身侧甩线，每 2 帧至多 1 条
            if (speed > 17f && StateTimer % 2 == 0) {
                FishPengVFX.WindShear(Projectile.Center, Projectile.velocity);
            }

            //低频雪尘垫底
            if (StateTimer % 6 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , DustID.Snow, -Projectile.velocity * 0.25f, 120, FishPengVFX.Snow, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        private void ImpactPhase() {
            if (StateTimer == 1) {
                bool first = bounceCount <= 1;
                CreateImpactEffect(first);
                DamageNearbyEnemies();
                FishPengVFX.ImpactBoom(Projectile.Center, first);
                if (first) {
                    //凝结尾迹与机体分离，留在天上缓蚀
                    FishPengVFX.SnapContrail(Projectile);
                }
            }

            Projectile.velocity *= 0.8f;
            //砸地后姿态迅速回正，准备一本正经站起来
            rotation = rotation.AngleLerp(0f, 0.25f);
            rotationSpeed *= 0.9f;

            if (StateTimer >= 10) {
                if (bounceCount < MaxBounces && Math.Abs(Projectile.velocity.Y) > 2f) {
                    State = PenguinState.Bouncing;
                    StateTimer = 0;
                }
                else {
                    State = PenguinState.Waddling;
                    StateTimer = 0;
                    rotation = 0f;
                }
            }
        }

        private void BouncingPhase() {
            if (StateTimer == 1) {
                //回弹翻滚：布娃娃式的喜剧空翻
                rotationSpeed = Main.rand.NextFloat(0.2f, 0.34f) * (Main.rand.NextBool() ? 1f : -1f);
            }

            Projectile.velocity.Y += Gravity * 0.8f;
            Projectile.velocity.X *= 0.95f;

            rotationSpeed *= 0.97f;

            if (StateTimer % 5 == 0) {
                SpawnBounceDust();
            }
        }

        private void WaddlingPhase() {
            Projectile.velocity.Y = 11f;
            Projectile.velocity.X *= 0.92f;
            VaultUtils.ClockFrame(ref Projectile.frame, 6, 11);
            Projectile.spriteDirection = Projectile.direction = Math.Sign(Projectile.velocity.X);


            if (Math.Abs(Projectile.velocity.X) < 0.5f) {
                Projectile.velocity.X = Main.rand.NextFloat(-2f, 2f);
            }

            rotation = (float)Math.Sin(StateTimer * 0.3f) * 0.15f;

            if (StateTimer % 10 == 0) {
                SpawnWaddleDust();
            }

            if (StateTimer >= 60) {
                State = PenguinState.Disappearing;
                StateTimer = 0;
            }
        }

        private void DisappearingPhase() {
            if (StateTimer == 1) {
                //小雪雾 + 几根羽毛盖住退场，禁 pop-out
                FishPengVFX.SnowBurst(Projectile.Center, 5, 2.2f);
                FishPengVFX.FeatherBurst(Projectile.Center, 3, 2.4f);
                SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.25f, Pitch = 0.5f }, Projectile.Center);
            }

            Projectile.velocity *= 0.9f;
            Projectile.scale = Math.Max(0f, 1f - StateTimer / 20f);
            Projectile.alpha = (int)(255 * StateTimer / 20f);

            if (StateTimer >= 20 || Projectile.scale <= 0.1f) {
                Projectile.Kill();
            }
        }

        private NPC GetTarget() {
            int id = (int)TargetID;
            if (id < 0 || id >= Main.maxNPCs) return null;

            NPC target = Main.npc[id];
            if (!target.active || !target.CanBeChasedBy()) return null;

            return target;
        }

        /// <summary>向下最多 56 格扫实心地面，供阴影预告；纯视觉，服务端跳过</summary>
        private void UpdateGroundCache() {
            if (Main.dedServ) {
                return;
            }
            if (--groundScanCd > 0) {
                return;
            }
            groundScanCd = 4;
            groundY = -1f;
            Point p = Projectile.Bottom.ToTileCoordinates();
            for (int dy = 0; dy < 56; dy++) {
                int ty = p.Y + dy;
                if (!WorldGen.InWorld(p.X, ty, 10)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(p.X, ty);
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    groundY = ty * 16f;
                    break;
                }
            }
        }

        private void CreateImpactEffect(bool first) {
            float ke = first ? 1f : 0.5f;
            Vector2 ground = Projectile.Bottom;

            FishPengVFX.SnowBurst(Projectile.Center, first ? 12 : 6, 4.5f + 6f * ke);
            FishPengVFX.FeatherBurst(Projectile.Center - new Vector2(0f, 6f), first ? 10 : 4, 5.5f * ke);
            FishPengVFX.ImpactRings(ground - new Vector2(0f, 4f), ke);
            if (first) {
                FishPengVFX.GroundPatch(ground);
            }
            FishPengVFX.Punch(Projectile.Center, first ? 4.5f : 2.2f, first ? 8 : 5);

            //Dust 只作雪屑填充底噪：贴地锥形上抛后受重力落回
            int dustN = first ? 12 : 6;
            for (int i = 0; i < dustN; i++) {
                Dust d = Dust.NewDustPerfect(ground + new Vector2(Main.rand.NextFloat(-16f, 16f), -4f)
                    , DustID.Snow, new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-7f, -2f))
                    , 120, FishPengVFX.Snow, Main.rand.NextFloat(1.1f, 1.8f));
                d.noGravity = false;
            }
        }

        private void DamageNearbyEnemies() {
            if (!Projectile.IsOwnedByLocalPlayer()) return;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float distance = Vector2.Distance(Projectile.Center, npc.Center);
                    if (distance < ImpactRadius) {
                        float damageMultiplier = 1f - (distance / ImpactRadius) * 0.5f;
                        int damage = (int)(Projectile.damage * damageMultiplier);

                        Vector2 knockbackDir = (npc.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                        float knockbackForce = Projectile.knockBack * (1.5f - distance / ImpactRadius);

                        npc.SimpleStrikeNPC(damage, Math.Sign(knockbackDir.X),
                            false, knockbackForce);

                        npc.AddBuff(BuffID.Frostburn, 120);
                        npc.AddBuff(BuffID.Slow, 180);

                        if (!VaultUtils.isSinglePlayer) {
                            NetMessage.SendData(MessageID.DamageNPC, -1, -1, null, i, damage,
                                knockbackForce, Math.Sign(knockbackDir.X));
                        }
                    }
                }
            }
        }

        private void SpawnBounceDust() {
            for (int i = 0; i < 3; i++) {
                Dust bounce = Dust.NewDustDirect(
                    Projectile.Bottom - new Vector2(10, 5),
                    20, 5,
                    DustID.Snow,
                    Scale: Main.rand.NextFloat(1f, 1.5f)
                );
                bounce.velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4f, -1f));
            }
        }

        private void SpawnWaddleDust() {
            if (Main.rand.NextBool(2)) {
                Dust waddle = Dust.NewDustDirect(
                    Projectile.Bottom - new Vector2(8, 4),
                    16, 4,
                    DustID.Snow,
                    Scale: Main.rand.NextFloat(0.8f, 1.2f)
                );
                waddle.velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0.5f));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (State == PenguinState.Descending || State == PenguinState.Bouncing) {
                bool hitGround = Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.1f && oldVelocity.Y > 0;

                if (hitGround) {
                    State = PenguinState.Impact;
                    StateTimer = 0;
                    Projectile.velocity.Y = -oldVelocity.Y * 0.5f;
                    bounceCount++;

                    return false;
                }

                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.1f) {
                    Projectile.velocity.X = -oldVelocity.X * 0.6f;
                }
            }

            return false;
        }

        public override void OnKill(int timeLeft) {
            //空中超时消亡也给一小口雪雾，禁 pop-out
            if (State == PenguinState.Falling || State == PenguinState.Descending) {
                FishPengVFX.SnowBurst(Projectile.Center, 4, 2f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 120);
            target.AddBuff(BuffID.Slow, 180);
        }

        /// <summary>落点阴影预告：软阴影椭圆随高度渐缩聚焦 + 冰蓝预告圈收拢</summary>
        private void DrawGroundShadow(SpriteBatch sb, float bodyAlpha) {
            if (groundY < 0f || groundY <= Projectile.Bottom.Y) {
                return;
            }
            //Extra_98 带真 alpha 的软椭圆，AlphaBlend 画暗色不出黑块（SoftGlow 灰度图会）
            Texture2D soft = CWRAsset.Extra_98?.Value;
            if (soft == null) {
                return;
            }
            float dist = groundY - Projectile.Bottom.Y;
            float closeness = 1f - MathHelper.Clamp(dist / 620f, 0f, 1f);
            Vector2 sPos = new Vector2(Projectile.Center.X, groundY - 2f) - Main.screenPosition;

            //软阴影底 + 浓芯
            float wPx = MathHelper.Lerp(112f, 46f, closeness);
            float sA = MathHelper.Lerp(0.14f, 0.46f, closeness * closeness) * bodyAlpha;
            Vector2 softOrigin = soft.Size() * 0.5f;
            sb.Draw(soft, sPos, null, FishPengVFX.ShadowInk * sA, 0f, softOrigin
                , new Vector2(wPx * 1.6f / soft.Width, wPx * 0.42f / soft.Height), SpriteEffects.None, 0f);
            sb.Draw(soft, sPos, null, FishPengVFX.ShadowInk * (sA * 0.8f), 0f, softOrigin
                , new Vector2(wPx * 0.9f / soft.Width, wPx * 0.24f / soft.Height), SpriteEffects.None, 0f);

            //冰蓝预告圈：随企鹅逼近向落点收拢
            Texture2D ring = CWRAsset.Ring01?.Value;
            if (ring != null) {
                float rPx = wPx * MathHelper.Lerp(1.7f, 0.62f, closeness);
                float ringA = (0.16f + 0.30f * closeness) * bodyAlpha;
                sb.Draw(ring, sPos, null, FishPengVFX.IceRing with { A = 0 } * ringA, 0f, ring.Size() * 0.5f
                    , new Vector2(rPx / ring.Width, rPx * 0.30f / ring.Height), SpriteEffects.None, 0f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Main.instance.LoadNPC(NPCID.Penguin);
            Texture2D texture = TextureAssets.Npc[NPCID.Penguin].Value;

            if (texture == null) return false;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle rectangle = texture.GetRectangle(Projectile.frame, 12);
            Vector2 origin = rectangle.Size() / 2f;
            SpriteEffects spriteEffects = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            float bodyAlpha = 1f - Projectile.alpha / 255f;
            Color drawColor = Projectile.GetAlpha(lightColor);
            bool diving = State == PenguinState.Falling || State == PenguinState.Descending;
            float speedT = MathHelper.Clamp(Projectile.velocity.Length() / MaxFallSpeed, 0f, 1f);

            //层 1：落点阴影预告，画在一切之下
            if (diving) {
                DrawGroundShadow(sb, bodyAlpha);
            }

            //层 2：凝结尾迹（活体），画在企鹅精灵之下
            if (diving) {
                float contrailStrength = MathHelper.Clamp((Projectile.velocity.Length() - 8f) / 16f, 0f, 1f) * bodyAlpha;
                FishPengVFX.DrawLiveContrail(sb, Projectile, contrailStrength);
            }

            //挤压拉伸与入场缩放
            Vector2 scaleVec = Vector2.One;
            float entryScale = 1f;
            if (diving) {
                //俯冲：沿速度拉伸；高空时读作小黑点，逼近后放大成形
                scaleVec = new Vector2(1f - 0.20f * speedT, 1f + 0.40f * speedT);
                float closeness = groundY > 0f ? 1f - MathHelper.Clamp((groundY - Projectile.Bottom.Y) / 620f, 0f, 1f) : 0f;
                //入场年龄由 timeLeft 推导（初值 600），高空小黑点在 45 帧内长成机体
                float ageGrow = MathHelper.Clamp((600f - Projectile.timeLeft) / 45f, 0f, 1f);
                entryScale = MathHelper.Lerp(0.6f, 1f, Math.Max(closeness, ageGrow));
                //高空剪影：远处的小黑点比机体更暗
                drawColor = Color.Lerp(drawColor, FishPengVFX.ShadowInk, (1f - Math.Max(closeness, ageGrow)) * 0.5f);
                //贴近音障的冷雾染面
                drawColor = Color.Lerp(drawColor, FishPengVFX.Mist, speedT * 0.22f);
            }
            else if (State == PenguinState.Impact) {
                //着陆压扁与过冲回弹
                float k = MathHelper.Clamp(StateTimer / 9f, 0f, 1f);
                float over = FishPengVFX.EaseOutBack(k);
                scaleVec = new Vector2(MathHelper.Lerp(1.35f, 1f, over), MathHelper.Lerp(0.55f, 1f, over));
            }
            else if (State == PenguinState.Bouncing) {
                float s2 = MathHelper.Clamp(Projectile.velocity.Length() / 14f, 0f, 1f);
                scaleVec = new Vector2(1f - 0.10f * s2, 1f + 0.20f * s2);
            }
            else if (State == PenguinState.Waddling) {
                //踏步果冻感
                float jig = 0.05f * (float)Math.Sin(StateTimer * 0.55f);
                scaleVec = new Vector2(1f + jig, 1f - jig);
            }

            float drawScale = Projectile.scale * entryScale;

            //层 3：俯冲速度残影链（哑光，非加色）
            if (diving && Projectile.oldPos != null && Projectile.oldPos.Length > 9 && speedT > 0.3f) {
                Vector2 half = Projectile.Size * 0.5f;
                for (int k = 0; k < 2; k++) {
                    int idx = k == 0 ? 4 : 8;
                    if (Projectile.oldPos[idx] == Vector2.Zero) {
                        break;
                    }
                    float ga = (k == 0 ? 0.30f : 0.14f) * speedT * bodyAlpha;
                    sb.Draw(texture, Projectile.oldPos[idx] + half - Main.screenPosition, rectangle
                        , FishPengVFX.Mist * ga, rotation, origin, drawScale * scaleVec, spriteEffects, 0f);
                }
            }

            //层 4：翻滚期旋转拖影（位置残影表达不了自旋）
            if (Math.Abs(rotationSpeed) > 0.05f) {
                sb.Draw(texture, drawPos, rectangle, drawColor * 0.28f, rotation - rotationSpeed * 2.5f
                    , origin, drawScale * scaleVec, spriteEffects, 0f);
            }

            //层 5：企鹅本体
            sb.Draw(texture, drawPos, rectangle, drawColor, rotation, origin, drawScale * scaleVec, spriteEffects, 0f);

            //层 6：着陆 2 帧过冲爆点（A=0 加色观感，绝不常驻）
            if (State == PenguinState.Impact && StateTimer <= 2 && bounceCount <= 1) {
                Texture2D burst = CWRAsset.RayBurst01?.Value;
                if (burst != null) {
                    float bA = StateTimer <= 1 ? 0.85f : 0.4f;
                    float bS = StateTimer <= 1 ? 0.5f : 0.62f;
                    sb.Draw(burst, Projectile.Bottom - new Vector2(0f, 8f) - Main.screenPosition, null
                        , FishPengVFX.Core with { A = 0 } * bA, Projectile.whoAmI * 1.7f, burst.Size() * 0.5f
                        , bS, SpriteEffects.None, 0f);
                }
            }

            return false;
        }
    }
}
