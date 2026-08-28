using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【丛林活体草叶】材质：活体草叶锻成的丛林中剑，剑身有弹性会呼吸。签名：
    /// ①孢子弧：每拍斩切后沿挥弧洒落毒孢雾，驻留噬咬并挂毒 ②藤蔓缠斩：终结拍
    /// 触及拉长 1.35 倍、沿刃爬出分段藤蔓虚影，命中藤蔓缠定（击退清零 + 剧毒加深）
    /// ③叶脉苏醒：举剑时叶脉光点从柄向尖逐个点亮
    /// </summary>
    internal class GsBladeofGrass : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BladeofGrass;

        protected override int HeldProjID => ModContent.ProjectileType<GsBladeofGrassHeld>();

        protected override string GsDescFallback =>
            "Reforged: a living jungle blade; every slash scatters biting spore mist along its arc, " +
            "leaf-veins wake as you raise it, and the third strike lashes out with vines that bind the target";

        //丛林绿黄色板
        internal static readonly Color GrassBright = new(186, 232, 96); //黄绿刃缘
        internal static readonly Color GrassMain = new(96, 176, 64);    //丛林绿剑身
        internal static readonly Color GrassHot = new(222, 255, 128);   //亮叶光
        internal static readonly Color GrassDeep = new(14, 34, 18);     //暗丛林影

        //预算：原版 18 伤/20 帧 = 0.9 伤帧，另有叶弹补射（接管后压掉，孢雾顶其位）。
        //周期 = 21+22+28 = 71f，直击 = 18×1.05×(1+1+1.3) ≈ 62.4，
        //孢雾每周期 7 团×20% 期望半数命中 ≈ +13 → 约 1.06 伤帧，
        //对原版含叶弹（~1.0 伤帧）约 106%，孢雾满命中理论上限 ~117%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 草刃剑手持：三拍连击走族标准弧（中剑语言，与太刀/大剑作对照），
    /// 活体剑身过冲更大回坐更弹。每拍收势沿挥弧洒 2~3 团孢雾；
    /// 终结拍触及 1.35 倍藤蔓缠斩。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBladeofGrassHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BladeofGrass;
        protected override Color EdgeBright => GsBladeofGrass.GrassBright;
        protected override Color BodyMain => GsBladeofGrass.GrassMain;
        protected override Color HotAccent => GsBladeofGrass.GrassHot;
        protected override Color DeepShadow => GsBladeofGrass.GrassDeep;

        private bool sporeSpawned;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //藤蔓缠斩：长距终结，藤蔓虚影随刃扫出
                return new GsBroadBeat {
                    Raise = 8, Hold = 3, Slash = 5, Recover = 12,
                    RaiseBack = 2.2f, Follow = 1.3f, ReachScale = 1.35f, LeanAmp = 0.09f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.5f, SwingPitch = -0.15f,
                };
            }
            if (stage == 1) {
                return new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 4, Recover = 10,
                    RaiseBack = 2.05f, Follow = 1.05f, ReachScale = 1.06f, LeanAmp = 0.05f,
                    DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.1f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.SwingPitch = 0.02f;
            return b;
        }

        /// <summary>活体剑身：过冲更大、回坐更弹</summary>
        protected override float SwingCurve(float p) {
            const float burstEnd = 0.5f;
            const float overshoot = 1.07f;
            if (p < burstEnd) {
                return overshoot * SmoothStep01(p / burstEnd);
            }
            return MathHelper.Lerp(overshoot, 1f, SmoothStep01((p - burstEnd) / (1f - burstEnd)));
        }

        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsBladeofGrass.GrassMain, 0.12f);
        protected override Color GlowColor => GsBladeofGrass.GrassHot;

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            //孢子弧：收势首帧沿挥过的弧线洒落孢雾
            if (!sporeSpawned && phase == PhaseRecover) {
                sporeSpawned = true;
                int count = IsFinisher ? 3 : 2;
                for (int i = 0; i < count; i++) {
                    float t = (i + 0.5f) / count;
                    float ang = MathHelper.Lerp(ArcStart, ArcEnd, t);
                    Vector2 pos = Hand + (ang.ToRotationVector2() * (FullReach * 0.82f));
                    Vector2 drift = ((ang + (swingDir * MathHelper.PiOver2)).ToRotationVector2() * 0.5f)
                        + new Vector2(0f, 0.22f);
                    //孢雾一跳约 20% 当前伤（终结拍随 1.3 倍水涨，已记入预算）
                    SpawnOwnedProj(ModContent.ProjectileType<GsBladeofGrassSporeProj>(), pos, drift,
                        Math.Max(1, (int)(Projectile.damage * 0.2f)), 0f);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = -0.2f }, Hand);
                }
            }
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            //草叶剑每一挥都带叶擦声
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.35f, Pitch = 0.1f }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.4f }, Owner.Center);
            }
        }

        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            //藤蔓缠定：终结拍命中把目标缠在原地（击退清零）。
            //查证 NPC.cs：原版 Slow(32) buff 对 NPC 无任何处理，故减速改为缠定
            if (IsFinisher) {
                modifiers.Knockback *= 0.1f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, IsFinisher ? 360 : 180);
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //毒液飞沫 + 丛林绿光
            Vector2 tangent = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            int drops = IsFinisher ? 9 : 5;
            for (int i = 0; i < drops; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Poisoned,
                    tangent.RotatedByRandom(0.8) * Main.rand.NextFloat(2f, 6f), 60, default,
                    Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = Main.rand.NextBool();
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GsBladeofGrass.GrassMain,
                IsFinisher ? 0.26f : 0.16f)?.Configure(10, 0.75f);
            if (IsFinisher) {
                //藤蔓缠上的瞬间迸一圈叶屑
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.JunglePlants,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f), 40, default,
                        Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = true;
                }
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                //斩切期沿切线甩出叶屑
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f));
                Dust d = Dust.NewDustPerfect(at, DustID.JunglePlants,
                    (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2() * Main.rand.NextFloat(2f, 5f),
                    60, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }
        }

        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            int phase = CurrentPhase;
            if (phase == PhaseRaise) {
                //叶脉苏醒：光点按举相进度从柄向尖逐个点亮，identity 相位明灭
                float p = timer / (float)raiseDur;
                const int dots = 5;
                for (int i = 0; i < dots; i++) {
                    float wake = MathHelper.Clamp((p - (i / (float)dots)) * dots, 0f, 1f);
                    if (wake <= 0.01f) {
                        continue;
                    }
                    float pulse = 0.75f + 0.25f * MathF.Sin((Main.GlobalTimeWrappedHourly * 7f)
                        + (DrawRand01(i + 11) * MathHelper.TwoPi));
                    Color c = GsBladeofGrass.GrassHot * (0.5f * wake * pulse);
                    c.A = 0;
                    Vector2 at = Vector2.Lerp(Hand, mainTip, 0.2f + (0.16f * i)) - Main.screenPosition;
                    sb.Draw(glow, at, null, c, 0f, glow.Size() / 2f, 0.16f * wake, SpriteEffects.None, 0f);
                }
                return;
            }
            if (!IsFinisher || phase < PhaseSlash || fanFade <= 0.05f) {
                return;
            }
            //藤蔓缠斩：沿刃分段藤蔓虚影，暗叶节（真 alpha）与亮叶尖（加色）交替缠绕
            Texture2D blot = CWRAsset.Extra_98?.Value;
            if (blot == null) {
                return;
            }
            Vector2 along = mainAngle.ToRotationVector2();
            Vector2 perp = (mainAngle + MathHelper.PiOver2).ToRotationVector2();
            const int segs = 6;
            for (int i = 0; i < segs; i++) {
                float t = 0.22f + (0.13f * i);
                float sway = MathF.Sin((i * 2.1f) + (DrawRand01(3) * MathHelper.TwoPi)) * 7f;
                Vector2 at = Hand + (along * (mainReach * t)) + (perp * sway) - Main.screenPosition;
                Color dark = GsBladeofGrass.GrassDeep * (0.5f * fanFade);
                sb.Draw(blot, at, null, dark, mainAngle + i, blot.Size() / 2f,
                    new Vector2(0.10f, 0.06f) * (1f + (0.4f * DrawRand01(i + 20))), SpriteEffects.None, 0f);
                Color leaf = GsBladeofGrass.GrassBright * (0.55f * fanFade);
                leaf.A = 0;
                sb.Draw(glow, at, null, leaf, 0f, glow.Size() / 2f, 0.13f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 毒孢雾：斩切沿弧洒落的驻留孢团。生成时带微速缓漂，驻留 30 帧对碰到的目标
    /// 咬一口（约 20% 底伤）并挂毒。暗绿团走真 alpha 压底，黄绿边走加色，
    /// 明灭相位 identity 播种
    /// </summary>
    internal class GsBladeofGrassSporeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Life = 30;

        private float Life01 => 1f - (Projectile.timeLeft / (float)Life);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Life;//一生只咬一口
            Projectile.timeLeft = Life;
        }

        public override bool? CanDamage() => Projectile.timeLeft > 4 ? null : false;

        public override void AI() {
            Projectile.velocity *= 0.93f;
            Lighting.AddLight(Projectile.Center, GsBladeofGrass.GrassMain.ToVector3() * (0.25f * (1f - Life01)));
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 8f),
                    DustID.Poisoned, new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.7f)), 120, default,
                    Main.rand.NextFloat(0.6f, 1f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 240);
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Poisoned,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f), 90, default,
                    Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        /// <summary>确定性伪随机（identity+salt 播种，逐帧稳定）</summary>
        private float SegRand(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blot == null || glow == null) {
                return false;
            }
            float fadeIn = MathHelper.Clamp((Life - Projectile.timeLeft) / 4f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
            float fade = fadeIn * fadeOut;
            Vector2 center = Projectile.Center - Main.screenPosition;

            //暗绿孢团：两瓣真 alpha 叠出不规则轮廓，缓慢自转
            for (int i = 0; i < 2; i++) {
                float rot = (SegRand(i + 5) * MathHelper.TwoPi)
                    + (Main.GlobalTimeWrappedHourly * (i == 0 ? 0.4f : -0.3f));
                float s = (0.17f + (0.05f * SegRand(i + 15))) * (1f + (0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3f)));
                Color dark = GsBladeofGrass.GrassDeep * (0.55f * fade);
                Main.EntitySpriteDraw(blot, center, null, dark, rot, blot.Size() * 0.5f,
                    s, SpriteEffects.None, 0);
            }

            //黄绿孢光：加色边缘，identity 种子明灭
            float pulse = 0.6f + 0.4f * MathF.Sin((Main.GlobalTimeWrappedHourly * 5f) + (SegRand(9) * MathHelper.TwoPi));
            Color edge = GsBladeofGrass.GrassBright * (0.45f * fade * pulse);
            edge.A = 0;
            Main.EntitySpriteDraw(glow, center, null, edge, 0f, glow.Size() * 0.5f, 0.34f, SpriteEffects.None, 0);
            Color core = GsBladeofGrass.GrassHot * (0.3f * fade * pulse);
            core.A = 0;
            Main.EntitySpriteDraw(glow, center, null, core, 0f, glow.Size() * 0.5f, 0.18f, SpriteEffects.None, 0);
            return false;
        }
    }
}
