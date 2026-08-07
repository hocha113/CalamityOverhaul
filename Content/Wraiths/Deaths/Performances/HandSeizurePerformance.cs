using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Deaths.Performances
{
    /// <summary>
    /// 焦黑枯手夺身「五指之内」。<br/>
    /// 前兆：脚下焦土渗烟，五枚指尖破土；<br/>
    /// 显形：远大于役使时的巨掌自地底托起，五指自四周合拢，按 32%/68% 节拍两度碾紧；<br/>
    /// 处决：五指完全攥死成拳；余韵：拳头带着人缩回地底，只留一枚焦黑掌印。<br/>
    /// 材质：焦炭枯尸手——近实心暗体、龟裂缝透血烬（沿用枯手橙红烬 + 焦烟 + 暗血）。
    /// </summary>
    internal sealed class HandSeizurePerformance : WraithDeathPerformance
    {
        public override int OmenEndFrame => 42;
        public override int ExecuteFrame => 124;
        public override int TotalFrames => 190;

        private static readonly Color CharBody = new(24, 18, 15);
        private static readonly Color CharEdge = new(12, 9, 8);
        private static readonly Color EmberHot = new(212, 70, 26);
        private static readonly Color EmberDim = new(120, 38, 18);
        private static readonly Color DarkBlood = new(126, 16, 20);

        //五指横位（相对掌心），负=拇指侧
        private static readonly float[] FingerOffsets = [-64f, -30f, 4f, 36f, 66f];
        private static readonly float[] FingerLengths = [66f, 88f, 96f, 88f, 72f];

        //碾紧节拍：0.32 / 0.68 各一步，处决拳死
        private int crushStep;
        private int crushFlash;
        private float clampHeld;
        private Vector2 palmAnchor;
        private bool palmSet;

        public override void OnBegin() {
            SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                Pitch = -0.85f,
                Volume = 0.55f,
                MaxInstances = 1,
            }, Player.Center);
        }

        private Vector2 PalmCenter {
            get {
                if (!palmSet) {
                    return Player.Center + new Vector2(0f, 52f);
                }
                //显形期掌心从地底托到脚下，余韵拖着拳沉回去
                float lift = Phase switch {
                    WraithSeizePhase.Omen => 0f,
                    WraithSeizePhase.Manifest => MathHelper.Clamp(PhaseProgress / 0.3f, 0f, 1f),
                    _ => 1f,
                };
                float sink = Phase == WraithSeizePhase.Linger
                    ? VaultUtils.EaseOutCubic(MathHelper.Clamp((PhaseProgress - 0.1f) / 0.75f, 0f, 1f)) * 150f
                    : 0f;
                return palmAnchor + new Vector2(0f, MathHelper.Lerp(96f, 50f, lift) + sink);
            }
        }

        /// <summary>合拢量 0..1：相位基线 + 碾紧步进，处决后拳死。</summary>
        private float Clamp01 {
            get {
                if (Phase == WraithSeizePhase.Linger) {
                    return 1f;
                }
                float baseClamp = Phase == WraithSeizePhase.Manifest ? PhaseProgress * 0.5f : 0f;
                return MathHelper.Clamp(baseClamp + clampHeld
                    + (crushFlash > 0 ? crushFlash / 12f * 0.06f : 0f), 0f, 1f);
            }
        }

        public override void Update() {
            if (!palmSet) {
                palmAnchor = Player.Center;
                palmSet = true;
            }
            if (!Player.dead) {
                palmAnchor = Player.Center;
            }
            if (crushFlash > 0) {
                crushFlash--;
            }

            switch (Phase) {
                case WraithSeizePhase.Omen:
                    //破土：指尖处焦烟与烬
                    if (Timer == 12 || Timer == 30) {
                        SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                            Pitch = -0.9f + Timer * 0.004f,
                            Volume = 0.6f,
                            MaxInstances = 2,
                        }, Player.Center);
                    }
                    if (Timer % 3 == 0) {
                        float offset = FingerOffsets[Main.rand.Next(FingerOffsets.Length)];
                        SpawnCharSmoke(Player.Center + new Vector2(offset, 44f), 1);
                        if (Main.rand.NextBool(2)) {
                            SpawnEmber(Player.Center + new Vector2(offset, 40f), 1, 1.6f);
                        }
                    }
                    break;

                case WraithSeizePhase.Manifest:
                    if (Timer == OmenEndFrame + 1) {
                        SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                            Pitch = -0.6f,
                            Volume = 0.8f,
                            MaxInstances = 1,
                        }, Player.Center);
                        SoundEngine.PlaySound(SoundID.Item32 with {
                            Pitch = -0.7f,
                            Volume = 0.45f,
                            MaxInstances = 1,
                        }, Player.Center);
                        SpawnCharSmoke(PalmCenter, 10);
                        SpawnEmber(PalmCenter, 8, 3.4f);
                    }
                    //32% / 68%：它攥猎物的老节拍，这次落在你身上
                    if (PhaseProgress >= 0.32f && crushStep == 0) {
                        Crush(0.16f);
                    }
                    if (PhaseProgress >= 0.68f && crushStep == 1) {
                        Crush(0.2f);
                    }
                    if (Timer % 4 == 0) {
                        SpawnCharSmoke(PalmCenter + Main.rand.NextVector2Circular(60f, 10f), 1);
                    }
                    break;

                case WraithSeizePhase.Linger:
                    //拳头沉回地底，土层合拢
                    if (Timer % 4 == 0 && PhaseProgress < 0.8f) {
                        SpawnCharSmoke(PalmCenter + Main.rand.NextVector2Circular(50f, 8f), 2);
                    }
                    if (Timer % 7 == 0 && PhaseProgress < 0.6f) {
                        SpawnEmber(PalmCenter + Main.rand.NextVector2Circular(40f, 10f), 1, 2f);
                    }
                    break;
            }
        }

        private void Crush(float clampGain) {
            crushStep++;
            crushFlash = 12;
            clampHeld += clampGain;
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Pitch = -0.55f + crushStep * 0.1f,
                Volume = 0.85f,
                MaxInstances = 2,
            }, Player.Center);
            SpawnEmber(Player.Center, 7, 3f);
            //第二次碾紧起就见血
            if (crushStep >= 2) {
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                        Player.Center + Main.rand.NextVector2Circular(12f, 16f),
                        Main.rand.NextVector2Circular(3f, 2.4f) - Vector2.UnitY * 1.4f,
                        DarkBlood, Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(Main.rand.Next(16, 26), 0.3f);
                }
            }
        }

        public override void OnExecute() {
            //五指攥死
            clampHeld = 1f;
            crushFlash = 14;
            SoundEngine.PlaySound(SoundID.NPCDeath13 with {
                Pitch = -0.7f,
                Volume = 0.9f,
                MaxInstances = 1,
            }, Player.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Pitch = -0.35f,
                Volume = 1f,
                MaxInstances = 1,
            }, Player.Center);
            SpawnEmber(Player.Center, 14, 5f);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    Player.Center + Main.rand.NextVector2Circular(14f, 18f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6.5f),
                    new Color(132, 16, 22), Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(Main.rand.Next(18, 30), 0.32f);
            }
            SpawnCharSmoke(Player.Center, 8);
        }

        public override void Draw(SpriteBatch sb) {
            if (!palmSet) {
                return;
            }
            float sinkFade = Phase == WraithSeizePhase.Linger
                ? MathHelper.Clamp(1.15f - PhaseProgress, 0f, 1f) : 1f;

            //掌印余痕：拳头沉走后地上留焦黑手形
            if (Phase == WraithSeizePhase.Linger) {
                DrawScorchPrint(sb, MathHelper.Clamp(PhaseProgress * 1.4f, 0f, 1f));
            }

            if (Phase == WraithSeizePhase.Omen) {
                //破土的五枚指尖
                float poke = MathHelper.Clamp((Timer - 8f) / 30f, 0f, 1f);
                for (int i = 0; i < FingerOffsets.Length; i++) {
                    float h = FingerLengths[i] * 0.22f * poke
                        * (0.8f + 0.2f * MathF.Sin(Timer * 0.2f + i * 1.3f));
                    Vector2 tipBase = palmAnchor + new Vector2(FingerOffsets[i], 48f);
                    DrawCharSegment(sb, tipBase, tipBase - new Vector2(0f, h),
                        MathHelper.Lerp(9f, 13f, poke), 1f, i * 1.7f);
                }
                return;
            }

            //掌与五指
            Vector2 palm = PalmCenter;
            float clampAmount = Clamp01;
            DrawPalm(sb, palm, sinkFade);
            Vector2 targetPoint = (Player.dead ? DeathAnchor : Player.Center) - new Vector2(0f, 6f);
            for (int i = 0; i < FingerOffsets.Length; i++) {
                DrawFinger(sb, palm, i, clampAmount, targetPoint, sinkFade);
            }
        }

        private void DrawPalm(SpriteBatch sb, Vector2 palm, float fade) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            float crushBoost = crushFlash > 0 ? crushFlash / 14f * 0.3f : 0f;
            //掌身：焦炭厚板，两层压边
            sb.Draw(pixel, palm - Main.screenPosition, src, CharEdge * (0.9f * fade), 0f,
                new Vector2(0.5f), new Vector2(158f, 34f), SpriteEffects.None, 0f);
            sb.Draw(pixel, palm - Main.screenPosition, src, CharBody * (0.96f * fade), 0f,
                new Vector2(0.5f), new Vector2(148f, 27f), SpriteEffects.None, 0f);
            //龟裂缝：横贯掌面的两道烬线，随碾紧发亮
            float flick = 0.6f + 0.4f * MathF.Sin(Timer * 0.23f + Seed);
            for (int i = 0; i < 2; i++) {
                float y = i == 0 ? -5f : 6f;
                float crackAlpha = (0.35f + crushBoost) * flick * fade;
                sb.Draw(pixel, palm + new Vector2(i == 0 ? -18f : 24f, y) - Main.screenPosition,
                    src, EmberHot * crackAlpha, i == 0 ? 0.06f : -0.04f, new Vector2(0.5f),
                    new Vector2(74f, 1.6f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>一根手指：三节焦炭骨节自掌缘弓向合拢点，节间透烬。</summary>
        private void DrawFinger(SpriteBatch sb, Vector2 palm, int index, float clampAmount,
            Vector2 target, float fade) {
            float baseX = FingerOffsets[index];
            Vector2 root = palm + new Vector2(baseX, -10f);
            float length = FingerLengths[index];
            //张开姿态：指根朝外上方；合拢时逐节转向目标上方合拢点
            Vector2 openDir = new Vector2(baseX * 0.012f, -1f).SafeNormalize(-Vector2.UnitY);
            Vector2 clampPoint = target + new Vector2(baseX * 0.1f, -26f);

            Vector2 nodePos = root;
            Vector2 currentDir = openDir;
            const int Segments = 3;
            for (int s = 0; s < Segments; s++) {
                float segLen = length * (s == 0 ? 0.42f : s == 1 ? 0.34f : 0.26f);
                //越靠指尖越吃合拢量
                float segClamp = MathHelper.Clamp(clampAmount * (0.55f + s * 0.45f), 0f, 1f);
                Vector2 toClamp = (clampPoint - nodePos).SafeNormalize(currentDir);
                Vector2 dir = Vector2.Lerp(currentDir, toClamp, segClamp).SafeNormalize(currentDir);
                Vector2 next = nodePos + dir * segLen;
                float width = MathHelper.Lerp(15f, 8f, s / (float)(Segments - 1));
                DrawCharSegment(sb, nodePos, next, width, fade, index * 2.1f + s * 0.9f);
                //节间烬缝
                float flick = 0.5f + 0.5f * MathF.Sin(Timer * 0.27f + index * 1.9f + s * 2.3f);
                float crackAlpha = (0.3f + clampAmount * 0.4f
                    + (crushFlash > 0 ? crushFlash / 14f * 0.35f : 0f)) * flick * fade;
                sb.Draw(VaultAsset.placeholder2.Value, next - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1), EmberHot * crackAlpha,
                    dir.ToRotation() + MathHelper.PiOver2, new Vector2(0.5f),
                    new Vector2(width * 0.66f, 1.5f), SpriteEffects.None, 0f);
                nodePos = next;
                currentDir = dir;
            }
        }

        /// <summary>一节焦炭：暗边 + 近实心体。</summary>
        private void DrawCharSegment(SpriteBatch sb, Vector2 from, Vector2 to, float width,
            float fade, float seedOffset) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 delta = to - from;
            float len = delta.Length();
            if (len < 1f) {
                return;
            }
            float rot = delta.ToRotation();
            float wobble = 1f + 0.04f * MathF.Sin(Timer * 0.15f + seedOffset + Seed);
            sb.Draw(pixel, from - Main.screenPosition, src, CharEdge * (0.88f * fade), rot,
                new Vector2(0f, 0.5f), new Vector2(len, width * wobble + 3f), SpriteEffects.None, 0f);
            sb.Draw(pixel, from - Main.screenPosition, src, CharBody * (0.95f * fade), rot,
                new Vector2(0f, 0.5f), new Vector2(len, width * wobble), SpriteEffects.None, 0f);
        }

        /// <summary>焦黑掌印：掌斑 + 五道指痕，随余韵浮现又缓灭。</summary>
        private void DrawScorchPrint(SpriteBatch sb, float appear) {
            Texture2D glow = TextureAssets.Extra[ExtrasID.ThePerfectGlow].Value;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            float fade = appear * MathHelper.Clamp(1.3f - PhaseProgress, 0f, 1f);
            Vector2 ground = palmAnchor + new Vector2(0f, 46f) - Main.screenPosition;
            sb.Draw(glow, ground, null, CharEdge * (0.8f * fade), 0f, glowOrigin,
                new Vector2(0.5f, 0.14f), SpriteEffects.None, 0f);
            for (int i = 0; i < FingerOffsets.Length; i++) {
                Vector2 mark = ground + new Vector2(FingerOffsets[i] * 0.9f, -6f);
                sb.Draw(glow, mark, null, CharEdge * (0.65f * fade), 0f, glowOrigin,
                    new Vector2(0.07f, 0.16f), SpriteEffects.None, 0f);
                if (i % 2 == 0) {
                    sb.Draw(glow, mark, null, EmberDim * (0.3f * fade), 0f, glowOrigin,
                        new Vector2(0.04f, 0.08f), SpriteEffects.None, 0f);
                }
            }
        }

        private static void SpawnCharSmoke(Vector2 pos, int count) {
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(pos + Main.rand.NextVector2Circular(10f, 6f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.1f)
                    + Main.rand.NextVector2Circular(0.5f, 0.3f),
                    new Color(58, 30, 20), Main.rand.NextFloat(0.09f, 0.15f))
                    ?.Configure(Main.rand.Next(22, 38), 0.45f, Main.rand.NextFloat(-0.02f, 0.02f));
            }
        }

        private static void SpawnEmber(Vector2 pos, int count, float speed) {
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_PallbearerEmber>(
                    pos + Main.rand.NextVector2Circular(10f, 8f),
                    Main.rand.NextVector2Circular(speed, speed) - Vector2.UnitY * speed * 0.4f,
                    Main.rand.NextBool() ? EmberHot : new Color(222, 82, 30),
                    Main.rand.NextFloat(0.4f, 0.85f))
                    ?.Configure(Main.rand.Next(14, 24), 0.05f);
            }
        }

        public override Vector2 CameraFocus => Phase == WraithSeizePhase.Linger
            ? DeathAnchor + new Vector2(0f, 24f)
            : Player?.Center ?? DeathAnchor;

        public override float CameraZoom => Phase switch {
            WraithSeizePhase.Omen => 1.12f,
            WraithSeizePhase.Manifest => 1.32f,
            WraithSeizePhase.Linger => 1.14f,
            _ => 1f,
        };

        public override float ShakeIntensity => Phase switch {
            WraithSeizePhase.Omen => 2f * PhaseProgress,
            WraithSeizePhase.Manifest => 2.2f + (crushFlash > 0 ? crushFlash * 0.5f : 0f),
            WraithSeizePhase.Linger => crushFlash > 0 ? crushFlash * 0.45f : 0f,
            _ => 0f,
        };
    }
}
