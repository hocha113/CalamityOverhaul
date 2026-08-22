using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Deaths.Performances
{
    /// <summary>
    /// 提灯童子夺身「灯中人」。<br/>
    /// 前兆：三盏鬼灯骤停环游、缓缓围拢；<br/>
    /// 显形：逐盏熄灭，黑暗一层层合拢，最后只剩玩家身上一点残光；<br/>
    /// 处决：黑暗中三道灯火斩以 0/2/4 帧错拍自三盏灯的方位穿身而过，暗幕随第三刀骤然揭开；<br/>
    /// 余韵：尸旁多点起第四盏灯，「再看已是灯中人」。<br/>
    /// 材质：灯笼红纸与暖焰（WraithLantern shader + SoftGlow），安静恐怖，不借雷血。
    /// </summary>
    internal sealed class LanternSeizurePerformance : WraithDeathPerformance
    {
        public override int OmenEndFrame => 44;
        public override int ExecuteFrame => 124;
        public override int TotalFrames => 196;

        private static readonly Color LanternPaper = new(185, 39, 24);
        private static readonly Color FlameHot = new(255, 122, 58);
        private static readonly Color AshSmoke = new(58, 50, 46);

        private const int LanternCount = 3;
        //每盏灯的熄灭进度 0..1
        private readonly float[] snuff = new float[LanternCount];
        private readonly bool[] snuffed = new bool[LanternCount];
        //三刀斩闪：出生帧（相对 ExecuteFrame 的 0/2/4）
        private readonly int[] slashBorn = [-1, -1, -1];
        private float orbitFreeze;
        private int snuffFlash;
        private float fourthIgnite;

        public override void OnBegin() {
            orbitFreeze = Seed * 0.13f;
            SoundEngine.PlaySound(SoundID.Item32 with {
                Pitch = -0.5f,
                Volume = 0.4f,
                MaxInstances = 1,
            }, Player.Center);
        }

        /// <summary>三盏灯的定格环位：前兆期从环游半径缓缓围拢。</summary>
        private Vector2 LanternPos(int slot) {
            Vector2 anchor = Player.dead ? DeathAnchor : Player.Center;
            float angle = orbitFreeze + slot * MathHelper.TwoPi / LanternCount
                + MathF.Sin(Timer * 0.01f + slot) * 0.02f;
            float radius = Phase == WraithSeizePhase.Omen
                ? MathHelper.Lerp(84f, 60f, PhaseProgress)
                : 60f;
            float bob = MathF.Sin(Timer * 0.05f + slot * 2.1f) * 3f;
            return anchor + angle.ToRotationVector2() * radius + new Vector2(0f, bob - 12f);
        }

        /// <summary>暗幕浓度：随熄灯层层压下，处决第三刀后骤然揭开。</summary>
        private float Darkness {
            get {
                if (Phase == WraithSeizePhase.Linger) {
                    int sinceExecute = Timer - ExecuteFrame;
                    if (sinceExecute <= 6) {
                        return 0.78f;
                    }
                    return MathHelper.Clamp(0.78f - (sinceExecute - 6) / 5f * 0.78f, 0f, 1f);
                }
                float total = 0f;
                for (int i = 0; i < LanternCount; i++) {
                    total += snuff[i];
                }
                return MathHelper.Clamp(total * 0.26f, 0f, 0.78f);
            }
        }

        public override void Update() {
            if (snuffFlash > 0) {
                snuffFlash--;
            }

            switch (Phase) {
                case WraithSeizePhase.Manifest:
                    //三拍熄灯：0.15 / 0.42 / 0.68
                    TrySnuff(0, 0.15f);
                    TrySnuff(1, 0.42f);
                    TrySnuff(2, 0.68f);
                    for (int i = 0; i < LanternCount; i++) {
                        if (snuffed[i] && snuff[i] < 1f) {
                            snuff[i] = MathHelper.Clamp(snuff[i] + 1f / 9f, 0f, 1f);
                        }
                    }
                    break;

                case WraithSeizePhase.Linger:
                    //暗中三闪的错拍音（第一刀由 OnExecute 播）
                    if (Timer == ExecuteFrame + 2 || Timer == ExecuteFrame + 4) {
                        int index = Timer == ExecuteFrame + 2 ? 1 : 2;
                        slashBorn[index] = Timer;
                        SoundEngine.PlaySound(SoundID.Item71 with {
                            Pitch = -0.2f + index * 0.14f,
                            Volume = 0.62f,
                            MaxInstances = 3,
                        }, DeathAnchor);
                    }
                    //第四盏灯：暗幕揭开后在尸旁点起
                    if (Timer > ExecuteFrame + 12) {
                        if (fourthIgnite == 0f) {
                            SoundEngine.PlaySound(SoundID.Item32 with {
                                Pitch = 0.15f,
                                Volume = 0.42f,
                                MaxInstances = 1,
                            }, FourthLanternPos);
                        }
                        fourthIgnite = MathHelper.Clamp(fourthIgnite + 1f / 34f, 0f, 1f);
                        if (Timer % 9 == 0) {
                            PRTLoader.NewParticle<PRT_PallbearerEmber>(
                                FourthLanternPos + Main.rand.NextVector2Circular(5f, 8f),
                                -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f),
                                FlameHot, Main.rand.NextFloat(0.3f, 0.55f))
                                ?.Configure(Main.rand.Next(16, 26), 0.03f);
                        }
                    }
                    break;
            }
        }

        private void TrySnuff(int slot, float threshold) {
            if (snuffed[slot] || PhaseProgress < threshold) {
                return;
            }
            snuffed[slot] = true;
            snuffFlash = 9;
            Vector2 pos = LanternPos(slot);
            SoundEngine.PlaySound(SoundID.Item32 with {
                Pitch = 0.35f - slot * 0.18f,
                Volume = 0.5f,
                MaxInstances = 2,
            }, pos);
            //灯灭起一缕纸灰烟
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(pos + Main.rand.NextVector2Circular(6f, 10f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.2f),
                    AshSmoke, Main.rand.NextFloat(0.07f, 0.12f))
                    ?.Configure(Main.rand.Next(24, 40), 0.4f, Main.rand.NextFloat(-0.02f, 0.02f));
            }
            PRTLoader.NewParticle<PRT_PallbearerEmber>(pos,
                -Vector2.UnitY * Main.rand.NextFloat(0.4f, 0.9f),
                FlameHot, Main.rand.NextFloat(0.35f, 0.6f))
                ?.Configure(Main.rand.Next(12, 20), 0.04f);
        }

        public override void OnExecute() {
            slashBorn[0] = Timer + 1;
            SoundEngine.PlaySound(SoundID.Item71 with {
                Pitch = -0.34f,
                Volume = 0.7f,
                MaxInstances = 3,
            }, Player.Center);
            //三方向火星迸出
            for (int i = 0; i < 9; i++) {
                PRTLoader.NewParticle<PRT_PallbearerEmber>(
                    Player.Center + Main.rand.NextVector2Circular(10f, 14f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.8f, 4.6f),
                    Main.rand.NextBool() ? FlameHot : LanternPaper,
                    Main.rand.NextFloat(0.4f, 0.75f))
                    ?.Configure(Main.rand.Next(14, 24), 0.05f);
            }
        }

        public override void Draw(SpriteBatch sb) {
            Vector2 anchor = Player.dead ? DeathAnchor : Player.Center;

            //---- 灯体需要自己的批次（WraithLantern shader）----
            sb.End();
            DrawLanternGlows(sb);
            DrawLanternBodies(sb);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            //---- 暗幕：覆盖全屏，压过灯体之外的一切 ----
            float darkness = Darkness;
            if (darkness > 0.01f) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                Rectangle screenRect = new(
                    (int)Main.screenPosition.X - 240, (int)Main.screenPosition.Y - 240,
                    Main.screenWidth + 480, Main.screenHeight + 480);
                screenRect.Offset(-(int)Main.screenPosition.X, -(int)Main.screenPosition.Y);
                sb.Draw(pixel, screenRect, new Rectangle(0, 0, 1, 1), Color.Black * darkness);

                //黑暗合拢时玩家身上仅存的一点残光，越收越小
                if (Phase == WraithSeizePhase.Manifest && CWRAsset.SoftGlow?.Value is Texture2D spot) {
                    float shrink = MathHelper.Clamp((PhaseProgress - 0.6f) / 0.4f, 0f, 1f);
                    float radius = MathHelper.Lerp(130f, 46f, shrink);
                    float flick = 0.8f + 0.2f * MathF.Sin(Timer * 0.33f);
                    sb.Draw(spot, anchor - Main.screenPosition, null,
                        new Color(0.72f, 0.14f, 0.03f, 0f) * (darkness * 0.75f * flick), 0f,
                        spot.Size() * 0.5f, radius * 2f / spot.Width, SpriteEffects.None, 0f);
                }
            }

            //---- 暗中三闪：自三盏灯的方位穿身 ----
            for (int i = 0; i < slashBorn.Length; i++) {
                if (slashBorn[i] < 0) {
                    continue;
                }
                int age = Timer - slashBorn[i];
                if (age is < 0 or > 13) {
                    continue;
                }
                DrawExecutionSlash(sb, anchor, i, age);
            }
        }

        private void DrawExecutionSlash(SpriteBatch sb, Vector2 anchor, int index, int age) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            //刀向：从对应灯的定格方位穿过玩家
            float angle = (LanternPos(index) - anchor).ToRotation() + MathHelper.Pi;
            Vector2 dir = angle.ToRotationVector2();
            //1 帧过冲，其后收敛
            float overshoot = age == 0 ? 1.12f : 1f;
            float fade = 1f - MathHelper.Clamp((age - 2) / 11f, 0f, 1f);
            float length = 210f * overshoot;
            Vector2 start = anchor - dir * (length * 0.5f) - Main.screenPosition;
            sb.Draw(pixel, start, src, LanternPaper * (0.85f * fade), angle,
                new Vector2(0f, 0.5f), new Vector2(length, 10f * fade + 2f), SpriteEffects.None, 0f);
            sb.Draw(pixel, start, src, FlameHot * fade, angle,
                new Vector2(0f, 0.5f), new Vector2(length, 3.4f * fade + 0.8f), SpriteEffects.None, 0f);
        }

        private Vector2 FourthLanternPos => DeathAnchor + new Vector2(34f, 16f);

        /// <summary>灯体：三盏定格灯 + 余韵里的第四盏，走 WraithLantern shader。</summary>
        private void DrawLanternBodies(SpriteBatch sb) {
            Texture2D white = VaultAsset.placeholder2?.Value ?? Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            Effect effect = EffectLoader.WraithLantern?.Value;
            if (effect == null || noise == null) {
                return;
            }
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect,
                Main.GameViewMatrix.TransformationMatrix);
            for (int slot = 0; slot < LanternCount; slot++) {
                float opacity = 1f - snuff[slot];
                if (opacity <= 0.01f) {
                    continue;
                }
                DrawOneLantern(sb, effect, noise, white, LanternPos(slot), opacity,
                    1f, snuff[slot], slot * 0.311f, 1f);
            }
            if (fourthIgnite > 0.01f) {
                DrawOneLantern(sb, effect, noise, white, FourthLanternPos, fourthIgnite,
                    fourthIgnite, 0f, 0.77f, 0.8f);
            }
            sb.End();
        }

        private void DrawOneLantern(SpriteBatch sb, Effect effect, Texture2D noise,
            Texture2D white, Vector2 pos, float opacity, float ignition, float extinguish,
            float seedOffset, float scale) {
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uOpacity"]?.SetValue(opacity);
            effect.Parameters["uIgnition"]?.SetValue(ignition);
            effect.Parameters["uExtinguish"]?.SetValue(extinguish);
            effect.Parameters["uPulse"]?.SetValue(snuffFlash > 0 ? snuffFlash / 9f * 0.5f : 0f);
            effect.Parameters["uSeed"]?.SetValue((Seed * 0.173f + seedOffset) % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            effect.CurrentTechnique.Passes[0].Apply();
            float rotation = MathF.Sin(Timer * 0.045f + seedOffset * 7f) * 0.045f;
            Vector2 drawScale = new(46f * scale / white.Width, 62f * scale / white.Height);
            sb.Draw(white, pos - Main.screenPosition, null, LanternPaper, rotation,
                white.Size() * 0.5f, drawScale, SpriteEffects.None, 0f);
        }

        private void DrawLanternGlows(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            for (int slot = 0; slot < LanternCount; slot++) {
                float opacity = 1f - snuff[slot];
                if (opacity <= 0.01f) {
                    continue;
                }
                sb.Draw(glow, LanternPos(slot) - Main.screenPosition, null,
                    new Color(0.75f, 0.10f, 0.025f, 0f) * (opacity * 0.24f), 0f,
                    glow.Size() * 0.5f, 72f / glow.Width, SpriteEffects.None, 0f);
            }
            if (fourthIgnite > 0.01f) {
                sb.Draw(glow, FourthLanternPos - Main.screenPosition, null,
                    new Color(0.75f, 0.10f, 0.025f, 0f) * (fourthIgnite * 0.28f), 0f,
                    glow.Size() * 0.5f, 62f / glow.Width, SpriteEffects.None, 0f);
            }
            sb.End();
        }

        public override Vector2 CameraFocus => Phase == WraithSeizePhase.Linger
            ? Vector2.Lerp(DeathAnchor, FourthLanternPos, MathHelper.Clamp(PhaseProgress * 1.4f, 0f, 0.6f))
            : Player?.Center ?? DeathAnchor;

        public override float CameraZoom => Phase switch {
            WraithSeizePhase.Omen => 1.15f,
            WraithSeizePhase.Manifest => MathHelper.Lerp(1.24f, 1.44f, PhaseProgress),
            WraithSeizePhase.Linger => 1.2f,
            _ => 1f,
        };

        //这一只走安静恐怖：几乎不震屏，只有熄灯与斩闪的短促一颤
        public override float ShakeIntensity => snuffFlash > 0 ? snuffFlash * 0.35f : 0f;
    }
}
