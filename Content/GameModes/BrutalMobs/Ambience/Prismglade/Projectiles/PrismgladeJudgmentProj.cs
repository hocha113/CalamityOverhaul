using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Prismglade.Projectiles
{
    /// <summary>
    /// 「棱光审判」棱晶折射光柱。ai[0]=变体（0 地表悬晶折射 / 1 地下晶簇共鸣），ai[2]=判定柱高（px，权威端定标）。
    /// 世界解释链：地表变体在圈心上空凝聚一颗可见悬晶（真色体，旋转+汇光），光柱自悬晶折射而下；
    /// 地下变体由地面长出的晶簇共鸣喷发——审判有来处，不再凭空。
    /// 生成位置即锁定圈心（预告即承诺，光圈明确可走出）：
    /// 地面警示圈+晶尘向心汇聚+和声渐强 52 帧 → 白金光柱命中 16 帧（仅此窗口有判定）→ 碎晶渐融余韵 26 帧。
    /// 预告期出现 Boss 则整发熄灭（伤害机制随 Boss 在场暂停）；
    /// 各端从同步的 timeLeft 推演同一时间轴，音画在每个客户端本地自播（位置衰减免费拿到）
    /// </summary>
    internal class PrismgladeJudgmentProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalShard;

        /// <summary>预告帧数（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 52;
        /// <summary>命中帧数（判定窗=可见光涌窗）</summary>
        private const int StrikeFrames = 16;
        /// <summary>余韵帧数</summary>
        private const int AfterglowFrames = 26;
        /// <summary>审判圈半径（判定柱半宽与它同源，圈内即判定）</summary>
        internal const float CircleRadius = 84f;
        /// <summary>光柱展开用时（帧）</summary>
        private const int BeamOpenFrames = 5;
        /// <summary>悬晶中心悬于柱顶之上的距离（px，地表变体的折射源）</summary>
        private const float PrismHover = 26f;

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> DiffusionCircle4 = null;

        /// <summary>0 地表悬晶折射 / 1 地下晶簇共鸣</summary>
        private int Variant => (int)Projectile.ai[0];
        /// <summary>判定柱高（px），权威端按头顶净空定标后随生成包同步</summary>
        private float HitHeight => Projectile.ai[2];
        private int TotalLife => TelegraphFrames + StrikeFrames + AfterglowFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>色相基准：identity 同步一致 + 缓慢流转</summary>
        private float BaseHue => (Projectile.identity * 0.1372f + Main.GlobalTimeWrappedHourly * 0.045f) % 1f;

        private bool Cancelled {
            get => Projectile.localAI[1] == 1f;
            set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        /// <summary>光柱展开程度 0~1（命中窗前 5 帧快速张开）</summary>
        private float StrikeProgress {
            get {
                int t = Elapsed - TelegraphFrames;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= BeamOpenFrames) {
                    return 1f;
                }
                float x = t / (float)BeamOpenFrames;
                return 1f - (1f - x) * (1f - x) * (1f - x);
            }
        }

        /// <summary>余韵收束 1→0（宽度与亮度一起收细熄灭）</summary>
        private float CollapseFactor {
            get {
                int t = Elapsed - TelegraphFrames - StrikeFrames;
                if (t <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - t / (float)AfterglowFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;//命中窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TelegraphFrames + StrikeFrames + AfterglowFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;

            //Boss 在场：预告期直接熄灭（伤害机制暂停）。HasBoss 各端逐帧自算，
            //快照同步的极端时差最多让受害端少判一发，方向是安全侧
            if (!Cancelled && elapsed < TelegraphFrames && CWRWorld.HasBoss) {
                Cancelled = true;
            }
            if (Cancelled && elapsed >= TelegraphFrames) {
                Projectile.Kill();
                return;
            }

            //判定窗=可见光涌窗；hostile 由同步的 timeLeft 逐帧推演，无临时清零的同步字段
            Projectile.hostile = !Cancelled
                && elapsed >= TelegraphFrames && elapsed < TelegraphFrames + StrikeFrames;

            if (Main.dedServ) {
                return;
            }

            bool underground = Variant == 1;
            float pitchShift = underground ? -0.22f : 0f;

            if (elapsed == 0) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.32f, Pitch = 0.5f + pitchShift, MaxInstances = 5 }, Projectile.Center);
            }

            if (Cancelled) {
                return;
            }

            if (elapsed < TelegraphFrames) {
                //和声渐强：三记泛音逐级抬升（听觉通道预告）
                if (elapsed == 8) {
                    SoundEngine.PlaySound(SoundID.Item26 with { Volume = 0.3f, Pitch = -0.05f + pitchShift, MaxInstances = 5 }, Projectile.Center);
                }
                else if (elapsed == 24) {
                    SoundEngine.PlaySound(SoundID.Item26 with { Volume = 0.42f, Pitch = 0.28f + pitchShift, MaxInstances = 5 }, Projectile.Center);
                }
                else if (elapsed == 40) {
                    SoundEngine.PlaySound(SoundID.Item26 with { Volume = 0.55f, Pitch = 0.6f + pitchShift, MaxInstances = 5 }, Projectile.Center);
                }
                //地面晶尘汇聚：星尘贴地向圈心收拢（警示环之外的物质预告通道）
                if (elapsed >= 8 && elapsed % 2 == 0) {
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 spawn = Projectile.Center + new Vector2(
                        side * Main.rand.NextFloat(0.55f, 1.05f) * CircleRadius, Main.rand.NextFloat(-8f, 2f));
                    Dust star = Dust.NewDustPerfect(spawn,
                        Main.rand.NextBool(4) ? DustID.Pixie : DustID.YellowStarDust,
                        new Vector2(-side * Main.rand.NextFloat(1.3f, 2.4f), -Main.rand.NextFloat(0f, 0.3f)),
                        120, default, Main.rand.NextFloat(0.7f, 1.05f));
                    star.noGravity = true;
                }
                //汇光：光尘被吸入圈心上空的凝晶（地表变体的蓄能可见通道）
                if (Variant == 0 && elapsed >= 12 && elapsed % 3 == 0) {
                    Vector2 prismPos = Projectile.Center - new Vector2(0f, Math.Max(HitHeight, 64f) + PrismHover);
                    Vector2 offset = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(46f, 88f);
                    var inflow = PRTLoader.NewParticle<PRT_PrismgladeMote>(prismPos + offset,
                        -offset.SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(2.4f, 3.4f),
                        default, Main.rand.NextFloat(0.14f, 0.22f));
                    if (inflow != null) {
                        inflow.hue = BaseHue + Main.rand.NextFloat(0.25f);
                        inflow.Lifetime = Main.rand.Next(18, 28);
                    }
                }
                return;
            }

            if (elapsed == TelegraphFrames) {
                //落柱拍：晶鸣+钟声，圈心炸开彩虹光尘
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.9f, Pitch = 0.12f + pitchShift, MaxInstances = 5 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.42f, Pitch = 0.55f + pitchShift, MaxInstances = 5 }, Projectile.Center);

                float dist = Main.LocalPlayer.Distance(Projectile.Center);
                if (dist < 800f) {
                    Main.LocalPlayer.CWR().GetScreenShake(2.8f * (1f - dist / 800f));
                }
                for (int i = 0; i < 10; i++) {
                    var mote = PRTLoader.NewParticle<PRT_PrismgladeMote>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-CircleRadius, CircleRadius) * 0.7f, -4f),
                        new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(2.5f, 6.5f)),
                        default, Main.rand.NextFloat(0.24f, 0.38f));
                    if (mote != null) {
                        mote.hue = BaseHue + i / 10f;
                        mote.Lifetime = Main.rand.Next(34, 60);
                    }
                }
                //落点晶屑迸溅：原版水晶碎尘带重力抛洒（柱体接地的物质反馈）
                for (int i = 0; i < 16; i++) {
                    int chipType = Main.rand.Next(3) switch {
                        0 => DustID.BlueCrystalShard,
                        1 => DustID.PinkCrystalShard,
                        _ => DustID.PurpleCrystalShard,
                    };
                    Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * CircleRadius, -2f),
                        chipType,
                        new Vector2(Main.rand.NextFloat(-3.2f, 3.2f), -Main.rand.NextFloat(1.5f, 5.5f)),
                        60, default, Main.rand.NextFloat(0.9f, 1.4f));
                }
            }
            else if (elapsed < TelegraphFrames + StrikeFrames) {
                //命中窗：柱内微尘被照亮上浮（棱镜光的显影介质）
                var riser = PRTLoader.NewParticle<PRT_PrismgladeMote>(
                    Projectile.Center - new Vector2(Main.rand.NextFloat(-CircleRadius, CircleRadius) * 0.4f,
                        Main.rand.NextFloat(0f, HitHeight * 0.8f)),
                    new Vector2(0f, -Main.rand.NextFloat(1.2f, 2.6f)),
                    default, Main.rand.NextFloat(0.16f, 0.26f));
                if (riser != null) {
                    riser.hue = BaseHue + Main.rand.NextFloat(0.3f);
                    riser.Lifetime = Main.rand.Next(26, 44);
                }
                //柱脚持续崩晶：命中窗内接触点小股晶屑
                if (elapsed % 2 == 0) {
                    Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * CircleRadius, -2f),
                        DustID.PinkCrystalShard,
                        new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), -Main.rand.NextFloat(1f, 3f)),
                        90, default, Main.rand.NextFloat(0.7f, 1f));
                }
            }
            else if (elapsed == TelegraphFrames + StrikeFrames + 5) {
                //余韵轻音
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.22f, Pitch = 0.75f + pitchShift, MaxInstances = 5 }, Projectile.Center);
            }

            //余韵：碎晶残留渐融——真 alpha 微晶漂浮收缩，活过弹幕死亡（消散而非删除）
            if (elapsed >= TelegraphFrames + StrikeFrames && elapsed % 3 == 0) {
                bool fromPrism = Variant == 0 && Main.rand.NextBool(3);
                Vector2 meltPos = fromPrism
                    ? Projectile.Center - new Vector2(Main.rand.NextFloat(-14f, 14f), Math.Max(HitHeight, 64f) + PrismHover)
                    : Projectile.Center + new Vector2(Main.rand.NextFloat(-0.6f, 0.6f) * CircleRadius, -Main.rand.NextFloat(2f, 26f));
                var melt = PRTLoader.NewParticle<PRT_PrismgladeChip>(meltPos,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f),
                        fromPrism ? Main.rand.NextFloat(0.3f, 0.8f) : -Main.rand.NextFloat(0.1f, 0.5f)),
                    default, Main.rand.NextFloat(0.2f, 0.32f));
                if (melt != null) {
                    melt.hue = BaseHue + Main.rand.NextFloat();
                    melt.Lifetime = Main.rand.Next(50, 90);
                }
            }

            float glow = StrikeProgress * CollapseFactor;
            if (glow > 0.05f) {
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * 20f, new Vector3(1.1f, 1.05f, 1.2f) * glow);
                Lighting.AddLight(Projectile.Center - Vector2.UnitY * HitHeight * 0.6f,
                    PrismgladeFX.Prism(BaseHue, 0.8f, 0.6f).ToVector3() * (0.8f * glow));
            }
        }

        /// <summary>柱形判定：圈内即判定（半宽=圈半径），一整块居中矩形与可见光涌精确对齐</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile || StrikeProgress < 0.12f) {
                return false;
            }
            float height = Math.Max(HitHeight, 64f);
            Rectangle column = Utils.CenteredRectangle(
                Projectile.Center - new Vector2(0f, height * 0.5f),
                new Vector2(CircleRadius * 2f, height));
            return column.Intersects(targetHitbox);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //命中在受害端本机结算：强辉光只给被打中的本人
            if (!Main.dedServ && target.whoAmI == Main.myPlayer) {
                target.GetModPlayer<PrismgladePlayer>().TriggerJudgedGlow();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            float dim = Cancelled ? 0.35f : 1f;
            float hue = BaseHue;

            DrawGroundRing(elapsed, hue, dim);
            if (Variant == 1) {
                DrawCrystalCluster(elapsed, hue, dim, lightColor);
            }
            if (!Cancelled && elapsed >= TelegraphFrames) {
                DrawBeam(hue);
            }
            if (Variant == 0) {
                //悬晶最后画：实体晶体压在光柱之上，柱读作从晶尖折射而出
                DrawPrism(elapsed, hue, dim, lightColor);
            }
            return false;
        }

        /// <summary>地面审判圈：预告期脉动加速，命中期打满，余韵随柱一起收拢</summary>
        private void DrawGroundRing(int elapsed, float hue, float dim) {
            Texture2D ring = DiffusionCircle4.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 basePos = Projectile.Center + new Vector2(0f, 4f) - Main.screenPosition;

            float grow = MathHelper.Clamp(elapsed / 10f, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float radius = CircleRadius * grow * MathHelper.Clamp(CollapseFactor * 1.4f, 0f, 1f);
            if (radius < 4f) {
                return;
            }

            float progress = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            //脉动随预告推进加速（可读性阀门：越接近落柱闪得越急）
            float pulse = 0.62f + 0.38f * MathF.Sin(elapsed * (0.14f + 0.24f * progress));
            float strike = StrikeProgress;
            float alpha = (0.34f + 0.4f * progress + 0.5f * strike) * pulse * dim * MathHelper.Clamp(CollapseFactor * 1.2f, 0f, 1f);

            //薄锐外缘环（DiffusionCircle4 内容缘约 0.95R），压扁成地面透视椭圆
            float ringScale = radius * 2f / (ring.Width * 0.95f);
            Color rim = PrismgladeFX.Prism(hue, 0.9f, 0.66f) with { A = 0 };
            Main.EntitySpriteDraw(ring, basePos, null, rim * alpha, 0f, ring.Size() * 0.5f,
                new Vector2(ringScale, ringScale * 0.42f), SpriteEffects.None, 0);
            //第二圈色散重影
            Color rim2 = PrismgladeFX.Prism(hue + 0.12f, 0.9f, 0.6f) with { A = 0 };
            Main.EntitySpriteDraw(ring, basePos, null, rim2 * (alpha * 0.45f), 0f, ring.Size() * 0.5f,
                new Vector2(ringScale * 1.07f, ringScale * 0.45f), SpriteEffects.None, 0);

            //圈内浅光池
            Color pool = PrismgladeFX.Prism(hue + 0.05f, 0.5f, 0.72f) with { A = 0 };
            Main.EntitySpriteDraw(glow, basePos, null, pool * (alpha * 0.5f), 0f, glow.Size() * 0.5f,
                new Vector2(radius * 2.2f / 52f, radius * 0.9f / 52f), SpriteEffects.None, 0);

            //预告后半的圈缘光珠：六点沿缘缓爬升（视觉通道的"渐强"）
            if (elapsed >= TelegraphFrames - 22 && elapsed < TelegraphFrames) {
                float lift = (elapsed - (TelegraphFrames - 22)) / 22f;
                for (int i = 0; i < 6; i++) {
                    float ang = MathHelper.TwoPi * i / 6f + Main.GlobalTimeWrappedHourly * 0.8f;
                    Vector2 pos = basePos + new Vector2(MathF.Cos(ang) * radius, MathF.Sin(ang) * radius * 0.42f - 26f * lift);
                    Color bead = PrismgladeFX.Prism(hue + i / 6f, 0.85f, 0.66f) with { A = 0 };
                    Main.EntitySpriteDraw(glow, pos, null, bead * (0.55f * lift * dim), 0f,
                        glow.Size() * 0.5f, 0.16f, SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>地下变体的源头锚点：圈心长出的大水晶，预告期自地面生长，命中期炽亮</summary>
        private void DrawCrystalCluster(int elapsed, float hue, float dim, Color lightColor) {
            Texture2D shard = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 basePos = Projectile.Center + new Vector2(0f, 6f) - Main.screenPosition;

            float grow = MathHelper.Clamp(elapsed / (float)TelegraphFrames, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float strike = StrikeProgress * CollapseFactor;
            float fade = MathHelper.Clamp(CollapseFactor * 1.3f, 0f, 1f) * dim;
            if (grow <= 0.05f || fade <= 0.02f) {
                return;
            }

            //晶簇背光
            Color back = PrismgladeFX.Prism(hue, 0.7f, 0.6f) with { A = 0 };
            Main.EntitySpriteDraw(glow, basePos - new Vector2(0f, 14f * grow), null,
                back * ((0.3f + 0.6f * strike) * grow * fade), 0f, glow.Size() * 0.5f,
                new Vector2(1.6f, 1.1f) * grow, SpriteEffects.None, 0);

            //五根扇排晶柱：中间最高，向两侧递减外倾
            ReadOnlySpan<float> fan = [-0.52f, -0.24f, 0f, 0.24f, 0.52f];
            ReadOnlySpan<float> tall = [1.1f, 1.6f, 2.2f, 1.6f, 1.1f];
            Vector2 orig = new(shard.Width * 0.5f, shard.Height);
            for (int i = 0; i < 5; i++) {
                float wob = MathF.Sin(Projectile.identity * 1.7f + i * 2.3f) * 0.05f;
                Color body = Color.Lerp(lightColor, Color.White, 0.45f + 0.4f * strike) * fade;
                Main.EntitySpriteDraw(shard, basePos + new Vector2(fan[i] * 26f * grow, 2f), null,
                    body, fan[i] + wob, orig, tall[i] * grow, SpriteEffects.None, 0);
                //晶面彩虹敷光
                Color sheen = PrismgladeFX.Prism(hue + i * 0.09f, 0.85f, 0.62f) with { A = 0 };
                Main.EntitySpriteDraw(shard, basePos + new Vector2(fan[i] * 26f * grow, 2f), null,
                    sheen * (fade * (0.3f + 0.55f * strike)), fan[i] + wob, orig,
                    tall[i] * grow * 1.06f, SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 地表变体的源头锚点：圈心上空凝聚的悬浮棱晶（真色 A&gt;0 遮挡体，尖端朝下如悬剑）。
        /// 预告期从无到有凝实并有双卫星绕行（旋转动画）+天光馈入线（来处交代），
        /// 命中期炽亮，余韵随柱一起消融——与地下变体的晶簇同材质，两变体共享"水晶"身份
        /// </summary>
        private void DrawPrism(int elapsed, float hue, float dim, Color lightColor) {
            Texture2D shard = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float height = Math.Max(HitHeight, 64f);
            Vector2 prismPos = Projectile.Center - Main.screenPosition - new Vector2(0f, height + PrismHover);

            //凝实进度：预告 6 帧后开始凝聚，落柱前凝满
            float condense = MathHelper.Clamp((elapsed - 6) / (TelegraphFrames - 14f), 0f, 1f);
            condense = 1f - (1f - condense) * (1f - condense);
            float strike = StrikeProgress;
            float fade = MathHelper.Clamp(CollapseFactor * 1.25f, 0f, 1f) * dim;
            if (condense <= 0.03f || fade <= 0.02f) {
                return;
            }

            //背光晕（加色敷料）
            Color back = PrismgladeFX.Prism(hue, 0.6f, 0.6f) with { A = 0 };
            Main.EntitySpriteDraw(glow, prismPos, null, back * ((0.28f + 0.5f * strike) * condense * fade), 0f,
                glow.Size() * 0.5f, 1.3f * condense, SpriteEffects.None, 0);

            //预告期天光馈入：一缕细光自上而下汇进棱晶（光的来处）
            if (elapsed < TelegraphFrames) {
                Texture2D feedTex = CWRAsset.Extra_98.Value;
                Color feed = PrismgladeFX.Prism(hue + 0.16f, 0.5f, 0.7f) with { A = 0 };
                Main.EntitySpriteDraw(feedTex, prismPos - new Vector2(0f, 130f), null,
                    feed * (0.3f * condense * fade), 0f, feedTex.Size() * 0.5f,
                    new Vector2(0.24f, 5.6f), SpriteEffects.None, 0);
            }

            Vector2 orig = shard.Size() * 0.5f;
            float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + Projectile.identity) * 0.07f;
            //主晶：尖端朝下的悬晶（真色 A>0 遮挡体；魔法凝晶自发光为主，弱吃环境光）
            Color bodyCol = Color.Lerp(lightColor, Color.White, 0.62f + 0.3f * strike) * (condense * fade);
            Main.EntitySpriteDraw(shard, prismPos, null, bodyCol, MathHelper.Pi + sway, orig,
                2.5f * condense, SpriteEffects.None, 0);
            //晶面彩虹敷光（加色）
            Color sheen = PrismgladeFX.Prism(hue, 0.85f, 0.6f) with { A = 0 };
            Main.EntitySpriteDraw(shard, prismPos, null, sheen * ((0.3f + 0.5f * strike) * condense * fade),
                MathHelper.Pi + sway, orig, 2.62f * condense, SpriteEffects.None, 0);

            //双卫星小晶绕行（旋转动画主载体，椭圆轨道压出透视）
            for (int i = 0; i < 2; i++) {
                float ang = Main.GlobalTimeWrappedHourly * 2.1f + Projectile.identity * 0.9f + MathHelper.Pi * i;
                Vector2 off = new(MathF.Cos(ang) * 34f, MathF.Sin(ang) * 13f);
                Color moonCol = Color.Lerp(lightColor, Color.White, 0.55f) * (condense * fade * 0.9f);
                Main.EntitySpriteDraw(shard, prismPos + off, null, moonCol, ang + MathHelper.PiOver2, orig,
                    0.85f * condense, SpriteEffects.None, 0);
                Color moonSheen = PrismgladeFX.Prism(hue + 0.3f + i * 0.25f, 0.85f, 0.62f) with { A = 0 };
                Main.EntitySpriteDraw(shard, prismPos + off, null, moonSheen * (0.35f * condense * fade),
                    ang + MathHelper.PiOver2, orig, 0.92f * condense, SpriteEffects.None, 0);
            }

            //晶心白热点（命中期打满）
            Color heart = PrismgladeFX.Prism(hue, 0.2f, 0.85f) with { A = 0 };
            Main.EntitySpriteDraw(glow, prismPos, null, heart * ((0.3f + 0.6f * strike) * condense * fade), 0f,
                glow.Size() * 0.5f, 0.4f * condense, SpriteEffects.None, 0);
        }

        /// <summary>
        /// 光柱本体。材质=白金晶柱（真 alpha 遮挡层给柱身轮廓）+棱镜分光（加色辉光敷其上）：
        /// ①暗晶缘+折射暗纹（A&gt;0 真色层，亮背景下也有剪影）②柱身纵向彩虹渐变（三段错相，加色）
        /// ③色散镶边（两缘红移/蓝移，加色）④和声微颤（亮度 8Hz 脉动）。宽度有生命周期：5 帧张开 → 维持 → 余韵收细熄灭。
        /// 端部收口：地表变体上端收进悬晶（天光漏斗自上而下馈入），地下变体上端靠贴图梭形自然衰减+顶端星芒；
        /// 下端一律由落点星芒与圈内光涌盖口。判定=圈宽，光涌可见体与判定同宽（不打空气）
        /// </summary>
        private void DrawBeam(float hue) {
            Texture2D body = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D flare = CWRAsset.StarFlare01.Value;
            Vector2 groundPos = Projectile.Center - Main.screenPosition;

            float open = StrikeProgress;
            float collapse = CollapseFactor;
            if (open <= 0.01f || collapse <= 0.01f) {
                return;
            }
            //宽度生命周期：张开→维持→收细；亮度另带和声微颤
            float widthEnv = open * (0.25f + 0.75f * collapse);
            float alphaEnv = open * collapse
                * (0.92f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 42f + Projectile.identity));
            float height = Math.Max(HitHeight, 64f);
            //Extra_98 内容量测：可见宽约 47px、窄芯约 12px（VFX.md 内容表）
            const float ContentW = 47f;
            const float ContentH = 47f;
            float beamW = 44f * widthEnv;

            //圈内光涌：可见体与判定同宽的低穹光潮（判定不宽于可见体）
            Color floodA = PrismgladeFX.Prism(hue, 0.35f, 0.8f) with { A = 0 };
            Color floodB = PrismgladeFX.Prism(hue + 0.14f, 0.7f, 0.66f) with { A = 0 };
            Main.EntitySpriteDraw(glow, groundPos, null, floodA * (0.55f * alphaEnv), 0f,
                glow.Size() * 0.5f, new Vector2(CircleRadius * 2.15f / 52f, CircleRadius * 0.95f / 52f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, groundPos - new Vector2(0f, 18f), null, floodB * (0.4f * alphaEnv), 0f,
                glow.Size() * 0.5f, new Vector2(CircleRadius * 1.7f / 52f, CircleRadius * 1.15f / 52f), SpriteEffects.None, 0);

            Vector2 beamMid = groundPos - new Vector2(0f, height * 0.5f);
            //白金晶柱遮挡层（真 alpha A>0）：暗晶缘先铺出两缘轮廓，白金柱身覆其内——
            //柱体有剪影可读，不再全靠加法发光（Extra_98 是真 alpha 贴图，可承载暗层与实体染色）
            float bodyEnv = open * collapse;
            Color edgeDark = PrismgladeFX.Prism(hue, 0.55f, 0.16f) * (0.6f * bodyEnv);
            Vector2 edgeScale = new(beamW * 0.3f / ContentW, height / ContentH);
            Main.EntitySpriteDraw(body, beamMid - new Vector2(beamW * 0.58f, 0f), null,
                edgeDark, 0f, body.Size() * 0.5f, edgeScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(body, beamMid + new Vector2(beamW * 0.58f, 0f), null,
                edgeDark, 0f, body.Size() * 0.5f, edgeScale, SpriteEffects.None, 0);
            Color platinum = new Color(255, 246, 222) * (0.5f * bodyEnv);
            Main.EntitySpriteDraw(body, beamMid, null, platinum, 0f, body.Size() * 0.5f,
                new Vector2(beamW * 0.95f / ContentW, height / ContentH), SpriteEffects.None, 0);

            //柱身三段纵向彩虹渐变（段间 25% 重叠防露缝，加色敷在白金柱身上）
            for (int i = 0; i < 3; i++) {
                float segH = height / 3f * 1.25f;
                Vector2 segCenter = groundPos - new Vector2(0f, height * (i + 0.5f) / 3f);
                Color segCol = PrismgladeFX.Prism(hue + i * 0.08f, 0.75f, 0.68f) with { A = 0 };
                Main.EntitySpriteDraw(body, segCenter, null, segCol * (0.85f * alphaEnv), 0f,
                    body.Size() * 0.5f, new Vector2(beamW / ContentW, segH / ContentH), SpriteEffects.None, 0);
            }

            //折射暗纹：柱内两道不对称暗竖纹（晶体内部折射面，真 alpha 压出纵深）
            Color facet = PrismgladeFX.Prism(hue, 0.5f, 0.22f) * (0.28f * bodyEnv);
            Vector2 facetScale = new(beamW * 0.1f / ContentW, height * 0.96f / ContentH);
            Main.EntitySpriteDraw(body, beamMid - new Vector2(beamW * 0.2f, 0f), null,
                facet, 0f, body.Size() * 0.5f, facetScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(body, beamMid + new Vector2(beamW * 0.26f, 0f), null,
                facet, 0f, body.Size() * 0.5f, facetScale, SpriteEffects.None, 0);

            //色散镶边：左红移右蓝移（棱镜签名之一）
            Color fringeR = PrismgladeFX.Prism(hue - 0.07f, 0.95f, 0.6f) with { A = 0 };
            Color fringeB = PrismgladeFX.Prism(hue + 0.07f, 0.95f, 0.6f) with { A = 0 };
            Vector2 fringeScale = new(beamW * 0.36f / ContentW, height * 1.04f / ContentH);
            Main.EntitySpriteDraw(body, beamMid - new Vector2(beamW * 0.55f, 0f), null,
                fringeR * (0.5f * alphaEnv), 0f, body.Size() * 0.5f, fringeScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(body, beamMid + new Vector2(beamW * 0.55f, 0f), null,
                fringeB * (0.5f * alphaEnv), 0f, body.Size() * 0.5f, fringeScale, SpriteEffects.None, 0);

            //白热窄芯
            Color core = PrismgladeFX.Prism(hue, 0.15f, 0.88f) with { A = 0 };
            Main.EntitySpriteDraw(body, beamMid, null, core * (0.9f * alphaEnv), 0f,
                body.Size() * 0.5f, new Vector2(7f * widthEnv / 12f, height / ContentH), SpriteEffects.None, 0);

            if (Variant == 0) {
                //地表：天光漏斗两段（越高越宽越淡），下端收进悬晶——柱的来处是晶折射的天光
                Vector2 prismTop = groundPos - new Vector2(0f, height + PrismHover);
                Vector2 tail1 = prismTop - new Vector2(0f, height * 0.5f);
                Vector2 tail2 = prismTop - new Vector2(0f, height * 1.3f);
                Color tailCol = PrismgladeFX.Prism(hue + 0.2f, 0.6f, 0.7f) with { A = 0 };
                Main.EntitySpriteDraw(body, tail1, null, tailCol * (0.38f * alphaEnv), 0f,
                    body.Size() * 0.5f, new Vector2(beamW * 1.3f / ContentW, height * 1.1f / ContentH), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(body, tail2, null, tailCol * (0.2f * alphaEnv), 0f,
                    body.Size() * 0.5f, new Vector2(beamW * 1.75f / ContentW, height * 1.6f / ContentH), SpriteEffects.None, 0);
            }
            else {
                //地下：柱顶星芒收口（晶簇共鸣的能量尖）
                Color tip = PrismgladeFX.Prism(hue + 0.1f, 0.4f, 0.8f) with { A = 0 };
                Main.EntitySpriteDraw(flare, groundPos - new Vector2(0f, height), null,
                    tip * (0.5f * alphaEnv), Projectile.identity * 0.7f, flare.Size() * 0.5f,
                    0.24f * widthEnv, SpriteEffects.None, 0);
            }

            //落点星芒：命中拍打满后随余韵退潮
            float flarePulse = MathHelper.Clamp(open * 1.2f, 0f, 1f) * (0.45f + 0.55f * collapse);
            Color impact = PrismgladeFX.Prism(hue, 0.25f, 0.85f) with { A = 0 };
            Main.EntitySpriteDraw(flare, groundPos - new Vector2(0f, 6f), null,
                impact * (0.8f * flarePulse), -Projectile.identity * 0.5f, flare.Size() * 0.5f,
                new Vector2(0.5f, 0.34f) * (0.6f + 0.4f * open), SpriteEffects.None, 0);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || Cancelled) {
                return;
            }
            //光尘余韵：活得比弹幕久的慢速光尘（余韵三段的最后一口气）
            for (int i = 0; i < 5; i++) {
                var mote = PRTLoader.NewParticle<PRT_PrismgladeMote>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-CircleRadius, CircleRadius) * 0.6f,
                        -Main.rand.NextFloat(0f, 30f)),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 1f)),
                    default, Main.rand.NextFloat(0.18f, 0.3f));
                if (mote != null) {
                    mote.hue = BaseHue + i / 5f;
                    mote.Lifetime = Main.rand.Next(70, 110);
                }
            }
            //碎晶终融：真 alpha 微晶缓沉（地表变体后两粒剥落自消融的悬晶），收束整场审判的物质余账
            for (int i = 0; i < 4; i++) {
                bool fromPrism = Variant == 0 && i >= 2;
                Vector2 pos = fromPrism
                    ? Projectile.Center - new Vector2(Main.rand.NextFloat(-12f, 12f), Math.Max(HitHeight, 64f) + PrismHover)
                    : Projectile.Center + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * CircleRadius, -Main.rand.NextFloat(0f, 18f));
                var chip = PRTLoader.NewParticle<PRT_PrismgladeChip>(pos,
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(0.1f, 0.45f)),
                    default, Main.rand.NextFloat(0.22f, 0.34f));
                if (chip != null) {
                    chip.hue = BaseHue + i / 4f;
                    chip.Lifetime = Main.rand.Next(70, 120);
                }
            }
        }
    }
}
