using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Summon.Deepclaws
{
    /// <summary>
    /// 钳渊尾链与粒子帮助。色板沿用 <see cref="AbyssrendFX"/>。
    /// 尾链参数按组装参考图实测:尾节间距 7px@1x(=14px@2x),三节尾节接尾扇
    /// </summary>
    internal static class DeepclawVFX
    {
        /// <summary>尾节数(不含尾扇)</summary>
        public const int TailSegments = 3;
        /// <summary>链节点数:尾根 + 尾节 + 尾扇</summary>
        public const int TailNodes = TailSegments + 2;
        public const float SegmentSpacing = 14f;
        public const float FanSpacing = 17f;
        /// <summary>尾根相对躯干贴图中心的偏移(贴图朝上姿态,像素)</summary>
        public static readonly Vector2 TailAnchorOffset = new(0f, 38f);

        /// <summary>
        /// 参数化尾链:节点朝 restAngle 收拢并叠加游动波,位置带惯性平滑。
        /// swim 为常态摆幅,whip 为甩尾增幅
        /// </summary>
        public static void BuildTail(Vector2[] nodes, Vector2 anchor, float restAngle
            , float time, float swim, float whip, float smooth) {
            nodes[0] = anchor;
            for (int i = 1; i < nodes.Length; i++) {
                float spacing = i == nodes.Length - 1 ? FanSpacing : SegmentSpacing;
                float tailT = i / (nodes.Length - 1f);
                float wave = MathF.Sin(time * 3.2f - i * 0.95f) * swim * (0.35f + 0.65f * tailT)
                    + MathF.Sin(time * 8.6f - i * 1.4f) * whip * tailT;
                Vector2 rest = (restAngle + wave).ToRotationVector2();
                Vector2 want = nodes[i - 1] + rest * spacing;
                nodes[i] = Vector2.Lerp(nodes[i], want, smooth);
                //距离约束防拉断/塌缩
                Vector2 d = nodes[i] - nodes[i - 1];
                float len = d.Length();
                if (len > spacing * 1.3f || len < spacing * 0.6f) {
                    nodes[i] = nodes[i - 1] + d.SafeNormalize(rest) * spacing;
                }
            }
        }

        /// <summary>沿 restAngle 直线铺开节点,生成/瞬移后重置用</summary>
        public static void ResetTail(Vector2[] nodes, Vector2 anchor, float restAngle) {
            nodes[0] = anchor;
            Vector2 dir = restAngle.ToRotationVector2();
            for (int i = 1; i < nodes.Length; i++) {
                float spacing = i == nodes.Length - 1 ? FanSpacing : SegmentSpacing;
                nodes[i] = nodes[i - 1] + dir * spacing;
            }
        }

        /// <summary>尾扇最先画,尾节由尾向根叠上来,躯干由调用方压轴</summary>
        public static void DrawTail(Vector2[] nodes, Color lightColor, float alpha, float scale) {
            Texture2D seg = DeepclawLobster.SegmentTex?.Value;
            Texture2D fan = DeepclawLobster.FanTex?.Value;
            if (seg == null || fan == null) {
                return;
            }
            Color col = lightColor * alpha;
            for (int i = nodes.Length - 1; i >= 1; i--) {
                Texture2D tex = i == nodes.Length - 1 ? fan : seg;
                //贴图朝下(+Y)姿态,链向量转贴图旋转
                float rot = (nodes[i] - nodes[i - 1]).SafeNormalize(Vector2.UnitY).ToRotation() - MathHelper.PiOver2;
                Main.EntitySpriteDraw(tex, nodes[i] - Main.screenPosition, null, col
                    , rot, tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            }
        }

        /// <summary>钳鸣蓄势:水团向钳口收拢</summary>
        public static void SnapGather(Vector2 clawPos, float progress) {
            if (Main.rand.NextFloat() > 0.4f + progress * 0.5f) {
                return;
            }
            Vector2 dir = Main.rand.NextVector2Unit();
            float dist = MathHelper.Lerp(56f, 20f, progress);
            PRTLoader.NewParticle<PRT_AbyssGlob>(clawPos + dir * dist
                , -dir * MathHelper.Lerp(2f, 5f, progress)
                , Color.Lerp(AbyssrendFX.Body, AbyssrendFX.Cyan, progress * 0.6f)
                , Main.rand.NextFloat(0.25f, 0.45f))
                .Configure(12, 1.3f);
        }

        /// <summary>钳击命中水花</summary>
        public static void HitSplat(Vector2 pos, Vector2 vel) {
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(pos
                    , vel.SafeNormalize(Vector2.UnitX).RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(2f, 6f)
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.3f, 0.55f))
                    .Configure(14, 1.4f);
            }
            PRTLoader.NewParticle<PRT_AbyssSpark>(pos, Main.rand.NextVector2Circular(3f, 3f)
                , AbyssrendFX.Cyan, Main.rand.NextFloat(0.8f, 1.1f))
                .Configure(10);
        }

        /// <summary>冲刺水尾:少量深色水团顺流散开</summary>
        public static void DashSpray(Vector2 pos, Vector2 vel) {
            PRTLoader.NewParticle<PRT_AbyssGlob>(pos + Main.rand.NextVector2Circular(8f, 8f)
                , -vel * 0.1f + Main.rand.NextVector2Circular(0.8f, 0.8f)
                , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                , Main.rand.NextFloat(0.22f, 0.4f))
                .Configure(11, 1.4f);
        }
    }
}
