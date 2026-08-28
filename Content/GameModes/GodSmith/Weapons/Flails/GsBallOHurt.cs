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
    /// 【连枷·首把】痛苦之球重铸：腐化荆棘铁球。签名行为：①甩转充能满出手更快更狠（族链体物理）
    /// ②掷出飞行沿途崩落悬停倒刺，触碰刺伤 ③满转速命中在目标处炸出一圈倒刺
    /// </summary>
    internal class GsBallOHurt : GsFlailScheme
    {
        public override int TargetItemID => ItemID.BallOHurt;

        protected override int FlailProjType => ModContent.ProjectileType<GsBallOHurtHead>();

        protected override string GsDescFallback =>
            "Reforged: spin to charge the swing; the flying ball sheds hovering barbs along its path" +
            "\nA fully charged strike bursts a ring of barbs on the target";

        //公认弱势的早期连枷：底伤补一成二，倒刺群收益另计，综合 DPS 落在原版 120%~130%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.12f;
    }

    /// <summary>
    /// 痛苦之球锤头。飞行期每隔数帧崩落一枚倒刺（单掷上限 6），
    /// 满转命中时以目标为心环爆 5 枚；倒刺一律走 owner 端生成
    /// </summary>
    internal class GsBallOHurtHead : GsFlailHeadProj
    {
        /// <summary>腐化荆棘紫</summary>
        internal static readonly Color ThornPurple = new(158, 96, 220);
        /// <summary>暗棘影紫</summary>
        internal static readonly Color ThornDeep = new(74, 42, 118);

        public override int SourceItemID => ItemID.BallOHurt;
        public override int VanillaProjID => ProjectileID.BallOHurt;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain2;
        public override Color GlowColor => ThornPurple;

        public override float MaxChainLength => 330f;
        public override float LaunchSpeed => 16f;
        public override int LaunchFrames => 18;

        /// <summary>本次掷出已崩落的倒刺数</summary>
        private int barbsShed;

        /// <summary>倒刺伤害系数</summary>
        private const float BarbDamageMul = 0.4f;
        /// <summary>单掷崩落上限</summary>
        private const int BarbCapPerThrow = 6;
        /// <summary>全场悬停倒刺上限</summary>
        private const int BarbCapTotal = 14;

        protected override void OnLaunch(float charge) => barbsShed = 0;

        protected override void OnSpinTick(float charge) {
            //高转速时球身甩出腐化碎屑，预告倒刺要来了
            if (!VaultUtils.isServer && charge > 0.55f && spinTimer % 6 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.CorruptGibs,
                    Main.rand.NextVector2Circular(1.6f, 1.6f), 120, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = true;
            }
        }

        protected override void OnLaunchTick(int flightTime) {
            //沿途崩刺：每 5 帧一枚，带一点横向散布；owner 端生成随包广播
            if (!Projectile.IsOwnedByLocalPlayer() || flightTime % 5 != 0
                || barbsShed >= BarbCapPerThrow
                || Owner.ownedProjectileCounts[ModContent.ProjectileType<GsBallOHurtBarbProj>()] >= BarbCapTotal) {
                return;
            }
            barbsShed++;
            Vector2 drift = Projectile.velocity.SafeNormalize(Vector2.UnitX)
                .RotatedBy(MathHelper.PiOver2 * (barbsShed % 2 == 0 ? 1 : -1)) * Main.rand.NextFloat(0.7f, 1.6f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, drift,
                ModContent.ProjectileType<GsBallOHurtBarbProj>(),
                Math.Max(1, (int)(Projectile.damage * BarbDamageMul)), 0.5f, Projectile.owner);
        }

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //满转命中：以目标为心环爆一圈倒刺
            if (LaunchCharge >= 0.99f && State == StateLaunch) {
                const int ringCount = 5;
                for (int i = 0; i < ringCount; i++) {
                    Vector2 dir = (MathHelper.TwoPi * i / ringCount + Projectile.identity * 0.7f).ToRotationVector2();
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        target.Center + dir * 30f, dir * 2.2f,
                        ModContent.ProjectileType<GsBallOHurtBarbProj>(),
                        Math.Max(1, (int)(Projectile.damage * BarbDamageMul)), 0.5f, Projectile.owner);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath23 with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
                }
            }
        }

        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            //腐化质感补层：紫棘碎屑
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.CorruptGibs,
                    Main.rand.NextVector2Circular(3f, 3f), 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = Main.rand.NextBool();
            }
        }
    }

    /// <summary>
    /// 腐化倒刺：崩落后急减速悬停原地（残留物也有加速度曲线），
    /// 悬停约 2.6 秒刺伤触碰者，尾段淡出。全程自绘：暗棘本体+紫辉核+呼吸星芒
    /// </summary>
    internal class GsBallOHurtBarbProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 156;
        private const int FadeInFrames = 6;
        private const int FadeOutFrames = 20;

        /// <summary>identity 播种的相位，绘制抖动不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.917f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = LifeFrames;
        }

        private float LifeT => 1f - Projectile.timeLeft / (float)LifeFrames;

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
            //崩落初速急衰减到悬停，随后按 identity 相位轻微呼吸浮动
            Projectile.velocity *= 0.86f;
            if (Projectile.velocity.LengthSquared() < 0.02f) {
                Projectile.velocity = Vector2.Zero;
                Projectile.position.Y += MathF.Sin(Main.GameUpdateCount * 0.05f + Seed) * 0.12f;
            }
            if (Projectile.rotation == 0f) {
                Projectile.rotation = Seed % MathHelper.TwoPi;
            }
            Projectile.rotation += 0.006f;
            Lighting.AddLight(Projectile.Center, GsBallOHurtHead.ThornPurple.ToVector3() * 0.14f * Opacity);
        }

        public override bool? CanDamage() => Opacity > 0.5f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //被踩中的倒刺立即碎掉，不做持续磨床
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = 0.5f }, Projectile.Center);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Circular(3f, 3f), GsBallOHurtHead.ThornPurple,
                        Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(8, 14));
                }
            }
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.CorruptGibs,
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 130, default, 0.8f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D thorn = CWRAsset.TearSpread01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (thorn == null || glow == null || star == null) {
                return false;
            }
            float alpha = Opacity;
            float breathe = 0.85f + 0.15f * MathF.Sin(Main.GameUpdateCount * 0.11f + Seed);
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //紫辉底衬（加色 A=0）
            Color halo = GsBallOHurtHead.ThornPurple * (0.30f * alpha);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, halo, 0f, glow.Size() / 2f, 0.32f * breathe, SpriteEffects.None, 0);

            //暗棘本体（真 alpha 贴图压深色，读得出实体）
            Color body = Color.Lerp(GsBallOHurtHead.ThornDeep, GsBallOHurtHead.ThornPurple, 0.3f) * alpha;
            Main.EntitySpriteDraw(thorn, pos, null, body, Projectile.rotation,
                thorn.Size() / 2f, 0.20f, SpriteEffects.None, 0);
            //亮缘第二层，转 90 度叠出十字棘
            Color rim = GsBallOHurtHead.ThornPurple * (0.75f * alpha);
            Main.EntitySpriteDraw(thorn, pos, null, rim, Projectile.rotation + MathHelper.PiOver2,
                thorn.Size() / 2f, 0.14f, SpriteEffects.None, 0);

            //呼吸星芒刺尖（加色 A=0）
            Color glint = Color.Lerp(GsBallOHurtHead.ThornPurple, Color.White, 0.4f) * (0.5f * alpha * breathe);
            glint.A = 0;
            Main.EntitySpriteDraw(star, pos, null, glint, -Projectile.rotation * 0.5f,
                star.Size() / 2f, 0.16f * breathe, SpriteEffects.None, 0);
            return false;
        }
    }
}
