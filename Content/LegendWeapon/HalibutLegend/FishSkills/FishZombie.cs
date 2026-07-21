using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishZombie : FishSkill
    {
        public override int UnlockFishID => ItemID.ZombieFish;

        /// <summary>
        /// 可召唤的溺尸数量
        /// </summary>
        public virtual int ZombieCount => 5 + 1 * HalibutData.GetDomainLayer();//5+1倍领域等级

        public override int DefaultCooldown => 60 * (12 - HalibutData.GetDomainLayer());
        public override int ResearchDuration => 60 * 12;
        public override bool? AltFunctionUse(Item item, Player player) {
            return true;
        }

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (Cooldown > 0) {
                    return false;
                }

                item.UseSound = null;
                Use(item, player);
                return false;
            }

            return base.CanUseItem(item, player);
        }

        public override void Use(Item item, Player player) {
            HalibutPlayer halibutPlayer = player.GetOverride<HalibutPlayer>();

            SetCooldown();

            Vector2 targetDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero);

            ShootState shootState = player.GetShootState();

            for (int i = 0; i < ZombieCount; i++) {
                float angleSpread = MathHelper.ToRadians(60f); //60度扇形
                float angle = targetDirection.ToRotation() + Main.rand.NextFloat(-angleSpread, angleSpread);
                float distance = Main.rand.NextFloat(150f, 300f);

                Vector2 spawnOffset = new(
                    (float)Math.Cos(angle) * distance,
                    (float)Math.Sin(angle) * distance
                );

                Vector2 spawnPos = player.Center + spawnOffset;

                Vector2 groundPos = FindGroundPosition(spawnPos);

                //延迟入场，每尸+8帧
                int delay = i * 8; //每个溺尸延迟8帧

                Projectile.NewProjectile(
                    player.GetSource_ItemUse(item),
                    groundPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<WaterZombie>(),
                    (int)(shootState.WeaponDamage * (1.75f + HalibutData.GetDomainLayer() * 0.45f)),//伤害倍率
                    shootState.WeaponKnockback,
                    player.whoAmI,
                    ai0: delay //延迟帧数
                );
            }

            SoundEngine.PlaySound(SoundID.Zombie1 with { Volume = 0.8f, Pitch = -0.3f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item8, player.Center);
        }

        /// <summary>
        /// 寻找地面位置（向下检测）
        /// </summary>
        private static Vector2 FindGroundPosition(Vector2 startPos) {
            for (int y = 0; y < 500; y += 16) {
                Vector2 checkPos = startPos + new Vector2(0, y);
                Point tilePos = checkPos.ToTileCoordinates();

                if (tilePos.X >= 0 && tilePos.X < Main.maxTilesX && tilePos.Y >= 0 && tilePos.Y < Main.maxTilesY) {
                    Tile tile = Main.tile[tilePos.X, tilePos.Y];
                    if (tile != null && tile.HasTile && Main.tileSolid[tile.TileType]) {
                        //找到地面，返回地面上方位置
                        return new Vector2(checkPos.X, tilePos.Y * 16 - 8);
                    }
                }
            }

            return startPos;
        }
    }

    /// <summary>溺尸弹幕，破土爬出 → 蹒跚寻敌 → 锁定倾身 → 前倾狂奔 → 尸胀爆裂</summary>
    internal class WaterZombie : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>
        /// 延迟帧数（等待后才开始出现）
        /// </summary>
        private ref float DelayTime => ref Projectile.ai[0];

        /// <summary>
        /// 状态机，0=破土爬出，1=蹒跚寻敌，2=锁定+冲刺，3=尸胀爆裂
        /// </summary>
        private int State {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        /// <summary>
        /// 状态计时器
        /// </summary>
        private ref float StateTimer => ref Projectile.localAI[1];

        /// <summary>
        /// 目标NPC索引
        /// </summary>
        private int targetNPC = -1;

        /// <summary>
        /// 从地下爬出持续时间
        /// </summary>
        private const int EmergeDuration = 30;

        /// <summary>
        /// 蹒跚基准时长，叠加 whoAmI 抖动让尸群波次不齐
        /// </summary>
        private const int SeekDuration = 20;

        /// <summary>锁定预告拍，刹停+倾身</summary>
        private const int LockDuration = 10;

        /// <summary>
        /// 冲刺持续时间
        /// </summary>
        private const int ChargeDuration = 60;

        /// <summary>尸胀帧数，躯体鼓胀挣动后爆开</summary>
        private const int InflateDuration = 8;

        /// <summary>
        /// 最大生存时间
        /// </summary>
        private const int MaxLifeTime = 600;

        /// <summary>僵尸贴图单帧高</summary>
        private const float SpriteHeight = 58f;

        private int animationFrame;
        private float stepPhase;//步频相位，推进速度自身波动出蹒跚感
        private int facing = -1;//贴图原朝左，1=右（翻转绘制）
        private float bodyRot;
        private Vector2 bodySquash = Vector2.One;
        private float groundY;//出土地面线
        private float breachX;//破口横坐标，洞口暗斑锚点
        private bool groundInit;
        private bool bursted;
        private int shambleDur;
        private float holeFade = 1f;

        private float FeetY {
            get => Projectile.position.Y + Projectile.height;
            set => Projectile.position.Y = value - Projectile.height;
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 38;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLifeTime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            if (DelayTime > 0) {
                DelayTime--;
                Projectile.alpha = 255;
                return;
            }
            Projectile.alpha = 0;

            if (!groundInit) {
                groundInit = true;
                groundY = Projectile.Center.Y + 8f;//生成点在砖顶上方8像素
                breachX = Projectile.Center.X;
                shambleDur = SeekDuration + Projectile.whoAmI * 7 % 17;
                facing = Main.player[Projectile.owner].direction;
                if (facing == 0) {
                    facing = 1;
                }
                //整具埋入地面线之下，靠裁剪绘制逐帧拔出
                FeetY = groundY + SpriteHeight;
                Projectile.velocity = Vector2.Zero;
            }

            switch (State) {
                case 0: //破土爬出
                    EmergeFromGroundAI();
                    break;
                case 1: //蹒跚寻敌
                    ShambleAI();
                    break;
                case 2: //锁定+冲刺
                    LockChargeAI();
                    break;
                case 3: //尸胀爆裂
                    BloatExplodeAI();
                    break;
            }

            if (State != 0 && holeFade > 0f) {
                holeFade -= 0.04f;
            }

            Projectile.rotation = bodyRot;//喂给残影缓存
        }

        /// <summary>拔出曲线，缓探头 → 卡住 → 猛拔过冲 → 落定</summary>
        private static float EmergeCurve(float p) {
            if (p < 0.30f) {
                float t = p / 0.30f;
                return 0.24f * (t * t * (3f - 2f * t));
            }
            if (p < 0.46f) {
                return 0.24f + 0.05f * ((p - 0.30f) / 0.16f);
            }
            if (p < 0.80f) {
                float t = (p - 0.46f) / 0.34f;
                return 0.29f + 0.75f * t * t;
            }
            float u = (p - 0.80f) / 0.20f;
            return MathHelper.Lerp(1.04f, 1f, u * u * (3f - 2f * u));
        }

        /// <summary>爬出 tick</summary>
        private void EmergeFromGroundAI() {
            StateTimer++;
            float p = StateTimer / (float)EmergeDuration;
            float rise = EmergeCurve(p);
            FeetY = groundY + SpriteHeight * (1f - rise);
            Projectile.velocity = Vector2.Zero;
            animationFrame = 0;

            //出土挣动微晃，越接近拔出越明显
            bodyRot = MathF.Sin(StateTimer * 0.9f + Projectile.whoAmI) * 0.035f * p;
            bodySquash = Vector2.One;

            bool hasDirt = HasSolidGround();
            if (p < 0.72f && Main.rand.NextBool(hasDirt ? 2 : 4)) {
                if (hasDirt) {
                    //破口两侧土块抛物
                    float side = Main.rand.NextBool() ? -1f : 1f;
                    Dust dirt = Dust.NewDustPerfect(
                        new Vector2(Projectile.Center.X + side * Main.rand.NextFloat(4f, 16f), groundY),
                        DustID.Dirt,
                        new Vector2(side * Main.rand.NextFloat(0.8f, 2.8f), Main.rand.NextFloat(-4.2f, -1.6f)),
                        30, default, Main.rand.NextFloat(0.9f, 1.5f));
                    dirt.noGravity = false;
                }
                else {
                    //水面或悬空生成时改用浊雾盖住破口
                    PRTLoader.NewParticle<PRT_FishZombieMurk>(
                        new Vector2(Projectile.Center.X, groundY), new Vector2(0f, -0.4f),
                        FishZombieVFX.MurkMid, 0.14f)
                        ?.Configure(26, FishZombieVFX.MurkMid, FishZombieVFX.MurkDeep, 1.008f, 0.012f);
                }
            }

            //已露出的躯体持续滴浊水
            if (rise > 0.25f && Main.rand.NextBool(4)) {
                DripFromBody(0.4f);
            }

            //猛拔瞬间，甩水一环 + 出土音效
            if ((int)StateTimer == EmergeDuration / 2) {
                SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);
                FishZombieVFX.ShakeOff(new Vector2(Projectile.Center.X, groundY - 20f), 7, 3.4f);
            }

            if (StateTimer >= EmergeDuration) {
                State = 1;
                StateTimer = 0;
                FeetY = groundY;
                //落定小水花
                FishZombieVFX.ShakeOff(new Vector2(Projectile.Center.X, groundY - 10f), 4, 2.2f);
            }
        }

        /// <summary>蹒跚 tick，不规则步频 + 摇晃 + 滴水，走完预定拍才进锁定</summary>
        private void ShambleAI() {
            StateTimer++;

            //每4帧扫一次最近敌人
            if (targetNPC == -1 && (int)StateTimer % 4 == 0) {
                var npc = Projectile.Center.FindClosestNPC(1800f, true, true);
                if (npc != null) {
                    targetNPC = npc.whoAmI;
                }
            }
            if (targetNPC != -1 && !Main.npc[targetNPC].active) {
                targetNPC = -1;
            }

            //步频相位的推进速度自身在波动
            float phaseSpeed = 0.11f + 0.10f * MathF.Sin(StateTimer * 0.047f + Projectile.whoAmI * 2.7f);
            stepPhase += phaseSpeed;
            float pulse = MathF.Max(0f, MathF.Sin(stepPhase));
            float lurch = pulse * pulse;

            int dir = targetNPC != -1
                ? Math.Sign(Main.npc[targetNPC].Center.X - Projectile.Center.X)
                : Main.player[Projectile.owner].direction;
            if (dir == 0) {
                dir = facing;
            }
            facing = dir;

            Projectile.velocity.X = dir * (0.25f + 1.35f * lurch);
            Projectile.velocity.Y = 0f;
            GroundFollow();

            //蹒跚的身体从不竖直
            bodyRot = MathF.Sin(stepPhase * 0.5f + Projectile.whoAmI) * 0.075f + facing * 0.04f;
            bodySquash = new Vector2(1f + lurch * 0.03f, 1f - lurch * 0.05f);
            animationFrame = (int)(stepPhase / MathHelper.Pi) % 3;

            //湿身滴水
            if (Main.rand.NextBool(7)) {
                DripFromBody();
            }

            if (StateTimer >= shambleDur) {
                State = 2;
                StateTimer = 0;
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);
            }
        }

        /// <summary>贴地跟随，脚下小范围找砖顶吸附，悬空缓沉，仅在蹒跚期作视觉修正</summary>
        private void GroundFollow() {
            int tx = (int)(Projectile.Center.X / 16f);
            int startTy = (int)((FeetY - 22f) / 16f);
            for (int ty = startTy; ty <= startTy + 4; ty++) {
                if (!WorldGen.InWorld(tx, ty, 10)) {
                    return;
                }
                Tile t = Framing.GetTileSafely(tx, ty);
                if (t.HasTile && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType])) {
                    float top = ty * 16f;
                    if (top < FeetY - 26f) {
                        return;//高坎不硬爬
                    }
                    FeetY = MathHelper.Lerp(FeetY, top, 0.35f);
                    return;
                }
            }
            FeetY += 2.2f;//悬空缓沉
        }

        /// <summary>锁定+冲刺 tick，前 LockDuration 帧刹停倾身预告，随后扑出</summary>
        private void LockChargeAI() {
            StateTimer++;

            Vector2 targetPosition = Main.MouseWorld;
            if (targetNPC != -1 && Main.npc[targetNPC].active) {
                targetPosition = Main.npc[targetNPC].Center;
            }

            if (StateTimer <= LockDuration) {
                //锁定拍，刹停 + 朝目标压低倾身
                Projectile.velocity *= 0.70f;
                int dir = Math.Sign(targetPosition.X - Projectile.Center.X);
                if (dir != 0) {
                    facing = dir;
                }

                bodyRot = MathHelper.Lerp(bodyRot, facing * 0.26f, 0.28f);
                bodySquash = Vector2.Lerp(bodySquash, new Vector2(1.06f, 0.92f), 0.3f);
                animationFrame = 0;

                if ((int)StateTimer == 2) {
                    //甩水预告，湿身抖出一圈浊水
                    FishZombieVFX.ShakeOff(Projectile.Center, 6, 3.8f);
                    SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.55f, Pitch = -0.3f }, Projectile.Center);
                }
                if ((int)StateTimer == LockDuration) {
                    //起跑帧，一帧给足初速
                    Projectile.velocity = (targetPosition - Projectile.Center).SafeNormalize(Vector2.UnitX * facing) * 7.5f;
                }
                return;
            }

            float ct = StateTimer - LockDuration;
            Vector2 chargeDirection = (targetPosition - Projectile.Center).SafeNormalize(Vector2.Zero);

            //复合加速，前段猛蹬后段续力
            if (ct < 8f) {
                Projectile.velocity += chargeDirection * 2.2f;
            }
            else if (ct < 20f) {
                Projectile.velocity += chargeDirection * 1.1f;
            }

            float maxChargeSpeed = 20f;
            if (Projectile.velocity.Length() > maxChargeSpeed) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxChargeSpeed;
            }

            if (Projectile.velocity.X != 0) {
                facing = Math.Sign(Projectile.velocity.X);
            }

            //前倾扑咬角
            float speed = Projectile.velocity.Length();
            float pounce = facing == 1
                ? Projectile.velocity.ToRotation()
                : MathHelper.WrapAngle(MathHelper.Pi - Projectile.velocity.ToRotation());
            float leanFull = MathHelper.Clamp(pounce, -1.1f, 1.1f) * 0.5f + 0.35f;
            bodyRot = MathHelper.Lerp(bodyRot, facing * leanFull, 0.18f);
            bodySquash = Vector2.Lerp(bodySquash, new Vector2(0.96f, 1.04f), 0.2f);

            //狂奔帧，速度越快循环越快
            int frameRate = Math.Max(2, 7 - (int)(speed * 0.25f));
            if ((int)StateTimer % frameRate == 0) {
                animationFrame = (animationFrame + 1) % 3;
            }

            //向后甩水 + 浊雾尾
            int shed = speed > 14f ? 2 : 1;
            for (int i = 0; i < shed; i++) {
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(9f, 15f);
                FishZombieVFX.Drip(pos, -Projectile.velocity * Main.rand.NextFloat(0.10f, 0.22f)
                    + new Vector2(0f, Main.rand.NextFloat(-0.5f, 0.8f)));
            }
            if ((int)StateTimer % 3 == 0 && speed > 6f) {
                PRTLoader.NewParticle<PRT_FishZombieMurk>(
                    Projectile.Center - Projectile.velocity * 0.6f, -Projectile.velocity * 0.05f,
                    FishZombieVFX.MurkMid, Main.rand.NextFloat(0.13f, 0.21f))
                    ?.Configure(Main.rand.Next(18, 28), FishZombieVFX.MurkMid, FishZombieVFX.MurkDeep, 1.006f, -0.010f);
            }

            if (ct >= ChargeDuration) {
                //冲刺结束，进入爆裂
                State = 3;
                StateTimer = 0;
            }
        }

        /// <summary>尸胀爆裂 tick，鼓胀挣动 InflateDuration 帧后爆开</summary>
        private void BloatExplodeAI() {
            StateTimer++;
            Projectile.velocity *= 0.62f;

            if ((int)StateTimer == 1) {
                //临爆哽咽
                SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }

            if (StateTimer <= InflateDuration) {
                //尸胀，越鼓越快，表皮挣动
                float sw = StateTimer / (float)InflateDuration;
                float wobble = MathF.Sin(StateTimer * 1.7f) * 0.05f * sw;
                bodySquash = new Vector2(1f + sw * sw * 0.24f + wobble, 1f + sw * sw * 0.18f - wobble);
                bodyRot = MathHelper.Lerp(bodyRot, 0f, 0.25f);

                //缝隙漏气
                if ((int)StateTimer % 2 == 0) {
                    PRTLoader.NewParticle<PRT_FishZombieMurk>(
                        Projectile.Center + Main.rand.NextVector2Circular(10f, 16f), new Vector2(0f, -0.7f),
                        FishZombieVFX.GasOlive, Main.rand.NextFloat(0.10f, 0.16f))
                        ?.Configure(Main.rand.Next(16, 26), FishZombieVFX.GasOlive, FishZombieVFX.GasDeep, 1.010f, 0.016f);
                    DripFromBody();
                }
            }

            if ((int)StateTimer == InflateDuration) {
                bursted = true;

                //伤害判定与旧版一致
                Projectile.Explode(220, default, false);

                FishZombieVFX.BloatBurst(Projectile.Center);
                FishZombieVFX.Punch(Projectile.Center);
                SpawnGoreChunks();

                SoundEngine.PlaySound(SoundID.NPCDeath1, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 1.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
            }

            if (StateTimer >= InflateDuration + 4) {
                Projectile.Kill();
            }
        }

        /// <summary>尸块抛物，原版僵尸 Gore，带上抛偏置读出重力弧线</summary>
        private void SpawnGoreChunks() {
            if (VaultUtils.isServer) {
                return;
            }
            int mainGoreCount = Main.rand.Next(3, 6);
            for (int i = 0; i < mainGoreCount; i++) {
                int goreType = Main.rand.Next(11, 14);//僵尸头/臂/腿
                Vector2 goreVel = Main.rand.NextVector2CircularEdge(5f, 5f);
                goreVel.Y -= 1.5f;
                Gore.NewGore(
                    Projectile.GetSource_Death(),
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    goreVel,
                    goreType
                );
            }
        }

        /// <summary>从躯体随机位置滴一滴浊水，出土期只从露出部分滴</summary>
        private void DripFromBody(float velScale = 1f) {
            Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-20f, 14f));
            if (State == 0 && pos.Y > groundY - 4f) {
                pos.Y = groundY - 4f;
            }
            FishZombieVFX.Drip(pos, Projectile.velocity * 0.35f * velScale
                + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.4f, 1.3f)));
        }

        /// <summary>破口下方是否有实体地面（决定破土介质用土还是雾）</summary>
        private bool HasSolidGround() {
            Point p = new Vector2(Projectile.Center.X, groundY + 8f).ToTileCoordinates();
            if (!WorldGen.InWorld(p.X, p.Y, 10)) {
                return false;
            }
            Tile t = Framing.GetTileSafely(p.X, p.Y);
            return t.HasTile && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]);
        }

        /// <summary>
        /// 碰撞检测
        /// </summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //只在冲刺状态造成伤害
            if (State == 2) {
                return base.Colliding(projHitbox, targetHitbox);
            }
            return false;
        }

        /// <summary>
        /// 击中NPC后立即转入尸胀爆裂
        /// </summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            State = 3;
            StateTimer = 0;
        }

        public override void OnKill(int timeLeft) {
            if (bursted) {
                return;
            }
            //未爆而亡的退场保底
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishZombieMurk>(Projectile.Center, new Vector2(0f, -0.3f)
                    , FishZombieVFX.MurkMid, 0.2f)?.Configure(30, FishZombieVFX.MurkMid, FishZombieVFX.MurkDeep, 1.010f, 0.008f);
            }
            FishZombieVFX.ShakeOff(Projectile.Center, 4, 2.5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (DelayTime > 0 || bursted || !groundInit) {
                return false;
            }

            //加载僵尸纹理
            Main.instance.LoadNPC(NPCID.Zombie);
            Texture2D texture = TextureAssets.Npc[NPCID.Zombie].Value;
            Rectangle sourceRect = texture.GetRectangle(animationFrame, 3);
            SpriteEffects effects = facing > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //浸水腐肉调色
            Color env = State == 0
                ? Lighting.GetColor(new Vector2(Projectile.Center.X, groundY - 20f).ToTileCoordinates())
                : lightColor;
            Color body = env.MultiplyRGB(new Color(150, 172, 160));
            body = Color.Lerp(body, FishZombieVFX.FleshSoak, 0.22f);

            DrawBreachHole();

            if (State == 0) {
                DrawEmerging(texture, sourceRect, body, effects);
                return false;
            }

            Vector2 feetScreen = new Vector2(Projectile.Center.X, FeetY) - Main.screenPosition;
            Vector2 origin = new(sourceRect.Width / 2f, sourceRect.Height);

            //冲刺残影
            if (State == 2 && Projectile.velocity.Length() > 8f) {
                for (int k = 5; k >= 1; k -= 2) {
                    Vector2 ghostFeet = Projectile.oldPos[k] + new Vector2(Projectile.width / 2f, Projectile.height);
                    float ga = 0.26f - k * 0.038f;
                    Main.EntitySpriteDraw(texture, ghostFeet - Main.screenPosition, sourceRect,
                        FishZombieVFX.FleshDark * ga, Projectile.oldRot[k], origin, bodySquash, effects, 0);
                }
            }

            //主体
            Main.EntitySpriteDraw(texture, feetScreen, sourceRect, body, bodyRot, origin, bodySquash, effects, 0);

            //下半身浸水更深，底部四成再压一层暗青
            int soakH = (int)(sourceRect.Height * 0.42f);
            Rectangle soakRect = new(sourceRect.X, sourceRect.Y + sourceRect.Height - soakH, sourceRect.Width, soakH);
            Vector2 soakOrigin = new(sourceRect.Width / 2f, soakH);
            Main.EntitySpriteDraw(texture, feetScreen, soakRect,
                FishZombieVFX.MurkDeep * 0.30f, bodyRot, soakOrigin, bodySquash, effects, 0);

            return false;
        }

        /// <summary>出土期绘制</summary>
        private void DrawEmerging(Texture2D texture, Rectangle sourceRect, Color body, SpriteEffects effects) {
            float buried = FeetY - groundY;
            if (buried <= 0f) {
                //过冲小跳帧，已完全离地，整帧正常画
                Vector2 fullFeet = new Vector2(Projectile.Center.X, FeetY) - Main.screenPosition;
                Main.EntitySpriteDraw(texture, fullFeet, sourceRect, body, bodyRot,
                    new Vector2(sourceRect.Width / 2f, sourceRect.Height), Vector2.One, effects, 0);
                return;
            }

            int visibleH = (int)(sourceRect.Height - buried);
            if (visibleH <= 0) {
                return;
            }

            Rectangle clipRect = new(sourceRect.X, sourceRect.Y, sourceRect.Width, visibleH);
            Vector2 anchor = new Vector2(Projectile.Center.X, groundY) - Main.screenPosition;
            Vector2 clipOrigin = new(sourceRect.Width / 2f, visibleH);
            Main.EntitySpriteDraw(texture, anchor, clipRect, body, bodyRot, clipOrigin, Vector2.One, effects, 0);
        }

        /// <summary>破口暗斑，垫在尸体之下的湿土洞口，出土后渐淡</summary>
        private void DrawBreachHole() {
            if (holeFade <= 0f) {
                return;
            }
            Texture2D blob = FishZombieAssets.Blob?.Value;
            if (blob == null) {
                return;
            }
            float fadeIn = State == 0 ? MathF.Min(1f, StateTimer / 6f) : 1f;
            float a = 0.45f * holeFade * fadeIn;
            Vector2 holePos = new Vector2(breachX, groundY) - Main.screenPosition;
            Vector2 orig = blob.Size() * 0.5f;
            Main.EntitySpriteDraw(blob, holePos, null, new Color(14, 20, 20) * a, 0f,
                orig, new Vector2(0.68f, 0.13f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(blob, holePos, null, new Color(24, 34, 34) * (a * 0.6f), 0f,
                orig, new Vector2(1.0f, 0.09f), SpriteEffects.None, 0);
        }
    }
}
