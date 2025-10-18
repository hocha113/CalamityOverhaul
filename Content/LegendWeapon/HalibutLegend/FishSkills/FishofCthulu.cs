using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishofCthulu : FishSkill
    {
        public override int UnlockFishID => ItemID.TheFishofCthulu;
        public override int DefaultCooldown => 180 - HalibutData.GetDomainLayer() * 12;
        public override int ResearchDuration => 60 * 25;
        //触手生成计时器
        private int tentacleSpawnTimer = 0;
        //触手生成间隔
        private static int TentacleSpawnInterval => 60 - HalibutData.GetDomainLayer() * 4;

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player)
        {
            if (Active(player))
            {
                tentacleSpawnTimer++;

                //周期性生成触手
                if (tentacleSpawnTimer >= TentacleSpawnInterval)
                {
                    tentacleSpawnTimer = 0;
                    SpawnTentacles(player);
                }
            }
            return true;
        }

        //生成触手
        private static void SpawnTentacles(Player player)
        {
            int tentacleCount = 2 + HalibutData.GetDomainLayer() / 2;

            for (int i = 0; i < tentacleCount; i++)
            {
                //在玩家周围寻找地面位置
                Vector2 spawnPos = FindGroundPosition(player);

                if (spawnPos != Vector2.Zero)
                {
                    //寻找附近的敌人作为目标
                    NPC target = spawnPos.FindClosestNPC(800f);
                    int targetID = target?.whoAmI ?? -1;

                    Projectile.NewProjectile(
                        player.GetSource_FromThis(),
                        spawnPos,
                        Vector2.Zero,
                        ModContent.ProjectileType<CthulhuTentacle>(),
                        (int)(player.GetWeaponDamage(player.GetItem()) * (0.8f + HalibutData.GetDomainLayer() * 0.15f)),
                        4f,
                        player.whoAmI,
                        targetID
                    );
                }
            }

            //音效
            SoundEngine.PlaySound(SoundID.NPCHit13 with
            {
                Volume = 0.6f,
                Pitch = -0.5f
            }, player.Center);
        }

        //寻找玩家周围的地面位置
        private static Vector2 FindGroundPosition(Player player)
        {
            //在玩家周围随机选择一个角度
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(200f, 400f);
            Vector2 testPos = player.Center + angle.ToRotationVector2() * distance;

            //向下搜索地面
            for (int y = (int)(testPos.Y / 16); y < Main.maxTilesY && y < (int)(testPos.Y / 16) + 30; y++)
            {
                int x = (int)(testPos.X / 16);
                if (x < 0 || x >= Main.maxTilesX || y < 0 || y >= Main.maxTilesY)
                    continue;

                Tile tile = Main.tile[x, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType])
                {
                    return new Vector2(x * 16 + 8, y * 16);
                }
            }

            return Vector2.Zero;
        }
    }

    //克苏鲁触手弹幕
    internal class CthulhuTentacle : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //触手状态枚举
        private enum TentacleState
        {
            Emerging,//钻出地面
            Idle,//待机摇摆
            Attacking,//攻击
            Retracting//收回
        }

        private TentacleState State
        {
            get => (TentacleState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float TargetNPCID => ref Projectile.ai[1];
        private ref float StateTimer => ref Projectile.ai[2];

        //触手节点数量
        private const int SegmentCount = 15;
        //触手节点列表
        private readonly List<TentacleSegment> segments = new();
        //触手宽度
        private float tentacleWidth = 28f;

        //IK相关参数
        private Vector2 tipPosition;//触手尖端位置
        private Vector2 tipVelocity;//触手尖端速度
        private Vector2 targetPosition;//目标位置

        //动画相关
        private float swayPhase = 0f;//摇摆相位
        private float attackForce = 0f;//攻击力量

        //生命周期
        private const int EmergeDuration = 30;
        private const int IdleDuration = 240;
        private const int AttackDuration = 60;
        private const int RetractDuration = 20;

        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EmergeDuration + IdleDuration + AttackDuration + RetractDuration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI()
        {
            //初始化触手节点
            if (segments.Count == 0)
            {
                InitializeSegments();
            }

            StateTimer++;

            //状态机更新
            switch (State)
            {
                case TentacleState.Emerging:
                    EmergingAI();
                    break;
                case TentacleState.Idle:
                    IdleAI();
                    break;
                case TentacleState.Attacking:
                    AttackingAI();
                    break;
                case TentacleState.Retracting:
                    RetractingAI();
                    break;
            }

            //更新触手节点位置（IK算法）
            UpdateTentacleIK();

            //生成粒子效果
            SpawnTentacleParticles();

            //照明效果
            Lighting.AddLight(Projectile.Center, 0.3f, 0.1f, 0.4f);
        }

        //初始化触手节点
        private void InitializeSegments()
        {
            float segmentLength = 16f;

            for (int i = 0; i < SegmentCount; i++)
            {
                segments.Add(new TentacleSegment
                {
                    Position = Projectile.Center - new Vector2(0, i * segmentLength),
                    Length = segmentLength
                });
            }

            tipPosition = segments[0].Position;
            tipVelocity = Vector2.Zero;
            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        //钻出地面状态AI
        private void EmergingAI()
        {
            float progress = StateTimer / EmergeDuration;
            float emergeHeight = MathHelper.Lerp(0f, 240f, EaseOutBack(progress));

            //更新尖端目标位置
            targetPosition = Projectile.Center - new Vector2(0, emergeHeight);

            //添加震动效果
            float shake = (float)Math.Sin(progress * MathHelper.Pi * 8f) * 3f * (1f - progress);
            targetPosition.X += shake;

            //阶段结束，进入待机状态
            if (StateTimer >= EmergeDuration)
            {
                State = TentacleState.Idle;
                StateTimer = 0;

                //震动音效
                SoundEngine.PlaySound(SoundID.Item14 with
                {
                    Volume = 0.4f,
                    Pitch = -0.6f
                }, Projectile.Center);

                //尘埃效果
                for (int i = 0; i < 20; i++)
                {
                    Dust.NewDust(Projectile.Center, 30, 30, DustID.Shadowflame,
                        Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, -1f), 0, default, 1.5f);
                }
            }
        }

        //待机摇摆状态AI
        private void IdleAI()
        {
            //更新摇摆相位
            swayPhase += 0.05f;

            //摇摆运动
            float swayX = (float)Math.Sin(swayPhase) * 40f;
            float swayY = (float)Math.Sin(swayPhase * 0.7f) * 15f;

            targetPosition = Projectile.Center - new Vector2(swayX, 220f + swayY);

            //检测是否有目标，如果有则进入攻击状态
            if (TargetNPCID >= 0 && TargetNPCID < Main.maxNPCs)
            {
                NPC target = Main.npc[(int)TargetNPCID];
                if (target.active && !target.dontTakeDamage && Vector2.Distance(Projectile.Center, target.Center) < 600f)
                {
                    State = TentacleState.Attacking;
                    StateTimer = 0;
                    return;
                }
                else
                {
                    //重新寻找目标
                    NPC newTarget = Projectile.Center.FindClosestNPC(600f);
                    TargetNPCID = newTarget?.whoAmI ?? -1;
                }
            }
            else
            {
                //寻找新目标
                NPC newTarget = Projectile.Center.FindClosestNPC(600f);
                TargetNPCID = newTarget?.whoAmI ?? -1;
            }

            //待机时间结束，进入收回状态
            if (StateTimer >= IdleDuration)
            {
                State = TentacleState.Retracting;
                StateTimer = 0;
            }
        }

        //攻击状态AI
        private void AttackingAI()
        {
            float attackProgress = StateTimer / AttackDuration;

            //获取目标
            if (TargetNPCID >= 0 && TargetNPCID < Main.maxNPCs)
            {
                NPC target = Main.npc[(int)TargetNPCID];

                if (target.active && !target.dontTakeDamage)
                {
                    //分为三个阶段：后拉-快速攻击-回收
                    if (attackProgress < 0.3f)
                    {
                        //后拉蓄力阶段
                        float pullbackRatio = attackProgress / 0.3f;
                        Vector2 pullbackOffset = (Projectile.Center - target.Center).SafeNormalize(Vector2.Zero) * 100f;
                        targetPosition = Projectile.Center - new Vector2(0, 200f) + pullbackOffset * EaseInQuad(pullbackRatio);
                        attackForce = 0f;
                    }
                    else if (attackProgress < 0.6f)
                    {
                        //快速攻击阶段
                        float strikeRatio = (attackProgress - 0.3f) / 0.3f;
                        targetPosition = Vector2.Lerp(targetPosition, target.Center, EaseOutCubic(strikeRatio));
                        attackForce = MathHelper.Lerp(0f, 1f, strikeRatio);

                        //在攻击峰值播放音效
                        if (StateTimer == (int)(AttackDuration * 0.45f))
                        {
                            SoundEngine.PlaySound(SoundID.NPCHit1 with
                            {
                                Volume = 0.7f,
                                Pitch = -0.4f
                            }, targetPosition);

                            //生成冲击波粒子
                            for (int i = 0; i < 15; i++)
                            {
                                Vector2 velocity = Main.rand.NextVector2CircularEdge(8f, 8f);
                                Dust.NewDust(targetPosition, 20, 20, DustID.Shadowflame, velocity.X, velocity.Y, 0, default, 2f);
                            }
                        }
                    }
                    else
                    {
                        //回收阶段
                        float retractRatio = (attackProgress - 0.6f) / 0.4f;
                        Vector2 idlePos = Projectile.Center - new Vector2(0, 200f);
                        targetPosition = Vector2.Lerp(target.Center, idlePos, EaseInQuad(retractRatio));
                        attackForce = MathHelper.Lerp(1f, 0f, retractRatio);
                    }
                }
                else
                {
                    //目标无效，返回待机
                    State = TentacleState.Idle;
                    StateTimer = 0;
                    return;
                }
            }
            else
            {
                //没有目标，返回待机
                State = TentacleState.Idle;
                StateTimer = 0;
                return;
            }

            //攻击阶段结束，返回待机
            if (StateTimer >= AttackDuration)
            {
                State = TentacleState.Idle;
                StateTimer = 0;
            }
        }

        //收回状态AI
        private void RetractingAI()
        {
            float progress = StateTimer / RetractDuration;
            float retractHeight = MathHelper.Lerp(240f, 0f, EaseInBack(progress));

            targetPosition = Projectile.Center - new Vector2(0, retractHeight);

            //收回完成，杀死弹幕
            if (StateTimer >= RetractDuration)
            {
                Projectile.Kill();
            }
        }

        //更新触手IK（逆向运动学）
        private void UpdateTentacleIK()
        {
            //FABRIK（Forward And Backward Reaching Inverse Kinematics）算法

            //向目标移动尖端（带有平滑过渡）
            Vector2 toTarget = targetPosition - tipPosition;
            tipVelocity += toTarget * 0.15f;
            tipVelocity *= 0.85f;//阻尼
            tipPosition += tipVelocity;

            //前向阶段：从尖端向根部
            segments[0].Position = tipPosition;
            for (int i = 1; i < segments.Count; i++)
            {
                Vector2 direction = (segments[i].Position - segments[i - 1].Position).SafeNormalize(Vector2.Zero);
                segments[i].Position = segments[i - 1].Position + direction * segments[i - 1].Length;

                //添加重力影响
                float gravityStrength = 0.3f * (i / (float)segments.Count);
                segments[i].Position += new Vector2(0, gravityStrength);

                //添加摇摆效果
                if (State == TentacleState.Idle || State == TentacleState.Emerging)
                {
                    float swayOffset = (float)Math.Sin(swayPhase + i * 0.3f) * (3f + i * 0.5f);
                    segments[i].Position += new Vector2(swayOffset, 0);
                }
            }

            //后向阶段：从根部向尖端
            segments[segments.Count - 1].Position = Projectile.Center;
            for (int i = segments.Count - 2; i >= 0; i--)
            {
                Vector2 direction = (segments[i].Position - segments[i + 1].Position).SafeNormalize(Vector2.Zero);
                segments[i].Position = segments[i + 1].Position + direction * segments[i].Length;
            }

            //更新尖端位置
            tipPosition = segments[0].Position;

            //更新碰撞箱
            Projectile.Center = segments[segments.Count / 2].Position;
        }

        //生成触手粒子效果
        private void SpawnTentacleParticles()
        {
            if (Main.rand.NextBool(3))
            {
                int particleIndex = Main.rand.Next(segments.Count);
                Vector2 particlePos = segments[particleIndex].Position;

                Dust dust = Dust.NewDustPerfect(particlePos, DustID.Shadowflame, Main.rand.NextVector2Circular(1f, 1f), 0, default, 1.2f);
                dust.noGravity = true;
            }

            //在攻击时生成更多粒子
            if (State == TentacleState.Attacking && attackForce > 0.5f && Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(tipPosition, DustID.Shadowflame, Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.5f);
                dust.noGravity = true;
            }
        }

        //碰撞检测
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            //只在攻击状态且攻击力量足够时才造成伤害
            if (State != TentacleState.Attacking || attackForce < 0.7f)
                return false;

            //检测触手前半段的碰撞
            for (int i = 0; i < segments.Count / 2; i++)
            {
                float collisionPoint = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    segments[i].Position, segments[i + 1].Position, tentacleWidth * 0.5f, ref collisionPoint))
                {
                    return true;
                }
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.ShadowFlame, 180);

            //击中时的视觉反馈
            for (int i = 0; i < 10; i++)
            {
                Dust.NewDust(target.position, target.width, target.height, DustID.Shadowflame,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 0, default, 1.8f);
            }
        }

        //绘制触手
        public override bool PreDraw(ref Color lightColor)
        {
            if (segments.Count < 2)
                return false;

            SpriteBatch sb = Main.spriteBatch;
            Texture2D maskTexture = CWRAsset.DiffusionCircle.Value;

            //使用加法混合增强效果
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //绘制触手本体
            for (int i = 0; i < segments.Count - 1; i++)
            {
                Vector2 start = segments[i].Position;
                Vector2 end = segments[i + 1].Position;
                Vector2 direction = end - start;
                float rotation = direction.ToRotation();
                float distance = direction.Length();

                //计算宽度（根部更粗）
                float widthRatio = 1f - (i / (float)segments.Count);
                float currentWidth = tentacleWidth * widthRatio * (0.8f + attackForce * 0.4f);

                //颜色随深度变化
                Color segmentColor = Color.Lerp(new Color(80, 30, 100), new Color(120, 50, 140), widthRatio);
                segmentColor *= 0.8f;

                //绘制触手段落
                Vector2 scale = new Vector2(distance / maskTexture.Width, currentWidth / maskTexture.Height);
                Vector2 origin = new Vector2(0, maskTexture.Height / 2f);

                sb.Draw(maskTexture, start - Main.screenPosition, null, segmentColor,
                    rotation, origin, scale, SpriteEffects.None, 0);
            }

            //绘制触手光晕层
            for (int i = 0; i < segments.Count - 1; i++)
            {
                Vector2 start = segments[i].Position;
                Vector2 end = segments[i + 1].Position;
                Vector2 direction = end - start;
                float rotation = direction.ToRotation();
                float distance = direction.Length();

                float widthRatio = 1f - (i / (float)segments.Count);
                float currentWidth = tentacleWidth * widthRatio * 1.5f;

                Color glowColor = new Color(150, 80, 180, 0) * 0.5f * widthRatio;

                Vector2 scale = new Vector2(distance / maskTexture.Width, currentWidth / maskTexture.Height);
                Vector2 origin = new Vector2(0, maskTexture.Height / 2f);

                sb.Draw(maskTexture, start - Main.screenPosition, null, glowColor,
                    rotation, origin, scale, SpriteEffects.None, 0);
            }

            //绘制触手尖端
            Color tipColor = new Color(180, 100, 200) * (0.8f + attackForce * 0.4f);
            sb.Draw(maskTexture, tipPosition - Main.screenPosition, null, tipColor, 0f,
                maskTexture.Size() / 2f, 0.3f + attackForce * 0.2f, SpriteEffects.None, 0);

            //恢复正常混合
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        public override void OnKill(int timeLeft)
        {
            //死亡时生成粒子
            for (int i = 0; i < 30; i++)
            {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(5f, 5f);
                Dust.NewDust(Projectile.Center, 40, 40, DustID.Shadowflame, velocity.X, velocity.Y, 0, default, 1.5f);
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with
            {
                Volume = 0.5f,
                Pitch = -0.5f
            }, Projectile.Center);
        }

        //缓动函数
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1f, 3f) + c1 * (float)Math.Pow(t - 1f, 2f);
        }

        private static float EaseInBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }

        private static float EaseInQuad(float t)
        {
            return t * t;
        }

        private static float EaseOutCubic(float t)
        {
            return 1f - (float)Math.Pow(1f - t, 3f);
        }
    }

    //触手节点数据结构
    internal class TentacleSegment
    {
        public Vector2 Position;
        public float Length;
    }
}
