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

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Stock
{
    /// <summary>
    /// 突击枪托（骷髅王 Prime）：连续命中积累压制值。攒满会自动部署一架肩部突击无人机；
    /// 也可在右键发射时把当前压制值一次性转化为更持久的无人机。无人机自行寻敌补射。
    /// </summary>
    internal sealed class AssaultStockModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //突击橙红
        public override Color TintColor => new(255, 100, 60);

        private const int MaxSuppress = 10;
        private const int MaxDrones = 2;
        private const int DroneBaseTime = 180;
        private const int DroneTimePerStack = 24;

        private int _suppress;
        private int _hitThrottle;

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += 0.05f;
            ctx.AttackSpeedMul += 0.10f;
            ctx.ManaCostMul += 0.5f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            Suppress(beam.Projectile, 0.5f);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            Suppress(laser.Projectile, 0.4f);
        }

        public override void OnOrbLaunched(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer || _suppress <= 0) return;
            //右键倾泻：把压制值转换成更持久的无人机
            int time = DroneBaseTime + _suppress * DroneTimePerStack;
            _suppress = 0;
            DeployDrone(orb.Projectile, time);
        }

        private void Suppress(Projectile src, float amount) {
            if (src.owner != Main.myPlayer) return;
            //命中频率较高，按帧节流积累，避免瞬间攒满
            if (++_hitThrottle < 3) return;
            _hitThrottle = 0;
            _suppress = Math.Min(_suppress + 1, MaxSuppress);
            if (_suppress >= MaxSuppress) {
                _suppress = 0;
                DeployDrone(src, DroneBaseTime);
            }
        }

        private static void DeployDrone(Projectile src, int time) {
            if (src.owner != Main.myPlayer) return;
            if (SHPCNaturalFx.CountOwned(src.owner, ModContent.ProjectileType<SHPCAssaultDroneProj>()) >= MaxDrones) return;
            Player p = Main.player[src.owner];
            int idx = Projectile.NewProjectile(src.GetSource_FromThis(),
                p.Center + new Vector2(0f, -60f), Vector2.Zero,
                ModContent.ProjectileType<SHPCAssaultDroneProj>(),
                Math.Max(src.damage, 1), 0f, src.owner, ai0: time);
            _ = idx;
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item149 with { Volume = 0.5f, Pitch = 0.2f }, p.Center);
            }
        }
    }

    /// <summary>
    /// 突击无人机：悬停在玩家肩部上方，定时向最近敌人补射微型派生束，存活时间由 ai0 指定。
    /// </summary>
    internal sealed class SHPCAssaultDroneProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private const int FireInterval = 18;
        private const float FireRange = 760f;
        private float bob;

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Projectile.ai[0] > 1f) Projectile.timeLeft = (int)Projectile.ai[0];
            }
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            //悬停在肩部上方，带轻微浮动
            bob += 0.08f;
            Vector2 desired = owner.Center + new Vector2(owner.direction * 30f, -60f + MathF.Sin(bob) * 6f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, desired, 0.18f);
            Projectile.rotation = MathF.Sin(bob) * 0.1f;
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.45f, 0.2f) * 0.5f);

            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % FireInterval == 0 && Projectile.owner == Main.myPlayer) {
                NPC target = Projectile.Center.FindClosestNPC(FireRange, false, true);
                if (target != null) {
                    Vector2 vel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                    SHPCNaturalFx.SpawnDerivedBeam(Projectile, Projectile.Center, vel, Math.Max(Projectile.damage / 2, 1), 1.6f, 0.5f, theme: 1);
                    if (Main.netMode != NetmodeID.Server) {
                        SoundEngine.PlaySound(SoundID.Item41 with { Volume = 0.25f, Pitch = 0.5f }, Projectile.Center);
                        for (int i = 0; i < 3; i++) {
                            PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, vel.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f), new Color(255, 170, 80), Main.rand.NextFloat(0.5f, 1f)).Configure(new Color(255, 80, 30), Main.rand.Next(8, 14));
                        }
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float life = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 screen = Projectile.Center - Main.screenPosition;
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, screen, new Color(255, 150, 70, 0) * life, new Color(160, 40, 20, 0) * life * 0.4f, 0.55f, Projectile.rotation, 3);
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null) {
                spriteBatch.Draw(star, screen, null, new Color(255, 210, 150, 0) * life, (float)Main.timeForVisualEffects * 0.05f, star.Size() * 0.5f, 0.1f, SpriteEffects.None, 0f);
            }
        }
    }
}
