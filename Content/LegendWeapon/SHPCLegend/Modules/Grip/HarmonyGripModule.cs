using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>
    /// 谐振握把（血肉墙）：生物电节拍。连续命中积累"心跳"，满拍后进入谐振窗口；
    /// 窗口内左键命中释放小型音叉波、右键发射释放大型共鸣脉冲，二者择一消费。
    /// 状态仅存于模块私有字段（每客户端各自的装备实例），脉冲为独立弹幕，不改主光束。
    /// </summary>
    internal sealed class HarmonyGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //节能薄荷绿
        public override Color TintColor => new(120, 255, 180);

        private const int BeatsToArm = 4;
        private const int WindowTime = 300;

        private int _beats;
        private int _window;

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -0.3f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            if (_window > 0) {
                _window = 0;
                SpawnPulse(beam.Projectile, target.Center, 150f, Math.Max((int)(beam.Projectile.damage * 0.85f), 1));
            }
            else {
                AddBeat(Main.player[beam.Projectile.owner]);
            }
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            if (_window > 0) {
                _window = 0;
                SpawnPulse(laser.Projectile, target.Center, 150f, Math.Max((int)(laser.Projectile.damage * 1.2f), 1));
                return;
            }
            //激光命中频率很高，按帧节流计拍，避免脉冲刷屏
            if ((int)Main.GameUpdateCount % 10 != 0) return;
            AddBeat(Main.player[laser.Projectile.owner]);
        }

        public override void OnOrbLaunched(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            if (_window > 0) {
                _window = 0;
                SpawnPulse(orb.Projectile, orb.Projectile.Center, 320f, Math.Max((int)(orb.Projectile.damage * 1.2f), 1));
            }
        }

        public override void OnPlayerUpdate(Player player) {
            if (_window > 0) {
                _window--;
            }
        }

        private void AddBeat(Player player) {
            _beats++;
            if (_beats < BeatsToArm) return;
            _beats = 0;
            _window = WindowTime;
            if (Main.netMode == NetmodeID.Server || player == null || !player.active) return;
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.4f, Pitch = 0.6f }, player.Center);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.5f, 3.5f);
                PRTLoader.NewParticle<PRT_CyberSquare>(player.Center, vel, new Color(150, 255, 200), Main.rand.NextFloat(0.6f, 1.3f)).Configure(new Color(60, 200, 130), Main.rand.Next(12, 22));
            }
        }

        private static void SpawnPulse(Projectile src, Vector2 pos, float radius, int dmg) {
            if (src.owner != Main.myPlayer) return;
            Projectile.NewProjectile(src.GetSource_FromThis(), pos, Vector2.Zero,
                ModContent.ProjectileType<SHPCHarmonyPulseProj>(),
                dmg, 0f, src.owner, ai0: radius);
        }
    }

    /// <summary>
    /// 谐振脉冲：从生成点向外扩张的环形冲击波，沿途对敌人造成一次伤害。
    /// ai0 = 最大半径。纯独立弹幕，视觉用扩张柔光环 + 脉冲星环粒子。
    /// </summary>
    internal sealed class SHPCHarmonyPulseProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        private const int Lifetime = 30;
        private float maxRadius = 160f;

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Lifetime;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        private float CurRadius => MathHelper.SmoothStep(0f, maxRadius, 1f - Projectile.timeLeft / (float)Lifetime);

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                maxRadius = Projectile.ai[0] > 1f ? Projectile.ai[0] : 160f;
                int size = (int)(maxRadius * 2f);
                Projectile.Resize(size, size);
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.45f, Pitch = 0.1f }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, new Color(150, 255, 200, 0), 0.05f).Configure(0.05f, maxRadius / 320f, Lifetime - 4);
                }
            }
            Projectile.velocity = Vector2.Zero;
            float t = 1f - Projectile.timeLeft / (float)Lifetime;
            Lighting.AddLight(Projectile.Center, new Vector3(0.45f, 1f, 0.7f) * (1f - t) * 0.6f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            float r = CurRadius;
            //环带判定：只在波前一定厚度内命中，营造"扩张环扫过"手感
            return dist <= r && dist >= r - 56f;
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            float t = 1f - Projectile.timeLeft / (float)Lifetime;
            float r = CurRadius;
            float alpha = MathF.Sin(MathHelper.Clamp(t, 0f, 1f) * MathHelper.Pi);
            Vector2 screen = Projectile.Center - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;
            //外环（薄荷绿）与内核（青白）双层
            float outerScale = (r * 2f) / glow.Width;
            spriteBatch.Draw(glow, screen, null, new Color(120, 255, 180, 0) * alpha * 0.55f, 0f, origin, outerScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, screen, null, new Color(200, 255, 235, 0) * alpha * 0.35f, 0f, origin, outerScale * 0.7f, SpriteEffects.None, 0f);
        }
    }
}
