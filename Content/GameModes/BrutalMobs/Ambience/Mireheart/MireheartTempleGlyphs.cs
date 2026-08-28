using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Mireheart
{
    /// <summary>
    /// 「庙纹苏醒」：神庙墙面的太阳符纹低频亮起，沿墙逐个点燃扫过，
    /// 金色粒子流顺着符链行进，配古老石声。纯氛围压迫，无判定无伤害。
    /// 全部状态是本地屏幕演出（随本地玩家的神庙旗标走），不进网络。
    /// 符纹画在 DrawNPCsOverTiles（演员身后），读作墙上的纹样而非前景特效
    /// </summary>
    internal sealed class MireheartTempleRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.76（Ambience/Mireheart 专属）</summary>
        public override float Weight => 1.76f;

        //DiffusionCircle4 128² 黑底薄锐缘环（0.95R），加色批合法
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> DiffusionCircle4 = null;

        //==== 符链参数 ====
        private const int MaxGlyphs = 9;
        private const int MinGlyphs = 3;
        /// <summary>相邻符纹点燃间隔（帧），扫墙节奏</summary>
        private const int IgniteGap = 12;
        /// <summary>单个符纹寿命：起 18 / 驻 46 / 收 32</summary>
        private const int GlyphRise = 18;
        private const int GlyphHold = 46;
        private const int GlyphFade = 32;
        private const int GlyphLife = GlyphRise + GlyphHold + GlyphFade;
        /// <summary>符链低频间隔（帧）</summary>
        private const int ChainGapMin = 480;
        private const int ChainGapMax = 840;
        /// <summary>建链失败的短复查（帧）</summary>
        private const int ChainRetry = 90;

        private struct Glyph
        {
            internal Vector2 Pos;
            internal int Delay;
            internal float Seed;
        }

        private static readonly Glyph[] glyphs = new Glyph[MaxGlyphs];
        private static int glyphCount;
        private static int chainAge;
        private static int chainCooldown = 240;

        //==== 逻辑推进 ====

        public override void UpdateBySystem(int index) {
            if (Main.dedServ || Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = MireheartAmbience.TemplePresence;
            if (presence <= 0.05f) {
                glyphCount = 0;
                return;
            }

            if (glyphCount > 0) {
                AdvanceChain();
                return;
            }
            if (--chainCooldown > 0) {
                return;
            }
            chainCooldown = TryBuildChain() ? Main.rand.Next(ChainGapMin, ChainGapMax) : ChainRetry;
        }

        private static void AdvanceChain() {
            chainAge++;
            int lastDelay = glyphs[glyphCount - 1].Delay;
            if (chainAge > lastDelay + GlyphLife) {
                glyphCount = 0;
                return;
            }

            float dim = CWRWorld.HasBoss ? 0.5f : 1f;
            for (int i = 0; i < glyphCount; i++) {
                int local = chainAge - glyphs[i].Delay;
                if (local == 0) {
                    IgniteGlyph(i);
                }
                if (local <= 0 || local >= GlyphLife) {
                    continue;
                }
                Lighting.AddLight(glyphs[i].Pos,
                    new Vector3(0.5f, 0.36f, 0.12f) * (Envelope(local) * 0.6f * dim));
            }

            //金色粒子流：顺链跟随点燃头行进（≤2 粒/帧，只在扫墙期）
            if (chainAge < lastDelay && glyphCount > 1 && !Main.rand.NextBool(3)) {
                float headFrac = MathHelper.Clamp(chainAge / (float)lastDelay, 0f, 0.999f);
                float scaled = headFrac * (glyphCount - 1);
                int seg = (int)scaled;
                Vector2 flowPos = Vector2.Lerp(glyphs[seg].Pos, glyphs[seg + 1].Pos, scaled - seg)
                    + Main.rand.NextVector2Circular(9f, 9f);
                Vector2 flowDir = glyphs[seg + 1].Pos - glyphs[seg].Pos;
                if (flowDir.LengthSquared() > 1f) {
                    flowDir.Normalize();
                }
                Dust dust = Dust.NewDustPerfect(flowPos, DustID.GoldFlame,
                    flowDir * Main.rand.NextFloat(0.5f, 1.1f), 90, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
        }

        /// <summary>符纹点燃帧：金尘一簇 + 石屑两粒；每第三枚补一记轻微石磬</summary>
        private static void IgniteGlyph(int i) {
            Vector2 pos = glyphs[i].Pos;
            for (int k = 0; k < 4; k++) {
                Dust dust = Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(6f, 6f),
                    DustID.GoldFlame, Main.rand.NextVector2Circular(0.8f, 0.8f),
                    80, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = true;
            }
            for (int k = 0; k < 2; k++) {
                Dust.NewDustPerfect(pos + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.Lihzahrd, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), 0.6f),
                    60, default, 0.9f);
            }
            if (i % 3 == 0) {
                SoundEngine.PlaySound(SoundID.Item4 with {
                    Volume = 0.14f,
                    Pitch = 0.35f + i * 0.03f,
                    MaxInstances = 2
                }, pos);
            }
        }

        /// <summary>
        /// 建链：先在玩家四周找一块裸露神庙墙作锚点，再沿随机方向逐段跟墙，
        /// 每段允许上下 2 格内追随墙面起伏，断墙即止。不足 3 枚放弃
        /// </summary>
        private static bool TryBuildChain() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                return false;
            }

            Point anchor = default;
            bool found = false;
            Point center = player.Center.ToTileCoordinates();
            for (int attempt = 0; attempt < 24 && !found; attempt++) {
                int x = center.X + Main.rand.Next(-10, 11);
                int y = center.Y + Main.rand.Next(-6, 7);
                if (IsBareTempleWall(x, y)) {
                    anchor = new Point(x, y);
                    found = true;
                }
            }
            if (!found) {
                return false;
            }

            int dir = Main.rand.NextBool() ? 1 : -1;
            int wy = anchor.Y;
            int wx = anchor.X;
            glyphs[0] = new Glyph {
                Pos = anchor.ToWorldCoordinates(),
                Delay = 0,
                Seed = Main.rand.NextFloat()
            };
            int count = 1;
            //探测序 0,-1,+1,-2,+2：优先水平延续，允许跟随墙面起伏
            ReadOnlySpan<int> probeOrder = [0, -1, 1, -2, 2];
            for (int k = 1; k < MaxGlyphs; k++) {
                wx += dir * (3 + (k & 1));
                bool stepped = false;
                for (int probe = 0; probe < probeOrder.Length && !stepped; probe++) {
                    int dy = probeOrder[probe];
                    if (IsBareTempleWall(wx, wy + dy)) {
                        wy += dy;
                        glyphs[count] = new Glyph {
                            Pos = new Point(wx, wy).ToWorldCoordinates(),
                            Delay = count * IgniteGap,
                            Seed = Main.rand.NextFloat()
                        };
                        count++;
                        stepped = true;
                    }
                }
                if (!stepped) {
                    break;
                }
            }
            if (count < MinGlyphs) {
                return false;
            }

            glyphCount = count;
            chainAge = 0;
            //古老石声：链首低沉滚石，苏醒的开场白
            SoundEngine.PlaySound(SoundID.WormDig with {
                Volume = 0.34f,
                Pitch = -0.78f,
                MaxInstances = 2
            }, glyphs[0].Pos);
            return true;
        }

        /// <summary>裸露的神庙砖墙：有墙、无实心覆盖</summary>
        private static bool IsBareTempleWall(int x, int y) {
            if (!WorldGen.InWorld(x, y, 10)) {
                return false;
            }
            return Main.tile[x, y].WallType == WallID.LihzahrdBrickUnsafe && !WorldGen.SolidTile(x, y);
        }

        /// <summary>符纹寿命包络 0~1</summary>
        private static float Envelope(int local) {
            if (local < GlyphRise) {
                return local / (float)GlyphRise;
            }
            if (local < GlyphRise + GlyphHold) {
                return 1f;
            }
            return MathHelper.Clamp(1f - (local - GlyphRise - GlyphHold) / (float)GlyphFade, 0f, 1f);
        }

        //==== 绘制 ====

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu || glyphCount <= 0) {
                return;
            }
            float presence = MireheartAmbience.TemplePresence;
            if (presence <= 0.05f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D ring = DiffusionCircle4?.Value;
            if (glow == null || star == null) {
                return;
            }

            float dim = presence * (CWRWorld.HasBoss ? 0.5f : 1f);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Vector2 glowOrigin = glow.Size() * 0.5f;
            Vector2 starOrigin = star.Size() * 0.5f;
            for (int i = 0; i < glyphCount; i++) {
                int local = chainAge - glyphs[i].Delay;
                if (local <= 0 || local >= GlyphLife) {
                    continue;
                }
                float env = Envelope(local);
                //慢呼吸：去同相靠逐符 Seed，不齐闪
                float breath = 0.9f + 0.1f * MathF.Sin(
                    Main.GlobalTimeWrappedHourly * 3f + glyphs[i].Seed * 12f);
                float a = env * breath * dim;
                if (a <= 0.01f) {
                    continue;
                }
                Vector2 pos = glyphs[i].Pos - Main.screenPosition;
                float wobble = (glyphs[i].Seed - 0.5f) * 0.24f;

                //暖晕衬底（加色批 A 随强度走）
                spriteBatch.Draw(glow, pos, null, new Color(255, 186, 84) * (0.30f * a),
                    0f, glowOrigin, 0.62f, SpriteEffects.None, 0f);
                //太阳符主星（四芒）+ 斜四芒副星，组成八向日纹
                spriteBatch.Draw(star, pos, null, new Color(255, 226, 150) * (0.8f * a),
                    wobble, starOrigin, 0.13f, SpriteEffects.None, 0f);
                spriteBatch.Draw(star, pos, null, new Color(255, 200, 96) * (0.45f * a),
                    wobble + MathHelper.PiOver4, starOrigin, 0.085f, SpriteEffects.None, 0f);
                //日轮细环
                if (ring != null) {
                    spriteBatch.Draw(ring, pos, null, new Color(240, 160, 56) * (0.42f * a),
                        0f, ring.Size() * 0.5f, 0.26f, SpriteEffects.None, 0f);
                }
            }
            spriteBatch.End();
        }
    }
}
