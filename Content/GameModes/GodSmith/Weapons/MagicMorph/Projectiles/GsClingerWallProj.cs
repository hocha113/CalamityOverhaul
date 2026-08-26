using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles
{
    /// <summary>
    /// 爬藤怪法杖 B 形态「诅咒藤墙」：沿部署方向铺 260px 线形咒火墙，驻场 8s，可斜置。<br/>
    /// ai[0]=墙轴角度，ai[1]=1 时两端各立 60px 垂直裙墙（满蓄奖励）；
    /// 判定为主线 + 裙墙的分段线带（宽 24px），命中叠诅咒焰
    /// </summary>
    internal class GsClingerWallProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeTicks = 480;
        private const float HalfLength = 130f;
        private const float SkirtLength = 60f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.timeLeft = LifeTicks;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override bool ShouldUpdatePosition() => false;

        private Vector2 Axis => Projectile.ai[0].ToRotationVector2();

        /// <summary>裙墙垂直向量（取指向世界上方的一支）</summary>
        private Vector2 SkirtUp {
            get {
                Vector2 perp = Axis.RotatedBy(MathHelper.PiOver2);
                return perp.Y <= 0f ? perp : -perp;
            }
        }

        private bool HasSkirt => Projectile.ai[1] >= 1f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 a = Projectile.Center - Axis * HalfLength;
            Vector2 b = Projectile.Center + Axis * HalfLength;
            float _ = 0f;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), a, b, 24f, ref _)) {
                return true;
            }
            if (HasSkirt) {
                Vector2 up = SkirtUp * SkirtLength;
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), a, a + up, 24f, ref _)
                    || Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), b, b + up, 24f, ref _)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.CursedInferno, 180);

        public override void AI() {
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }
            if (VaultUtils.isServer) {
                return;
            }
            //沿线咒火（≤3/帧）：主线 2 粒，裙墙轮换 1 粒
            Vector2 a = Projectile.Center - Axis * HalfLength;
            Vector2 b = Projectile.Center + Axis * HalfLength;
            for (int i = 0; i < 2; i++) {
                Vector2 pos = Vector2.Lerp(a, b, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_HellFlame>(pos + Main.rand.NextVector2Circular(6f, 6f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f),
                    new Color(128, 230, 76), Main.rand.NextFloat(0.35f, 0.6f));
            }
            if (HasSkirt && Projectile.timeLeft % 2 == 0) {
                Vector2 root = Main.rand.NextBool() ? a : b;
                Vector2 pos = root + SkirtUp * Main.rand.NextFloat(SkirtLength);
                PRTLoader.NewParticle<PRT_HellFlame>(pos, -Vector2.UnitY * 0.6f,
                    new Color(150, 240, 90), Main.rand.NextFloat(0.3f, 0.5f));
            }
            Lighting.AddLight(Projectile.Center, 0.12f, 0.3f, 0.08f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float fade = MathHelper.Clamp((LifeTicks - Projectile.timeLeft) / 10f, 0f, 1f)
                * MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
            Vector2 a = Projectile.Center - Axis * HalfLength;
            Vector2 b = Projectile.Center + Axis * HalfLength;
            DrawWallSegment(glow, a, b, fade, 0.5f);
            if (HasSkirt) {
                Vector2 up = SkirtUp * SkirtLength;
                DrawWallSegment(glow, a, a + up, fade, 0.34f);
                DrawWallSegment(glow, b, b + up, fade, 0.34f);
            }
            return false;
        }

        private static void DrawWallSegment(Texture2D glow, Vector2 from, Vector2 to, float fade, float strength) {
            Vector2 mid = (from + to) * 0.5f;
            float len = from.Distance(to);
            float rot = (to - from).ToRotation();
            //咒火绿双层：宽鞘 + 亮芯（A=0 加色安全）
            Color sheath = new Color(80, 190, 60) * (strength * fade);
            sheath.A = 0;
            Main.EntitySpriteDraw(glow, mid - Main.screenPosition, null, sheath, rot,
                glow.Size() / 2f, new Vector2(len / glow.Width * 1.1f, 0.5f),
                Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
            Color core = new Color(190, 255, 130) * (strength * 0.7f * fade);
            core.A = 0;
            Main.EntitySpriteDraw(glow, mid - Main.screenPosition, null, core, rot,
                glow.Size() / 2f, new Vector2(len / glow.Width, 0.2f),
                Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
        }
    }
}
