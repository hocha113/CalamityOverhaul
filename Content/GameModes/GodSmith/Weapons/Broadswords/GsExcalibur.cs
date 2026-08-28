using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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
    /// 【圣辉裁决】材质：神圣秘银铸的圣剑，昼光淬刃。
    /// 签名：①每一斩沿出手向放出短程圣光弧，刃外距离由弧波补足（1.4.4 原版光弧挥击保留并升级）
    /// ②斩击命中积攒圣辉（上限 6），第四拍裁决斩命中时引爆圣光新星，随层数升阶
    /// ③裁决拍蓄力时刃身聚光、身侧升起旋转光环，命中迸溅金白圣火
    /// </summary>
    internal class GsExcalibur : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Excalibur;

        protected override int HeldProjID => ModContent.ProjectileType<GsExcaliburHeld>();

        protected override int ComboBeats => 4;

        //裁决仪式的续段窗口放宽
        protected override int ComboResetFrames => 65;

        protected override string GsDescFallback =>
            "Reforged: a four-beat rite of daylight; every slash casts a holy arc, " +
            "hits build Radiance, and the fourth judgment strike detonates it as a sacred nova";

        //圣辉色板
        internal static readonly Color HolyBright = new(255, 246, 214); //白金刃缘
        internal static readonly Color HolyMain = new(252, 208, 92);    //鎏金体色
        internal static readonly Color HolyHot = new(255, 168, 52);     //裁决灼金
        internal static readonly Color HolyDeep = new(54, 40, 22);      //暖调垫影

        /// <summary>圣辉层数（0~6）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int Radiance;

        //底伤不加成（1.4.4 圣剑本体已强）：拍均 1.05x + 每斩 0.7x 圣光弧只管刃外 +
        //裁决新星命中才引爆（典型 3 层约 0.6x/循环、满 6 层 0.83x），
        //按四拍循环约 83 帧摊算，综合单体 DPS 约原版 108%~119%，新星多目标是 AoE 收益
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 圣辉裁决手持：四拍连段。0 横斩 / 1 返斩 / 2 疾斩（快拍）/ 3 裁决下劈
    /// （长举聚光+前压+命中引爆圣光新星）。每拍斩切爆发时放出圣光弧。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsExcaliburHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Excalibur;
        protected override int BeatCount => 4;
        protected override Color EdgeBright => GsExcalibur.HolyBright;
        protected override Color BodyMain => GsExcalibur.HolyMain;
        protected override Color HotAccent => GsExcalibur.HolyHot;
        protected override Color DeepShadow => GsExcalibur.HolyDeep;

        //圣剑常亮金辉
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsExcalibur.HolyHot : GsExcalibur.HolyBright;

        private bool arcFired;
        private bool novaFired;

        private GsExcalibur Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsExcalibur : null;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 横斩
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.06f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.12f,
            },
            //拍2 疾斩：短举快出，音调上扬
            2 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.6f, Follow = 0.95f, ReachScale = 0.98f, LeanAmp = 0.04f,
                DamageMult = 0.9f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.02f,
            },
            //拍3 裁决：长举聚光、死寂滞谷、前压重劈
            _ => new GsBroadBeat {
                Raise = 9, Hold = 3, Slash = 5, Recover = 12,
                RaiseBack = 2.3f, Follow = 1.3f, ReachScale = 1.18f, LeanAmp = 0.09f,
                DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 3.2f, SwingPitch = -0.3f,
            },
        };

        //==================== 圣辉演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //裁决爆发：圣光钟鸣垫底
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.15f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = -0.4f }, Owner.Center);
            }
        }

        /// <summary>每拍斩切爆发：沿出手向放出圣光弧（刃外接管），裁决拍弧更大</summary>
        protected override void OnSlashBegin() {
            if (arcFired) {
                return;
            }
            arcFired = true;
            if (IsFinisher) {
                SetFlash(7);
            }
            Vector2 dir = baseAngle.ToRotationVector2();
            int arcDamage = Math.Max(1, (int)(Projectile.damage * 0.7f));
            SpawnOwnedProj(ModContent.ProjectileType<GsExcaliburArcProj>(),
                Hand + dir * (FullReach * 0.92f), dir * 11f, arcDamage,
                Projectile.knockBack * 0.5f, swingDir, IsFinisher ? 1f : 0f);
        }

        /// <summary>命中记账：普通拍攒圣辉；裁决拍首个命中引爆圣光新星（清空层数）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsExcalibur scheme = Scheme;
            if (scheme == null) {
                return;
            }
            if (!IsFinisher) {
                int old = scheme.Radiance;
                scheme.Radiance = Math.Min(6, scheme.Radiance + 1);
                if (old < 6 && scheme.Radiance == 6) {
                    //攒满 6 层：一记提示音 + 刃身闪
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = 0.35f }, Owner.Center);
                    SetFlash(6);
                }
                return;
            }
            if (novaFired) {
                return;
            }
            novaFired = true;
            int stacks = scheme.Radiance;
            scheme.Radiance = 0;
            int novaDamage = Math.Max(1, (int)(Projectile.damage * (0.28f + 0.06f * stacks)));
            SpawnOwnedProj(ModContent.ProjectileType<GsExcaliburNovaProj>(),
                target.Center, Vector2.Zero, novaDamage, Projectile.knockBack * 0.8f, stacks);
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (!IsFinisher || phase > PhaseHold) {
                return;
            }
            //裁决聚光：金色光尘自四周汇入刀身
            Vector2 hand = Hand;
            Vector2 at = hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 72f);
            PRTLoader.NewParticle<PRT_Light>(at, (Vector2.Lerp(hand, mainTip, 0.6f) - at) * 0.15f,
                GsExcalibur.HolyMain, Main.rand.NextFloat(0.06f, 0.11f))?.Configure(9, 0.6f);
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //金白圣火迸溅，裁决拍加量
            int motes = IsFinisher ? 6 : 3;
            for (int i = 0; i < motes; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    target.Center + Main.rand.NextVector2Circular(12f, 12f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.6f),
                    Main.rand.NextBool() ? GsExcalibur.HolyBright : GsExcalibur.HolyMain,
                    Main.rand.NextFloat(0.08f, 0.14f))?.Configure(12, 0.7f);
            }
        }

        /// <summary>裁决蓄力光环 + 圣辉计数刻光（只画给 owner，别的玩家层数不共享）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            Texture2D star = CWRAsset.StarGlow01?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (star == null || flare == null) {
                return;
            }

            //裁决拍蓄力：身侧旋转光环
            if (IsFinisher && CurrentPhase <= PhaseHold) {
                float p = CurrentPhase == PhaseHold ? 1f : MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
                Vector2 anchor = Vector2.Lerp(Hand, mainTip, 0.55f) - Main.screenPosition;
                float rot = Main.GlobalTimeWrappedHourly * 1.4f * swingDir + DrawRand01(1) * 6.28f;
                Color halo = GsExcalibur.HolyMain * (0.28f + 0.3f * p);
                halo.A = 0;
                sb.Draw(flare, anchor, null, halo, rot, flare.Size() * 0.5f, 0.36f + 0.16f * p, SpriteEffects.None, 0f);
            }

            //圣辉刻光：沿刀脊排出已攒层数
            GsExcalibur scheme = Scheme;
            int stacks = scheme?.Radiance ?? 0;
            if (stacks <= 0 || fanFade <= 0.05f) {
                return;
            }
            Vector2 hand = Hand;
            for (int i = 0; i < stacks; i++) {
                Vector2 at = hand + mainAngle.ToRotationVector2() * (mainReach * (0.30f + 0.11f * i))
                    - Main.screenPosition;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + i * 1.3f);
                Color c = GsExcalibur.HolyBright * (0.5f * fanFade * pulse);
                c.A = 0;
                sb.Draw(star, at, null, c, 0f, star.Size() * 0.5f, 0.14f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 圣光弧：每一斩放出的短程弧波，管刃外距离。出膛 2 帧撑满带过冲，
    /// 减速滑行渐薄渐透，消亡余痕光尘上飘。ai[0]=挥动符号（月牙弯向）ai[1]=裁决旗（弧更大）
    /// </summary>
    internal class GsExcaliburArcProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float SwingSign => Projectile.ai[0] >= 0f ? 1f : -1f;
        private bool Judgment => Projectile.ai[1] > 0.5f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 52;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = 26;
        }

        public override void AI() {
            Life++;
            //出膛减速：11 → 约 4，弧波是短程延伸不是远程光束
            Projectile.velocity *= 0.955f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsExcalibur.HolyMain.ToVector3() * 0.4f);

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                //航迹余痕：金尘自弧身上飘
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f),
                    GsExcalibur.HolyMain, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(10, 0.6f);
            }
        }

        public override bool? CanDamage() => Life >= 1f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? GsExcalibur.HolyBright : GsExcalibur.HolyHot,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //消散：几粒光尘缓缓上浮
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f),
                    GsExcalibur.HolyBright, Main.rand.NextFloat(0.06f, 0.1f))?.Configure(12, 0.65f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (smear == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation + SwingSign * 0.3f;
            //出生暴烈：2 帧撑满带 12% 过冲，随后回坐；消亡温和渐隐
            float grow = Life <= 2f ? 1.12f * (Life / 2f) : MathHelper.Lerp(1.12f, 1f, MathHelper.Clamp((Life - 2f) / 4f, 0f, 1f));
            float fade = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
            //行进中渐薄：厚度随寿命收窄，弧刃越飞越锋利
            float thin = MathHelper.Lerp(1f, 0.55f, MathHelper.Clamp(Life / 26f, 0f, 1f));
            float sizeMul = (Judgment ? 1.25f : 1f) * grow;

            //旧位置残弧
            for (int i = 1; i <= 3; i++) {
                Vector2 back = center - Projectile.velocity * (i * 1.6f);
                Color trail = GsExcalibur.HolyMain * (0.14f * (1f - i / 4f) * fade);
                trail.A = 0;
                Main.EntitySpriteDraw(smear, back, null, trail, rot,
                    smear.Size() * 0.5f, new Vector2(0.34f, 0.15f * thin) * sizeMul, SpriteEffects.None, 0);
            }

            //弧身：金体 + 白金刃缘细线
            Color body = GsExcalibur.HolyMain * (0.55f * fade);
            body.A = 0;
            Main.EntitySpriteDraw(smear, center, null, body, rot,
                smear.Size() * 0.5f, new Vector2(0.40f, 0.18f * thin) * sizeMul, SpriteEffects.None, 0);
            Color edge = GsExcalibur.HolyBright * (0.7f * fade);
            edge.A = 0;
            Main.EntitySpriteDraw(smear, center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 5f, null, edge, rot,
                smear.Size() * 0.5f, new Vector2(0.36f, 0.08f * thin) * sizeMul, SpriteEffects.None, 0);

            //月牙双角亮点
            Vector2 side = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2() * SwingSign;
            for (int i = -1; i <= 1; i += 2) {
                Color horn = GsExcalibur.HolyBright * (0.4f * fade);
                horn.A = 0;
                Main.EntitySpriteDraw(glow, center + side * (i * 20f * sizeMul) - Projectile.velocity.SafeNormalize(Vector2.Zero) * 4f,
                    null, horn, 0f, glow.Size() * 0.5f, 0.24f * sizeMul, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 圣光新星：裁决斩命中引爆。8 帧过冲撑到满径后回坐，伤害只在扩张期结算一次；
    /// ai[0]=圣辉层数（决定半径与观感强度）。绘制全走确定性相位，禁 Main.rand
    /// </summary>
    internal class GsExcaliburNovaProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 24;
        private int Stacks => Math.Clamp((int)Projectile.ai[0], 0, 6);
        private float MaxRadius => 96f + 16f * Stacks;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：8 帧过冲 6% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 8f, 0f, 1f);
                float burst = p < 0.7f ? 1.06f * (p / 0.7f) : MathHelper.Lerp(1.06f, 1f, (p - 0.7f) / 0.3f);
                return MaxRadius * burst;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
                //爆心圣火上涌
                int motes = 6 + Stacks * 2;
                for (int i = 0; i < motes; i++) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.8f, 2.4f),
                        Main.rand.NextBool() ? GsExcalibur.HolyBright : GsExcalibur.HolyMain,
                        Main.rand.NextFloat(0.08f, 0.16f))?.Configure(14, 0.75f);
                }
                for (int i = 0; i < 8 + Stacks * 2; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f + Stacks),
                        Main.rand.NextBool(3) ? GsExcalibur.HolyHot : GsExcalibur.HolyBright,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(14, 24));
                }
            }
            Lighting.AddLight(Projectile.Center, GsExcalibur.HolyMain.ToVector3() * (0.9f * (1f - Life01)));
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 9f ? null : false;

        /// <summary>圆形判定：目标碰到当前扩张半径即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//击退向外

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (glow == null || star == null || flare == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            float radius = Radius;

            //爆心十字星芒：首帧最亮随后蚀散
            Color flash = GsExcalibur.HolyBright * (0.8f * fade * fade);
            flash.A = 0;
            Main.EntitySpriteDraw(star, center, null, flash, SegRand(9) * 6.28f,
                star.Size() * 0.5f, 0.4f + 0.08f * Stacks, SpriteEffects.None, 0);
            Color flareC = GsExcalibur.HolyMain * (0.5f * fade);
            flareC.A = 0;
            Main.EntitySpriteDraw(flare, center, null, flareC, Life * 0.05f,
                flare.Size() * 0.5f, 0.5f * (0.6f + 0.4f * Life01), SpriteEffects.None, 0);

            //扩张光环：一圈光珠沿当前半径排布，相位确定性错开
            int beads = 12 + Stacks * 2;
            for (int i = 0; i < beads; i++) {
                float ang = MathHelper.TwoPi * i / beads + SegRand(i) * 0.4f;
                Vector2 at = center + ang.ToRotationVector2() * radius;
                float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + SegRand(i + 30) * 6.28f);
                Color bead = GsExcalibur.HolyMain * (0.55f * fade * pulse);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f,
                    0.28f + 0.1f * SegRand(i + 60), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
