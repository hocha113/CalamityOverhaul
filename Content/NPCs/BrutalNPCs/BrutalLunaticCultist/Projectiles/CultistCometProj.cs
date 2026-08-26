using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 彗星:自司祭身侧切向甩出,受场心引力拉成沿黄道内壁的大弧,速度复利有顶<br/>
    /// ai[0]=宿主npc ai[1]=绕向(±1);阶段色随宿主 ai[0]<br/>
    /// 公平阀:出生 WarmupFrames 无伤且亮度渐起(声明常量,判定同读);弧线轨迹可预读;
    /// 末段拖尾随头部同步淡出,不在死亡帧消失
    /// </summary>
    internal class CultistCometProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>出生无伤窗(帧):彗星从司祭身边划出,亮起前不咬人</summary>
        internal const int WarmupFrames = 20;
        private const int Lifetime = 260;
        /// <summary>末段淡出帧:头与尾一起收,拖尾不在死亡帧砍断</summary>
        private const int FadeFrames = 30;
        /// <summary>碰撞半径:小于可见星芒核(头核直径约 45px)</summary>
        private const float HitRadius = 20f;

        private int OwnerWho => (int)Projectile.ai[0];
        private float OrbitDir => Projectile.ai[1] >= 0f ? 1f : -1f;
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 22;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>在场存在感 0~1:出生渐起,末段渐没(亮度与判定同源)</summary>
        private float Presence =>
            MathHelper.Clamp(Timer / WarmupFrames, 0f, 1f)
            * MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);

        public override void AI() {
            Timer++;
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            bool ownerAlive = owner != null && owner.active && owner.type == NPCID.CultistBoss;
            if (!ownerAlive) {
                Projectile.Kill();
                return;
            }

            //场心引力:速度向切向缓靠+轻微向内,画出贴黄道内壁的大弧
            Vector2 arena = Projectile.Center;
            if (owner.TryGetOverride(out CultistBossAI overrideAI) && overrideAI.Context is { ArenaSpawned: true } ctx) {
                arena = ctx.ArenaCenter;
            }
            Vector2 toCenter = arena - Projectile.Center;
            float dist = MathHelper.Max(toCenter.Length(), 1f);
            Vector2 radial = toCenter / dist;
            Vector2 tangent = radial.RotatedBy(OrbitDir * MathHelper.PiOver2);
            //速度复利有顶:拒绝匀速直线
            float speed = MathHelper.Min(8f + Timer * 0.032f, 15.5f);
            Vector2 desired = (tangent + radial * 0.12f).SafeNormalize(Vector2.UnitX) * speed;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.045f);
            Projectile.rotation = Projectile.velocity.ToRotation();

            //沿途星屑剥落
            if (Timer % 9 == 0 && Presence > 0.5f) {
                CultistMotion.RuneBurst(Projectile.Center - Projectile.velocity * 1.5f,
                    CultistMotion.PhaseCore(PaletteOf(owner)), 1, 2.5f);
            }

            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(PaletteOf(owner)).ToVector3() * 0.5f * Presence);
        }

        private static int PaletteOf(NPC owner) => owner != null && owner.active ? (int)owner.ai[0] : 0;

        /// <summary>伤害窗=亮度窗:暖起前与淡出尾都不咬人</summary>
        public override bool CanHitPlayer(Player target) => Presence > 0.85f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = HitRadius * Projectile.scale;
            Vector2 center = Projectile.Center;
            Vector2 closest = new(
                MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(center, closest) < radius * radius;
        }

        public override void OnKill(int timeLeft) {
            //余痕:散作火花残辉,活过弹体
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            CultistMotion.ImpactBurst(Projectile.Center, CultistMotion.PhaseLegacyElement(PaletteOf(owner)), 0.6f, false);
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            int palette = PaletteOf(owner);
            Color mid = CultistMotion.PhaseCore(palette);
            Color edge = CultistMotion.PhaseEdge(palette);
            Color bright = Color.Lerp(mid, Color.White, 0.5f);
            Color deep = Color.Lerp(edge, Color.Black, 0.5f);
            float presence = Presence;
            if (presence <= 0.02f) {
                return false;
            }

            //彗尾:回溯位置折线条带,同料同色,头宽≈头核 0.7 倍(横轴幅度契约)
            int count = 0;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    break;
                }
                count++;
            }
            if (count >= 3) {
                Vector2[] pts = new Vector2[count];
                float[] widths = new float[count];
                float[] alphas = new float[count];
                for (int i = 0; i < count; i++) {
                    float t = 1f - i / (float)count;
                    pts[i] = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    widths[i] = MathHelper.Lerp(2.5f, 16f, t * t) * presence;
                    alphas[i] = t * 0.9f * presence;
                }
                SpriteBatch sbStrip = Main.spriteBatch;
                sbStrip.End();
                Rendering.CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                    deep, mid, bright, 1f, 0f, 0.35f * presence, Projectile.identity % 100 * 0.083f, presence);
                sbStrip.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            //头体:星芒三层(暗缘承剪影),沿速度轻拉伸由旋转涂抹表达
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float spin = Projectile.rotation + Timer * 0.11f;
            Rendering.CultistOrreryRenderer.DrawStarBead(sb, drawPos - Projectile.velocity * 0.6f,
                mid, edge, 0.30f * presence, 0.45f * presence, spin - 0.4f);
            Rendering.CultistOrreryRenderer.DrawStarBead(sb, drawPos, mid, edge,
                0.42f * presence, presence, spin);
            return false;
        }
    }
}
