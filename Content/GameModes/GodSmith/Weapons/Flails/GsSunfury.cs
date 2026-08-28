using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·阳炎之怒】阳炎之怒重铸：狱火黑曜链锤。签名行为：①掷出飞行沿途留悬空灼痕，
    /// 触敌灼伤并点狱火 ②满转掷出灼痕更密 ③锤头飞行自带火尾
    /// </summary>
    internal class GsSunfury : GsFlailScheme
    {
        public override int TargetItemID => ItemID.Sunfury;

        protected override int FlailProjType => ModContent.ProjectileType<GsSunfuryHead>();

        protected override string GsDescFallback =>
            "Reforged: the flying ball sears hovering scorch marks along its path" +
            "\nThe marks linger and burn whoever touches them with hellfire";

        //原版就是地狱强锤，签名灼痕收益大：底伤只补半成，综合 DPS 落在原版 105%~118%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 阳炎之怒锤头。掷出飞行每 4 帧（满转 3 帧）在头位留一段悬空灼痕（owner 端生成，单掷上限 10），
    /// 飞行自带火尾
    /// </summary>
    internal class GsSunfuryHead : GsFlailHeadProj
    {
        /// <summary>狱火橙</summary>
        internal static readonly Color FireOrange = new(255, 138, 40);
        /// <summary>焦黑曜</summary>
        internal static readonly Color CharBlack = new(48, 32, 26);
        /// <summary>焰亮心</summary>
        internal static readonly Color EmberBright = new(255, 206, 112);

        public override int SourceItemID => ItemID.Sunfury;
        public override int VanillaProjID => ProjectileID.Sunfury;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain6;
        public override Color GlowColor => FireOrange;

        /// <summary>灼痕伤害系数</summary>
        private const float ScorchDamageMul = 0.3f;
        /// <summary>单掷灼痕上限</summary>
        private const int ScorchCapPerThrow = 10;

        /// <summary>本次掷出已留下的灼痕数</summary>
        private int scorchLaid;

        protected override void OnLaunch(float charge) => scorchLaid = 0;

        protected override void OnSpinTick(float charge) {
            //高转甩转喷火星预告
            if (!VaultUtils.isServer && charge > 0.6f && spinTimer % 5 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 100, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }
        }

        protected override void OnLaunchTick(int flightTime) {
            //飞行火尾
            if (!VaultUtils.isServer) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -Projectile.velocity * 0.15f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    80, default, Main.rand.NextFloat(1.1f, 1.6f));
                d.noGravity = true;
            }
            //灼痕轨迹：满转 3 帧一段，否则 4 帧一段；owner 端生成随包广播
            int interval = LaunchCharge >= 0.99f ? 3 : 4;
            if (!Projectile.IsOwnedByLocalPlayer() || flightTime % interval != 0
                || scorchLaid >= ScorchCapPerThrow) {
                return;
            }
            scorchLaid++;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<GsSunfuryScorchProj>(),
                Math.Max(1, (int)(Projectile.damage * ScorchDamageMul)), 0.3f, Projectile.owner);
        }

        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            //狱火质感补层：命中处炸一把火星
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), 80, default, Main.rand.NextFloat(1.2f, 1.8f));
                d.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 悬空灼痕：锤头沿途留下的焦黑余焰，存续约 108 帧，触敌灼伤并点狱火。
    /// 自绘：焦黑基底（Extra_98 真 alpha）+ 橙红焰缘（加色）+ 零星上飘火星；淡入淡出有生命周期
    /// </summary>
    internal class GsSunfuryScorchProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 108;
        private const int FadeInFrames = 8;
        private const int FadeOutFrames = 20;

        /// <summary>identity 播种的相位，绘制抖动不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.917f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = LifeFrames;
        }

        private float Opacity {
            get {
                if (Projectile.timeLeft > LifeFrames - FadeInFrames) {
                    return (LifeFrames - Projectile.timeLeft) / (float)FadeInFrames;
                }
                if (Projectile.timeLeft < FadeOutFrames) {
                    return Projectile.timeLeft / (float)FadeOutFrames;
                }
                return 1f;
            }
        }

        public override void AI() {
            //悬空驻定，identity 相位轻微呼吸浮动
            Projectile.velocity = Vector2.Zero;
            Projectile.position.Y += MathF.Sin(Main.GameUpdateCount * 0.04f + Seed) * 0.08f;
            //零星上飘火星
            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 6f),
                    DustID.Torch, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.3f)), 100, default,
                    Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, GsSunfuryHead.FireOrange.ToVector3() * 0.28f * Opacity);
        }

        public override bool? CanDamage() => Opacity > 0.5f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire3, 180);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (blot == null || glow == null || star == null) {
                return false;
            }
            float alpha = Opacity;
            float flicker = 0.8f + 0.2f * MathF.Sin(Main.GameUpdateCount * 0.19f + Seed * 4.1f);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //焦黑基底：真 alpha 压出黑曜余烬的实体感（两块错角叠出不规则）
            Color chars = GsSunfuryHead.CharBlack * (0.7f * alpha);
            Main.EntitySpriteDraw(blot, pos, null, chars, Seed,
                blot.Size() * 0.5f, new Vector2(0.34f, 0.24f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(blot, pos, null, chars * 0.8f, Seed + 1.9f,
                blot.Size() * 0.5f, new Vector2(0.26f, 0.30f), SpriteEffects.None, 0);

            //橙红焰缘（加色 A=0），随 flicker 明灭
            Color rim = GsSunfuryHead.FireOrange * (0.5f * alpha * flicker);
            rim.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, rim, 0f, glow.Size() * 0.5f,
                0.42f * flicker, SpriteEffects.None, 0);
            //焰亮心小星
            Color core = GsSunfuryHead.EmberBright * (0.45f * alpha * flicker);
            core.A = 0;
            Main.EntitySpriteDraw(star, pos, null, core, Seed * 0.5f + Main.GameUpdateCount * 0.01f,
                star.Size() * 0.5f, 0.22f * flicker, SpriteEffects.None, 0);
            return false;
        }
    }
}
