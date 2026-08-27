using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 坠星:天穹标记落点后垂直坠下的星矢(坠星祷用)<br/>
    /// ai[0]=宿主npc ai[1]=阶段色 ai[2]=个性种子(0..1,预告延展/落速/体型各端同步派生)<br/>
    /// 公平阀:预告柱(与坠道同线同宽,预告即坠道);落点出手拍锁定不追踪;<br/>
    /// 伤害窗=坠落提速后(预告与起步段无伤);判定半宽恒定不随体型变
    /// </summary>
    internal class CultistFallingStar : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>预告帧下限:坠道虚线+落标星,足够横移一条巷</summary>
        internal const int WarnFrames = 26;
        /// <summary>错拍延展上限(帧):每星预告在下限上再加 0..此值,打破整波齐落</summary>
        private const int MaxWarnExtra = 10;
        private const int FallFrames = 128;
        private const int Lifetime = WarnFrames + MaxWarnExtra + FallFrames;
        /// <summary>坠道长(px):预告线与坠程同长</summary>
        private const float LaneLength = 1900f;
        private const float FallAccel = 1.05f;
        private const float MaxFallSpeed = 24f;
        /// <summary>判定半宽:藏于星矢亮体(最小体型下仍藏得住)</summary>
        private const float HitHalf = 20f;

        private int OwnerWho => (int)Projectile.ai[0];
        private int Palette => (int)Projectile.ai[1];
        private float Age => Lifetime - Projectile.timeLeft;

        /// <summary>个性种子(0..1):随生成包同步,各端派生一致</summary>
        private float Seed => Projectile.ai[2];
        private static float Hash(float seed, float mul) => seed * mul % 1f;
        /// <summary>本星预告总帧:下限+错拍延展</summary>
        private int WarnTotal => WarnFrames + (int)(Seed * (MaxWarnExtra + 1f));
        private float AccelMul => 0.92f + Hash(Seed, 7.31f) * 0.26f;
        private float SpeedMul => 0.9f + Hash(Seed, 13.7f) * 0.22f;
        /// <summary>体型系数:纯视觉,判定不随之变</summary>
        private float BodyScale => 0.85f + Hash(Seed, 29.3f) * 0.35f;

        /// <summary>坠道起点(出生位,预告线自此向下)</summary>
        private Vector2 spawnPos;
        private bool spawnCached;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (!spawnCached) {
                spawnPos = Projectile.Center;
                spawnCached = true;
            }
            NPC owner = OwnerWho >= 0 && OwnerWho < Main.maxNPCs ? Main.npc[OwnerWho] : null;
            if (owner == null || !owner.active || owner.type != NPCID.CultistBoss) {
                Projectile.Kill();
                return;
            }
            float age = Age;
            int warnTotal = WarnTotal;

            if (age < warnTotal) {
                //预告:悬停微颤,坠道虚线渐亮
                Projectile.velocity = Vector2.Zero;
                Projectile.Center = spawnPos + new Vector2((float)System.Math.Sin(age * 0.7f + Projectile.identity) * 3f, 0f);
            }
            else {
                //坠落:重加速,拒绝匀速;落速吃种子差异,同波不齐步
                if ((int)age == warnTotal && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.7f, Pitch = -0.3f + Seed * 0.25f }, Projectile.Center);
                }
                Projectile.velocity.X = 0f;
                Projectile.velocity.Y = MathHelper.Min(Projectile.velocity.Y + FallAccel * AccelMul, MaxFallSpeed * SpeedMul);
                //坠道走完即散
                if (Projectile.Center.Y > spawnPos.Y + LaneLength) {
                    Projectile.Kill();
                    return;
                }
            }
            Projectile.rotation = MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, CultistMotion.PhaseCore(Palette).ToVector3() * 0.4f);
        }

        /// <summary>伤害窗=坠落起速后(预告柱只是光)</summary>
        public override bool CanHitPlayer(Player target) {
            return Age > WarnTotal + 3 && Projectile.velocity.Y > 6f;
        }

        /// <summary>胶囊判定:沿坠向的短刃(高速防穿帧)</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Age <= WarnTotal + 3) {
                return false;
            }
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - Projectile.velocity * 1.4f, Projectile.Center + Projectile.velocity * 0.4f,
                HitHalf, ref point);
        }

        public override void OnKill(int timeLeft) {
            //余痕:落点炸开星屑,活过弹体
            CultistMotion.ImpactBurst(Projectile.Center, CultistMotion.PhaseLegacyElement(Palette), 0.7f, false);
            CultistMotion.RuneBurst(Projectile.Center, CultistMotion.PhaseCore(Palette), 3, 5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Color mid = CultistMotion.PhaseCore(Palette);
            Color edge = CultistMotion.PhaseEdge(Palette);
            Color bright = Color.Lerp(mid, Color.White, 0.5f);
            Color deep = Color.Lerp(edge, Color.Black, 0.45f);
            float age = Age;
            int warnTotal = WarnTotal;
            float seed = Projectile.identity % 100 * 0.077f;

            SpriteBatch sb = Main.spriteBatch;
            sb.End();

            if (age < warnTotal) {
                //预告柱:自落标向下的星屑虚线,渐亮渐宽(与坠道同线)
                float warnT = age / warnTotal;
                Vector2 top = (spawnCached ? spawnPos : Projectile.Center) - Main.screenPosition;
                Vector2[] pts = [top, top + new Vector2(0f, LaneLength)];
                float[] widths = [7f + warnT * 4f, 5f + warnT * 3f];
                float[] alphas = [0.30f + warnT * 0.35f, 0.18f + warnT * 0.25f];
                CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                    deep, mid, bright, 1f, 15f, warnT * 0.6f, seed, 0.6f + warnT * 0.4f);
            }
            else {
                //坠尾:回溯条带,同料同色
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
                        widths[i] = MathHelper.Lerp(3f, 14f, t * t) * BodyScale;
                        alphas[i] = t * 0.85f;
                    }
                    CultistOrreryRenderer.DrawTechniqueStrip("TechStarLine", pts, widths, alphas,
                        deep, mid, bright, 1f, 0f, 0.4f, seed, 1f);
                }
            }

            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //星矢头体/落标星:预告期在顶端脉动,坠落期是头核;体型吃种子差异
            float pulse = age < warnTotal
                ? 0.16f + age / warnTotal * 0.14f + (float)System.Math.Sin(age * 0.5f) * 0.03f
                : 0.34f;
            CultistOrreryRenderer.DrawStarBead(sb, Projectile.Center - Main.screenPosition, mid, edge,
                pulse * BodyScale, 1f, Main.GlobalTimeWrappedHourly * 2.4f + Projectile.identity);
            return false;
        }
    }
}
