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
    /// 【夜刃·四魂合一】材质：四剑之魂合铸的夜色暗刃（蚀影/血肉/草叶/狱炎熔于一体）。
    /// 签名：①五拍连段前四拍逐一显现四把源剑之魂：魂色刃缘、专属几何、专属附伤各不相同
    /// ②第五拍「合一」：四色光丝汇入刀身、死寂滞谷后爆发，斩出贯穿的夜之刃波
    /// ③每拍起手刃身闪对应魂色符光，命中反馈按魂分流
    /// </summary>
    internal class GsNightsEdge : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.NightsEdge;

        protected override int HeldProjID => ModContent.ProjectileType<GsNightsEdgeHeld>();

        protected override int ComboBeats => 5;

        //完成一次合一仪式的时间窗放宽
        protected override int ComboResetFrames => 70;

        protected override string GsDescFallback =>
            "Reforged: a five-beat rite; the first four strikes each channel one of the blades that forged it, " +
            "and the fifth fuses all four souls into a piercing wave of night";

        //底伤 +5%（签名机制强则底伤加成弱）：五拍平均倍率 1.13 + 每五拍一道夜之刃波（约 0.63x 底伤），
        //拍 3/4 时长偏长拉低节奏，按 max(useTime, 弹幕总帧) 摊算综合 DPS 约原版 107%~119%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 夜刃手持：五拍四魂连段。拍 0 蚀影快斩 / 拍 1 血肉重劈 / 拍 2 草刃长扫 /
    /// 拍 3 炎阳沉劈（前压）/ 拍 4 合一（四色聚魂长蓄+死寂滞谷+爆发放波）。
    /// 刃缘色、涂抹色、辉光色、附伤、命中反馈全部随拍号轮转。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsNightsEdgeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.NightsEdge;
        protected override int BeatCount => 5;

        //==================== 四魂色板（索引=拍号，4=合一夜色） ====================

        internal static readonly Color[] SoulHot = [
            new(168, 64, 255),   //蚀影紫
            new(232, 48, 64),    //血肉红
            new(108, 220, 76),   //草叶绿
            new(255, 138, 40),   //狱炎橙
            new(150, 90, 255),   //合一·夜紫
        ];
        internal static readonly Color[] SoulBright = [
            new(196, 156, 255),
            new(255, 120, 120),
            new(188, 255, 128),
            new(255, 200, 96),
            new(216, 180, 255),
        ];
        internal static readonly Color NightBody = new(88, 66, 150);
        internal static readonly Color NightDeep = new(26, 14, 46);

        protected override Color EdgeBright => SoulBright[ComboStage];
        protected override Color BodyMain => NightBody;
        protected override Color HotAccent => SoulHot[ComboStage];
        protected override Color DeepShadow => NightDeep;

        //夜色刀身吸光，刃缘常亮当前魂色
        protected override Color BodyTint(Color lightColor) => Color.Lerp(lightColor, NightDeep, 0.25f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => SoulHot[ComboStage];

        private bool waveFired;

        /// <summary>五拍几何各不相同：快斩/重劈/长扫/沉劈/合一</summary>
        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 蚀影之魂：轻快斜斩
            0 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 3, Recover = 8,
                RaiseBack = 1.7f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
            },
            //拍1 血肉之魂：沉重劈砍
            1 => new GsBroadBeat {
                Raise = 7, Hold = 2, Slash = 4, Recover = 10,
                RaiseBack = 2.0f, Follow = 0.9f, ReachScale = 1.02f, LeanAmp = 0.06f,
                DamageMult = 1.1f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.22f,
            },
            //拍2 草刃之魂：大弧长扫（触及最远）
            2 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 5, Recover = 9,
                RaiseBack = 1.8f, Follow = 1.35f, ReachScale = 1.2f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.1f,
            },
            //拍3 炎阳之魂：巨剑式沉劈+前压
            3 => new GsBroadBeat {
                Raise = 9, Hold = 3, Slash = 5, Recover = 11,
                RaiseBack = 2.3f, Follow = 1.05f, ReachScale = 1.08f, LeanAmp = 0.08f,
                DamageMult = 1.15f, Hitstop = 2, LungeSpeed = 2.5f, SwingPitch = -0.32f,
            },
            //拍4 合一：长蓄聚魂、死寂滞谷、爆发放波
            _ => new GsBroadBeat {
                Raise = 12, Hold = 4, Slash = 4, Recover = 13,
                RaiseBack = 2.4f, Follow = 1.4f, ReachScale = 1.22f, LeanAmp = 0.1f,
                DamageMult = 1.4f, Hitstop = 3, LungeSpeed = 4.0f, SwingPitch = -0.4f,
            },
        };

        //==================== 仪式演出 ====================

        protected override void HandlePhaseEvents(int phase) {
            //每拍起手：刃身闪对应魂色符光；合一拍再加一记暗色聚魂低鸣
            if (timer == 1) {
                SetFlash(5);
                if (IsFinisher && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.5f }, Owner.Center);
                }
            }
            base.HandlePhaseEvents(phase);
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //合一爆发：恶魔低鸣垫底
                SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.4f, Pitch = -0.25f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = -0.5f }, Owner.Center);
            }
        }

        /// <summary>合一拍爆发：斩出夜之刃波（0.45x 当前拍伤害，约 0.63x 底伤）</summary>
        protected override void OnSlashBegin() {
            if (!IsFinisher || waveFired) {
                return;
            }
            waveFired = true;
            SetFlash(7);
            Vector2 dir = baseAngle.ToRotationVector2();
            SpawnOwnedProj(ModContent.ProjectileType<GsNightsEdgeWaveProj>(),
                Hand + dir * 30f, dir * 15f, Math.Max(1, (int)(Projectile.damage * 0.45f)),
                Projectile.knockBack * 0.6f, swingDir);
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (!IsFinisher || phase > PhaseHold) {
                return;
            }
            //聚魂：四色光丝自四方螺旋汇入刀身，滞谷期密度翻倍
            int count = phase == PhaseHold ? 2 : (timer % 2 == 0 ? 1 : 0);
            for (int i = 0; i < count; i++) {
                int soul = (timer + i) % 4;
                float orbit = Main.GlobalTimeWrappedHourly * 4.2f + soul * MathHelper.PiOver2;
                float dist = Main.rand.NextFloat(46f, 76f);
                Vector2 at = Hand + orbit.ToRotationVector2() * dist;
                Vector2 toBlade = (Vector2.Lerp(Hand, mainTip, 0.55f) - at) * 0.16f;
                PRTLoader.NewParticle<PRT_Light>(at, toBlade, SoulHot[soul],
                    Main.rand.NextFloat(0.07f, 0.12f))?.Configure(9, 0.6f);
            }
        }

        /// <summary>命中附伤按魂分流（owner 端，原版会同步 buff）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            switch (ComboStage) {
                case 0: target.AddBuff(BuffID.ShadowFlame, 120); break;
                case 1: target.AddBuff(BuffID.Bleeding, 180); break;
                case 2: target.AddBuff(BuffID.Poisoned, 240); break;
                case 3: target.AddBuff(BuffID.OnFire, 180); break;
                default: target.AddBuff(BuffID.ShadowFlame, 240); break;
            }
        }

        /// <summary>命中反馈按魂分流；合一拍四色齐迸</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            Vector2 c = target.Center;
            switch (ComboStage) {
                case 0:
                    SoulDust(c, DustID.Shadowflame, 5, 1.2f);
                    break;
                case 1:
                    SoulDust(c, DustID.Blood, 7, 1.4f);
                    break;
                case 2:
                    SoulDust(c, DustID.GrassBlades, 5, 1.1f);
                    SoulDust(c, DustID.Poisoned, 3, 1f);
                    break;
                case 3:
                    SoulDust(c, DustID.Torch, 7, 1.4f);
                    break;
                default: {
                    //合一：四色魂火环形齐迸 + 大记夜紫闪
                    PRTLoader.NewParticle<PRT_Light>(c, Vector2.Zero, SoulHot[4], 0.34f)?.Configure(12, 0.9f);
                    for (int i = 0; i < 12; i++) {
                        Vector2 vel = (MathHelper.TwoPi * i / 12f).ToRotationVector2()
                            * Main.rand.NextFloat(3f, 7f);
                        PRTLoader.NewParticle<PRT_Spark>(c, vel, SoulHot[i % 4],
                            Main.rand.NextFloat(0.4f, 0.65f))?.Configure(true, Main.rand.Next(16, 26));
                    }
                    break;
                }
            }
        }

        private static void SoulDust(Vector2 pos, short dustType, int count, float scale) {
            for (int i = 0; i < count; i++) {
                Dust d = Dust.NewDustPerfect(pos, dustType,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f), 60, default,
                    Main.rand.NextFloat(0.8f, scale + 0.3f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>合一拍蓄势：四枚魂珠绕刀身螺旋收拢（纯绘制，identity 相位）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!IsFinisher || CurrentPhase > PhaseHold) {
                return;
            }
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex == null) {
                return;
            }
            //收拢进度：举相 0→1，滞谷保持 1
            float p = CurrentPhase == PhaseHold ? 1f : MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
            float radius = MathHelper.Lerp(58f, 10f, p * p);
            Vector2 anchor = Vector2.Lerp(Hand, mainTip, 0.5f) - Main.screenPosition;
            for (int i = 0; i < 4; i++) {
                float ang = Main.GlobalTimeWrappedHourly * 5f + i * MathHelper.PiOver2 + DrawRand01(i) * 6.28f;
                Vector2 at = anchor + ang.ToRotationVector2() * radius;
                float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + i * 1.7f);
                Color c = SoulHot[i] * (0.5f + 0.4f * p) * pulse;
                c.A = 0;
                sb.Draw(glowTex, at, null, c, 0f, glowTex.Size() * 0.5f, 0.34f + 0.1f * p, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 夜之刃波：合一拍斩出的贯穿暗弧。暗体真 alpha 压暗、紫刃缘加色、
    /// 四魂色光点随行、拖尾渐隐；出膛 15→10 减速回稳（不匀速直飞）。
    /// ai[0]=挥动符号（决定月牙弯向）。命中上暗影焰，消亡四色魂屑散场
    /// </summary>
    internal class GsNightsEdgeWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float SwingSign => Projectile.ai[0] >= 0f ? 1f : -1f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 66;
        }

        public override void AI() {
            Life++;
            //出膛减速回稳：前 18 帧 15→约 10，之后匀稳滑行，尾段轻微收速
            if (Life <= 18f) {
                Projectile.velocity *= 0.978f;
            }
            else if (Projectile.timeLeft < 14) {
                Projectile.velocity *= 0.96f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsNightsEdgeHeld.SoulHot[4].ToVector3() * 0.5f);

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                //刃缘渗出暗影焰，微微向后拖
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Shadowflame, -Projectile.velocity * 0.12f, 110, default,
                    Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer) {
                target.AddBuff(BuffID.ShadowFlame, 120);
            }
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                    GsNightsEdgeHeld.SoulHot[i % 4], Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //散场余痕：四色魂屑缓散
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 18f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.2f),
                    GsNightsEdgeHeld.SoulHot[i % 4], Main.rand.NextFloat(0.08f, 0.15f))?.Configure(14, 0.7f);
            }
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blot == null || smear == null || glow == null) {
                return false;
            }
            Vector2 screen = Main.screenPosition;
            float rot = Projectile.rotation;
            float grow = MathHelper.Clamp(Life / 4f, 0f, 1f);          //出膛 4 帧撑满
            float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            float k = grow * fade;
            Vector2 fwd = rot.ToRotationVector2();
            Vector2 side = (rot + MathHelper.PiOver2).ToRotationVector2() * SwingSign;

            //拖尾：旧位置的紫弧渐隐
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - screen;
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Color trail = GsNightsEdgeHeld.SoulHot[4] * (0.16f * t * k);
                trail.A = 0;
                Main.EntitySpriteDraw(smear, at, null, trail, rot + SwingSign * 0.3f,
                    smear.Size() * 0.5f, new Vector2(0.30f, 0.13f) * t, SpriteEffects.None, 0);
            }

            Vector2 center = Projectile.Center - screen;

            //暗体月牙：三块真 alpha 暗斑排出月牙剪影（中间领先，两角拖后）
            for (int i = -1; i <= 1; i++) {
                Vector2 at = center + side * (i * 21f) + fwd * (i == 0 ? 9f : -5f);
                float segScale = (i == 0 ? 1f : 0.78f) * (0.9f + 0.2f * SegRand(i + 5));
                Color dark = GsNightsEdgeHeld.NightDeep * (0.72f * k);
                Main.EntitySpriteDraw(blot, at, null, dark, rot,
                    blot.Size() * 0.5f, new Vector2(0.30f, 0.17f) * segScale, SpriteEffects.None, 0);
            }

            //紫刃缘：加色涂抹沿前缘
            Color edge = GsNightsEdgeHeld.SoulBright[4] * (0.55f * k);
            edge.A = 0;
            Main.EntitySpriteDraw(smear, center + fwd * 8f, null, edge, rot + SwingSign * 0.25f,
                smear.Size() * 0.5f, new Vector2(0.4f, 0.2f), SpriteEffects.None, 0);

            //月牙双角亮点
            for (int i = -1; i <= 1; i += 2) {
                Vector2 horn = center + side * (i * 24f) - fwd * 4f;
                Color hc = GsNightsEdgeHeld.SoulBright[4] * (0.5f * k);
                hc.A = 0;
                Main.EntitySpriteDraw(glow, horn, null, hc, 0f, glow.Size() * 0.5f, 0.3f, SpriteEffects.None, 0);
            }

            //四魂色光点绕波身随行（identity 相位错开）
            for (int i = 0; i < 4; i++) {
                float ang = Main.GlobalTimeWrappedHourly * 6f + i * MathHelper.PiOver2 + SegRand(i + 20) * 6.28f;
                Vector2 at = center - fwd * 14f + ang.ToRotationVector2() * 17f;
                float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + i * 1.9f);
                Color sc = GsNightsEdgeHeld.SoulHot[i] * (0.4f * k * pulse);
                sc.A = 0;
                Main.EntitySpriteDraw(glow, at, null, sc, 0f, glow.Size() * 0.5f, 0.2f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
