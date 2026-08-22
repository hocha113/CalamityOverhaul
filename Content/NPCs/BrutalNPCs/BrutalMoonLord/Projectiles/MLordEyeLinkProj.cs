using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
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
    /// 真眼链式死光：两只真眼之间的有限长弦光束（集群组合技的接力边/星芒弦）。
    /// 两端点每帧读活体位置，编队缓旋时弦随之转动，视觉永不脱锚。
    /// ai[0]=源真眼 whoAmI，ai[1]=靶真眼 whoAmI，ai[2]=预警帧数。
    /// 端点任一失效则快进收束
    /// </summary>
    internal class MLordEyeLinkProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int BurstTime = 22;
        internal const int FadeTime = 12;
        internal const float MaxWidth = 58f;

        private ref float Timer => ref Projectile.localAI[0];
        private int TelegraphTime => (int)Projectile.ai[2];
        private NPC Source => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private NPC Target => ((int)Projectile.ai[1]).TryGetNPC(out NPC n) ? n : null;

        private float beamWidth;
        /// <summary>弦长（每帧由端点重算，端点失效时沿用上帧）</summary>
        private float chordLength;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            NPC source = Source;
            NPC target = Target;
            bool anchored = source.Alives() && target.Alives()
                && source.type == NPCID.MoonLordFreeEye && target.type == NPCID.MoonLordFreeEye;

            if (anchored) {
                Vector2 chord = target.Center - source.Center;
                Projectile.Center = source.Center;
                Projectile.rotation = chord.ToRotation();
                chordLength = chord.Length();
            }
            else if (Timer < TelegraphTime + BurstTime) {
                //端点失效：快进收束（沿用上帧几何走完淡出）
                Timer = TelegraphTime + BurstTime;
            }

            int telegraph = TelegraphTime;
            float t = Timer;

            if (t < telegraph) {
                beamWidth = 0f;
            }
            else if (t < telegraph + BurstTime) {
                float p = (t - telegraph) / 6f;
                beamWidth = MaxWidth * MathHelper.Clamp(1f - (1f - p) * (1f - p) * (1f - p), 0f, 1f);
                if ((int)t == telegraph && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.6f, Pitch = 0.5f, MaxInstances = 6 }, Projectile.Center);
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
            if ((int)Timer == telegraph - 14 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.4f, Pitch = 0.7f, MaxInstances = 6 }, Projectile.Center);
            }

            if (beamWidth > 8f && chordLength > 8f) {
                Vector2 dir = Projectile.rotation.ToRotationVector2();
                for (int i = 0; i < 3; i++) {
                    Lighting.AddLight(Projectile.Center + dir * (chordLength / 3f * i),
                        MLordDirector.Phantasmal.ToVector3() * 0.5f);
                }
                //沿弦星尘（客户端）
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    Vector2 pos = Projectile.Center + dir * chordLength * Main.rand.NextFloat()
                        + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.4f, beamWidth * 0.4f);
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(pos,
                        dir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(1.5f, 4.5f),
                        MLordDirector.Phantasmal, Main.rand.NextFloat(0.4f, 0.75f))?.Configure(false, Main.rand.Next(10, 16));
                }
            }
        }

        public override bool? CanDamage() => Timer > TelegraphTime + 3 && beamWidth > MaxWidth * 0.4f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (chordLength <= 8f) {
                return false;
            }
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * chordLength,
                beamWidth * 0.62f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Timer < TelegraphTime || chordLength <= 8f) {
                return;
            }
            MLordRayRender.DrawBeam(Projectile.Center, Projectile.rotation, chordLength, beamWidth,
                MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f), Projectile.whoAmI * 0.137f % 1f);
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (chordLength <= 8f) {
                return;
            }
            if (Timer < TelegraphTime) {
                float strength = MathHelper.Clamp(Timer / (float)System.Math.Max(TelegraphTime - 8, 1), 0f, 1f);
                MLordRayRender.DrawGuideLine(Projectile.Center, Projectile.rotation, chordLength, strength, additiveBatch: true);
                return;
            }
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            //两端各压一枚口部光核（弦两头都有主人）
            MLordRayRender.DrawMuzzle(Projectile.Center, beamWidth / MaxWidth * 0.8f, opacity, additiveBatch: true);
            MLordRayRender.DrawMuzzle(Projectile.Center + Projectile.rotation.ToRotationVector2() * chordLength,
                beamWidth / MaxWidth * 0.8f, opacity, additiveBatch: true);
        }
    }
}
