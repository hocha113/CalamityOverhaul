using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 十二宫辐条:黄道环刻痕亮起充能,自环缘向环心点燃一道星力封条<br/>
    /// ai[0]=宿主npc ai[1]=基角 ai[2]=进动速率(rad/f,带符号,全组同源)<br/>
    /// 公平阀:80 帧宫位充能+虚线预告(与点燃同参同角);伤害窗=点燃可见窗;
    /// 判定宽 30 藏于亮体;辐条内端止步 InnerClear,场心恒为通路;
    /// 进动是常量匀速(整组刚体旋转),选宫时已排除玩家所在扇区,安全扇随组同转不塌
    /// </summary>
    internal class CultistZodiacSpokeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int WarnFrames = 80;
        internal const int FireFrames = 110;
        internal const int FadeFrames = 18;
        internal const int Lifetime = WarnFrames + FireFrames + FadeFrames;
        /// <summary>内端净空:辐条不扎到场心,中央恒为走位通路</summary>
        private const float InnerClear = 300f;
        /// <summary>判定半宽:窄于点燃亮体(可见半宽 26+软缘)</summary>
        private const float HitHalfWidth = 30f;

        private int OwnerWho => (int)Projectile.ai[0];
        private float BaseAngle => Projectile.ai[1];
        private float DriftRate => Projectile.ai[2];
        private float Age => Lifetime - Projectile.timeLeft;
        private float CurrentAngle => BaseAngle + DriftRate * Age;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>场心解析:宿主上下文,拿不到就散(各端 ArenaCenter 由黄道环同步兜底)</summary>
        private bool TryGetArena(out Vector2 arena) {
            arena = Vector2.Zero;
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss) {
                return false;
            }
            if (owner.TryGetOverride(out CultistBossAI overrideAI) && overrideAI.Context is { ArenaSpawned: true } ctx) {
                arena = ctx.ArenaCenter;
                return true;
            }
            return false;
        }

        private static int PaletteOf(int ownerWho) {
            NPC owner = ownerWho >= 0 && ownerWho < Main.maxNPCs ? Main.npc[ownerWho] : null;
            return owner != null && owner.active ? (int)owner.ai[0] : 0;
        }

        public override void AI() {
            if (!TryGetArena(out Vector2 arena)) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = arena;
            Projectile.velocity = Vector2.Zero;
            float age = Age;

            //点燃拍(各端本地推)
            if ((int)age == WarnFrames) {
                Vector2 rim = arena + CurrentAngle.ToRotationVector2() * CultistStateContext.ArenaRadius;
                CultistMotion.SigilCommitFX(rim, CultistMotion.PhaseCore(PaletteOf(OwnerWho)), 1.2f);
                CultistMotion.Shake(rim, 4f, 10, CurrentAngle.ToRotationVector2());
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = -0.2f }, rim);
                }
            }

            Lighting.AddLight(arena + CurrentAngle.ToRotationVector2() * CultistStateContext.ArenaRadius * 0.5f,
                CultistMotion.PhaseCore(PaletteOf(OwnerWho)).ToVector3() * 0.4f);
        }

        /// <summary>伤害窗=点燃可见窗</summary>
        public override bool CanHitPlayer(Player target) {
            float age = Age;
            return age >= WarnFrames && age < WarnFrames + FireFrames;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float age = Age;
            if (age < WarnFrames || age >= WarnFrames + FireFrames) {
                return false;
            }
            if (!TryGetArena(out Vector2 arena)) {
                return false;
            }
            Vector2 dir = CurrentAngle.ToRotationVector2();
            Vector2 start = arena + dir * CultistStateContext.ArenaRadius;
            Vector2 end = arena + dir * InnerClear;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, HitHalfWidth, ref point);
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            behindNPCs.Add(index);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!TryGetArena(out Vector2 arena)) {
                return false;
            }
            int palette = PaletteOf(OwnerWho);
            Color mid = CultistMotion.PhaseCore(palette);
            Color bright = Color.Lerp(mid, Color.White, 0.5f);
            Color deep = Color.Lerp(CultistMotion.PhaseEdge(palette), Color.Black, 0.45f);
            float age = Age;

            Vector2 dir = CurrentAngle.ToRotationVector2();
            Vector2 rim = arena + dir * CultistStateContext.ArenaRadius - Main.screenPosition;
            Vector2 inner = arena + dir * InnerClear - Main.screenPosition;

            float warnT = MathHelper.Clamp(age / WarnFrames, 0f, 1f);
            bool firing = age >= WarnFrames && age < WarnFrames + FireFrames;
            float fade = MathHelper.Clamp(1f - (age - WarnFrames - FireFrames) / FadeFrames, 0f, 1f);
            //点燃展开:5 帧自环缘向内窜满
            float igniteT = firing ? MathHelper.Clamp((age - WarnFrames) / 5f, 0f, 1f) : 0f;

            float halfWidth = firing ? MathHelper.Lerp(9f, 26f, igniteT) : 7f + warnT * 3f;
            float alpha = (firing ? 0.95f : 0.30f + warnT * 0.25f) * fade;
            float dash = firing ? 0f : 13f;
            float charge = firing ? 0.85f : warnT * 0.5f;

            //根在环缘、尖向环心:宽度与透明度沿线收尾(内端不平切)
            Vector2[] pts = [rim, Vector2.Lerp(rim, inner, 0.72f), inner];
            float[] widths = [halfWidth, halfWidth * 0.86f, halfWidth * 0.55f];
            float[] alphas = [alpha, alpha * 0.9f, alpha * 0.42f];

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            Rendering.CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                deep, mid, bright, firing ? igniteT : 1f, dash, charge,
                Projectile.identity % 100 * 0.091f, MathHelper.Min(alpha + 0.15f, 1f));
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //宫位刻痕充能:环缘星标随充能涨亮,是"哪几宫要封"的第一预告
            float notchPulse = firing ? 1.25f : 0.5f + warnT * 0.75f
                + (float)System.Math.Sin(age * 0.35f) * 0.08f * warnT;
            Rendering.CultistOrreryRenderer.DrawStarBead(sb, rim, mid, CultistMotion.PhaseEdge(palette),
                0.16f + 0.16f * notchPulse, (0.5f + 0.5f * warnT) * fade,
                Main.GlobalTimeWrappedHourly * 1.4f + BaseAngle);
            return false;
        }
    }
}
