using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers
{
    /// <summary>农牧机组状态灯的统一状态语义</summary>
    internal enum FarmLampState : byte
    {
        /// <summary>熄灭:待机/手动停机</summary>
        Off,
        /// <summary>昏暗常亮:可运转但暂无作业对象</summary>
        Idle,
        /// <summary>系列色慢呼吸:作业中</summary>
        Working,
        /// <summary>琥珀双闪:缺瓶/缺水/缺燃料/满仓等物料阻塞</summary>
        MissingResource,
        /// <summary>红色慢闪:缺电</summary>
        NoPower,
    }

    /// <summary>
    /// 农牧机组(蘑菇农场机/养蜂箱/史莱姆培养槽/生物质发电机)共用的机身状态灯。
    /// 与既有"缺电贴图变暗"的状态语言互补:灯给出原因编码,贴图变暗给出总体可用性。
    /// 纯客户端绘制,画在 TP 主体层(AlphaBlend 批),亮层走 A=0 加色技巧
    /// </summary>
    internal static class FarmStatusLamp
    {
        public static void Draw(SpriteBatch sb, Vector2 lampWorldPos, FarmLampState state, Color seriesTint, int seed) {
            if (state == FarmLampState.Off) {
                return;
            }

            //各机相位错开,一屏机器不齐闪
            float phase = (Main.GameUpdateCount + (uint)(seed * 37)) % 100000u;
            float intensity;
            Color color;
            switch (state) {
                case FarmLampState.Working:
                    //慢呼吸:两个不可通约频率叠加,避免机械正弦
                    intensity = 0.62f + 0.24f * MathF.Sin(phase * 0.041f) + 0.14f * MathF.Sin(phase * 0.0173f);
                    color = seriesTint;
                    break;
                case FarmLampState.MissingResource:
                    //琥珀双闪:亮6灭8亮6,再灭一大段,一组72tick
                    float t72 = phase % 72f;
                    bool on = t72 < 6f || (t72 >= 14f && t72 < 20f);
                    intensity = on ? 1f : 0.06f;
                    color = new Color(255, 190, 60);
                    break;
                case FarmLampState.NoPower:
                    //红色慢闪:90tick 缓升骤降
                    float t90 = phase % 90f / 90f;
                    intensity = (t90 < 0.7f ? t90 / 0.7f : (1f - t90) / 0.3f) * 0.9f;
                    color = new Color(255, 64, 48);
                    break;
                default:
                    intensity = 0.22f;
                    color = seriesTint;
                    break;
            }

            if (intensity <= 0.02f) {
                return;
            }

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = lampWorldPos - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;
            Color add = new(color.R, color.G, color.B, 0);
            //晕-核-白芯三层,核心 3px 上下的小灯珠
            sb.Draw(glow, drawPos, null, add * (0.5f * intensity), 0f, origin, 0.30f, SpriteEffects.None, 0f);
            sb.Draw(glow, drawPos, null, add * intensity, 0f, origin, 0.11f, SpriteEffects.None, 0f);
            sb.Draw(glow, drawPos, null, new Color(255, 255, 255, 0) * (0.5f * intensity), 0f, origin, 0.05f, SpriteEffects.None, 0f);
        }
    }
}
