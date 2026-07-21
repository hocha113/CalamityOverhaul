using CalamityOverhaul.Content.Items.Placeable;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Draedon.ExoMechdusaSums;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers
{
    /// <summary>量子塔自我构建器</summary>
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
            condition = () => DraedonStorySync.ReadDraedon(
                d => d.UseConstructionBlueprint,
                d => d.UseConstructionBlueprint);
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

    //6×14=80块星流镀板，满足后换DeploySignaltowerTile(同6×14)
    //构建器2×2底中 X:2-3 Y:12-13，底下一格起检地面5格，放置原点2,13
    internal class CQETConstructorTP : TileProcessor, ILocalizedModType
    {
        public override int TargetTileID => ModContent.TileType<CQETConstructorTile>();

        public string LocalizationCategory => "UI";

        private int constructionTime;
        private int checkDelay;
        private bool isConstructing;
        private const int ConstructionDuration = 180; //3秒 60帧
        private const int CheckInterval = 30; //0.5秒

        public int frame;
        private int frameCounter;

        private bool showGuide;
        private int guideAlphaTime;
        private const float GuideMaxDistance = 300f; //近距显示指示

        private bool isGroundIncomplete = false;
        private readonly List<Point> incompleteGroundPositions = new();

        public static LocalizedText GuideText_NeedBlocks { get; private set; }
        public static LocalizedText GuideText_Ready { get; private set; }
        public static LocalizedText GuideText_GroundIncomplete { get; private set; }

        public override void SetStaticDefaults() {
            GuideText_NeedBlocks = this.GetLocalization(nameof(GuideText_NeedBlocks), () => "需要 {0} 个星流镀板");
            GuideText_Ready = this.GetLocalization(nameof(GuideText_Ready), () => "准备就绪！");
            GuideText_GroundIncomplete = this.GetLocalization(nameof(GuideText_GroundIncomplete), () => "地面不完整！");
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

                if (!CheckGroundIntegrity()) {
                    isGroundIncomplete = true;
                    isConstructing = false;
                }
                else {
                    isGroundIncomplete = false;

                    if (!isConstructing && CheckConstructionConditions()) {
                        isConstructing = true;
                        constructionTime = 0;
                        SoundEngine.PlaySound(SoundID.Item4, PosInWorld);
                    }
                }
            }

            if (isConstructing) {
                constructionTime++;

                if (constructionTime % 5 == 0) {
                    CreateConstructionDust();
                }

                if (constructionTime >= ConstructionDuration) {
                    PerformConstruction();
                    isConstructing = false;
                }
            }

            UpdateGuideDisplay();
        }

        private void UpdateGuideDisplay() {
            if (VaultUtils.isServer) {
                return;
            }

            Player closestPlayer = CenterInWorld.FindClosestPlayer(GuideMaxDistance);
            showGuide = closestPlayer != null && !isConstructing;

            if (showGuide) {
                guideAlphaTime++;
            }
            else {
                guideAlphaTime = 0;
            }
        }

        /// <summary>地面5格须实心</summary>
        private bool CheckGroundIntegrity() {
            incompleteGroundPositions.Clear();

            int groundY = Position.Y + 2;//构建器下一格

            for (int offsetX = -2; offsetX <= 3; offsetX++) {
                int checkX = Position.X + offsetX;
                int checkY = groundY;

                Tile tile = Framing.GetTileSafely(checkX, checkY);

                if (!tile.HasTile || tile.IsHalfBlock || tile.Slope != SlopeType.Solid) {
                    incompleteGroundPositions.Add(new Point(checkX, checkY));
                }
            }

            return incompleteGroundPositions.Count == 0;
        }

        private bool CheckConstructionConditions() {
            if (VaultUtils.isClient) {
                return false;
            }

            int starflowBlockType = ModContent.TileType<StarflowPlatedBlockTile>();

            //6×14，构建器2×2占 X:2-3 Y:12-13
            int baseX = Position.X - 2;//左2格
            int baseY = Position.Y - 12;//上12格

            for (int x = 0; x < 6; x++) {
                for (int y = 0; y < 14; y++) {
                    int checkX = baseX + x;
                    int checkY = baseY + y;

                    //跳过构建器2×2
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

            //清除6×14区
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
            //DeploySignaltowerTile 原点2,13
            WorldGen.PlaceTile(placeX, placeY, signalTowerType, true, true);
            if (TPUtils.TryGetTopLeft(placeX, placeY, out var point)) {
                TileProcessorLoader.AddInWorld(signalTowerType, point, null);
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendObjectPlacement(-1, placeX, placeY, signalTowerType, 0, 0, -1, -1);
                    TileProcessorNetWork.PlaceInWorldNetSend(VaultMod.Instance, signalTowerType, point);
                }
            }

            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 1.5f }, PosInWorld);

            for (int i = 0; i < 50; i++) {
                Vector2 dustPos = PosInWorld + new Vector2(Main.rand.Next(-48, 48), Main.rand.Next(-96, 32));
                Dust dust = Dust.NewDustPerfect(dustPos, DustID.Electric, Vector2.Zero, 0, default, 1.5f);
                dust.noGravity = true;
            }

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

            float progress = constructionTime / (float)ConstructionDuration;

            for (int i = 0; i < 2; i++) {
                int x = baseX + Main.rand.Next(0, 6);
                int y = baseY + (int)(14 * progress) + Main.rand.Next(-2, 2);

                Vector2 particlePos = new Vector2(x * 16, y * 16) + new Vector2(Main.rand.Next(0, 16), Main.rand.Next(0, 16));

                PRTLoader.NewParticle<PRT_TileHightlight>(particlePos, Vector2.Zero, Color.Gold);
            }
        }

        public override void BackDraw(SpriteBatch spriteBatch) {
            if (isGroundIncomplete && showGuide) {
                DrawGroundIncompleteWarning(spriteBatch);
            }
            else if (showGuide && !isConstructing) {
                DrawConstructionGuide(spriteBatch);
            }

            if (isConstructing) {
                DrawConstructionProgress(spriteBatch);
            }
        }

        [VaultLoaden(CWRConstant.Item + "Placeable/")]
        public static Texture2D StarflowPlatedBlockAlt = null!;//占位纹理意外不错

        private void DrawGroundIncompleteWarning(SpriteBatch spriteBatch) {
            float alphaBase = 0.5f + 0.3f * MathF.Sin(guideAlphaTime * 0.08f);

            foreach (Point pos in incompleteGroundPositions) {
                Vector2 drawPos = new Vector2(pos.X * 16, pos.Y * 16) - Main.screenPosition;

                Color warningColor = Color.Red * alphaBase;
                Color fillColor = new Color(255, 100, 100) * (alphaBase * 0.3f);

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

                int borderThickness = 2;
                spriteBatch.Draw(
                    VaultAsset.placeholder2.Value,
                    drawPos,
                    new Rectangle(0, 0, 1, 1),
                    warningColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(16, borderThickness),
                    SpriteEffects.None,
                    0f
                );
                spriteBatch.Draw(
                    VaultAsset.placeholder2.Value,
                    drawPos + new Vector2(0, 16 - borderThickness),
                    new Rectangle(0, 0, 1, 1),
                    warningColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(16, borderThickness),
                    SpriteEffects.None,
                    0f
                );
                spriteBatch.Draw(
                    VaultAsset.placeholder2.Value,
                    drawPos,
                    new Rectangle(0, 0, 1, 1),
                    warningColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(borderThickness, 16),
                    SpriteEffects.None,
                    0f
                );
                spriteBatch.Draw(
                    VaultAsset.placeholder2.Value,
                    drawPos + new Vector2(16 - borderThickness, 0),
                    new Rectangle(0, 0, 1, 1),
                    warningColor,
                    0f,
                    Vector2.Zero,
                    new Vector2(borderThickness, 16),
                    SpriteEffects.None,
                    0f
                );

                float crossSize = 12f;
                Vector2 crossCenter = drawPos + new Vector2(8, 8);

                for (int i = 0; i < (int)crossSize; i++) {
                    Vector2 pixelPos = crossCenter + new Vector2(-crossSize / 2 + i, -crossSize / 2 + i);
                    spriteBatch.Draw(
                        VaultAsset.placeholder2.Value,
                        pixelPos,
                        new Rectangle(0, 0, 1, 1),
                        warningColor,
                        0f,
                        Vector2.Zero,
                        2f,
                        SpriteEffects.None,
                        0f
                    );
                }

                for (int i = 0; i < (int)crossSize; i++) {
                    Vector2 pixelPos = crossCenter + new Vector2(-crossSize / 2 + i, crossSize / 2 - i);
                    spriteBatch.Draw(
                        VaultAsset.placeholder2.Value,
                        pixelPos,
                        new Rectangle(0, 0, 1, 1),
                        warningColor,
                        0f,
                        Vector2.Zero,
                        2f,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            Vector2 textPos = new Vector2(Position.X * 16, (Position.Y - 2) * 16) - Main.screenPosition;
            float textAlpha = 0.9f + 0.1f * MathF.Sin(guideAlphaTime * 0.1f);

            string warningText = GuideText_GroundIncomplete.Value;
            Color textColor = Color.Red * textAlpha;
            Color shadowColor = Color.Black * textAlpha * 0.7f;

            Utils.DrawBorderString(spriteBatch, warningText, textPos + new Vector2(2, 2), shadowColor, 1.2f);
            float flashEffect = 0.8f + 0.2f * MathF.Sin(guideAlphaTime * 0.15f);
            Utils.DrawBorderString(spriteBatch, warningText, textPos, textColor * flashEffect, 1.2f);

            DrawGroundCheckArea(spriteBatch);
        }

        private void DrawGroundCheckArea(SpriteBatch spriteBatch) {
            int groundY = Position.Y + 2;
            Vector2 topLeft = new Vector2((Position.X - 2) * 16, groundY * 16) - Main.screenPosition;
            int width = 6 * 16;
            int height = 16;
            int borderThickness = 2;

            float alpha = 0.7f + 0.3f * MathF.Sin(guideAlphaTime * 0.1f);
            Color borderColor = Color.Red * alpha;

            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(-borderThickness, -borderThickness),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(width + borderThickness * 2, borderThickness), SpriteEffects.None, 0f);
            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(-borderThickness, height),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(width + borderThickness * 2, borderThickness), SpriteEffects.None, 0f);
            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(-borderThickness, 0),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(borderThickness, height), SpriteEffects.None, 0f);
            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(width, 0),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(borderThickness, height), SpriteEffects.None, 0f);
        }

        private void DrawConstructionGuide(SpriteBatch spriteBatch) {
            int starflowBlockType = ModContent.TileType<StarflowPlatedBlockTile>();
            int baseX = Position.X - 2;
            int baseY = Position.Y - 12;

            float alphaBase = 0.3f + 0.2f * MathF.Sin(guideAlphaTime * 0.05f);

            Texture2D blockTexture = StarflowPlatedBlockAlt;

            for (int x = 0; x < 6; x++) {
                for (int y = 0; y < 14; y++) {
                    int checkX = baseX + x;
                    int checkY = baseY + y;

                    //跳过构建器2×2
                    if (x >= 2 && x < 4 && y >= 12 && y < 14) {
                        continue;
                    }

                    Tile tile = Framing.GetTileSafely(checkX, checkY);

                    if (!tile.HasTile || tile.TileType != starflowBlockType) {
                        Vector2 drawPos = new Vector2(checkX * 16, checkY * 16) - Main.screenPosition;

                        float heightFactor = 1f - y / 14f;//自下而上渐变
                        float alpha = alphaBase * heightFactor;

                        Color borderColor = new Color(100, 200, 255) * alpha;
                        Color fillColor = new Color(150, 180, 220) * (alpha * 0.5f);

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

                        int borderThickness = 1;
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

            DrawConstructionAreaBorder(spriteBatch, baseX, baseY);
            DrawGuideText(spriteBatch, baseX, baseY);
        }

        private void DrawConstructionAreaBorder(SpriteBatch spriteBatch, int baseX, int baseY) {
            Vector2 topLeft = new Vector2(baseX * 16, baseY * 16) - Main.screenPosition;
            int width = 6 * 16;
            int height = 14 * 16;
            int borderThickness = 2;

            float alpha = 0.6f + 0.4f * MathF.Sin(guideAlphaTime * 0.08f);
            Color borderColor = new Color(255, 200, 100) * alpha;

            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(-borderThickness, -borderThickness),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(width + borderThickness * 2, borderThickness), SpriteEffects.None, 0f);
            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(-borderThickness, height),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(width + borderThickness * 2, borderThickness), SpriteEffects.None, 0f);
            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(-borderThickness, 0),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(borderThickness, height), SpriteEffects.None, 0f);
            spriteBatch.Draw(VaultAsset.placeholder2.Value, topLeft + new Vector2(width, 0),
                new Rectangle(0, 0, 1, 1), borderColor, 0f, Vector2.Zero,
                new Vector2(borderThickness, height), SpriteEffects.None, 0f);
        }

        private void DrawGuideText(SpriteBatch spriteBatch, int baseX, int baseY) {
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

                Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(2, 2), shadowColor, 1f);
                Utils.DrawBorderString(spriteBatch, text, textPos, textColor, 1f);
            }
            else {
                string text = GuideText_Ready.Value;
                Color textColor = Color.Lime * textAlpha;
                Color shadowColor = Color.Black * textAlpha * 0.5f;

                Utils.DrawBorderString(spriteBatch, text, textPos + new Vector2(2, 2), shadowColor, 1.2f);
                Utils.DrawBorderString(spriteBatch, text, textPos, textColor, 1.2f);
            }
        }

        private void DrawConstructionProgress(SpriteBatch spriteBatch) {
            float progress = constructionTime / (float)ConstructionDuration;
            Color glowColor = Color.Cyan * (0.5f + 0.5f * MathF.Sin(constructionTime * 0.1f));

            int baseX = Position.X - 2;
            int baseY = Position.Y - 12;
            Vector2 drawPos = new Vector2((baseX + 3) * 16, (baseY + 7) * 16) - Main.screenPosition;

            Vector2 progressBarPos = new Vector2((baseX + 3) * 16, (baseY - 1) * 16) - Main.screenPosition;
            int barWidth = 80;
            int barHeight = 8;

            spriteBatch.Draw(VaultAsset.placeholder2.Value, progressBarPos - new Vector2(barWidth / 2, 0),
                new Rectangle(0, 0, 1, 1), Color.Black * 0.7f, 0f, Vector2.Zero,
                new Vector2(barWidth, barHeight), SpriteEffects.None, 0f);

            Color progressColor = Color.Lerp(Color.Yellow, Color.Lime, progress);
            spriteBatch.Draw(VaultAsset.placeholder2.Value, progressBarPos - new Vector2(barWidth / 2, 0) + new Vector2(1, 1),
                new Rectangle(0, 0, 1, 1), progressColor, 0f, Vector2.Zero,
                new Vector2((barWidth - 2) * progress, barHeight - 2), SpriteEffects.None, 0f);

            string progressText = $"{(int)(progress * 100)}%";
            Vector2 textPos = progressBarPos + new Vector2(0, barHeight + 5);
            Color shadowColor = Color.Black * 0.8f;

            Utils.DrawBorderString(spriteBatch, progressText, textPos + new Vector2(1, 1), shadowColor, 0.8f);
            Utils.DrawBorderString(spriteBatch, progressText, textPos, Color.White, 0.8f);
        }
    }
}
