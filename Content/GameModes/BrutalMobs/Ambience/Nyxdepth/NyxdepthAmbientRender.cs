using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Nyxdepth
{
    /// <summary>
    /// 「渊压」屏幕层绘制：屏边黑雾收束（自屏缘生出、向视野中心缓慢渗入的暗雾团，
    /// Fog 真 alpha 暗层，粒子层实现，不改光照、不铺全屏遮挡）+「深渊凝视」的眼睛绘制。<br/>
    /// 挂 EndEntityDraw 盖在实体之上，读作压在视野上的水中暗翳；自开自收 AlphaBlend 批。<br/>
    /// 密度预算：池上限 18 团即硬预算，均寿约 2 秒，稳态周转约 9 团/秒
    /// （满压生成尝试约 15 次/秒，池满自弃），屏外剔除
    /// </summary>
    internal sealed class NyxdepthAmbientRender : RenderHandle
    {
        /// <summary>权重 1.85（本槽位分配值）</summary>
        public override float Weight => 1.85f;

        private const int MaxWisps = 18;

        private struct Wisp
        {
            internal bool Active;
            internal Vector2 Pos;
            internal Vector2 Vel;
            internal int Life;
            internal int MaxLife;
            internal float Scale;
            internal float Alpha;
            internal float Rot;
            internal float RotVel;
            internal bool Mirror;
        }

        private static readonly Wisp[] wisps = new Wisp[MaxWisps];
        private static int spawnIn;

        internal static void ClearWisps() {
            for (int i = 0; i < wisps.Length; i++) {
                wisps[i].Active = false;
            }
            spawnIn = 0;
        }

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }

            //推进在途雾团（快速移动时留在身后自然散去，读作从暗翳里游过）
            for (int i = 0; i < wisps.Length; i++) {
                if (!wisps[i].Active) {
                    continue;
                }
                wisps[i].Pos += wisps[i].Vel;
                wisps[i].Rot += wisps[i].RotVel;
                if (++wisps[i].Life >= wisps[i].MaxLife || OutOfView(wisps[i].Pos)) {
                    wisps[i].Active = false;
                }
            }

            float pressure = NyxdepthAmbience.Pressure;
            if (pressure < 0.05f) {
                return;
            }
            if (--spawnIn > 0) {
                return;
            }
            //渊压越大尝试越勤（满压约 15 次/秒）；稳态密度由池上限 18 硬预算，周转约 9 团/秒
            spawnIn = (int)MathHelper.Lerp(14f, 4f, pressure);
            SpawnWisp(pressure);
        }

        private static bool OutOfView(Vector2 pos) {
            return pos.X < Main.screenPosition.X - 500f
                || pos.X > Main.screenPosition.X + Main.screenWidth + 500f
                || pos.Y < Main.screenPosition.Y - 500f
                || pos.Y > Main.screenPosition.Y + Main.screenHeight + 500f;
        }

        /// <summary>自随机屏缘生出，带一点向屏心的收束速度</summary>
        private static void SpawnWisp(float pressure) {
            for (int i = 0; i < wisps.Length; i++) {
                if (wisps[i].Active) {
                    continue;
                }
                int edge = Main.rand.Next(4);
                float w = Main.screenWidth;
                float h = Main.screenHeight;
                Vector2 pos = edge switch {
                    0 => Main.screenPosition + new Vector2(-40f, Main.rand.NextFloat(h)),
                    1 => Main.screenPosition + new Vector2(w + 40f, Main.rand.NextFloat(h)),
                    2 => Main.screenPosition + new Vector2(Main.rand.NextFloat(w), -40f),
                    _ => Main.screenPosition + new Vector2(Main.rand.NextFloat(w), h + 40f),
                };
                Vector2 toCenter = (Main.screenPosition + new Vector2(w, h) * 0.5f - pos)
                    .SafeNormalize(Vector2.UnitY);
                wisps[i] = new Wisp {
                    Active = true,
                    Pos = pos,
                    Vel = toCenter * Main.rand.NextFloat(0.15f, 0.42f) + Main.rand.NextVector2Circular(0.12f, 0.12f),
                    Life = 0,
                    MaxLife = Main.rand.Next(90, 150),
                    Scale = Main.rand.NextFloat(1.4f, 3.0f),
                    Alpha = 0.10f + 0.10f * pressure,
                    Rot = Main.rand.NextFloat(MathHelper.TwoPi),
                    RotVel = Main.rand.NextFloat(-0.004f, 0.004f),
                    Mirror = Main.rand.NextBool(),
                };
                return;
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float presence = NyxdepthAmbience.Presence;
            bool anyWisp = false;
            if (presence > 0.01f) {
                for (int i = 0; i < wisps.Length; i++) {
                    if (wisps[i].Active) {
                        anyWisp = true;
                        break;
                    }
                }
            }
            bool gaze = NyxdepthGaze.Visible;
            if (!anyWisp && !gaze) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            //先画凝视再压黑雾：眼睛嵌在暗翳后头
            if (gaze) {
                NyxdepthGaze.Draw(spriteBatch);
            }
            if (anyWisp) {
                DrawWisps(spriteBatch, presence);
            }
            spriteBatch.End();
        }

        private static void DrawWisps(SpriteBatch sb, float presence) {
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null || fog.IsDisposed) {
                return;
            }
            Vector2 origin = fog.Size() * 0.5f;
            for (int i = 0; i < wisps.Length; i++) {
                if (!wisps[i].Active) {
                    continue;
                }
                float t = wisps[i].Life / (float)wisps[i].MaxLife;
                float env = MathHelper.Min(t / 0.25f, 1f) * MathHelper.Clamp((1f - t) / 0.35f, 0f, 1f);
                float a = wisps[i].Alpha * env * presence;
                if (a < 0.005f) {
                    continue;
                }
                //Fog 是真 alpha，近黑染色即真暗层；逐团随机镜像防同贴纸感
                sb.Draw(fog, wisps[i].Pos - Main.screenPosition, null,
                    new Color(6, 10, 16) * a, wisps[i].Rot, origin, wisps[i].Scale,
                    wisps[i].Mirror ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
        }
    }
}
