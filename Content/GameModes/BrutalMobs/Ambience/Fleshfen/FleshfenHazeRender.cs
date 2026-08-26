using CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Fleshfen
{
    /// <summary>
    /// 「血雾」屏幕层绘制：暗红雾团（Fog 真 alpha，AlphaBlend 才压得出暗层）+
    /// 缓慢上升的血珠微粒与血沼泡珠、肉面脉动微光（黑底 SoftGlow 一律走加色批）。
    /// 挂 EndEntityDraw 盖在实体之上（人在血雾里）；自开自收两段批，无 RT 槽。
    /// 雾团世界锚定随风缓漂，随心跳呼吸
    /// </summary>
    internal sealed class FleshfenHazeRender : RenderHandle
    {
        /// <summary>权重 1.66（本批槽位分配值，同批邻位 1.65=Rotmire / 1.67=Prismglade）</summary>
        public override float Weight => 1.66f;

        private const int PatchCount = 6;
        private const int MaxMotes = 16;

        private static readonly Color DeepBlood = EvilBiomeFX.Deep(EvilBiomeFX.FlavorCrimson);
        private static readonly Color BrightBlood = EvilBiomeFX.Bright(EvilBiomeFX.FlavorCrimson);

        private struct HazePatch
        {
            internal Vector2 Pos;
            internal float Scale;
            internal float Phase;
            internal float Spin;
        }

        private struct Mote
        {
            internal bool Active;
            internal Vector2 Pos;
            internal float Rise;
            internal float SwayPhase;
            internal int Life;
            internal int MaxLife;
            internal float Size;
        }

        private static readonly HazePatch[] patches = new HazePatch[PatchCount];
        private static readonly Mote[] motes = new Mote[MaxMotes];
        private static bool patchesInited;
        private static int moteSpawnIn;

        //==================== 逻辑更新 ====================

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = FleshfenAmbience.Presence;
            if (presence < 0.02f) {
                patchesInited = false;
                for (int i = 0; i < motes.Length; i++) {
                    motes[i].Active = false;
                }
                return;
            }

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            if (!patchesInited) {
                patchesInited = true;
                for (int i = 0; i < patches.Length; i++) {
                    patches[i] = new HazePatch {
                        Pos = player.Center + new Vector2(Main.rand.NextFloat(-1200f, 1200f), Main.rand.NextFloat(-650f, 650f)),
                        Scale = Main.rand.NextFloat(2.3f, 3.8f),
                        Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                        Spin = Main.rand.NextFloat(-0.0016f, 0.0016f),
                    };
                }
            }

            //雾团缓漂 + 越界回卷（相对本机玩家）
            float wind = MathHelper.Clamp(Main.windSpeedCurrent, -1f, 1f);
            for (int i = 0; i < patches.Length; i++) {
                patches[i].Pos.X += wind * 0.55f + MathF.Sin(Main.GlobalTimeWrappedHourly * 0.5f + patches[i].Phase) * 0.12f;
                patches[i].Pos.Y += MathF.Sin(Main.GlobalTimeWrappedHourly * 0.33f + patches[i].Phase * 1.7f) * 0.1f;
                float dx = patches[i].Pos.X - player.Center.X;
                float dy = patches[i].Pos.Y - player.Center.Y;
                if (dx > 1500f) {
                    patches[i].Pos.X -= 3000f;
                }
                else if (dx < -1500f) {
                    patches[i].Pos.X += 3000f;
                }
                if (dy > 900f) {
                    patches[i].Pos.Y -= 1800f;
                }
                else if (dy < -900f) {
                    patches[i].Pos.Y += 1800f;
                }
            }

            //推进血珠微粒
            for (int i = 0; i < motes.Length; i++) {
                if (!motes[i].Active) {
                    continue;
                }
                motes[i].Pos.Y -= motes[i].Rise;
                motes[i].Pos.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 1.3f + motes[i].SwayPhase) * 0.16f;
                motes[i].Life++;
                if (motes[i].Life >= motes[i].MaxLife
                    || motes[i].Pos.Y < Main.screenPosition.Y - 80f) {
                    motes[i].Active = false;
                }
            }

            //补充微粒（约 1 粒/16 帧，偏屏面下半，向上缓升）
            if (--moteSpawnIn > 0) {
                return;
            }
            moteSpawnIn = 16;
            for (int i = 0; i < motes.Length; i++) {
                if (motes[i].Active) {
                    continue;
                }
                motes[i] = new Mote {
                    Active = true,
                    Pos = new Vector2(
                        Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth),
                        Main.screenPosition.Y + Main.screenHeight * Main.rand.NextFloat(0.3f, 1.05f)),
                    Rise = Main.rand.NextFloat(0.25f, 0.6f),
                    SwayPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    Life = 0,
                    MaxLife = Main.rand.Next(110, 170),
                    Size = Main.rand.NextFloat(0.05f, 0.09f),
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
            float presence = FleshfenAmbience.Presence;
            if (presence < 0.02f || !patchesInited) {
                return;
            }
            Texture2D fog = CWRAsset.Fog?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (fog == null || glow == null || fog.IsDisposed || glow.IsDisposed) {
                return;
            }

            float breathe = 1f + 0.09f * FleshfenAmbience.BeatEnvelope;

            //暗雾层：真 alpha 才能压暗，走 AlphaBlend（加色物理上做不出暗层）
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            for (int i = 0; i < patches.Length; i++) {
                float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 0.7f + patches[i].Phase);
                float alpha = 0.15f * presence * pulse * breathe;
                Vector2 pos = patches[i].Pos - Main.screenPosition;
                float rot = patches[i].Phase + Main.GlobalTimeWrappedHourly * patches[i].Spin * 60f;
                spriteBatch.Draw(fog, pos, null, new Color(70, 12, 14) * alpha, rot,
                    fog.Size() * 0.5f, patches[i].Scale, SpriteEffects.None, 0f);
            }
            spriteBatch.End();

            //亮层：黑底 SoftGlow 一律加色批（微粒/泡珠/肉面微光）
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            DrawMotes(spriteBatch, glow, presence);
            DrawBubbles(spriteBatch, glow, presence);
            DrawPulseGlows(spriteBatch, glow);
            spriteBatch.End();
        }

        /// <summary>上升血珠微粒：暗红外晕 + 亮芯，竖向微拉（上升的各向异性）</summary>
        private static void DrawMotes(SpriteBatch sb, Texture2D glow, float presence) {
            Vector2 origin = glow.Size() * 0.5f;
            for (int i = 0; i < motes.Length; i++) {
                if (!motes[i].Active) {
                    continue;
                }
                float t = motes[i].Life / (float)motes[i].MaxLife;
                float env = Math.Min(t / 0.18f, 1f) * MathHelper.Clamp((1f - t) / 0.3f, 0f, 1f);
                float a = env * presence;
                if (a < 0.01f) {
                    continue;
                }
                Vector2 pos = motes[i].Pos - Main.screenPosition;
                float s = motes[i].Size;
                sb.Draw(glow, pos, null, new Color(196, 40, 34) * (0.5f * a), 0f, origin,
                    new Vector2(s * 0.95f, s * 1.55f), SpriteEffects.None, 0f);
                sb.Draw(glow, pos, null, new Color(255, 116, 92) * (0.35f * a), 0f, origin,
                    new Vector2(s * 0.4f, s * 0.62f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>血沼泡珠：水面鼓起的小血泡，渐大待破（破裂溅血由氛围层的粉尘承担）</summary>
        private static void DrawBubbles(SpriteBatch sb, Texture2D glow, float presence) {
            Vector2 origin = glow.Size() * 0.5f;
            var bubbles = FleshfenAmbience.MireBubbles;
            for (int i = 0; i < bubbles.Length; i++) {
                if (!bubbles[i].Active) {
                    continue;
                }
                float t = bubbles[i].Life / (float)bubbles[i].MaxLife;
                float grow = (0.03f + 0.055f * t) * bubbles[i].Size;
                float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + i * 2.3f) * 1.2f;
                Vector2 pos = bubbles[i].Pos - Main.screenPosition + new Vector2(0f, bob - 2f * t);
                sb.Draw(glow, pos, null, new Color(232, 60, 50) * (0.42f * presence), 0f, origin,
                    new Vector2(grow, grow * 0.85f), SpriteEffects.None, 0f);
                sb.Draw(glow, pos + new Vector2(-grow * 8f, -grow * 9f), null,
                    new Color(255, 150, 120) * (0.22f * presence), 0f, origin,
                    new Vector2(grow * 0.3f, grow * 0.3f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>肉面脉动微光：贴面的扁平血光，与心跳同一条包络</summary>
        private static void DrawPulseGlows(SpriteBatch sb, Texture2D glow) {
            Vector2 origin = glow.Size() * 0.5f;
            var spots = FleshfenAmbience.PulseSpots;
            for (int i = 0; i < spots.Length; i++) {
                if (!spots[i].Active) {
                    continue;
                }
                float env = FleshfenAmbience.SpotEnv(in spots[i]);
                if (env < 0.02f) {
                    continue;
                }
                Vector2 pos = spots[i].Pos - Main.screenPosition;
                Color warm = Color.Lerp(BrightBlood, DeepBlood, 0.35f);
                sb.Draw(glow, pos, null, warm * (0.32f * env), 0f, origin,
                    new Vector2(0.9f * spots[i].Strength, 0.42f * spots[i].Strength), SpriteEffects.None, 0f);
            }
        }
    }
}
