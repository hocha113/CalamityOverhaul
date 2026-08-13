using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 扫描线死光：自带细线预警→短促成束→收线。
    /// ai[0]=宿主部件 whoAmI，ai[1]=角度，ai[2]=预警帧数（允许逐束不同）
    /// </summary>
    internal class MLordScanRayProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int BurstTime = 24;
        internal const int FadeTime = 12;
        internal const float BeamLength = 4200f;
        internal const float MaxWidth = 86f;

        private ref float Timer => ref Projectile.localAI[0];
        private int TelegraphTime => (int)Projectile.ai[2];
        private NPC Host => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;

        private float beamWidth;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            NPC host = Host;
            //宿主锚定；失效则原地走完
            if (host.Alives()) {
                Projectile.Center = host.Center + Projectile.rotation.ToRotationVector2() * 30f;
            }
            Projectile.rotation = Projectile.ai[1];

            int telegraph = TelegraphTime;
            float t = Timer;

            if (t < telegraph) {
                beamWidth = 0f;
            }
            else if (t < telegraph + BurstTime) {
                //成束：poly 陡峭出束
                float p = (t - telegraph) / 6f;
                beamWidth = MaxWidth * MathHelper.Clamp(1f - (1f - p) * (1f - p) * (1f - p), 0f, 1f);
                if ((int)t == telegraph && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.75f, Pitch = 0.35f, MaxInstances = 5 }, Projectile.Center);
                    MLordScreenFX.Punch(Projectile.Center, 3f, 8);
                }
            }
            else {
                float p = (t - telegraph - BurstTime) / FadeTime;
                beamWidth = MaxWidth * MathHelper.Clamp(1f - p, 0f, 1f) * 0.8f;
            }

            Timer++;
            if (Timer >= telegraph + BurstTime + FadeTime) {
                Projectile.Kill();
                return;
            }

            //预警末拍提示音
            if ((int)Timer == telegraph - 18 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.5f, Pitch = 0.6f, MaxInstances = 5 }, Projectile.Center);
            }

            if (beamWidth > 8f) {
                Vector2 dir = Projectile.rotation.ToRotationVector2();
                for (int i = 0; i < 5; i++) {
                    Lighting.AddLight(Projectile.Center + dir * (BeamLength / 5f * i),
                        MLordDirector.Phantasmal.ToVector3() * 0.6f);
                }
                //沿束星尘（客户端）
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    float along = Main.rand.NextFloat();
                    Vector2 pos = Projectile.Center + dir * BeamLength * along
                        + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.4f, beamWidth * 0.4f);
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(pos,
                        dir.RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f)) * Main.rand.NextFloat(2f, 6f),
                        MLordDirector.Phantasmal, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
        }

        public override bool? CanDamage() => Timer > TelegraphTime + 3 && beamWidth > MaxWidth * 0.4f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * BeamLength,
                beamWidth * 0.62f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Timer < TelegraphTime) {
                return;
            }
            MLordRayRender.DrawBeam(Projectile.Center, Projectile.rotation, BeamLength, beamWidth,
                MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f), Projectile.whoAmI * 0.173f % 1f);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (Timer < TelegraphTime) {
                float strength = MathHelper.Clamp(Timer / (float)System.Math.Max(TelegraphTime - 8, 1), 0f, 1f);
                MLordRayRender.DrawGuideLine(Projectile.Center, Projectile.rotation, BeamLength, strength, additiveBatch: true);
                return;
            }
            MLordRayRender.DrawMuzzle(Projectile.Center, beamWidth / MaxWidth,
                MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f), additiveBatch: true);
        }
    }
}
