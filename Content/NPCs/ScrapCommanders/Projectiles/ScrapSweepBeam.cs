using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders.Projectiles
{
    /// <summary>
    /// 镭射扫削射线：30 帧虚线预扫（无伤害，展示完整弧程）→
    /// 同轨迹 30 帧热射线回扫。锚在统帅镭射臂口，角度是本地计时的确定性函数。
    /// ai[0]=统帅 whoAmI，ai[1]=扫向 ±1；生成时的 velocity 携带瞄准向量
    /// </summary>
    internal class ScrapSweepBeam : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TelegraphFrames = 30;
        private const int FireFrames = 30;
        private const float HalfArc = 0.5f;
        private const float BeamLength = 880f;

        private NPC Boss => Main.npc[(int)Projectile.ai[0]];
        private float SweepDir => Projectile.ai[1];
        private ref float LocalTimer => ref Projectile.localAI[0];
        private ref float StartAngle => ref Projectile.localAI[1];
        private bool aimed;
        private bool fired;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = TelegraphFrames + FireFrames + 4;
        }

        /// <summary>只有回扫段咬人；预扫是给玩家读弧线的</summary>
        public override bool? CanDamage() => LocalTimer > TelegraphFrames ? null : false;

        /// <summary>当前扫掠角：预扫与回扫走同一条弧（同速同程）</summary>
        private float CurrentAngle() {
            float t = LocalTimer;
            float progress = t <= TelegraphFrames
                ? t / TelegraphFrames
                : (t - TelegraphFrames) / FireFrames;
            return StartAngle + SweepDir * progress * HalfArc * 2f;
        }

        private Vector2 Muzzle() {
            NPC boss = Boss;
            if (boss != null && boss.active && boss.ModNPC is ScrapCommander owner) {
                return owner.GetArmPos(ScrapCommander.ArmLaser)
                    + CurrentAngle().ToRotationVector2() * 24f;
            }
            return Projectile.Center;
        }

        public override void AI() {
            NPC boss = Boss;
            if (boss == null || !boss.active) {
                Projectile.Kill();
                return;
            }
            if (!aimed) {
                //生成 velocity 携带瞄准向量：从弧线中点回推起扫角
                aimed = true;
                StartAngle = Projectile.velocity.ToRotation() - SweepDir * HalfArc;
                Projectile.velocity = Vector2.Zero;
            }
            LocalTimer++;
            Projectile.rotation = CurrentAngle();
            Projectile.Center = Muzzle();

            if (LocalTimer > TelegraphFrames && !fired) {
                fired = true;
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.7f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.55f, Pitch = -0.1f, MaxInstances = 2 }, Projectile.Center);
            }
            //回扫期灼烧噪点沿线洒
            if (!Main.dedServ && fired && LocalTimer % 3 == 0) {
                float d = Main.rand.NextFloat(80f, BeamLength);
                Vector2 at = Projectile.Center + Projectile.rotation.ToRotationVector2() * d;
                PRTLoader.NewParticle<PRT_Spark>(at, Main.rand.NextVector2Circular(2f, 2f),
                    new Color(255, 150, 58) * 0.8f, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(true, Main.rand.Next(8, 13));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 start = Projectile.Center;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * BeamLength;
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                start, end, 26f, ref _);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            bool telegraph = LocalTimer <= TelegraphFrames;
            float alpha = telegraph
                ? MathHelper.Clamp(LocalTimer / 10f, 0f, 0.85f)
                : MathHelper.Clamp((TelegraphFrames + FireFrames - LocalTimer) / 6f, 0f, 1f);

            ScrapVfx.BeginBeamBatch(sb);
            ScrapVfx.DrawBeam(sb, Projectile.Center, Projectile.Center + dir * BeamLength,
                telegraph ? 20f : 34f, telegraph ? 0.5f : 1f, telegraph ? 1f : 0f,
                Projectile.identity * 0.61f, ScrapVfx.BeamCoreWarm,
                telegraph ? ScrapVfx.BeamEdgeRed : ScrapVfx.BeamEdgeRust,
                0.01f, 0.08f, alpha);
            ScrapVfx.EndBeamBatch(sb);
            return false;
        }
    }
}
