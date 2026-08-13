using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.UI
{
    /// <summary>
    /// 入场揭示层：加载硬切后的「落底—棺门推开」演出，追加为最顶层 LegacyGameInterfaceLayer<br/>
    /// 加载屏收不到完成预告（gameMenu 翻 false 当帧即被硬切），一切「抵达」演出在世界内接力完成；<br/>
    /// 由 <see cref="DungeonworldLoadingScreen.PendingEntryReveal"/> 自动拉起，A 路无需接线；全程不锁输入
    /// </summary>
    internal class DungeonworldEntryReveal : ModSystem
    {
        /// <summary>是否身处地牢子世界</summary>
        internal static bool InDungeonworld => Dungeonworld.Active;

        private const float HoldDuration = 0.15f;   //纯黑保持，掩护世界首帧
        private const float OpenDuration = 1.40f;   //棺门竖缝向两侧推开
        private const float FadeDuration = 0.55f;   //残余黑角淡出
        private const float TotalDuration = HoldDuration + OpenDuration + FadeDuration;
        private const float BellAt = 0.30f;         //落底钟（最低沉一响）落点

        //-1=未激活；0..Total 进行中
        private static float revealTime = -1f;
        private static bool bellDone;

        public static bool Active => revealTime >= 0f && revealTime < TotalDuration;

        public override void OnWorldUnload() {
            revealTime = -1f;
            bellDone = false;
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            //消费加载屏挂起的待启标志；落进了别的世界则直接撤销
            if (DungeonworldLoadingScreen.PendingEntryReveal) {
                DungeonworldLoadingScreen.PendingEntryReveal = false;
                if (InDungeonworld) {
                    revealTime = 0f;
                    bellDone = false;
                }
            }
            if (!Active) {
                return;
            }
            if (!bellDone && revealTime >= BellAt) {
                bellDone = true;
                //第七响·落底：配方与加载屏 Toll 同源（Item52 主钟体 + 风底）
                SoundEngine.PlaySound(SoundID.Item52 with { Pitch = -0.9f, Volume = 0.85f });
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = -0.9f, Volume = 0.45f });
            }
            //世界内固定 60Hz
            revealTime += 1f / 60f;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (!InDungeonworld || !Active) {
                return;
            }
            //末层盖住常规 UI，演出结束自动撤下
            layers.Add(new LegacyGameInterfaceLayer(
                "CWRMod: Dungeonworld Entry Reveal",
                delegate {
                    DrawOverlay(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI));
        }

        private static void DrawOverlay(SpriteBatch sb) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }

            float t = revealTime;
            //reveal 三段：Hold（0）/ Open（0..1 平滑）/ Fade（1..1.18）
            float reveal;
            if (t < HoldDuration) {
                reveal = 0f;
            }
            else if (t < HoldDuration + OpenDuration) {
                float u = (t - HoldDuration) / OpenDuration;
                reveal = MathHelper.SmoothStep(0f, 1f, u);
            }
            else {
                float u = (t - HoldDuration - OpenDuration) / FadeDuration;
                reveal = 1f + MathHelper.Clamp(u, 0f, 1f) * 0.18f;
            }

            int w = Main.screenWidth;
            int h = Main.screenHeight;
            var shader = EffectLoader.DungeonworldEntryReveal?.Value;
            if (shader == null) {
                //shader 缺席回退：纯黑实底横开（纯黑实底是 magic-pixel 的合法用途），不许裸切
                DrawOverlayFallback(sb, px, w, h, reveal);
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);

            shader.Parameters["uTime"]?.SetValue(t);
            shader.Parameters["uReveal"]?.SetValue(reveal);
            shader.Parameters["uAspectRatio"]?.SetValue((float)w / h);
            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(px, new Rectangle(0, 0, w, h), Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);
        }

        //CPU 回退：两块纯黑幕布自中央竖缝向两侧退场
        private static void DrawOverlayFallback(SpriteBatch sb, Texture2D px, int w, int h, float reveal) {
            float open01 = MathHelper.Clamp(reveal, 0f, 1f);
            float fade = MathHelper.Clamp((reveal - 1f) / 0.18f, 0f, 1f);
            float alpha = 1f - fade;
            int halfOpen = (int)(open01 * (w * 0.5f + 8f));
            int leftW = w / 2 - halfOpen;
            int rightX = w / 2 + halfOpen;
            if (leftW > 0) {
                sb.Draw(px, new Rectangle(0, 0, leftW, h), Color.Black * alpha);
            }
            if (rightX < w) {
                sb.Draw(px, new Rectangle(rightX, 0, w - rightX, h), Color.Black * alpha);
            }
        }
    }
}
