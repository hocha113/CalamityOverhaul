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
    /// 【白昼裁决·真圣剑】材质：白昼熔金淬成的真圣剑，掷出的刃波就是会飞的日光。
    /// 签名：①每一斩掷出贯穿的白昼刃波（炽白核线+鎏金罩+虹彩微光边），三拍逐拍升阶
    /// ②第三拍裁决刃波命中处炸出十字圣光 ③裁决拍蓄力身后升起白昼光柱、金尘上涌、刃脊日辉游走
    /// </summary>
    internal class GsTrueExcalibur : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.TrueExcalibur;

        protected override int HeldProjID => ModContent.ProjectileType<GsTrueExcaliburHeld>();

        //白昼三拍的续段窗口
        protected override int ComboResetFrames => 60;

        protected override string GsDescFallback =>
            "Reforged: a three-beat rite of daybreak; every slash hurls a piercing wave of daylight " +
            "that grows mightier with each beat, and the third wave detonates a cross of holy light where it strikes";

        //白昼色板
        internal static readonly Color DayBright = new(255, 252, 238); //炽白核线
        internal static readonly Color DayGold = new(255, 214, 118);   //鎏金罩
        internal static readonly Color DayHot = new(255, 176, 64);     //裁决灼金
        internal static readonly Color DayDeep = new(58, 42, 20);      //暖调垫影

        //底伤不加成（原版 72/18f 整个攻击就是远程光弧）：刀身拍均 1.13x 管刃程内，
        //刃波 0.75/0.75/0.9x 自刃尖外放出只管刃外（两域互斥不叠加），裁决十字 0.3x 是命中处 AoE 收益，
        //按三拍循环约 61 帧摊算，刃内综合约原版 100%~112%、刃外约 80% 由贯穿与十字补偿
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 白昼裁决手持：三拍连段。0 升斩 / 1 返斩（音调上扬）/ 2 裁决重劈
    /// （长举聚光+前压+刃波升阶带十字圣光）。每拍斩切爆发自刃尖掷出白昼刃波。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsTrueExcaliburHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.TrueExcalibur;
        protected override Color EdgeBright => GsTrueExcalibur.DayBright;
        protected override Color BodyMain => GsTrueExcalibur.DayGold;
        protected override Color HotAccent => GsTrueExcalibur.DayHot;
        protected override Color DeepShadow => GsTrueExcalibur.DayDeep;

        //真圣剑常亮昼辉
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsTrueExcalibur.DayHot : GsTrueExcalibur.DayBright;

        private bool waveFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 升斩
            0 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.8f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1.05f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
            },
            //拍1 返斩：略快，音调上扬
            1 => new GsBroadBeat {
                Raise = 4, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.85f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 1.05f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.08f,
            },
            //拍2 裁决：长举聚光、前压重劈
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 10,
                RaiseBack = 2.25f, Follow = 1.3f, ReachScale = 1.15f, LeanAmp = 0.09f,
                DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.25f,
            },
        };

        //==================== 白昼演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //裁决爆发：圣光钟鸣 + 厚重风声
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.1f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = -0.35f }, Owner.Center);
            }
        }

        /// <summary>每拍斩切爆发：自刃尖掷出白昼刃波（只管刃外），拍号即刃波阶级</summary>
        protected override void OnSlashBegin() {
            if (waveFired) {
                return;
            }
            waveFired = true;
            if (IsFinisher) {
                SetFlash(7);
            }
            Vector2 dir = baseAngle.ToRotationVector2();
            float mult = IsFinisher ? 0.9f : 0.75f;
            int dmg = Math.Max(1, (int)(Projectile.damage * mult));
            SpawnOwnedProj(ModContent.ProjectileType<GsTrueExcaliburWaveProj>(),
                Hand + dir * (FullReach * 0.95f), dir * (13f + ComboStage), dmg,
                Projectile.knockBack * 0.5f, swingDir, ComboStage);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.32f, Pitch = 0.3f + 0.12f * ComboStage }, Owner.Center);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (!IsFinisher || phase > PhaseHold) {
                return;
            }
            //白昼上涌：金尘自脚边升入刀身
            Vector2 at = Owner.Center + new Vector2(Main.rand.NextFloat(-46f, 46f), Main.rand.NextFloat(10f, 34f));
            Vector2 vel = ((Vector2.Lerp(Hand, mainTip, 0.6f) - at) * 0.12f) - (Vector2.UnitY * 1.1f);
            PRTLoader.NewParticle<PRT_Light>(at, vel, GsTrueExcalibur.DayGold,
                Main.rand.NextFloat(0.06f, 0.11f))?.Configure(10, 0.6f);
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //白昼碎光：金白光屑外加一撮虹彩微光
            int motes = IsFinisher ? 5 : 2;
            for (int i = 0; i < motes; i++) {
                Color c = Main.rand.NextBool(3)
                    ? Main.hslToRgb(Main.rand.NextFloat(), 0.65f, 0.72f)
                    : GsTrueExcalibur.DayBright;
                PRTLoader.NewParticle<PRT_Light>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.4f), c,
                    Main.rand.NextFloat(0.07f, 0.12f))?.Configure(11, 0.65f);
            }
        }

        /// <summary>裁决蓄力白昼光柱 + 举拍刃脊日辉游走（纯演出，全端可见）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (glow == null || star == null || flare == null) {
                return;
            }

            //举拍：一点日辉沿刀脊自柄滑向尖
            if (CurrentPhase == PhaseRaise) {
                float t = MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
                Vector2 at = Hand + (mainAngle.ToRotationVector2() * (mainReach * (0.2f + 0.75f * t))) - Main.screenPosition;
                Color g = GsTrueExcalibur.DayBright * (0.5f * (1f - t * 0.35f));
                g.A = 0;
                sb.Draw(star, at, null, g, 0f, star.Size() * 0.5f, 0.15f + 0.07f * t, SpriteEffects.None, 0f);
            }

            //裁决拍蓄力：身后升起白昼光柱，顶端星芒缓旋
            if (IsFinisher && CurrentPhase <= PhaseHold) {
                float p = CurrentPhase == PhaseHold ? 1f : MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
                Vector2 anchor = Owner.Center - Main.screenPosition;
                Color shaft = GsTrueExcalibur.DayGold * (0.14f + 0.2f * p);
                shaft.A = 0;
                sb.Draw(glow, anchor - new Vector2(0f, 42f * p), null, shaft, 0f, glow.Size() * 0.5f,
                    new Vector2(0.9f, 0.5f + 2.4f * p), SpriteEffects.None, 0f);
                Color core = GsTrueExcalibur.DayBright * (0.28f * p);
                core.A = 0;
                sb.Draw(glow, anchor - new Vector2(0f, 48f * p), null, core, 0f, glow.Size() * 0.5f,
                    new Vector2(0.32f, 0.4f + 2.7f * p), SpriteEffects.None, 0f);
                Color crown = GsTrueExcalibur.DayGold * (0.3f * p);
                crown.A = 0;
                sb.Draw(flare, anchor - new Vector2(0f, 92f * p), null, crown,
                    Main.GlobalTimeWrappedHourly * 0.9f + DrawRand01(3) * 6.28f,
                    flare.Size() * 0.5f, 0.3f + 0.12f * p, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 白昼刃波：每一斩掷出的贯穿日光弧。出膛 3 帧撑满带 15% 过冲，前 20 帧减速回稳后滑行，
    /// 行进中渐薄；炽白核线+鎏金罩+虹彩微光边，拖尾旧位残弧，消亡金尘上浮。
    /// ai[0]=挥动符号（月牙弯向）ai[1]=拍号阶级（0/1/2，越高越大越能穿；2=裁决，命中炸十字圣光）
    /// </summary>
    internal class GsTrueExcaliburWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float SwingSign => Projectile.ai[0] >= 0f ? 1f : -1f;
        private int Tier => Math.Clamp((int)Projectile.ai[1], 0, 2);
        private ref float Life => ref Projectile.localAI[0];
        private ref float CrossFired => ref Projectile.localAI[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 52;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 4;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = 42;
        }

        public override void AI() {
            Life++;
            if (Life == 1f) {
                //升阶：更能穿，裁决波体更大
                if (Tier >= 2) {
                    Projectile.penetrate = 7;
                    Projectile.Resize(64, 64);
                }
                else if (Tier == 1) {
                    Projectile.penetrate = 5;
                }
            }

            //出膛减速回稳：前 20 帧 13~15 → 约 9，之后滑行，尾段收速
            if (Life <= 20f) {
                Projectile.velocity *= 0.972f;
            }
            else if (Projectile.timeLeft < 12) {
                Projectile.velocity *= 0.95f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsTrueExcalibur.DayGold.ToVector3() * (0.35f + 0.12f * Tier));

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                //航迹余痕：金尘自波身上飘
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                    (-Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f)) - (Projectile.velocity * 0.05f),
                    GsTrueExcalibur.DayGold, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(10, 0.6f);
            }
        }

        public override bool? CanDamage() => Life >= 1f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //裁决波：首个命中处炸出十字圣光（owner 端生成，随包过线）
            if (Tier >= 2 && CrossFired == 0f) {
                CrossFired = 1f;
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                        ModContent.ProjectileType<GsTrueExcaliburCrossProj>(),
                        Math.Max(1, (int)(Projectile.damage * 0.3f)), Projectile.knockBack * 0.6f, Projectile.owner);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? GsTrueExcalibur.DayBright : GsTrueExcalibur.DayHot,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //消散：金尘缓缓上浮，留住日光的余温
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f),
                    Main.rand.NextBool() ? GsTrueExcalibur.DayBright : GsTrueExcalibur.DayGold,
                    Main.rand.NextFloat(0.06f, 0.1f))?.Configure(12, 0.65f);
            }
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        private static float Hue01(float v) => v - MathF.Floor(v);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (smear == null || glow == null || star == null) {
                return false;
            }
            Vector2 screen = Main.screenPosition;
            float rot = Projectile.rotation;
            Vector2 fwd = rot.ToRotationVector2();
            Vector2 side = (rot + MathHelper.PiOver2).ToRotationVector2() * SwingSign;
            //出生暴烈：3 帧撑满带 15% 过冲再回坐；消亡温和渐隐
            float grow = Life <= 3f
                ? 1.15f * (Life / 3f)
                : MathHelper.Lerp(1.15f, 1f, MathHelper.Clamp((Life - 3f) / 5f, 0f, 1f));
            float fade = MathHelper.Clamp(Projectile.timeLeft / 11f, 0f, 1f);
            //行进中渐薄：越飞越锋利
            float thin = MathHelper.Lerp(1f, 0.6f, MathHelper.Clamp(Life / 42f, 0f, 1f));
            float sizeMul = (1f + 0.16f * Tier) * grow;

            //拖尾：旧位置残弧渐隐
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + (Projectile.Size * 0.5f) - screen;
                float t = 1f - (i / (float)Projectile.oldPos.Length);
                Color trail = GsTrueExcalibur.DayGold * (0.13f * t * fade);
                trail.A = 0;
                Main.EntitySpriteDraw(smear, at, null, trail, rot + (SwingSign * 0.3f),
                    smear.Size() * 0.5f, new Vector2(0.32f, 0.14f * thin) * sizeMul * t, SpriteEffects.None, 0);
            }

            Vector2 center = Projectile.Center - screen;

            //鎏金罩：柔光垫底
            Color halo = GsTrueExcalibur.DayGold * (0.38f * fade);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, center, null, halo, 0f, glow.Size() * 0.5f, 0.6f * sizeMul, SpriteEffects.None, 0);

            //金体月牙
            Color body = GsTrueExcalibur.DayGold * (0.6f * fade);
            body.A = 0;
            Main.EntitySpriteDraw(smear, center, null, body, rot + (SwingSign * 0.28f),
                smear.Size() * 0.5f, new Vector2(0.4f, 0.19f * thin) * sizeMul, SpriteEffects.None, 0);

            //炽白核线：星贴图沿飞行向拉长的细亮线
            Color coreC = GsTrueExcalibur.DayBright * (0.85f * fade);
            coreC.A = 0;
            Main.EntitySpriteDraw(star, center + (fwd * 6f), null, coreC, rot,
                star.Size() * 0.5f, new Vector2(0.3f, 0.05f * thin) * sizeMul, SpriteEffects.None, 0);

            //虹彩微光边：前缘细弧逐帧转色
            Color irid = Main.hslToRgb(Hue01(Main.GlobalTimeWrappedHourly * 0.32f + SegRand(7)), 0.68f, 0.72f) * (0.42f * fade);
            irid.A = 0;
            Main.EntitySpriteDraw(smear, center + (fwd * 10f), null, irid, rot + (SwingSign * 0.22f),
                smear.Size() * 0.5f, new Vector2(0.34f, 0.07f * thin) * sizeMul, SpriteEffects.None, 0);

            //月牙双角虹彩亮点
            for (int i = -1; i <= 1; i += 2) {
                Color horn = Main.hslToRgb(Hue01(Main.GlobalTimeWrappedHourly * 0.32f + 0.33f * i + SegRand(11)), 0.6f, 0.75f) * (0.38f * fade);
                horn.A = 0;
                Main.EntitySpriteDraw(glow, center + (side * (i * 19f * sizeMul)) - (fwd * 4f), null, horn, 0f,
                    glow.Size() * 0.5f, 0.2f * sizeMul, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 十字圣光：裁决刃波命中处炸出的十字光。臂长 8 帧过冲撑满后回坐，
    /// 伤害只在扩张期结算一次；横竖双臂线判定。绘制全走确定性相位，禁 Main.rand
    /// </summary>
    internal class GsTrueExcaliburCrossProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 22;
        private const float MaxArm = 150f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前臂长：8 帧过冲 8% 再回坐</summary>
        private float ArmLength {
            get {
                float p = MathHelper.Clamp(Life / 8f, 0f, 1f);
                float burst = p < 0.7f ? 1.08f * (p / 0.7f) : MathHelper.Lerp(1.08f, 1f, (p - 0.7f) / 0.3f);
                return MaxArm * burst;
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
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);
                //四臂圣火：沿十字方向迸溅
                for (int arm = 0; arm < 4; arm++) {
                    Vector2 dir = (MathHelper.PiOver2 * arm).ToRotationVector2();
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                            dir.RotatedByRandom(0.16) * Main.rand.NextFloat(4f, 9f),
                            Main.rand.NextBool() ? GsTrueExcalibur.DayBright : GsTrueExcalibur.DayHot,
                            Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(12, 22));
                    }
                }
            }
            Lighting.AddLight(Projectile.Center, GsTrueExcalibur.DayGold.ToVector3() * (0.9f * (1f - Life01)));
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 9f ? null : false;

        /// <summary>十字判定：横竖两条臂线各查一次</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float arm = ArmLength;
            Vector2 c = Projectile.Center;
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    c - (Vector2.UnitX * arm), c + (Vector2.UnitX * arm), 30f, ref point)
                || Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    c - (Vector2.UnitY * arm), c + (Vector2.UnitY * arm), 30f, ref point);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//击退向外

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            if (star == null || glow == null || flare == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            float armScale = ArmLength / (star.Height * 0.5f);

            //横竖双臂：金外鞘 + 白核线
            for (int arm = 0; arm < 2; arm++) {
                float rot = MathHelper.PiOver2 * arm;
                Color sheath = GsTrueExcalibur.DayGold * (0.5f * fade);
                sheath.A = 0;
                Main.EntitySpriteDraw(star, center, null, sheath, rot,
                    star.Size() * 0.5f, new Vector2(0.16f, armScale), SpriteEffects.None, 0);
                Color coreC = GsTrueExcalibur.DayBright * (0.8f * fade);
                coreC.A = 0;
                Main.EntitySpriteDraw(star, center, null, coreC, rot,
                    star.Size() * 0.5f, new Vector2(0.07f, armScale * 0.96f), SpriteEffects.None, 0);
            }

            //臂端亮点
            for (int arm = 0; arm < 4; arm++) {
                Vector2 tip = center + ((MathHelper.PiOver2 * arm).ToRotationVector2() * ArmLength * 0.96f);
                Color tipC = GsTrueExcalibur.DayBright * (0.5f * fade);
                tipC.A = 0;
                Main.EntitySpriteDraw(glow, tip, null, tipC, 0f, glow.Size() * 0.5f,
                    0.18f + 0.05f * SegRand(arm), SpriteEffects.None, 0);
            }

            //爆心：软光 + 缓旋光斑
            Color heart = GsTrueExcalibur.DayGold * (0.55f * fade);
            heart.A = 0;
            Main.EntitySpriteDraw(glow, center, null, heart, 0f, glow.Size() * 0.5f, 0.5f, SpriteEffects.None, 0);
            Color flareC = GsTrueExcalibur.DayBright * (0.55f * fade * fade);
            flareC.A = 0;
            Main.EntitySpriteDraw(flare, center, null, flareC, Life * 0.06f + SegRand(9) * 6.28f,
                flare.Size() * 0.5f, 0.42f, SpriteEffects.None, 0);
            return false;
        }
    }
}
