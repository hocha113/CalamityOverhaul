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
    /// 【连枷·蓝月】蓝月重铸：圣蓝秘银月锤。签名行为：①转速档位决定命中迸出的穿透新月刃数，
    /// 半充一枚、满充三枚 ②新月刃朝目标后方扇形飞出并轻微减速 ③甩转充能时锤头月晕渐亮
    /// </summary>
    internal class GsBlueMoon : GsFlailScheme
    {
        public override int TargetItemID => ItemID.BlueMoon;

        protected override int FlailProjType => ModContent.ProjectileType<GsBlueMoonHead>();

        protected override string GsDescFallback =>
            "Reforged: charged strikes loose piercing crescent blades through the target" +
            "\nHalf charge looses one blade, a full charge looses three";

        //中期圣蓝锤，签名收益按充能门槛给：底伤补零点八成，综合 DPS 落在原版 108%~120%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 蓝月锤头。族默认链体参数；命中按出手转速迸出新月刃（owner 端生成），
    /// 甩转充能时月晕渐亮
    /// </summary>
    internal class GsBlueMoonHead : GsFlailHeadProj
    {
        /// <summary>圣蓝</summary>
        internal static readonly Color HolyBlue = new(112, 162, 255);
        /// <summary>月白</summary>
        internal static readonly Color MoonWhite = new(214, 232, 255);

        public override int SourceItemID => ItemID.BlueMoon;
        public override int VanillaProjID => ProjectileID.BlueMoon;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain3;
        public override Color GlowColor => HolyBlue;

        /// <summary>新月刃伤害系数</summary>
        private const float CrescentDamageMul = 0.45f;

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || !Projectile.IsOwnedByLocalPlayer() || State != StateLaunch) {
                return;
            }
            //月辉蓄力：半充一枚、满充三枚穿透新月刃
            int count = LaunchCharge >= 0.99f ? 3 : LaunchCharge >= 0.5f ? 1 : 0;
            if (count <= 0) {
                return;
            }
            //朝目标后方（顺着出手方向穿过目标）扇形飞出
            Vector2 through = Owner.MountedCenter.To(target.Center).SafeNormalize(Vector2.UnitX * Owner.direction);
            for (int i = 0; i < count; i++) {
                float fan = count == 1 ? 0f : (i - (count - 1) * 0.5f) * 0.42f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    target.Center, through.RotatedBy(fan) * 11.5f,
                    ModContent.ProjectileType<GsBlueMoonCrescentProj>(),
                    Math.Max(1, (int)(Projectile.damage * CrescentDamageMul)), 1f, Projectile.owner);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.55f, Pitch = 0.15f }, target.Center);
            }
        }

        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            //月尘补层：圣蓝亮点飘散
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(target.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Circular(1.5f, 1.5f), MoonWhite, 0.08f)?.Configure(10, 0.75f);
            }
        }

        /// <summary>甩转充能月晕渐亮：SoftGlow 加色罩层，identity 播种呼吸不掷 Main.rand</summary>
        protected override void PostDrawHead(Color lightColor, float headRotation, Rectangle frame, Vector2 origin) {
            if (spinCharge <= 0.05f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            float breathe = 0.85f + 0.15f * MathF.Sin(Main.GameUpdateCount * 0.12f + Projectile.identity * 0.917f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color halo = HolyBlue * (0.38f * spinCharge * breathe);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, halo, 0f, glow.Size() * 0.5f,
                0.5f * (0.6f + spinCharge * 0.6f) * breathe, SpriteEffects.None, 0);
            //满转再压一层月白亮心
            if (spinCharge >= 0.99f) {
                Color core = MoonWhite * (0.30f * breathe);
                core.A = 0;
                Main.EntitySpriteDraw(glow, pos, null, core, 0f, glow.Size() * 0.5f,
                    0.30f * breathe, SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>
    /// 穿透新月刃：命中迸出的月弧，穿透 2 个目标，轻微减速淡出。
    /// 自绘：CrescentEdge01 月弧本体（加色）+ 圣蓝辉光罩层 + oldPos 残影
    /// </summary>
    internal class GsBlueMoonCrescentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 42;
        private const int FadeInFrames = 5;
        private const int FadeOutFrames = 12;

        /// <summary>identity 播种的相位，绘制抖动不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.917f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;//穿透 2 个目标
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//穿透型，同目标只结算一次
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
            //轻微减速曲线：月刃越飞越缓，尾段随淡出泄力
            Projectile.velocity *= 0.955f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch,
                    -Projectile.velocity * 0.1f, 120, default, 0.8f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, GsBlueMoonHead.HolyBlue.ToVector3() * 0.25f * Opacity);
        }

        public override bool? CanDamage() => Opacity > 0.4f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                    GsBlueMoonHead.MoonWhite, 0.12f)?.Configure(8, 0.8f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (crescent == null || glow == null) {
                return false;
            }
            float alpha = Opacity;
            Vector2 origin = crescent.Size() * 0.5f;
            float rot = Projectile.rotation + MathHelper.PiOver2;
            float breathe = 0.9f + 0.1f * MathF.Sin(Main.GameUpdateCount * 0.2f + Seed);

            //oldPos 残影：越旧越淡的月弧拖尾
            for (int g = Projectile.oldPos.Length - 1; g >= 1; g--) {
                Vector2 gp = Projectile.oldPos[g];
                if (gp == Vector2.Zero) {
                    continue;
                }
                float fade = (1f - g / (float)Projectile.oldPos.Length) * 0.22f * alpha;
                Color ghost = GsBlueMoonHead.HolyBlue * fade;
                ghost.A = 0;
                Main.EntitySpriteDraw(crescent, gp + Projectile.Size * 0.5f - Main.screenPosition,
                    null, ghost, Projectile.oldRot[g] + MathHelper.PiOver2, origin, 0.30f, SpriteEffects.None, 0);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //圣蓝辉光罩层（加色 A=0）
            Color halo = GsBlueMoonHead.HolyBlue * (0.35f * alpha);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, halo, 0f, glow.Size() * 0.5f, 0.36f * breathe, SpriteEffects.None, 0);
            //月弧本体：外圈圣蓝、内芯月白，双层错缩出刃口（黑底亮度贴图进加色语义）
            Color edge = GsBlueMoonHead.HolyBlue * (0.8f * alpha);
            edge.A = 0;
            Main.EntitySpriteDraw(crescent, pos, null, edge, rot, origin, 0.34f, SpriteEffects.None, 0);
            Color core = GsBlueMoonHead.MoonWhite * (0.9f * alpha);
            core.A = 0;
            Main.EntitySpriteDraw(crescent, pos, null, core, rot, origin, 0.27f, SpriteEffects.None, 0);
            return false;
        }
    }
}
