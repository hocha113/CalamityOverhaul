using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEmpress
{
    /// <summary>
    /// 女皇鬼奴的曼陀罗花瓣：一片有体积的血水花瓣，不是光点。
    /// 波内六瓣等分放射、逐帧旋进（ai0=角速度），三波彼此错角旋向交替，
    /// 整场图案咬合成缓慢呼吸的玫瑰纹样；瓣身沿轴翻折扑动、飞行甩珠，
    /// 末段凋萎吃重力飘坠；命中/贴壁迸溅碎瓣，落空坠回血湖时被湖收走
    /// </summary>
    internal class KikasaEmpressPetal : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>凋萎起始帧：图案完成使命后花瓣开始坠落</summary>
        private const int WiltStart = 104;

        /// <summary>旋进角速度（rad/帧，符号=旋向）</summary>
        private ref float Spin => ref Projectile.ai[0];

        /// <summary>瓣序：扑动相位与虹彩相位的确定性来源</summary>
        private ref float PetalIndex => ref Projectile.ai[1];

        private ref float Life => ref Projectile.localAI[0];

        private bool lakeSwallowed;

        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color PearlBright => KikasaDomain.CoolTint(new(246, 170, 150), new(180, 204, 208));

        /// <summary>出生 5 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 5f, 0f, 1f)
            * MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);

        /// <summary>扑动相位：瓣序错开，同波六瓣不同拍</summary>
        private float FlutterPhase => Life * 0.16f + PetalIndex * 1.17f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 168;
            //鬼物花瓣穿地飘：湖下真地形被湖面演出盖住，撞上去像凭空截停；
            //谢幕统一走 OnKill 碎裂，不再依赖撞地
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            if (Life < WiltStart) {
                //旋进：速度方向逐帧转动，六瓣同转 = 图案整体旋转成玫瑰；
                //复利微加速让花环缓慢扩张，禁匀速
                Projectile.velocity = Projectile.velocity.RotatedBy(Spin) * 1.005f;
                if (Projectile.velocity.Length() > 13f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 13f;
                }
            }
            else {
                //凋萎：旋进泄劲、重力接管，花瓣离开图案飘坠
                Spin *= 0.94f;
                Projectile.velocity = Projectile.velocity.RotatedBy(Spin);
                Projectile.velocity.X *= 0.985f;
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.14f, 7f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行甩珠：瓣尖偶发撕下小血珠，横向微散
            if (!Main.dedServ && Life % 4 == 1) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    Projectile.Center - dir * Main.rand.NextFloat(4f, 10f),
                    Projectile.velocity * 0.2f + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-0.8f, 0.8f),
                    (Main.rand.NextBool(3) ? BloodDeep : BloodMain) * 0.55f,
                    Main.rand.NextFloat(0.26f, 0.42f))?.Configure(Main.rand.Next(12, 20));
            }
            //珠光碎星：极稀疏的虹彩点缀
            if (!Main.dedServ && Main.rand.NextBool(14)) {
                PRTLoader.NewParticle<PRT_Sparkle>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), Vector2.Zero,
                    KikasaEmpressServant.IridescentTint(PetalIndex * 0.13f + Main.rand.NextFloat(0.2f)) * 0.45f,
                    Main.rand.NextFloat(0.2f, 0.35f))?.Configure(PearlBright * 0.4f, 12, 0f, 0.4f);
            }

            float glow = 0.4f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.45f * glow, 0.12f * glow, 0.15f * glow);

            //落空坠回血湖：湖收回自己的花，不迸溅
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.6f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 3);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        //==================== 命中与谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            //命中 NPC / 超时凋落共用（penetrate=1，Kill 各端都跑，队友也看得见）
            PetalBurst(Projectile.Center, Projectile.velocity);
        }

        /// <summary>花瓣碎裂：半球血珠 + 三两片碎瓣打着旋飘落 + 一朵珠光</summary>
        private static void PetalBurst(Vector2 pos, Vector2 impactVel) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            float mainAngle = normal.ToRotation();

            for (int i = 0; i < 7; i++) {
                float spread = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                Vector2 vel = (mainAngle + spread).ToRotationVector2()
                    * Main.rand.NextFloat(1.6f, 5.4f) * (1f - MathF.Abs(spread) / MathHelper.Pi);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos + Main.rand.NextVector2Circular(5f, 5f),
                    vel, Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.34f, 0.6f))?.Configure(Main.rand.Next(16, 28));
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_KikasaEmpressPetal>(pos,
                    normal.RotatedByRandom(0.8f) * Main.rand.NextFloat(0.8f, 2.2f),
                    BloodMain * Main.rand.NextFloat(0.5f, 0.7f),
                    Main.rand.NextFloat(0.3f, 0.48f))
                    ?.Configure(Main.rand.Next(30, 50), Main.rand.NextFloat(0.7f, 1.4f));
            }
            PRTLoader.NewParticle<PRT_Sparkle>(pos, Vector2.Zero,
                KikasaEmpressServant.IridescentTint(Main.rand.NextFloat()) * 0.5f,
                Main.rand.NextFloat(0.3f, 0.45f))?.Configure(PearlBright * 0.5f, 14, 0.02f, 0.6f);

            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.32f, Pitch = 0.1f, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = -0.25f, MaxInstances = 3 }, pos);
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            if (fade <= 0.01f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;
            float rot = Projectile.rotation;

            //翻折扑动：宽度沿 |sin| 收零再张（对称贴图用 abs 防负缩放翻绕序）
            float fold = 0.3f + 0.7f * MathF.Abs(MathF.Sin(FlutterPhase));
            Vector2 dir = (rot - MathHelper.PiOver2).ToRotationVector2();

            //旋进残影：旧位淡瓣读出图案的转动
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos != null) {
                for (int k = 6; k >= 2; k -= 2) {
                    if (oldPos[k] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 ghostPos = oldPos[k] + Projectile.Size * 0.5f - Main.screenPosition;
                    float ghostA = 0.16f * (1f - k / 8f) * fade;
                    sb.Draw(tex, ghostPos, null, BloodDeep * ghostA, Projectile.oldRot[k],
                        origin, new Vector2(0.30f * fold, 0.52f), SpriteEffects.None, 0f);
                }
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //暗血压边
            sb.Draw(tex, pos, null, BloodDark * (0.85f * fade), rot, origin,
                new Vector2(0.38f * fold, 0.62f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos + dir * 8f, null, BloodDark * (0.7f * fade), rot, origin,
                new Vector2(0.22f * fold, 0.34f), SpriteEffects.None, 0f);
            //血红瓣身 + 瓣尖小叶：两团错位拼出泪滴瓣形
            sb.Draw(tex, pos, null, BloodMain * fade, rot, origin,
                new Vector2(0.30f * fold, 0.52f), SpriteEffects.None, 0f);
            sb.Draw(tex, pos + dir * 8f, null, BloodMain * (0.9f * fade), rot, origin,
                new Vector2(0.16f * fold, 0.28f), SpriteEffects.None, 0f);
            //珠光瓣脉：极小面积加色，虹彩随瓣序微移
            Color sheen = KikasaEmpressServant.IridescentTint(PetalIndex * 0.11f) with { A = 0 };
            sb.Draw(tex, pos + dir * 3f, null, sheen * (0.4f * fade * fold), rot, origin,
                new Vector2(0.09f, 0.4f), SpriteEffects.None, 0f);
            Color core = PearlBright with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.32f * fade), rot, origin,
                new Vector2(0.12f * fold, 0.2f), SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>
    /// 花瓣血雨粒子：溶解谢幕与碎裂共用的瓣片。低重力飘坠、横摆滑落、
    /// 沿轴翻面扑动（宽度 |sin| 收零再张），瓣缘沉色、瓣心珠光，尾段凝暗淡出
    /// </summary>
    internal class PRT_KikasaEmpressPetal : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 200;

        private Color initialColor;
        private float swayAmp;
        private float phase;

        public PRT_KikasaEmpressPetal Configure(int lifetime, float sway) {
            Lifetime = lifetime;
            swayAmp = sway;
            initialColor = Color;
            phase = Main.rand.NextFloat(MathHelper.TwoPi);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            swayAmp = 0f;
            phase = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 48;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //低重力飘坠 + 横摆滑落：瓣是片状的，落得慢、摆着落
            Velocity.Y = MathF.Min(Velocity.Y + 0.05f, 2.1f);
            Velocity.X = Velocity.X * 0.98f + MathF.Sin(Time * 0.11f + phase) * 0.05f * swayAmp;

            float t = LifetimeCompletion;
            Color = Color.Lerp(initialColor, KikasaEyeBloodShot.BloodDark, MathF.Pow(t, 1.7f) * 0.7f);
            Opacity = MathHelper.Clamp(Time / 5f, 0f, 1f) * (1f - MathF.Pow(t, 2.6f));
            //瓣身朝向随横摆轻转
            Rotation = MathF.Sin(Time * 0.08f + phase) * 0.6f + Velocity.X * 0.12f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;

            //翻面扑动：对称贴图用 abs 收零，不给负 scale
            float fold = 0.24f + 0.76f * MathF.Abs(MathF.Sin(Time * 0.13f + phase * 1.3f));
            Vector2 scale = new Vector2(0.30f * fold, 0.5f) * Scale;

            Color body = Color * Opacity;
            Color rim = Color.Lerp(Color, KikasaEyeBloodShot.BloodDark, 0.55f) * Opacity;
            spriteBatch.Draw(tex, pos, null, rim, Rotation, origin, scale * new Vector2(1.3f, 1.1f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body, Rotation, origin, scale, SpriteEffects.None, 0f);

            //新鲜期瓣心珠光
            float fresh = 1f - MathHelper.Clamp(LifetimeCompletion * 2f, 0f, 1f);
            if (fresh > 0.05f) {
                Color glint = KikasaEmpressServant.IridescentTint(phase * 0.16f) with { A = 0 };
                spriteBatch.Draw(tex, pos, null, glint * (0.34f * fresh * Opacity * fold), Rotation, origin,
                    scale * new Vector2(0.3f, 0.6f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
