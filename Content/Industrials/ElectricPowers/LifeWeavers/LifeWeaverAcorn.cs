using CalamityOverhaul.Content.Industrials.ElectricPowers.TreeRegrowths;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.LifeWeavers
{
    /// <summary>
    /// 植树者抛射的橡子Actor，使用预计算的抛物线轨迹飞行
    /// </summary>
    internal class LifeWeaverAcorn : Actor
    {
        //目标种植位置(物块坐标)
        private int targetTileX;
        private int targetTileY;
        //地面类型
        private int groundType;

        //物理参数
        private Vector2 initialVelocity;
        private Vector2 startPosition;
        public const float Gravity = 0.25f;

        //飞行控制
        private float expectedFlightTime;
        private float currentFlightTime;

        //状态
        private bool landed;
        private int landedTimer;

        //轨迹粒子
        private int particleTimer;

        //旋转
        private float rotationSpeed;

        public override void OnSpawn(params object[] args) {
            Width = 10;
            Height = 10;
            DrawExtendMode = 600;
            DrawLayer = ActorDrawLayer.Default;

            if (args is not null && args.Length >= 4) {
                targetTileX = (int)args[0];
                targetTileY = (int)args[1];
                groundType = (int)args[2];
                expectedFlightTime = (float)args[3];
            }

            initialVelocity = Velocity;
            startPosition = Position;
            currentFlightTime = 0f;
            landed = false;
            landedTimer = 0;
            particleTimer = 0;
            rotationSpeed = Main.rand.NextFloat(0.08f, 0.15f) * (initialVelocity.X > 0 ? 1 : -1);

            //播放发射音效
            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.5f,
                Pitch = 0.3f
            }, Center);
        }

        public override void AI() {
            if (!landed) {
                UpdateFlying();
            }
            else {
                UpdateLanded();
            }
        }

        /// <summary>
        /// 更新飞行状态(使用精确的抛物线公式)
        /// </summary>
        private void UpdateFlying() {
            currentFlightTime++;

            //使用抛物线公式计算当前位置: pos = start + v*t + 0.5*g*t^2
            float t = currentFlightTime;
            float x = startPosition.X + initialVelocity.X * t;
            float y = startPosition.Y + initialVelocity.Y * t + 0.5f * Gravity * t * t;
            Position = new Vector2(x, y);

            //计算当前速度用于旋转方向: v = v0 + g*t
            float vx = initialVelocity.X;
            float vy = initialVelocity.Y + Gravity * t;
            Vector2 currentVelocity = new Vector2(vx, vy);

            //更新旋转
            Rotation += rotationSpeed;

            //生成轨迹粒子
            SpawnTrailParticles(currentVelocity);

            //检测是否到达预计时间(接近目标)
            if (currentFlightTime >= expectedFlightTime - 5f) {
                CheckLanding();
            }

            //安全检查：超时销毁
            if (currentFlightTime > expectedFlightTime + 60f) {
                ActorLoader.KillActor(WhoAmI);
            }
        }

        /// <summary>
        /// 生成飞行轨迹粒子
        /// </summary>
        private void SpawnTrailParticles(Vector2 currentVelocity) {
            if (Main.netMode == NetmodeID.Server) return;

            particleTimer++;

            //绿色叶片轨迹
            if (particleTimer % 3 == 0) {
                Vector2 dustVel = -currentVelocity * 0.1f + Main.rand.NextVector2Circular(0.5f, 0.5f);
                Dust dust = Dust.NewDustDirect(Center - Vector2.One * 4, 8, 8, DustID.Grass, dustVel.X, dustVel.Y, 100, default, 0.7f);
                dust.noGravity = true;
                dust.fadeIn = 0.8f;
            }

            //偶尔的闪光
            if (particleTimer % 8 == 0) {
                Dust sparkle = Dust.NewDustDirect(Center, 4, 4, DustID.GreenFairy, 0, 0, 150, default, 0.5f);
                sparkle.noGravity = true;
                sparkle.velocity = currentVelocity * 0.2f;
            }
        }

        /// <summary>
        /// 检测落地
        /// </summary>
        private void CheckLanding() {
            //检测是否接近目标位置
            Vector2 targetWorld = new Vector2(targetTileX * 16 + 8, targetTileY * 16);
            float distToTarget = Vector2.Distance(Center, targetWorld);

            //计算当前垂直速度
            float vy = initialVelocity.Y + Gravity * currentFlightTime;

            //接近目标并且在下降阶段
            if (distToTarget < 24f && vy > 0) {
                //精确落到目标位置
                Position = new Vector2(targetTileX * 16 + 8 - Width / 2, targetTileY * 16 - Height);
                OnLand();
                return;
            }

            //如果已经超过预计时间，检测是否碰到地面
            if (currentFlightTime >= expectedFlightTime) {
                int tileX = (int)(Center.X / 16);
                int tileY = (int)(Center.Y / 16);

                if (WorldGen.InWorld(tileX, tileY)) {
                    Tile tile = Main.tile[tileX, tileY];
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        OnLand();
                        return;
                    }
                }

                //如果超时太久且还没落地，直接落地
                if (currentFlightTime > expectedFlightTime + 30f) {
                    OnLand();
                }
            }
        }

        /// <summary>
        /// 落地处理
        /// </summary>
        private void OnLand() {
            landed = true;
            landedTimer = 0;
            Rotation = 0f;

            //播放落地音效
            SoundEngine.PlaySound(SoundID.Dig with {
                Volume = 0.5f,
                Pitch = 0.4f
            }, Center);

            //落地粒子效果
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 6; i++) {
                    Vector2 dustVel = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2f, -0.5f));
                    Dust dust = Dust.NewDustDirect(Center - Vector2.One * 4, 8, 8, DustID.Dirt, dustVel.X, dustVel.Y, 100, default, 1f);
                    dust.noGravity = false;
                }

                //绿色能量粒子
                for (int i = 0; i < 4; i++) {
                    Vector2 dustVel = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, -1f));
                    Dust dust = Dust.NewDustDirect(Center - Vector2.One * 4, 8, 8, DustID.GreenFairy, dustVel.X, dustVel.Y, 100, default, 0.8f);
                    dust.noGravity = true;
                }
            }
        }

        /// <summary>
        /// 更新落地状态
        /// </summary>
        private void UpdateLanded() {
            landedTimer++;

            //落地后短暂停留
            if (landedTimer == 20) {
                //检测并种植树木
                if (CanPlantTree()) {
                    //生成树木生长Actor
                    Vector2 treePos = new Vector2(targetTileX * 16, targetTileY * 16);
                    int actorIndex = ActorLoader.NewActor<TreeRegrowth>(treePos, Vector2.Zero);
                    if (actorIndex >= 0 && actorIndex < ActorLoader.MaxActorCount) {
                        ActorLoader.Actors[actorIndex].OnSpawn(targetTileX, targetTileY, GetTreeTypeForGround());
                    }
                }
            }

            //逐渐缩小消失
            if (landedTimer > 15) {
                Scale -= 0.05f;
                if (Scale <= 0) {
                    ActorLoader.KillActor(WhoAmI);
                }
            }
        }

        /// <summary>
        /// 检测是否可以种树
        /// </summary>
        private bool CanPlantTree() {
            if (!WorldGen.InWorld(targetTileX, targetTileY, 10)) return false;

            Tile groundTile = Main.tile[targetTileX, targetTileY];
            if (!groundTile.HasTile) return false;

            //检查上方空间
            for (int y = targetTileY - 1; y >= targetTileY - 10; y--) {
                if (!WorldGen.InWorld(targetTileX, y)) return false;
                Tile tile = Main.tile[targetTileX, y];
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 根据地面类型获取树木类型
        /// </summary>
        private int GetTreeTypeForGround() {
            return groundType switch {
                TileID.Sand or TileID.Crimsand or TileID.Ebonsand or TileID.Pearlsand => TileID.PalmTree,
                TileID.Ash => TileID.TreeAsh,
                _ => TileID.Trees
            };
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            Texture2D texture = TextureAssets.Item[ItemID.Acorn].Value;
            Vector2 origin = texture.Size() / 2f;
            Color color = Lighting.GetColor((int)(Center.X / 16), (int)(Center.Y / 16));

            //计算当前速度用于拖影
            float vx = initialVelocity.X;
            float vy = initialVelocity.Y + Gravity * currentFlightTime;
            Vector2 currentVelocity = new Vector2(vx, vy);

            //飞行时添加运动模糊效果
            if (!landed && currentVelocity.LengthSquared() > 4f) {
                //绘制拖影
                for (int i = 1; i <= 3; i++) {
                    Vector2 trailPos = Center - currentVelocity * i * 0.3f;
                    float trailAlpha = 1f - i * 0.25f;
                    float trailScale = Scale * (1f - i * 0.1f);
                    Color trailColor = color * trailAlpha * 0.5f;

                    spriteBatch.Draw(texture, trailPos - Main.screenPosition, null, trailColor, Rotation - rotationSpeed * i, origin, trailScale, SpriteEffects.None, 0f);
                }
            }

            //绘制主体
            spriteBatch.Draw(texture, Center - Main.screenPosition, null, color, Rotation, origin, Scale, SpriteEffects.None, 0f);

            //飞行时添加光晕
            if (!landed) {
                float glowIntensity = 0.3f + (float)Math.Sin(currentFlightTime * 0.2f) * 0.1f;
                Color glowColor = Color.LimeGreen * glowIntensity * 0.5f;
                spriteBatch.Draw(texture, Center - Main.screenPosition, null, glowColor, Rotation, origin, Scale * 1.2f, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
