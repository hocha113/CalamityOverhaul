using CalamityOverhaul.Content.ADV.Scenarios.Draedons.ExoMechdusaSums;
using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers
{
    /// <summary>
    /// 量子塔自我构建器
    /// </summary>
    internal class CQETConstructor : ModItem
    {
        public override string Texture => CWRConstant.Item_Tools + "CQETConstructor";
        public static LocalizedText UseConstructionBlueprint;
        public override void SetStaticDefaults() => UseConstructionBlueprint = this.GetLocalization(nameof(UseConstructionBlueprint), () => "学习构造蓝图(量子塔)");
        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 99;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = Item.buyPrice(gold: 2);
            Item.rare = ItemRarityID.LightRed;
            Item.createTile = ModContent.TileType<CQETConstructorTile>();
        }
        public static LocalizedText RecipeCondition(out Func<bool> condition) {
            condition = new Func<bool>(() =>
            Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)
            && halibutPlayer.ADCSave.UseConstructionBlueprint);
            return UseConstructionBlueprint;
        }
        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<StarflowPlatedBlock>(80)
                .AddIngredient(ItemID.Wire, 20)
                .AddIngredient(ItemID.Actuator, 20)
                .AddCondition(RecipeCondition(out var condition), condition)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }

    internal class CQETConstructorTile : ModTile
    {
        public override string Texture => CWRConstant.Item_Tools + "CQETConstructorTile";

        public override void SetStaticDefaults() {
            Main.tileLighted[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            AddMapEntry(new Color(150, 200, 255), VaultUtils.GetLocalizedItemName<CQETConstructor>());

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = 2;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.Origin = new Point16(0, 1);
            TileObjectData.newTile.CoordinateHeights = [16, 16];
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile
                | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.addTile(Type);
        }

        public override void MouseOver(int i, int j) => Main.LocalPlayer.SetMouseOverByTile(ModContent.ItemType<CQETConstructor>());

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            r = 0.15f;
            g = 0.25f;
            b = 0.4f;
        }

        public override bool CreateDust(int i, int j, ref int type) {
            type = DustID.TreasureSparkle;
            return true;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }
            if (!TileProcessorLoader.ByPositionGetTP(point, out CQETConstructorTP constructorTP)) {
                return false;
            }

            Tile t = Main.tile[i, j];
            int frameXPos = t.TileFrameX;
            int frameYPos = t.TileFrameY;
            frameYPos += constructorTP.frame * 18 * 2;
            Texture2D tex = TextureAssets.Tile[Type].Value;
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 drawOffset = new Vector2(i * 16 - Main.screenPosition.X, j * 16 - Main.screenPosition.Y) + offset;
            Color drawColor = Lighting.GetColor(i, j);
            if (!t.IsHalfBlock && t.Slope == 0) {
                spriteBatch.Draw(tex, drawOffset, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            else if (t.IsHalfBlock) {
                spriteBatch.Draw(tex, drawOffset + Vector2.UnitY * 8f, new Rectangle(frameXPos, frameYPos, 16, 16)
                    , drawColor, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.0f);
            }
            return false;
        }
    }

    //检测到量子塔自我构建器后进行的处理
    //检测左边两格为StarflowPlatedBlock，右侧两格为StarflowPlatedBlock，并往上检测高14格都为StarflowPlatedBlock
    //也就是需要6*14-4=80个StarflowPlatedBlock
    //如果满足，则将这些物块替换为量子塔，也就是移除自己，并在原地放置量子塔 DeploySignaltowerTile，这个多结构物块也是6*14大小，刚好够填充
    internal class CQETConstructorTP : TileProcessor, ILocalizedModType
    {
        public override int TargetTileID => ModContent.TileType<CQETConstructorTile>();

        public string LocalizationCategory => "UI";

        private int constructionTime;
        private int checkDelay;
        private bool isConstructing;
        private const int ConstructionDuration = 180; //3秒（60帧/秒）
        private const int CheckInterval = 30; //每0.5秒检测一次

        public int frame;
        private int frameCounter;

        //用于搭建指示
        private bool showGuide;
        private int guideAlphaTime;
        private const float GuideMaxDistance = 300f; //玩家距离小于此值时显示指示

        //本地化文本
        public static LocalizedText GuideText_NeedBlocks { get; private set; }
        public static LocalizedText GuideText_Ready { get; private set; }

        public override void SetStaticDefaults() {
            GuideText_NeedBlocks = this.GetLocalization(nameof(GuideText_NeedBlocks), () => "需要 {0} 个星流镀板");
            GuideText_Ready = this.GetLocalization(nameof(GuideText_Ready), () => "准备就绪！");
        }

        public override void Update() {
            if (++frameCounter > 5) {
                frameCounter = 0;
                if (frame < 28) {
                    frame++;
                    if (frame == 24) {
                        SoundEngine.PlaySound(ExoMechdusaSumRender.AresIconHover with { Pitch = 0.2f }, PosInWorld);
                    }
                }
            }

            checkDelay++;

            if (checkDelay >= CheckInterval) {
                checkDelay = 0;

                if (!isConstructing && CheckConstructionConditions()) {
                    isConstructing = true;
                    constructionTime = 0;
                    SoundEngine.PlaySound(SoundID.Item4, PosInWorld);
                }
            }

            if (isConstructing) {
                constructionTime++;

                //显示粒子效果
                if (constructionTime % 5 == 0) {
                    CreateConstructionDust();
                }

                //完成构建
                if (constructionTime >= ConstructionDuration) {
                    PerformConstruction();
                    isConstructing = false;
                }
            }

            //更新搭建指示状态
            UpdateGuideDisplay();
        }

        private void UpdateGuideDisplay() {
            if (VaultUtils.isServer) {
                return;
            }

            //检测玩家是否在附近
            Player closestPlayer = CenterInWorld.FindClosestPlayer(GuideMaxDistance);
            showGuide = closestPlayer != null && !isConstructing;

            if (showGuide) {
                guideAlphaTime++;
            }
            else {
                guideAlphaTime = 0;
            }
        }

        private bool CheckConstructionConditions() {
            if (VaultUtils.isClient) {
                return false;
            }

            int starflowBlockType = ModContent.TileType<StarflowPlatedBlockTile>();

            //构建器位于底部中间位置，需要检测周围6×14区域
            //构建器是2×2，位于底部中间（占用X: 2-3, Y: 12-13）
            int baseX = Position.X - 2; //向左延伸2格
            int baseY = Position.Y - 12; //向上延伸12格（总高14格）

            //检测6×14区域是否都是StarflowPlatedBlock
            for (int x = 0; x < 6; x++) {
                for (int y = 0; y < 14; y++) {
                    int checkX = baseX + x;
                    int checkY = baseY + y;

                    //跳过构建器自身位置（2×2）
                    if (x >= 2 && x < 4 && y >= 12 && y < 14) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (!tile.HasTile || tile.TileType != starflowBlockType) {
                        return false;
                    }
                }
            }

            return true;
        }

        private void PerformConstruction() {
            if (VaultUtils.isClient) {
                return;
            }

            int starflowBlockType = ModContent.TileType<StarflowPlatedBlockTile>();
            int signalTowerType = ModContent.TileType<DeploySignaltowerTile>();

            int baseX = Position.X - 2;
            int baseY = Position.Y - 12;

            //清除所有StarflowPlatedBlock和构建器
            for (int x = 0; x < 6; x++) {
                for (int y = 0; y < 14; y++) {
                    int checkX = baseX + x;
                    int checkY = baseY + y;

                    Tile tile = Framing.GetTileSafely(checkX, checkY);
                    if (tile.HasTile) {
                        WorldGen.KillTile(checkX, checkY, false, false, true);
                    }
                }
            }

            int placeX = baseX + 2;
            int placeY = baseY + 13;
            //放置信号塔（6×14，原点在底部中间偏左：2, 13）
            WorldGen.PlaceTile(placeX, placeY, signalTowerType, true, true);
            //放置TP实体
            if (TPUtils.TryGetTopLeft(placeX, placeY, out var point)) {
                TileProcessorLoader.AddInWorld(signalTowerType, point, null);
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendObjectPlacement(-1, placeX, placeY, signalTowerType, 0, 0, -1, -1);
                    TileProcessorNetWork.PlaceInWorldNetSend(VaultMod.Instance, signalTowerType, point);
                }
            }

            //播放完成音效
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 1.5f }, PosInWorld);

            //创建完成特效
            for (int i = 0; i < 50; i++) {
                Vector2 dustPos = PosInWorld + new Vector2(Main.rand.Next(-48, 48), Main.rand.Next(-96, 32));
                Dust dust = Dust.NewDustPerfect(dustPos, DustID.Electric, Vector2.Zero, 0, default, 1.5f);
                dust.noGravity = true;
            }

            //同步到其他客户端
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendTileSquare(-1, baseX, baseY, 6, 14);
            }
        }

        private void CreateConstructionDust() {
            if (VaultUtils.isServer) {
                return;
            }

            int baseX = Position.X - 2;
            int baseY = Position.Y - 12;

            //在6×14区域周围创建粒子
            float progress = constructionTime / (float)ConstructionDuration;

            //使用 PRT_TileHightlight 粒子创建更好的构建效果
            for (int i = 0; i < 2; i++) {
                int x = baseX + Main.rand.Next(0, 6);
                int y = baseY + (int)(14 * progress) + Main.rand.Next(-2, 2);

                Vector2 particlePos = new Vector2(x * 16, y * 16) + new Vector2(Main.rand.Next(0, 16), Main.rand.Next(0, 16));

                //生成 TileHightlight 粒子，颜色为青色，表示构建进度
                PRTLoader.NewParticle<PRT_TileHightlight>(particlePos, Vector2.Zero, Color.Gold);
            }
        }

        public override void BackDraw(SpriteBatch spriteBatch) {
            //绘制搭建指示
            if (showGuide && !isConstructing) {
                DrawConstructionGuide(spriteBatch);
            }

            //绘制构建进度指示
            if (isConstructing) {
                DrawConstructionProgress(spriteBatch);
            }
        }

        [VaultLoaden(CWRConstant.Item + "Placeable/")]
        public static Texture2D StarflowPlatedBlockAlt;//发现这个占位符纹理效果意外不错，于是便留着

        private void DrawConstructionGuide(SpriteBatch spriteBatch) {
            int starflowBlockType = ModContent.TileType<StarflowPlatedBlockTile>();
            int baseX = Position.X - 2;
            int baseY = Position.Y - 12;

            //计算透明度（呼吸效果）
            float alphaBase = 0.3f + 0.2f * MathF.Sin(guideAlphaTime * 0.05f);

            Texture2D blockTexture = StarflowPlatedBlockAlt;

            for (int x = 0; x < 6; x++) {
                for (int y = 0; y < 14; y++) {
                    int checkX = baseX + x;
                    int checkY = baseY + y;

                    //跳过构建器自身位置（2×2）
                    if (x >= 2 && x < 4 && y >= 12 && y < 14) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(checkX, checkY);

                    //只为缺失的方块绘制虚影
                    if (!tile.HasTile || tile.TileType != starflowBlockType) {
                        Vector2 drawPos = new Vector2(checkX * 16, checkY * 16) - Main.screenPosition;

                        //计算当前方块的特殊效果（从下到上渐变）
                        float heightFactor = 1f - y / 14f;
                        float alpha = alphaBase * heightFactor;

                        //边框颜色（青色）
                        Color borderColor = new Color(100, 200, 255) * alpha;
                        //填充颜色（更淡的青色）
                        Color fillColor = new Color(150, 180, 220) * (alpha * 0.5f);

                        //绘制填充
                        spriteBatch.Draw(
                            VaultAsset.placeholder2.Value,
                            drawPos,
                            new Rectangle(0, 0, 1, 1),
                            fillColor,
                            0f,
                            Vector2.Zero,
                            new Vector2(16, 16),
                            SpriteEffects.None,
                            0f
                        );

                        //绘制边框（4条线）
                        int borderThickness = 1;
                        //上边框
                        spriteBatch.Draw(
                            VaultAsset.placeholder2.Value,
                            drawPos,
                            new Rectangle(0, 0, 1, 1),
                            borderColor,
                            0f,
                            Vector2.Zero,
                            new Vector2(16, borderThickness),
                            SpriteEffects.None,
                            0f
                        );
                        //下边框
                        spriteBatch.Draw(
                            VaultAsset.placeholder2.Value,
                            drawPos + new Vector2(0, 16 - borderThickness),
                            new Rectangle(0, 0, 1, 1),
                            borderColor,
                            0f,
                            Vector2.Zero,
                            new Vector2(16, borderThickness),
                            SpriteEffects.None,
                            0f
                        );
                        //左边框
                        spriteBatch.Draw(
                            VaultAsset.placeholder2.Value,
                            drawPos,
                            new Rectangle(0, 0, 1, 1),
                            borderColor,
                            0f,
                            Vector2.Zero,
                            new Vector2(borderThickness, 16),
                            SpriteEffects.None,
                            0f
                        );
                        //右边框
                        spriteBatch.Draw(
                            VaultAsset.placeholder2.Value,
                            drawPos + new Vector2(16 - borderThickness, 0),
                            new Rectangle(0, 0, 1, 1),
                            borderColor,
                            0f,
                            Vector2.Zero,
                            new Vector2(borderThickness, 16),
                            SpriteEffects.None,
                            0f
                        );

                        //绘制方块纹理预览（半透明）
                        spriteBatch.Draw(
                            blockTexture,
                            drawPos,
                            new Rectangle(0, 0, 16, 16),
                            Color.White * (alpha * 0.6f),
                            0f,
                            Vector2.Zero,
                            1f,
                            SpriteEffects.None,
                            0f
                        );
                    }
                }
            }

            //绘制构建区域外框和提示文本
            DrawConstructionAreaBorder(spriteBatch, baseX, baseY);
            DrawGuideText(spriteBatch, baseX, baseY);
        }

        private void DrawConstructionAreaBorder(SpriteBatch spriteBatch, int baseX, int baseY) {
            //绘制6×14区域的外边框
            Vector2 topLeft = new Vector2(baseX * 16, baseY * 16) - Main.screenPosition;
            int width = 6 * 16;
            int height = 14 * 16;
            int borderThickness = 2;

            float alpha = 0.6f + 0.4f * MathF.Sin(guideAlphaTime * 0.08f);
            Color borderColor = new Color(255, 200, 100) * alpha; //金色边框

            //上边框
            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(-borderThickness, -borderThickness),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(width + borderThickness * 2, borderThickness), SpriteEffects.None, 0f);
            //下边框
            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(-borderThickness, height),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(width + borderThickness * 2, borderThickness), SpriteEffects.None, 0f);
            //左边框
            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(-borderThickness, 0),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(borderThickness, height), SpriteEffects.None, 0f);
            //右边框
            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(width, 0),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(borderThickness, height), SpriteEffects.None, 0f);
        }

        private void DrawGuideText(SpriteBatch spriteBatch, int baseX, int baseY) {
            //计算缺失的方块数量
            int starflowBlockType = ModContent.TileType<StarflowPlatedBlockTile>();
            int missingBlocks = 0;

            for (int x = 0; x < 6; x++) {
                for (int y = 0; y < 14; y++) {
                    if (x >= 2 && x < 4 && y >= 12 && y < 14) {
                        continue;
                    }

                    int checkX = baseX + x;
                    int checkY = baseY + y;
                    Tile tile = Framing.GetTileSafely(checkX, checkY);

                    if (!tile.HasTile || tile.TileType != starflowBlockType) {
                        missingBlocks++;
                    }
                }
            }

            Vector2 textPos = new Vector2((baseX + 3) * 16, (baseY - 2) * 16) - Main.screenPosition;
            float textAlpha = 0.8f + 0.2f * MathF.Sin(guideAlphaTime * 0.06f);

            if (missingBlocks > 0) {
                string text = string.Format(GuideText_NeedBlocks.Value, missingBlocks);
                Color textColor = Color.Yellow * textAlpha;
                Color shadowColor = Color.Black * textAlpha * 0.5f;

                //绘制阴影
                Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(2, 2), shadowColor, 1f);
                //绘制文本
                Utils.DrawBorderString(spriteBatch, text, textPos, textColor, 1f);
            }
            else {
                string text = GuideText_Ready.Value;
                Color textColor = Color.Lime * textAlpha;
                Color shadowColor = Color.Black * textAlpha * 0.5f;

                //绘制阴影
                Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(2, 2), shadowColor, 1.2f);
                //绘制文本
                Utils.DrawBorderString(spriteBatch, text, textPos, textColor, 1.2f);
            }
        }

        private void DrawConstructionProgress(SpriteBatch spriteBatch) {
            float progress = constructionTime / (float)ConstructionDuration;
            Color glowColor = Color.Cyan * (0.5f + 0.5f * MathF.Sin(constructionTime * 0.1f));

            int baseX = Position.X - 2;
            int baseY = Position.Y - 12;
            Vector2 drawPos = new Vector2((baseX + 3) * 16, (baseY + 7) * 16) - Main.screenPosition;

            //绘制进度条
            Vector2 progressBarPos = new Vector2((baseX + 3) * 16, (baseY - 1) * 16) - Main.screenPosition;
            int barWidth = 80;
            int barHeight = 8;

            //进度条背景
            spriteBatch.Draw(VaultAsset.placeholder2.Value, progressBarPos - new Vector2(barWidth / 2, 0),
                new Rectangle(0, 0, 1, 1), Color.Black * 0.7f, 0f, Vector2.Zero,
                new Vector2(barWidth, barHeight), SpriteEffects.None, 0f);

            //进度条填充
            Color progressColor = Color.Lerp(Color.Yellow, Color.Lime, progress);
            spriteBatch.Draw(VaultAsset.placeholder2.Value, progressBarPos - new Vector2(barWidth / 2, 0) + new Vector2(1, 1),
                new Rectangle(0, 0, 1, 1), progressColor, 0f, Vector2.Zero,
                new Vector2((barWidth - 2) * progress, barHeight - 2), SpriteEffects.None, 0f);

            //进度百分比文字
            string progressText = $"{(int)(progress * 100)}%";
            Vector2 textPos = progressBarPos + new Vector2(0, barHeight + 5);
            Color shadowColor = Color.Black * 0.8f;

            //绘制阴影
            Utils.DrawBorderString(spriteBatch, progressText, textPos + new Vector2(1, 1), shadowColor, 0.8f);
            //绘制文本
            Utils.DrawBorderString(spriteBatch, progressText, textPos, Color.White, 0.8f);
        }
    }
}
