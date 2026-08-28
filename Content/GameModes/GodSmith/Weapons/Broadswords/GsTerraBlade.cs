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
    /// 【泰拉之刃·双魂合鸣】材质：真圣剑与真夜刃合铸的翠绿大地圣剑，刃中同居光与夜两道剑魂。
    /// 签名：①四拍连段光魂拍与夜魂拍交替，刃缘、辉光、刃波魂色随拍轮转
    /// ②每一斩放出泰拉刃波（翠绿刃形波+炽白核线+叶脉光丝拖尾，出膛加速后缓）
    /// ③终结拍双魂合一：金紫双魂聚刃后斩出大型合鸣刃，命中引爆翠绿星环
    /// </summary>
    internal class GsTerraBlade : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.TerraBlade;

        protected override int HeldProjID => ModContent.ProjectileType<GsTerraBladeHeld>();

        protected override int ComboBeats => 4;

        //完成一次合鸣仪式的续段窗口放宽
        protected override int ComboResetFrames => 65;

        protected override string GsDescFallback =>
            "Reforged: a four-beat duet where the light soul and the night soul take turns; " +
            "every slash casts a terra wave in that soul's color, and the fourth beat fuses both souls " +
            "into a grand resonant blade that detonates a verdant star ring on hit";

        //双魂色板
        internal static readonly Color TerraBright = new(168, 255, 182);  //翠亮刃缘
        internal static readonly Color TerraMain = new(58, 208, 108);     //大地翠绿体色
        internal static readonly Color TerraGlow = new(122, 255, 150);    //翠色热光
        internal static readonly Color TerraDeep = new(10, 40, 24);       //深林垫影
        internal static readonly Color LightBright = new(255, 246, 198);  //光魂白金
        internal static readonly Color LightHot = new(255, 206, 92);      //光魂鎏金
        internal static readonly Color NightBright = new(206, 162, 255);  //夜魂亮紫
        internal static readonly Color NightHot = new(148, 86, 240);      //夜魂深紫
        internal static readonly Color FuseCore = new(240, 255, 226);     //合鸣炽白

        //底伤不加成（原版 1.4.4 本体 noMelee，全部输出在每挥 1.0x 光刃波）：贴身近战拍均约 0.39x 只管补贴身，
        //每斩魂色刃波 0.8x（疾斩拍双波 2×0.4x）接管原版光刃职责 + 终结合鸣刃 0.95x、首个命中星环 0.35x，
        //按四拍循环约 79 帧对照原版（1.0x 刃波每 18 帧）摊算，综合单体 DPS 约原版 105%~120%（星环命中才结算）
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 双魂合鸣手持：四拍连段。0 光魂横斩 / 1 夜魂返斩 / 2 双魂疾斩（金紫双细波）/
    /// 3 合鸣终结（金紫聚魂长举+前压+大型合鸣刃）。刃缘、辉光、命中反馈随拍换魂。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsTerraBladeHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.TerraBlade;
        protected override int BeatCount => 4;
        protected override float BaseReach => 126f;

        //拍号=魂：0 光 / 1 夜 / 2 双魂 / 3 合一
        internal static readonly Color[] SoulEdge = [
            GsTerraBlade.LightBright, GsTerraBlade.NightBright, GsTerraBlade.TerraBright, GsTerraBlade.FuseCore,
        ];
        internal static readonly Color[] SoulGlow = [
            GsTerraBlade.LightHot, GsTerraBlade.NightHot, GsTerraBlade.TerraGlow, new Color(255, 244, 186),
        ];

        protected override Color EdgeBright => SoulEdge[ComboStage];
        protected override Color BodyMain => GsTerraBlade.TerraMain;
        protected override Color HotAccent => SoulGlow[ComboStage];
        protected override Color DeepShadow => GsTerraBlade.TerraDeep;

        //大地圣剑常亮，刀身往翠色压
        protected override Color BodyTint(Color lightColor) => Color.Lerp(lightColor, GsTerraBlade.TerraMain, 0.18f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => SoulGlow[ComboStage];

        private bool waveFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 光魂横斩
            0 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 0.35f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
            },
            //拍1 夜魂返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 0.35f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.16f,
            },
            //拍2 双魂疾斩：短举快出、金紫双细波
            2 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 6,
                RaiseBack = 1.6f, Follow = 0.95f, ReachScale = 0.98f, LeanAmp = 0.04f,
                DamageMult = 0.30f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.04f,
            },
            //拍3 合鸣：长举聚魂、死寂滞谷、前压放出合鸣刃
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 11,
                RaiseBack = 2.3f, Follow = 1.3f, ReachScale = 1.16f, LeanAmp = 0.09f,
                DamageMult = 0.55f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.3f,
            },
        };

        //==================== 双魂演出 ====================

        protected override void HandlePhaseEvents(int phase) {
            //每拍起手闪当前魂色符光；合鸣拍加一记聚魂低鸣
            if (timer == 1) {
                SetFlash(5);
                if (IsFinisher && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.45f, Pitch = -0.35f }, Owner.Center);
                }
            }
            base.HandlePhaseEvents(phase);
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (ComboStage == 1) {
                //夜魂拍垫暗色低鸣
                SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.2f, Pitch = -0.2f }, Owner.Center);
            }
            if (IsFinisher) {
                //合鸣爆发：圣光钟鸣 + 厚响
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = -0.1f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.42f }, Owner.Center);
            }
        }

        /// <summary>每拍斩切爆发放刃波：光/夜单波、疾斩金紫双细波、合鸣大刃（除回 DamageMult 取底伤摊账）</summary>
        protected override void OnSlashBegin() {
            if (waveFired) {
                return;
            }
            waveFired = true;
            if (IsFinisher) {
                SetFlash(7);
            }
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            Vector2 dir = baseAngle.ToRotationVector2();
            Vector2 muzzle = Hand + dir * (FullReach * 0.9f);
            int waveType = ModContent.ProjectileType<GsTerraBladeWaveProj>();
            switch (ComboStage) {
                case 0:
                case 1:
                    SpawnOwnedProj(waveType, muzzle, dir * 5.5f,
                        Math.Max(1, (int)(baseDamage * 0.8f)), Projectile.knockBack * 0.5f, ComboStage, swingDir);
                    break;
                case 2:
                    //双魂疾斩：金紫双细波小角散开
                    for (int i = 0; i < 2; i++) {
                        Vector2 v = dir.RotatedBy((i == 0 ? -1 : 1) * 0.11f) * 5.5f;
                        SpawnOwnedProj(waveType, muzzle, v,
                            Math.Max(1, (int)(baseDamage * 0.4f)), Projectile.knockBack * 0.35f, i, swingDir, 0.72f);
                    }
                    break;
                default:
                    SpawnOwnedProj(ModContent.ProjectileType<GsTerraBladeBurstProj>(), muzzle, dir * 5f,
                        Math.Max(1, (int)(baseDamage * 0.95f)), Projectile.knockBack * 0.7f, swingDir);
                    break;
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (!IsFinisher || phase > PhaseHold) {
                return;
            }
            //合鸣聚魂：金紫光尘自两侧螺旋汇入刀身，滞谷期加密
            int count = phase == PhaseHold ? 2 : (timer % 2 == 0 ? 1 : 0);
            for (int i = 0; i < count; i++) {
                int soul = (timer + i) % 2;
                float orbit = Main.GlobalTimeWrappedHourly * 4.6f + soul * MathHelper.Pi;
                Vector2 at = Hand + orbit.ToRotationVector2() * Main.rand.NextFloat(44f, 72f);
                Vector2 toBlade = (Vector2.Lerp(Hand, mainTip, 0.6f) - at) * 0.16f;
                PRTLoader.NewParticle<PRT_Light>(at, toBlade,
                    soul == 0 ? GsTerraBlade.LightHot : GsTerraBlade.NightHot,
                    Main.rand.NextFloat(0.07f, 0.12f))?.Configure(9, 0.6f);
            }
        }

        /// <summary>命中反馈按魂分流：光魂金尘上飘、夜魂暗影焰、双魂各半、合鸣三色星屑齐迸</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            Vector2 c = target.Center;
            switch (ComboStage) {
                case 0:
                    LightMotes(c, 3);
                    break;
                case 1:
                    NightFlames(c, 4);
                    break;
                case 2:
                    LightMotes(c, 2);
                    NightFlames(c, 2);
                    break;
                default:
                    //合鸣：金紫翠三色星屑
                    for (int i = 0; i < 6; i++) {
                        Color sc = (i % 3) switch {
                            0 => GsTerraBlade.LightHot,
                            1 => GsTerraBlade.NightHot,
                            _ => GsTerraBlade.TerraBright,
                        };
                        PRTLoader.NewParticle<PRT_HeavenfallStar>(c,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f), sc,
                            Main.rand.NextFloat(0.26f, 0.42f))?.Configure(true, Main.rand.Next(14, 22));
                    }
                    break;
            }
        }

        private static void LightMotes(Vector2 pos, int count) {
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Light>(pos + Main.rand.NextVector2Circular(10f, 10f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.5f),
                    Main.rand.NextBool() ? GsTerraBlade.LightBright : GsTerraBlade.LightHot,
                    Main.rand.NextFloat(0.07f, 0.12f))?.Configure(11, 0.7f);
            }
        }

        private static void NightFlames(Vector2 pos, int count) {
            for (int i = 0; i < count; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Shadowflame,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 100, default,
                    Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }
        }

        /// <summary>合鸣蓄势：金紫双魂珠螺旋收拢，滞谷期合入刃身并起白绿星闪（纯绘制，identity 相位）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!IsFinisher || CurrentPhase > PhaseHold) {
                return;
            }
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glowTex == null || star == null) {
                return;
            }
            float p = CurrentPhase == PhaseHold ? 1f : MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
            Vector2 anchor = Vector2.Lerp(Hand, mainTip, 0.55f) - Main.screenPosition;
            float radius = MathHelper.Lerp(62f, 8f, p * p);
            for (int i = 0; i < 2; i++) {
                float ang = Main.GlobalTimeWrappedHourly * 5.2f + i * MathHelper.Pi + DrawRand01(i) * 6.28f;
                Vector2 at = anchor + ang.ToRotationVector2() * radius;
                float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + i * 2.4f);
                Color c = (i == 0 ? GsTerraBlade.LightHot : GsTerraBlade.NightHot) * ((0.45f + 0.4f * p) * pulse);
                c.A = 0;
                sb.Draw(glowTex, at, null, c, 0f, glowTex.Size() * 0.5f, 0.32f + 0.1f * p, SpriteEffects.None, 0f);
            }
            //滞谷合一瞬间：刃身上炸开白绿星光
            if (CurrentPhase == PhaseHold) {
                float flashPulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 14f);
                Color fuse = GsTerraBlade.FuseCore * (0.55f * flashPulse);
                fuse.A = 0;
                sb.Draw(star, anchor, null, fuse, Main.GlobalTimeWrappedHourly * 0.8f,
                    star.Size() * 0.5f, 0.34f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 泰拉刃波：每一斩放出的魂色刃形波。翠绿刃身+魂色前缘+炽白核线+叶脉光丝拖尾；
    /// 出膛 5.5→约 15 加速再缓（禁匀速），行进渐薄。
    /// ai[0]=魂（0 光 1 夜）ai[1]=挥动符号 ai[2]=体型倍率（0=1）
    /// </summary>
    internal class GsTerraBladeWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int Soul => Projectile.ai[0] >= 1f ? 1 : 0;
        private float SwingSign => Projectile.ai[1] >= 0f ? 1f : -1f;
        private float SizeMul => Projectile.ai[2] > 0.01f ? Projectile.ai[2] : 1f;
        private ref float Life => ref Projectile.localAI[0];

        private Color SoulBright => Soul == 0 ? GsTerraBlade.LightBright : GsTerraBlade.NightBright;
        private Color SoulHot => Soul == 0 ? GsTerraBlade.LightHot : GsTerraBlade.NightHot;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = 42;
        }

        public override void AI() {
            Life++;
            //出膛加速后缓：5.5 → 约 15 再缓滑（全程不匀速）
            if (Life <= 11f) {
                Projectile.velocity *= 1.095f;
            }
            else {
                Projectile.velocity *= 0.985f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsTerraBlade.TerraMain.ToVector3() * 0.4f);

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                //航迹余痕：翠色光尘微微后曳
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    -Projectile.velocity * 0.06f, GsTerraBlade.TerraMain,
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(10, 0.6f);
            }
        }

        public override bool? CanDamage() => Life >= 2f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? SoulBright : GsTerraBlade.TerraBright,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //消散余痕：翠尘与魂色光珠缓散
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.3f, 0.9f),
                    Main.rand.NextBool() ? GsTerraBlade.TerraMain : SoulHot,
                    Main.rand.NextFloat(0.05f, 0.1f))?.Configure(12, 0.6f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (smear == null || glow == null) {
                return false;
            }
            Vector2 screen = Main.screenPosition;
            Vector2 center = Projectile.Center - screen;
            float rot = Projectile.rotation + SwingSign * 0.22f;
            //出生 3 帧撑满带 10% 过冲；消亡渐隐；行进渐薄如刃越飞越锋利
            float grow = Life <= 3f
                ? 1.10f * (Life / 3f)
                : MathHelper.Lerp(1.10f, 1f, MathHelper.Clamp((Life - 3f) / 5f, 0f, 1f));
            float fade = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
            float thin = MathHelper.Lerp(1f, 0.55f, MathHelper.Clamp(Life / 42f, 0f, 1f));
            float k = grow * fade;
            float size = SizeMul * grow;
            Vector2 fwd = Projectile.rotation.ToRotationVector2();

            //叶脉光丝：旧位两侧交替甩细斜光丝，如叶脉自中脉分岔；中脉残波垫底
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - screen;
                float t = 1f - i / (float)Projectile.oldPos.Length;
                int vSide = i % 2 == 0 ? 1 : -1;
                Color vein = GsTerraBlade.TerraMain * (0.20f * t * k);
                vein.A = 0;
                Main.EntitySpriteDraw(smear, at, null, vein, Projectile.rotation + vSide * 0.55f,
                    smear.Size() * 0.5f, new Vector2(0.16f, 0.030f) * (t * size + 0.2f), SpriteEffects.None, 0);
                Color trail = GsTerraBlade.TerraMain * (0.12f * t * k);
                trail.A = 0;
                Main.EntitySpriteDraw(smear, at, null, trail, rot,
                    smear.Size() * 0.5f, new Vector2(0.30f, 0.10f * thin) * (t * size), SpriteEffects.None, 0);
            }

            //刃身：翠绿刃形主体
            Color body = GsTerraBlade.TerraMain * (0.55f * k);
            body.A = 0;
            Main.EntitySpriteDraw(smear, center, null, body, rot,
                smear.Size() * 0.5f, new Vector2(0.52f, 0.15f * thin) * size, SpriteEffects.None, 0);
            //魂色前缘
            Color edge = SoulBright * (0.65f * k);
            edge.A = 0;
            Main.EntitySpriteDraw(smear, center + fwd * 6f, null, edge, rot,
                smear.Size() * 0.5f, new Vector2(0.46f, 0.075f * thin) * size, SpriteEffects.None, 0);
            //炽白核线：细窄一线压正中
            Color core = GsTerraBlade.FuseCore * (0.85f * k);
            core.A = 0;
            Main.EntitySpriteDraw(smear, center + fwd * 3f, null, core, rot,
                smear.Size() * 0.5f, new Vector2(0.40f, 0.028f * thin) * size, SpriteEffects.None, 0);
            //刃尖光点
            Color tip = SoulHot * (0.5f * k);
            tip.A = 0;
            Main.EntitySpriteDraw(glow, center + fwd * (26f * size), null, tip, 0f,
                glow.Size() * 0.5f, 0.22f * size, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 合鸣刃：终结拍斩出的大型双魂刃波。金紫双缘辫绕翠绿刃身、炽白核线加粗、双魂光点绕行；
    /// 出膛 5→约 14.5 加速后缓；首个命中引爆翠绿星环。ai[0]=挥动符号
    /// </summary>
    internal class GsTerraBladeBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float SwingSign => Projectile.ai[0] >= 0f ? 1f : -1f;
        private ref float Life => ref Projectile.localAI[0];
        private bool ringFired;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 26;
            Projectile.timeLeft = 52;
        }

        public override void AI() {
            Life++;
            //出膛加速后缓：5 → 约 14.5 再缓滑（全程不匀速）
            if (Life <= 13f) {
                Projectile.velocity *= 1.085f;
            }
            else {
                Projectile.velocity *= 0.988f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsTerraBlade.TerraGlow.ToVector3() * 0.6f);

            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                //双魂随行：金紫光尘交替渗出
                Color c = Main.rand.NextBool() ? GsTerraBlade.LightHot : GsTerraBlade.NightHot;
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(18f, 18f),
                    -Projectile.velocity * 0.08f, c,
                    Main.rand.NextFloat(0.06f, 0.1f))?.Configure(11, 0.6f);
            }
        }

        public override bool? CanDamage() => Life >= 2f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //首个命中引爆翠绿星环（owner 生成，随包全端可见）
            if (Projectile.owner == Main.myPlayer && !ringFired) {
                ringFired = true;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsTerraBladeRingProj>(),
                    Math.Max(1, (int)(Projectile.damage * 0.37f)), Projectile.knockBack * 0.7f, Projectile.owner);
            }
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Color c = (i % 3) switch {
                    0 => GsTerraBlade.LightHot,
                    1 => GsTerraBlade.NightHot,
                    _ => GsTerraBlade.TerraBright,
                };
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 6.5f), c,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //散场余痕：金紫翠光珠缓散
            for (int i = 0; i < 7; i++) {
                Color c = (i % 3) switch {
                    0 => GsTerraBlade.LightHot,
                    1 => GsTerraBlade.NightHot,
                    _ => GsTerraBlade.TerraMain,
                };
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.2f), c,
                    Main.rand.NextFloat(0.07f, 0.12f))?.Configure(14, 0.7f);
            }
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (smear == null || glow == null || star == null) {
                return false;
            }
            Vector2 screen = Main.screenPosition;
            Vector2 center = Projectile.Center - screen;
            float rot = Projectile.rotation + SwingSign * 0.26f;
            float grow = Life <= 3f
                ? 1.12f * (Life / 3f)
                : MathHelper.Lerp(1.12f, 1f, MathHelper.Clamp((Life - 3f) / 5f, 0f, 1f));
            float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            float k = grow * fade;
            Vector2 fwd = Projectile.rotation.ToRotationVector2();
            Vector2 side = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();

            //拖尾：旧位翠色残弧
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - screen;
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Color trail = GsTerraBlade.TerraMain * (0.16f * t * k);
                trail.A = 0;
                Main.EntitySpriteDraw(smear, at, null, trail, rot,
                    smear.Size() * 0.5f, new Vector2(0.40f, 0.16f) * t * grow, SpriteEffects.None, 0);
            }

            //刃身：加大号翠绿主体
            Color body = GsTerraBlade.TerraMain * (0.6f * k);
            body.A = 0;
            Main.EntitySpriteDraw(smear, center, null, body, rot,
                smear.Size() * 0.5f, new Vector2(0.66f, 0.22f) * grow, SpriteEffects.None, 0);

            //金紫双缘辫绕：随寿命正弦换边，如两魂缠刃而行
            float braid = MathF.Sin(Life * 0.5f + SegRand(7) * 6.28f);
            Color goldEdge = GsTerraBlade.LightHot * (0.55f * k);
            goldEdge.A = 0;
            Main.EntitySpriteDraw(smear, center + side * (8f * braid) + fwd * 7f, null, goldEdge, rot,
                smear.Size() * 0.5f, new Vector2(0.56f, 0.07f) * grow, SpriteEffects.None, 0);
            Color nightEdge = GsTerraBlade.NightHot * (0.55f * k);
            nightEdge.A = 0;
            Main.EntitySpriteDraw(smear, center - side * (8f * braid) + fwd * 7f, null, nightEdge, rot,
                smear.Size() * 0.5f, new Vector2(0.56f, 0.07f) * grow, SpriteEffects.None, 0);

            //炽白核线加粗
            Color core = GsTerraBlade.FuseCore * (0.9f * k);
            core.A = 0;
            Main.EntitySpriteDraw(smear, center + fwd * 4f, null, core, rot,
                smear.Size() * 0.5f, new Vector2(0.50f, 0.045f) * grow, SpriteEffects.None, 0);

            //月牙双角亮点 + 刃尖星光
            for (int i = -1; i <= 1; i += 2) {
                Color horn = GsTerraBlade.TerraBright * (0.45f * k);
                horn.A = 0;
                Main.EntitySpriteDraw(glow, center + side * (i * 26f * grow) - fwd * 5f, null, horn, 0f,
                    glow.Size() * 0.5f, 0.28f, SpriteEffects.None, 0);
            }
            Color tipStar = GsTerraBlade.FuseCore * (0.6f * k);
            tipStar.A = 0;
            Main.EntitySpriteDraw(star, center + fwd * (34f * grow), null, tipStar,
                Life * 0.12f, star.Size() * 0.5f, 0.3f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 翠绿星环：合鸣刃命中引爆的扩张星环。8 帧过冲撑到满径回坐，伤害只在扩张期结算一次；
    /// 环珠金紫翠三色相间，环心白绿星芒。绘制全走确定性相位，禁 Main.rand
    /// </summary>
    internal class GsTerraBladeRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 22;
        private const float MaxRadius = 128f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        //三色环珠轮转表
        private static readonly Color[] RingColors = [
            GsTerraBlade.LightHot, GsTerraBlade.TerraBright, GsTerraBlade.NightHot,
        ];

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
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = -0.25f }, Projectile.Center);
                //爆心三色星屑上涌
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(
                        Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                        -Vector2.UnitY * Main.rand.NextFloat(1f, 3f) + Main.rand.NextVector2Circular(1.5f, 0.5f),
                        RingColors[i % 3], Main.rand.NextFloat(0.28f, 0.46f))?.Configure(true, Main.rand.Next(16, 26));
                }
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                    GsTerraBlade.TerraGlow, 0.34f)?.Configure(12, 0.9f);
            }
            Lighting.AddLight(Projectile.Center, GsTerraBlade.TerraGlow.ToVector3() * (0.8f * (1f - Life01)));
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

            //环心星芒：首帧最亮随后蚀散
            Color flash = GsTerraBlade.FuseCore * (0.8f * fade * fade);
            flash.A = 0;
            Main.EntitySpriteDraw(star, center, null, flash, SegRand(9) * 6.28f,
                star.Size() * 0.5f, 0.44f, SpriteEffects.None, 0);
            Color flareC = GsTerraBlade.TerraGlow * (0.5f * fade);
            flareC.A = 0;
            Main.EntitySpriteDraw(flare, center, null, flareC, Life * 0.05f,
                flare.Size() * 0.5f, 0.5f * (0.6f + 0.4f * Life01), SpriteEffects.None, 0);

            //扩张星环：三色光珠沿当前半径排布，相位确定性错开
            const int beads = 15;
            for (int i = 0; i < beads; i++) {
                float ang = MathHelper.TwoPi * i / beads + SegRand(i) * 0.4f;
                Vector2 at = center + ang.ToRotationVector2() * radius;
                float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + SegRand(i + 30) * 6.28f);
                Color bead = RingColors[i % 3] * (0.55f * fade * pulse);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f,
                    0.26f + 0.1f * SegRand(i + 60), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
