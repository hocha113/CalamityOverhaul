using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums.Disciples
{
    /// <summary>
    /// 马太·税吏(席位7)：财富祝福。
    /// 把祝福金印送到未受福的敌人身上，受福者死亡时迸出奉献银币与金雨伤害
    /// (兑现在 <see cref="ElysiumGlobalNPC.OnKill"/>)
    /// </summary>
    internal class Matthew : BaseDisciple
    {
        public override int Seat => 7;

        private const float CastRange = 520f;
        private const int MaxBless = 3;

        protected override bool TryCast() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy(Projectile)
                    && !npc.HasBuff<WealthBlessingDebuff>()
                    && Vector2.Distance(npc.Center, Projectile.Center) < CastRange) {
                    return true;
                }
            }
            return false;
        }

        protected override void ExecuteAbility() {
            SoundEngine.PlaySound(SoundID.CoinPickup with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int sent = 0;
            int damage = (int)(ElysiumPlayer.GetElysiumDamage(Owner) * 0.2f);
            for (int i = 0; i < Main.maxNPCs && sent < MaxBless; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !npc.CanBeChasedBy(Projectile)
                    || npc.HasBuff<WealthBlessingDebuff>()
                    || Vector2.Distance(npc.Center, Projectile.Center) >= CastRange) {
                    continue;
                }
                Vector2 vel = (npc.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 8f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                    ModContent.ProjectileType<DiscipleSigilBolt>(), damage, 1f, Projectile.owner, 1, i);
                sent++;
            }
        }
    }

    /// <summary>奉献金雨：受福者死亡时的金币迸发，一瞬的圆域伤害与四溅金光</summary>
    internal class MatthewCoinBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const float BurstRadius = 130f;
        private const int Life = 22;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.timeLeft != Life - 1 || Main.dedServ) {
                return;
            }
            //迸发瞬间：金币星四溅 + 上抛金光
            SoundEngine.PlaySound(SoundID.CoinPickup with { Volume = 1f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center
                    , angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f)
                    , new Color(255, 226, 120), Main.rand.NextFloat(0.6f, 1f))?.Configure(true, Main.rand.Next(16, 26));
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(3f, 7f));
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, vel
                    , new Color(255, 238, 160), Main.rand.NextFloat(0.25f, 0.42f))?.Configure(Main.rand.Next(20, 32), 0.9f);
            }
        }

        /// <summary>圆域一次性判定，只在前几帧开窗</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Projectile.timeLeft < Life - 6) {
                return false;
            }
            Vector2 nearest = new(MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right)
                , MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(Projectile.Center, nearest) <= BurstRadius;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float prog = 1f - Projectile.timeLeft / (float)Life;
            float fade = 1f - prog;
            Main.spriteBatch.Draw(glow, Projectile.Center - Main.screenPosition, null
                , new Color(255, 226, 130) with { A = 0 } * (0.7f * fade), 0f
                , glow.Size() / 2f, 0.5f + prog * 0.5f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
