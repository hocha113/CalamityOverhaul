using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 雾里的眼光（P2 §1.2 招牌镜头）：犬体画在背景雾层后会被雾吞没，
    /// 警觉 ≥ 起疑阈值时在雾层之后补画双目小光点，强度 = 警觉比 × 雾浓度门 × 雾 presence。
    /// 浓雾里看不见犬身，只看见两点渐亮的红，这就是警觉表本身。<br/>
    /// 层序：由 KiyumeFogSystem.PostDrawTiles 方法尾调用（贴地雾之后、原版玩家绘制之前），
    /// 调用顺序即契约（裁决 18：零 RenderHandle）。<br/>
    /// 混合纪律（VFX 缺陷①）：加色批源因子=SrcAlpha，顶点色 A=0 会整层隐形——
    /// 强度写进整支色（color × k，A 随乘法同缩），禁 A=0；SoftGlow 黑底图只进加色批
    /// </summary>
    internal static class KiyumeHoundEyeGleam
    {
        //目芯/目晕与 KikasaHound.fx 的 EMBER_CORE/EMBER_HALO 同源
        private static readonly Color EmberCore = new(242, 87, 36);
        private static readonly Color EmberHalo = new(158, 26, 15);
        //雾浓度门归一：与犬影出没门（KiyumeHoundShade.Advance 的 0.28/0.26）同源
        private const float FogGateFloor = 0.28f;
        private const float FogGateSpan = 0.26f;

        //逐槽平滑强度：ai[2] 经同步是台阶量，这里补插值让红光是"渐亮"不是跳档
        private static readonly float[] smooth = new float[Main.maxNPCs];

        /// <summary>会话复位（KiyumeFogSystem.HardReset 调）</summary>
        internal static void Clear() => Array.Clear(smooth);

        internal static void Draw(SpriteBatch sb) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || glow.IsDisposed) {
                return;
            }
            int houndType = ModContent.NPCType<KiyumeHound>();
            float presence = KiyumeFogSystem.Presence;
            var view = new Rectangle((int)Main.screenPosition.X - 160, (int)Main.screenPosition.Y - 160,
                Main.screenWidth + 320, Main.screenHeight + 320);
            Vector2 origin = glow.Size() * 0.5f;
            bool begun = false;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                bool isHound = npc.active && npc.type == houndType;
                float target = 0f;
                if (isHound && presence > 0.02f) {
                    float awareness = npc.ai[2];
                    if (awareness >= KiyumeHoundMetrics.AlertThreshold) {
                        float fogGate = MathHelper.Clamp(
                            (KiyumeFogSim.DensityAt(KiyumeHound.EyeWorldPos(npc, second: false))
                                - FogGateFloor) / FogGateSpan, 0f, 1f);
                        target = awareness / KiyumeHoundMetrics.ChaseThreshold
                            * fogGate * presence * StateMul(npc);
                    }
                }
                smooth[i] = MathHelper.Lerp(smooth[i], target, 0.1f);
                if (!isHound) {
                    //槽位换主即清余辉，防旧光挂在新实体上
                    smooth[i] = 0f;
                    continue;
                }
                if (smooth[i] < 0.015f) {
                    continue;
                }
                //警觉掉档后眼位仍要取（余辉渐熄跟着犬走，不原地悬灯）
                Vector2 eye = KiyumeHound.EyeWorldPos(npc, second: false);
                if (!view.Contains((int)eye.X, (int)eye.Y)) {
                    continue;
                }
                if (!begun) {
                    //自开加色批：PostDrawTiles 此刻无外层批（DrawOverlayShader 同款自开自收）
                    sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null,
                        Main.GameViewMatrix.TransformationMatrix);
                    begun = true;
                }
                DrawEye(sb, glow, origin, eye, smooth[i]);
                //后眼：偏移与 fx 内 eye2x=0.055 同源，弱一半，侧面像里只是一点余光
                DrawEye(sb, glow, origin, KiyumeHound.EyeWorldPos(npc, second: true), smooth[i] * 0.45f);
            }
            if (begun) {
                sb.End();
            }
        }

        //凝实眼未亮 / 化雾渐熄：与犬体眼光包络同拍（读 ai，不碰实例）
        private static float StateMul(NPC npc) {
            int state = (int)npc.ai[0];
            if (state == KiyumeHound.StateEmerge) {
                float t01 = MathHelper.Clamp(npc.ai[1] / KiyumeHoundMetrics.EmergeTicks, 0f, 1f);
                return t01 <= 0.6f ? 0f : (t01 - 0.6f) / 0.4f;
            }
            if (state == KiyumeHound.StateFade) {
                return 1f - MathHelper.Clamp(npc.ai[1] / KiyumeHoundMetrics.FadeTicks, 0f, 1f);
            }
            return 1f;
        }

        private static void DrawEye(SpriteBatch sb, Texture2D glow, Vector2 origin,
            Vector2 worldPos, float k) {
            if (k < 0.015f) {
                return;
            }
            Vector2 pos = worldPos - Main.screenPosition;
            float breath = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.1f + worldPos.X * 0.03f);
            //强度写进整支色：Color × k 连 A 一起缩（加色批禁 A=0）
            sb.Draw(glow, pos, null, EmberHalo * (0.85f * k * breath), 0f, origin,
                (26f + 10f * k) / glow.Width, SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, EmberCore * (k * breath), 0f, origin,
                9f / glow.Width, SpriteEffects.None, 0f);
        }
    }
}
