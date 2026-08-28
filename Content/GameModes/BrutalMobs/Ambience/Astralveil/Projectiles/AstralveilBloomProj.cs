using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Astralveil.Projectiles
{
    /// <summary>
    /// 「感染绽放」星尘花。ai[0]=存续帧（档位只调持续，随生成包同步，各端首个本地刻自设 timeLeft）。
    /// 星矛落点绽开一朵靛/橙星尘晶羽：向上扇形展瓣（带过冲，无伤）→ 边界晶环列装（自持预告 ≥45f）
    /// → 驻留期滞留圈内累积微量伤害（原版受击无敌帧天然节流）→ 凋散期花瓣垂落、晶环黯灭、
    /// 星屑坠地，判定先于视觉半程关闭。落点因此有"余威"：星矛躲过了也别原路折返
    /// </summary>
    internal class AstralveilBloomProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>滞留伤害（经典模式基线：星辉群系怪接触伤中位 50 × 0.4，取白金星舰前口径——
        /// 灾厄源码 NPCs/Astral 各怪 SetDefaults 中位 50，击败白金星舰后才升至 ~85；困难度加成由原版结算）</summary>
        internal const int TickDamage = 20;

        /// <summary>展瓣帧数</summary>
        private const int UnfurlFrames = 26;
        /// <summary>凋散帧数</summary>
        private const int WitherFrames = 42;
        /// <summary>伤害起始帧（自持预告契约 ≥45f：展瓣毕且边界晶环列装满才开窗，半开不咬人）</summary>
        private const int HitStartFrames = 48;
        /// <summary>感染区半径</summary>
        private const float Radius = 84f;
        /// <summary>判定板高度（低矮贴地）</summary>
        private const float SlabHeight = 96f;
        /// <summary>地面透视压扁比（与星纹圈同语汇）</summary>
        private const float GroundSquash = 0.42f;
        /// <summary>花瓣数</summary>
        private const int PetalCount = 6;
        /// <summary>花瓣长度</summary>
        private const float PetalLength = 78f;
        /// <summary>边界晶粒数</summary>
        private const int RingGrains = 12;

        /// <summary>暗橙晶体底色（真 alpha 暗层用，与暗靛交替成星辉双色）</summary>
        private static readonly Color EmberDeep = new(96, 52, 22);

        private int Duration => (int)Projectile.localAI[1];
        private int Elapsed => Duration - Projectile.timeLeft;
        private bool Ready => Projectile.localAI[1] > 0f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 240;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            //占位寿命：首个本地刻按 ai[0] 重设（各端同规则自算，非服务端单方面改动）
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>展瓣包络：EaseOutCubic + 轻微过冲，收在 1</summary>
        private float Unfurl {
            get {
                float u = Math.Min(Elapsed / (float)UnfurlFrames, 1f);
                float s = 1f - (1f - u) * (1f - u) * (1f - u);
                return s + 0.10f * MathF.Sin(s * MathF.PI);
            }
        }

        /// <summary>凋散包络 1→0</summary>
        private float WitherEase => MathHelper.Clamp(Projectile.timeLeft / (float)WitherFrames, 0f, 1f);

        public override void AI() {
            if (!Ready) {
                int duration = (int)Projectile.ai[0];
                if (duration < 120) {
                    duration = 120;//防御：ai 缺省时的最短寿命
                }
                Projectile.localAI[1] = duration;
                Projectile.timeLeft = duration;
            }
            int elapsed = Elapsed;

            //判定窗：展瓣毕且晶环列装满才起效（自持预告 ≥45f），凋散半程即关（花瓣收拢+晶环黯灭=安全信号）；
            //中途关残酷模式或 Boss 在场时伤害层让位
            Projectile.hostile = GameModeSystem.BrutalActive
                && elapsed >= HitStartFrames && Projectile.timeLeft > WitherFrames / 2
                && !CWRWorld.HasBoss;

            if (Main.dedServ) {
                return;
            }

            if (elapsed == 8) {
                //绽放和音：一声上挑水晶震音（与星矛蜂鸣同族但更轻更高）
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.26f, Pitch = 0.75f, MaxInstances = 4 },
                    Projectile.Center);
            }
            if (elapsed == HitStartFrames) {
                //晶环列装完毕：判定开启的听觉确认（双通道预告的收尾拍）
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.30f, Pitch = 0.95f, MaxInstances = 4 },
                    Projectile.Center);
            }
            if (Projectile.timeLeft == WitherFrames) {
                //凋散起点：干燥沙沙声
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 4 },
                    Projectile.Center);
            }

            float env = Unfurl * WitherEase;
            if (Projectile.timeLeft > WitherFrames) {
                //驻留期：感染区内星屑缓升（≤1 粒/8 帧）
                if (Main.rand.NextBool(8)) {
                    bool indigo = Main.rand.NextFloat() < AstralveilFX.IndigoFraction;
                    Dust dust = Dust.NewDustPerfect(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-Radius, Radius) * 0.7f,
                            -Main.rand.NextFloat(0f, 40f)),
                        AstralveilFX.DustFor(indigo),
                        new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.3f, 0.8f)),
                        140, default, Main.rand.NextFloat(0.8f, 1.15f));
                    dust.noGravity = true;
                }
            }
            else if (Main.rand.NextBool(3)) {
                //凋散期：星屑失去浮力坠地
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-Radius, Radius) * 0.6f,
                        -Main.rand.NextFloat(10f, 60f)),
                    AstralveilFX.DustFor(Main.rand.NextBool()),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.4f, 1.2f)),
                    120, default, Main.rand.NextFloat(0.7f, 1.05f));
                dust.noGravity = false;
            }

            Lighting.AddLight(Projectile.Center - new Vector2(0f, 24f),
                new Vector3(0.30f, 0.22f, 0.52f) * env);
        }

        /// <summary>滞留判定：感染区低矮判定板；受击无敌帧天然把伤害节流成"累积微量"</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile || !Ready) {
                return false;
            }
            float reach = Radius * Math.Min(Unfurl, 1f);
            Rectangle slab = new(
                (int)(Projectile.Center.X - reach),
                (int)(Projectile.Center.Y - SlabHeight),
                (int)(reach * 2f), (int)SlabHeight + 8);
            return slab.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!Ready) {
                return false;
            }
            Texture2D glow = CWRAsset.SoftGlow.Value;
            //黑底四芒星：花心星芒走 A=0 加色写法，黑底孪生才是正配（真 alpha 版形状在 A 通道，A=0 会扔掉它）
            Texture2D star = CWRAsset.StarTexture.Value;
            Texture2D petalTex = CWRAsset.Extra_98.Value;
            if (glow == null || star == null || petalTex == null) {
                return false;
            }
            int elapsed = Elapsed;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 glowOrig = glow.Size() * 0.5f;
            Vector2 starOrig = star.Size() * 0.5f;
            Vector2 petalOrig = petalTex.Size() * 0.5f;
            float phase = Projectile.identity * 1.91f;
            float unfurl = Unfurl;
            float wither = WitherEase;
            float sag = (1f - wither) * 0.55f;
            float alpha = MathF.Pow(wither, 0.7f) * Math.Min(unfurl * 1.6f, 1f);

            //地面暗垫（真 alpha）：感染区的"根"
            Main.EntitySpriteDraw(petalTex, center, null, AstralveilFX.IndigoDeep * (0.36f * alpha),
                MathHelper.PiOver2, petalOrig,
                new Vector2(Radius * 0.8f / 24f, Radius * 2.1f / 42f), SpriteEffects.None, 0);

            //花瓣：上半扇 6 支星尘晶羽，靛/橙交替，逐瓣摆动与呼吸；凋散时向两侧垂落
            for (int i = 0; i < PetalCount; i++) {
                float baseAng = -MathF.PI + 0.42f + i * (MathF.PI - 0.84f) / (PetalCount - 1);
                float sway = MathF.Sin(elapsed * 0.045f + phase + i * 1.3f) * 0.05f * unfurl;
                float droop = sag * MathF.Sign(MathF.Cos(baseAng));
                float ang = baseAng + sway + droop;
                float len = PetalLength * unfurl * (0.35f + 0.65f * wither)
                    * (1f + 0.05f * MathF.Sin(elapsed * 0.07f + i));
                Vector2 dir = new(MathF.Cos(ang), MathF.Sin(ang));
                Vector2 pos = center + dir * (len * 0.5f) + new Vector2(0f, -2f);
                bool indigo = (i & 1) == 0;
                Color body = (indigo ? AstralveilFX.Indigo : AstralveilFX.Orange) * (0.60f * alpha);
                //瓣体（真 alpha 梭形承轮廓）
                Main.EntitySpriteDraw(petalTex, pos, null, body, ang + MathHelper.PiOver2,
                    petalOrig, new Vector2(0.55f, len / 42f), SpriteEffects.None, 0);
                //瓣脉亮线（加色敷料，尖端更亮）
                Color vein = AstralveilFX.A0(indigo ? AstralveilFX.IndigoPale : AstralveilFX.OrangePale);
                Main.EntitySpriteDraw(glow, center + dir * (len * 0.7f), null, vein * (0.5f * alpha),
                    ang + MathHelper.PiOver2, glowOrig, new Vector2(0.12f, len / 90f), SpriteEffects.None, 0);
            }

            //花心：星芒 + 橙核脉动
            float corePulse = 0.75f + 0.25f * MathF.Sin(elapsed * 0.11f + phase);
            Main.EntitySpriteDraw(glow, center + new Vector2(0f, -6f), null,
                AstralveilFX.A0(AstralveilFX.OrangePale) * (0.55f * alpha * corePulse),
                0f, glowOrig, 0.55f * unfurl, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, center + new Vector2(0f, -6f), null,
                AstralveilFX.A0(Color.White) * (0.7f * alpha * corePulse),
                elapsed * 0.01f + phase, starOrig, 0.06f * unfurl, SpriteEffects.None, 0);

            //边界晶环：星辉晶尘沿感染区边缘列装（暗芯真 alpha 承遮挡+晶尖加色微光）。
            //列装进度与伤害窗同源（elapsed/timeLeft）：列装满=判定开，关窗后晶粒随即黯灭
            //（可见环=判定环的公平读数，预告期渐显、退场期渐隐都压在无伤窗内）
            float arm = MathHelper.Clamp((elapsed - UnfurlFrames) / (float)(HitStartFrames - UnfurlFrames), 0f, 1f);
            float armGate = MathHelper.Clamp((Projectile.timeLeft - WitherFrames / 2 + 14) / 14f, 0f, 1f);
            float ring = arm * armGate * alpha;
            if (ring > 0.02f) {
                float edgePulse = 0.6f + 0.4f * MathF.Sin(elapsed * 0.16f + phase);
                for (int i = 0; i < RingGrains; i++) {
                    float ang = phase + elapsed * 0.008f + MathHelper.TwoPi * i / RingGrains;
                    float h = 0.5f + 0.5f * MathF.Sin(i * 2.7f + phase * 3f);
                    Vector2 pos = center + new Vector2(
                        MathF.Cos(ang) * Radius * unfurl, MathF.Sin(ang) * Radius * unfurl * GroundSquash);
                    float len = (9f + 7f * h) * (0.75f + 0.25f * arm);
                    float lean = MathF.Cos(ang) * 0.30f + (h - 0.5f) * 0.26f;
                    bool indigo = (i & 1) == 0;
                    //晶粒暗芯（真 alpha 梭形，微微外倾如自地面析出）
                    Main.EntitySpriteDraw(petalTex, pos + new Vector2(0f, -len * 0.28f), null,
                        (indigo ? AstralveilFX.IndigoDeep : EmberDeep) * (0.85f * ring),
                        lean, petalOrig, new Vector2(0.26f, len / 42f), SpriteEffects.None, 0);
                    //晶尖微光（加色敷料）
                    Main.EntitySpriteDraw(glow,
                        pos + new Vector2(MathF.Sin(lean), -MathF.Cos(lean)) * (len * 0.42f), null,
                        AstralveilFX.A0(indigo ? AstralveilFX.IndigoPale : AstralveilFX.OrangePale)
                            * (0.32f * edgePulse * ring),
                        0f, glowOrig, 0.085f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
