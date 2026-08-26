using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Lumindepth
{
    /// <summary>
    /// 「澄光」屏幕层描画：洞壁摇曳的蓝白水光斑、缓漂缓沉的荧光水母光点群、
    /// 晶铃共鸣的光纹涟漪。自开自收加色批，无 RT 槽；
    /// 亮度统一吃在场强度、「荧潮」呼吸与 Boss 让位系数
    /// </summary>
    internal sealed class LumindepthAmbientRender : RenderHandle
    {
        /// <summary>槽位分配权重 1.82</summary>
        public override float Weight => 1.82f;

        //DiffusionCircle4：0.95R 薄锐缘扩散环（黑底亮度型，只进加色批），涟漪专用
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> DiffusionCircle4 = null;
        //Sparkle：小闪点（黑底亮度型），水光斑的偶发高光粒
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> Sparkle = null;

        private const int MaxDapples = 12;
        private const int MaxMotes = 14;

        /// <summary>贴壁水光斑：焦散光在洞壁上的摇曳斑块</summary>
        private struct Dapple
        {
            internal bool Active;
            internal Vector2 Pos;
            internal int Life;
            internal int MaxLife;
            internal float Phase;
            internal float Scale;
            internal float Tilt;
            internal int SrcY;
        }

        /// <summary>荧光水母光点：收伞脉冲上顶、随后缓沉的漂浮微光</summary>
        private struct Mote
        {
            internal bool Active;
            internal Vector2 Pos;
            internal Vector2 Vel;
            internal int Life;
            internal int MaxLife;
            internal int PulsePeriod;
            internal float PulseT;
            internal float Size;
            internal float SwayPhase;
            internal float HueMix;
        }

        private static readonly Dapple[] dapples = new Dapple[MaxDapples];
        private static readonly Mote[] motes = new Mote[MaxMotes];
        private static int dappleSpawnIn;
        private static int moteSpawnIn;

        //==================== 逻辑更新 ====================

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = LumindepthAmbience.Presence;
            if (presence < 0.02f) {
                for (int i = 0; i < dapples.Length; i++) {
                    dapples[i].Active = false;
                }
                for (int i = 0; i < motes.Length; i++) {
                    motes[i].Active = false;
                }
                return;
            }
            UpdateDapples(presence);
            UpdateMotes(presence);
        }

        private static void UpdateDapples(float presence) {
            for (int i = 0; i < dapples.Length; i++) {
                if (!dapples[i].Active) {
                    continue;
                }
                dapples[i].Life++;
                if (dapples[i].Life >= dapples[i].MaxLife || OffScreen(dapples[i].Pos, 420f)) {
                    dapples[i].Active = false;
                }
            }
            if (--dappleSpawnIn > 0 || presence < 0.25f) {
                return;
            }
            dappleSpawnIn = 10;
            //随机屏内探针：实心瓦且四邻沾水才配得上一枚焦散光斑
            for (int probe = 0; probe < 5; probe++) {
                int x = (int)((Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth)) / 16f);
                int y = (int)((Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight)) / 16f);
                if (!WorldGen.InWorld(x, y, 10) || !WorldGen.SolidTile(x, y)) {
                    continue;
                }
                bool nearWater = false;
                for (int k = 0; k < 4; k++) {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0);
                    int ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    Tile neighbor = Framing.GetTileSafely(nx, ny);
                    if (neighbor.LiquidAmount > 100 && neighbor.LiquidType == Terraria.ID.LiquidID.Water) {
                        nearWater = true;
                        break;
                    }
                }
                if (!nearWater) {
                    continue;
                }
                for (int i = 0; i < dapples.Length; i++) {
                    if (dapples[i].Active) {
                        continue;
                    }
                    dapples[i] = new Dapple {
                        Active = true,
                        Pos = new Vector2(x * 16f + 8f, y * 16f + 8f),
                        Life = 0,
                        MaxLife = Main.rand.Next(360, 560),
                        Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                        Scale = Main.rand.NextFloat(0.55f, 1f),
                        Tilt = Main.rand.NextFloat(-0.45f, 0.45f),
                        SrcY = Main.rand.Next(0, 180),
                    };
                    return;
                }
                return;
            }
        }

        private static void UpdateMotes(float presence) {
            for (int i = 0; i < motes.Length; i++) {
                if (!motes[i].Active) {
                    continue;
                }
                motes[i].Life++;
                motes[i].SwayPhase += 0.013f;
                motes[i].Vel.X = MathF.Sin(motes[i].SwayPhase) * 0.07f;
                //收伞脉冲上顶，随后水阻消化冲量、缓沉接管
                if (motes[i].Life % motes[i].PulsePeriod == 0) {
                    motes[i].PulseT = 1f;
                    motes[i].Vel.Y -= 0.42f;
                }
                motes[i].PulseT *= 0.94f;
                motes[i].Vel.Y = (motes[i].Vel.Y + 0.003f) * 0.965f;
                motes[i].Pos += motes[i].Vel;
                //漂离水体就提前谢幕（截短寿命走渐隐，不硬切）
                if (motes[i].Life % 12 == 0 && !LumindepthPlayer.IsOpenWater(motes[i].Pos)) {
                    motes[i].MaxLife = Math.Min(motes[i].MaxLife, motes[i].Life + 40);
                }
                if (motes[i].Life >= motes[i].MaxLife || OffScreen(motes[i].Pos, 320f)) {
                    motes[i].Active = false;
                }
            }
            if (--moteSpawnIn > 0 || presence < 0.3f) {
                return;
            }
            moteSpawnIn = 18;
            Vector2 candidate = new(
                Main.screenPosition.X + Main.rand.NextFloat(-60f, Main.screenWidth + 60f),
                Main.screenPosition.Y + Main.rand.NextFloat(-60f, Main.screenHeight + 60f));
            if (!LumindepthPlayer.IsOpenWater(candidate)) {
                return;
            }
            for (int i = 0; i < motes.Length; i++) {
                if (motes[i].Active) {
                    continue;
                }
                motes[i] = new Mote {
                    Active = true,
                    Pos = candidate,
                    Vel = Vector2.Zero,
                    Life = 0,
                    MaxLife = Main.rand.Next(500, 820),
                    PulsePeriod = Main.rand.Next(100, 150),
                    PulseT = 0f,
                    Size = Main.rand.NextFloat(0.5f, 0.9f),
                    SwayPhase = Main.rand.NextFloat(MathHelper.TwoPi),
                    HueMix = Main.rand.NextFloat(),
                };
                return;
            }
        }

        private static bool OffScreen(Vector2 worldPos, float margin) {
            return worldPos.X < Main.screenPosition.X - margin
                || worldPos.X > Main.screenPosition.X + Main.screenWidth + margin
                || worldPos.Y < Main.screenPosition.Y - margin
                || worldPos.Y > Main.screenPosition.Y + Main.screenHeight + margin;
        }

        //==================== 绘制 ====================

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float presence = LumindepthAmbience.Presence;
            if (presence < 0.02f) {
                return;
            }
            bool anyDapple = false;
            for (int i = 0; i < dapples.Length; i++) {
                if (dapples[i].Active) {
                    anyDapple = true;
                    break;
                }
            }
            bool anyMote = false;
            for (int i = 0; i < motes.Length; i++) {
                if (motes[i].Active) {
                    anyMote = true;
                    break;
                }
            }
            bool anyRipple = false;
            var ripples = LumindepthCrystalChime.Ripples;
            for (int i = 0; i < ripples.Length; i++) {
                if (ripples[i].Active) {
                    anyRipple = true;
                    break;
                }
            }
            if (!anyDapple && !anyMote && !anyRipple) {
                return;
            }

            //荧潮呼吸与 Boss 让位统一在此收口
            float glowK = presence * (0.55f + 0.45f * LumindepthAmbience.Tide) * LumindepthAmbience.BossDim;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            if (anyDapple) {
                DrawDapples(spriteBatch, glowK);
            }
            if (anyMote) {
                DrawMotes(spriteBatch, glowK);
            }
            if (anyRipple) {
                DrawRipples(spriteBatch, presence * LumindepthAmbience.BossDim);
            }
            spriteBatch.End();
        }

        //水光斑：柔光垫底 + 两片错动流光纹（源区滚动出焦散感）+ 偶发闪点
        private static void DrawDapples(SpriteBatch sb, float glowK) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D flow = CWRAsset.Airflow?.Value;
            if (glow == null || flow == null) {
                return;
            }
            Texture2D sparkle = Sparkle?.Value;
            Color caustic = new(150, 220, 255);
            float time = (float)Main.timeForVisualEffects * 0.016f;

            for (int i = 0; i < dapples.Length; i++) {
                if (!dapples[i].Active) {
                    continue;
                }
                float t = dapples[i].Life / (float)dapples[i].MaxLife;
                float env = Math.Min(t / 0.18f, 1f) * MathHelper.Clamp((1f - t) / 0.3f, 0f, 1f);
                float alpha = 0.11f * env * glowK;
                if (alpha < 0.004f) {
                    continue;
                }
                //摇曳：位置轻摆 + 亮度微闪
                float sway = MathF.Sin(time * 0.9f + dapples[i].Phase);
                float shimmer = 0.8f + 0.2f * MathF.Sin(time * 2.6f + dapples[i].Phase * 2f);
                Vector2 pos = dapples[i].Pos + new Vector2(sway * 3f, MathF.Cos(time * 0.7f + dapples[i].Phase) * 2f)
                    - Main.screenPosition;
                float rot = dapples[i].Tilt + sway * 0.06f;

                //垫底柔光（占比压低，不当本体）
                sb.Draw(glow, pos, null, caustic * (alpha * 0.55f * shimmer), rot,
                    glow.Size() / 2f, dapples[i].Scale * 1.5f, SpriteEffects.None, 0f);
                //两片流光纹相对漂移，读作贴壁焦散
                int scroll1 = (int)(time * 26f + dapples[i].Phase * 40f) % 160;
                int scroll2 = (int)(time * 17f + dapples[i].Phase * 25f) % 160;
                var src1 = new Rectangle(scroll1, dapples[i].SrcY, 96, 44);
                var src2 = new Rectangle(160 - scroll2, (dapples[i].SrcY + 90) % 200, 96, 44);
                sb.Draw(flow, pos, src1, caustic * (alpha * shimmer), rot,
                    new Vector2(48f, 22f), new Vector2(dapples[i].Scale * 0.95f, dapples[i].Scale * 0.6f),
                    SpriteEffects.None, 0f);
                sb.Draw(flow, pos, src2, caustic * (alpha * 0.8f), rot + 0.18f,
                    new Vector2(48f, 22f), new Vector2(dapples[i].Scale * 0.8f, dapples[i].Scale * 0.5f),
                    SpriteEffects.FlipHorizontally, 0f);
                //偶发高光粒：闪点掠过水光斑
                if (sparkle != null) {
                    float blink = MathF.Sin(time * 3.1f + dapples[i].Phase * 3f);
                    if (blink > 0.62f) {
                        float glint = (blink - 0.62f) / 0.38f;
                        sb.Draw(sparkle, pos + new Vector2(sway * 8f, -4f), null,
                            new Color(200, 240, 255) * (0.4f * glint * env * glowK), 0f,
                            sparkle.Size() / 2f, 0.5f * dapples[i].Scale, SpriteEffects.None, 0f);
                    }
                }
            }
        }

        //水母光点：柔光伞体 + 星芒核，收伞脉冲时一并提亮放大
        private static void DrawMotes(SpriteBatch sb, float glowK) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D core = CWRAsset.StarGlow01?.Value;
            if (glow == null || core == null) {
                return;
            }
            Color teal = new(120, 225, 240);
            Color violet = new(170, 190, 255);

            for (int i = 0; i < motes.Length; i++) {
                if (!motes[i].Active) {
                    continue;
                }
                float t = motes[i].Life / (float)motes[i].MaxLife;
                float env = Math.Min(t / 0.12f, 1f) * MathHelper.Clamp((1f - t) / 0.22f, 0f, 1f);
                float alpha = (0.42f + 0.5f * motes[i].PulseT) * env * glowK;
                if (alpha < 0.004f) {
                    continue;
                }
                Vector2 pos = motes[i].Pos - Main.screenPosition;
                float pop = motes[i].Size * (1f + 0.3f * motes[i].PulseT);
                Color hue = Color.Lerp(teal, violet, motes[i].HueMix);
                sb.Draw(glow, pos, null, hue * (alpha * 0.5f), 0f,
                    glow.Size() / 2f, pop * 0.55f, SpriteEffects.None, 0f);
                sb.Draw(core, pos, null, new Color(196, 240, 255) * alpha, 0f,
                    core.Size() / 2f, pop * 0.17f, SpriteEffects.None, 0f);
            }
        }

        //晶铃涟漪：薄锐缘扩散环双层错拍，自晶簇处荡开
        private static void DrawRipples(SpriteBatch sb, float k) {
            Texture2D ring = DiffusionCircle4?.Value;
            if (ring == null) {
                return;
            }
            var ripples = LumindepthCrystalChime.Ripples;
            //薄锐缘位于 0.95R，按可见半径折算缩放
            float edgeDiameter = ring.Width * 0.95f;
            Color glint = new(160, 232, 255);

            for (int i = 0; i < ripples.Length; i++) {
                if (!ripples[i].Active) {
                    continue;
                }
                float t = ripples[i].Life / (float)ripples[i].MaxLife;
                Vector2 pos = ripples[i].Pos - Main.screenPosition;
                //主环
                float ease = 1f - (1f - t) * (1f - t);
                float diameter = 24f + 150f * ease;
                float alpha = 0.45f * (1f - t) * k;
                sb.Draw(ring, pos, null, glint * alpha, 0f,
                    ring.Size() / 2f, diameter / edgeDiameter, SpriteEffects.None, 0f);
                //迟一拍的内环
                float t2 = t - 0.3f;
                if (t2 > 0f) {
                    float ease2 = 1f - (1f - t2 / 0.7f) * (1f - t2 / 0.7f);
                    float diameter2 = 16f + 96f * MathHelper.Clamp(ease2, 0f, 1f);
                    sb.Draw(ring, pos, null, glint * (alpha * 0.6f), 0f,
                        ring.Size() / 2f, diameter2 / edgeDiameter, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
