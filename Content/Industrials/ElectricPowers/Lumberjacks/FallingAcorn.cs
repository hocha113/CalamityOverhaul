using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Lumberjacks
{
    /// <summary>
    /// 橡子下落动画Actor，用于表示树木被砍伐后的种植演出
    /// </summary>
    internal class FallingAcorn : Actor
    {
        //目标种植位置(物块坐标)
        private int targetTileX;
        private int targetTileY;
        //下落速度
        private float fallSpeed;
        //旋转速度
        private float rotationSpeed;
        //是否已经落地
        private bool landed;
        //落地后的延迟计时器
        private int landedTimer;
        //原始树木类型，用于确定生长的树木种类
        private int originalTreeType;

        public override void OnSpawn(params object[] args) {
            Width = 12;
            Height = 12;
            DrawExtendMode = 400;
            DrawLayer = ActorDrawLayer.Default;

            if (args is not null && args.Length >= 3) {
                targetTileX = (int)args[0];
                targetTileY = (int)args[1];
                originalTreeType = (int)args[2];
            }

            //从目标位置上方生成
            Position = new Vector2(targetTileX * 16 + 8, (targetTileY - 20) * 16);
            fallSpeed = 0f;
            rotationSpeed = Main.rand.NextFloat(-0.15f, 0.15f);
            landed = false;
            landedTimer = 0;
        }

        public override void AI() {
            if (!landed) {
                //重力加速
                fallSpeed += 0.3f;
                fallSpeed = Math.Min(fallSpeed, 12f);

                Position.Y += fallSpeed;
                Rotation += rotationSpeed;

                //左右轻微摇摆
                Position.X += (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.5f;

                //生成下落轨迹粒子
                if (Main.rand.NextBool(3) && Main.netMode != NetmodeID.Server) {
                    Dust dust = Dust.NewDustDirect(Position, Width, Height, DustID.Grass, 0, -1, 100, default, 0.8f);
                    dust.noGravity = true;
                    dust.velocity *= 0.3f;
                }

                //检测是否到达目标位置
                float targetWorldY = targetTileY * 16;
                if (Position.Y >= targetWorldY) {
                    Position.Y = targetWorldY;
                    landed = true;
                    Rotation = 0f;

                    //播放落地音效
                    SoundEngine.PlaySound(SoundID.Dig with {
                        Volume = 0.6f,
                        Pitch = 0.3f
                    }, Center);

                    //落地粒子爆发
                    if (Main.netMode != NetmodeID.Server) {
                        for (int i = 0; i < 8; i++) {
                            Vector2 dustVel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-3f, -1f));
                            Dust dust = Dust.NewDustDirect(Position, Width, Height, DustID.Dirt, dustVel.X, dustVel.Y, 100, default, 1.2f);
                            dust.noGravity = false;
                        }
                    }
                }
            }
            else {
                landedTimer++;

                //落地后短暂停留，然后触发树木生长
                if (landedTimer == 30) {
                    //检测地形是否可以种树
                    if (CanPlantTree(targetTileX, targetTileY)) {
                        //生成树木生长Actor
                        int actorIndex = ActorLoader.NewActor<TreeRegrowth>(Position, Vector2.Zero);
                        ActorLoader.Actors[actorIndex].OnSpawn(targetTileX, targetTileY, originalTreeType);
                    }
                }

                //逐渐缩小消失
                if (landedTimer > 25) {
                    Scale -= 0.04f;
                    if (Scale <= 0) {
                        ActorLoader.KillActor(WhoAmI);
                    }
                }
            }
        }

        /// <summary>
        /// 检测指定位置是否可以种植树木
        /// </summary>
        private static bool CanPlantTree(int x, int y) {
            //检查是否在世界范围内
            if (!WorldGen.InWorld(x, y, 10)) return false;

            //检查目标位置上方是否有足够空间(至少需要16格高度)
            for (int checkY = y - 1; checkY >= y - 16; checkY--) {
                if (!WorldGen.InWorld(x, checkY)) return false;
                Tile aboveTile = Main.tile[x, checkY];
                if (aboveTile.HasTile && Main.tileSolid[aboveTile.TileType]) {
                    return false;
                }
            }

            //检查地面物块是否适合种树
            Tile groundTile = Main.tile[x, y];
            if (!groundTile.HasTile) return false;

            int groundType = groundTile.TileType;

            //草地、腐化草、猩红草、神圣草、丛林草都可以种树
            bool validGround = groundType == TileID.Grass ||
                               groundType == TileID.CorruptGrass ||
                               groundType == TileID.CrimsonGrass ||
                               groundType == TileID.HallowedGrass ||
                               groundType == TileID.JungleGrass ||
                               groundType == TileID.MushroomGrass ||
                               groundType == TileID.Sand ||
                               groundType == TileID.Crimsand ||
                               groundType == TileID.Ebonsand ||
                               groundType == TileID.Pearlsand ||
                               groundType == TileID.Ash;

            return validGround;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            //使用橡子物品纹理
            Texture2D texture = TextureAssets.Item[ItemID.Acorn].Value;
            Vector2 origin = texture.Size() / 2f;
            Color color = Lighting.GetColor((int)(Center.X / 16), (int)(Center.Y / 16));

            spriteBatch.Draw(texture, Center - Main.screenPosition, null, color, Rotation - MathHelper.PiOver4, origin, Scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}
