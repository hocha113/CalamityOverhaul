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
    /// 【常青灯树圣剑】材质：缀满彩灯与饰品的节日松木魔剑。
    /// 签名：①刃脊灯串四色流转明灭，节日感常驻 ②连段轮抛三种饰品：红球落地爆裂、
    /// 金星旋转穿刺、礼盒炸成碎片扇（原版饰品星保留并升级为轮换）
    /// ③终结礼盒落点开出小圣诞光树，灯串逐层点亮、周期洒星屑伤害 ④命中彩屑迸溅+铃音
    /// </summary>
    internal class GsChristmasTreeSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.ChristmasTreeSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsChristmasTreeSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: each slash hurls a festive ornament in turn: a bursting bauble, " +
            "a piercing gold star, then a gift box that cracks into shards " +
            "and raises a little tree of blinking lights that sprinkles stinging stardust";

        //节日灯树色板
        internal static readonly Color FestSnow = new(255, 248, 232);  //暖白灯
        internal static readonly Color FestPine = new(88, 176, 104);   //松绿体色
        internal static readonly Color FestRed = new(232, 62, 70);     //饰品红
        internal static readonly Color FestGold = new(255, 208, 96);   //饰品金
        internal static readonly Color FestBlue = new(122, 172, 255);  //冷蓝灯
        internal static readonly Color FestDeep = new(13, 30, 21);     //松影深绿

        /// <summary>灯串四色轮转（刃脊灯/光树灯/彩屑共用）</summary>
        internal static readonly Color[] LightCycle = [FestRed, FestGold, FestBlue, FestSnow];

        //底伤不加成：拍伤 1.0/1.0/1.3 + 饰品轮换 红球0.7x/金星0.7x/礼盒0.6x + 礼盒碎片 4×0.2x + 光树 0.25x×4跳
        //循环 71 帧（23+23+25）全中口径 ≈7.1 单位 vs 原版全套(挥1.0+饰品星1.0)/23 帧同窗 6.17 → 综合约 105%~113%
        //碎片扇与光树洒屑对群是 AoE 收益，单体典型吃不满全中口径
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 灯树圣剑手持：三拍轻快连击，铃音逐拍上行。0 红球拍 / 1 金星拍 / 2 礼盒终结
    /// （重敲+前压，礼盒落点开光树）。刃脊灯串在 DrawExtra 常驻流转。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsChristmasTreeSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.ChristmasTreeSword;
        protected override Color EdgeBright => GsChristmasTreeSword.FestSnow;
        protected override Color BodyMain => GsChristmasTreeSword.FestPine;
        protected override Color HotAccent => GsChristmasTreeSword.FestRed;
        protected override Color DeepShadow => GsChristmasTreeSword.FestDeep;

        //松木染绿；彩屑代血
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsChristmasTreeSword.FestPine, 0.16f);
        protected override bool GlowAlways => IsFinisher;
        protected override Color GlowColor => GsChristmasTreeSword.FestGold;
        protected override bool BleedOnFlesh => false;

        private bool ornamentThrown;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 红球斩：轻快高音
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.8f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.12f,
            },
            //拍1 金星斩：回手更高一格
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.7f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.26f,
            },
            //拍2 礼盒重敲：前压抛盒
            _ => new GsBroadBeat {
                Raise = 7, Hold = 3, Slash = 5, Recover = 10,
                RaiseBack = 2.15f, Follow = 1.2f, ReachScale = 1.12f, LeanAmp = 0.08f,
                DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.6f, SwingPitch = -0.1f,
            },
        };

        //==================== 节日演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            //铃音节拍：连段逐拍上行，终结补一记高音和声
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.4f, Pitch = 0.08f + 0.18f * ComboStage }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = 0.65f }, Owner.Center);
            }
        }

        /// <summary>圣诞轮换：每拍斩切爆发抛出本拍饰品（除回拍伤取底伤摊账）</summary>
        protected override void OnSlashBegin() {
            if (ornamentThrown) {
                return;
            }
            ornamentThrown = true;
            if (IsFinisher) {
                SetFlash(6);
            }
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            int mode = ComboStage;
            float frac = mode == 2 ? 0.6f : 0.7f;
            Vector2 dir = baseAngle.ToRotationVector2();
            Vector2 vel = mode switch {
                0 => dir * 8.5f + new Vector2(0f, -2.6f),  //红球：饱满抛物线
                1 => dir * 12.5f + new Vector2(0f, -1.2f), //金星：快而平
                _ => dir * 7.5f + new Vector2(0f, -3.4f),  //礼盒：高抛落点开树
            };
            SpawnOwnedProj(ModContent.ProjectileType<GsChristmasTreeSwordOrnamentProj>(),
                Hand + dir * (FullReach * 0.55f), vel,
                Math.Max(1, (int)(baseDamage * frac)), Projectile.knockBack * 0.5f, mode);
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (IsFinisher && phase is PhaseRaise or PhaseHold) {
                //重敲蓄势：雪白星尘向刃身收拢
                Vector2 blade = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.4f, 1f));
                Vector2 from = blade + Main.rand.NextVector2Unit() * Main.rand.NextFloat(32f, 60f);
                PRTLoader.NewParticle<PRT_Light>(from, (blade - from) * 0.14f,
                    Main.rand.NextBool() ? GsChristmasTreeSword.FestSnow : GsChristmasTreeSword.FestGold,
                    Main.rand.NextFloat(0.05f, 0.1f))?.Configure(9, 0.6f);
            }
        }

        /// <summary>命中反馈：四色彩屑迸溅 + 碎铃音</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            SoundEngine.PlaySound(SoundID.Item35 with {
                Volume = 0.26f,
                Pitch = Main.rand.NextFloat(0.4f, 0.7f),
                MaxInstances = 3
            }, target.Center);
            int bits = IsFinisher ? 8 : 5;
            for (int i = 0; i < bits; i++) {
                //彩屑按索引轮色
                Color c = GsChristmasTreeSword.LightCycle[i % 4];
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6.5f),
                    c, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(12, 22));
            }
        }

        /// <summary>刃脊灯串：四色灯泡沿刀脊流转明灭；终结拍刀尖亮金星顶饰（纯演出，各端可见）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (fanFade <= 0.05f) {
                return;
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return;
            }
            Vector2 hand = Hand;
            for (int i = 0; i < 6; i++) {
                float t = 0.32f + 0.13f * i;
                Vector2 at = hand + mainAngle.ToRotationVector2() * (mainReach * t) - Main.screenPosition;
                //逐灯错相的追逐式明灭
                float blink = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(
                    Main.GlobalTimeWrappedHourly * 5.5f + i * 1.9f + DrawRand01(i + 3) * 6.28f));
                Color bulb = GsChristmasTreeSword.LightCycle[i % 4] * (0.55f * blink * fanFade);
                bulb.A = 0;
                sb.Draw(glow, at, null, bulb, 0f, glow.Size() * 0.5f, 0.11f, SpriteEffects.None, 0f);
            }
            //终结拍：刀尖金星顶饰，蓄力期越亮
            if (IsFinisher) {
                float charge = CurrentPhase <= PhaseHold
                    ? MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f) : 1f;
                float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + DrawRand01(9) * 6.28f);
                Color tip = GsChristmasTreeSword.FestGold * (0.65f * charge * pulse * fanFade);
                tip.A = 0;
                sb.Draw(star, mainTip - Main.screenPosition, null, tip,
                    Main.GlobalTimeWrappedHourly * 1.5f, star.Size() * 0.5f,
                    0.34f + 0.1f * charge, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 节日饰品：ai[0]=模式（0 红球：重力抛物+弹跳一次+爆裂；1 金星：平抛自旋穿 3；
    /// 2 礼盒：高抛落地开箱，炸 4 碎片扇并立起光树；3 碎片：礼盒抛出的小彩屑）。
    /// 全程重力弧线禁匀速；自绘：暗体饰品+高光+彩晕，绘制抖动 identity 播种
    /// </summary>
    internal class GsChristmasTreeSwordOrnamentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int Mode => (int)Projectile.ai[0];
        private ref float Age => ref Projectile.localAI[0];
        private ref float Bounces => ref Projectile.localAI[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 150;
        }

        /// <summary>确定性伪随机（identity+salt 播种）</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override void AI() {
            Age++;
            if (Age == 1f) {
                //按模式定弹跳数与穿透：金星穿 3 且可弹两次，红球弹一次，礼盒/碎片落地即碎
                Bounces = Mode switch { 0 => 1f, 1 => 2f, _ => 0f };
                if (Mode == 1) {
                    Projectile.penetrate = 3;
                }
                if (Mode == 3) {
                    Projectile.timeLeft = 44;
                }
            }

            //重力弧线：金星平缓、其余饱满
            float gravity = Mode switch { 1 => 0.22f, 3 => 0.30f, _ => 0.34f };
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + gravity, 15f);

            //姿态：球/盒滚转，星快旋，碎片翻飞
            Projectile.rotation += Mode switch {
                1 => 0.30f,
                3 => 0.22f * (SegRand(2) > 0.5f ? 1f : -1f),
                _ => Projectile.velocity.X * 0.045f,
            };

            if (!VaultUtils.isServer) {
                if (Mode != 3 && Main.rand.NextBool(5)) {
                    //航迹彩晕
                    Color c = Mode == 1 ? GsChristmasTreeSword.FestGold : GsChristmasTreeSword.LightCycle[(int)(SegRand(4) * 4) % 4];
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, -Projectile.velocity * 0.05f,
                        c, Main.rand.NextFloat(0.04f, 0.08f))?.Configure(10, 0.6f);
                }
                Lighting.AddLight(Projectile.Center, ModeColor().ToVector3() * 0.35f);
            }
        }

        private Color ModeColor() => Mode switch {
            0 => GsChristmasTreeSword.FestRed,
            1 => GsChristmasTreeSword.FestGold,
            2 => GsChristmasTreeSword.FestRed,
            _ => GsChristmasTreeSword.LightCycle[(int)Projectile.ai[1] % 4],
        };

        /// <summary>落地：还有弹跳数就叮一声弹起，否则碎裂（礼盒即开箱）</summary>
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Bounces > 0f && MathF.Abs(oldVelocity.Y) > 1.4f) {
                Bounces--;
                if (Projectile.velocity.X != oldVelocity.X) {
                    Projectile.velocity.X = -oldVelocity.X * 0.6f;
                }
                Projectile.velocity.Y = -MathF.Abs(oldVelocity.Y) * 0.55f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.22f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Bottom, -Vector2.UnitY * 0.8f,
                        ModeColor(), 0.08f)?.Configure(8, 0.6f);
                }
                return false;
            }
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.2f, Pitch = 0.6f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    GsChristmasTreeSword.LightCycle[i % 4], Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            //礼盒开箱：碎片扇 + 落点立光树（owner 端生成，随包同步）
            if (Mode == 2 && Projectile.owner == Main.myPlayer) {
                int shardDamage = Math.Max(1, (int)(Projectile.damage / 3f));
                for (int i = 0; i < 4; i++) {
                    Vector2 vel = (-MathHelper.PiOver2 + (i - 1.5f) * 0.42f).ToRotationVector2()
                        * Main.rand.NextFloat(4.5f, 6.5f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                        Projectile.type, shardDamage, Projectile.knockBack * 0.4f, Projectile.owner, 3f, i);
                }
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsChristmasTreeSwordTreeProj>(),
                    Math.Max(1, (int)(Projectile.damage * 0.42f)), 2f, Projectile.owner);
            }
            if (VaultUtils.isServer) {
                return;
            }

            //碎裂相：按模式配彩
            if (Mode == 3) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                        ModeColor(), Main.rand.NextFloat(0.25f, 0.4f))?.Configure(true, Main.rand.Next(8, 14));
                }
                return;
            }
            if (Mode == 0) {
                //红球爆裂：玻璃脆响 + 红白屑放射 + 红环
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.38f, Pitch = 0.35f }, Projectile.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                    GsChristmasTreeSword.FestRed, 0.05f)?.Configure(0.06f, 0.4f, 14);
                for (int i = 0; i < 10; i++) {
                    Color c = i % 2 == 0 ? GsChristmasTreeSword.FestRed : GsChristmasTreeSword.FestSnow;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        (MathHelper.TwoPi * i / 10f).ToRotationVector2().RotatedByRandom(0.2) * Main.rand.NextFloat(3f, 7f),
                        c, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(true, Main.rand.Next(14, 24));
                }
            }
            else if (Mode == 1) {
                //金星散芒：星屑四溅
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = 0.7f }, Projectile.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                        GsChristmasTreeSword.FestGold, Main.rand.NextFloat(0.26f, 0.42f))
                        ?.Configure(true, Main.rand.Next(12, 20));
                }
            }
            else {
                //礼盒炸开：金缎带屑 + 低铃
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.35f, Pitch = -0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.25f, Pitch = 0.55f }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    Color c = i % 2 == 0 ? GsChristmasTreeSword.FestGold : GsChristmasTreeSword.FestRed;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                        c, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(12, 22));
                }
            }
            //余痕相：两三粒彩尘缓缓飘落
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.3f, 0.8f)),
                    GsChristmasTreeSword.LightCycle[i % 4], Main.rand.NextFloat(0.05f, 0.09f))?.Configure(16, 0.7f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            Texture2D star4 = CWRAsset.StarTexture_White?.Value;
            if (blot == null || glow == null || star == null || star4 == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float presence = MathHelper.Clamp(Age / 4f, 0f, 1f);

            //航迹拖尾：旧位彩晕
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                Vector2 pos = Projectile.oldPos[i];
                if (pos == Vector2.Zero) {
                    continue;
                }
                pos += Projectile.Size / 2f - Main.screenPosition;
                float k = 1f - i / (float)Projectile.oldPos.Length;
                Color tail = ModeColor() * (0.2f * k * presence);
                tail.A = 0;
                Main.EntitySpriteDraw(glow, pos, null, tail, 0f, glow.Size() * 0.5f,
                    0.3f * k + 0.06f, SpriteEffects.None, 0);
            }

            switch (Mode) {
                case 0: {
                    //红球：暗玻璃球体 + 红晕 + 定向高光点 + 滚动的金挂扣
                    Color body = new Color(112, 22, 30) * (0.9f * presence);
                    Main.EntitySpriteDraw(blot, center, null, body, Projectile.rotation,
                        blot.Size() * 0.5f, new Vector2(0.155f, 0.155f) * presence, SpriteEffects.None, 0);
                    Color rim = GsChristmasTreeSword.FestRed * (0.5f * presence);
                    rim.A = 0;
                    Main.EntitySpriteDraw(glow, center, null, rim, 0f, glow.Size() * 0.5f, 0.32f * presence, SpriteEffects.None, 0);
                    Color spec = GsChristmasTreeSword.FestSnow * (0.75f * presence);
                    spec.A = 0;
                    Main.EntitySpriteDraw(glow, center + new Vector2(-4f, -4f), null, spec, 0f,
                        glow.Size() * 0.5f, 0.07f, SpriteEffects.None, 0);
                    Color cap = GsChristmasTreeSword.FestGold * (0.7f * presence);
                    cap.A = 0;
                    Main.EntitySpriteDraw(glow, center + Projectile.rotation.ToRotationVector2() * 11f, null, cap,
                        0f, glow.Size() * 0.5f, 0.06f, SpriteEffects.None, 0);
                    break;
                }
                case 1: {
                    //金星：四芒星自旋 + 白芯 + 金晕
                    Color halo = GsChristmasTreeSword.FestGold * (0.45f * presence);
                    halo.A = 0;
                    Main.EntitySpriteDraw(glow, center, null, halo, 0f, glow.Size() * 0.5f, 0.4f * presence, SpriteEffects.None, 0);
                    Color gold = GsChristmasTreeSword.FestGold * (0.85f * presence);
                    gold.A = 0;
                    Main.EntitySpriteDraw(star4, center, null, gold, Projectile.rotation,
                        star4.Size() * 0.5f, 0.085f * presence, SpriteEffects.None, 0);
                    Color core = GsChristmasTreeSword.FestSnow * (0.8f * presence);
                    core.A = 0;
                    Main.EntitySpriteDraw(star, center, null, core, -Projectile.rotation * 0.5f,
                        star.Size() * 0.5f, 0.24f * presence, SpriteEffects.None, 0);
                    break;
                }
                case 2: {
                    //礼盒：暗红盒体 + 金缎带十字 + 顶结 + 红晕
                    Color body = new Color(96, 26, 30) * (0.92f * presence);
                    Main.EntitySpriteDraw(blot, center, null, body, Projectile.rotation,
                        blot.Size() * 0.5f, new Vector2(0.165f, 0.135f) * presence, SpriteEffects.None, 0);
                    Color ribbon = GsChristmasTreeSword.FestGold * (0.65f * presence);
                    ribbon.A = 0;
                    Main.EntitySpriteDraw(glow, center, null, ribbon, Projectile.rotation,
                        glow.Size() * 0.5f, new Vector2(0.34f, 0.05f), SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(glow, center, null, ribbon, Projectile.rotation + MathHelper.PiOver2,
                        glow.Size() * 0.5f, new Vector2(0.30f, 0.05f), SpriteEffects.None, 0);
                    Vector2 bow = center + (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * 9f;
                    Main.EntitySpriteDraw(glow, bow, null, ribbon, 0f, glow.Size() * 0.5f, 0.09f, SpriteEffects.None, 0);
                    Color rim = GsChristmasTreeSword.FestRed * (0.3f * presence);
                    rim.A = 0;
                    Main.EntitySpriteDraw(glow, center, null, rim, 0f, glow.Size() * 0.5f, 0.3f * presence, SpriteEffects.None, 0);
                    break;
                }
                default: {
                    //碎片：小彩芒翻飞，尾段渐隐
                    float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
                    Color c = ModeColor() * (0.8f * presence * fade);
                    c.A = 0;
                    Main.EntitySpriteDraw(star, center, null, c, Projectile.rotation,
                        star.Size() * 0.5f, 0.16f * presence, SpriteEffects.None, 0);
                    break;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 小圣诞光树：礼盒落点竖起的驻场灯树。坠地扎根 → 12 帧逐层长成（灯串随生长逐颗点亮）
    /// → 驻场明灭、每 22 帧洒一轮星屑并对树冠半径结算一跳 → 末 12 帧折叠散灯。
    /// 暗松针体用真 alpha 压底，灯串/顶星全加色；绘制抖动 identity 播种
    /// </summary>
    internal class GsChristmasTreeSwordTreeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int RootLife = 100;   //扎根后的总寿命
        private const int GrowFrames = 12;
        private const int CollapseFrames = 12;
        private const int TickInterval = 22;
        private const int FirstTick = 14;
        private const float CanopyRadius = 104f;

        private ref float Age => ref Projectile.localAI[0];
        private ref float RootMark => ref Projectile.localAI[1];
        private bool Rooted => RootMark > 0f;
        private float RootAge => Rooted ? Age - (RootMark - 1f) : 0f;
        private Vector2 CanopyCenter => Projectile.Center + new Vector2(0f, -30f);

        /// <summary>灯串挂点（树体局部坐标，自下而上之字排布）</summary>
        private static readonly Vector2[] BulbOffsets = [
            new(-21f, -4f), new(15f, -8f), new(-16f, -22f), new(17f, -26f),
            new(-12f, -38f), new(12f, -42f), new(-7f, -52f), new(8f, -55f), new(-2f, -63f),
        ];

        //三层松针的锚点与横纵缩放
        private static readonly Vector2[] TierOffsets = [new(0f, -12f), new(0f, -32f), new(0f, -50f)];
        private static readonly Vector2[] TierScales = [new(0.36f, 0.24f), new(0.29f, 0.20f), new(0.22f, 0.17f)];

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.timeLeft = 150;
        }

        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override void AI() {
            Age++;
            if (!Rooted) {
                //落苗：慢旋下坠，最多 24 帧后原地扎根
                Projectile.velocity.X *= 0.9f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.42f, 12f);
                if (Age >= 24f) {
                    Root();
                }
                return;
            }

            float ra = RootAge;
            //洒屑节拍：进入新一轮 tick 时鸣铃、冒星屑、放脉冲环
            if (ra >= FirstTick && ra <= RootLife - CollapseFrames && (ra - FirstTick) % TickInterval == 0
                && !VaultUtils.isServer) {
                int tickIdx = (int)((ra - FirstTick) / TickInterval);
                SoundEngine.PlaySound(SoundID.Item35 with {
                    Volume = 0.3f,
                    Pitch = 0.2f + 0.15f * (tickIdx % 3),
                    MaxInstances = 3
                }, Projectile.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(CanopyCenter, Vector2.Zero,
                    GsChristmasTreeSword.FestGold, 0.05f)?.Configure(0.07f, CanopyRadius / 380f, 16);
                for (int i = 0; i < 6; i++) {
                    //树冠洒下金星屑
                    PRTLoader.NewParticle<PRT_Light>(
                        CanopyCenter + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(-30f, 0f)),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.5f, 1.1f)),
                        Main.rand.NextBool() ? GsChristmasTreeSword.FestGold : GsChristmasTreeSword.FestSnow,
                        Main.rand.NextFloat(0.05f, 0.09f))?.Configure(14, 0.65f);
                }
            }
            if (!VaultUtils.isServer) {
                Lighting.AddLight(CanopyCenter, GsChristmasTreeSword.FestGold.ToVector3() * 0.4f);
                Lighting.AddLight(Projectile.Center, GsChristmasTreeSword.FestPine.ToVector3() * 0.25f);
            }
        }

        private void Root() {
            RootMark = Age;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.timeLeft = RootLife;
            if (VaultUtils.isServer) {
                return;
            }
            //落成和弦：双铃 + 立树光尘
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.4f, Pitch = 0.1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = 0.55f }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Bottom + Main.rand.NextVector2Circular(14f, 4f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.6f),
                    GsChristmasTreeSword.FestSnow, Main.rand.NextFloat(0.06f, 0.1f))?.Configure(12, 0.7f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!Rooted) {
                Root();
            }
            return false;
        }

        /// <summary>只在洒屑节拍的头 3 帧结算，冠内一跳</summary>
        public override bool? CanDamage() {
            if (!Rooted) {
                return false;
            }
            float ra = RootAge;
            return ra >= FirstTick && ra <= RootLife - CollapseFrames
                && (ra - FirstTick) % TickInterval < 3 ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(CanopyCenter) <= CanopyRadius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //收树：灯串散作彩屑升腾
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = -0.15f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(CanopyCenter + Main.rand.NextVector2Circular(18f, 26f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.8f, 2.2f),
                    GsChristmasTreeSword.LightCycle[i % 4], Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(true, Main.rand.Next(14, 24));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            Texture2D star4 = CWRAsset.StarTexture_White?.Value;
            if (blot == null || glow == null || star == null || star4 == null) {
                return false;
            }
            Vector2 basePos = Projectile.Center + new Vector2(0f, 14f) - Main.screenPosition;

            //生长/折叠包络：长成带 8% 过冲，收场纵向折叠
            float grow;
            float alpha = 1f;
            if (!Rooted) {
                grow = 0.4f; //落苗蜷缩
            }
            else {
                float ra = RootAge;
                float p = MathHelper.Clamp(ra / GrowFrames, 0f, 1f);
                grow = p < 0.7f ? 1.08f * (p / 0.7f) : MathHelper.Lerp(1.08f, 1f, (p - 0.7f) / 0.3f);
                float left = Projectile.timeLeft;
                if (left < CollapseFrames) {
                    alpha = left / CollapseFrames;
                    grow *= 0.5f + 0.5f * alpha;
                }
            }
            float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.3f + SegRand(1) * 6.28f) * 0.03f;

            //树干 + 三层松针暗体（真 alpha 压底）
            Color trunk = new Color(44, 28, 16) * (0.85f * alpha);
            Main.EntitySpriteDraw(blot, basePos + new Vector2(0f, -4f) * grow, null, trunk, 0f,
                blot.Size() * 0.5f, new Vector2(0.07f, 0.11f) * grow, SpriteEffects.None, 0);
            Color pine = new Color(20, 44, 30) * (0.92f * alpha);
            for (int t = 0; t < 3; t++) {
                //逐层长出：下层先立
                float tp = MathHelper.Clamp(grow * 3f - t, 0f, 1f);
                if (tp <= 0.01f) {
                    continue;
                }
                Main.EntitySpriteDraw(blot, basePos + TierOffsets[t] * grow, null, pine * tp, sway * (t + 1),
                    blot.Size() * 0.5f, TierScales[t] * grow * (0.6f + 0.4f * tp), SpriteEffects.None, 0);
            }

            //灯串：随生长逐颗点亮，四色追逐明灭
            for (int i = 0; i < BulbOffsets.Length; i++) {
                float reveal = MathHelper.Clamp(grow * BulbOffsets.Length - i, 0f, 1f);
                if (reveal <= 0.01f) {
                    continue;
                }
                float blink = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(
                    Main.GlobalTimeWrappedHourly * 5f + i * 2.1f + SegRand(i + 10) * 6.28f));
                Color bulb = GsChristmasTreeSword.LightCycle[i % 4] * (0.6f * blink * reveal * alpha);
                bulb.A = 0;
                Main.EntitySpriteDraw(glow, basePos + BulbOffsets[i] * grow, null, bulb, 0f,
                    glow.Size() * 0.5f, 0.10f, SpriteEffects.None, 0);
            }

            //顶星：长成末尾亮起，金星缓旋 + 白芯 + 光晕
            float topReveal = MathHelper.Clamp((grow - 0.85f) / 0.15f, 0f, 1f);
            if (topReveal > 0.01f) {
                Vector2 top = basePos + new Vector2(0f, -64f) * grow;
                float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + SegRand(20) * 6.28f);
                Color haloC = GsChristmasTreeSword.FestGold * (0.35f * topReveal * alpha);
                haloC.A = 0;
                Main.EntitySpriteDraw(glow, top, null, haloC, 0f, glow.Size() * 0.5f, 0.5f * pulse, SpriteEffects.None, 0);
                Color topGold = GsChristmasTreeSword.FestGold * (0.85f * topReveal * alpha * pulse);
                topGold.A = 0;
                Main.EntitySpriteDraw(star4, top, null, topGold, Main.GlobalTimeWrappedHourly * 0.8f,
                    star4.Size() * 0.5f, 0.075f, SpriteEffects.None, 0);
                Color topCore = GsChristmasTreeSword.FestSnow * (0.8f * topReveal * alpha);
                topCore.A = 0;
                Main.EntitySpriteDraw(star, top, null, topCore, 0f, star.Size() * 0.5f, 0.2f * pulse, SpriteEffects.None, 0);
            }

            //根雪：一小抔暖白垫底
            Color snowC = GsChristmasTreeSword.FestSnow * (0.2f * alpha);
            snowC.A = 0;
            Main.EntitySpriteDraw(glow, basePos, null, snowC, 0f, glow.Size() * 0.5f,
                new Vector2(0.36f, 0.1f) * grow, SpriteEffects.None, 0);
            return false;
        }
    }
}
