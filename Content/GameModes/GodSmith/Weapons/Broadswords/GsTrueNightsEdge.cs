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
    /// 【夜潮·真夜刃】材质：夜之潮汐淬成的真夜刃，刃波是一涌会退的黑潮。
    /// 签名：①慢三拍大开大合，每一斩掷出大型夜之刃波（暗体真 alpha+幽绿刃缘），
    /// 去程减速、势尽处折返加速回坠，一去一回两段判定 ②终结拍波体更大，折返瞬间分裂两道小夜刃
    /// ③终结拍蓄力脚下升起暗潮位环、幽绿汐光汇入刀身
    /// </summary>
    internal class GsTrueNightsEdge : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.TrueNightsEdge;

        protected override int HeldProjID => ModContent.ProjectileType<GsTrueNightsEdgeHeld>();

        //慢重三拍的续段窗口放宽
        protected override int ComboResetFrames => 75;

        protected override string GsDescFallback =>
            "Reforged: the night tide; each heavy slash hurls a great wave of night that slows, stalls, " +
            "then rushes back for a second cut, and the final wave swells larger, " +
            "shedding two lesser night blades as it turns";

        //夜潮色板
        internal static readonly Color TideBright = new(172, 244, 200); //幽绿刃缘
        internal static readonly Color TideHot = new(104, 230, 160);    //潮汐幽绿
        internal static readonly Color TideBody = new(74, 62, 138);     //夜蓝紫体色
        internal static readonly Color TideDeep = new(14, 12, 36);      //深夜垫影

        //底伤不加成（原版 70/32f 全程远程大刃波）：刀身拍均 1.12x 管刃程内、大开大合触及最远，
        //刃波 0.5/0.5/0.6x 自刃尖外放出、一去一回各一段判定（走廊贴脸双段全中 ~122%，需目标存活两程），
        //终结折返分裂小夜刃 0.55x 波伤×2 向前分叉；按三拍循环约 94 帧摊算，刃内 ~114%、刃外单程 ~61%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 夜潮手持：慢三拍大开大合。0 阔横扫 / 1 反手撩斩 / 2 夜潮终结
    /// （长蓄+前压+大波折返分裂）。每拍斩切爆发自刃尖掷出夜潮刃波。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsTrueNightsEdgeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.TrueNightsEdge;
        protected override Color EdgeBright => GsTrueNightsEdge.TideBright;
        protected override Color BodyMain => GsTrueNightsEdge.TideBody;
        protected override Color HotAccent => GsTrueNightsEdge.TideHot;
        protected override Color DeepShadow => GsTrueNightsEdge.TideDeep;

        //大剑触距
        protected override float BaseReach => 126f;
        protected override float CollisionWidth => 44f;

        //夜色刀身吸光，幽绿刃缘常亮
        protected override Color BodyTint(Color lightColor) => Color.Lerp(lightColor, GsTrueNightsEdge.TideDeep, 0.28f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsTrueNightsEdge.TideHot : GsTrueNightsEdge.TideBright;

        //大开大合的残影更铺张
        protected override int GhostCount => IsFinisher ? 4 : 3;
        protected override float GhostSpacing => IsFinisher ? 0.26f : 0.2f;

        private bool waveFired;

        /// <summary>慢三拍：阔横扫 / 反手撩斩 / 夜潮终结，全员大后摆大跟进</summary>
        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 阔横扫
            0 => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 12,
                RaiseBack = 2.2f, Follow = 1.35f, ReachScale = 1.12f, LeanAmp = 0.07f,
                DamageMult = 1.0f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.2f,
            },
            //拍1 反手撩斩
            1 => new GsBroadBeat {
                Raise = 9, Hold = 3, Slash = 5, Recover = 12,
                RaiseBack = 2.35f, Follow = 1.3f, ReachScale = 1.15f, LeanAmp = 0.08f,
                DamageMult = 1.05f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.28f,
            },
            //拍2 夜潮终结：长蓄、死寂滞谷、前压重劈
            _ => new GsBroadBeat {
                Raise = 12, Hold = 4, Slash = 6, Recover = 15,
                RaiseBack = 2.55f, Follow = 1.45f, ReachScale = 1.25f, LeanAmp = 0.1f,
                DamageMult = 1.3f, Hitstop = 3, LungeSpeed = 3.5f, SwingPitch = -0.42f,
            },
        };

        //==================== 夜潮演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //夜潮爆发：深水闷响垫底
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.55f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.35f, Pitch = -0.6f }, Owner.Center);
            }
        }

        /// <summary>每拍斩切爆发：自刃尖掷出夜潮刃波（只管刃外），终结拍波体更大</summary>
        protected override void OnSlashBegin() {
            if (waveFired) {
                return;
            }
            waveFired = true;
            if (IsFinisher) {
                SetFlash(8);
            }
            Vector2 dir = baseAngle.ToRotationVector2();
            float mult = IsFinisher ? 0.6f : 0.5f;
            int dmg = Math.Max(1, (int)(Projectile.damage * mult));
            SpawnOwnedProj(ModContent.ProjectileType<GsTrueNightsEdgeWaveProj>(),
                Hand + dir * (FullReach * 0.9f), dir * (IsFinisher ? 15f : 14f), dmg,
                Projectile.knockBack * 0.6f, swingDir, IsFinisher ? 1f : 0f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.4f, Pitch = -0.35f }, Owner.Center);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (!IsFinisher || phase > PhaseHold) {
                return;
            }
            //汐光汇入：幽绿光点与暗影焰自四周涌向刀身，滞谷期加密
            int count = phase == PhaseHold ? 2 : (timer % 2 == 0 ? 1 : 0);
            for (int i = 0; i < count; i++) {
                Vector2 at = Hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(44f, 80f);
                Vector2 toBlade = (Vector2.Lerp(Hand, mainTip, 0.55f) - at) * 0.15f;
                if (Main.rand.NextBool()) {
                    PRTLoader.NewParticle<PRT_Light>(at, toBlade, GsTrueNightsEdge.TideHot,
                        Main.rand.NextFloat(0.07f, 0.12f))?.Configure(9, 0.6f);
                }
                else {
                    Dust d = Dust.NewDustPerfect(at, DustID.Shadowflame, toBlade, 120, default,
                        Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = true;
                }
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //潮汐飞沫：幽绿光沫溅起后回落
            int motes = IsFinisher ? 6 : 3;
            for (int i = 0; i < motes; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    target.Center + Main.rand.NextVector2Circular(12f, 12f),
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(-2.2f, -0.6f)),
                    Main.rand.NextBool() ? GsTrueNightsEdge.TideBright : GsTrueNightsEdge.TideHot,
                    Main.rand.NextFloat(0.08f, 0.14f))?.Configure(13, 0.7f);
            }
        }

        /// <summary>终结拍蓄势：脚下升起暗潮位环（真 alpha 暗斑扁圆轨道 + 幽绿汐光），纯演出全端可见</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!IsFinisher || CurrentPhase > PhaseHold) {
                return;
            }
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blot == null || glow == null) {
                return;
            }
            //潮位随蓄力自脚踝升到腰际
            float p = CurrentPhase == PhaseHold ? 1f : MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
            Vector2 anchor = Owner.Center + new Vector2(0f, MathHelper.Lerp(34f, 2f, p)) - Main.screenPosition;
            for (int i = 0; i < 6; i++) {
                float ang = Main.GlobalTimeWrappedHourly * 2.4f + (i * MathHelper.TwoPi / 6f) + DrawRand01(i) * 0.5f;
                Vector2 at = anchor + new Vector2(MathF.Cos(ang) * 44f, MathF.Sin(ang) * 11f);
                //暗潮体：真 alpha 暗斑压出水面剪影
                Color dark = GsTrueNightsEdge.TideDeep * (0.55f * p);
                sb.Draw(blot, at, null, dark, ang * 0.5f, blot.Size() * 0.5f,
                    new Vector2(0.13f, 0.07f), SpriteEffects.None, 0f);
                //前侧浪尖的幽绿汐光
                float front = 0.5f + 0.5f * MathF.Sin(ang);
                Color g = GsTrueNightsEdge.TideHot * (0.34f * p * front);
                g.A = 0;
                sb.Draw(glow, at - new Vector2(0f, 4f), null, g, 0f, glow.Size() * 0.5f,
                    0.15f + 0.05f * front, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 夜潮刃波：去程减速的黑潮，势尽处折返加速回坠（一去一回两段判定，判定窗由命中冷却隔开）。
    /// 暗体真 alpha 压暗+幽绿刃缘加色+拖尾渐隐；折返瞬间终结波分裂两道小夜刃向前分叉。
    /// ai[0]=挥动符号（月牙弯向）ai[1]=终结旗（波体更大）ai[2]=潮态（0 去 1 回）
    /// </summary>
    internal class GsTrueNightsEdgeWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float SwingSign => Projectile.ai[0] >= 0f ? 1f : -1f;
        private bool Big => Projectile.ai[1] > 0.5f;
        private ref float TideState => ref Projectile.ai[2];
        private ref float Life => ref Projectile.localAI[0];
        /// <summary>折返后的计时（本地演出与提速用，不过线）</summary>
        private int returnTimer;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 9;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 58;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 26;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && Big) {
                Projectile.Resize(74, 74);
            }
            Player owner = Main.player[Projectile.owner];

            if (TideState == 0f) {
                //去程：黑潮渐缓，势头一点点耗尽
                Projectile.velocity *= 0.945f;
                if (Projectile.velocity.Length() < 1.3f) {
                    //势尽折返
                    TideState = 1f;
                    Projectile.netUpdate = true;
                    Vector2 lastDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.3f, Pitch = 0.35f }, Projectile.Center);
                    }
                    //终结波折返瞬间：残势向前分叉成两道小夜刃
                    if (Big && Projectile.owner == Main.myPlayer) {
                        int dmg = Math.Max(1, (int)(Projectile.damage * 0.55f));
                        for (int i = -1; i <= 1; i += 2) {
                            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center,
                                lastDir.RotatedBy(i * 0.5f) * 8.5f,
                                ModContent.ProjectileType<GsTrueNightsEdgeShardProj>(), dmg,
                                Projectile.knockBack * 0.5f, Projectile.owner, SwingSign);
                        }
                    }
                }
            }
            else {
                //回程：向持有者加速回坠，越坠越快
                returnTimer++;
                Vector2 toOwner = (owner.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                float maxSpd = Math.Min(17f, 2.5f + returnTimer * 0.45f);
                Projectile.velocity += toOwner * 0.9f;
                if (Projectile.velocity.Length() > maxSpd) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * maxSpd;
                }
                if (Projectile.Distance(owner.Center) < 42f) {
                    Projectile.Kill();
                    return;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsTrueNightsEdge.TideHot.ToVector3() * (Big ? 0.55f : 0.4f));

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                //潮沫：暗影焰贴着波身向后拖
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Shadowflame, -Projectile.velocity * 0.1f, 110, default,
                    Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                    Main.rand.NextBool() ? GsTrueNightsEdge.TideBright : GsTrueNightsEdge.TideBody,
                    Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //潮退余痕：幽绿光沫与暗雾缓散
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 18f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.2f),
                    Main.rand.NextBool() ? GsTrueNightsEdge.TideHot : GsTrueNightsEdge.TideBody,
                    Main.rand.NextFloat(0.08f, 0.15f))?.Configure(14, 0.7f);
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
            Vector2 fwd = rot.ToRotationVector2();
            Vector2 side = (rot + MathHelper.PiOver2).ToRotationVector2() * SwingSign;
            float grow = MathHelper.Clamp(Life / 4f, 0f, 1f);          //出膛 4 帧撑满
            float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            float k = grow * fade;
            float sizeMul = Big ? 1.32f : 1f;
            float speed = Projectile.velocity.Length();
            //回程刃缘更亮：黑潮带着劲头砸回来
            float backGain = TideState == 1f ? MathHelper.Clamp(returnTimer / 14f, 0f, 1f) * 0.3f : 0f;

            //拖尾：旧位置暗紫残弧
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + (Projectile.Size * 0.5f) - screen;
                float t = 1f - (i / (float)Projectile.oldPos.Length);
                Color trail = GsTrueNightsEdge.TideBody * (0.15f * t * k);
                trail.A = 0;
                Main.EntitySpriteDraw(smear, at, null, trail, rot + (SwingSign * 0.3f),
                    smear.Size() * 0.5f, new Vector2(0.3f, 0.13f) * sizeMul * t, SpriteEffects.None, 0);
            }

            Vector2 center = Projectile.Center - screen;

            //暗体月牙：三块真 alpha 暗斑排出黑潮剪影（中间领先，两角拖后）
            for (int i = -1; i <= 1; i++) {
                Vector2 at = center + (side * (i * 22f * sizeMul)) + (fwd * (i == 0 ? 10f : -5f));
                float segScale = (i == 0 ? 1f : 0.78f) * (0.9f + 0.2f * SegRand(i + 5)) * sizeMul;
                Color dark = GsTrueNightsEdge.TideDeep * (0.74f * k);
                Main.EntitySpriteDraw(blot, at, null, dark, rot,
                    blot.Size() * 0.5f, new Vector2(0.3f, 0.17f) * segScale, SpriteEffects.None, 0);
            }

            //幽绿刃缘：加色涂抹沿前缘，回程增亮
            Color edge = GsTrueNightsEdge.TideBright * ((0.5f + backGain) * k);
            edge.A = 0;
            Main.EntitySpriteDraw(smear, center + (fwd * 9f), null, edge, rot + (SwingSign * 0.25f),
                smear.Size() * 0.5f, new Vector2(0.4f, 0.2f) * sizeMul, SpriteEffects.None, 0);

            //月牙双角亮点
            for (int i = -1; i <= 1; i += 2) {
                Vector2 horn = center + (side * (i * 25f * sizeMul)) - (fwd * 4f);
                Color hc = GsTrueNightsEdge.TideHot * ((0.45f + backGain) * k);
                hc.A = 0;
                Main.EntitySpriteDraw(glow, horn, null, hc, 0f, glow.Size() * 0.5f, 0.28f * sizeMul, SpriteEffects.None, 0);
            }

            //势尽将折返：波身屏息发亮（速度越低越亮的潮心汐光）
            if (speed < 3.5f) {
                float stall = 1f - (speed / 3.5f);
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + SegRand(17) * 6.28f);
                Color heart = GsTrueNightsEdge.TideHot * (0.5f * stall * pulse * k);
                heart.A = 0;
                Main.EntitySpriteDraw(glow, center, null, heart, 0f, glow.Size() * 0.5f,
                    (0.36f + 0.14f * stall) * sizeMul, SpriteEffects.None, 0);
            }

            //潮沫光点绕波身随行
            for (int i = 0; i < 3; i++) {
                float ang = Main.GlobalTimeWrappedHourly * 5f + (i * MathHelper.TwoPi / 3f) + SegRand(i + 20) * 6.28f;
                Vector2 at = center - (fwd * 13f) + (ang.ToRotationVector2() * 16f * sizeMul);
                Color sc = GsTrueNightsEdge.TideHot * (0.34f * k);
                sc.A = 0;
                Main.EntitySpriteDraw(glow, at, null, sc, 0f, glow.Size() * 0.5f, 0.17f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 小夜刃：终结波折返瞬间分裂的残势，向前分叉滑行渐缓。
    /// 单斑暗体+幽绿缘+短拖尾。ai[0]=挥动符号
    /// </summary>
    internal class GsTrueNightsEdgeShardProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float SwingSign => Projectile.ai[0] >= 0f ? 1f : -1f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 46;
        }

        public override void AI() {
            Life++;
            //残势滑行：前 20 帧渐缓，尾段再收
            if (Life <= 20f) {
                Projectile.velocity *= 0.97f;
            }
            else if (Projectile.timeLeft < 10) {
                Projectile.velocity *= 0.94f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, GsTrueNightsEdge.TideHot.ToVector3() * 0.25f);

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Shadowflame, -Projectile.velocity * 0.08f, 120, default,
                    Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.3f, 0.9f),
                    GsTrueNightsEdge.TideHot, Main.rand.NextFloat(0.06f, 0.11f))?.Configure(11, 0.6f);
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
            Vector2 fwd = rot.ToRotationVector2();
            float grow = MathHelper.Clamp(Life / 3f, 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 9f, 0f, 1f);
            float k = grow * fade;

            //短拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + (Projectile.Size * 0.5f) - screen;
                float t = 1f - (i / (float)Projectile.oldPos.Length);
                Color trail = GsTrueNightsEdge.TideBody * (0.13f * t * k);
                trail.A = 0;
                Main.EntitySpriteDraw(smear, at, null, trail, rot + (SwingSign * 0.3f),
                    smear.Size() * 0.5f, new Vector2(0.18f, 0.08f) * t, SpriteEffects.None, 0);
            }

            Vector2 center = Projectile.Center - screen;

            //单斑暗体
            Color dark = GsTrueNightsEdge.TideDeep * (0.7f * k);
            Main.EntitySpriteDraw(blot, center + (fwd * 4f), null, dark, rot,
                blot.Size() * 0.5f, new Vector2(0.18f, 0.1f) * (0.9f + 0.2f * SegRand(3)), SpriteEffects.None, 0);

            //幽绿缘
            Color edge = GsTrueNightsEdge.TideBright * (0.5f * k);
            edge.A = 0;
            Main.EntitySpriteDraw(smear, center + (fwd * 6f), null, edge, rot + (SwingSign * 0.25f),
                smear.Size() * 0.5f, new Vector2(0.22f, 0.09f), SpriteEffects.None, 0);

            //角端汐光
            Color hc = GsTrueNightsEdge.TideHot * (0.4f * k);
            hc.A = 0;
            Main.EntitySpriteDraw(glow, center - (fwd * 3f), null, hc, 0f, glow.Size() * 0.5f, 0.16f, SpriteEffects.None, 0);
            return false;
        }
    }
}
