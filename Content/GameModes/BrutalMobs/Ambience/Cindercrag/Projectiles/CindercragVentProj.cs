using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Cindercrag.Projectiles
{
    /// <summary>
    /// 「崖口喷焰」：崖壁裂口周期喷吐的硫火舌。ai[0]=喷向弧度 ai[1]=喷长px ai[2]=裂口面(0右1左2上3下)。
    /// 生成帧锁死喷口与喷向（预告即承诺）：裂口蓄压 56 帧（红光渐盛+汽笛式嘶鸣，双通道预告）
    /// → 喷焰 38 帧（仅此窗口有判定，触碰微量伤害+短暂着火）→ 裂口冷却暗淡 20 帧。
    /// 喷长在生成时按净空钳过，火舌不穿岩；源头在崖壁裂口，与岩浆池液面熔泡严格分野。
    /// 可见体=Extra_98 真 alpha 焦烟暗衬底（承遮挡与轮廓）+TearFlame 加色焰层敷其上（暗体+热边，同烬羽层序）
    /// </summary>
    internal class CindercragVentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "TearFlame01")]
        private static Asset<Texture2D> FlameTex = null;

        /// <summary>真 alpha 梭形，暗衬底专用（TearFlame/SoftGlow 是黑底贴图画不出暗体）</summary>
        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        private static Asset<Texture2D> SootTex = null;

        /// <summary>喷长上限（短程火舌）</summary>
        internal const float JetMaxLen = 210f;
        /// <summary>蓄压预告帧（公平契约 ≥45，各档位一律不缩短）</summary>
        private const int TelegraphFrames = 56;
        /// <summary>喷焰帧（判定窗=可见喷焰窗）</summary>
        private const int EruptFrames = 38;
        private const int FadeFrames = 20;
        /// <summary>火舌判定半宽</summary>
        private const float JetHalfWidth = 15f;
        /// <summary>着火时长（短暂原版 On Fire!）</summary>
        private const int OnFireTicks = 150;
        /// <summary>火舌展开用时</summary>
        private const int JetRiseFrames = 8;

        /// <summary>裂口暗红</summary>
        private static readonly Color CrackDeep = new(196, 44, 26);
        /// <summary>焰体红橙</summary>
        private static readonly Color FlameMid = new(255, 92, 30);
        /// <summary>焰芯暖金（暖材质不走纯白）</summary>
        private static readonly Color FlameCore = new(255, 190, 90);
        /// <summary>焰外缘焦暗红</summary>
        private static readonly Color FlameOuter = new(150, 36, 20);
        /// <summary>舌身焦烟暗衬（带 A 承担轮廓与遮挡，同烬羽暗体一族）</summary>
        private static readonly Color SootBody = new(46, 20, 14);
        /// <summary>翻卷缘片暗赭</summary>
        private static readonly Color SootOchre = new(76, 32, 18);
        /// <summary>裂口暗岩缘</summary>
        private static readonly Color RockRim = new(38, 20, 16);
        /// <summary>Extra_98 可见域占画布比（VFX.md 实测 ext≈0.65，可见像素换算用）</summary>
        private const float SootVisFrac = 0.65f;

        private float JetDir => Projectile.ai[0];
        private float JetLen => Projectile.ai[1];
        private int Face => (int)Projectile.ai[2];

        private static int TotalLife => TelegraphFrames + EruptFrames + FadeFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        internal static Vector2 FaceNormal(int face) => face switch {
            0 => Vector2.UnitX,
            1 => -Vector2.UnitX,
            2 => -Vector2.UnitY,
            _ => Vector2.UnitY,
        };

        /// <summary>喷焰展开 0~1（快速爆出）</summary>
        private float JetProgress {
            get {
                int t = Elapsed - TelegraphFrames;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= JetRiseFrames) {
                    return 1f;
                }
                float x = t / (float)JetRiseFrames;
                return 1f - (1f - x) * (1f - x) * (1f - x);
            }
        }

        /// <summary>退场收缩 1→0</summary>
        private float RetractFactor {
            get {
                int t = Elapsed - TelegraphFrames - EruptFrames;
                if (t <= 0) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - t / (float)FadeFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 340;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = false;//喷焰窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int elapsed = Elapsed;
            //判定窗=可见喷焰窗；Boss 在场即时停伤（世界旗标各端一致，判伤端以本机视图为准）
            Projectile.hostile = elapsed >= TelegraphFrames
                && elapsed < TelegraphFrames + EruptFrames && !CWRWorld.HasBoss;

            if (Main.dedServ) {
                return;
            }

            Vector2 dir = JetDir.ToRotationVector2();
            if (elapsed < TelegraphFrames) {
                float progress = elapsed / (float)TelegraphFrames;
                //汽笛式嘶鸣：音高音量随蓄压攀升，听觉通道预告
                if (elapsed % 14 == 0) {
                    SoundEngine.PlaySound(SoundID.LiquidsWaterLava with {
                        Volume = 0.24f + 0.30f * progress,
                        Pitch = -0.25f + 0.75f * progress,
                        MaxInstances = 5,
                    }, Projectile.Center);
                }
                //裂口渗烟与火星（喷向侧渗出，喷向预告期即可读）
                if (Main.rand.NextBool(3)) {
                    Dust smoke = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(4f, 14f),
                        DustID.Smoke, dir * Main.rand.NextFloat(0.6f, 1.6f) - Vector2.UnitY * 0.4f,
                        160, new Color(60, 40, 40), 0.9f + 0.5f * progress);
                    smoke.noGravity = true;
                }
                if (Main.rand.NextBool(5)) {
                    Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch,
                        dir * Main.rand.NextFloat(0.8f, 2.2f + 2f * progress), 100, default, 0.9f);
                    spark.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.14f, 0.06f) * (0.3f + 0.7f * progress));
                return;
            }

            if (elapsed == TelegraphFrames) {
                //点火拍：喷焰轰响 + 汽液爆嘶
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.8f, Pitch = -0.15f, MaxInstances = 4 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.45f, Pitch = -0.4f, MaxInstances = 4 }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    Dust burst = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch,
                        dir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(3f, 8f),
                        90, default, Main.rand.NextFloat(1.2f, 1.9f));
                    burst.noGravity = true;
                }
            }
            else if (elapsed == TelegraphFrames + 18) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.4f, Pitch = -0.05f, MaxInstances = 4 }, Projectile.Center);
            }

            float reach = JetLen * JetProgress * RetractFactor;
            if (elapsed < TelegraphFrames + EruptFrames) {
                //喷焰期：沿舌身持续火尘，尖端剥落烬羽（烬羽与常态氛围同料，火有来处）
                for (int i = 0; i < 2; i++) {
                    if (!Main.rand.NextBool(3)) {
                        Dust flame = Dust.NewDustPerfect(
                            Projectile.Center + dir * Main.rand.NextFloat(0f, MathF.Max(reach - 8f, 8f)),
                            Main.rand.NextBool(4) ? DustID.Torch : DustID.RedTorch,
                            dir * Main.rand.NextFloat(2f, 5f) - Vector2.UnitY * 0.5f,
                            80, default, Main.rand.NextFloat(1f, 1.6f));
                        flame.noGravity = true;
                    }
                }
                if (Main.rand.NextBool(7)) {
                    PRTLoader.NewParticle<PRT_CindercragFeather>(Projectile.Center + dir * reach,
                        dir * 1.4f - Vector2.UnitY * 0.5f, default, Main.rand.NextFloat(0.55f, 0.85f));
                }
                //舌尖飞溅熔星：熔渣星点带重力甩出坠回崖底，火有去处
                if (Main.rand.NextBool(2)) {
                    Dust.NewDustPerfect(Projectile.Center + dir * reach, DustID.Lava,
                        dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2.5f, 6.5f)
                        - Vector2.UnitY * Main.rand.NextFloat(0.3f, 1.1f),
                        0, default, Main.rand.NextFloat(0.9f, 1.4f));
                }
            }
            else if (Main.rand.NextBool(2)) {
                //冷却期：裂口余烟
                Dust smoke = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(2f, 10f),
                    DustID.Smoke, dir * 0.8f - Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.2f),
                    170, new Color(56, 38, 38), 1.1f);
                smoke.noGravity = true;
            }

            //舌身照明
            float bodyLight = JetProgress * RetractFactor;
            if (bodyLight > 0.05f) {
                Lighting.AddLight(Projectile.Center + dir * reach * 0.35f, new Vector3(0.7f, 0.26f, 0.1f) * bodyLight);
                Lighting.AddLight(Projectile.Center + dir * reach * 0.8f, new Vector3(0.45f, 0.16f, 0.06f) * bodyLight);
            }
        }

        /// <summary>线形判定：喷口到当前舌端的加宽线段，判定窗已由 hostile 门控</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            float reach = JetLen * JetProgress;
            if (reach < 30f) {
                return false;
            }
            float _ = 0f;
            Vector2 dir = JetDir.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + dir * reach, JetHalfWidth * 2f, ref _);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
            => target.AddBuff(BuffID.OnFire, OnFireTicks);

        public override bool PreDraw(ref Color lightColor) {
            int elapsed = Elapsed;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            Vector2 glowOrigin = glow.Size() * 0.5f;
            Vector2 mouthPos = Projectile.Center - Main.screenPosition;
            Vector2 dir = JetDir.ToRotationVector2();
            //裂口面切向（辉光贴面拉长）
            Vector2 tangent = FaceNormal(Face).RotatedBy(MathHelper.PiOver2);
            float slitRot = tangent.ToRotation();

            if (elapsed < TelegraphFrames) {
                //蓄压：裂口红光渐盛，脉动频率随蓄压攀升（视觉侧的汽笛）
                float progress = elapsed / (float)TelegraphFrames;
                float pulse = 0.66f + 0.34f * MathF.Sin(elapsed * (0.18f + 0.5f * progress) + Projectile.identity);
                float heat = (0.25f + 0.75f * progress * progress) * pulse;

                Texture2D rimTex = SootTex?.Value;
                if (rimTex != null) {
                    //暗岩缘：真 alpha 暗层沿崖面切向勾出裂口剪影，岩缘稳定不随光脉动；预热光保留加色在内
                    float rimGrow = 0.9f + 0.1f * progress;
                    DrawSlit(rimTex, mouthPos, rimTex.Size() * 0.5f, slitRot + MathHelper.PiOver2,
                        RockRim * (0.55f + 0.25f * progress), new Vector2(0.85f, 2.15f) * rimGrow);
                }

                Color deep = CrackDeep with { A = 0 };
                Color core = FlameMid with { A = 0 };
                DrawSlit(glow, mouthPos, glowOrigin, slitRot, deep * (0.4f * heat), new Vector2(1.7f, 0.6f));
                DrawSlit(glow, mouthPos, glowOrigin, slitRot, core * (0.3f * heat), new Vector2(0.9f, 0.32f));
                //喷向指示：喷口沿喷向淌出一条暗红微光，预告期即可读
                Vector2 hintPos = mouthPos + dir * 22f;
                Main.EntitySpriteDraw(glow, hintPos, null, deep * (0.22f * heat), JetDir,
                    glowOrigin, new Vector2(1.5f, 0.3f), SpriteEffects.None, 0);
                return false;
            }

            float reachVis = JetLen * JetProgress * MathHelper.Clamp(RetractFactor * 1.25f, 0f, 1f);
            float fade = RetractFactor;
            if (reachVis <= 4f) {
                return false;
            }

            float tongueRot = JetDir + MathHelper.PiOver2;
            Texture2D soot = SootTex?.Value;
            if (soot != null) {
                Vector2 sootOrigin = soot.Size() * 0.5f;
                float visW = soot.Width * SootVisFrac;
                float visH = soot.Height * SootVisFrac;
                //暗岩唇：点火拍裂口撑开一圈，冷却尾段随余焰缓熄（岩缘比焰体熄得慢）
                float rimFade = MathHelper.Clamp(fade * 2f, 0f, 1f);
                DrawSlit(soot, mouthPos, sootOrigin, slitRot + MathHelper.PiOver2,
                    RockRim * ((0.62f + 0.18f * fade) * rimFade), new Vector2(1f, 2.45f));

                //焦烟暗衬底：真 alpha 舌身剪影先落，加色焰层敷其上（暗体+热边，同烬羽层序）；
                //剪影略宽略长于外焰，在亮红崖背景上兜出可辨轮廓，随喷发相位伸缩
                float sootFade = 0.45f + 0.55f * fade;
                float bodyLen = reachVis * 1.06f;
                Main.EntitySpriteDraw(soot, mouthPos + dir * (bodyLen * 0.5f), null,
                    SootBody * (0.32f * sootFade), tongueRot, sootOrigin,
                    new Vector2(84f / visW, bodyLen * 1.12f / visH), SpriteEffects.None, 0);
                float bodyJit = 0.95f + 0.05f * MathF.Sin((elapsed * 1.1f + Projectile.identity) * 1.3f);
                Main.EntitySpriteDraw(soot, mouthPos + dir * (bodyLen * bodyJit * 0.5f), null,
                    SootBody * (0.8f * sootFade), tongueRot, sootOrigin,
                    new Vector2(62f / visW, bodyLen * bodyJit / visH), SpriteEffects.None, 0);

                //缘片翻卷：暗赭缘鳞沿舌身错相摆动，撕开平滑轮廓
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                ReadOnlySpan<float> curlFrac = [0.3f, 0.56f, 0.82f];
                for (int i = 0; i < 3; i++) {
                    float side = i % 2 == 0 ? 1f : -1f;
                    float wob = MathF.Sin(elapsed * 0.33f + i * 2.1f + Projectile.identity * 0.7f);
                    Vector2 curlPos = mouthPos + dir * (reachVis * curlFrac[i]) + perp * (side * (10f + 4f * wob));
                    Main.EntitySpriteDraw(soot, curlPos, null, SootOchre * (0.5f * sootFade),
                        tongueRot + side * (0.5f + 0.28f * wob), sootOrigin,
                        new Vector2(0.5f, reachVis * (0.34f - 0.06f * i) / visH), SpriteEffects.None, 0);
                }
                //舌尖烟帽：焰尖过渡成烟，端头不平滑收口，兜住焰层抖动越界的舌尖
                Main.EntitySpriteDraw(soot, mouthPos + dir * (reachVis * 0.98f), null,
                    SootBody * (0.5f * sootFade), tongueRot + 0.35f * MathF.Sin(elapsed * 0.5f + Projectile.identity),
                    sootOrigin, new Vector2(0.9f, reachVis * 0.3f / visH), SpriteEffects.None, 0);
            }

            //裂口口部辉光（喷焰期最亮，冷却期暗淡；加色在暗岩唇与舌根剪影之上）
            Color mouthDeep = CrackDeep with { A = 0 };
            DrawSlit(glow, mouthPos, glowOrigin, slitRot, mouthDeep * (0.5f * fade), new Vector2(1.9f, 0.7f));
            DrawSlit(glow, mouthPos, glowOrigin, slitRot, (FlameCore with { A = 0 }) * (0.35f * fade), new Vector2(0.8f, 0.3f));

            Texture2D flame = FlameTex?.Value;
            if (flame == null) {
                return false;
            }
            //火舌加色焰层：根锚喷口向外舔，三层异宽异长，逐帧高频抖动是火的时域签名；
            //贴图自带噪声撕裂端头，外缘焦暗→焰体红橙→窄焰芯暖金
            var flameOrigin = new Vector2(flame.Width * 0.5f, flame.Height);
            //外层全宽 ~46px，判定半宽 15px 藏在可见焰体之内（判定不宽于可见）
            const float OuterWidthPx = 46f;
            ReadOnlySpan<float> lenFrac = [1f, 0.82f, 0.6f];
            ReadOnlySpan<float> widFrac = [1f, 0.72f, 0.48f];
            Span<Color> cols = [FlameOuter, FlameMid, FlameCore];
            for (int layer = 0; layer < 3; layer++) {
                float jitter = 0.86f + 0.24f * MathF.Sin((elapsed * 2.3f + layer * 2.1f + Projectile.identity) * 1.7f);
                float len = reachVis * lenFrac[layer] * jitter;
                //冷却期焰色向焦暗收拢
                Color col = Color.Lerp(FlameOuter, cols[layer], fade) with { A = 0 };
                Main.EntitySpriteDraw(flame, mouthPos, null, col * (0.25f + 0.75f * fade),
                    tongueRot, flameOrigin,
                    new Vector2(OuterWidthPx * widFrac[layer] / flame.Width, len / flame.Height),
                    SpriteEffects.None, 0);
            }
            return false;
        }

        /// <summary>裂口贴面辉光的统一画法（长轴沿崖面切向）</summary>
        private static void DrawSlit(Texture2D tex, Vector2 pos, Vector2 origin,
            float rotation, Color color, Vector2 scale)
            => Main.EntitySpriteDraw(tex, pos, null, color, rotation, origin, scale, SpriteEffects.None, 0);

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //余韵：裂口末缕烟
            Vector2 dir = JetDir.ToRotationVector2();
            for (int i = 0; i < 4; i++) {
                Dust smoke = Dust.NewDustPerfect(Projectile.Center + dir * Main.rand.NextFloat(0f, 16f),
                    DustID.Smoke, dir * Main.rand.NextFloat(0.4f, 1f) - Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f),
                    180, new Color(52, 36, 36), 1f);
                smoke.noGravity = true;
            }
        }
    }
}
