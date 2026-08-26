using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 星图审判:天穹上逐笔连出星座,定形后沿边线的无限延长线放光<br/>
    /// ai[0]=宿主npc ai[1]=种子(节点/边全由此确定,各端一致) ai[2]=节点数<br/>
    /// 公平阀:生成端已用 PlayerClearance 校验过种子(任何延长线不贴脸);描绘期+定形拍全程无伤预告;<br/>
    /// 图形是开折线,永不合围
    /// </summary>
    internal class CultistStarChart : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int MaxLifetime = 260;
        private const int EdgeDrawFrames = 16;
        private const int DrawStart = 10;
        /// <summary>光刃判定半宽(可见条带半宽 44,亮体盖过判定)</summary>
        private const float BeamHitWidth = 40f;
        /// <summary>延长线半长</summary>
        private const float BeamHalfLen = 2400f;
        /// <summary>生成校验:任何延长线到玩家的最小距离(生成端读)</summary>
        internal const float PlayerClearance = 170f;

        private int OwnerWho => (int)Projectile.ai[0];
        private int Seed => (int)Projectile.ai[1];
        private int NodeCount => Math.Clamp((int)Projectile.ai[2], 4, 8);
        private float Age => MaxLifetime - Projectile.timeLeft;

        private int DrawEnd => DrawStart + EdgeDrawFrames * (NodeCount - 1);
        private int CommitFrame => DrawEnd + 12;
        private int BeamStart => CommitFrame + 10;
        private int BeamEnd => BeamStart + 46;

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
            nodes[0] = angle.ToRotationVector2() * (200f + Hash01(seed, 1) * 150f);
            float heading = angle + MathHelper.Pi + (Hash01(seed, 2) - 0.5f) * 1.2f;
            for (int k = 1; k < nodeCount; k++) {
                float step = 260f + Hash01(seed, k * 7 + 3) * 170f;
                nodes[k] = nodes[k - 1] + heading.ToRotationVector2() * step;
                //下一笔转向:±0.55~1.45 rad,方向交替倾向防打圈
                float turn = 0.55f + Hash01(seed, k * 7 + 4) * 0.9f;
                heading += (Hash01(seed, k * 7 + 5) > 0.5f ? turn : -turn);
                //收在图幅内
                if (nodes[k].Length() > 780f) {
                    nodes[k] = nodes[k].SafeNormalize(Vector2.UnitX) * 780f;
                    heading = (Vector2.Zero - nodes[k]).ToRotation() + (Hash01(seed, k * 7 + 6) - 0.5f) * 1.4f;
                }
            }
        }

        public override void AI() {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss) {
                Projectile.Kill();
                return;
            }
            float age = Age;
            int palette = (int)owner.ai[0];

            //连线笔到达节点:落笔音(各端本地)
            if (age > DrawStart && age <= DrawEnd && ((int)age - DrawStart) % EdgeDrawFrames == 0) {
                int nodeIdx = ((int)age - DrawStart) / EdgeDrawFrames;
                if (!VaultUtils.isServer && nodeIdx < NodeCount) {
                    SoundEngine.PlaySound(SoundID.Item26 with { Volume = 0.6f, Pitch = -0.3f + nodeIdx * 0.12f }, Projectile.Center);
                }
            }

            //定形拍:星座落印
            if ((int)age == CommitFrame) {
                CultistMotion.SigilCommitFX(Projectile.Center, CultistMotion.PhaseCore(palette), 1.5f);
                CultistScreenFX.PushFlash(0.25f);
                CultistMotion.Shake(Projectile.Center, 5f, 12);
            }
            //放光拍
            if ((int)age == BeamStart && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.95f, Pitch = -0.25f }, Projectile.Center);
            }

            if (age > BeamEnd + 26) {
                Projectile.Kill();
                return;
            }
            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(palette).ToVector3() * 0.4f);
        }

        /// <summary>伤害窗=放光可见窗;判定=各边延长线(与视觉同参)</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float age = Age;
            if (age < BeamStart || age > BeamEnd) {
                return false;
            }
            Span<Vector2> nodes = stackalloc Vector2[8];
            BuildNodes(Seed, NodeCount, nodes);
            for (int e = 0; e < NodeCount - 1; e++) {
                Vector2 a = Projectile.Center + nodes[e];
                Vector2 b = Projectile.Center + nodes[e + 1];
                Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
                Vector2 mid = (a + b) * 0.5f;
                float point = 0f;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    mid - dir * BeamHalfLen, mid + dir * BeamHalfLen, BeamHitWidth, ref point)) {
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

            Span<Vector2> nodes = stackalloc Vector2[8];
            BuildNodes(Seed, NodeCount, nodes);

            float fadeOut = MathHelper.Clamp(1f - (age - BeamEnd) / 24f, 0f, 1f);
            float commitPulse = age >= CommitFrame
                ? MathHelper.Clamp(1f - (age - CommitFrame) / 18f, 0f, 1f) : 0f;
            bool beaming = age >= BeamStart && age <= BeamEnd + 8;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();

            //连线:逐边描绘进度
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
                    //延长线光刃:与判定同参的可见体
                    Vector2 dir = (b - a).SafeNormalize(Vector2.UnitX);
                    Vector2 midPt = (a + b) * 0.5f;
                    pts[0] = midPt - dir * BeamHalfLen;
                    pts[1] = midPt + dir * BeamHalfLen;
                    widths[0] = widths[1] = 44f;
                    alphas[0] = alphas[1] = fadeOut;
                    CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                        deep, mid, bright, 1f, 0f, 1f, e * 0.31f, fadeOut);
                }
                else {
                    pts[0] = a;
                    pts[1] = b;
                    widths[0] = widths[1] = 15f + commitPulse * 9f;
                    alphas[0] = alphas[1] = 1f;
                    CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                        deep, mid, bright, prog, 8f, commitPulse, e * 0.31f, 1f);
                }
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //节点星:笔到即亮
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
            return false;
        }
    }
}
