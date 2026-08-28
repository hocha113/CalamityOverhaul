using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sporeshine
{
    /// <summary>
    /// 发光蘑菇地屏幕层绘制（挂 EndEntityDraw，自开自收批次，无 RT 槽）：<br/>
    /// 环境雾体：真 alpha 蓝澜雾片走 AlphaBlend 乘环境光，缓慢弥漫漂移，承担遮挡感；<br/>
    /// 「蓝澜」孢子光尘：屏内漂浮的蓝色光点，被移动中的玩家推挤散开（加色只做雾中光点）；<br/>
    /// 荧光波纹：踩菇的地面扩散椭圆与菌歌的圆晕（数据在 <see cref="SporeshineAmbience"/>）；<br/>
    /// 「孢醉」屏边：迷醉越深屏幕四缘蓝光越浓、色调轻微摇曳（亮边警示，可读性保留）
    /// </summary>
    internal sealed class SporeshineRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.68</summary>
        public override float Weight => 1.68f;

        private const int MaxMotes = 40;
        /// <summary>光尘受玩家扰动的作用半径</summary>
        private const float StirRange = 110f;
        /// <summary>光尘速度封顶（被推开也不乱飞）</summary>
        private const float MoteMaxSpeed = 3f;

        //==== 环境雾体（真 alpha 遮挡层）====
        private const int MaxHaze = 10;
        /// <summary>雾片基础透明度（AlphaBlend 真 alpha，承担遮挡感）</summary>
        private const float HazeAlpha = 0.17f;
        /// <summary>雾片环境光乘算下限（微弱孢光，防全黑处彻底沉没）</summary>
        private const float HazeLightFloor = 0.3f;

        private static readonly Color MoteBlue = new(96, 190, 255);
        private static readonly Color RippleBlue = new(110, 215, 255);
        private static readonly Color EdgeBlueA = new(70, 150, 255);
        private static readonly Color EdgeBlueB = new(125, 115, 255);
        private static readonly Color HazeBlue = new(26, 44, 82);

        private struct Mote
        {
            internal bool Active;
            internal Vector2 Pos;
            internal Vector2 Vel;
            internal int Life;
            internal int MaxLife;
            internal float Scale;
            internal float Seed;
        }

        private static readonly Mote[] motes = new Mote[MaxMotes];
        private static int moteSpawnIn;

        private struct Haze
        {
            internal bool Active;
            internal Vector2 Pos;
            internal Vector2 Vel;
            internal int Life;
            internal int MaxLife;
            internal float Scale;
            internal float Seed;
        }

        //屏幕级演出量（非逐玩家状态），与光尘同一口径
        private static readonly Haze[] hazes = new Haze[MaxHaze];
        private static int hazeSpawnIn;

        //本帧会扰动光尘的玩家复用缓冲（避免逐光尘全槽扫描）
        private static readonly Player[] stirPlayers = new Player[Main.maxPlayers];

        //==================== 逻辑更新（孢子光尘） ====================

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = SporeshineAmbience.Presence;
            if (presence < 0.02f) {
                for (int i = 0; i < motes.Length; i++) {
                    motes[i].Active = false;
                }
                for (int i = 0; i < hazes.Length; i++) {
                    hazes[i].Active = false;
                }
                return;
            }

            UpdateHazes();

            //先收集会扰动光尘的玩家，免得每粒光尘都过一遍 255 槽
            int stirCount = 0;
            foreach (Player stirPlayer in Main.ActivePlayers) {
                if (stirPlayer.dead || stirPlayer.velocity.Length() < 1f) {
                    continue;
                }
                stirPlayers[stirCount++] = stirPlayer;
            }

            float time = Main.GlobalTimeWrappedHourly;
            for (int i = 0; i < motes.Length; i++) {
                if (!motes[i].Active) {
                    continue;
                }
                ref Mote m = ref motes[i];
                //基础漂移：极缓的横向游摆+微微上浮
                m.Vel.X += MathF.Sin(time * 0.7f + m.Seed) * 0.006f;
                m.Vel.Y -= 0.0035f;
                //玩家扰动：移动中的玩家把身边光尘推挤散开
                for (int p = 0; p < stirCount; p++) {
                    Player player = stirPlayers[p];
                    float playerSpeed = player.velocity.Length();
                    Vector2 away = m.Pos - player.Center;
                    float dist = away.Length();
                    if (dist > StirRange || dist < 1f) {
                        continue;
                    }
                    m.Vel += away / dist * (playerSpeed * 0.045f) * (1f - dist / StirRange);
                }
                if (m.Vel.LengthSquared() > MoteMaxSpeed * MoteMaxSpeed) {
                    m.Vel = Vector2.Normalize(m.Vel) * MoteMaxSpeed;
                }
                m.Vel *= 0.955f;
                m.Pos += m.Vel;
                m.Life++;

                bool offScreen = m.Pos.X < Main.screenPosition.X - 260f
                    || m.Pos.X > Main.screenPosition.X + Main.screenWidth + 260f
                    || m.Pos.Y < Main.screenPosition.Y - 260f
                    || m.Pos.Y > Main.screenPosition.Y + Main.screenHeight + 260f;
                if (m.Life >= m.MaxLife || offScreen) {
                    m.Active = false;
                }
            }

            //补充：每 4 帧至多一粒，稳态约三十余粒在屏
            if (--moteSpawnIn > 0) {
                return;
            }
            moteSpawnIn = 4;
            for (int i = 0; i < motes.Length; i++) {
                if (motes[i].Active) {
                    continue;
                }
                motes[i] = new Mote {
                    Active = true,
                    Pos = new Vector2(
                        Main.screenPosition.X + Main.rand.NextFloat(-60f, Main.screenWidth + 60f),
                        Main.screenPosition.Y + Main.rand.NextFloat(-60f, Main.screenHeight + 60f)),
                    Vel = new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.15f, 0.05f)),
                    Life = 0,
                    MaxLife = Main.rand.Next(300, 480),
                    Scale = Main.rand.NextFloat(0.09f, 0.16f),
                    Seed = Main.rand.NextFloat(MathHelper.TwoPi),
                };
                return;
            }
        }

        /// <summary>环境雾片推进：极缓漂移，寿命尽或漂出屏外即回收，每 14 帧至多补一片</summary>
        private static void UpdateHazes() {
            float time = Main.GlobalTimeWrappedHourly;
            for (int i = 0; i < hazes.Length; i++) {
                if (!hazes[i].Active) {
                    continue;
                }
                ref Haze h = ref hazes[i];
                h.Pos += h.Vel + new Vector2(MathF.Sin(time * 0.3f + h.Seed) * 0.08f, 0f);
                h.Life++;

                bool offScreen = h.Pos.X < Main.screenPosition.X - 420f
                    || h.Pos.X > Main.screenPosition.X + Main.screenWidth + 420f
                    || h.Pos.Y < Main.screenPosition.Y - 420f
                    || h.Pos.Y > Main.screenPosition.Y + Main.screenHeight + 420f;
                if (h.Life >= h.MaxLife || offScreen) {
                    h.Active = false;
                }
            }

            if (--hazeSpawnIn > 0) {
                return;
            }
            hazeSpawnIn = 14;
            for (int i = 0; i < hazes.Length; i++) {
                if (hazes[i].Active) {
                    continue;
                }
                hazes[i] = new Haze {
                    Active = true,
                    Pos = new Vector2(
                        Main.screenPosition.X + Main.rand.NextFloat(-160f, Main.screenWidth + 160f),
                        Main.screenPosition.Y + Main.rand.NextFloat(-160f, Main.screenHeight + 160f)),
                    Vel = new Vector2(Main.rand.NextFloat(-0.22f, 0.22f), Main.rand.NextFloat(-0.04f, 0.04f)),
                    Life = 0,
                    MaxLife = Main.rand.Next(480, 840),
                    Scale = Main.rand.NextFloat(0.9f, 1.8f),
                    Seed = Main.rand.NextFloat(MathHelper.TwoPi),
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
            float presence = SporeshineAmbience.EffectivePresence;
            float daze = 0f;
            if (Main.LocalPlayer.active) {
                daze = Main.LocalPlayer.GetModPlayer<SporeshinePlayer>().DazeVisual;
            }
            if (presence < 0.02f && daze < 0.02f) {
                return;
            }

            //世界层一：环境雾体（AlphaBlend 真 alpha 乘环境光，承担遮挡感）
            if (presence >= 0.02f) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
                DrawHaze(spriteBatch, presence);
                spriteBatch.End();

                //世界层二：光尘与波纹（加色只做雾中光点，随镜头矩阵）
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null,
                    Main.GameViewMatrix.TransformationMatrix);
                DrawMotes(spriteBatch, presence);
                DrawRipples(spriteBatch, presence);
                spriteBatch.End();
            }

            //屏幕层：孢醉屏边蓝光（加色批，屏幕坐标）
            if (daze >= 0.02f) {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone);
                DrawDazeVeil(spriteBatch, daze);
                spriteBatch.End();
            }
        }

        /// <summary>环境雾片：真 alpha 蓝澜雾体，逐片乘环境光（亮处显形暗处沉没）</summary>
        private static void DrawHaze(SpriteBatch sb, float presence) {
            Texture2D fogTex = CWRAsset.Fog?.Value;
            if (fogTex == null || fogTex.IsDisposed) {
                return;
            }
            Vector2 origin = fogTex.Size() * 0.5f;
            float time = Main.GlobalTimeWrappedHourly;

            for (int i = 0; i < hazes.Length; i++) {
                if (!hazes[i].Active) {
                    continue;
                }
                ref Haze h = ref hazes[i];
                float t = h.Life / (float)h.MaxLife;
                float env = MathF.Min(t / 0.22f, 1f) * MathHelper.Clamp((1f - t) / 0.25f, 0f, 1f);
                Color lit = Lighting.GetColor((int)(h.Pos.X / 16f), (int)(h.Pos.Y / 16f));
                float lightK = HazeLightFloor + (1f - HazeLightFloor) * ((lit.R + lit.G + lit.B) / 765f);
                float alpha = HazeAlpha * env * presence * lightK;
                if (alpha < 0.004f) {
                    continue;
                }
                float breathe = 1f + 0.05f * MathF.Sin(time * 0.5f + h.Seed);
                sb.Draw(fogTex, h.Pos - Main.screenPosition, null, HazeBlue * alpha,
                    h.Seed + time * 0.02f, origin, h.Scale * breathe, SpriteEffects.None, 0f);
            }
        }

        private static void DrawMotes(SpriteBatch sb, float presence) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            if (glow == null || glow.IsDisposed) {
                return;
            }
            Vector2 glowOrigin = glow.Size() * 0.5f;
            float time = Main.GlobalTimeWrappedHourly;

            for (int i = 0; i < motes.Length; i++) {
                if (!motes[i].Active) {
                    continue;
                }
                ref Mote m = ref motes[i];
                float t = m.Life / (float)m.MaxLife;
                float env = MathF.Min(t / 0.18f, 1f) * MathHelper.Clamp((1f - t) / 0.3f, 0f, 1f);
                float alpha = 0.34f * env * presence;
                if (alpha < 0.005f) {
                    continue;
                }
                Vector2 pos = m.Pos - Main.screenPosition;
                sb.Draw(glow, pos, null, MoteBlue * alpha, 0f, glowOrigin, m.Scale, SpriteEffects.None, 0f);
                //偶发星芒闪烁（荧光孢子眨眼）
                float glint = MathF.Sin(time * (2.2f + m.Seed * 0.6f) + m.Seed * 5f);
                if (star != null && !star.IsDisposed && glint > 0.9f) {
                    float gs = (glint - 0.9f) * 10f;
                    sb.Draw(star, pos, null, Color.White * (alpha * 1.1f * gs), m.Seed,
                        star.Size() * 0.5f, m.Scale * 0.45f, SpriteEffects.None, 0f);
                }
            }
        }

        private static void DrawRipples(SpriteBatch sb, float presence) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || glow.IsDisposed) {
                return;
            }
            Vector2 origin = glow.Size() * 0.5f;
            var ripples = SporeshineAmbience.Ripples;
            for (int i = 0; i < ripples.Length; i++) {
                if (!ripples[i].Active) {
                    continue;
                }
                float t = ripples[i].Life / (float)ripples[i].MaxLife;
                Vector2 pos = ripples[i].Pos - Main.screenPosition;
                if (ripples[i].Kind == 0) {
                    //踩菇：沿地面扩散的扁椭圆双层（外圈快、内圈慢半拍）
                    float alpha = 0.4f * (1f - t) * presence;
                    sb.Draw(glow, pos, null, RippleBlue * alpha, 0f, origin,
                        new Vector2(0.5f + 1.6f * t, 0.15f), SpriteEffects.None, 0f);
                    sb.Draw(glow, pos, null, RippleBlue * (alpha * 0.55f), 0f, origin,
                        new Vector2(0.3f + 1.1f * t, 0.1f), SpriteEffects.None, 0f);
                }
                else {
                    //菌歌：菌盖圆晕缓涨缓灭
                    float alpha = 0.3f * MathF.Sin(t * MathHelper.Pi) * presence;
                    sb.Draw(glow, pos, null, RippleBlue * alpha, ripples[i].Seed, origin,
                        0.5f + 0.9f * t, SpriteEffects.None, 0f);
                }
            }
        }

        //孢醉屏边：四缘蓝光柔化+轻微色偏摇曳。只用加色缘带，禁全屏遮挡
        private static void DrawDazeVeil(SpriteBatch sb, float daze) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || glow.IsDisposed) {
                return;
            }
            Vector2 origin = glow.Size() * 0.5f;
            float time = Main.GlobalTimeWrappedHourly;
            int w = Main.screenWidth;
            int h = Main.screenHeight;

            //色偏摇曳：青蓝与蓝紫之间缓慢摆动
            Color sway = Color.Lerp(EdgeBlueA, EdgeBlueB, 0.5f + 0.5f * MathF.Sin(time * 0.83f));
            float breathe = 0.9f + 0.1f * MathF.Sin(time * 2.1f);
            float edgeAlpha = 0.34f * daze * breathe;

            //上下缘横带（中心压在屏缘，半幅露出）
            Vector2 hScale = new(w * 1.35f / glow.Width, h * 0.0069f);
            sb.Draw(glow, new Vector2(w * 0.5f, 0f), null, sway * edgeAlpha, 0f, origin, hScale, SpriteEffects.None, 0f);
            sb.Draw(glow, new Vector2(w * 0.5f, h), null, sway * edgeAlpha, 0f, origin, hScale, SpriteEffects.None, 0f);
            //左右缘竖带
            Vector2 vScale = new(w * 0.0058f, h * 1.35f / glow.Height);
            sb.Draw(glow, new Vector2(0f, h * 0.5f), null, sway * (edgeAlpha * 0.9f), 0f, origin, vScale, SpriteEffects.None, 0f);
            sb.Draw(glow, new Vector2(w, h * 0.5f), null, sway * (edgeAlpha * 0.9f), 0f, origin, vScale, SpriteEffects.None, 0f);
        }
    }
}
