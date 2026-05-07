using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.WorldBuilding;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys
{
    /// <summary>
    /// 虚空聚落 - 嘉登虚空实验室集群所在的亚空间维度
    /// 到处是虚空，现实和亚空间的屏障非常薄弱
    /// 地形由漂浮在虚空中的岛屿群组成，核心岛屿上建有最大的实验室
    /// </summary>
    internal class VoidColony : Subworld
    {
        public static VoidColony Instance { get; private set; }

        //加载动画累计时间，每次进入子世界时重置
        private static float _loadTime = 0f;
        //预估加载时长（秒），超出后钉在95%
        private const float _estDuration = 6.5f;

        /// <summary>
        /// 世界宽度 - 使用中等尺寸，足够容纳多个浮岛实验室
        /// </summary>
        public override int Width => 4200;

        /// <summary>
        /// 世界高度 - 较高的世界以容纳上下分布的浮岛
        /// </summary>
        public override int Height => 1800;

        public static bool Active => SubworldSystem.IsActive<VoidColony>();

        public override List<GenPass> Tasks => [new VoidColonyGen()];

        public static void Enter() {
            SubworldSystem.Enter<VoidColony>();
        }

        public static void Exit() {
            SubworldSystem.Exit();
        }

        public override void Load() {
            Instance = this;
        }

        public override void Unload() {
            Instance = null;
        }

        public override void OnEnter() {
            _loadTime = 0f;
        }

        public override void OnExit() {

        }

        public override void OnLoad() {
            //虚空维度永远处于昏暗的"白昼"状态
            Main.dayTime = true;
            Main.time = Main.dayLength / 2;
            //将地表线推到底部，整个世界视为"天空"
            Main.worldSurface = Main.maxTilesY - 2;
            Main.rockLayer = Main.maxTilesY - 1;
        }

        public override void Update() {
            //保持时间静止
            Main.dayTime = true;
            Main.time = Main.dayLength / 2;

            //更新机械和实体
            Wiring.UpdateMech();
            TileEntity.UpdateStart();
            foreach (TileEntity te in TileEntity.ByID.Values) {
                te.Update();
            }
            TileEntity.UpdateEnd();

            for (int i = 0; i < 10; i++) {
                Liquid.UpdateLiquid();
            }
        }

        public override float GetGravity(Entity entity) {
            //亚空间中重力略微降低，营造漂浮感
            return 0.85f;
        }

        //完全接管加载界面绘制逻辑
        public override void DrawSetup(GameTime gameTime) {
            _loadTime += 0.02f;

            PlayerInput.SetZoom_UI();
            Main.instance.GraphicsDevice.Clear(Color.Black);

            DrawLoadingBackground(_loadTime);

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);
            DrawMenu(gameTime);
            Main.DrawCursor(Main.DrawThickCursor());
            Main.spriteBatch.End();
        }

        private static void DrawLoadingBackground(float time) {
            var shader = EffectLoader.VoidColonyLoading?.Value;
            if (shader == null || VaultAsset.placeholder2 == null || VaultAsset.placeholder2.IsDisposed) {
                return;
            }
            int w = Main.screenWidth;
            int h = Main.screenHeight;
            float progress = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(_loadTime / _estDuration, 0f, 0.95f));

            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer,
                null, Main.UIScaleMatrix);

            shader.Parameters["uTime"]?.SetValue(time);
            shader.Parameters["uProgress"]?.SetValue(progress);
            shader.Parameters["uAspectRatio"]?.SetValue((float)w / h);
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(VaultAsset.placeholder2.Value, new Rectangle(0, 0, w, h), Color.White);
            Main.spriteBatch.End();
        }

        public override void DrawMenu(GameTime gameTime) {
            int sw = Main.screenWidth;
            int sh = Main.screenHeight;
            float progress = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(_loadTime / _estDuration, 0f, 0.95f));

            Color crimson = new Color(210, 50, 20);
            Color warm = new Color(255, 160, 80);
            Color dim = new Color(185, 165, 168);

            DynamicSpriteFont titleFont = FontAssets.DeathText.Value;
            DynamicSpriteFont bodyFont = FontAssets.MouseText.Value;
            Texture2D px = VaultAsset.placeholder2.Value;

            DrawVCTopIdentifier(sw, sh, bodyFont, dim);
            DrawVCTitleBlock(sw, sh, titleFont, bodyFont, crimson, dim, px);
            DrawVCDialPercentage(sw, sh, titleFont, bodyFont, crimson, warm, progress);
            DrawVCStatus(sw, sh, bodyFont, dim);
            DrawVCBarLabel(sw, sh, bodyFont, dim);
        }

        private static void DrawVCTopIdentifier(int sw, int sh, DynamicSpriteFont font, Color dim) {
            string tag = "// VOID_GRID  RIFT-\u03a9X14";
            DynamicSpriteFontExtensionMethods.DrawString(Main.spriteBatch, font, tag,
                new Vector2(sw * 0.034f, sh * 0.090f), dim * 0.50f);
        }

        private static void DrawVCTitleBlock(int sw, int sh, DynamicSpriteFont titleFont,
            DynamicSpriteFont bodyFont, Color crimson, Color dim, Texture2D px) {
            string title = "VOID COLONY";
            Vector2 titleSz = titleFont.MeasureString(title);
            Vector2 titlePos = new Vector2(sw * 0.5f - titleSz.X * 0.5f, sh * 0.180f);

            DynamicSpriteFontExtensionMethods.DrawString(Main.spriteBatch, titleFont, title,
                titlePos + new Vector2(2f, 3f), Color.Black * 0.55f);
            DynamicSpriteFontExtensionMethods.DrawString(Main.spriteBatch, titleFont, title,
                titlePos, crimson);

            string sub = "SUBSPACE  RIFT  TRANSIT";
            Vector2 subSz = bodyFont.MeasureString(sub);
            Vector2 subPos = new Vector2(sw * 0.5f - subSz.X * 0.5f,
                                         titlePos.Y + titleSz.Y + 8f);
            DynamicSpriteFontExtensionMethods.DrawString(Main.spriteBatch, bodyFont, sub,
                subPos, dim * 0.65f);

            int ulY = (int)(subPos.Y + subSz.Y + 14f);
            int ulW = (int)(titleSz.X * 0.55f);
            int ulX = (int)(sw * 0.5f - ulW / 2f);
            int gap = 6;
            Main.spriteBatch.Draw(px, new Rectangle(ulX, ulY, ulW / 2 - gap, 1),
                new Rectangle(0, 0, 1, 1), crimson * 0.55f);
            Main.spriteBatch.Draw(px, new Rectangle(ulX + ulW / 2 + gap, ulY, ulW / 2 - gap, 1),
                new Rectangle(0, 0, 1, 1), crimson * 0.55f);

            int cx = ulX + ulW / 2;
            Main.spriteBatch.Draw(px, new Rectangle(cx - 1, ulY - 2, 2, 1),
                new Rectangle(0, 0, 1, 1), crimson * 0.95f);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 2, ulY - 1, 4, 1),
                new Rectangle(0, 0, 1, 1), crimson);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 3, ulY, 6, 1),
                new Rectangle(0, 0, 1, 1), crimson);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 2, ulY + 1, 4, 1),
                new Rectangle(0, 0, 1, 1), crimson);
            Main.spriteBatch.Draw(px, new Rectangle(cx - 1, ulY + 2, 2, 1),
                new Rectangle(0, 0, 1, 1), crimson * 0.95f);
        }

        private static void DrawVCDialPercentage(int sw, int sh, DynamicSpriteFont titleFont,
            DynamicSpriteFont bodyFont, Color crimson, Color warm, float progress) {
            int pct = (int)(progress * 100);
            string num = pct.ToString("D2");
            Vector2 numSz = titleFont.MeasureString(num);
            Vector2 scale = new Vector2(0.92f);
            Vector2 numActSz = new Vector2(numSz.X * scale.X, numSz.Y * scale.Y);

            Vector2 dialCenter = new Vector2(sw * 0.5f, sh * 0.510f);
            float signOffset = 6f;
            Vector2 numPos = new Vector2(
                dialCenter.X - (numActSz.X + signOffset + 14f) * 0.5f,
                dialCenter.Y - numActSz.Y * 0.5f);

            DynamicSpriteFontExtensionMethods.DrawString(Main.spriteBatch, titleFont, num,
                numPos + new Vector2(2f, 3f), Color.Black * 0.55f,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            DynamicSpriteFontExtensionMethods.DrawString(Main.spriteBatch, titleFont, num,
                numPos, crimson,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            string sign = "%";
            Vector2 signSz = bodyFont.MeasureString(sign);
            Vector2 signPos = new Vector2(
                numPos.X + numActSz.X + signOffset,
                numPos.Y + numActSz.Y - signSz.Y - 4f);
            DynamicSpriteFontExtensionMethods.DrawString(Main.spriteBatch, bodyFont, sign,
                signPos, warm * 0.92f);
        }

        private static void DrawVCStatus(int sw, int sh, DynamicSpriteFont font, Color dim) {
            string status = (Main.statusText ?? string.Empty).ToUpperInvariant();
            if (string.IsNullOrEmpty(status)) {
                status = "CALIBRATING DIMENSIONAL ANCHOR";
            }
            int dotN = (int)(_loadTime * 1.7f) % 4;
            string full = status + new string('.', dotN);

            Vector2 sz = font.MeasureString(full);
            Vector2 pos = new Vector2(sw * 0.5f - sz.X * 0.5f, sh * 0.785f);
            DynamicSpriteFontExtensionMethods.DrawString(Main.spriteBatch, font, full,
                pos + new Vector2(1f, 1f), Color.Black * 0.55f);
            DynamicSpriteFontExtensionMethods.DrawString(Main.spriteBatch, font, full,
                pos, dim * 0.85f);
        }

        private static void DrawVCBarLabel(int sw, int sh, DynamicSpriteFont font, Color dim) {
            string label = "RIFT  BRIDGE";
            DynamicSpriteFontExtensionMethods.DrawString(Main.spriteBatch, font, label,
                new Vector2(sw * 0.034f, sh * 0.892f), dim * 0.55f);
        }
    }
}
