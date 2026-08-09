using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    internal class Godslight : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        internal Vector2[] RayPoint;
        internal int pointNum => 100;
        internal Color[] colors;
        public override bool ShouldUpdatePosition() => false;
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 8000;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.timeLeft = 190;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.scale = 0.1f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override bool PreAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.ai[0] == 0) {
                colors = [Color.Red, LonginusVFX.HolyGold, Color.OrangeRed];
                RayPoint = new Vector2[pointNum];
                Vector2 rotByY = Projectile.velocity.UnitVector();

                for (int i = 0; i < pointNum; i++) {
                    RayPoint[i] = rotByY * (-pointNum * 30 + 60 * i) + Projectile.Center;
                }

                for (int i = 0; i < 4; i++) {
                    foreach (Vector2 pos in RayPoint) {
                        Vector2 spanPos = pos + Main.rand.NextVector2Unit() * Main.rand.Next(56);
                        Vector2 vr = new Vector2((Main.rand.NextBool() ? -1 : 1) * Main.rand.Next(7, 51), 0);
                        PRT_Light light = PRTLoader.NewParticle<PRT_Light>(spanPos
                            , vr, VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), colors), 0.3f).Configure(30);
                        //不要在屏幕外面就消除了，否则玩家什么都看不到
                        light.ShouldKillWhenOffScreen = false;
                    }
                }

                Projectile.ai[0] = 1;
            }
            if (Projectile.timeLeft > 60) {
                Projectile.scale += 0.5f;
                if (Projectile.scale > 9)
                    Projectile.scale = 9;
            }
            if (Projectile.timeLeft < 20) {
                Projectile.scale -= 1f;
                if (Projectile.scale < 0)
                    Projectile.scale = 0;
            }
            if (Projectile.timeLeft == 20) {
                SpanDeadLightPenms();
            }
            return true;
        }

        public void SpanDeadLightPenms() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                foreach (Vector2 pos in RayPoint) {
                    PRTLoader.NewParticle<PRT_Light>(pos + Main.rand.NextVector2Unit() * Main.rand.Next(56)
                        , new Vector2(0, Main.rand.Next(7, 51)), VaultUtils.MultiStepColorLerp(Main.rand.NextFloat(), colors), 0.3f).Configure(30);
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.rotation.ToRotationVector2() * -3000 + Projectile.Center
                , Projectile.rotation.ToRotationVector2() * 3000 + Projectile.Center, 132, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.damage = (int)(Projectile.damage * 0.98f);
            target.AddBuff(CWRID.Buff_GodSlayerInferno, 300);
        }

        public override void OnKill(int timeLeft) => SpanDeadLightPenms();

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Projectile.ai[0] == 0) {
                return;
            }
            float age = 190 - Projectile.timeLeft;
            //八道按 ai[1] 错帧依次拔起，阵列有先后而非同帧糊团
            float delay = (Projectile.ai[1] - 2f) * 2f;
            float grow = MathHelper.Clamp((age - delay) / 15f, 0f, 1f);
            float dissolve = Projectile.timeLeft < 45 ? MathHelper.Clamp((45 - Projectile.timeLeft) / 45f, 0f, 1f) : 0f;
            Vector2 upDir = -Projectile.rotation.ToRotationVector2();
            //粗细随 ai[1](2~9) 分层
            float widthUnits = 0.022f + Projectile.ai[1] * 0.011f;
            //完成瞬间过曝后指数退潮，细柱余温略高
            float sinceDone = age - delay - 15f;
            float hot = sinceDone > 0 ? 1.25f * (float)System.Math.Exp(-sinceDone * 0.11f) : 0f;
            hot += Projectile.ai[1] < 4f ? 0.22f : 0.08f;
            LonginusVFX.DrawCross(Projectile.Center, upDir, 3000f, 1150f, grow, dissolve, 0.42f, widthUnits, hot);
        }
    }
}
