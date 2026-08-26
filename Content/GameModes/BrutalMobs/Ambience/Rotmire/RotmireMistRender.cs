using CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rotmire
{
    /// <summary>
    /// 「腐澜」屏幕层：紫瘴薄雾（前后双层视差雾片，Fog 真 alpha 承担暗层）
    /// 与上浮孢光点（A=0 加色敷料）。挂 EndEntityDraw，雾盖在实体之上（人走进瘴气里）；
    /// 自开自收 AlphaBlend 批，无 RT 槽。配色只读引用邪地风味表保持家族一致
    /// </summary>
    internal sealed class RotmireMistRender : RenderHandle
    {
        /// <summary>权重 1.65（本槽位分配值）</summary>
        public override float Weight => 1.65f;

        private const int PatchCount = 12;
        private const int MoteCount = 9;
        /// <summary>屏外回收余量（px）</summary>
        private const float Margin = 420f;

        private struct Patch
        {
            internal bool Active;
            internal Vector2 Pos;
            internal Vector2 Drift;
            internal float SizePx;
            internal float Rot;
            internal float RotVel;
            internal int Life;
            internal int MaxLife;
            internal bool Back;
        }

        private struct Mote
        {
            internal bool Active;
            internal Vector2 Pos;
            internal float RiseSpeed;
            internal float SwayPhase;
            internal int Life;
            internal int MaxLife;
            internal float Scale;
        }

        private static readonly Patch[] patches = new Patch[PatchCount];
        private static readonly Mote[] motes = new Mote[MoteCount];

        //暗层雾色（真 alpha 才能压暗）与孢光亮色（A=0 只加光）
        private static readonly Color VeilDeep =
            Color.Lerp(EvilBiomeFX.Deep(EvilBiomeFX.FlavorCorrupt), Color.Black, 0.35f);
        private static readonly Color SporeBright = EvilBiomeFX.Bright(EvilBiomeFX.FlavorCorrupt);

        //==================== 逻辑更新 ====================

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = RotmireAmbience.Presence;
            if (presence < 0.02f) {
                for (int i = 0; i < patches.Length; i++) {
                    patches[i].Active = false;
                }
                for (int i = 0; i < motes.Length; i++) {
                    motes[i].Active = false;
                }
                return;
            }

            float wind = Main.windSpeedCurrent;
            for (int i = 0; i < patches.Length; i++) {
                if (!patches[i].Active) {
                    continue;
                }
                patches[i].Pos += patches[i].Drift
                    + new Vector2(wind * (patches[i].Back ? 0.35f : 0.6f), 0f);
                patches[i].Rot += patches[i].RotVel;
                patches[i].Life++;
                if (patches[i].Life >= patches[i].MaxLife || OutOfView(patches[i].Pos)) {
                    patches[i].Active = false;
                }
            }
            for (int i = 0; i < motes.Length; i++) {
                if (!motes[i].Active) {
                    continue;
                }
                motes[i].Pos.Y -= motes[i].RiseSpeed;
                motes[i].Pos.X += MathF.Sin((motes[i].Life + motes[i].SwayPhase) * 0.05f) * 0.4f + wind * 0.8f;
                motes[i].Life++;
                if (motes[i].Life >= motes[i].MaxLife || OutOfView(motes[i].Pos)) {
                    motes[i].Active = false;
                }
            }

            //每帧最多补一片/一点，错帧铺开避免整屏同拍
            SpawnOnePatch();
            SpawnOneMote();
        }

        private static bool OutOfView(Vector2 worldPos) {
            return worldPos.X < Main.screenPosition.X - Margin - 200f
                || worldPos.X > Main.screenPosition.X + Main.screenWidth + Margin + 200f
                || worldPos.Y < Main.screenPosition.Y - Margin - 200f
                || worldPos.Y > Main.screenPosition.Y + Main.screenHeight + Margin + 200f;
        }

        private static Vector2 RandomViewPos() {
            return Main.screenPosition + new Vector2(
                Main.rand.NextFloat(-Margin, Main.screenWidth + Margin),
                Main.rand.NextFloat(-Margin * 0.4f, Main.screenHeight + Margin * 0.6f));
        }

        private static void SpawnOnePatch() {
            for (int i = 0; i < patches.Length; i++) {
                if (patches[i].Active) {
                    continue;
                }
                bool back = i % 2 == 0;
                patches[i] = new Patch {
                    Active = true,
                    Pos = RandomViewPos(),
                    Drift = new Vector2(Main.rand.NextFloat(-0.22f, 0.22f), Main.rand.NextFloat(-0.1f, 0.05f)),
                    SizePx = back ? Main.rand.NextFloat(420f, 700f) : Main.rand.NextFloat(260f, 420f),
                    Rot = Main.rand.NextFloat(MathHelper.TwoPi),
                    RotVel = Main.rand.NextFloat(-0.0022f, 0.0022f),
                    Life = 0,
                    MaxLife = Main.rand.Next(320, 540),
                    Back = back,
                };
                return;
            }
        }

        private static void SpawnOneMote() {
            for (int i = 0; i < motes.Length; i++) {
                if (motes[i].Active) {
                    continue;
                }
                motes[i] = new Mote {
                    Active = true,
                    Pos = Main.screenPosition + new Vector2(
                        Main.rand.NextFloat(-40f, Main.screenWidth + 40f),
                        Main.rand.NextFloat(Main.screenHeight * 0.35f, Main.screenHeight + 120f)),
                    RiseSpeed = Main.rand.NextFloat(0.5f, 1.1f),
                    SwayPhase = Main.rand.NextFloat(120f),
                    Life = 0,
                    MaxLife = Main.rand.Next(200, 340),
                    Scale = Main.rand.NextFloat(0.09f, 0.16f),
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
            float presence = RotmireAmbience.Presence;
            if (presence < 0.02f) {
                return;
            }
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null || fog.IsDisposed) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;

            float dim = RotmireAmbience.BossDim;
            float depth = RotmireAmbience.DepthGrade;
            //地下深谷雾更沉
            float backAlpha = (0.14f + 0.08f * depth) * presence * dim;
            float frontAlpha = (0.09f + 0.05f * depth) * presence * dim;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            Vector2 fogOrigin = fog.Size() * 0.5f;
            //后层雾
            DrawPatchLayer(spriteBatch, fog, fogOrigin, backAlpha, back: true);
            //孢光点夹在两层雾之间（A=0 加色）
            if (glow != null && !glow.IsDisposed) {
                Vector2 glowOrigin = glow.Size() * 0.5f;
                for (int i = 0; i < motes.Length; i++) {
                    if (!motes[i].Active) {
                        continue;
                    }
                    float t = motes[i].Life / (float)motes[i].MaxLife;
                    float env = Math.Min(t / 0.18f, 1f) * MathHelper.Clamp((1f - t) / 0.3f, 0f, 1f);
                    float flicker = 0.8f + 0.2f * MathF.Sin(motes[i].Life * 0.11f + motes[i].SwayPhase);
                    Color tint = new Color(SporeBright.R, SporeBright.G, SporeBright.B, (byte)0)
                        * (0.5f * env * flicker * presence * dim);
                    spriteBatch.Draw(glow, motes[i].Pos - Main.screenPosition, null, tint, 0f,
                        glowOrigin, motes[i].Scale, SpriteEffects.None, 0f);
                }
            }
            //前层雾
            DrawPatchLayer(spriteBatch, fog, fogOrigin, frontAlpha, back: false);

            spriteBatch.End();
        }

        private static void DrawPatchLayer(SpriteBatch sb, Texture2D fog, Vector2 origin, float alpha, bool back) {
            if (alpha < 0.004f) {
                return;
            }
            for (int i = 0; i < patches.Length; i++) {
                if (!patches[i].Active || patches[i].Back != back) {
                    continue;
                }
                float t = patches[i].Life / (float)patches[i].MaxLife;
                float env = Math.Min(t / 0.15f, 1f) * MathHelper.Clamp((1f - t) / 0.25f, 0f, 1f);
                float scale = patches[i].SizePx / fog.Width;
                sb.Draw(fog, patches[i].Pos - Main.screenPosition, null,
                    VeilDeep * (alpha * env), patches[i].Rot, origin, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
