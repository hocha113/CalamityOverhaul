using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 星图审判:天穹上逐笔连出星座,定形拍外环哨星应召点亮,全体延长线(主图折线+外环弦线)
    /// 先亮满长虚线预警;预警末拍整体熄灭 BlackoutFrames(吸气),光刃随即骤现;<br/>
    /// 放光期逐星较差自转(同旋向、每星速率 0.65~1.35×基速)=激光彼此相对移动、口袋缓慢形变,
    /// 外环弦线封死"站远即无视",躲避空间在图心口袋<br/>
    /// ai[0]=宿主npc ai[1]=种子 ai[2]=主图节点数(外环哨星数由此导出,全部几何各端一致)<br/>
    /// 公平阀:生成端 PlayerClearance 校验全部延长线(含外环)不贴脸;预警即承诺:落刃瞬间位置=预警位置
    /// (逐星转角在落刃帧为零);主图与外环均为开链永不合围(外环恒留逃生扇);全程无音效(用户裁定 2026-08-28)
    /// </summary>
    internal class CultistStarChart : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int MaxLifetime = 300;
        private const int EdgeDrawFrames = 10;
        private const int DrawStart = 8;
        /// <summary>光刃判定半宽(可见条带半宽 44,亮体盖过判定)</summary>
        private const float BeamHitWidth = 40f;
        /// <summary>延长线半长</summary>
        private const float BeamHalfLen = 3400f;
        /// <summary>生成校验:任何延长线到玩家的最小距离(生成端读)</summary>
        internal const float PlayerClearance = 170f;
        /// <summary>定形后满长虚线预警帧数(末 BlackoutFrames 帧整体熄灭)</summary>
        private const int WarnFrames = 48;
        /// <summary>预警熄灭帧数:落刃前预警线短暂消失一拍,随后光刃骤现(吸气→爆发)</summary>
        private const int BlackoutFrames = 10;
        /// <summary>放光持续帧数(2026-08-28 用户令:旋转时间 -70%,320→96)</summary>
        private const int BeamFrames = 96;
        /// <summary>较差自转基速(rad/帧):0.0192 再降 40%;每星实际速率 0.65~1.35×此值,
        /// 最快星 600px 处线扫速≈9.3px/帧(带翅可跟),窗口短=形变总量有界</summary>
        private const float RotRate = 0.0115f;

        private int OwnerWho => (int)Projectile.ai[0];
        private int Seed => (int)Projectile.ai[1];
        private int NodeCount => Math.Clamp((int)Projectile.ai[2], 4, 10);
        private float Age => MaxLifetime - Projectile.timeLeft;

        private int DrawEnd => DrawStart + EdgeDrawFrames * (NodeCount - 1);
        private int CommitFrame => DrawEnd + 9;
        private int BeamStart => BeamStartFor(NodeCount);
        private int BeamEnd => BeamStart + BeamFrames;

        /// <summary>落刃帧(相对弹龄);州侧用它界定"星案形成期"(生成 Timer 12+此值=落刃 Timer)</summary>
        internal static int BeamStartFor(int nodeCount) =>
            DrawStart + EdgeDrawFrames * (nodeCount - 1) + 9 + WarnFrames;

        /// <summary>延长线半长 3400 远超弹体:屏检余量放到光刃级,离图心时预警/落刃不消失</summary>
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLifetime;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        private static float Hash01(int seed, int salt) {
            uint h = (uint)(seed * 747796405 + salt * 2891336453u);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) % 10000 / 10000f;
        }

        /// <summary>确定性星座节点(相对图心);开折线,步长/转角受限,永不合围</summary>
        internal static void BuildNodes(int seed, int nodeCount, Span<Vector2> nodes) {
            float angle = Hash01(seed, 0) * MathHelper.TwoPi;
            nodes[0] = angle.ToRotationVector2() * (240f + Hash01(seed, 1) * 180f);
            float heading = angle + MathHelper.Pi + (Hash01(seed, 2) - 0.5f) * 1.2f;
            for (int k = 1; k < nodeCount; k++) {
                float step = 360f + Hash01(seed, k * 7 + 3) * 220f;
                nodes[k] = nodes[k - 1] + heading.ToRotationVector2() * step;
                //下一笔转向:±0.55~1.45 rad,方向交替倾向防打圈
                float turn = 0.55f + Hash01(seed, k * 7 + 4) * 0.9f;
                heading += (Hash01(seed, k * 7 + 5) > 0.5f ? turn : -turn);
                //收在图幅内
                if (nodes[k].Length() > 1120f) {
                    nodes[k] = nodes[k].SafeNormalize(Vector2.UnitX) * 1120f;
                    heading = (Vector2.Zero - nodes[k]).ToRotation() + (Hash01(seed, k * 7 + 6) - 0.5f) * 1.4f;
                }
            }
        }

        /// <summary>外环哨星数(由主图节点数导出,零同步):星云主场 9 节点配 5,其余配 4</summary>
        internal static int OuterCountFor(int nodeCount) => nodeCount >= 9 ? 5 : 4;

        /// <summary>外环哨星(相对图心):基角随机,等分+抖动,半径 1350~1650;
        /// 相邻弦线延长=外围激光,开链不合围=恒留逃生扇</summary>
        internal static void BuildOuterNodes(int seed, int outerCount, Span<Vector2> nodes) {
            float baseAngle = Hash01(seed, 199) * MathHelper.TwoPi;
            for (int k = 0; k < outerCount; k++) {
                float angle = baseAngle + k * MathHelper.TwoPi / outerCount
                    + (Hash01(seed, 210 + k) - 0.5f) * 0.5f;
                nodes[k] = angle.ToRotationVector2() * (1350f + Hash01(seed, 230 + k) * 300f);
            }
        }

        /// <summary>逐星转角(Age 纯函数,零同步):全体同旋向,每星速率 0.65~1.35×基速=星系式较差自转,
        /// 激光彼此相对移动;落刃帧转角为零=预告承诺保持;salt 区分星位(主图 300+k,外环 340+o)</summary>
        private float NodeRotationAt(float age, int salt) {
            float t = age - BeamStart;
            if (t <= 0f) {
                return 0f;
            }
            float swept = RotRate * (0.65f + 0.7f * Hash01(Seed, salt)) * t;
            return Hash01(Seed, 91) > 0.5f ? swept : -swept;
        }

        public override void AI() {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss) {
                Projectile.Kill();
                return;
            }
            float age = Age;
            int palette = (int)owner.ai[0];

            //定形拍:星座落印(闪+震,无音效)
            if ((int)age == CommitFrame) {
                CultistScreenFX.PushFlash(0.25f);
                CultistMotion.Shake(Projectile.Center, 5f, 12);
            }
            //放光拍:熄灭一拍后光刃骤现(闪+震,无音效)
            if ((int)age == BeamStart) {
                CultistScreenFX.PushFlash(0.35f);
                CultistMotion.Shake(Projectile.Center, 5f, 12);
            }

            if (age > BeamEnd + 26) {
                Projectile.Kill();
                return;
            }
            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(palette).ToVector3() * 0.4f);
        }

        /// <summary>单条延长线判定(a/b 为线上两点,向两侧各延 BeamHalfLen)</summary>
        private static bool LineHits(Vector2 a, Vector2 b, Rectangle targetHitbox) {
            Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
            Vector2 mid = (a + b) * 0.5f;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                mid - dir * BeamHalfLen, mid + dir * BeamHalfLen, BeamHitWidth, ref point);
        }

        /// <summary>伤害窗=放光可见窗;判定=主图+外环各边延长线(与视觉同参,含逐星转扫)</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float age = Age;
            if (age < BeamStart || age > BeamEnd) {
                return false;
            }
            Span<Vector2> nodes = stackalloc Vector2[10];
            BuildNodes(Seed, NodeCount, nodes);
            int outerCount = OuterCountFor(NodeCount);
            Span<Vector2> outer = stackalloc Vector2[5];
            BuildOuterNodes(Seed, outerCount, outer);
            for (int k = 0; k < NodeCount; k++) {
                nodes[k] = nodes[k].RotatedBy(NodeRotationAt(age, 300 + k));
            }
            for (int o = 0; o < outerCount; o++) {
                outer[o] = outer[o].RotatedBy(NodeRotationAt(age, 340 + o));
            }
            for (int e = 0; e < NodeCount - 1; e++) {
                if (LineHits(Projectile.Center + nodes[e], Projectile.Center + nodes[e + 1], targetHitbox)) {
                    return true;
                }
            }
            for (int o = 0; o < outerCount - 1; o++) {
                if (LineHits(Projectile.Center + outer[o], Projectile.Center + outer[o + 1], targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override bool CanHitPlayer(Player target) {
            float age = Age;
            return age >= BeamStart && age <= BeamEnd;
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            int palette = owner != null && owner.active ? (int)owner.ai[0] : 0;
            Color mid = CultistMotion.PhaseCore(palette);
            Color bright = Color.Lerp(mid, Color.White, 0.5f);
            Color deep = Color.Lerp(CultistMotion.PhaseEdge(palette), Color.Black, 0.4f);
            float age = Age;

            Span<Vector2> nodes = stackalloc Vector2[10];
            BuildNodes(Seed, NodeCount, nodes);
            int outerCount = OuterCountFor(NodeCount);
            Span<Vector2> outer = stackalloc Vector2[5];
            BuildOuterNodes(Seed, outerCount, outer);
            if (age > BeamStart) {
                for (int k = 0; k < NodeCount; k++) {
                    nodes[k] = nodes[k].RotatedBy(NodeRotationAt(age, 300 + k));
                }
                for (int o = 0; o < outerCount; o++) {
                    outer[o] = outer[o].RotatedBy(NodeRotationAt(age, 340 + o));
                }
            }

            float fadeOut = MathHelper.Clamp(1f - (age - BeamEnd) / 24f, 0f, 1f);
            float commitPulse = age >= CommitFrame
                ? MathHelper.Clamp(1f - (age - CommitFrame) / 18f, 0f, 1f) : 0f;
            bool beaming = age >= BeamStart && age <= BeamEnd + 8;
            //预警包络:10f 淡入,末 BlackoutFrames 帧整体熄灭(短暂消失→光刃骤现)
            float warnAlpha = 0f;
            float warnCharge = 0f;
            if (age >= CommitFrame && age < BeamStart - BlackoutFrames) {
                warnAlpha = MathHelper.Clamp((age - CommitFrame) / 10f, 0f, 1f) * 0.6f;
                warnCharge = (age - CommitFrame) / WarnFrames * 0.55f;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();

            //主图连线:逐边描绘进度;定形后先亮延长线虚线预警,放光期换全宽光刃
            Vector2[] pts = new Vector2[2];
            float[] widths = new float[2];
            float[] alphas = new float[2];
            for (int e = 0; e < NodeCount - 1; e++) {
                float edgeStart = DrawStart + e * EdgeDrawFrames;
                float prog = MathHelper.Clamp((age - edgeStart) / EdgeDrawFrames, 0f, 1f);
                if (prog <= 0.001f) {
                    continue;
                }
                Vector2 a = Projectile.Center + nodes[e] - Main.screenPosition;
                Vector2 b = Projectile.Center + nodes[e + 1] - Main.screenPosition;

                if (beaming) {
                    //延长线光刃:与判定同参的可见体;熄灭一拍后 2f 宽度过冲 1.5=骤现砸下
                    Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
                    Vector2 midPt = (a + b) * 0.5f;
                    float slam = MathHelper.Clamp((age - BeamStart) / 2f, 0f, 1f);
                    pts[0] = midPt - dir * BeamHalfLen;
                    pts[1] = midPt + dir * BeamHalfLen;
                    widths[0] = widths[1] = 44f * MathHelper.Lerp(1.5f, 1f, slam);
                    alphas[0] = alphas[1] = fadeOut;
                    CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                        deep, mid, bright, 1f, 0f, 1f, e * 0.31f, fadeOut);
                }
                else {
                    if (warnAlpha > 0.01f) {
                        //预警虚线:满长延长线=放光的原位承诺,星屑沿线流动
                        Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
                        Vector2 midPt = (a + b) * 0.5f;
                        pts[0] = midPt - dir * BeamHalfLen;
                        pts[1] = midPt + dir * BeamHalfLen;
                        widths[0] = widths[1] = 12f;
                        alphas[0] = alphas[1] = warnAlpha;
                        CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                            deep, mid, bright, 1f, 7f, warnCharge, e * 0.31f, warnAlpha);
                    }
                    //骨架段:放光结束后随 fadeOut 一起熄(不许光刃谢幕后草稿线回闪)
                    pts[0] = a;
                    pts[1] = b;
                    widths[0] = widths[1] = 15f + commitPulse * 9f;
                    alphas[0] = alphas[1] = fadeOut;
                    CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                        deep, mid, bright, prog, 8f, commitPulse, e * 0.31f, fadeOut);
                }
            }

            //外环弦线:定形前不存在;预警期虚线,放光期光刃(封死"站远即无视")
            for (int o = 0; o < outerCount - 1; o++) {
                Vector2 a = Projectile.Center + outer[o] - Main.screenPosition;
                Vector2 b = Projectile.Center + outer[o + 1] - Main.screenPosition;
                Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
                Vector2 midPt = (a + b) * 0.5f;
                float stripSeed = (NodeCount - 1 + o) * 0.31f;
                if (beaming) {
                    float slam = MathHelper.Clamp((age - BeamStart) / 2f, 0f, 1f);
                    pts[0] = midPt - dir * BeamHalfLen;
                    pts[1] = midPt + dir * BeamHalfLen;
                    widths[0] = widths[1] = 44f * MathHelper.Lerp(1.5f, 1f, slam);
                    alphas[0] = alphas[1] = fadeOut;
                    CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                        deep, mid, bright, 1f, 0f, 1f, stripSeed, fadeOut);
                }
                else if (warnAlpha > 0.01f) {
                    pts[0] = midPt - dir * BeamHalfLen;
                    pts[1] = midPt + dir * BeamHalfLen;
                    widths[0] = widths[1] = 12f;
                    alphas[0] = alphas[1] = warnAlpha;
                    CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                        deep, mid, bright, 1f, 7f, warnCharge, stripSeed, warnAlpha);
                }
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //节点星:笔到即亮(随各自转角移动)
            Color edgeCol = CultistMotion.PhaseEdge(palette);
            for (int k = 0; k < NodeCount; k++) {
                float nodeTime = DrawStart + k * EdgeDrawFrames;
                float appear = MathHelper.Clamp((age - nodeTime) / 8f, 0f, 1f);
                if (appear <= 0.001f) {
                    continue;
                }
                float glowUp = 1f + commitPulse * 0.6f + (beaming ? 0.35f : 0f);
                CultistOrreryRenderer.DrawStarBead(sb,
                    Projectile.Center + nodes[k] - Main.screenPosition, mid, edgeCol,
                    0.26f * appear * glowUp, appear * fadeOut,
                    Main.GlobalTimeWrappedHourly * 1.3f + k * 0.9f);
            }
            //外环哨星:定形拍应召点亮(略小于主星=层级)
            float outerAppear = MathHelper.Clamp((age - CommitFrame) / 8f, 0f, 1f);
            if (outerAppear > 0.001f) {
                float glowUp = 1f + commitPulse * 0.6f + (beaming ? 0.35f : 0f);
                for (int o = 0; o < outerCount; o++) {
                    CultistOrreryRenderer.DrawStarBead(sb,
                        Projectile.Center + outer[o] - Main.screenPosition, mid, edgeCol,
                        0.22f * outerAppear * glowUp, outerAppear * fadeOut,
                        Main.GlobalTimeWrappedHourly * 1.3f + (10 + o) * 0.9f);
                }
            }
            return false;
        }
    }
}
