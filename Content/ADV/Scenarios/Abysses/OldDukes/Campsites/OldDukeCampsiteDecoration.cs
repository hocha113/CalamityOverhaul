using InnoVault.RenderHandles;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    /// <summary>
    /// 老公爵营地装饰渲染器
    /// 负责渲染营地中的装饰物品和环境效果
    /// </summary>
    internal class OldDukeCampsiteDecoration : RenderHandle
    {
        //多个锅的位置信息
        private class PotData
        {
            public Vector2 WorldPosition;
            public float GlowTimer;
            public float BubbleTimer;
            public float SteamTimer;
            public List<SteamParticlePRT> SteamParticles = [];
            public List<BubbleParticlePRT> BubbleParticles = [];
            public int SteamSpawnTimer;
            public int BubbleSpawnTimer;
        }

        //旗杆位置信息
        private class FlagpoleData
        {
            public Vector2 WorldPosition;
            public float SwayTimer;
        }

        private static readonly List<PotData> pots = [];
        private static readonly List<FlagpoleData> flagpoles = [];
        private static bool decorationsPositionSet;

        /// <summary>
        /// 设置装饰物的位置
        /// 在营地生成时自动调用
        /// </summary>
        public static void SetupPotPosition(Vector2 campsiteCenter) {
            if (decorationsPositionSet) {
                return;
            }

            pots.Clear();
            flagpoles.Clear();

            //定义多个锅的相对偏移位置
            //主要布置在老公爵前方和两侧避免被遮挡
            Vector2[] potOffsets = [
                new Vector2(220f, 40f),
                new Vector2(-240f, 35f),
                new Vector2(280f, 50f),
                new Vector2(-160f, 55f)
            ];

            foreach (var offset in potOffsets) {
                Vector2 searchPos = campsiteCenter + offset;
                int tileX = (int)(searchPos.X / 16f);
                int tileY = (int)(searchPos.Y / 16f) - 60;

                Vector2 finalPos = searchPos;
                bool foundGround = false;

                //向下搜索最近的实心地面
                for (int y = tileY; y < tileY + 125; y++) {
                    if (y < 0 || y >= Main.maxTilesY) {
                        continue;
                    }

                    Tile tile = Main.tile[tileX, y];
                    if (tile != null && tile.HasTile && Main.tileSolid[tile.TileType]) {
                        finalPos = new Vector2(tileX * 16f + 8f, y * 16f - 16f);
                        foundGround = true;
                        break;
                    }
                }

                if (foundGround || true) {
                    PotData pot = new PotData {
                        WorldPosition = finalPos,
                        GlowTimer = Main.rand.NextFloat(0f, MathHelper.TwoPi),
                        BubbleTimer = Main.rand.NextFloat(0f, MathHelper.TwoPi),
                        SteamTimer = Main.rand.NextFloat(0f, MathHelper.TwoPi)
                    };
                    pots.Add(pot);
                }
            }

            //定义旗杆的相对偏移位置
            //放置在营地的高处和侧面
            Vector2[] flagpoleOffsets = [
                new Vector2(-180f, -20f),
                new Vector2(200f, -15f)
            ];

            foreach (var offset in flagpoleOffsets) {
                Vector2 searchPos = campsiteCenter + offset;
                int tileX = (int)(searchPos.X / 16f);
                int tileY = (int)(searchPos.Y / 16f) - 60;

                Vector2 finalPos = searchPos;
                bool foundGround = false;

                //向下搜索最近的实心地面
                for (int y = tileY; y < tileY + 125; y++) {
                    if (y < 0 || y >= Main.maxTilesY) {
                        continue;
                    }

                    Tile tile = Main.tile[tileX, y];
                    if (tile != null && tile.HasTile && Main.tileSolid[tile.TileType]) {
                        finalPos = new Vector2(tileX * 16f + 8f, y * 16f);
                        foundGround = true;
                        break;
                    }
                }

                if (foundGround) {
                    FlagpoleData flagpole = new FlagpoleData {
                        WorldPosition = finalPos,
                        SwayTimer = Main.rand.NextFloat(0f, MathHelper.TwoPi)
                    };
                    flagpoles.Add(flagpole);
                }
            }

            //放置老箱子
            PlaceOldChest(campsiteCenter);

            decorationsPositionSet = true;
        }

        /// <summary>
        /// 放置老箱子到营地
        /// </summary>
        private static void PlaceOldChest(Vector2 campsiteCenter) {
            if (VaultUtils.isClient) {
                return;
            }

            //箱子放在营地左侧较远的位置
            Vector2 chestOffset = new Vector2(-320f, 20f);
            Vector2 searchPos = campsiteCenter + chestOffset;
            int baseTileX = (int)(searchPos.X / 16f);
            int baseTileY = (int)(searchPos.Y / 16f) - 60;

            //向下搜索地面
            for (int y = baseTileY; y < baseTileY + 125; y++) {
                if (y < 0 || y >= Main.maxTilesY) {
                    continue;
                }

                Tile tile = Main.tile[baseTileX, y];
                if (tile != null && tile.HasTile && Main.tileSolid[tile.TileType]) {
                    //找到地面在上方放置箱子
                    int chestTileX = baseTileX - 2;
                    int chestTileY = y - 1;

                    //清理箱子放置区域箱子是6x4格
                    for (int cx = 0; cx < 6; cx++) {
                        for (int cy = 0; cy < 4; cy++) {
                            int clearX = chestTileX + cx;
                            int clearY = chestTileY - cy;

                            if (clearX >= 0 && clearX < Main.maxTilesX && clearY >= 0 && clearY < Main.maxTilesY) {
                                Tile clearTile = Main.tile[clearX, clearY];
                                if (clearTile != null && clearTile.HasTile) {
                                    WorldGen.KillTile(clearX, clearY, false, false, true);
                                }
                            }
                        }
                    }

                    //确保底座是实心的
                    for (int bx = 0; bx < 6; bx++) {
                        int baseX = chestTileX + bx;
                        int baseY = chestTileY + 1;

                        if (baseX >= 0 && baseX < Main.maxTilesX && baseY >= 0 && baseY < Main.maxTilesY) {
                            Tile baseTile = Main.tile[baseX, baseY];
                            baseTile.Slope = SlopeType.Solid;
                            WorldGen.PlaceTile(baseX, baseY, CWRID.Tile_SulphurousSand, true, true);
                        }
                    }

                    //放置老箱子（箱子的原点在3,3位置）
                    int chestType = ModContent.TileType<Items.OldDuchests.OldDuchestTile>();
                    int placeX = chestTileX + 3;
                    int placeY = chestTileY;

                    WorldGen.PlaceTile(placeX, placeY, chestType, true, false, -1, 0);

                    //获取箱子左上角位置并创建TP实体
                    if (TPUtils.TryGetTopLeft(placeX, placeY, out var point)) {
                        TileProcessorLoader.AddInWorld(chestType, point, null);

                        //填充箱子内容
                        if (TileProcessorLoader.ByPositionGetTP(point, out Items.OldDuchests.OldDuchestTP chestTP)) {
                            FillChestWithItems(chestTP);
                        }

                        //网络同步
                        if (Main.netMode == NetmodeID.Server) {
                            NetMessage.SendObjectPlacement(-1, placeX, placeY, chestType, 0, 0, -1, -1);
                            TileProcessorNetWork.PlaceInWorldNetSend(VaultMod.Instance, chestType, point);
                        }
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// 向箱子TP实体中填充随机物品
        /// </summary>
        private static void FillChestWithItems(Items.OldDuchests.OldDuchestTP chestTP) {
            if (chestTP == null) {
                return;
            }

            //清空现有物品
            chestTP.storedItems.Clear();

            //添加钱币金额随机
            int coinAmount = Main.rand.Next(50, 200);
            int platinumCoins = coinAmount / 100;
            int goldCoins = (coinAmount % 100) / 10;
            int silverCoins = coinAmount % 10;

            if (platinumCoins > 0) {
                Item coin = new Item();
                coin.SetDefaults(ItemID.PlatinumCoin);
                coin.stack = platinumCoins;
                chestTP.storedItems.Add(coin);
            }

            if (goldCoins > 0) {
                Item coin = new Item();
                coin.SetDefaults(ItemID.GoldCoin);
                coin.stack = goldCoins;
                chestTP.storedItems.Add(coin);
            }

            if (silverCoins > 0) {
                Item coin = new Item();
                coin.SetDefaults(ItemID.SilverCoin);
                coin.stack = silverCoins;
                chestTP.storedItems.Add(coin);
            }

            //获取老公爵掉落物品
            HashSet<int> oldDukeDrops = OldDukeShops.OldDukeShopHandle.GetNPCDrops(CWRID.NPC_OldDuke, true);
            List<int> dropList = oldDukeDrops.ToList();
            List<int> dropList2 = [];
            foreach (int drop in dropList) {
                Item item = new Item(drop);
                if (item.value > 6000 * 30) {
                    dropList2.Add(drop);
                }
            }
            dropList = dropList2;

            //随机选择3到5个物品
            int itemCount = Main.rand.Next(3, 6);
            for (int i = 0; i < itemCount && chestTP.storedItems.Count < 240; i++) {
                if (dropList.Count == 0) {
                    break;
                }

                int randomIndex = Main.rand.Next(dropList.Count);
                int itemType = dropList[randomIndex];
                dropList.RemoveAt(randomIndex);

                Item item = new();
                item.SetDefaults(itemType);
                item.stack = 1;
                chestTP.storedItems.Add(item);
            }

            //添加一些海洋主题的消耗品
            int[] oceanItems = [
                ItemID.Coral,
                ItemID.Starfish,
                ItemID.Seashell,
                ItemID.SharkFin,
                ItemID.GillsPotion,
                ItemID.SonarPotion
            ];

            int extraItemCount = Main.rand.Next(2, 4);
            for (int i = 0; i < extraItemCount && chestTP.storedItems.Count < 240; i++) {
                int itemType = oceanItems[Main.rand.Next(oceanItems.Length)];
                Item item = new Item();
                item.SetDefaults(itemType);
                item.stack = Main.rand.Next(3, 10);
                chestTP.storedItems.Add(item);
            }

            //保存数据到TP
            chestTP.SendData();
        }

        /// <summary>
        /// 重置装饰状态
        /// 在营地清除时调用
        /// </summary>
        public static void ResetDecoration() {
            decorationsPositionSet = false;
            pots.Clear();
            flagpoles.Clear();
        }

        public override void UpdateBySystem(int index) {
            if (!OldDukeCampsite.IsGenerated || !decorationsPositionSet) {
                return;
            }

            //更新每个锅的状态
            foreach (var pot in pots) {
                UpdatePot(pot);
            }

            //更新旗杆摇摆
            foreach (var flagpole in flagpoles) {
                flagpole.SwayTimer += 0.02f;
                if (flagpole.SwayTimer > MathHelper.TwoPi) {
                    flagpole.SwayTimer -= MathHelper.TwoPi;
                }
            }
        }

        /// <summary>
        /// 更新单个锅的状态
        /// </summary>
        private void UpdatePot(PotData pot) {
            pot.GlowTimer += 0.025f;
            pot.BubbleTimer += 0.035f;
            pot.SteamTimer += 0.028f;

            if (pot.GlowTimer > MathHelper.TwoPi) pot.GlowTimer -= MathHelper.TwoPi;
            if (pot.BubbleTimer > MathHelper.TwoPi) pot.BubbleTimer -= MathHelper.TwoPi;
            if (pot.SteamTimer > MathHelper.TwoPi) pot.SteamTimer -= MathHelper.TwoPi;

            pot.SteamSpawnTimer++;
            if (pot.SteamSpawnTimer >= 10 && pot.SteamParticles.Count < 12) {
                pot.SteamSpawnTimer = 0;
                SpawnSteamParticle(pot);
            }

            pot.BubbleSpawnTimer++;
            if (pot.BubbleSpawnTimer >= 15 && pot.BubbleParticles.Count < 6) {
                pot.BubbleSpawnTimer = 0;
                SpawnBubbleParticle(pot);
            }

            for (int i = pot.SteamParticles.Count - 1; i >= 0; i--) {
                if (pot.SteamParticles[i].Update()) {
                    pot.SteamParticles.RemoveAt(i);
                }
            }

            for (int i = pot.BubbleParticles.Count - 1; i >= 0; i--) {
                if (pot.BubbleParticles[i].Update()) {
                    pot.BubbleParticles.RemoveAt(i);
                }
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (!OldDukeCampsite.IsGenerated || !decorationsPositionSet) {
                return;
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //绘制所有锅
            foreach (var pot in pots) {
                Vector2 screenPos = pot.WorldPosition - Main.screenPosition;

                if (!VaultUtils.IsPointOnScreen(screenPos, 200)) {
                    continue;
                }

                foreach (var particle in pot.BubbleParticles) {
                    particle.Draw(spriteBatch);
                }

                DrawPot(spriteBatch, screenPos, pot);

                foreach (var particle in pot.SteamParticles) {
                    particle.Draw(spriteBatch);
                }
            }

            //绘制所有旗杆
            foreach (var flagpole in flagpoles) {
                Vector2 screenPos = flagpole.WorldPosition - Main.screenPosition;

                if (!VaultUtils.IsPointOnScreen(screenPos, 200)) {
                    continue;
                }

                DrawFlagpole(spriteBatch, screenPos, flagpole);
            }

            Main.spriteBatch.End();
        }

        /// <summary>
        /// 绘制锅
        /// </summary>
        private void DrawPot(SpriteBatch sb, Vector2 screenPos, PotData pot) {
            if (OldDukeCampsite.OldPot == null) {
                return;
            }

            Texture2D potTexture = OldDukeCampsite.OldPot;
            Vector2 origin = potTexture.Size() / 2f;

            float glowIntensity = (MathF.Sin(pot.GlowTimer * 3f) * 0.5f + 0.5f) * 0.6f;
            Color fireGlow = new Color(255, 120, 60) with { A = 0 };

            for (int i = 0; i < 3; i++) {
                float glowScale = 1.1f + i * 0.08f;
                float glowAlpha = glowIntensity * (1f - i * 0.3f);
                Vector2 glowOffset = new Vector2(0, -6f + i * 2f);

                sb.Draw(
                    potTexture,
                    screenPos + glowOffset,
                    null,
                    fireGlow * glowAlpha,
                    0f,
                    origin,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }

            sb.Draw(
                potTexture,
                screenPos,
                null,
                Color.White,
                0f,
                origin,
                1f,
                SpriteEffects.None,
                0f
            );

            DrawHeatWave(sb, screenPos, pot);
        }

        /// <summary>
        /// 绘制旗杆
        /// </summary>
        private void DrawFlagpole(SpriteBatch sb, Vector2 screenPos, FlagpoleData flagpole) {
            if (OldDukeCampsite.Oldflagpole == null) {
                return;
            }

            Texture2D flagTexture = OldDukeCampsite.Oldflagpole;
            Vector2 origin = new Vector2(flagTexture.Width / 2f, flagTexture.Height);

            //风吹摇摆效果
            float swayAmount = MathF.Sin(flagpole.SwayTimer * 2f) * 0.08f;

            sb.Draw(
                flagTexture,
                screenPos,
                null,
                Color.White,
                swayAmount,
                origin,
                1f,
                SpriteEffects.None,
                0f
            );

            //添加旗帜的飘动感绘制稍微透明的重影
            for (int i = 1; i <= 2; i++) {
                float offsetAmount = i * 3f;
                float alpha = 0.3f / i;
                Vector2 offset = new Vector2(-offsetAmount * MathF.Sin(swayAmount), 0);

                sb.Draw(
                    flagTexture,
                    screenPos + offset,
                    null,
                    Color.White * alpha,
                    swayAmount,
                    origin,
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 绘制热气波动效果
        /// </summary>
        private void DrawHeatWave(SpriteBatch sb, Vector2 screenPos, PotData pot) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int i = 0; i < 3; i++) {
                float t = i / 3f;
                float yOffset = -20f - i * 8f;
                float wavePhase = pot.SteamTimer + t * MathHelper.Pi;
                float xOffset = MathF.Sin(wavePhase) * 6f;

                Vector2 wavePos = screenPos + new Vector2(xOffset, yOffset);
                Color waveColor = new Color(255, 200, 150) * (0.15f * (1f - t * 0.5f));

                sb.Draw(
                    pixel,
                    wavePos,
                    new Rectangle(0, 0, 1, 1),
                    waveColor,
                    0f,
                    new Vector2(0.5f),
                    new Vector2(20f - i * 4f, 1.5f),
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 生成蒸汽粒子
        /// </summary>
        private void SpawnSteamParticle(PotData pot) {
            Vector2 spawnPos = pot.WorldPosition;
            spawnPos += new Vector2(
                Main.rand.NextFloat(-12f, 12f),
                -24f + Main.rand.NextFloat(-4f, 4f)
            );

            pot.SteamParticles.Add(new SteamParticlePRT(spawnPos));
        }

        /// <summary>
        /// 生成气泡粒子
        /// </summary>
        private void SpawnBubbleParticle(PotData pot) {
            Vector2 spawnPos = pot.WorldPosition;
            spawnPos += new Vector2(
                Main.rand.NextFloat(-10f, 10f),
                Main.rand.NextFloat(-8f, 0f)
            );

            pot.BubbleParticles.Add(new BubbleParticlePRT(spawnPos));
        }

        /// <summary>
        /// 蒸汽粒子
        /// 从锅中升起的热蒸汽效果
        /// </summary>
        private class SteamParticlePRT
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale;
            public float Life;
            public float MaxLife;
            public float Rotation;
            public float RotationSpeed;
            public Color Color;

            public SteamParticlePRT(Vector2 startPos) {
                Position = startPos;
                Velocity = new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f),
                    Main.rand.NextFloat(-1.5f, -0.8f)
                );
                Scale = Main.rand.NextFloat(0.4f, 0.8f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(45f, 75f);
                Rotation = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                RotationSpeed = Main.rand.NextFloat(-0.05f, 0.05f);

                Color = VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Yellow, Color.YellowGreen);
            }

            public bool Update() {
                Life++;
                Position += Velocity;
                Rotation += RotationSpeed;

                Velocity.X += MathF.Sin(Life * 0.08f) * 0.03f;

                Velocity.Y *= 0.98f;
                Velocity.X *= 0.99f;

                Scale += 0.008f;

                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb) {
                Texture2D pixel = CWRAsset.SoftGlow.Value;
                float alpha = MathF.Sin((Life / MaxLife) * MathHelper.Pi);
                Vector2 screenPos = Position - Main.screenPosition;

                sb.Draw(
                    pixel,
                    screenPos,
                    null,
                    Color with { A = 0 } * (alpha * 0.5f),
                    Rotation,
                    pixel.Size() / 2,
                    Scale,
                    SpriteEffects.None,
                    0f
                );

                sb.Draw(
                    pixel,
                    screenPos,
                    null,
                    Color with { A = 0 } * (alpha * 0.7f),
                    Rotation,
                    pixel.Size() / 2,
                    Scale / 2,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        /// <summary>
        /// 气泡粒子
        /// 锅内沸腾的气泡效果
        /// </summary>
        private class BubbleParticlePRT
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale;
            public float Life;
            public float MaxLife;
            public Color Color;

            public BubbleParticlePRT(Vector2 startPos) {
                Position = startPos;
                Velocity = new Vector2(
                    Main.rand.NextFloat(-0.2f, 0.2f),
                    Main.rand.NextFloat(-0.8f, -0.4f)
                );
                Scale = Main.rand.NextFloat(0.3f, 0.6f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(20f, 35f);

                Color = Main.rand.NextBool()
                    ? new Color(140, 200, 120, 200)
                    : new Color(160, 220, 140, 220);
            }

            public bool Update() {
                Life++;
                Position += Velocity;

                Velocity.X += MathF.Sin(Life * 0.15f) * 0.01f;

                Velocity *= 0.98f;

                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float alpha = MathF.Sin((Life / MaxLife) * MathHelper.Pi);
                Vector2 screenPos = Position - Main.screenPosition;

                sb.Draw(
                    pixel,
                    screenPos,
                    new Rectangle(0, 0, 1, 1),
                    Color * (alpha * 0.5f),
                    0f,
                    new Vector2(0.5f),
                    Scale * 6f,
                    SpriteEffects.None,
                    0f
                );

                sb.Draw(
                    pixel,
                    screenPos,
                    new Rectangle(0, 0, 1, 1),
                    Color * alpha,
                    0f,
                    new Vector2(0.5f),
                    Scale * 3f,
                    SpriteEffects.None,
                    0f
                );

                Vector2 highlightOffset = new Vector2(-Scale * 1.5f, -Scale * 1.5f);
                sb.Draw(
                    pixel,
                    screenPos + highlightOffset,
                    new Rectangle(0, 0, 1, 1),
                    new Color(255, 255, 255, 150) * alpha,
                    0f,
                    new Vector2(0.5f),
                    Scale * 1.5f,
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
