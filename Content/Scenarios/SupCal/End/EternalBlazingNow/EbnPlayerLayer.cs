using CalamityOverhaul.Content.Items.Accessories;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow
{
    /// <summary>Ebn结局玩家视觉层</summary>
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
                    return;//需佩戴Proverbs戒指
                }
            }

            auraAnimationTimer += 0.04f;
            if (auraAnimationTimer > MathHelper.TwoPi) {
                auraAnimationTimer -= MathHelper.TwoPi;
            }

            if (runeOrbits.Count == 0) {
                InitializeRuneOrbits();
            }

            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPosition = player.MountedCenter - Main.screenPosition;
            Vector2 playerCenter = drawPosition;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            DrawRuneOrbits(sb, playerCenter);
            DrawEnergyPulse(sb, playerCenter);

            if (player.wingTime > 0) {
                DrawWingFlames(sb, playerCenter);
            }

            DrawBodyAura(sb, playerCenter);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void InitializeRuneOrbits() {
            runeOrbits.Clear();

            //三层符文轨道
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
            Texture2D pixel = VaultAsset.placeholder2.Value;

            foreach (var rune in runeOrbits) {
                rune.Angle += rune.RotationSpeed;
                rune.PulsePhase += 0.08f;

                float pulse = (float)Math.Sin(rune.PulsePhase) * 0.5f + 0.5f;
                float currentDist = rune.Distance * (1f + pulse * 0.2f);
                Vector2 pos = center + rune.Angle.ToRotationVector2() * currentDist;

                float scale = rune.Scale * (0.8f + pulse * 0.4f);
                Color color = rune.Color with { A = 0 } * (0.6f + pulse * 0.4f);

                //外发光、核心、高光
                sb.Draw(pixel, pos, null, color * 0.3f, 0f,
                    pixel.Size() / 2f, new Vector2(scale * 8f, scale * 8f), SpriteEffects.None, 0f);

                sb.Draw(pixel, pos, null, color, 0f,
                    pixel.Size() / 2f, new Vector2(scale * 4f, scale * 4f), SpriteEffects.None, 0f);

                sb.Draw(pixel, pos, null, Color.White with { A = 0 } * pulse * 0.5f, 0f,
                    pixel.Size() / 2f, new Vector2(scale * 2f, scale * 2f), SpriteEffects.None, 0f);
            }

            DrawOrbitConnections(sb, center);
        }

        private static void DrawOrbitConnections(SpriteBatch sb, Vector2 center) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            for (int layer = 0; layer < 3; layer++) {
                int countPerLayer = 6 + layer * 2;
                float baseDistance = 60f + layer * 30f;

                for (int i = 0; i < countPerLayer; i++) {
                    int index = layer * countPerLayer + i;
                    if (index >= runeOrbits.Count) break;

                    var rune = runeOrbits[index];
                    Vector2 pos1 = center + rune.Angle.ToRotationVector2() * baseDistance;

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

            float corePulse = (float)Math.Sin(auraAnimationTimer * 3f) * 0.5f + 0.5f;
            Color coreColor = new Color(255, 120, 60) with { A = 0 };

            sb.Draw(glow, center, null, coreColor * corePulse * 0.8f, 0f,
                glow.Size() / 2f, new Vector2(0.4f, 0.4f), SpriteEffects.None, 0f);

            sb.Draw(glow, center, null, Color.White with { A = 0 } * corePulse * 0.3f, 0f,
                glow.Size() / 2f, new Vector2(0.2f, 0.2f), SpriteEffects.None, 0f);
        }

        private static void DrawWingFlames(SpriteBatch sb, Vector2 center) {
            Texture2D glow = VaultAsset.placeholder2.Value;

            float wingSpread = 28f;
            float wingHeight = -12f;

            for (int i = 0; i < 2; i++) {
                float side = i == 0 ? -1f : 1f;
                Vector2 wingPos = center + new Vector2(wingSpread * side, wingHeight);

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

                Color coreColor = new Color(255, 180, 90) with { A = 0 };
                float corePulse = (float)Math.Sin(auraAnimationTimer * 4f) * 0.3f + 0.7f;
                sb.Draw(glow, wingPos, null, coreColor * corePulse * 0.6f, 0f,
                    glow.Size() / 2f, new Vector2(0.25f, 0.35f), SpriteEffects.None, 0f);
            }
        }

        private static void DrawBodyAura(SpriteBatch sb, Vector2 center) {
            Texture2D glow = VaultAsset.placeholder2.Value;

            float bodyPulse = (float)Math.Sin(auraAnimationTimer * 2.5f) * 0.5f + 0.5f;

            Color outerColor = new Color(200, 60, 30) with { A = 0 };
            sb.Draw(glow, center, null, outerColor * bodyPulse * 0.25f,
                auraAnimationTimer,
                glow.Size() / 2f,
                new Vector2(1.2f, 1.5f),
                SpriteEffects.None, 0f);

            Color midColor = new Color(255, 100, 50) with { A = 0 };
            sb.Draw(glow, center, null, midColor * bodyPulse * 0.35f,
                -auraAnimationTimer * 1.3f,
                glow.Size() / 2f,
                new Vector2(0.9f, 1.2f),
                SpriteEffects.None, 0f);

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
