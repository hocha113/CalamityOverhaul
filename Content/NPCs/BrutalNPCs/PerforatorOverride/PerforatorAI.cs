using CalamityMod;
using CalamityMod.Events;
using CalamityMod.NPCs;
using CalamityMod.NPCs.Perforator;
using CalamityMod.Projectiles.Boss;
using CalamityMod.World;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.PerforatorOverride
{
    internal class PerforatorAI : CWRNPCOverride
    {
        public override int TargetID => ModContent.NPCType<PerforatorHive>();

        //AI状态枚举，用于清晰地管理Boss行为
        private enum AIState
        {
            InitialSpawn,       //0:初始化，飞入战场
            Hovering,           //1:在玩家上方悬停，准备攻击
            ProjectileBarrage,  //2:发射弹幕攻击
            IchorRainAttack,    //3:移动到高处，发动大范围血雨攻击
            CrimsonCharge,      //4:蓄力并向玩家冲锋
            WormSpawnSequence,  //5:召唤蠕虫的演出序列
            DesperationPhase    //6:所有蠕虫被击败后的最终狂怒阶段
        }

        //使用npc.ai[0]来存储当前状态
        private AIState State {
            get => (AIState)npc.ai[0];
            set => npc.ai[0] = (int)value;
        }

        //计时器和状态变量
        private ref float Timer => ref npc.ai[1]; //通用计时器
        private ref float StateIdentifier => ref npc.ai[2]; //状态标识，用于区分不同蠕虫的召唤等
        private ref float LastWormKilledCounter => ref npc.ai[3]; //用于追踪上一个被杀死的蠕虫，触发报复攻击

        //蠕虫生成状态
        private bool smallWormSpawned = false;
        private bool mediumWormSpawned = false;
        private bool largeWormSpawned = false;

        //音效资源
        public static readonly SoundStyle GeyserShoot = new("CalamityMod/Sounds/Custom/Perforator/PerfHiveShoot", 3);
        public static readonly SoundStyle IchorShoot = new("CalamityMod/Sounds/Custom/Perforator/PerfHiveIchorShoot");
        public static readonly SoundStyle WormSpawn = new("CalamityMod/Sounds/Custom/Perforator/PerfHiveWormSpawn");
        public static readonly SoundStyle ChargeTelegraphSound = SoundID.Roar; //冲锋蓄力音效
        public static readonly SoundStyle HitSound = new("CalamityMod/Sounds/NPCHit/PerfHiveHit", 3);
        public static readonly SoundStyle DeathSound = new("CalamityMod/Sounds/NPCKilled/PerfHiveDeath");

        public override void SetProperty() {
            NPCID.Sets.TrailingMode[npc.type] = 2;
            NPCID.Sets.TrailCacheLength[npc.type] = 8;
        }

        public override bool AI() {
            //===================[全局逻辑: 目标、消失、激怒]===================
            CalamityGlobalNPC.perfHive = npc.whoAmI;

            //获取目标玩家
            if (npc.target < 0 || npc.target == Main.maxPlayers || Main.player[npc.target].dead || !Main.player[npc.target].active) {
                npc.TargetClosest();
            }

            //如果目标距离过远，重新选择最近的玩家
            if (Vector2.Distance(Main.player[npc.target].Center, npc.Center) > CalamityGlobalNPC.CatchUpDistance200Tiles) {
                npc.TargetClosest();
            }

            Player player = Main.player[npc.target];

            //处理玩家死亡或逃跑的情况
            if (!player.active || player.dead) {
                npc.TargetClosest(false);
                player = Main.player[npc.target];
                if (!player.active || player.dead) {
                    npc.velocity.Y += 0.1f;
                    if (npc.velocity.Y > 12f) {
                        npc.velocity.Y = 12f;
                    }
                    if (npc.timeLeft > 60) {
                        npc.timeLeft = 60;
                    }
                    return false;
                }
            }
            else if (npc.timeLeft < 1800) {
                npc.timeLeft = 1800;
            }

            //难度和模式设置
            bool bossRush = BossRushEvent.BossRushActive;
            bool expertMode = Main.expertMode || bossRush;
            bool revenge = CalamityWorld.revenge || bossRush;
            bool death = CalamityWorld.death || bossRush;

            //激怒逻辑
            bool isEnraged = false;
            if (!player.ZoneCrimson || (npc.position.Y / 16f) < Main.worldSurface) {
                isEnraged = true;
            }
            npc.Calamity().CurrentlyEnraged = isEnraged;
            float enrageScale = isEnraged ? 1.25f : 1.0f; //激怒状态下提升速度和攻击频率

            bool largeWormAlive = NPC.AnyNPCs(ModContent.NPCType<PerforatorHeadLarge>());
            bool mediumWormAlive = NPC.AnyNPCs(ModContent.NPCType<PerforatorHeadMedium>());
            bool smallWormAlive = NPC.AnyNPCs(ModContent.NPCType<PerforatorHeadSmall>());
            int wormsAlive = (largeWormAlive ? 1 : 0) + (mediumWormAlive ? 1 : 0) + (smallWormAlive ? 1 : 0);

            //当与蠕虫链接时，腐巢意志无敌，强制玩家转火
            bool isLinkedToWorm = (State == AIState.WormSpawnSequence && Timer > 120f);
            if (isLinkedToWorm) {
                npc.dontTakeDamage = true;
                npc.damage = 0; //链接期间不造成接触伤害
                //进行简单的悬浮移动并偶尔发射骚扰性弹幕
                Movement(player, 4f, 0.1f, 300f);
                //检查链接的蠕虫是否死亡
                bool linkedWormIsDead = false;
                if (StateIdentifier == 1 && !smallWormAlive) {
                    linkedWormIsDead = true;
                }
                if (StateIdentifier == 2 && !mediumWormAlive) {
                    linkedWormIsDead = true;
                }
                if (StateIdentifier == 3 && !largeWormAlive) {
                    linkedWormIsDead = true;
                }

                if (linkedWormIsDead) {
                    //链接断开，发出怒吼并发动报复攻击
                    npc.dontTakeDamage = false;
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                    LastWormKilledCounter = 30f; //设置报复攻击计时器

                    if (smallWormSpawned && mediumWormSpawned && largeWormSpawned) {
                        //如果所有蠕虫都已被击败，进入最终阶段
                        State = AIState.DesperationPhase;
                        Timer = 0;
                    }
                    else {
                        State = AIState.Hovering;
                        Timer = 0;
                    }
                }

                return false; //跳过后续AI
            }
            else {
                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;
            }

            //根据存活蠕虫数量提供减伤（旧机制保留，但无敌链接是主要机制）
            npc.Calamity().DR = wormsAlive * 0.3f;

            if (LastWormKilledCounter > 0) {
                LastWormKilledCounter--;
                if (LastWormKilledCounter == 1) {
                    //发动一次快速、密集的弹幕作为报复
                    SpawnProjectileFan(player, 20, 90, 10f, death);
                }
            }

            switch (State) {
                case AIState.InitialSpawn:
                    HandleInitialSpawn(player);
                    break;
                case AIState.Hovering:
                    HandleHovering(player, enrageScale, death);
                    break;
                case AIState.ProjectileBarrage:
                    HandleProjectileBarrage(player, death);
                    break;
                case AIState.IchorRainAttack:
                    HandleIchorRainAttack(player, expertMode);
                    break;
                case AIState.CrimsonCharge:
                    HandleCrimsonCharge(player, enrageScale, death);
                    break;
                case AIState.WormSpawnSequence:
                    HandleWormSpawnSequence(player);
                    break;
                case AIState.DesperationPhase:
                    HandleDesperationPhase(player, enrageScale, expertMode, death);
                    break;
            }

            //生命值检测，用于触发蠕虫召唤
            float lifeRatio = npc.life / (float)npc.lifeMax;
            if (lifeRatio < 0.8f && !smallWormSpawned && State != AIState.WormSpawnSequence) {
                smallWormSpawned = true;
                State = AIState.WormSpawnSequence;
                StateIdentifier = 1; //标记召唤小型蠕虫
                Timer = 0;
                npc.netUpdate = true;
            }
            else if (lifeRatio < 0.55f && !mediumWormSpawned && State != AIState.WormSpawnSequence) {
                mediumWormSpawned = true;
                State = AIState.WormSpawnSequence;
                StateIdentifier = 2; //标记召唤中型蠕虫
                Timer = 0;
                npc.netUpdate = true;
            }
            else if (lifeRatio < 0.3f && !largeWormSpawned && State != AIState.WormSpawnSequence) {
                largeWormSpawned = true;
                State = AIState.WormSpawnSequence;
                StateIdentifier = 3; //标记召唤大型蠕虫
                Timer = 0;
                npc.netUpdate = true;
            }

            //通用外观设置
            npc.rotation = npc.velocity.X * 0.04f;
            npc.spriteDirection = npc.direction;
            return false;
        }

        private void HandleInitialSpawn(Player target) {
            Timer++;
            //缓慢飞入战场上方
            Vector2 destination = new Vector2(target.Center.X, target.Center.Y - 350f);
            Vector2 vectorToTarget = destination - npc.Center;
            npc.velocity = (npc.velocity * 20f + vectorToTarget * 0.8f) / 21f;

            if (Timer > 120f) {
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                State = AIState.Hovering;
                Timer = 0;
            }
        }

        private void HandleHovering(Player target, float enrageScale, bool death) {
            Timer++;
            Movement(target, 6f * enrageScale, 0.15f, 350f);

            //根据计时器选择下一个攻击
            float attackInterval = death ? 150f : 200f;
            if (Timer > attackInterval / enrageScale) {
                Timer = 0;
                int choice = Main.rand.Next(3);
                if (choice == 0) {
                    State = AIState.CrimsonCharge;
                }
                else if (choice == 1) {
                    State = AIState.ProjectileBarrage;
                }
                else {
                    State = AIState.IchorRainAttack;
                }
                npc.netUpdate = true;
            }
        }

        private void HandleProjectileBarrage(Player target, bool death) {
            Timer++;
            //减速以准备攻击
            npc.velocity *= 0.96f;
            Movement(target, 4f, 0.1f, 350f);

            if (Timer > 60f) {
                SpawnProjectileFan(target, death ? 16 : 12, 75, 8f, death);
                State = AIState.Hovering;
                Timer = 0;
                npc.netUpdate = true;
            }
        }

        private void HandleIchorRainAttack(Player target, bool expertMode) {
            Timer++;

            //飞到玩家斜上方高处
            Movement(target, 8f, 0.2f, 450f);

            if (Timer > 60f && Timer < 240f && Timer % 10 == 0) {
                SoundEngine.PlaySound(IchorShoot, npc.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int type = ModContent.ProjectileType<IchorBlob>();
                    int damage = npc.GetProjectileDamage(type);
                    int numBlobs = expertMode ? 2 : 1;
                    for (int i = 0; i < numBlobs; i++) {
                        Vector2 spawnPos = npc.Center + new Vector2(Main.rand.NextFloat(-100, 100), Main.rand.NextFloat(-50, 50));
                        Vector2 blobVelocity = new Vector2(Main.rand.NextFloat(-3, 3), Main.rand.NextFloat(5, 9));
                        Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, blobVelocity, type, damage, 0f, Main.myPlayer, 0f, target.Center.Y);
                    }
                }
            }

            if (Timer > 260f) {
                State = AIState.Hovering;
                Timer = 0;
            }
        }

        private void HandleCrimsonCharge(Player target, float enrageScale, bool death) {
            Timer++;
            float telegraphTime = death ? 45f : 60f;

            if (Timer <= telegraphTime) {
                //蓄力阶段
                npc.velocity *= 0.9f; //减速

                //面向玩家
                npc.direction = (target.Center.X > npc.Center.X) ? 1 : -1;

                //视觉提示：发光和粒子效果
                if (Main.rand.NextBool(3)) {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, npc.velocity.X, npc.velocity.Y, 100, default, 2f);
                }

                //音效提示
                if (Timer == 1) {
                    SoundEngine.PlaySound(ChargeTelegraphSound, npc.Center);
                }
            }
            else if (Timer == telegraphTime + 1) {
                //冲锋阶段
                float chargeSpeed = death ? 22f : 18f;
                Vector2 directionToTarget = Vector2.Normalize(target.Center - npc.Center);
                npc.velocity = directionToTarget * chargeSpeed * enrageScale;
            }
            else if (Timer > telegraphTime + 75f) {
                //冲锋后恢复
                State = AIState.Hovering;
                Timer = 0;
            }
        }

        private void HandleWormSpawnSequence(Player target) {
            Timer++;

            //移动到场地中央
            Vector2 destination = new Vector2(target.Center.X, target.Center.Y - 100f);
            Movement(target, 10f, 0.3f, 100f);
            npc.velocity *= 0.95f; //到达后减速

            //播放演出
            if (Timer == 60f) {
                SoundEngine.PlaySound(WormSpawn, npc.Center);
                //剧烈的视觉效果
                for (int i = 0; i < 50; i++) {
                    int dustType = Main.rand.NextBool() ? DustID.Blood : DustID.Ichor;
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, dustType, 0f, 0f, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                    dust.velocity *= Main.rand.NextFloat(2f, 4f);
                    if (dustType == DustID.Blood) {
                        dust.noGravity = true;
                    }
                }
            }

            //在演出高潮时生成蠕虫
            if (Timer == 120f) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int wormType = 0;
                    if (StateIdentifier == 1) {
                        wormType = ModContent.NPCType<PerforatorHeadSmall>();
                    }
                    else if (StateIdentifier == 2) {
                        wormType = ModContent.NPCType<PerforatorHeadMedium>();
                    }
                    else if (StateIdentifier == 3) {
                        wormType = ModContent.NPCType<PerforatorHeadLarge>();
                    }

                    if (wormType != 0) {
                        NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, wormType, 1);
                    }
                }
                //此时进入"血肉链接"状态，AI主循环将处理后续逻辑
            }
        }

        private void HandleDesperationPhase(Player target, float enrageScale, bool expertMode, bool death) {
            Timer++;

            //视觉效果：持续发出红色光芒
            Lighting.AddLight(npc.Center, 1f, 0f, 0f);

            //更具攻击性的移动
            Movement(target, (death ? 13f : 11f) * enrageScale, death ? 0.1125f : 0.0975f, 200f);

            //攻击循环大幅加速
            float attackInterval = death ? 80f : 120f;
            if (Timer > attackInterval / enrageScale) {
                Timer = 0;
                //在最终阶段，冲锋和弹幕交替出现
                if (Main.rand.NextBool(3)) //三分之一的概率使用弹幕
                {
                    SpawnProjectileFan(target, expertMode ? 24 : 18, 80, 11f, death);
                }
                else //三分之二的概率冲锋
                {
                    State = AIState.CrimsonCharge; //切换到冲锋状态，但冲锋后会直接返回DesperationPhase
                    Timer = 0; //重置计时器以触发冲锋逻辑
                }
            }
        }

        //===================[辅助函数]===================

        //发射扇形弹幕的辅助方法
        private void SpawnProjectileFan(Player target, int numProj, int spread, float velocity, bool isDeathMode) {
            SoundEngine.PlaySound(GeyserShoot, npc.Center);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }

            int type = Main.rand.NextBool() ? ModContent.ProjectileType<IchorShot>() : ModContent.ProjectileType<BloodGeyser>();
            int damage = npc.GetProjectileDamage(type);

            Vector2 targetCenter = target.Center;
            Vector2 direction = Vector2.Normalize(targetCenter - npc.Center);
            Vector2 projectileVelocity = direction * velocity;
            float rotation = MathHelper.ToRadians(spread);

            for (int i = 0; i < numProj; i++) {
                Vector2 perturbedSpeed = projectileVelocity.RotatedBy(MathHelper.Lerp(-rotation, rotation, i / (float)(numProj - 1)));
                if (isDeathMode) {
                    //死亡模式下增加一些随机性
                    perturbedSpeed += new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.5f, 0.5f));
                }
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perturbedSpeed, type, damage, 0f, Main.myPlayer, 0f, target.Center.Y);
            }
        }

        //移动逻辑 (从原版AI中提取和优化)
        private void Movement(Player target, float velocity, float acceleration, float hoverHeight) {
            Vector2 destination = new Vector2(target.Center.X, target.Center.Y - hoverHeight);
            Vector2 distanceFromDestination = destination - npc.Center;

            //当距离目标点很近时，减缓移动以避免抖动
            if (distanceFromDestination.Length() < 50f) {
                acceleration *= 0.5f;
            }

            npc.SimpleFlyMovement(distanceFromDestination.SafeNormalize(Vector2.Zero) * velocity, acceleration);
        }

        //帧动画
        public override bool FindFrame(int frameHeight) {
            npc.frameCounter += 0.15f;
            npc.frameCounter %= Main.npcFrameCount[npc.type];
            int frame = (int)npc.frameCounter;
            npc.frame.Y = frame * frameHeight;
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D mainValue = TextureAssets.Npc[npc.type].Value;
            Rectangle rectangle = npc.frame;
            Vector2 orig = rectangle.Size() / 2;

            float sengs = 0.2f;
            for (int i = 0; i < npc.oldPos.Length; i++) {
                Vector2 drawOldPos = npc.oldPos[i] + npc.Size / 2 - Main.screenPosition;
                Main.EntitySpriteDraw(mainValue, drawOldPos, rectangle, Color.White * sengs
                    , npc.rotation, orig, npc.scale * (0.8f + sengs), SpriteEffects.None, 0);
                sengs *= 0.8f;
            }

            Main.EntitySpriteDraw(mainValue, npc.Center - Main.screenPosition, rectangle
                , drawColor, npc.rotation, orig, npc.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}