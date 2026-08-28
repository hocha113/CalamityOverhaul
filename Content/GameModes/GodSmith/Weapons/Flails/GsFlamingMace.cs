using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·烈焰钉头锤】烈焰钉头锤重铸：燃焦铸铁锤。签名行为：①甩转期按转速离心甩出带重力的火星弹，
    /// 转速越高甩越快甩越远 ②火星触敌或落地小燃爆并点燃 ③链条近头段向焰橙炽亮
    /// </summary>
    internal class GsFlamingMace : GsFlailScheme
    {
        public override int TargetItemID => ItemID.FlamingMace;

        protected override int FlailProjType => ModContent.ProjectileType<GsFlamingMaceHead>();

        protected override string GsDescFallback =>
            "Reforged: spinning the mace flings burning embers outward" +
            "\nFaster spins throw the embers harder and farther, igniting whatever they strike";

        //早期弱锤，甩火收益要甩转养：底伤补一成，综合 DPS 落在原版 110%~125%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;
    }

    /// <summary>
    /// 烈焰钉头锤锤头。甩转期离心甩火（owner 端生成，充能低于 0.3 不甩，单场上限 10），
    /// 链条近头段向焰橙提亮
    /// </summary>
    internal class GsFlamingMaceHead : GsFlailHeadProj
    {
        /// <summary>焰橙</summary>
        internal static readonly Color FlameOrange = new(255, 132, 36);
        /// <summary>焦棕</summary>
        internal static readonly Color CharBrown = new(96, 56, 30);
        /// <summary>焰亮心</summary>
        internal static readonly Color EmberBright = new(255, 208, 120);

        public override int SourceItemID => ItemID.FlamingMace;
        public override int VanillaProjID => ProjectileID.FlamingMace;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain43;
        public override Color GlowColor => FlameOrange;

        /// <summary>火星弹伤害系数</summary>
        private const float EmberDamageMul = 0.3f;
        /// <summary>全场火星弹上限</summary>
        private const int EmberCapTotal = 10;

        protected override void OnSpinTick(float charge) {
            //甩转喷火装点（远近端都跑，只是演出）
            if (!VaultUtils.isServer && charge > 0.4f && spinTimer % 4 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(1.2f, 1.2f), 100, default, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = true;
            }
            //离心甩火：初速沿切线，转速越高甩越快、频率越密；owner 端生成随包广播
            if (charge < 0.3f || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int interval = (int)MathHelper.Lerp(10f, 5f, charge);
            if (spinTimer % interval != 0
                || Owner.ownedProjectileCounts[ModContent.ProjectileType<GsFlamingMaceEmberProj>()] >= EmberCapTotal) {
                return;
            }
            Vector2 tangent = (spinAngle + MathHelper.PiOver2 * swingSign).ToRotationVector2();
            float fling = MathHelper.Lerp(3.5f, 9.5f, charge);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, tangent * fling,
                ModContent.ProjectileType<GsFlamingMaceEmberProj>(),
                Math.Max(1, (int)(Projectile.damage * EmberDamageMul)), 0.4f, Projectile.owner);
        }

        /// <summary>链条近头炽亮：t&gt;0.7 段向焰橙提亮（复刻原版烈焰链渐变神韵）</summary>
        public override Color ChainLinkColor(int linkIndex, float t, Color light) {
            if (t <= 0.7f) {
                return light;
            }
            float w = (t - 0.7f) / 0.3f;
            Color hot = Color.Lerp(light, FlameOrange, w * 0.85f);
            return Color.Lerp(hot, Color.White, w * 0.3f);
        }

        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            //燃焦质感补层：一撮火星
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(3f, 3f), 80, default, Main.rand.NextFloat(1.1f, 1.6f));
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 离心火星弹：甩转沿切线抛出的燃屑，带重力坠落，触敌或落地小燃爆并点燃。
    /// 自绘：焰核（加色）+ 拖尾微光 + 速度轻拉伸；抖动 identity 播种
    /// </summary>
    internal class GsFlamingMaceEmberProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 90;
        private const int FadeInFrames = 4;

        /// <summary>identity 播种的相位，绘制抖动不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.917f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;//触敌即爆
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = LifeFrames;
        }

        private float Opacity {
            get {
                if (Projectile.timeLeft > LifeFrames - FadeInFrames) {
                    return (LifeFrames - Projectile.timeLeft) / (float)FadeInFrames;
                }
                return 1f;
            }
        }

        public override void AI() {
            //抛体弹道：重力主导，横向微阻——离心甩出去就得往下砸
            Projectile.velocity.Y += 0.24f;
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -Projectile.velocity * 0.1f, 100, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, GsFlamingMaceHead.FlameOrange.ToVector3() * 0.2f * Opacity);
        }

        public override bool? CanDamage() => Opacity > 0.5f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 120);

        public override void OnKill(int timeLeft) {
            //小燃爆：触敌或落地都走这里，火星一撮 + 一声轻响
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.25f, Pitch = 0.3f }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(2.5f, 2f) - Vector2.UnitY * 0.8f, 80, default,
                    Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = Main.rand.NextBool();
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsFlamingMaceHead.FlameOrange, 0.12f)?.Configure(8, 0.8f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return false;
            }
            float alpha = Opacity;
            float flicker = 0.8f + 0.2f * MathF.Sin(Main.GameUpdateCount * 0.25f + Seed);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //拖尾微光：oldPos 越旧越淡
            for (int g = Projectile.oldPos.Length - 1; g >= 1; g--) {
                Vector2 gp = Projectile.oldPos[g];
                if (gp == Vector2.Zero) {
                    continue;
                }
                float fade = (1f - g / (float)Projectile.oldPos.Length) * 0.20f * alpha;
                Color trail = GsFlamingMaceHead.CharBrown * fade;
                trail.A = 0;
                Main.EntitySpriteDraw(glow, gp + Projectile.Size * 0.5f - Main.screenPosition,
                    null, trail, 0f, glow.Size() * 0.5f, 0.16f, SpriteEffects.None, 0);
            }

            //焰核：速度方向轻拉伸的加色双层（外橙内亮）
            float stretch = 1f + MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0f, 0.5f);
            Color halo = GsFlamingMaceHead.FlameOrange * (0.55f * alpha * flicker);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, halo, Projectile.rotation, glow.Size() * 0.5f,
                new Vector2(0.24f * stretch, 0.20f), SpriteEffects.None, 0);
            Color core = GsFlamingMaceHead.EmberBright * (0.7f * alpha * flicker);
            core.A = 0;
            Main.EntitySpriteDraw(star, pos, null, core, Seed + Main.GameUpdateCount * 0.05f,
                star.Size() * 0.5f, 0.14f * flicker, SpriteEffects.None, 0);
            return false;
        }
    }
}
