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
                //检查冷却
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

            //设置冷却
            SetCooldown();

            //计算目标方向（朝向光标）
            Vector2 targetDirection = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.Zero);

            ShootState shootState = player.GetShootState();

            //在玩家前方生成多个溺尸
            for (int i = 0; i < ZombieCount; i++) {
                //计算生成位置：在光标方向的扇形区域内
                float angleSpread = MathHelper.ToRadians(60f); //60度扇形
                float angle = targetDirection.ToRotation() + Main.rand.NextFloat(-angleSpread, angleSpread);
                float distance = Main.rand.NextFloat(150f, 300f);

                Vector2 spawnOffset = new(
                    (float)Math.Cos(angle) * distance,
                    (float)Math.Sin(angle) * distance
                );

                Vector2 spawnPos = player.Center + spawnOffset;

                //寻找地面位置
                Vector2 groundPos = FindGroundPosition(spawnPos);

                //生成溺尸弹幕，稍微延迟生成以产生连续出现的效果
                int delay = i * 8; //每个溺尸延迟8帧

                Projectile.NewProjectile(
                    player.GetSource_ItemUse(item),
                    groundPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<WaterZombie>(),
                    (int)(shootState.WeaponDamage * (2.5f + HalibutData.GetDomainLayer() * 1.25f)),//伤害倍率
                    shootState.WeaponKnockback,
                    player.whoAmI,
                    ai0: delay //延迟帧数
                );
            }

            //播放召唤音效
            SoundEngine.PlaySound(SoundID.Zombie1 with { Volume = 0.8f, Pitch = -0.3f }, player.Center);
            SoundEngine.PlaySound(SoundID.Item8, player.Center); //魔法召唤音效
        }

        /// <summary>
        /// 寻找地面位置（向下检测）
        /// </summary>
        private static Vector2 FindGroundPosition(Vector2 startPos) {
            //向下检测最多500像素
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

            //如果没找到地面，返回原位置
            return startPos;
        }
    }

    /// <summary>
    /// 溺尸弹幕 - 从地下爬出，冲向敌人并爆炸
    /// </summary>
    internal class WaterZombie : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        /// <summary>
        /// 延迟帧数（等待后才开始出现）
        /// </summary>
        private ref float DelayTime => ref Projectile.ai[0];

        /// <summary>
        /// 状态机：0=延迟等待，1=从地下爬出，2=寻找目标，3=冲刺攻击，4=爆炸
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
        /// 寻找目标持续时间
        /// </summary>
        private const int SeekDuration = 20;

        /// <summary>
        /// 冲刺持续时间
        /// </summary>
        private const int ChargeDuration = 60;

        /// <summary>
        /// 最大生存时间
        /// </summary>
        private const int MaxLifeTime = 600;

        /// <summary>
        /// 动画帧
        /// </summary>
        private int animationFrame = 0;
        private int animationTimer = 0;

        /// <summary>
        /// 透明度（用于渐隐渐现）
        /// </summary>
        private float alpha = 0f;

        /// <summary>
        /// 缩放比例
        /// </summary>
        private float scale = 0.8f;

        /// <summary>
        /// 地面粒子效果强度
        /// </summary>
        private float groundEffectIntensity = 0f;

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
            //延迟等待阶段
            if (DelayTime > 0) {
                DelayTime--;
                Projectile.alpha = 255;
                return;
            }

            //状态机
            switch (State) {
                case 0: //从地下爬出
                    EmergeFromGroundAI();
                    break;
                case 1: //寻找目标
                    SeekTargetAI();
                    break;
                case 2: //冲刺攻击
                    ChargeAttackAI();
                    break;
                case 3: //爆炸
                    ExplodeAI();
                    break;
            }

            //更新动画
            animationTimer++;
            if (animationTimer >= 8) {
                animationTimer = 0;
                animationFrame = (animationFrame + 1) % 3;
            }

            if (State != 2) {
                Projectile.velocity.X = Main.player[Projectile.owner].direction > 0 ? 1 : -1;
            }
        }

        /// <summary>
        /// 从地下爬出AI
        /// </summary>
        private void EmergeFromGroundAI() {
            StateTimer++;

            //计算出现进度（0-1）
            float emergeProgress = StateTimer / EmergeDuration;

            //透明度渐显
            alpha = MathHelper.Lerp(0f, 1f, emergeProgress);
            Projectile.alpha = (int)((1f - alpha) * 255f);

            //缩放效果（从小到正常）
            scale = MathHelper.Lerp(0.5f, 1f, EaseOutBack(emergeProgress));

            //地面粒子效果强度
            groundEffectIntensity = MathHelper.Lerp(1f, 0f, emergeProgress);

            //从地下爬出：Y位置向上移动
            float riseDistance = 60f;
            Projectile.position.Y -= riseDistance / EmergeDuration;

            if (StateTimer < EmergeDuration * 0.8f) {
                //初期（0-40%）：地面开裂效果
                if (emergeProgress < 0.4f) {
                    //小型泥土颗粒从裂缝中溅出
                    if (Main.rand.NextBool(3)) {
                        Vector2 dustPos = Projectile.Bottom + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-5f, 2f));
                        float xVel = Main.rand.NextFloat(-1.5f, 1.5f);
                        float yVel = Main.rand.NextFloat(-3f, -1f);

                        Dust dirt = Dust.NewDustPerfect(
                            dustPos,
                            DustID.Dirt,
                            new Vector2(xVel, yVel),
                            Scale: Main.rand.NextFloat(0.8f, 1.3f)
                        );
                        dirt.noGravity = false;
                        dirt.fadeIn = 0.8f;
                    }

                    //地面裂缝烟尘（灰色烟雾）
                    if (Main.rand.NextBool(5)) {
                        Dust smoke = Dust.NewDustPerfect(
                            Projectile.Bottom + new Vector2(Main.rand.NextFloat(-10f, 10f), 0),
                            DustID.Smoke,
                            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.5f, -0.5f)),
                            Scale: Main.rand.NextFloat(0.6f, 1f),
                            Alpha: 120
                        );
                        smoke.noGravity = true;
                        smoke.fadeIn = 0.5f;
                    }
                }

                //中期（40-70%）：强力爬出，泥土飞溅
                if (emergeProgress >= 0.4f && emergeProgress < 0.7f) {
                    //中型泥土块从两侧飞出
                    if (Main.rand.NextBool(4)) {
                        float side = Main.rand.NextBool() ? -1f : 1f;
                        Vector2 ejectVel = new Vector2(
                            side * Main.rand.NextFloat(2.5f, 4.5f),
                            Main.rand.NextFloat(-4f, -2.5f)
                        );

                        Dust dirtChunk = Dust.NewDustPerfect(
                            Projectile.Bottom + new Vector2(side * Main.rand.NextFloat(5f, 15f), 0),
                            DustID.Dirt,
                            ejectVel,
                            Scale: Main.rand.NextFloat(1f, 1.6f)
                        );
                        dirtChunk.noGravity = false;
                    }

                    //石块碎片
                    if (Main.rand.NextBool(6)) {
                        float side = Main.rand.NextBool() ? -1f : 1f;
                        Dust stone = Dust.NewDustPerfect(
                            Projectile.Bottom,
                            DustID.Stone,
                            new Vector2(side * Main.rand.NextFloat(2f, 4f), Main.rand.NextFloat(-3.5f, -2f)),
                            Scale: Main.rand.NextFloat(0.7f, 1.2f)
                        );
                        stone.noGravity = false;
                    }

                    //溺尸特色：水珠从身体滴落
                    if (Main.rand.NextBool(4)) {
                        Vector2 waterDropPos = Projectile.Center + new Vector2(
                            Main.rand.NextFloat(-10f, 10f),
                            Main.rand.NextFloat(-15f, 5f)
                        );
                        Dust waterDrop = Dust.NewDustPerfect(
                            waterDropPos,
                            DustID.Water,
                            new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(1f, 2.5f)),
                            Scale: Main.rand.NextFloat(0.6f, 1.1f),
                            Alpha: 80
                        );
                        waterDrop.noGravity = false;
                    }
                }

                //后期（70-80%）：尘埃落定
                if (emergeProgress >= 0.7f) {
                    //细小尘埃漂浮
                    if (Main.rand.NextBool(6)) {
                        Dust dustFloat = Dust.NewDustPerfect(
                            Projectile.Bottom + new Vector2(Main.rand.NextFloat(-15f, 15f), Main.rand.NextFloat(-5f, 0)),
                            DustID.Smoke,
                            new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.8f, -0.2f)),
                            Scale: Main.rand.NextFloat(0.4f, 0.7f),
                            Alpha: 150
                        );
                        dustFloat.noGravity = true;
                        dustFloat.fadeIn = 0.3f;
                    }

                    //水汽蒸发效果（青绿色）
                    if (Main.rand.NextBool(8)) {
                        Dust mist = Dust.NewDustPerfect(
                            Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                            DustID.DungeonWater,
                            new Vector2(0, Main.rand.NextFloat(-0.5f, 0)),
                            Scale: Main.rand.NextFloat(0.5f, 0.9f),
                            Alpha: 100
                        );
                        mist.noGravity = true;
                        mist.color = Color.Lerp(Color.Cyan, Color.LightGreen, 0.6f);
                    }
                }
            }

            if ((int)(emergeProgress * 100) == 30) {
                //地面冲击环
                for (int i = 0; i < 12; i++) {
                    float angle = MathHelper.TwoPi * i / 12f;
                    Vector2 shockDir = new Vector2(
                        (float)Math.Cos(angle),
                        (float)Math.Sin(angle)
                    );

                    Dust shockDust = Dust.NewDustPerfect(
                        Projectile.Bottom,
                        DustID.Smoke,
                        shockDir * Main.rand.NextFloat(2f, 3.5f),
                        Scale: Main.rand.NextFloat(0.8f, 1.2f),
                        Alpha: 100
                    );
                    shockDust.noGravity = true;
                }
            }

            //爬出完成音效
            if (StateTimer == EmergeDuration / 2) {
                SoundEngine.PlaySound(SoundID.Zombie2 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Dig, Projectile.Center); //挖掘音效
            }

            //状态转换
            if (StateTimer >= EmergeDuration) {
                State = 1;
                StateTimer = 0;
            }
        }

        /// <summary>
        /// 寻找目标AI
        /// </summary>
        private void SeekTargetAI() {
            StateTimer++;

            //寻找最近的敌人
            float maxDetectDistance = 1800f;
            var npc = Projectile.Center.FindClosestNPC(maxDetectDistance, true, true);
            if (npc != null) {
                targetNPC = npc.whoAmI;
            }

            //如果找到目标或超时，进入冲刺状态
            if (targetNPC != -1 || StateTimer >= SeekDuration) {
                State = 2;
                StateTimer = 0;

                //播放咆哮音效
                SoundEngine.PlaySound(SoundID.Zombie3 with { Volume = 0.8f, Pitch = -0.4f }, Projectile.Center);
            }

            //轻微晃动效果
            Projectile.velocity.X = (float)Math.Sin(StateTimer * 0.2f) * 0.5f;
        }

        /// <summary>
        /// 冲刺攻击AI
        /// </summary>
        private void ChargeAttackAI() {
            StateTimer++;

            //获取目标
            Vector2 targetPosition = Main.MouseWorld;
            if (targetNPC != -1 && Main.npc[targetNPC].active) {
                targetPosition = Main.npc[targetNPC].Center;
            }

            //冲刺加速
            if (StateTimer < ChargeDuration) {
                Vector2 chargeDirection = (targetPosition - Projectile.Center).SafeNormalize(Vector2.Zero);

                if (StateTimer == 1) {
                    Projectile.velocity = Projectile.velocity.RotatedByRandom(0.3f);
                }

                //加速阶段（前20帧）
                if (StateTimer < 20) {
                    Projectile.velocity += chargeDirection * 1.5f;
                }

                //最大速度限制
                float maxChargeSpeed = 20f;
                if (Projectile.velocity.Length() > maxChargeSpeed) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * maxChargeSpeed;
                }

                //冲刺轨迹粒子
                if (Main.rand.NextBool(2)) {
                    Dust chargeDust = Dust.NewDustPerfect(
                        Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                        DustID.Water,
                        -Projectile.velocity * 0.3f,
                        Scale: Main.rand.NextFloat(1.2f, 1.8f)
                    );
                    chargeDust.noGravity = true;
                    chargeDust.color = Color.Lerp(Color.Green, Color.DarkGreen, 0.5f);
                }

                //冲击波效果
                if (StateTimer % 10 == 0) {
                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.TwoPi * i / 8f;
                        Vector2 shockVel = new Vector2(
                            (float)Math.Cos(angle),
                            (float)Math.Sin(angle)
                        ) * 4f;

                        Dust shock = Dust.NewDustPerfect(
                            Projectile.Center,
                            DustID.Water,
                            shockVel,
                            Scale: Main.rand.NextFloat(1.5f, 2f)
                        );
                        shock.noGravity = true;
                        shock.alpha = 100;
                    }
                }
            }
            else {
                //冲刺结束，进入爆炸
                State = 3;
                StateTimer = 0;
            }
        }

        /// <summary>
        /// 爆炸AI
        /// </summary>
        private void ExplodeAI() {
            StateTimer++;

            if (StateTimer == 1) {
                //爆炸特效
                CreateExplosionEffect();

                Projectile.Explode(220, default, false);

                //播放爆炸音效
                SoundEngine.PlaySound(SoundID.NPCDeath1, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 1.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
            }

            //渐隐并销毁
            alpha -= 0.1f;
            if (alpha <= 0f || StateTimer > 20) {
                Projectile.Kill();
            }
        }

        /// <summary>
        /// 创建爆炸特效（重新设计）
        /// </summary>
        private void CreateExplosionEffect() {
            //强力冲击环（快速扩散）
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 shockVel = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                ) * Main.rand.NextFloat(10f, 14f);

                Dust shockWave = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    shockVel,
                    Scale: Main.rand.NextFloat(1.8f, 2.4f),
                    Alpha: 50
                );
                shockWave.noGravity = true;
                shockWave.color = Color.White;
            }

            //大型水花
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(7f, 7f);
                float speedBoost = Main.rand.NextFloat(1f, 1.5f);

                Dust largeSplash = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    DustID.Water,
                    velocity * speedBoost,
                    Scale: Main.rand.NextFloat(1.5f, 2.2f),
                    Alpha: 60
                );
                largeSplash.noGravity = true;
            }

            //中型水花
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(6f, 6f);

                Dust mediumSplash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    velocity,
                    Scale: Main.rand.NextFloat(1f, 1.5f),
                    Alpha: 80
                );
                mediumSplash.noGravity = Main.rand.NextBool();
            }

            //小型水雾
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);

                Dust mist = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.DungeonWater,
                    velocity,
                    Scale: Main.rand.NextFloat(0.6f, 1f),
                    Alpha: 120
                );
                mist.noGravity = true;
                mist.color = Color.Lerp(Color.White, Color.Cyan, 0.5f);
            }

            //绿色腐败水花
            for (int i = 0; i < 18; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(8f, 8f);

                Dust poison = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Poisoned,
                    velocity,
                    Scale: Main.rand.NextFloat(1f, 1.6f),
                    Alpha: 70
                );
                poison.noGravity = true;
                poison.color = Color.Lerp(Color.Green, Color.DarkGreen, Main.rand.NextFloat());
            }

            //腐烂气泡效果
            for (int i = 0; i < 10; i++) {
                Dust bubble = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.ToxicBubble,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f)),
                    Scale: Main.rand.NextFloat(0.8f, 1.3f),
                    Alpha: 100
                );
                bubble.noGravity = false;
            }

            if (!VaultUtils.isServer) {
                //主要尸块（3-5块）
                int mainGoreCount = Main.rand.Next(3, 6);
                for (int i = 0; i < mainGoreCount; i++) {
                    //使用僵尸NPC的Gore ID范围
                    int goreType = Main.rand.Next(11, 14);
                    Vector2 goreVel = Main.rand.NextVector2CircularEdge(5f, 5f);

                    Gore.NewGore(
                        Projectile.GetSource_Death(),
                        Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                        goreVel,
                        goreType
                    );
                }

                //小型碎片（2-3块）
                int smallGoreCount = Main.rand.Next(2, 4);
                for (int i = 0; i < smallGoreCount; i++) {
                    Gore.NewGore(
                        Projectile.GetSource_Death(),
                        Projectile.Center,
                        Main.rand.NextVector2CircularEdge(4f, 4f),
                        GoreID.Smoke1 + Main.rand.Next(3)
                    );
                }
            }

            for (int i = 0; i < 12; i++) {
                Vector2 bloodVel = Main.rand.NextVector2CircularEdge(6f, 6f);

                Dust blood = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Blood,
                    bloodVel,
                    Scale: Main.rand.NextFloat(0.8f, 1.4f)
                );
                blood.noGravity = false;
            }

            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 smokeVel = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                ) * Main.rand.NextFloat(3f, 5f);

                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    smokeVel,
                    Scale: Main.rand.NextFloat(1.2f, 1.8f),
                    Alpha: 120
                );
                smoke.noGravity = true;
                smoke.color = Color.Lerp(Color.Gray, Color.DarkGreen, 0.3f);
            }

            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 ringVel = new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                ) * Main.rand.NextFloat(9f, 12f);

                Dust ring = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Water,
                    ringVel,
                    Scale: Main.rand.NextFloat(1.5f, 2f),
                    Alpha: 90
                );
                ring.noGravity = true;
                ring.color = Color.Lerp(Color.Cyan, Color.LightBlue, Main.rand.NextFloat());
                ring.fadeIn = 0.8f;
            }

            //闪光效果
            for (int i = 0; i < 6; i++) {
                Dust flash = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Clentaminator_Cyan,
                    Main.rand.NextVector2Circular(2f, 2f),
                    Scale: Main.rand.NextFloat(1f, 1.5f),
                    Alpha: 0
                );
                flash.noGravity = true;
            }
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
        /// 击中NPC后立即爆炸
        /// </summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            State = 3;
            StateTimer = 0;
        }

        public override bool PreDraw(ref Color lightColor) {
            //加载僵尸纹理
            Main.instance.LoadNPC(NPCID.Zombie);
            Texture2D texture = TextureAssets.Npc[NPCID.Zombie].Value;

            //计算绘制参数
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = texture.GetRectangle(animationFrame, 3); //僵尸贴图每帧高度58
            Vector2 origin = sourceRect.Size() / 2f;

            //朝向
            SpriteEffects effects = Projectile.velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //颜色调制（溺尸应该偏绿色/青色）
            Color drawColor = Color.Lerp(lightColor, new Color(100, 200, 180), 0.4f) * alpha;

            //地面爬出时的暗影效果
            if (State == 0 && groundEffectIntensity > 0) {
                Color shadowColor = Color.Black * (groundEffectIntensity * 0.6f);
                Main.EntitySpriteDraw(
                    texture,
                    drawPosition + new Vector2(0, 10f),
                    sourceRect,
                    shadowColor,
                    0f,
                    origin,
                    scale * 1.2f,
                    effects,
                    0
                );
            }

            //绘制主体
            Main.EntitySpriteDraw(
                texture,
                drawPosition,
                sourceRect,
                drawColor,
                Projectile.rotation,
                origin,
                scale,
                effects,
                0
            );

            //冲刺状态发光效果
            if (State == 2 && Projectile.velocity.Length() > 15f) {
                Color glowColor = new Color(100, 255, 200) * (alpha * 0.4f);
                Main.EntitySpriteDraw(
                    texture,
                    drawPosition,
                    sourceRect,
                    glowColor,
                    Projectile.rotation,
                    origin,
                    scale * 1.1f,
                    effects,
                    0
                );
            }

            return false;
        }

        /// <summary>
        /// 缓动函数：回弹效果
        /// </summary>
        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1f, 3f) + c1 * (float)Math.Pow(t - 1f, 2f);
        }
    }
}
