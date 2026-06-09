using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>
    /// 精密瞄具（双子魔眼）：连续命中同一目标完成校准后，下一次命中不再追踪，而是从玩家射出一条高穿透窄线长枪。
    /// 若刚校准过的另一目标仍存活，长枪会在双目标之间再穿一次线，呼应双子主题。
    /// </summary>
    internal sealed class PrecisionOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //精确蓝绿色
        public override Color TintColor => new(80, 255, 200);

        private const int ZeroThreshold = 6;
        private int _zeroTarget = -1;
        private int _zeroStacks;
        private int _zeroTime;
        private int _lastLanced = -1;
        private int _lastLancedTime;

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -1.0f;
            ctx.CritAdd += 6;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.IsDerived) return;
            Zero(beam.Projectile, target);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if ((int)Main.GameUpdateCount % 10 == 0) Zero(laser.Projectile, target);
        }

        private void Zero(Projectile src, NPC target) {
            if (_lastLancedTime > 0) _lastLancedTime--; else _lastLanced = -1;

            if (target.whoAmI == _zeroTarget) {
                _zeroStacks++;
            }
            else {
                _zeroTarget = target.whoAmI;
                _zeroStacks = 1;
            }
            _zeroTime = 120;

            if (_zeroStacks < ZeroThreshold) return;
            _zeroStacks = 0;
            if (src.owner != Main.myPlayer) return;

            Player p = Main.player[src.owner];
            FireLance(src, p.Center, target.Center, Math.Max(src.damage, 1));

            //双子穿线：上一次校准目标仍存活时，在两目标之间再穿一次
            if (_lastLanced >= 0 && _lastLanced < Main.maxNPCs && _lastLanced != target.whoAmI) {
                NPC prev = Main.npc[_lastLanced];
                if (prev != null && prev.active && !prev.friendly) {
                    FireLance(src, target.Center, prev.Center, Math.Max(src.damage, 1));
                }
            }
            _lastLanced = target.whoAmI;
            _lastLancedTime = 240;

            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item40 with { Volume = 0.55f, Pitch = 0.2f }, p.Center);
            }
            SHPCNaturalFx.Shake(2.5f);
        }

        private static void FireLance(Projectile src, Vector2 from, Vector2 to, int damage) {
            Vector2 dir = (to - from).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(src.GetSource_FromThis(), from, dir * 30f,
                ModContent.ProjectileType<SHPCPrecisionLanceProj>(), damage, 2f, src.owner);
        }

        public override void OnPlayerUpdate(Player player) {
            if (_zeroTime > 0) _zeroTime--; else { _zeroStacks = 0; _zeroTarget = -1; }
        }
    }

    /// <summary>
    /// 精密长枪：沿固定方向高速穿行的窄线高穿透弹，命中时不追踪，专注单线刺穿。
    /// </summary>
    internal sealed class SHPCPrecisionLanceProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int TrailLen = 12;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 40;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, new Vector3(0.3f, 1f, 0.8f) * 0.6f);
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.6f, 0.6f), new Color(120, 255, 210), Main.rand.NextFloat(0.4f, 0.8f));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode != NetmodeID.Server) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, new Color(120, 255, 210, 0), 0.05f).Configure(0.05f, 0.24f, 10);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            float life = MathHelper.Clamp(Projectile.timeLeft / 12f, 0.2f, 1f);
            Vector2 origin = glow.Size() * 0.5f;
            //沿运动方向拉伸的窄长枪芒 + 残影
            for (int i = 0; i < TrailLen; i++) {
                Vector2 pos = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * i * 7f - Main.screenPosition;
                float fade = (1f - i / (float)TrailLen) * life;
                Vector2 scale = new(0.06f + 0.5f * (1f - i / (float)TrailLen), 0.16f);
                spriteBatch.Draw(glow, pos, null, new Color(120, 255, 210, 0) * fade * 0.6f, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            }
            Vector2 head = Projectile.Center - Main.screenPosition;
            spriteBatch.Draw(glow, head, null, new Color(200, 255, 235, 0) * life, Projectile.rotation, origin, new Vector2(0.7f, 0.22f), SpriteEffects.None, 0f);
        }
    }
}
