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
    /// 动能阻尼托（石巨人）：站稳积累阻尼充能。受到击退时把充能炸成一圈反击冲击片；
    /// 右键引爆时则在身前展开吸收冲击的动能护盾，碎裂后向前迸射反击碎片。
    /// </summary>
    internal sealed class KineticDamperModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Stock;
        //减震橄榄绿
        public override Color TintColor => new(140, 180, 90);

        private const int MaxDamp = 120;
        private const int MinTrigger = 30;
        private float _prevSpeed;
        private int _damp;

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.5f;
            ctx.AttackSpeedMul += -0.08f;
            ctx.CritAdd += 3;
        }

        public override void OnPlayerUpdate(Player player) {
            float speed = player.velocity.Length();
            if (speed < 1.5f) {
                _damp = Math.Min(_damp + 1, MaxDamp);
            }
            //受击退：速度骤增且储有阻尼 → 反击冲击片
            if (_damp >= MinTrigger && speed > 8f && _prevSpeed < 4f && player.whoAmI == Main.myPlayer) {
                ReleaseCounter(player);
                _damp = Math.Max(_damp - 40, 0);
            }
            _prevSpeed = speed;
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer || _damp < MinTrigger) return;
            float ratio = _damp / (float)MaxDamp;
            _damp = 0;
            int time = (int)MathHelper.Lerp(70f, 150f, ratio);
            Player p = Main.player[orb.Projectile.owner];
            Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                p.Center + new Vector2(p.direction * 64f, 0f), Vector2.Zero,
                ModContent.ProjectileType<SHPCKineticBarrierProj>(),
                Math.Max(orb.Projectile.damage / 3, 1), 0f, orb.Projectile.owner,
                ai0: time, ai1: ratio);
        }

        private static void ReleaseCounter(Player player) {
            //派生束需要一个弹幕宿主来取 GetSource；借用玩家当前任意一束弹幕，找不到则跳过
            Projectile source = FindOwnedProjectile(player);
            if (source == null) return;
            int dmg = Math.Max((player.HeldItem?.damage ?? 1) / 2, 1);
            for (int i = 0; i < 6; i++) {
                float ang = MathHelper.TwoPi * i / 6f + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(8f, 11f);
                SHPCNaturalFx.SpawnDerivedBeam(source, player.Center, vel, dmg, 1.4f, 0.45f, theme: 2);
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = -0.2f }, player.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero, new Color(160, 210, 110, 0), 0.05f).Configure(0.05f, 0.4f, 18);
            }
            SHPCNaturalFx.Shake(3f);
        }

        private static Projectile FindOwnedProjectile(Player player) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI) return p;
            }
            return null;
        }
    }

    /// <summary>
    /// 动能护盾：在玩家身前展开的半透明阻尼墙，接触敌人造成伤害，碎裂时向前迸射反击碎片。
    /// </summary>
    internal sealed class SHPCKineticBarrierProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private float ratio = 0.5f;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 132;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Projectile.ai[0] > 1f) Projectile.timeLeft = (int)Projectile.ai[0];
                ratio = MathHelper.Clamp(Projectile.ai[1], 0.1f, 1f);
                Projectile.scale = 0.8f + ratio * 0.5f;
                Projectile.Resize((int)(30 * Projectile.scale), (int)(132 * Projectile.scale));
            }
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.Center + new Vector2(owner.direction * 64f, 0f);
            Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.7f, 0.3f) * 0.6f);
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                Vector2 off = new(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-60f, 60f) * Projectile.scale);
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center + off, new Vector2(owner.direction * 0.6f, 0f), new Color(170, 220, 120), Main.rand.NextFloat(0.4f, 0.9f)).Configure(new Color(70, 140, 50), Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.owner != Main.myPlayer) return;
            Player owner = Main.player[Projectile.owner];
            int dir = owner != null ? owner.direction : 1;
            int dmg = Math.Max(Projectile.damage, 1);
            for (int i = -2; i <= 2; i++) {
                Vector2 vel = new Vector2(dir, 0f).RotatedBy(i * 0.28f) * 12f;
                SHPCNaturalFx.SpawnDerivedBeam(Projectile, Projectile.Center, vel, dmg, 1.2f, 0.5f, theme: 2);
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            float life = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
            float pulse = 0.8f + 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 0.25f);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 origin = glow.Size() * 0.5f;
            Vector2 screen = Projectile.Center - Main.screenPosition;
            //竖向拉伸的能量墙
            Vector2 wallScale = new(0.5f * Projectile.scale, 1.6f * Projectile.scale);
            spriteBatch.Draw(glow, screen, null, new Color(150, 220, 110, 0) * life * 0.5f * pulse, 0f, origin, wallScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, screen, null, new Color(210, 255, 170, 0) * life * 0.3f * pulse, 0f, origin, wallScale * new Vector2(0.5f, 0.9f), SpriteEffects.None, 0f);
        }
    }
}
