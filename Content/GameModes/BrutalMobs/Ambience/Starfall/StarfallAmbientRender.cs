using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Starfall
{
    /// <summary>
    /// 陨石坑氛围的屏幕层绘制：热浪微光（Airflow 横向流线自地块缓升，暖色极淡加色，
    /// 读作空气受热的扭曲暗示）与「磁暴弧」（空中紫色分形电弧，快闪后余辉衰减，纯氛围）。
    /// 挂 EndEntityDraw 盖在实体之上（弧与热浪都是玩家与镜头之间的空气层），自开自收加色批
    /// </summary>
    internal sealed class StarfallAmbientRender : RenderHandle
    {
        /// <summary>权重 1.71（本批槽位分配值）</summary>
        public override float Weight => 1.71f;

        //==================== 热浪微光 ====================

        private const int MaxWisps = 10;
        /// <summary>热浪暖色（加色批内以 alpha 乘子控强度）</summary>
        private static readonly Color HeatWarm = new(255, 138, 66);

        private struct Wisp
        {
            internal bool Active;
            internal Vector2 Origin;
            internal float Rise;
            internal int Life;
            internal int MaxLife;
            internal float Phase;
            internal float ScaleX;
        }

        private static readonly Wisp[] wisps = new Wisp[MaxWisps];
        private static int wispSpawnIn;

        //==================== 磁暴弧 ====================

        private const int MaxArcs = 3;
        /// <summary>主干顶点数（4 级中点位移细分：16 段 17 点）</summary>
        private const int ArcPts = 17;
        private const int BranchPts = 5;
        private static readonly Color ArcDeep = new(126, 70, 214);
        private static readonly Color ArcCore = new(216, 186, 255);

        private struct Arc
        {
            internal bool Active;
            internal int Life;
            internal int MaxLife;
            internal bool HasBranch;
            internal float Seed;
        }

        private static readonly Arc[] arcs = new Arc[MaxArcs];
        private static readonly Vector2[][] arcPts = BuildPtBuffer(MaxArcs, ArcPts);
        private static readonly Vector2[][] branchPts = BuildPtBuffer(MaxArcs, BranchPts);

        private static Vector2[][] BuildPtBuffer(int slots, int pts) {
            Vector2[][] buffer = new Vector2[slots][];
            for (int i = 0; i < slots; i++) {
                buffer[i] = new Vector2[pts];
            }
            return buffer;
        }

        internal static void Clear() {
            for (int i = 0; i < wisps.Length; i++) {
                wisps[i].Active = false;
            }
            for (int i = 0; i < arcs.Length; i++) {
                arcs[i].Active = false;
            }
        }

        /// <summary>
        /// 在指定空中位置放一道磁暴弧：主干中点位移成形，随机一条下垂支弧，
        /// 出生帧播电噪并顺弧撒少量紫色火花
        /// </summary>
        internal static void SpawnArc(Vector2 start) {
            int slot = -1;
            for (int i = 0; i < arcs.Length; i++) {
                if (!arcs[i].Active) {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) {
                return;
            }

            Vector2[] pts = arcPts[slot];
            float dir = Main.rand.NextBool() ? 1f : -1f;
            Vector2 end = start + new Vector2(Main.rand.NextFloat(240f, 420f) * dir,
                Main.rand.NextFloat(-90f, 90f));
            pts[0] = start;
            pts[ArcPts - 1] = end;
            //中点位移：位移幅度随细分尺度衰减，得到自然的分形折线
            for (int step = 8; step >= 1; step /= 2) {
                float amp = step * 7.5f;
                for (int i = step; i < ArcPts - 1; i += step * 2) {
                    Vector2 mid = (pts[i - step] + pts[i + step]) * 0.5f;
                    Vector2 normal = (pts[i + step] - pts[i - step])
                        .SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                    pts[i] = mid + normal * Main.rand.NextFloat(-amp, amp);
                }
            }

            bool hasBranch = !Main.rand.NextBool(3);//约 2/3 概率带支弧
            if (hasBranch) {
                Vector2[] branch = branchPts[slot];
                int from = Main.rand.Next(4, ArcPts - 5);
                Vector2 bDir = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), 1f)
                    .SafeNormalize(Vector2.UnitY);
                branch[0] = pts[from];
                float segLen = Main.rand.NextFloat(16f, 26f);
                for (int i = 1; i < BranchPts; i++) {
                    branch[i] = branch[i - 1] + bDir * segLen
                        + new Vector2(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-4f, 4f));
                }
            }

            arcs[slot] = new Arc {
                Active = true,
                Life = 0,
                MaxLife = Main.rand.Next(24, 34),
                HasBranch = hasBranch,
                Seed = Main.rand.NextFloat(20f),
            };

            Vector2 mid2 = pts[ArcPts / 2];
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with {
                Volume = 0.42f * StarfallAmbience.Presence,
                Pitch = -0.18f,
                MaxInstances = 3,
            }, mid2);
            for (int i = 0; i < 6; i++) {
                Dust fleck = Dust.NewDustPerfect(pts[Main.rand.Next(ArcPts)], DustID.PurpleTorch,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.2f, 1.1f)),
                    0, default, Main.rand.NextFloat(0.6f, 1f));
                fleck.noGravity = true;
            }
        }

        //==================== 逻辑更新 ====================

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = StarfallAmbience.Presence;

            for (int i = 0; i < arcs.Length; i++) {
                if (arcs[i].Active && ++arcs[i].Life >= arcs[i].MaxLife) {
                    arcs[i].Active = false;
                }
            }

            for (int i = 0; i < wisps.Length; i++) {
                if (wisps[i].Active && ++wisps[i].Life >= wisps[i].MaxLife) {
                    wisps[i].Active = false;
                }
            }
            if (presence < 0.02f) {
                return;
            }
            if (--wispSpawnIn > 0) {
                return;
            }
            wispSpawnIn = Main.rand.Next(14, 22);
            if (!StarfallAmbience.TryPickAnchor(out Vector2 anchor)) {
                return;
            }
            for (int i = 0; i < wisps.Length; i++) {
                if (wisps[i].Active) {
                    continue;
                }
                wisps[i] = new Wisp {
                    Active = true,
                    Origin = anchor + new Vector2(Main.rand.NextFloat(-10f, 10f), -6f),
                    Rise = Main.rand.NextFloat(0.5f, 1.05f),
                    Life = 0,
                    MaxLife = Main.rand.Next(70, 110),
                    Phase = Main.rand.NextFloat(MathHelper.TwoPi),
                    ScaleX = Main.rand.NextFloat(0.5f, 0.9f),
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
            float presence = StarfallAmbience.Presence;
            if (presence < 0.02f) {
                return;
            }
            bool anyWisp = false;
            for (int i = 0; i < wisps.Length; i++) {
                if (wisps[i].Active) {
                    anyWisp = true;
                    break;
                }
            }
            bool anyArc = false;
            for (int i = 0; i < arcs.Length; i++) {
                if (arcs[i].Active) {
                    anyArc = true;
                    break;
                }
            }
            if (!anyWisp && !anyArc) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            if (anyWisp) {
                DrawWisps(spriteBatch, presence);
            }
            if (anyArc) {
                DrawArcs(spriteBatch, presence);
            }
            spriteBatch.End();
        }

        //热浪微光：横向流线带自地块缓升，进出各有包络，横向随热流轻摆。
        //Airflow 实测 ext_w=1.00（长轴零端部衰减），整条拉伸=两端一刀切（VFX.md 禁令），
        //镜像 DuneStormRender 的截条三段透明度阶梯收口：暗-亮-暗
        private static void DrawWisps(SpriteBatch sb, float presence) {
            Texture2D flow = CWRAsset.Airflow?.Value;
            if (flow == null || flow.IsDisposed) {
                return;
            }
            //三段源截条（沿 256 长轴）与端部收口透明度
            ReadOnlySpan<int> segX = [0, 77, 179];
            ReadOnlySpan<int> segW = [77, 102, 77];
            ReadOnlySpan<float> segA = [0.35f, 1f, 0.35f];

            for (int i = 0; i < wisps.Length; i++) {
                if (!wisps[i].Active) {
                    continue;
                }
                float t = wisps[i].Life / (float)wisps[i].MaxLife;
                float env = Math.Min(t / 0.25f, 1f) * MathHelper.Clamp((1f - t) / 0.35f, 0f, 1f);
                float alpha = 0.075f * env * presence;
                if (alpha < 0.004f) {
                    continue;
                }
                float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + wisps[i].Phase) * 5f;
                Vector2 pos = wisps[i].Origin
                    + new Vector2(sway, -wisps[i].Rise * wisps[i].Life)
                    - Main.screenPosition;
                float scaleX = wisps[i].ScaleX;
                for (int s = 0; s < 3; s++) {
                    var src = new Rectangle(segX[s], 0, segW[s], flow.Height);
                    //截条中心相对贴图中心的横向偏移（对称阶梯）
                    float axisOffset = (segX[s] + segW[s] * 0.5f - flow.Width * 0.5f) * scaleX;
                    sb.Draw(flow, pos + new Vector2(axisOffset, 0f), src,
                        HeatWarm * (alpha * segA[s]), 0f,
                        new Vector2(segW[s] * 0.5f, flow.Height * 0.5f),
                        new Vector2(scaleX, 0.30f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>磁暴弧：双层折线（宽暗紫衬底 + 细亮芯），快闪 15% 寿命后二次衰减余辉</summary>
        private static void DrawArcs(SpriteBatch sb, float presence) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            for (int i = 0; i < arcs.Length; i++) {
                if (!arcs[i].Active) {
                    continue;
                }
                float t = arcs[i].Life / (float)arcs[i].MaxLife;
                float fade = t < 0.15f ? 1f : 1f - (t - 0.15f) / 0.85f;
                fade *= fade;
                float flicker = 0.75f + 0.25f * MathF.Sin(arcs[i].Life * 2.7f + arcs[i].Seed);
                float alpha = fade * flicker * presence;
                if (alpha < 0.01f) {
                    continue;
                }
                DrawBolt(sb, px, arcPts[i], ArcPts, alpha, 1f);
                if (arcs[i].HasBranch) {
                    DrawBolt(sb, px, branchPts[i], BranchPts, alpha * 0.7f, 0.6f);
                }
            }
        }

        private static void DrawBolt(SpriteBatch sb, Texture2D px, Vector2[] pts, int count,
            float alpha, float thickScale) {
            for (int s = 0; s < count - 1; s++) {
                Vector2 a = pts[s];
                Vector2 d = pts[s + 1] - a;
                float len = d.Length();
                if (len < 1f) {
                    continue;
                }
                float rot = d.ToRotation();
                Vector2 screenA = a - Main.screenPosition;
                //宽暗紫衬底
                sb.Draw(px, screenA, null, ArcDeep * (alpha * 0.32f), rot,
                    new Vector2(0f, px.Height * 0.5f),
                    new Vector2(len / px.Width, 6f * thickScale / px.Height), SpriteEffects.None, 0f);
                //细亮芯
                sb.Draw(px, screenA, null, ArcCore * (alpha * 0.85f), rot,
                    new Vector2(0f, px.Height * 0.5f),
                    new Vector2(len / px.Width, 2.2f * thickScale / px.Height), SpriteEffects.None, 0f);
            }
        }
    }
}
