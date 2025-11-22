using CalamityOverhaul.Content.Items.Accessories;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    /// <summary>
    /// 永恒燃烧结局的视觉特效层
    /// </summary>
    internal class EbnPlayerLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Wings);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) {
            return drawInfo.drawPlayer.GetModPlayer<EbnPlayer>().IsEbn;
        }

        private static float auraAnimationTimer = 0f;
        private static readonly List<RuneOrbitData> runeOrbits = new();

        private class RuneOrbitData
        {
            public float Angle;
            public float Distance;
            public float RotationSpeed;
            public float Scale;
            public float PulsePhase;
            public Color Color;
            public int Type;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            if (Main.gameMenu) {
                return;
            }

            Player player = drawInfo.drawPlayer;
            EbnPlayer ebnPlayer = player.GetModPlayer<EbnPlayer>();

            if (!ebnPlayer.IsEbn) return;
            if (player.TryGetModPlayer<ProverbsPlayer>(out var proverbsPlayer)) {
                if (!proverbsPlayer.HasProverbs || proverbsPlayer.HideVisual) {
                    return;//需要戴着戒指才能触发以下效果
                }
            }

            //更新动画计时器
            auraAnimationTimer += 0.04f;
            if (auraAnimationTimer > MathHelper.TwoPi) {
                auraAnimationTimer -= MathHelper.TwoPi;
            }

            //初始化符文轨道
            if (runeOrbits.Count == 0) {
                InitializeRuneOrbits();
            }

            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPosition = player.MountedCenter - Main.screenPosition;
            Vector2 playerCenter = drawPosition;

            //开始绘制
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //绘制中层符文轨道
            DrawRuneOrbits(sb, playerCenter);

            //绘制内层能量脉冲
            DrawEnergyPulse(sb, playerCenter);

            //绘制翅膀火焰
            if (player.wingTime > 0) {
                DrawWingFlames(sb, playerCenter);
            }

            //绘制身体光环
            DrawBodyAura(sb, playerCenter);

            //恢复默认绘制状态
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void InitializeRuneOrbits() {
            runeOrbits.Clear();

            //创建三层符文轨道
            for (int layer = 0; layer < 3; layer++) {
                int count = 6 + layer * 2;
                float baseDistance = 60f + layer * 30f;

                for (int i = 0; i < count; i++) {
                    runeOrbits.Add(new RuneOrbitData {
                        Angle = MathHelper.TwoPi * i / count + layer * 0.5f,
                        Distance = baseDistance,
                        RotationSpeed = (layer % 2 == 0 ? 0.02f : -0.02f) * (1f + layer * 0.1f),
                        Scale = 0.6f + layer * 0.15f,
                        PulsePhase = Main.rand.NextFloat(MathHelper.TwoPi),
                        Color = layer switch {
                            0 => new Color(255, 140, 70),
                            1 => new Color(255, 100, 50),
                            _ => new Color(200, 60, 30)
                        },
                        Type = Main.rand.Next(3)
                    });
                }
            }
        }

        private static void DrawRuneOrbits(SpriteBatch sb, Vector2 center) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;

            foreach (var rune in runeOrbits) {
                //更新角度
                rune.Angle += rune.RotationSpeed;
                rune.PulsePhase += 0.08f;

                //计算位置
                float pulse = (float)Math.Sin(rune.PulsePhase) * 0.5f + 0.5f;
                float currentDist = rune.Distance * (1f + pulse * 0.2f);
                Vector2 pos = center + rune.Angle.ToRotationVector2() * currentDist;

                //绘制符文光点
                float scale = rune.Scale * (0.8f + pulse * 0.4f);
                Color color = rune.Color with { A = 0 } * (0.6f + pulse * 0.4f);

                //外发光
                sb.Draw(pixel, pos, null, color * 0.3f, 0f,
                    pixel.Size() / 2f, new Vector2(scale * 8f, scale * 8f), SpriteEffects.None, 0f);

                //核心
                sb.Draw(pixel, pos, null, color, 0f,
                    pixel.Size() / 2f, new Vector2(scale * 4f, scale * 4f), SpriteEffects.None, 0f);

                //高光
                sb.Draw(pixel, pos, null, Color.White with { A = 0 } * pulse * 0.5f, 0f,
                    pixel.Size() / 2f, new Vector2(scale * 2f, scale * 2f), SpriteEffects.None, 0f);
            }

            //绘制连接线
            DrawOrbitConnections(sb, center);
        }

        private static void DrawOrbitConnections(SpriteBatch sb, Vector2 center) {
            Texture2D pixel = CWRAsset.Placeholder_White.Value;

            //每层内部连接
            for (int layer = 0; layer < 3; layer++) {
                int countPerLayer = 6 + layer * 2;
                float baseDistance = 60f + layer * 30f;

                for (int i = 0; i < countPerLayer; i++) {
                    int index = layer * countPerLayer + i;
                    if (index >= runeOrbits.Count) break;

                    var rune = runeOrbits[index];
                    Vector2 pos1 = center + rune.Angle.ToRotationVector2() * baseDistance;

                    //连接到下一个符文
                    int nextIndex = layer * countPerLayer + (i + 1) % countPerLayer;
                    if (nextIndex < runeOrbits.Count) {
                        var nextRune = runeOrbits[nextIndex];
                        Vector2 pos2 = center + nextRune.Angle.ToRotationVector2() * baseDistance;

                        DrawLine(sb, pixel, pos1, pos2, 1.5f, rune.Color with { A = 0 } * 0.2f);
                    }
                }
            }
        }

        private static void DrawEnergyPulse(SpriteBatch sb, Vector2 center) {
            Texture2D glow = CWRAsset.StarTexture_White.Value;

            //内层脉冲波
            for (int i = 0; i < 4; i++) {
                float phase = (auraAnimationTimer + i * MathHelper.PiOver2) % MathHelper.TwoPi;
                float intensity = (float)Math.Sin(phase);

                if (intensity > 0) {
                    float radius = 30f + intensity * 60f;
                    float alpha = intensity * 0.4f;

                    Color pulseColor = Color.Lerp(
                        new Color(255, 100, 50),
                        new Color(255, 140, 70),
                        intensity
                    ) with { A = 0 };

                    sb.Draw(glow, center, null,
                        pulseColor * alpha,
                        auraAnimationTimer * 2f,
                        glow.Size() / 2f,
                        new Vector2(radius / glow.Width * 2f, radius / glow.Height * 2f),
                        SpriteEffects.None, 0f);
                }
            }

            //核心光球
            float corePulse = (float)Math.Sin(auraAnimationTimer * 3f) * 0.5f + 0.5f;
            Color coreColor = new Color(255, 120, 60) with { A = 0 };

            sb.Draw(glow, center, null, coreColor * corePulse * 0.8f, 0f,
                glow.Size() / 2f, new Vector2(0.4f, 0.4f), SpriteEffects.None, 0f);

            sb.Draw(glow, center, null, Color.White with { A = 0 } * corePulse * 0.3f, 0f,
                glow.Size() / 2f, new Vector2(0.2f, 0.2f), SpriteEffects.None, 0f);
        }

        private static void DrawWingFlames(SpriteBatch sb, Vector2 center) {
            Texture2D glow = CWRAsset.Placeholder_White.Value;

            //翅膀火焰位置
            float wingSpread = 28f;
            float wingHeight = -12f;

            for (int i = 0; i < 2; i++) {
                float side = i == 0 ? -1f : 1f;
                Vector2 wingPos = center + new Vector2(wingSpread * side, wingHeight);

                //火焰拖尾
                for (int j = 0; j < 3; j++) {
                    float offset = j * 10f;
                    float alpha = (1f - j / 3f) * 0.5f;
                    float pulse = (float)Math.Sin(auraAnimationTimer * 2f + j) * 0.3f + 0.7f;

                    Vector2 flamePos = wingPos + new Vector2(0, offset);
                    Color flameColor = Color.Lerp(
                        new Color(255, 140, 70),
                        new Color(200, 60, 30),
                        j / 3f
                    ) with { A = 0 };

                    float scale = (0.4f - j * 0.1f) * pulse;
                    sb.Draw(glow, flamePos, null, flameColor * alpha * pulse, 0f,
                        glow.Size() / 2f, new Vector2(scale, scale * 1.5f), SpriteEffects.None, 0f);
                }

                //火焰核心
                Color coreColor = new Color(255, 180, 90) with { A = 0 };
                float corePulse = (float)Math.Sin(auraAnimationTimer * 4f) * 0.3f + 0.7f;
                sb.Draw(glow, wingPos, null, coreColor * corePulse * 0.6f, 0f,
                    glow.Size() / 2f, new Vector2(0.25f, 0.35f), SpriteEffects.None, 0f);
            }
        }

        private static void DrawBodyAura(SpriteBatch sb, Vector2 center) {
            Texture2D glow = CWRAsset.Placeholder_White.Value;

            //身体周围的硫磺火光环
            float bodyPulse = (float)Math.Sin(auraAnimationTimer * 2.5f) * 0.5f + 0.5f;

            //外层光环
            Color outerColor = new Color(200, 60, 30) with { A = 0 };
            sb.Draw(glow, center, null, outerColor * bodyPulse * 0.25f,
                auraAnimationTimer,
                glow.Size() / 2f,
                new Vector2(1.2f, 1.5f),
                SpriteEffects.None, 0f);

            //中层光环
            Color midColor = new Color(255, 100, 50) with { A = 0 };
            sb.Draw(glow, center, null, midColor * bodyPulse * 0.35f,
                -auraAnimationTimer * 1.3f,
                glow.Size() / 2f,
                new Vector2(0.9f, 1.2f),
                SpriteEffects.None, 0f);

            //内层光环
            Color innerColor = new Color(255, 140, 70) with { A = 0 };
            sb.Draw(glow, center, null, innerColor * bodyPulse * 0.45f,
                auraAnimationTimer * 1.7f,
                glow.Size() / 2f,
                new Vector2(0.6f, 0.8f),
                SpriteEffects.None, 0f);
        }

        private static void DrawLine(SpriteBatch sb, Texture2D pixel, Vector2 start, Vector2 end, float thickness, Color color) {
            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 1f) return;

            sb.Draw(pixel, start, null, color,
                diff.ToRotation(),
                Vector2.Zero,
                new Vector2(length, thickness),
                SpriteEffects.None, 0f);
        }
    }
}
