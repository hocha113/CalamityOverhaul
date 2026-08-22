using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.UI
{
    /// <summary>
    /// 入场揭示层：加载硬切后的「落底—棺门推开」演出，追加为最顶层 LegacyGameInterfaceLayer<br/>
    /// 过渡链路修复后的触发协议（旧一次性布尔在主世界残余帧被 PostUpdateEverything 提前消费，弃用）：<br/>
    /// 1. 军备时戳，加载屏每个下行帧刷新 <see cref="ArmFromLoading"/>，世界侧首个界面帧
    ///    检测到新鲜时戳即起黑幕，激活判定放在绘制侧保证世界首帧必被盖住（不变量）；<br/>
    /// 2. 首帧闩锁，纯黑保持到 PostDrawTiles 实跳 ≥2 帧（世界真实开画），8s 超时防御性放行，
    ///    黑幕期间显示「正在点亮烛火」状态行；<br/>
    /// 3. 演出用绘制侧墙钟限幅推进，只有真实呈现的帧才消耗演出时间，
    ///    Update 追帧突发（黑屏卡顿后的补跑）无法再把整段演出烧掉。全程不锁输入
    /// </summary>
    internal class DungeonworldEntryReveal : ModSystem
    {
        /// <summary>是否身处地牢子世界</summary>
        internal static bool InDungeonworld => Dungeonworld.Active;

        private const float MinHold = 0.15f;       //黑幕最短保持(宁可多黑不许闪帧)
        private const float HoldTimeout = 8f;      //首帧闩锁超时放行
        private const int TilesFramesNeed = 2;     //判定"世界开画"所需 PostDrawTiles 实跳帧数
        private const float OpenDuration = 1.40f;  //棺门竖缝向两侧推开
        private const float FadeDuration = 0.55f;  //残余黑角淡出
        private const float BellAt = 0.15f;        //开门起始后落底钟落点
        private const long ArmWindowMs = 30_000;   //加载末帧到世界首帧的最大容忍间隔(含长帧冻结)

        //军备时戳:加载屏下行帧逐帧刷新
        private static long armStamp = long.MinValue;
        //一次世界会话只播一次
        private static bool played;
        //阶段:0=闲置 1=黑幕等待(首帧闩锁) 2=开门播放
        private static int phase;
        private static float waitSeconds;
        private static float playSeconds;
        private static int tilesFrames;
        private static bool bellDone;
        private static long lastDrawTick;

        public static bool Active => phase != 0;

        /// <summary>加载屏下行帧逐帧调用，刷新军备时戳（替代旧 PendingEntryReveal 布尔）</summary>
        internal static void ArmFromLoading() => armStamp = Environment.TickCount64;

        public override void OnWorldLoad() {
            played = false;
            phase = 0;
        }

        public override void OnWorldUnload() {
            phase = 0;
            bellDone = false;
        }

        public override void PostDrawTiles() {
            if (phase != 1) {
                return;
            }
            tilesFrames++;
            if (tilesFrames == 1) {
                DungeonworldTransitionLog.Mark("PostDrawTiles 首跳(世界开画)");
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (Main.dedServ || !InDungeonworld) {
                return;
            }
            //世界侧首个界面帧就地起黑幕;时戳过期(非加载屏进入,如联机热重连)则安静跳过,不闪黑
            if (phase == 0) {
                if (played || Environment.TickCount64 - armStamp > ArmWindowMs) {
                    return;
                }
                played = true;
                phase = 1;
                waitSeconds = 0f;
                playSeconds = 0f;
                tilesFrames = 0;
                bellDone = false;
                lastDrawTick = Environment.TickCount64;
                DungeonworldTransitionLog.Mark("世界侧首帧,揭示黑幕起(距加载屏末帧 "
                    + $"{Environment.TickCount64 - DungeonworldLoadingScreen.LastDrawStamp}ms)");
            }
            //末层盖住常规 UI,演出结束自动撤下
            layers.Add(new LegacyGameInterfaceLayer(
                "CWRMod: Dungeonworld Entry Reveal",
                delegate {
                    DrawAndAdvance(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI));
        }

        //演出推进在绘制侧:墙钟限幅步进,冻结/追帧都偷不走演出时间
        private static void DrawAndAdvance(SpriteBatch sb) {
            if (phase == 0) {
                return;
            }
            long now = Environment.TickCount64;
            float dt = MathHelper.Clamp((now - lastDrawTick) / 1000f, 0f, 0.05f);
            lastDrawTick = now;

            float reveal = 0f;
            if (phase == 1) {
                waitSeconds += dt;
                bool worldAlive = tilesFrames >= TilesFramesNeed && waitSeconds >= MinHold;
                if (worldAlive || waitSeconds >= HoldTimeout) {
                    phase = 2;
                    DungeonworldTransitionLog.Mark(worldAlive
                        ? $"棺门开启(黑幕 {(int)(waitSeconds * 1000)}ms, 瓦片帧 {tilesFrames})"
                        : "黑幕等待超时,防御性放行开门");
                }
            }
            if (phase == 2) {
                playSeconds += dt;
                if (!bellDone && playSeconds >= BellAt) {
                    bellDone = true;
                    //第七响·落底:配方与加载屏 Toll 同源(Item52 主钟体 + 风底)
                    SoundEngine.PlaySound(SoundID.Item52 with { Pitch = -0.9f, Volume = 0.85f });
                    SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = -0.9f, Volume = 0.45f });
                }
                if (playSeconds < OpenDuration) {
                    reveal = MathHelper.SmoothStep(0f, 1f, playSeconds / OpenDuration);
                }
                else if (playSeconds < OpenDuration + FadeDuration) {
                    reveal = 1f + MathHelper.Clamp((playSeconds - OpenDuration) / FadeDuration, 0f, 1f) * 0.18f;
                }
                else {
                    phase = 0;
                    DungeonworldTransitionLog.Mark("揭示结束");
                    return;
                }
            }

            DrawOverlay(sb, reveal);
            if (phase == 1) {
                DrawHoldStatus(sb);
            }
        }

        private static void DrawOverlay(SpriteBatch sb, float reveal) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            int w = Main.screenWidth;
            int h = Main.screenHeight;
            var shader = EffectLoader.DungeonworldEntryReveal?.Value;
            if (shader == null) {
                //shader 缺席回退:纯黑实底横开(纯黑实底是 magic-pixel 的合法用途),不许裸切
                DrawOverlayFallback(sb, px, w, h, reveal);
                return;
            }

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);

            shader.Parameters["uTime"]?.SetValue(waitSeconds + playSeconds);
            shader.Parameters["uReveal"]?.SetValue(reveal);
            shader.Parameters["uAspectRatio"]?.SetValue((float)w / h);
            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(px, new Rectangle(0, 0, w, h), Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.UIScaleMatrix);
        }

        //黑幕等待期的极简状态行:告知玩家没卡死
        private static void DrawHoldStatus(SpriteBatch sb) {
            var text = DungeonworldLoadingScreen.RevealHold;
            if (text == null) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int dotN = (int)(waitSeconds * 1.7f) % 4;
            string line = text.Value + new string('.', dotN);
            const float scale = 0.9f;
            Vector2 size = font.MeasureString(line) * scale;
            var pos = new Vector2(Main.screenWidth * 0.5f - size.X * 0.5f, Main.screenHeight * 0.72f);
            float alpha = 0.55f + 0.2f * (float)Math.Sin(waitSeconds * 2.4f);
            sb.DrawString(font, line, pos + Vector2.One, Color.Black * (alpha * 0.55f),
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            sb.DrawString(font, line, pos, DungeonworldLoadTheme.Parchment * alpha,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        //CPU 回退:两块纯黑幕布自中央竖缝向两侧退场
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
