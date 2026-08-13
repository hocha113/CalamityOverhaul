using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>忍者斩波：短距快斩，2~9帧判定；ai[0]=斩向弧度 ai[1]=连段序号；服务端生成</summary>
    internal class BKSNinjaSlashProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int MaxLife = 16;
        private const float SlashLength = 230f;
        private const float SlashHalfWidth = 34f;

        private float SlashDir => Projectile.ai[0];
        private int ComboIndex => (int)Projectile.ai[1];

        private float Progress => 1f - Projectile.timeLeft / (float)MaxLife;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                SoundEngine.PlaySound(SoundID.Item71 with {
                    Pitch = 0.2f + ComboIndex * 0.12f, Volume = 0.85f, MaxInstances = 4
                }, Projectile.Center);
            }
            Projectile.rotation = SlashDir;
        }

        //斩击判定只在挥出帧
        public override bool? CanDamage() {
            int t = MaxLife - Projectile.timeLeft;
            return t is >= 2 and <= 9 ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            Vector2 dir = SlashDir.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - dir * SlashLength * 0.2f,
                Projectile.Center + dir * SlashLength * 0.8f,
                SlashHalfWidth * 2f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D slash = CWRUtils.GetT2DAsset(CWRConstant.Masking + "SlashJagged01").Value;
            Texture2D smear = CWRUtils.GetT2DAsset(CWRConstant.Masking + "CrescentSoft01").Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float t = Progress;

            //快出缓收：前30%冲到全尺寸，之后拖长淡出
            float grow = t < 0.3f ? VaultUtils.EaseOutCubic(t / 0.3f) : 1f;
            float fade = t < 0.3f ? 1f : 1f - VaultUtils.EaseInQuad((t - 0.3f) / 0.7f);

            Vector2 scale = new Vector2(SlashLength / slash.Width * grow, 0.55f + t * 0.15f);
            Color steel = new Color(205, 228, 255, 0);
            Color coldCore = new Color(240, 250, 255, 0);

            //软拖影垫底
            Main.EntitySpriteDraw(smear, pos, null, steel * (0.35f * fade), SlashDir,
                smear.Size() * 0.5f, scale * new Vector2(1.1f, 1.9f), SpriteEffects.None, 0);
            //锯齿刃光主体
            Main.EntitySpriteDraw(slash, pos, null, steel * (0.85f * fade), SlashDir,
                slash.Size() * 0.5f, scale, SpriteEffects.None, 0);
            //白冷芯，只在挥出帧
            if (t < 0.45f) {
                Main.EntitySpriteDraw(slash, pos, null, coldCore * (0.8f * (1f - t / 0.45f)), SlashDir,
                    slash.Size() * 0.5f, scale * new Vector2(0.92f, 0.55f), SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //斩末几点钢星
            Vector2 dir = SlashDir.ToRotationVector2();
            for (int i = 0; i < 3; i++) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGoldSpark>(
                    Projectile.Center + dir * Main.rand.NextFloat(30f, 150f),
                    dir.RotatedByRandom(0.6) * Main.rand.NextFloat(2f, 5f),
                    new Color(190, 220, 255), Main.rand.NextFloat(0.6f, 1f))?.Configure(12);
            }
        }
    }
}
