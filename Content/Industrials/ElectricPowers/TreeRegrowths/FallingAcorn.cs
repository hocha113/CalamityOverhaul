using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.TreeRegrowths
{
    /// <summary>
    /// 橡子下落动画Actor，落点在生成时即锁定，落地后交棒给 <see cref="TreeRegrowth"/>
    /// </summary>
    internal class FallingAcorn : Actor
    {
        //落点/树种/蓝图种子，权威端Setup后经SyncVar同步
        [SyncVar]
        public int targetTileX;
        [SyncVar]
        public int targetTileY;
        [SyncVar]
        public int treeTileType;
        [SyncVar]
        public int growSeed;

        private float fallSpeed;
        private float rotationSpeed;
        private bool landed;
        private int landedTimer;

        /// <summary>
        /// 权威端生成后立即调用，锁定落点与这棵树的蓝图种子
        /// </summary>
        public void Setup(int tileX, int groundY, int treeType, int seed) {
            targetTileX = tileX;
            targetTileY = groundY;
            treeTileType = treeType;
            growSeed = seed;
            NetUpdate = true;
        }

        public override void OnSpawn(params object[] args) {
            Width = 12;
            Height = 12;
            DrawExtendMode = 400;
            DrawLayer = ActorDrawLayer.Default;

            fallSpeed = 0f;
            rotationSpeed = Main.rand.NextFloat(-0.15f, 0.15f);
            landed = false;
            landedTimer = 0;
        }

        public override void AI() {
            //SyncVar未到达前悬停等待(客户端首帧)
            if (treeTileType == 0) {
                return;
            }

            if (!landed) {
                UpdateFalling();
            }
            else {
                UpdateLanded();
            }
        }

        private void UpdateFalling() {
            fallSpeed = Math.Min(fallSpeed + 0.3f, 12f);
            Position.Y += fallSpeed;
            Rotation += rotationSpeed;

            //下落轨迹粒子
            if (Main.rand.NextBool(3) && !Main.dedServ) {
                Dust dust = Dust.NewDustDirect(Position, Width, Height, DustID.Grass, 0, -1, 100, default, 0.8f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }

            //抵达地表(落在地面物块顶面上)
            float targetWorldY = targetTileY * 16 - Height;
            if (Position.Y >= targetWorldY) {
                Position.Y = targetWorldY;
                landed = true;
                Rotation = 0f;

                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = 0.3f }, Center);

                    for (int i = 0; i < 8; i++) {
                        Vector2 dustVel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-3f, -1f));
                        Dust dust = Dust.NewDustDirect(Position, Width, Height, DustID.Dirt, dustVel.X, dustVel.Y, 100, default, 1.2f);
                        dust.noGravity = false;
                    }
                }
            }
        }

        private void UpdateLanded() {
            landedTimer++;

            //落地稍作停留后交棒生长演出；蓝图生成与落地校验均以锁定的落点为准
            if (landedTimer == 30 && !VaultUtils.isClient) {
                if (TreeBlueprint.TryGenerate(targetTileX, targetTileY, treeTileType, growSeed, out TreeBlueprint blueprint)
                    && blueprint.CanPlace()) {
                    int actorIndex = ActorLoader.NewActor<TreeRegrowth>(new Vector2(targetTileX * 16, targetTileY * 16 - 16), Vector2.Zero);
                    if (actorIndex >= 0 && actorIndex < ActorLoader.MaxActorCount
                        && ActorLoader.Actors[actorIndex] is TreeRegrowth regrowth) {
                        regrowth.Setup(targetTileX, targetTileY, treeTileType, growSeed);
                    }
                }
            }

            //逐渐缩小消失
            if (landedTimer > 25) {
                Scale -= 0.04f;
                if (Scale <= 0f && !VaultUtils.isClient) {
                    RequestKill();
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (Scale <= 0f) {
                return false;
            }
            Texture2D texture = TextureAssets.Item[ItemID.Acorn].Value;
            Vector2 origin = texture.Size() / 2f;
            Color color = Lighting.GetColor((int)(Center.X / 16), (int)(Center.Y / 16));

            spriteBatch.Draw(texture, Center - Main.screenPosition, null, color, Rotation - MathHelper.PiOver4, origin, Scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}
