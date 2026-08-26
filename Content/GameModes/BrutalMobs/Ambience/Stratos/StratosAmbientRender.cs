using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stratos
{
    /// <summary>
    /// 「稀空」屏幕层绘制：细碎流星划痕（纯视觉，白天低频夜里加密，挂 DrawNPCsOverTiles
    /// 画在演员身后读作天幕）与「气薄」缺氧渐晕（屏边微暗收束+呼吸脉动，挂 EndEntityDraw
    /// 压在实体之上）。划痕用速度线截条三段收口，两端不许一刀切
    /// </summary>
    internal sealed class StratosAmbientRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.69</summary>
        public override float Weight => 1.69f;

        private const int MaxStreaks = 6;

        private struct Streak
        {
            internal bool Active;
            internal Vector2 Pos;
            internal Vector2 Dir;
            internal float Speed;
            internal int Life;
            internal int MaxLife;
            internal int SrcY;
            internal float LenScale;
            internal float Bright;
        }

        private static readonly Streak[] streaks = new Streak[MaxStreaks];
        private static int streakSpawnIn;

        private static readonly Color StreakCold = new(205, 225, 255);
        private static readonly Color VeilDark = new(6, 9, 15);

        //==================== 逻辑更新（流星划痕）====================

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = StratosAmbience.Presence;
            if (presence < 0.05f) {
                for (int i = 0; i < streaks.Length; i++) {
                    streaks[i].Active = false;
                }
                streakSpawnIn = 60;
                return;
            }

            for (int i = 0; i < streaks.Length; i++) {
                if (!streaks[i].Active) {
                    continue;
                }
                streaks[i].Pos += streaks[i].Dir * streaks[i].Speed;
                streaks[i].Life++;
                if (streaks[i].Life >= streaks[i].MaxLife) {
                    streaks[i].Active = false;
                }
            }

            if (--streakSpawnIn > 0) {
                return;
            }
            //夜里密度提升：这是纯视觉划痕的唯一昼夜差
            streakSpawnIn = Main.dayTime ? Main.rand.Next(320, 580) : Main.rand.Next(130, 260);
            for (int i = 0; i < streaks.Length; i++) {
                if (streaks[i].Active) {
                    continue;
                }
                //斜向下掠过屏幕上半，左右向随机
                float angle = Main.rand.NextFloat(0.55f, 0.95f);
                if (Main.rand.NextBool()) {
                    angle = MathHelper.Pi - angle;
                }
                streaks[i] = new Streak {
                    Active = true,
                    Pos = Main.screenPosition + new Vector2(
                        Main.rand.NextFloat(-0.1f, 1.1f) * Main.screenWidth,
                        Main.rand.NextFloat(0.02f, 0.32f) * Main.screenHeight),
                    Dir = angle.ToRotationVector2(),
                    Speed = Main.rand.NextFloat(15f, 25f),
                    Life = 0,
                    MaxLife = Main.rand.Next(28, 44),
                    SrcY = Main.rand.Next(0, 1010),
                    LenScale = Main.rand.NextFloat(0.09f, 0.14f),
                    Bright = Main.rand.NextFloat(0.55f, 1f),
                };
                return;
            }
        }

        //==================== 绘制 ====================

        /// <summary>划痕画在贴墙 NPC 层之前：读作远天幕上的擦痕，不盖玩家与敌人</summary>
        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float presence = StratosAmbience.Presence;
            if (presence < 0.05f) {
                return;
            }
            bool any = false;
            for (int i = 0; i < streaks.Length; i++) {
                if (streaks[i].Active) {
                    any = true;
                    break;
                }
            }
            if (!any) {
                return;
            }

            Texture2D lines = CWRAsset.SpeedLines01?.Value;
            Texture2D glow = CWRAsset.StarGlow01?.Value;
            if (lines == null || glow == null) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            //三段源截条与端部收口：尾暗-中亮-头满（SpeedLines01 长轴零端部衰减，整条拉伸=两端一刀切）
            ReadOnlySpan<int> segX = [0, 307, 717];
            ReadOnlySpan<int> segW = [307, 410, 307];
            ReadOnlySpan<float> segA = [0.30f, 0.65f, 1f];
            float dayDim = Main.dayTime ? 0.72f : 1f;

            for (int i = 0; i < streaks.Length; i++) {
                if (!streaks[i].Active) {
                    continue;
                }
                float t = streaks[i].Life / (float)streaks[i].MaxLife;
                float env = Math.Min(t / 0.18f, 1f) * MathHelper.Clamp((1f - t) / 0.30f, 0f, 1f);
                float alpha = 0.35f * env * presence * streaks[i].Bright * dayDim;
                if (alpha < 0.004f) {
                    continue;
                }
                float rotation = streaks[i].Dir.ToRotation();
                for (int s = 0; s < 3; s++) {
                    var src = new Rectangle(segX[s], streaks[i].SrcY, segW[s], 12);
                    //头端在 Pos：各截条按中心距头端沿轴回摆
                    float back = (1024f - (segX[s] + segW[s] * 0.5f)) * streaks[i].LenScale;
                    Vector2 pos = streaks[i].Pos - streaks[i].Dir * back - Main.screenPosition;
                    spriteBatch.Draw(lines, pos, src, StreakCold * (alpha * segA[s]), rotation,
                        new Vector2(segW[s] * 0.5f, 6f),
                        new Vector2(streaks[i].LenScale, 0.55f), SpriteEffects.None, 0f);
                }
                //亮头星点
                spriteBatch.Draw(glow, streaks[i].Pos - Main.screenPosition, null,
                    StreakCold * (alpha * 1.1f), rotation, glow.Size() * 0.5f,
                    0.10f + 0.05f * streaks[i].Bright, SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }

        /// <summary>缺氧渐晕：屏边真 alpha 暗带向内收束，随呼吸波脉动，压在实体之上 UI 之下</summary>
        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            StratosPlayer local = Main.LocalPlayer.GetModPlayer<StratosPlayer>();
            float hypoxia = local.Hypoxia;
            if (hypoxia < 0.03f) {
                return;
            }
            Texture2D spindle = CWRAsset.Extra_98?.Value;
            Texture2D fog = CWRAsset.Fog?.Value;
            if (spindle == null || fog == null) {
                return;
            }

            float pulse = 0.82f + 0.18f * local.BreathWave;
            float alpha = 0.46f * MathF.Pow(hypoxia, 1.4f) * pulse;
            float band = MathHelper.Lerp(70f, 170f, hypoxia);//越缺氧收束越深
            int w = Main.screenWidth;
            int h = Main.screenHeight;
            Vector2 spindleOrig = spindle.Size() * 0.5f;
            Color dark = VeilDark * alpha;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);

            //上下边带：梭形长轴横放（旋转 π/2 后贴图 Y 轴映到屏幕 X）
            Vector2 acrossScale = new(band * 2.2f / spindle.Width, w * 1.35f / spindle.Height);
            spriteBatch.Draw(spindle, new Vector2(w * 0.5f, 0f), null, dark,
                MathHelper.PiOver2, spindleOrig, acrossScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(spindle, new Vector2(w * 0.5f, h), null, dark,
                MathHelper.PiOver2, spindleOrig, acrossScale, SpriteEffects.None, 0f);
            //左右边带
            Vector2 sideScale = new(band * 2.2f / spindle.Width, h * 1.35f / spindle.Height);
            spriteBatch.Draw(spindle, new Vector2(0f, h * 0.5f), null, dark,
                0f, spindleOrig, sideScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(spindle, new Vector2(w, h * 0.5f), null, dark,
                0f, spindleOrig, sideScale, SpriteEffects.None, 0f);
            //四角雾团补角
            Vector2 fogOrig = fog.Size() * 0.5f;
            float cornerScale = 1.5f + 0.9f * hypoxia;
            Color cornerDark = VeilDark * (alpha * 0.75f);
            for (int k = 0; k < 4; k++) {
                Vector2 corner = new(k % 2 == 0 ? 0f : w, k < 2 ? 0f : h);
                spriteBatch.Draw(fog, corner, null, cornerDark, k * 1.7f, fogOrig,
                    cornerScale, SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }
    }
}
