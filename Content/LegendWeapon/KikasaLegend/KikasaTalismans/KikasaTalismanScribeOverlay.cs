using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans
{
    /// <summary>
    /// 书符演出：神夜礼物线共享的一段本地表现。符纸在玩家身前浮现，
    /// 字形循 <see cref="KikasaTalismanGlyph.DrawInk"/> 的 reveal 逐笔写就（节拍按笔数分配），
    /// 朱点落定回弹后符纸飞入玩家怀中。<br/>
    /// 由叙事 Command 节点调 <see cref="Begin"/> 启动，Wait 时长取 <see cref="TotalTicksFor"/> 对拍；
    /// 纯本地表现，不做任何发放/解锁副作用。未注册 Key 由字形库的伞形兜底章接住，演出照常
    /// </summary>
    internal sealed class KikasaTalismanScribeOverlay : RenderHandle
    {
        //====时间轴（tick），总时长约 2~3 秒====
        private const int AppearTicks = 30;
        private const int PerStrokeTicks = 14;
        private const int MinWriteTicks = 56;
        private const int MaxWriteTicks = 112;
        private const int SealHoldTicks = 14;
        private const int FlyTicks = 26;

        private static bool active;
        private static string key;
        private static int timer;
        private static int writeTicks;
        private static int totalTicks;
        private static int lastStrokeIndex;
        //符纸悬停位（世界系，缓动跟人）与飞行起点快照
        private static Vector2 anchor;
        private static Vector2 flyStart;

        /// <summary>书写段时长：按笔数分配节拍，夹在上下限之间</summary>
        public static int WriteTicksFor(string talismanKey)
            => Math.Clamp(KikasaTalismanGlyph.StrokeCount(talismanKey) * PerStrokeTicks, MinWriteTicks, MaxWriteTicks);

        /// <summary>整场演出时长，叙事 Wait 节点取这里与画面对拍</summary>
        public static int TotalTicksFor(string talismanKey)
            => AppearTicks + WriteTicksFor(talismanKey) + SealHoldTicks + FlyTicks;

        /// <summary>启动演出；重复调用直接重置时间轴。服务器/无效玩家静默跳过</summary>
        public static void Begin(string talismanKey) {
            Player player = Main.LocalPlayer;
            if (Main.dedServ || player == null || !player.active) {
                return;
            }
            key = talismanKey;
            timer = 0;
            writeTicks = WriteTicksFor(talismanKey);
            totalTicks = TotalTicksFor(talismanKey);
            lastStrokeIndex = -1;
            anchor = AnchorTarget(player);
            flyStart = anchor;
            active = true;
            //纸落一声轻雨
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.5f, Pitch = -0.25f, MaxInstances = 3 }, anchor);
        }

        /// <summary>面朝一侧偏上的悬停位</summary>
        private static Vector2 AnchorTarget(Player player)
            => player.Center + new Vector2(player.direction * 54f, -64f);

        /// <summary>符墨身份色：定义给出，未注册退回纸面水光色（与符纸物品同一兜底）</summary>
        private static Color Accent
            => KikasaTalismanRegistry.TryGet(key, out KikasaTalismanDefinition def)
                ? def.InkAccent : KikasaTalismanPaperDraw.Sheen;

        public override void UpdateBySystem(int index) {
            if (!active) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                active = false;
                return;
            }

            timer++;
            anchor = Vector2.Lerp(anchor, AnchorTarget(player), 0.10f);
            int flyFrom = AppearTicks + writeTicks + SealHoldTicks;
            if (timer <= flyFrom) {
                flyStart = anchor;
            }

            TickWriteBeats();
            TickFlyTrail(player, flyFrom);

            if (timer >= totalTicks) {
                active = false;
                //入怀：一小口墨雾 + 收纳音
                PRTLoader.NewParticle<PRT_KikasaInkMist>(player.Center, -Vector2.UnitY * 0.6f,
                    KikasaTalismanPaperDraw.Ink, 0.8f)?.Configure(22);
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.55f, Pitch = 0.1f }, player.Center);
            }
        }

        /// <summary>书写段：跨笔时给笔锋音与墨尘，收笔时朱点落定重拍</summary>
        private static void TickWriteBeats() {
            int writeElapsed = timer - AppearTicks;
            if (writeElapsed < 0 || writeElapsed > writeTicks) {
                return;
            }

            int strokeCount = KikasaTalismanGlyph.StrokeCount(key);
            int strokeIndex = Math.Min((int)(writeElapsed / (float)writeTicks * strokeCount), strokeCount - 1);
            Vector2 glyphPos = GlyphCenter(anchor);

            if (strokeIndex != lastStrokeIndex) {
                lastStrokeIndex = strokeIndex;
                //笔锋：雨滴脆响随笔序渐扬
                SoundEngine.PlaySound(SoundID.Drip with {
                    Volume = 0.34f,
                    Pitch = -0.1f + strokeIndex * 0.06f,
                    MaxInstances = 3,
                }, glyphPos);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_KikasaInkBead>(
                        glyphPos + Main.rand.NextVector2Circular(9f, 12f),
                        Main.rand.NextVector2Circular(0.8f, 0.5f) + Vector2.UnitY * 0.6f,
                        KikasaTalismanPaperDraw.Ink, Main.rand.NextFloat(0.2f, 0.3f))
                        ?.Configure(Main.rand.Next(14, 22), 0.16f);
                }
            }

            //收笔一拍：朱点落定，身份色溅墨 + 水花
            if (writeElapsed == writeTicks) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 2 }, glyphPos);
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.28f, Pitch = 0.1f, MaxInstances = 2 }, glyphPos);
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_KikasaInkBead>(
                        glyphPos + Main.rand.NextVector2Circular(5f, 5f),
                        Main.rand.NextVector2Circular(1.6f, 1.2f) - Vector2.UnitY * 0.5f,
                        i < 2 ? Accent : KikasaTalismanPaperDraw.Ink,
                        Main.rand.NextFloat(0.22f, 0.34f))?.Configure(Main.rand.Next(16, 26), 0.2f);
                }
                PRTLoader.NewParticle<PRT_KikasaInkMist>(glyphPos, -Vector2.UnitY * 0.4f,
                    KikasaTalismanPaperDraw.Ink, 0.7f)?.Configure(24);
            }
        }

        /// <summary>飞行段：符纸身后拖细碎墨珠</summary>
        private static void TickFlyTrail(Player player, int flyFrom) {
            if (timer <= flyFrom || timer % 3 != 0) {
                return;
            }
            float fly01 = MathHelper.Clamp((timer - flyFrom) / (float)FlyTicks, 0f, 1f);
            Vector2 pos = Vector2.Lerp(flyStart, player.Center, EaseIn(fly01));
            PRTLoader.NewParticle<PRT_KikasaInkBead>(pos + Main.rand.NextVector2Circular(4f, 6f),
                Main.rand.NextVector2Circular(0.5f, 0.5f),
                Accent, Main.rand.NextFloat(0.18f, 0.28f))?.Configure(Main.rand.Next(10, 16), 0.1f);
        }

        //慢起快到：符纸像被收回去的
        private static float EaseIn(float t) => t * t;

        private static float Smooth01(float t) => MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(t, 0f, 1f));

        /// <summary>字形中心：纸顶向下 40%、纸心上方约一成纸高处，与符纸物品同一排布</summary>
        private static Vector2 GlyphCenter(Vector2 paperCenter) => paperCenter - new Vector2(0f, 7f);

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (!active || Main.gameMenu) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            float time = Main.GlobalTimeWrappedHourly;
            int flyFrom = AppearTicks + writeTicks + SealHoldTicks;
            float appear01 = Smooth01(timer / (float)AppearTicks);
            float reveal = MathHelper.Clamp((timer - AppearTicks) / (float)writeTicks, 0f, 1f);
            float fly01 = timer <= flyFrom ? 0f : MathHelper.Clamp((timer - flyFrom) / (float)FlyTicks, 0f, 1f);

            //位置：浮现自上方一寸落定，飞行段吸向玩家
            Vector2 center = anchor - Vector2.UnitY * (1f - appear01) * 14f;
            if (fly01 > 0f) {
                center = Vector2.Lerp(flyStart, player.Center, EaseIn(fly01));
            }

            //透明度与尺度：浮现渐显，飞行尾段收拢淡出
            float alpha = appear01 * (1f - Smooth01((fly01 - 0.6f) / 0.4f) * 0.9f);
            float scale = 2.0f * (0.8f + 0.2f * appear01) * (1f - 0.5f * EaseIn(fly01));
            if (alpha <= 0.01f) {
                return;
            }

            //摆角：悬停轻摆，飞行段倒向速度方向
            float sway = MathF.Sin(time * 2.1f + 1.3f) * 0.07f * (1f - fly01);
            if (fly01 > 0f) {
                Vector2 dir = player.Center - flyStart;
                if (dir != Vector2.Zero) {
                    sway += (dir.ToRotation() - MathHelper.PiOver2) * 0.25f * fly01;
                }
            }
            //下缘潮息随书写渐涨——墨落纸湿
            float soak = 0.16f + 0.30f * reveal + 0.05f * MathF.Sin(time * 1.2f);

            Vector2 size = new Vector2(19f, 34f) * scale;
            Vector2 down = (MathHelper.PiOver2 + sway).ToRotationVector2();
            Vector2 top = center - down * size.Y * 0.5f;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //冷青背光衬纸，A=0 加色只亮不暗
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color backlight = new(KikasaTalismanPaperDraw.Sheen.R, KikasaTalismanPaperDraw.Sheen.G,
                KikasaTalismanPaperDraw.Sheen.B, 0);
            spriteBatch.Draw(glow, center - Main.screenPosition, null,
                backlight * (alpha * (0.25f + 0.20f * reveal)), 0f, glow.Size() * 0.5f,
                size.Y * 2.2f / glow.Width, SpriteEffects.None, 0f);

            //符纸本体（内部切批再复原，复原态与本批一致）
            KikasaTalismanPaperDraw.DrawWorld(spriteBatch, top - Main.screenPosition, sway, size,
                alpha, soak, time + 3.1f);

            //湿墨字形：reveal 逐笔揭示，未注册 Key 落在伞形兜底章
            Vector2 glyphCenter = top + down * size.Y * 0.40f;
            KikasaTalismanGlyph.DrawInk(spriteBatch, key, glyphCenter - Main.screenPosition, size.X * 1.18f,
                alpha, KikasaTalismanPaperDraw.Ink, Accent, time, sway, reveal);

            //顶端结绳孔一粒墨点
            spriteBatch.Draw(VaultAsset.placeholder2.Value, top + down * 3f - Main.screenPosition,
                new Rectangle(0, 0, 1, 1), KikasaTalismanPaperDraw.Ink * (alpha * 0.85f),
                MathHelper.PiOver4 + sway, new Vector2(0.5f), new Vector2(2.4f * scale), SpriteEffects.None, 0f);

            spriteBatch.End();
        }
    }
}
