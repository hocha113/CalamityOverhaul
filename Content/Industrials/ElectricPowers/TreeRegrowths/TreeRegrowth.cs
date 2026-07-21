using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.TreeRegrowths
{
    /// <summary>
    /// 树木快速重生的演出Actor：按 <see cref="TreeBlueprint"/> 用原版树纹理逐节长出，
    /// 结束时把同一份蓝图写入世界，动画所见即最终所得
    /// </summary>
    internal class TreeRegrowth : Actor
    {
        //目标与蓝图种子，权威端Setup后经SyncVar同步到各客户端
        [SyncVar]
        public int targetTileX;
        [SyncVar]
        public int targetTileY;
        [SyncVar]
        public int treeTileType;
        [SyncVar]
        public int growSeed;

        private TreeBlueprint blueprint;
        //0准备 1生长 2收尾(写块+滞留遮罩)
        private int phase;
        private int timer;
        private int growDuration;
        private float growProgress;
        private float lastProgress;
        //tile5=群系树皮变体 palm=沙种(0-3)+绿洲(+4)
        private int paletteVariant;

        private const int PrepDuration = 24;
        private const int LingerDuration = 30;
        //单个树块从萌出到定型占总进度的窗口
        private const float PopWindow = 0.14f;
        //树冠萌出时机与窗口(0.70+0.24，在0.94前定型，容忍客户端起步延迟)
        private const float TopFoliageAt = 0.70f;
        private const float TopPopWindow = 0.24f;

        /// <summary>
        /// 权威端生成后立即调用，落点/树种/种子经SyncVar广播
        /// </summary>
        public void Setup(int x, int y, int treeType, int seed) {
            targetTileX = x;
            targetTileY = y;
            treeTileType = treeType;
            growSeed = seed;
            NetUpdate = true;
        }

        public override void OnSpawn(params object[] args) {
            Width = 32;
            Height = 32;
            DrawExtendMode = 700;
            DrawLayer = ActorDrawLayer.BeforeTiles;
        }

        public override void AI() {
            if (blueprint == null) {
                TryBuildBlueprint();
                return;
            }

            timer++;
            switch (phase) {
                case 0:
                    UpdatePreparation();
                    break;
                case 1:
                    UpdateGrowing();
                    break;
                case 2:
                    UpdateFinish();
                    break;
            }
        }

        /// <summary>
        /// 客户端SyncVar可能晚于生成包到达，蓝图延迟构建；数据迟迟不来或地形不符则权威端销毁
        /// </summary>
        private void TryBuildBlueprint() {
            if (treeTileType == 0) {
                if (!VaultUtils.isClient && ++timer > 240) {
                    RequestKill();
                }
                return;
            }

            if (!TreeBlueprint.TryGenerate(targetTileX, targetTileY, treeTileType, growSeed, out blueprint)) {
                if (!VaultUtils.isClient) {
                    RequestKill();
                }
                return;
            }

            Position = new Vector2(targetTileX * 16, targetTileY * 16 - 16);
            growDuration = 66 + blueprint.Height * 6;
            paletteVariant = blueprint.IsPalm
                ? GetPalmVariant(targetTileX, targetTileY)
                : TileDrawing.GetTreeVariant(targetTileX, targetTileY);
            timer = 0;
        }

        private void UpdatePreparation() {
            if (timer == 1 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = 0.2f }, Center);
            }

            //根部酝酿：地表冒出草屑
            if (timer % 3 == 0 && !Main.dedServ) {
                for (int i = 0; i < 2; i++) {
                    Vector2 dustPos = new Vector2(targetTileX * 16 + Main.rand.NextFloat(-8f, 24f), targetTileY * 16);
                    Dust dust = Dust.NewDustDirect(dustPos, 4, 4, DustID.Grass, Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-3f, -1.5f), 100, default, 1.3f);
                    dust.noGravity = true;
                    dust.fadeIn = 1.1f;
                }
            }

            if (timer >= PrepDuration) {
                phase = 1;
                timer = 0;
                lastProgress = 0f;
            }
        }

        private void UpdateGrowing() {
            growProgress = VaultUtils.EaseOutQuad(Math.Clamp(timer / (float)growDuration, 0f, 1f));

            if (!Main.dedServ) {
                EmitGrowthDust();

                //生长沙沙声随进度升调
                if (timer % 24 == 0) {
                    SoundEngine.PlaySound(SoundID.Grass with {
                        Volume = 0.35f,
                        Pitch = 0.1f + growProgress * 0.35f
                    }, Center);
                }
            }
            lastProgress = growProgress;

            if (timer >= growDuration) {
                phase = 2;
                timer = 0;
                growProgress = 1f;
            }
        }

        private void UpdateFinish() {
            //第一帧写块；动画滞留数帧盖住多人下tile同步延迟(真树与假树逐像素一致)
            if (timer == 1 && !VaultUtils.isClient) {
                if (blueprint.CanPlace()) {
                    blueprint.Place();
                }
            }

            if (timer >= LingerDuration && !VaultUtils.isClient) {
                RequestKill();
            }
        }

        #region 生长节奏
        /// <summary>
        /// 单块的萌出进度阈值：主干自底向上0.02-0.62，侧枝叶随所在行错后，顶帽0.66
        /// </summary>
        private float PieceRevealAt(in TreeBlueprint.Piece piece) {
            if (IsTopPiece(piece)) {
                return 0.66f;
            }
            int h = blueprint.GroundY - 1 - piece.TileY;
            float ratio = blueprint.Height <= 1 ? 0f : h / (float)(blueprint.Height - 1);
            float at = 0.02f + ratio * 0.60f;
            if (piece.IsLeafyBranch) {
                at += 0.04f;
            }
            return at;
        }

        //顶帽块：普通树 frameY>=198，棕榈 frameX>=88
        private bool IsTopPiece(in TreeBlueprint.Piece piece) {
            return blueprint.IsPalm ? piece.FrameX >= 88 : piece.IsTopStub;
        }

        //easeOutBack，带约10%回弹的定型
        private static float PopScale(float t) {
            if (t >= 1f) {
                return 1f;
            }
            const float c1 = 1.70158f;
            float u = t - 1f;
            return 1f + (c1 + 1f) * u * u * u + c1 * u * u;
        }

        /// <summary>
        /// 越过萌出阈值的树块冒木屑，树冠萌出时爆发叶屑
        /// </summary>
        private void EmitGrowthDust() {
            foreach (TreeBlueprint.Piece piece in blueprint.Pieces) {
                float at = PieceRevealAt(piece);
                if (at <= lastProgress || at > growProgress) {
                    continue;
                }

                Vector2 tilePos = new Vector2((blueprint.TrunkX + piece.OffsetX) * 16 + 8, piece.TileY * 16 + 10);
                int count = piece.IsLeafyBranch ? 5 : 3;
                for (int i = 0; i < count; i++) {
                    Dust dust = Dust.NewDustDirect(tilePos - new Vector2(6, 6), 12, 12, DustID.WoodFurniture,
                        Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-1.5f, 0.2f), 80, default, 0.9f);
                    dust.noGravity = Main.rand.NextBool();
                }
                if (piece.IsLeafyBranch) {
                    for (int i = 0; i < 4; i++) {
                        Dust leaf = Dust.NewDustDirect(tilePos - new Vector2(8, 8), 16, 16, DustID.GrassBlades,
                            Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 0.5f), 100, default, 1.1f);
                        leaf.noGravity = true;
                        leaf.fadeIn = 0.7f;
                    }
                }
            }

            //树冠爆发
            if (TopFoliageAt > lastProgress && TopFoliageAt <= growProgress) {
                Vector2 topPos = new Vector2(blueprint.TrunkX * 16 + 8, (blueprint.GroundY - blueprint.Height) * 16);
                for (int i = 0; i < 16; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(2.5f, 2f) - new Vector2(0, 1f);
                    Dust leaf = Dust.NewDustDirect(topPos + Main.rand.NextVector2Circular(24f, 16f), 4, 4, DustID.GrassBlades,
                        vel.X, vel.Y, 100, default, 1.3f);
                    leaf.noGravity = true;
                    leaf.fadeIn = 1.2f;
                }
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, Pitch = 0.45f }, topPos);
            }
        }
        #endregion

        #region 原版轮子绘制
        //镜像 TileDrawing.GetPalmTreeVariant：沙种0-3，绿洲+4
        private static int GetPalmVariant(int x, int groundY) {
            int variant = Main.tile[x, groundY].TileType switch {
                TileID.Crimsand => 1,
                TileID.Pearlsand => 2,
                TileID.Ebonsand => 3,
                _ => 0
            };
            if (WorldGen.IsPalmOasisTree(x)) {
                variant += 4;
            }
            return variant;
        }

        /// <summary>
        /// 树冠/树枝风格与树冠帧尺寸；普通树走原版 GetCommonTreeFoliageData(只依赖地面，可在树存在前调用)，
        /// 樱花/柳树/灰烬树为固定常量(原版函数需树块本体存在)
        /// </summary>
        private bool GetFoliageData(int pieceX, int xoffToTrunk, int baseFrame, out int style, out int frame, out int topW, out int topH) {
            switch (treeTileType) {
                case TileID.VanityTreeSakura:
                    style = 29;
                    frame = baseFrame;
                    topW = 118;
                    topH = 96;
                    return true;
                case TileID.VanityTreeYellowWillow:
                    style = 30;
                    frame = baseFrame;
                    topW = 118;
                    topH = 96;
                    return true;
                case TileID.TreeAsh:
                    style = 31;
                    frame = baseFrame;
                    topW = 116;
                    topH = 96;
                    return true;
                default:
                    style = 0;
                    frame = baseFrame;
                    return WorldGen.GetCommonTreeFoliageData(pieceX, blueprint.GroundY, xoffToTrunk, ref frame, ref style, out _, out topW, out topH);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (blueprint == null || phase == 0) {
                return false;
            }

            float progress = phase >= 2 ? 1f : growProgress;
            TileDrawing renderer = Main.instance.TilesRenderer;
            double windCounter = renderer._treeWindCounter;

            Main.instance.LoadTiles(treeTileType);
            Texture2D trunkTex = TextureAssets.Tile[treeTileType].Value;
            //tile5大图集内按群系变体偏移(变体-1=森林,偏移0)；棕榈按沙种选行
            int trunkAtlasX = treeTileType == TileID.Trees ? 176 * (paletteVariant + 1) : 0;
            int palmRowY = blueprint.IsPalm ? 22 * paletteVariant : 0;

            //第一遍画枝干树块，第二遍叶簇/树冠盖在其上(原版特殊绘制层同样后于物块层)
            foreach (TreeBlueprint.Piece piece in blueprint.Pieces) {
                float pop = Math.Clamp((progress - PieceRevealAt(piece)) / PopWindow, 0f, 1f);
                if (pop <= 0f) {
                    continue;
                }
                //棕榈顶帽不画块只画树冠
                if (blueprint.IsPalm && IsTopPiece(piece)) {
                    continue;
                }

                int tx = blueprint.TrunkX + piece.OffsetX;
                int ty = piece.TileY;
                Color light = Lighting.GetColor(tx, ty);
                Rectangle src = blueprint.IsPalm
                    ? new Rectangle(piece.FrameX, palmRowY, 20, 20)
                    : new Rectangle(piece.FrameX + trunkAtlasX, piece.FrameY, 20, 20);
                //原版树块绘制在 (x*16-2, y*16) 起的20×20；缩放锚定块底中心
                float palmLean = blueprint.IsPalm ? piece.FrameY : 0;
                Vector2 anchor = new Vector2(tx * 16 + 8 + palmLean, ty * 16 + 20) - Main.screenPosition;
                spriteBatch.Draw(trunkTex, anchor, src, light * pop, 0f, new Vector2(10f, 20f), PopScale(pop), SpriteEffects.None, 0f);
            }

            foreach (TreeBlueprint.Piece piece in blueprint.Pieces) {
                int tx = blueprint.TrunkX + piece.OffsetX;
                int ty = piece.TileY;
                Color light = Lighting.GetColor(tx, ty);

                if (piece.IsLeafyBranch) {
                    DrawBranchFoliage(spriteBatch, piece, tx, ty, light, progress, renderer, windCounter);
                }
                else if (IsTopPiece(piece)) {
                    DrawTopFoliage(spriteBatch, piece, tx, ty, light, progress, renderer, windCounter);
                }
            }

            return false;
        }

        /// <summary>
        /// 侧枝树冠，位置/风摆/锚点公式照抄 TileDrawing.DrawTrees 的 case 44/66
        /// </summary>
        private void DrawBranchFoliage(SpriteBatch spriteBatch, in TreeBlueprint.Piece piece, int tx, int ty,
            Color light, float progress, TileDrawing renderer, double windCounter) {
            //叶簇比枝块本体稍晚萌出
            float pop = Math.Clamp((progress - (PieceRevealAt(piece) + 0.04f)) / PopWindow, 0f, 1f);
            if (pop <= 0f) {
                return;
            }

            int baseFrame = (piece.FrameY - 198) / 22;
            //frameX 44=向左伸(主干在右,xoff+1) 66=向右伸(主干在左,xoff-1)
            bool left = piece.FrameX == 44;
            if (!GetFoliageData(tx, left ? 1 : -1, baseFrame, out int style, out int frame, out _, out _)) {
                return;
            }

            Texture2D branchTex = TextureAssets.TreeBranch[style].Value;
            bool hasWall = Main.tile[tx, ty].WallType > WallID.None;
            float wind = hasWall ? 0f : renderer.GetWindCycle(tx, ty, windCounter);

            Vector2 pos;
            Rectangle src;
            Vector2 origin;
            if (left) {
                pos = new Vector2(tx * 16, ty * 16) - Main.screenPosition.Floor() + new Vector2(16f, 12f);
                if (wind > 0f) {
                    pos.X += wind;
                }
                pos.X += Math.Abs(wind) * 2f;
                src = new Rectangle(0, frame * 42, 40, 40);
                origin = new Vector2(40f, 24f);
            }
            else {
                pos = new Vector2(tx * 16, ty * 16) - Main.screenPosition.Floor() + new Vector2(0f, 18f);
                if (wind < 0f) {
                    pos.X += wind;
                }
                pos.X -= Math.Abs(wind) * 2f;
                src = new Rectangle(42, frame * 42, 40, 40);
                origin = new Vector2(0f, 30f);
            }

            float scale = PopScale(pop);
            spriteBatch.Draw(branchTex, pos, src, light * pop, wind * 0.06f, origin, scale, SpriteEffects.None, 0f);
            if (treeTileType == TileID.TreeAsh) {
                spriteBatch.Draw(TextureAssets.GlowMask[317].Value, pos, src, Color.White * pop, wind * 0.06f, origin, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 主干树冠，位置/风摆/锚点公式照抄 TileDrawing.DrawTrees 的 case 22 与棕榈分支
        /// </summary>
        private void DrawTopFoliage(SpriteBatch spriteBatch, in TreeBlueprint.Piece piece, int tx, int ty,
            Color light, float progress, TileDrawing renderer, double windCounter) {
            float pop = Math.Clamp((progress - TopFoliageAt) / TopPopWindow, 0f, 1f);
            if (pop <= 0f) {
                return;
            }
            float scale = PopScale(pop);
            bool hasWall = Main.tile[tx, ty].WallType > WallID.None;
            float wind = hasWall ? 0f : renderer.GetWindCycle(tx, ty, windCounter);

            if (blueprint.IsPalm) {
                int frameIdx = piece.FrameX switch { 110 => 1, 132 => 2, _ => 0 };
                bool oasis = paletteVariant >= 4;
                Texture2D topTex = TextureAssets.TreeTop[oasis ? 21 : 15].Value;
                int w = oasis ? 114 : 80;
                int h = oasis ? 98 : 80;
                int srcY = oasis ? (paletteVariant - 4) * 98 : paletteVariant * 82;
                int backOff = oasis ? 48 : 32;
                int yOff = oasis ? 2 : 0;

                Vector2 pos = new Vector2(tx * 16 - backOff + piece.FrameY + w / 2, ty * 16 + 16 + yOff) - Main.screenPosition;
                pos.X += wind * 2f;
                pos.Y += Math.Abs(wind) * 2f;
                spriteBatch.Draw(topTex, pos, new Rectangle(frameIdx * (w + 2), srcY, w, h), light * pop,
                    wind * 0.08f, new Vector2(w / 2f, h), scale, SpriteEffects.None, 0f);
                return;
            }

            //秃顶(frameX 0)无树冠
            if (piece.FrameX != 22) {
                return;
            }
            int baseFrame = (piece.FrameY - 198) / 22;
            if (!GetFoliageData(tx, 0, baseFrame, out int style, out int frame, out int topW, out int topH)) {
                return;
            }

            Texture2D treeTopTex = TextureAssets.TreeTop[style].Value;
            Vector2 topPos = new Vector2(tx * 16 + 8, ty * 16 + 16) - Main.screenPosition;
            topPos.X += wind * 2f;
            topPos.Y += Math.Abs(wind) * 2f;
            Rectangle src = new Rectangle(frame * (topW + 2), 0, topW, topH);
            Vector2 origin = new Vector2(topW / 2f, topH);

            spriteBatch.Draw(treeTopTex, topPos, src, light * pop, wind * 0.08f, origin, scale, SpriteEffects.None, 0f);
            if (treeTileType == TileID.TreeAsh) {
                spriteBatch.Draw(TextureAssets.GlowMask[316].Value, topPos, src, Color.White * pop, wind * 0.08f, origin, scale, SpriteEffects.None, 0f);
            }
            //蘑菇树自发光(style 14)
            if (style == 14) {
                Lighting.AddLight(tx, ty, 0.1f * pop, 0.3f * pop, 0.8f * pop);
            }
        }
        #endregion
    }
}
