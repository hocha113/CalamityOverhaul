using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.OniRainWorlds.KasaOnis
{
    /// <summary>
    /// 伞鬼绘制：与鬼伞伞奴同根同源的换装——贴图/凝聚着色器/污潭全走
    /// <see cref="KikasaThrallRenderer"/>（KikasaThrall.png + KikasaThrallForm 帧矩形钳制），
    /// 敌对杂兵按 <see cref="BodyDrawScale"/> 画得比召唤伞奴矮一头。<br/>
    /// 出现/落定的水环与冷闪在 <see cref="DrawEmergeBeats"/>，对齐伞奴成形拍；
    /// 落定头 14 帧撑伞 pop 弹性胀缩。
    /// </summary>
    internal static class KasaOniRenderer
    {
        /// <summary>
        /// 敌对伞鬼的贴图缩放：伞奴画布自带 1.6 倍身量，×0.75 后可见身板约为
        /// 旧 KasaOni 素材的 1.2 倍——比人显眼、比召唤伞奴矮一头。验收可调
        /// </summary>
        internal const float BodyDrawScale = 0.75f;

        /// <summary>演出件（污潭/水环/冷闪锚点）的身量系数：按画出来的身板走</summary>
        internal const float PresenceScale = BodyDrawScale * KikasaThrall.BodyBulk;

        /// <summary>撑伞 pop 帧数：落定进 Walking 后整个身量先胀一圈再落回</summary>
        private const int PopFrames = 14;

        internal static void Draw(SpriteBatch spriteBatch, KasaOniActor oni) {
            KasaOniPhase phase = oni.Phase;
            float progress = MathHelper.Clamp(oni.CondenseProgress, 0f, 1f);

            DrawPuddle(spriteBatch, oni, phase, progress);

            if (phase == KasaOniPhase.Submerged) {
                return;
            }

            Vector2 feet = oni.FeetAnchor;
            float seed = oni.WhoAmI * 0.7391f;
            float scale = BodyDrawScale * MaterializePop(oni);

            //夜雨里保轮廓：环境光染向湿墨灰白
            Color light = Lighting.GetColor((feet / 16f).ToPoint());
            light = Color.Lerp(light, KasaOniActor.PaleSheen, 0.30f);

            if (phase == KasaOniPhase.Walking) {
                float moveFactor = MathHelper.Clamp(Math.Abs(oni.Velocity.X) / 1.15f, 0f, 1f);
                KikasaThrallRenderer.DrawBodyWalking(spriteBatch, feet, WalkFrame(oni),
                    scale, oni.FacingLeft, light, oni.WaddlePhase, moveFactor, seed);
                DrawEmergeBeats(spriteBatch, oni);
                return;
            }

            //凝聚/消融走 KikasaThrallForm 着色器；期间的轻微蠕动让液体感不僵
            float wobble = MathF.Sin(Main.GlobalTimeWrappedHourly * 5.3f + oni.WhoAmI * 1.7f)
                * 0.035f * (1f - progress);
            KikasaThrallRenderer.DrawBodyCondensing(spriteBatch, feet, WalkFrame(oni),
                progress, scale, oni.FacingLeft, oni.GroundLineY, light, wobble, seed);
            DrawEmergeBeats(spriteBatch, oni);
        }

        /// <summary>脚下污潭：凝聚期铺开又被吸干、消融期反向涨起（包络留在伞鬼侧）</summary>
        private static void DrawPuddle(SpriteBatch spriteBatch, KasaOniActor oni,
            KasaOniPhase phase, float progress) {

            //包络：正弦弓形，0→张满→0；潜行期由冒泡粒子接管
            float envelope = phase switch {
                KasaOniPhase.Emerging => MathF.Sin(
                    MathHelper.Clamp(progress * 1.2f, 0f, 1f) * MathHelper.Pi),
                KasaOniPhase.Dissolving => MathF.Sin(
                    MathHelper.Clamp((1f - progress) * 1.2f, 0f, 1f) * MathHelper.Pi),
                _ => 0f,
            };
            KikasaThrallRenderer.DrawPuddle(spriteBatch, oni.FeetAnchor, envelope,
                PresenceScale, oni.WhoAmI * 0.7391f);
        }

        /// <summary>
        /// 出现/落定演出的环与光（对齐伞奴 DrawReformBeats）：
        /// 破土环荡开 → 长凝聚期脉冲 → 撑伞拍地面大环+伞面环+冷闪。
        /// 水环走 ShockRingDraw（内部切批还原），冷闪是普通批里的 A=0 软辉
        /// </summary>
        private static void DrawEmergeBeats(SpriteBatch sb, KasaOniActor oni) {
            int t = oni.PhaseTimer;
            Vector2 feet = oni.FeetAnchor;
            float seed = oni.WhoAmI * 0.7391f;

            if (oni.Phase == KasaOniPhase.Emerging) {
                //破土环：雨把地面顶开一圈
                if (t <= 20) {
                    float e = t / 20f;
                    KikasaThrallFX.Flash(sb, feet, 58f * PresenceScale, 0.55f, 0.4f * (1f - e));
                    KikasaThrallFX.WaterRing(sb, feet,
                        MathHelper.Lerp(10f, 100f, KikasaThrallFX.EaseOut(e)) * PresenceScale,
                        0.42f, 0.62f * (1f - e), seed);
                }

                //凝聚脉冲：96 帧长凝聚，每 14 帧荡一圈，越接近成形荡得越急
                int pulse = (t - 14) % 14;
                if (t > 14 && t < KasaOniActor.EmergeFrames && pulse < 8) {
                    float e = pulse / 8f;
                    float rise = t / (float)KasaOniActor.EmergeFrames;
                    KikasaThrallFX.WaterRing(sb, feet,
                        MathHelper.Lerp(8f, 60f + 34f * rise, KikasaThrallFX.EaseOut(e)) * PresenceScale,
                        0.4f, 0.3f * rise * (1f - e), seed + t * 0.05f);
                }
                return;
            }

            //撑伞拍：落定进 Walking 的头 18 帧，地面大环外扩、伞面另起一圈、中间压一记冷闪
            if (oni.Phase == KasaOniPhase.Walking && t <= 18) {
                float e = t / 18f;
                Vector2 canopy = oni.CanopyAnchor;
                Vector2 chest = feet - new Vector2(0f, KasaOniActor.HitboxHeight * PresenceScale * 0.5f);
                KikasaThrallFX.Flash(sb, chest, 95f * PresenceScale, 0.95f, 0.5f * (1f - e));
                KikasaThrallFX.WaterRing(sb, feet,
                    MathHelper.Lerp(20f, 185f, KikasaThrallFX.EaseOut(e)) * PresenceScale,
                    0.36f, 0.8f * (1f - e), seed);
                KikasaThrallFX.WaterRing(sb, canopy,
                    MathHelper.Lerp(8f, 80f, KikasaThrallFX.EaseOut(e)) * PresenceScale,
                    0.82f, 0.65f * (1f - e * e), seed + 2.1f);
            }
        }

        /// <summary>撑伞落定的弹性 pop：伞一撑开，整个身量先胀一圈再落回</summary>
        private static float MaterializePop(KasaOniActor oni) {
            if (oni.Phase != KasaOniPhase.Walking || oni.PhaseTimer > PopFrames) {
                return 1f;
            }
            float p = MathHelper.Clamp(oni.PhaseTimer / (float)PopFrames, 0f, 1f);
            return 1f + 0.26f * MathF.Sin(p * MathHelper.Pi);
        }

        /// <summary>多帧真贴图接入后的默认步频：0.12 相位一帧；单帧恒 0</summary>
        private static int WalkFrame(KasaOniActor oni)
            => KikasaThrallRenderer.FrameCount <= 1
                ? 0 : (int)(oni.WaddlePhase / 0.12f) % KikasaThrallRenderer.FrameCount;
    }
}
