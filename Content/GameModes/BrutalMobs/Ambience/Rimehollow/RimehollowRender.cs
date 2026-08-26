using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rimehollow
{
    /// <summary>
    /// 冰雪洞穴屏幕层绘制：「冽息」的冰晶折射星闪（晶质冰面上的四芒微光，
    /// 有光处更多）与「寒雾洼」的视野白雾边缘。挂 EndEntityDraw，
    /// 星闪走加色批贴世界，白边走屏幕空间 AlphaBlend；自开自收，无 RT 槽
    /// </summary>
    internal sealed class RimehollowRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.75</summary>
        public override float Weight => 1.75f;

        //StarTexture 326² 黑底四芒星（加色批合法）
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> StarTexture = null;
        //Fog 256² 白RGB+真alpha 烟羽，AlphaBlend 直接染色（白雾唯一合法载体）
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Fog = null;

        private const int MaxGlints = 36;
        /// <summary>星闪采光下限：暗处不闪，光源边最活跃</summary>
        private const float GlintMinBrightness = 0.2f;

        private struct Glint
        {
            internal bool Active;
            internal Vector2 Pos;
            internal int Life;
            internal int MaxLife;
            internal float Size;
            internal float Phase;
            internal bool Warm;
        }

        private static readonly Glint[] glints = new Glint[MaxGlints];

        //白雾边缘的锚位（单位屏幕坐标）与尺寸/浓度系数：四角浓、边中稍薄
        private static readonly Vector2[] EdgeAnchors = [
            new(0.02f, 0.04f), new(0.98f, 0.04f), new(0.02f, 0.96f), new(0.98f, 0.96f),
            new(0.5f, -0.02f), new(0.5f, 1.02f), new(-0.03f, 0.5f), new(1.03f, 0.5f),
        ];
        private static readonly float[] EdgeAlphaK = [1f, 1f, 1.15f, 1.15f, 0.8f, 0.95f, 0.85f, 0.85f];

        private static readonly Color GlintCold = new(168, 216, 255);
        private static readonly Color GlintWarmWhite = new(232, 240, 252);
        private static readonly Color MistWhite = new(222, 234, 244);

        //==================== 逻辑更新（星闪池） ====================

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = RimehollowAmbience.Presence;
            if (presence < 0.05f) {
                for (int i = 0; i < glints.Length; i++) {
                    glints[i].Active = false;
                }
                return;
            }

            //推进在场星闪
            for (int i = 0; i < glints.Length; i++) {
                if (!glints[i].Active) {
                    continue;
                }
                if (++glints[i].Life >= glints[i].MaxLife) {
                    glints[i].Active = false;
                }
            }

            //采样补充：随机取屏内瓦片，晶质冰+外露面+亮度权重（有光处更多）
            for (int attempt = 0; attempt < 4; attempt++) {
                int tx = (int)((Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth)) / 16f);
                int ty = (int)((Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight)) / 16f);
                if (!WorldGen.InWorld(tx, ty, 24)) {
                    continue;
                }
                Tile tile = Framing.GetTileSafely(tx, ty);
                if (!tile.HasTile || !RimehollowAmbience.IsCrystalIce(tile.TileType)) {
                    continue;
                }
                //至少一面暴露在空气里才可能反光
                if (WorldGen.SolidTile(tx - 1, ty) && WorldGen.SolidTile(tx + 1, ty)
                    && WorldGen.SolidTile(tx, ty - 1) && WorldGen.SolidTile(tx, ty + 1)) {
                    continue;
                }
                float brightness = Lighting.Brightness(tx, ty);
                if (brightness < GlintMinBrightness) {
                    continue;
                }
                if (Main.rand.NextFloat() > brightness * 0.5f * presence) {
                    continue;
                }
                SpawnGlint(new Vector2(tx * 16f + 8f, ty * 16f + 8f)
                    + Main.rand.NextVector2Circular(6f, 6f));
            }
        }

        private static void SpawnGlint(Vector2 pos) {
            for (int i = 0; i < glints.Length; i++) {
                if (glints[i].Active) {
                    continue;
                }
                glints[i] = new Glint {
                    Active = true,
                    Pos = pos,
                    Life = 0,
                    MaxLife = Main.rand.Next(45, 100),
                    Size = Main.rand.NextFloat(0.026f, 0.05f),
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Warm = Main.rand.NextBool(4),
                };
                return;
            }
        }

        //==================== 绘制 ====================

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float presence = RimehollowAmbience.Presence;
            if (presence < 0.02f) {
                return;
            }

            bool anyGlint = false;
            for (int i = 0; i < glints.Length; i++) {
                if (glints[i].Active) {
                    anyGlint = true;
                    break;
                }
            }
            float whiteEdge = Main.LocalPlayer.GetModPlayer<RimehollowPlayer>().WhiteEdge;

            if (anyGlint) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
                DrawGlints(spriteBatch, presence);
                spriteBatch.End();
            }

            if (whiteEdge > 0.02f) {
                //屏幕空间白边：不带视图矩阵，缩放时不漂
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                    SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                DrawMistVeil(spriteBatch, whiteEdge, graphicsDevice);
                spriteBatch.End();
            }
        }

        //冰晶折射星闪：四芒星微光，短寿命入出包络 + 高频明灭
        private static void DrawGlints(SpriteBatch sb, float presence) {
            Texture2D star = StarTexture?.Value;
            if (star == null || star.IsDisposed) {
                return;
            }
            Vector2 origin = star.Size() / 2f;
            float time = Main.GlobalTimeWrappedHourly;
            for (int i = 0; i < glints.Length; i++) {
                if (!glints[i].Active) {
                    continue;
                }
                float t = glints[i].Life / (float)glints[i].MaxLife;
                float env = MathF.Sin(MathHelper.Pi * t);
                float twinkle = 0.7f + 0.3f * MathF.Sin(time * 9f + glints[i].Phase);
                float alpha = 0.5f * env * twinkle * presence;
                if (alpha < 0.01f) {
                    continue;
                }
                Color tint = (glints[i].Warm ? GlintWarmWhite : GlintCold) * alpha;
                Vector2 pos = glints[i].Pos - Main.screenPosition;
                sb.Draw(star, pos, null, tint, glints[i].Phase,
                    origin, glints[i].Size * (0.8f + 0.2f * twinkle), SpriteEffects.None, 0f);
            }
        }

        //寒雾白边：真 alpha 烟羽沿屏缘呼吸，寒意越高越浓，深处再加一层薄乳白
        private static void DrawMistVeil(SpriteBatch sb, float whiteEdge, GraphicsDevice gd) {
            Texture2D fog = Fog?.Value;
            if (fog == null || fog.IsDisposed) {
                return;
            }
            int vpW = gd.Viewport.Width;
            int vpH = gd.Viewport.Height;
            Vector2 origin = fog.Size() / 2f;
            float time = Main.GlobalTimeWrappedHourly;
            float baseScale = vpW * 0.42f / fog.Width;

            for (int i = 0; i < EdgeAnchors.Length; i++) {
                Vector2 pos = new(EdgeAnchors[i].X * vpW, EdgeAnchors[i].Y * vpH);
                //缓慢的呼吸漂移，雾不是贴纸
                pos.X += MathF.Sin(time * 0.37f + i * 1.7f) * 26f;
                pos.Y += MathF.Cos(time * 0.29f + i * 2.3f) * 18f;
                float alpha = whiteEdge * 0.34f * EdgeAlphaK[i]
                    * (0.85f + 0.15f * MathF.Sin(time * 0.6f + i));
                float scale = baseScale * (0.8f + 0.25f * MathF.Sin(time * 0.23f + i * 0.9f));
                sb.Draw(fog, pos, null, MistWhite * alpha, i * 0.7f, origin, scale, SpriteEffects.None, 0f);
            }

            //寒意过半后加一层轻乳白整屏罩（真 alpha 白像素，浓度刻意压低）
            if (whiteEdge > 0.5f) {
                Texture2D white = VaultAsset.placeholder2?.Value;
                if (white != null) {
                    float haze = (whiteEdge - 0.5f) * 0.22f;
                    sb.Draw(white, new Rectangle(0, 0, vpW, vpH), MistWhite * haze);
                }
            }
        }
    }
}
