using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
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
    internal class FishScorpio : FishSkill
    {
        public override int UnlockFishID => ItemID.ScorpioFish;
        public override int DefaultCooldown => 60 * (15 - HalibutData.GetDomainLayer() / 2); //15-10秒冷却
        public override int ResearchDuration => 60 * 18;

        private static int MaxScorpionSentries => 1 + HalibutData.GetDomainLayer() / 10; //最多1-2只蝎子

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (!Active(player)) {
                return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
            }

            int existingCount = player.CountProjectilesOfID<ScorpionSentry>();
            int maxCount = MaxScorpionSentries;

            if (existingCount < maxCount) {
                SetCooldown();
                SpawnScorpionSentry(player, source, damage, knockback);
            }

            return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        private void SpawnScorpionSentry(Player player, EntitySource_ItemUse_WithAmmo source, int damage, float knockback) {
            //寻找附近敌人作为参考方向
            NPC target = player.Center.FindClosestNPC(1200f);

            Vector2 spawnPos = FindValidGroundPosition(player, target);
            if (spawnPos == Vector2.Zero) {
                //如果找不到合适位置，在玩家脚下生成
                spawnPos = player.Bottom + new Vector2(0, -8);
            }

            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(
                source,
                spawnPos,
                Vector2.Zero,
                ModContent.ProjectileType<ScorpionSentry>(),
                (int)(damage * (0.4f + HalibutData.GetDomainLayer() * 0.1f)),
                knockback * 0.7f,
                player.whoAmI,
                ai0: target?.whoAmI ?? -1
                );
            }

            //召唤特效：地面沙沸腾，先于蝎子出土
            SpawnSummonEffect(spawnPos);
            FishScorpioVFX.BurrowSound(spawnPos);
        }

        private static Vector2 FindValidGroundPosition(Player player, NPC target) {
            Vector2 dirToTarget;
            if (target != null) {
                dirToTarget = (target.Center - player.Center).SafeNormalize(Vector2.Zero);
            }
            else {
                //没有目标时在玩家移动方向生成
                dirToTarget = new Vector2(player.direction, 0);
            }

            //尝试在玩家前方寻找地面
            for (int attempt = 0; attempt < 8; attempt++) {
                float distance = Main.rand.NextFloat(80f, 200f);
                float angleOffset = Main.rand.NextFloat(-0.5f, 0.5f);
                Vector2 testDir = dirToTarget.RotatedBy(angleOffset);
                Vector2 testPos = player.Center + testDir * distance;

                //向下搜索地面
                for (int y = 0; y < 40; y++) {
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

        private static void SpawnSummonEffect(Vector2 position) {
            Vector2 ground = position + new Vector2(0f, 12f);
            //土浪 + 沙粒喷泉 + 隆起的沙丘：蝎子将从这里顶出来
            FishScorpioVFX.GroundPlume(ground, 14, 1.1f);
            FishScorpioVFX.Mound(ground, 60f, 40);
        }
    }

    /// <summary>
    /// 蝎子哨兵，从地面爬出，向敌人发射沙龙卷
    /// </summary>
    internal class ScorpionSentry : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Scorpion;

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float AttackTimer => ref Projectile.ai[2];

        private NPC target;
        private int direction = 1; //朝向
        private bool isEmerging = true; //正在从地面爬出
        private float emergeProgress = 0f;

        /// <summary>攻击预告帧数，尾部聚旋的时长</summary>
        private const int TelegraphFrames = 26;
        /// <summary>退场沉入地面的帧数</summary>
        private const int SinkFrames = 40;

        private float telegraphT;      //预告进度0..1
        private float leanAngle;       //身体倾角（后仰蓄力/前倾过冲/行走前倾）
        private int recoilTimer;       //发射后坐帧
        private bool telegraphSoundPlayed;
        private bool sinkSoundPlayed;

        private static int LifeTime => 60 * (5 + HalibutData.GetDomainLayer() / 2); //5-10秒存在时间
        private static int AttackInterval => 120 - HalibutData.GetDomainLayer() * 6; //攻击间隔（随层数减少）

        /// <summary>蝎尾聚旋点：尾刺卷在背后上方</summary>
        private Vector2 TailPoint => Projectile.Center + new Vector2(-direction * 14f, -16f);

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4; //蝎子有4帧动画
        }

        public override void SetDefaults() {
            Projectile.width = 42;
            Projectile.height = 20;
            Projectile.friendly = false; //哨兵本体不造成伤害
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (target.type == CWRID.NPC_DevourerofGodsHead || target.type == CWRID.NPC_DevourerofGodsTail) {
                modifiers.FinalDamage *= 2f;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return false;
        }

        public override bool? CanDamage() => false;

        /// <summary>是否踩在实心物块上</summary>
        private bool OnGround() {
            Point tile = (Projectile.Bottom + new Vector2(0f, 4f)).ToTileCoordinates();
            return WorldGen.InWorld(tile.X, tile.Y) && Main.tile[tile.X, tile.Y].HasSolidTile();
        }

        public override void AI() {
            if (!Owner.active || Owner.dead || !FishSkill.GetT<FishScorpio>().Active(Owner)) {
                Projectile.Kill();
                return;
            }

            int layer = HalibutData.GetDomainLayer(Owner);

            //爬出地面动画：源矩形裁剪从地面顶出，不用alpha淡入
            if (isEmerging) {
                emergeProgress += 0.04f;
                if (emergeProgress >= 1f) {
                    emergeProgress = 1f;
                    isEmerging = false;
                }
                if (!Main.dedServ) {
                    //沙帘：出土时细沙从背甲上滑落
                    if (Main.rand.NextBool(2)) {
                        Vector2 pos = Projectile.Bottom + new Vector2(Main.rand.NextFloat(-16f, 16f), -Projectile.height * emergeProgress);
                        PRTLoader.NewParticle<PRT_FishScorpioSand>(pos, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), 0.4f)
                            , FishScorpioVFX.RandGrain(), Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(14, 22), 0f);
                    }
                    if (Projectile.timeLeft % 8 == 0) {
                        FishScorpioVFX.Puff(Projectile.Bottom, new Vector2(0f, -0.6f), 0.2f, 0.24f, true);
                    }
                }
            }

            //寻找目标
            if (TargetIndex >= 0 && TargetIndex < Main.maxNPCs && Main.npc[(int)TargetIndex].active && Main.npc[(int)TargetIndex].CanBeChasedBy()) {
                target = Main.npc[(int)TargetIndex];
            }
            else {
                target = Projectile.Center.FindClosestNPC(800f);
                if (target != null) TargetIndex = target.whoAmI;
            }

            //面向目标
            if (target != null) {
                direction = target.Center.X > Projectile.Center.X ? 1 : -1;
            }

            if (recoilTimer > 0) {
                //发射后坐：先退半步再回到步速
                recoilTimer--;
                Projectile.velocity.X = direction * (3f - 6f * recoilTimer / 8f);
            }
            else if (target != null && Math.Abs(Projectile.Center.X - target.Center.X) > 6) {
                Projectile.velocity.X = direction * 3;
            }
            else {
                Projectile.velocity.X = direction * 0.01f;
            }

            if (Projectile.velocity.Y < 16) {
                Projectile.velocity.Y += 2;
            }

            //攻击逻辑
            AttackTimer++;
            int adjustedInterval = Math.Clamp(AttackInterval - layer * 8, 35, AttackInterval);
            int telegraphLen = Math.Min(TelegraphFrames, adjustedInterval - 6);

            //预告拍：尾部沙粒向心聚旋
            bool telegraphActive = !isEmerging && target != null && AttackTimer >= adjustedInterval - telegraphLen;
            if (telegraphActive) {
                telegraphT = MathHelper.Clamp((AttackTimer - (adjustedInterval - telegraphLen)) / (float)telegraphLen, 0f, 1f);
                if (!telegraphSoundPlayed) {
                    telegraphSoundPlayed = true;
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.25f, Pitch = -0.1f, MaxInstances = 3 }, TailPoint);
                }
                if (!Main.dedServ) {
                    //向心沙粒：从环带向尾点螺旋收拢
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radius = Main.rand.NextFloat(24f, 38f);
                    Vector2 spawn = TailPoint + ang.ToRotationVector2() * radius;
                    Vector2 inward = (TailPoint - spawn).SafeNormalize(Vector2.Zero);
                    Vector2 vel = inward.RotatedBy(0.7f * direction) * Main.rand.NextFloat(2f, 3.4f);
                    PRTLoader.NewParticle<PRT_FishScorpioSand>(spawn, vel, FishScorpioVFX.RandGrain(), Main.rand.NextFloat(0.55f, 0.9f))
                        ?.Configure(Main.rand.Next(12, 18), 1f, 0.26f, 0.9f);
                }
            }
            else {
                telegraphT = 0f;
                telegraphSoundPlayed = false;
            }

            if (!isEmerging && AttackTimer >= adjustedInterval && target != null) {
                AttackTimer = 0;
                telegraphSoundPlayed = false;
                recoilTimer = 8;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    ShootAtTarget(target, layer);
                }
            }

            //身体倾角：后仰蓄力 → 过冲前倾 → 行走前倾，围绕足底旋转
            float leanTarget = direction * Math.Min(Math.Abs(Projectile.velocity.X), 3f) * 0.02f;
            leanTarget += -direction * 0.11f * telegraphT;
            if (recoilTimer > 0) {
                leanTarget += direction * 0.13f * (recoilTimer / 8f);
            }
            leanAngle = MathHelper.Lerp(leanAngle, leanTarget, 0.2f);

            //帧动画：行走快踏、驻足慢摆
            int frameRate = Math.Abs(Projectile.velocity.X) > 0.5f ? 7 : 13;
            if (++Projectile.frameCounter >= frameRate) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= 4) Projectile.frame = 0;
            }

            //犁痕：行走在地面上犁开细沙
            if (!Main.dedServ && !isEmerging && OnGround() && Math.Abs(Projectile.velocity.X) > 1f) {
                if (Projectile.timeLeft % 3 == 0) {
                    Vector2 pos = Projectile.Bottom + new Vector2(-direction * Main.rand.NextFloat(8f, 18f), -2f);
                    Vector2 vel = new Vector2(-direction * Main.rand.NextFloat(0.5f, 1.6f), Main.rand.NextFloat(-2.2f, -0.8f));
                    PRTLoader.NewParticle<PRT_FishScorpioSand>(pos, vel, FishScorpioVFX.RandGrain(), Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(Main.rand.Next(14, 22), 0.25f, 0.26f, 0.35f);
                }
                if (Projectile.timeLeft % 14 == 0) {
                    FishScorpioVFX.Puff(Projectile.Bottom + new Vector2(-direction * 12f, -2f)
                        , new Vector2(-direction * 0.3f, -0.25f), 0.16f, 0.12f, true);
                }
            }

            //退场：沉回沙里，扬起土浪
            if (Projectile.timeLeft < SinkFrames) {
                if (!sinkSoundPlayed) {
                    sinkSoundPlayed = true;
                    FishScorpioVFX.BurrowSound(Projectile.Bottom, -0.15f);
                }
                if (!Main.dedServ) {
                    if (Projectile.timeLeft % 2 == 0) {
                        Vector2 pos = Projectile.Bottom + new Vector2(Main.rand.NextFloat(-16f, 16f), -2f);
                        PRTLoader.NewParticle<PRT_FishScorpioSand>(pos, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, -0.6f))
                            , FishScorpioVFX.RandGrain(), Main.rand.NextFloat(0.55f, 0.95f))?.Configure(Main.rand.Next(14, 22), 0.3f, 0.26f, 0.35f);
                    }
                    if (Projectile.timeLeft % 10 == 0) {
                        FishScorpioVFX.Puff(Projectile.Bottom, new Vector2(0f, -0.5f), 0.2f, 0.2f, true);
                    }
                }
            }
        }

        private void ShootAtTarget(NPC target, int layer) {
            Vector2 toTarget = target.Center - Projectile.Center;
            float distance = toTarget.Length();

            //预判目标移动
            Vector2 predictedPos = target.Center + target.velocity * (distance / 15f);
            Vector2 shootDir = (predictedPos - Projectile.Center).SafeNormalize(Vector2.Zero);

            //发射沙龙卷：从尾点聚旋处出手，出膛带过冲初速
            float speed = 12f + layer * 0.6f;
            int numShots = 1 + layer / 5; //高层数多发

            for (int i = 0; i < numShots; i++) {
                float angleOffset = numShots > 1 ? MathHelper.Lerp(-0.15f, 0.15f, i / (float)(numShots - 1)) : 0f;
                Vector2 vel = shootDir.RotatedBy(angleOffset) * speed * 1.35f;

                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    TailPoint,
                    vel,
                    ModContent.ProjectileType<FishScorpioSandnado>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Owner.whoAmI,
                    ai0: speed);
            }

            //攻击音效：出手拍 + 风啸
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.35f, Pitch = 0.55f, MaxInstances = 3 }, Projectile.Center);

            //释放拍：聚好的沙顺出手方向甩出
            FishScorpioVFX.GrainBurst(TailPoint, shootDir, 8, 3f, 6.5f, 0.6f, 0.45f);
            FishScorpioVFX.Puff(TailPoint, shootDir * 1.5f, 0.2f, 0.24f);
        }

        public override void OnKill(int timeLeft) {
            //消失时地面翻沙，留下短命沙丘
            FishScorpioVFX.GroundPlume(Projectile.Bottom, 10);
            Vector2? ground = FishScorpioVFX.FindGroundBelow(Projectile.Center, 6);
            if (ground != null) {
                FishScorpioVFX.Mound(ground.Value, 52f, 50);
            }
            FishScorpioVFX.BurrowSound(Projectile.Bottom, -0.3f);
        }

        public override bool PreDraw(ref Color lightColor) {
            //加载原版蝎子纹理
            Main.instance.LoadNPC(NPCID.Scorpion);
            Texture2D texture = TextureAssets.Npc[NPCID.Scorpion].Value;

            int frameHeight = texture.Height / 4;

            //出土/入土用源矩形裁剪：只画地面以上的身体，禁alpha幽灵
            float coverage = 1f;
            if (isEmerging) {
                coverage = 1f - MathF.Pow(1f - emergeProgress, 2.4f); //easeOut顶出
            }
            else if (Projectile.timeLeft < SinkFrames) {
                float sinkT = 1f - Projectile.timeLeft / (float)SinkFrames;
                coverage = 1f - sinkT * sinkT; //easeIn沉入
            }
            int visibleH = Math.Max((int)(frameHeight * coverage), 2);
            Rectangle source = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, visibleH);

            //足底锚点：裁剪后的可视切片贴着地面线
            Vector2 origin = new Vector2(texture.Width / 2f, visibleH);
            Vector2 drawPos = Projectile.Bottom - Main.screenPosition;

            //蝎子正面朝左，根据方向翻转
            SpriteEffects effects = direction > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            //预告拍：尾点后方聚旋的小沙涡，画在身体之下让尾刺压住涡根
            if (telegraphT > 0.05f) {
                float tp = telegraphT * coverage;
                FishScorpioVFX.DrawNado(Main.spriteBatch, TailPoint - new Vector2(0f, 6f * tp)
                    , 30f + 12f * tp, 44f + 18f * tp, Projectile.identity * 1.37f % 10f, 0.85f, tp * 0.85f, 0.8f * tp);
            }

            //阴影
            Main.EntitySpriteDraw(texture, drawPos + new Vector2(2, 4), source, Color.Black * 0.4f * coverage,
                leanAngle, origin, Projectile.scale, effects, 0);

            //主体
            Main.EntitySpriteDraw(texture, drawPos, source, lightColor,
                leanAngle, origin, Projectile.scale, effects, 0);

            return false;
        }
    }
}
