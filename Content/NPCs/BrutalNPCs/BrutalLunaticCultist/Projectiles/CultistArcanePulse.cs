using CalamityOverhaul.Common;
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
    /// 奥术新星脉冲:自司祭放出的扩散符环,匀速外扩,环上有一段声明缺口<br/>
    /// ai[0]=宿主npc ai[1]=缺口中心角(rad,出手即锁死) ai[2]=阶段色<br/>
    /// 公平阀:GapHalfAngle 声明缺口(判定与绘制同参,可见开口略窄于安全区=对玩家宽容);<br/>
    /// 扩速 Speed 恒定可外跑;出生 WarmFrames 无伤(环还贴着司祭)
    /// </summary>
    internal class CultistArcanePulse : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>声明缺口半角(rad):判定跳过与绘制开口同参;须与新星态 GapStep 保持 2×半角&gt;步进(门扇区重叠)</summary>
        internal const float GapHalfAngle = 0.55f;
        /// <summary>扩散速度(px/f):恒定,向外跑或穿缺口都躲得掉</summary>
        internal const float Speed = 11f;
        /// <summary>出生无伤窗(帧)</summary>
        internal const int WarmFrames = 9;
        private const float StartRadius = 52f;
        private const float MaxRadius = 1150f;
        private const int FadeFrames = 12;
        /// <summary>判定带半宽:含中心距余量后(+8)仍窄于可见亮带,线没压到身上不咬人(旧 24+18 宽于可见=被空气打)</summary>
        private const float BandHalf = 20f;
        private const int Lifetime = 112;

        private int OwnerWho => (int)Projectile.ai[0];
        private float GapCenter => Projectile.ai[1];
        private int Palette => (int)Projectile.ai[2];
        private float Age => Lifetime - Projectile.timeLeft;
        private float Radius => MathHelper.Min(StartRadius + Age * Speed, MaxRadius);
        private float FadeOut => MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);

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

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss) {
                Projectile.Kill();
                return;
            }
            //满径即提前收势(寿命兜底)
            if (Radius >= MaxRadius && Projectile.timeLeft > FadeFrames) {
                Projectile.timeLeft = FadeFrames;
            }
            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(Palette).ToVector3() * 0.35f * FadeOut);
        }

        /// <summary>伤害窗:出生暖场后、淡出前</summary>
        public override bool CanHitPlayer(Player target) {
            return Age > WarmFrames && Projectile.timeLeft > FadeFrames;
        }

        /// <summary>环带判定:半径带内且不在声明缺口扇区</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Age <= WarmFrames || Projectile.timeLeft <= FadeFrames) {
                return false;
            }
            Vector2 delta = targetHitbox.Center.ToVector2() - Projectile.Center;
            float dist = delta.Length();
            if (Math.Abs(dist - Radius) > BandHalf + 8f) {
                return false;
            }
            float angDelta = Math.Abs(MathHelper.WrapAngle(delta.ToRotation() - GapCenter));
            return angDelta > GapHalfAngle;
        }

        public override bool PreDraw(ref Color lightColor) {
            Color mid = CultistMotion.PhaseCore(Palette);
            Color bright = Color.Lerp(mid, Color.White, 0.5f);
            Color deep = Color.Lerp(CultistMotion.PhaseEdge(Palette), Color.Black, 0.45f);
            float radius = Radius;
            float fade = FadeOut;
            float bornIn = MathHelper.Clamp(Age / 6f, 0f, 1f);
            float alpha = bornIn * fade;
            if (alpha <= 0.02f) {
                return false;
            }

            //闭环折线:缺口段透明度压零(可见开口=安全区,收窄 0.74 倍=对玩家宽容)
            const int Segs = 72;
            Vector2[] pts = new Vector2[Segs + 1];
            float[] widths = new float[Segs + 1];
            float[] alphas = new float[Segs + 1];
            for (int i = 0; i <= Segs; i++) {
                float angle = i / (float)Segs * MathHelper.TwoPi;
                pts[i] = Projectile.Center + angle.ToRotationVector2() * radius - Main.screenPosition;
                widths[i] = 15f;
                float delta = Math.Abs(MathHelper.WrapAngle(angle - GapCenter));
                float open = MathHelper.Clamp((delta - GapHalfAngle * 0.74f) / (GapHalfAngle * 0.26f), 0f, 1f);
                alphas[i] = alpha * open;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                deep, mid, bright, 1f, 12f, 0.5f * fade, Projectile.identity % 100 * 0.083f, alpha);
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //缺口端标:两颗星珠钉出安全门的门框
            for (int side = -1; side <= 1; side += 2) {
                float edgeAngle = GapCenter + GapHalfAngle * side;
                Vector2 pos = Projectile.Center + edgeAngle.ToRotationVector2() * radius - Main.screenPosition;
                CultistOrreryRenderer.DrawStarBead(sb, pos, mid, CultistMotion.PhaseEdge(Palette),
                    0.20f, 0.9f * alpha, Main.GlobalTimeWrappedHourly * 1.8f + side);
            }
            return false;
        }
    }
}
