using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>替死娃娃专属资源（域内加载器，不动 EffectLoader）</summary>
    internal class FishVoodooAssets
    {
        /// <summary>程序化麻布娃娃，SDF 剪影 + 织纹 + 蛇形绕线显形 + 焚毁溶解</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishVoodooDoll { get; private set; }

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> Blob = null;
        [VaultLoaden(CWRConstant.Masking + "Ring01")]
        internal static Asset<Texture2D> Ring = null;
        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> Glow = null;
    }

    /// <summary>替死娃娃共享调色与小件绘制</summary>
    internal static class FishVoodooArt
    {
        public static readonly Color ClothLight = new(140, 115, 82);
        public static readonly Color ClothDark = new(66, 52, 38);
        public static readonly Color ThreadCrimson = new(150, 32, 42);
        public static readonly Color ThreadDark = new(62, 14, 20);
        public static readonly Color EmberDim = new(150, 62, 28);
        public static readonly Color EmberHot = new(232, 152, 64);
        public static readonly Color CharBlack = new(30, 22, 20);
        public static readonly Color NeedleSteel = new(202, 202, 212);

        public static Texture2D Pixel => VaultAsset.placeholder2.Value;

        public static Color Shade(Color c, float m) => new((int)(c.R * m), (int)(c.G * m), (int)(c.B * m), c.A);

        /// <summary>世界坐标细线段，1x1 白像素拉伸</summary>
        public static void DrawLine(SpriteBatch sb, Vector2 from, Vector2 to, Color color, float thick) {
            Vector2 d = to - from;
            float len = d.Length();
            if (len < 0.5f) {
                return;
            }
            sb.Draw(Pixel, from - Main.screenPosition, null, color, d.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len, thick), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// sprite 拼合小布偶（Mark 吊坠与 shader 缺失回退共用）
        /// </summary>
        public static void DrawEffigy(SpriteBatch sb, Vector2 center, float scale, float rot, float alpha, float lightMul = 1f, bool needle = true) {
            Texture2D blob = FishVoodooAssets.Blob?.Value;
            if (blob == null) {
                return;
            }
            Vector2 pos = center - Main.screenPosition;
            Vector2 o = blob.Size() / 2f;
            Color cloth = Shade(ClothLight, lightMul);
            Color clothDk = Shade(ClothDark, lightMul);
            Vector2 R(Vector2 v) => v.RotatedBy(rot) * scale;

            //横臂在躯干之下(夹心)
            DrawLine(sb, center + R(new Vector2(-13f, -4f)), center + R(new Vector2(13f, -4f)), Shade(cloth, 0.78f) * alpha, 3.6f * scale);
            //双腿
            DrawLine(sb, center + R(new Vector2(-4f, 9f)), center + R(new Vector2(-5f, 19f)), Shade(cloth, 0.72f) * alpha, 3.2f * scale);
            DrawLine(sb, center + R(new Vector2(4f, 9f)), center + R(new Vector2(5f, 19f)), Shade(cloth, 0.72f) * alpha, 3.2f * scale);
            //躯干 + 头
            sb.Draw(blob, pos + R(new Vector2(0f, 2f)), null, Shade(cloth, 0.92f) * alpha, rot, o, new Vector2(0.34f, 0.46f) * scale, SpriteEffects.None, 0f);
            sb.Draw(blob, pos + R(new Vector2(0f, -14f)), null, cloth * alpha, rot, o, new Vector2(0.25f, 0.24f) * scale, SpriteEffects.None, 0f);
            //中缝针脚
            for (int i = 0; i < 3; i++) {
                Vector2 a = center + R(new Vector2(0f, -4f + i * 5f));
                Vector2 b = center + R(new Vector2(0f, -1.4f + i * 5f));
                DrawLine(sb, a, b, ThreadCrimson * (0.9f * alpha), 1.3f * scale);
            }
            //X 眼:两颗暗点
            sb.Draw(Pixel, pos + R(new Vector2(-3.2f, -15f)), null, Shade(ThreadDark, lightMul) * alpha, rot, new Vector2(0.5f), 2f * scale, SpriteEffects.None, 0f);
            sb.Draw(Pixel, pos + R(new Vector2(3.2f, -15f)), null, Shade(ThreadDark, lightMul) * alpha, rot, new Vector2(0.5f), 2f * scale, SpriteEffects.None, 0f);
            //斜插钢针 + 针尾线结
            if (needle) {
                Vector2 nFrom = center + R(new Vector2(-10f, -10f));
                Vector2 nTo = center + R(new Vector2(7f, 3f));
                DrawLine(sb, nFrom, nTo, NeedleSteel * (0.9f * alpha), 1.4f * scale);
                sb.Draw(Pixel, nFrom - Main.screenPosition, null, ThreadCrimson * alpha, rot, new Vector2(0.5f), 2.2f * scale, SpriteEffects.None, 0f);
            }
            //底缘两缕散线头(禁平滑收口)
            DrawLine(sb, center + R(new Vector2(-3f, 12f)), center + R(new Vector2(-5f, 16f)), Shade(clothDk, 1.1f) * (0.8f * alpha), 1f * scale);
            DrawLine(sb, center + R(new Vector2(2f, 12f)), center + R(new Vector2(3f, 15f)), ThreadDark * (0.8f * alpha), 1f * scale);
        }
    }

    /// <summary>
    /// 替死仪式娃娃（纯视觉，伤害转移在 <see cref="FishVoodooPlayer"/> 即时结算）<br/>
    /// ai[0]=0 触发演出，绕线显形 → 持线等待 → 钢针刺入(≤2 帧白闪) → 自燃成灰<br/>
    /// ai[0]=1 冷却结束重织，双股丝线螺旋收拢 + 蛇形绕线显形 → 就绪音 → 消散
    /// </summary>
    internal class FishVoodooRitual : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int RevealTime = 9;       //mode0 显形帧数
        private const int PierceWaitMax = 34;   //最迟针刺帧
        private const int PlungeTime = 3;       //针下插帧数
        private const int BurnTime = 32;
        private const int FadeTime = 6;
        private const int RwRevealTime = 28;    //mode1 重织显形
        private const int RwHoldEnd = 38;
        private const int RwEnd = 46;
        private static readonly Vector2 DollSize = new(52f, 70f);

        private int timer;
        private int pierceStart = -1;
        private float burnAmt;
        private float reveal;
        private bool ignitePlayed;
        private float needleFallVel;
        private Vector2 needleFallOff;
        private Vector2[] motePos;
        private float[] moteSeed;

        private Player Owner => Main.player[Projectile.owner];
        private bool Reweave => Projectile.ai[0] == 1f;
        private int BurnStart => pierceStart < 0 ? int.MaxValue : pierceStart + PlungeTime + 2;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.timeLeft = 240;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
            Projectile.hostile = false;
        }

        private Vector2 DollAnchor() {
            float bob = MathF.Sin(timer * 0.075f) * 2.2f;
            return Owner.Center + new Vector2(-Owner.direction * 16f, -54f + bob);
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }
            timer++;
            Projectile.Center = DollAnchor();

            if (Reweave) {
                ReweaveAI();
                return;
            }

            //====mode0 触发演出====
            reveal = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(timer / (float)RevealTime, 0f, 1f));

            //凝滞悬尘:首帧捕获世界坐标,近乎静止地悬着
            if (timer == 1) {
                motePos = new Vector2[7];
                moteSeed = new float[7];
                for (int i = 0; i < motePos.Length; i++) {
                    motePos[i] = Owner.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(26f, 62f);
                    moteSeed[i] = Main.rand.NextFloat(MathHelper.TwoPi);
                }
            }
            if (motePos != null) {
                for (int i = 0; i < motePos.Length; i++) {
                    motePos[i] = Vector2.Lerp(motePos[i], Projectile.Center, 0.012f);
                }
            }

            //针刺节拍:所有缝线到位(或超时)后钢针刺入
            if (pierceStart < 0 && timer >= RevealTime + 3 && (!AnyThreadFlying() || timer >= PierceWaitMax)) {
                pierceStart = timer;
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.15f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.3f, Pitch = 0.5f }, Projectile.Center);
                if (!VaultUtils.isServer) {
                    Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center, Vector2.UnitY, 2f, 5f, 6, 700f, "FishVoodooPierce"));
                }
            }

            //自燃:燃线自下而上,灰烬沿燃缘升腾
            if (timer >= BurnStart) {
                if (!ignitePlayed) {
                    ignitePlayed = true;
                    SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.42f, Pitch = -0.2f }, Projectile.Center);
                }
                float t = MathHelper.Clamp((timer - BurnStart) / (float)BurnTime, 0f, 1f);
                burnAmt = t * t; //easeIn:起燃慢,吞噬渐快
                SpawnBurnAsh();
                float flick = 0.75f + 0.25f * MathF.Sin(timer * 0.9f);
                Lighting.AddLight(BurnFrontPos(), 0.24f * flick, 0.11f * flick, 0.04f * flick);

                //针随布身烧散而坠落
                if (burnAmt > 0.72f) {
                    needleFallVel += 0.24f;
                    needleFallOff.Y += needleFallVel;
                }
            }

            //焚尽 -> 终末灰扬 -> 消隐
            if (burnAmt >= 1f && timer >= BurnStart + BurnTime + FadeTime) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_FishVoodooAsh>(BurnFrontPos() + Main.rand.NextVector2Circular(10f, 6f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1f, -0.3f)), Color.White, 1f)?.Configure(Main.rand.Next(45, 70), 0.6f);
                }
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_FishVoodooFiber>(Projectile.Center + Main.rand.NextVector2Circular(8f, 10f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-0.4f, 0.2f)), Color.White, 1f)?.Configure(Main.rand.Next(26, 38));
                }
                Projectile.Kill();
            }
        }

        private void ReweaveAI() {
            reveal = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(timer / (float)RwRevealTime, 0f, 1f));
            if (timer == RwRevealTime) {
                //织毕:就绪音 + 线头轻弹
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.22f, Pitch = 0.8f }, Projectile.Center);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_FishVoodooFiber>(Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), 10f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.2f, -0.6f)), Color.White, 0.8f)?.Configure(24, true);
                }
            }
            if (timer >= RwEnd) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_FishVoodooFiber>(Projectile.Center + Main.rand.NextVector2Circular(8f, 12f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0f, 0.5f)), Color.White, 1f)?.Configure(Main.rand.Next(24, 34));
                }
                Projectile.Kill();
            }
        }

        private bool AnyThreadFlying() {
            int type = ModContent.ProjectileType<FishVoodooThread>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == type
                    && p.ModProjectile is FishVoodooThread t && t.IsFlying) {
                    return true;
                }
            }
            return false;
        }

        private Vector2 BurnFrontPos() {
            float frontY = Projectile.Center.Y + DollSize.Y * 0.5f - burnAmt * DollSize.Y;
            return new Vector2(Projectile.Center.X + Main.rand.NextFloat(-14f, 14f), frontY);
        }

        private void SpawnBurnAsh() {
            if (burnAmt >= 1f) {
                return;
            }
            int count = timer % 2 == 0 ? 2 : 1;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_FishVoodooAsh>(BurnFrontPos(),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.8f, -0.3f)),
                    Color.White, Main.rand.NextFloat(0.8f, 1.15f))?.Configure(Main.rand.Next(45, 70), 1f);
            }
        }

        private float DollRotation() {
            float sway = MathF.Sin(timer * 0.055f) * 0.045f;
            if (!Reweave && burnAmt > 0f && burnAmt < 1f) {
                sway += MathF.Sin(timer * 1.7f) * 0.012f; //焚身微颤
            }
            return sway;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 dollPos = Projectile.Center;
            float rot = DollRotation();
            float alpha = Reweave ? 0.85f : 1f;
            Color light = Lighting.GetColor(dollPos.ToTileCoordinates());
            float lightMul = 0.45f + 0.55f * (light.R + light.G + light.B) / 765f;

            if (!Reweave) {
                DrawStasisDressing(sb);
            }
            else {
                DrawReweaveStrands(sb, dollPos);
            }

            DrawDoll(sb, dollPos, rot, alpha, lightMul);
            DrawNeedle(sb, dollPos, rot, alpha);
            return false;
        }

        /// <summary>mode0 开场:暗色诅咒环 + 凝滞悬尘(均为衬托层,亮度克制)</summary>
        private void DrawStasisDressing(SpriteBatch sb) {
            //诅咒环:哑光暗红环扩张消隐,非加色光盘
            if (timer <= 16 && FishVoodooAssets.Ring?.Value != null) {
                Texture2D ring = FishVoodooAssets.Ring.Value;
                float t = timer / 16f;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                float radius = 26f + 86f * ease;
                float a = (1f - t) * 0.55f;
                Vector2 pos = Owner.Center - Main.screenPosition;
                Color dark = new Color(34, 10, 13);
                sb.Draw(ring, pos, null, dark * a, timer * 0.02f, ring.Size() / 2f, radius * 2f / ring.Width, SpriteEffects.None, 0f);
                //内缘一圈极淡暗红加色描边
                sb.Draw(ring, pos, null, new Color(120, 26, 34, 0) * (a * 0.35f), timer * 0.02f, ring.Size() / 2f, radius * 1.86f / ring.Width, SpriteEffects.None, 0f);
            }
            //悬尘:近乎静止的小暗红点,呼吸闪烁
            if (motePos != null && timer <= 30 && FishVoodooAssets.Glow?.Value != null) {
                Texture2D glow = FishVoodooAssets.Glow.Value;
                float fade = timer > 22 ? 1f - (timer - 22) / 8f : MathHelper.Clamp(timer / 4f, 0f, 1f);
                for (int i = 0; i < motePos.Length; i++) {
                    float flick = 0.6f + 0.4f * MathF.Sin(timer * 0.33f + moteSeed[i]);
                    Vector2 pos = motePos[i] - Main.screenPosition;
                    sb.Draw(glow, pos, null, new Color(140, 40, 46, 0) * (0.5f * fade * flick), 0f, glow.Size() / 2f, 0.055f, SpriteEffects.None, 0f);
                    sb.Draw(glow, pos, null, new Color(200, 90, 80, 0) * (0.35f * fade * flick), 0f, glow.Size() / 2f, 0.025f, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>mode1 重织:双股丝线螺旋收拢,虚线针脚段落画法</summary>
        private void DrawReweaveStrands(SpriteBatch sb, Vector2 dollPos) {
            if (timer > RwRevealTime + 4) {
                return;
            }
            float t = MathHelper.Clamp(timer / (float)RwRevealTime, 0f, 1f);
            float radius = MathHelper.Lerp(46f, 9f, t);
            float fade = timer > RwRevealTime ? 1f - (timer - RwRevealTime) / 4f : 1f;
            for (int s = 0; s < 2; s++) {
                float baseAng = timer * 0.24f + s * MathHelper.Pi;
                for (int k = 0; k < 5; k++) {
                    float ang = baseAng - k * 0.22f;
                    float r = radius + k * 2.2f;
                    Vector2 a = dollPos + ang.ToRotationVector2() * r;
                    Vector2 b = dollPos + (ang - 0.13f).ToRotationVector2() * (r + 0.8f);
                    float segA = fade * (1f - k * 0.16f);
                    FishVoodooArt.DrawLine(sb, a, b, FishVoodooArt.ThreadDark * (0.8f * segA), 2.6f);
                    FishVoodooArt.DrawLine(sb, a, b, FishVoodooArt.ThreadCrimson * segA, 1.3f);
                }
            }
        }

        private void DrawDoll(SpriteBatch sb, Vector2 dollPos, float rot, float alpha, float lightMul) {
            //重织期整体缩入退场
            Vector2 scaleMul = Vector2.One;
            if (Reweave && timer > RwHoldEnd) {
                float t = (timer - RwHoldEnd) / (float)(RwEnd - RwHoldEnd);
                alpha *= 1f - t;
                scaleMul *= 1f - t * 0.08f;
            }
            //针刺入的 2 帧布身受压微扁(冲击回馈)
            if (!Reweave && pierceStart > 0) {
                int hitFrame = timer - (pierceStart + PlungeTime);
                if (hitFrame >= 0 && hitFrame < 3) {
                    scaleMul *= new Vector2(1.03f, 0.94f);
                }
            }
            Effect fx = FishVoodooAssets.FishVoodooDoll;
            if (fx != null) {
                Vector3 clothL = FishVoodooArt.ClothLight.ToVector3() * lightMul;
                Vector3 clothD = FishVoodooArt.ClothDark.ToVector3() * lightMul;
                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uAlpha"]?.SetValue(alpha);
                fx.Parameters["uReveal"]?.SetValue(reveal);
                fx.Parameters["uBurn"]?.SetValue(burnAmt);
                fx.Parameters["uSize"]?.SetValue(DollSize);
                fx.Parameters["uColCloth"]?.SetValue(clothL);
                fx.Parameters["uColClothDark"]?.SetValue(clothD);
                fx.Parameters["uColThread"]?.SetValue(FishVoodooArt.ThreadCrimson.ToVector3());
                fx.Parameters["uColChar"]?.SetValue(FishVoodooArt.CharBlack.ToVector3());
                fx.Parameters["uColEmberDim"]?.SetValue(FishVoodooArt.EmberDim.ToVector3());
                fx.Parameters["uColEmberHot"]?.SetValue(FishVoodooArt.EmberHot.ToVector3());

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp
                    , DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

                sb.Draw(FishVoodooArt.Pixel, dollPos - Main.screenPosition, null, Color.White, rot
                    , new Vector2(0.5f, 0.5f), DollSize * scaleMul, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else {
                //CPU 回退:sprite 拼合布偶,显形/焚毁用整体透明近似,缺 .fxc 不至于黑块
                float cpuAlpha = alpha * reveal * (1f - burnAmt);
                FishVoodooArt.DrawEffigy(sb, dollPos, 1.6f * scaleMul.Y, rot, cpuAlpha, lightMul, needle: false);
            }
        }

        /// <summary>钢针:刺入前悬于娃娃上方,3 帧下插,插入瞬间 ≤2 帧白色闪光,焚身后坠落</summary>
        private void DrawNeedle(SpriteBatch sb, Vector2 dollPos, float rot, float alpha) {
            if (Reweave || pierceStart < 0) {
                return;
            }
            float plunge = MathHelper.Clamp((timer - pierceStart) / (float)PlungeTime, 0f, 1f);
            plunge = plunge * plunge; //ease-in 加速下插
            if (burnAmt >= 1f) {
                return;
            }
            float needleAlpha = alpha * (1f - MathHelper.Clamp((burnAmt - 0.8f) / 0.2f, 0f, 1f));
            //起针 2 帧渐显,禁 pop-in
            needleAlpha *= MathHelper.Clamp((timer - pierceStart + 1) / 2f, 0f, 1f);
            Vector2 restOff = new(-2f, -6f);
            Vector2 startOff = new(-8f, -40f);
            Vector2 off = Vector2.Lerp(startOff, restOff, plunge) + needleFallOff;
            float needleRot = rot + MathHelper.Lerp(0.9f, 0.62f, plunge) + needleFallVel * 0.06f;
            Vector2 tip = dollPos + off;
            Vector2 tail = tip - needleRot.ToRotationVector2() * 24f;
            FishVoodooArt.DrawLine(sb, tail, tip, FishVoodooArt.NeedleSteel * (0.92f * needleAlpha), 1.6f);
            //针尾小线结
            sb.Draw(FishVoodooArt.Pixel, tail - Main.screenPosition, null, FishVoodooArt.ThreadCrimson * needleAlpha, needleRot, new Vector2(0.5f), 2.6f, SpriteEffects.None, 0f);

            //刺入闪光:白色只留 2 帧
            int glintFrame = timer - (pierceStart + PlungeTime);
            if (glintFrame >= 0 && glintFrame < 2 && FishVoodooAssets.Glow?.Value != null) {
                Texture2D glow = FishVoodooAssets.Glow.Value;
                Vector2 pos = tip - Main.screenPosition;
                float g = 1f - glintFrame * 0.45f;
                sb.Draw(glow, pos, null, new Color(255, 240, 230, 0) * (0.85f * g), 0f, glow.Size() / 2f, 0.16f * g, SpriteEffects.None, 0f);
                FishVoodooArt.DrawLine(sb, tip - new Vector2(9f * g, 0f), tip + new Vector2(9f * g, 0f), new Color(255, 250, 245, 0) * (0.8f * g), 1.2f);
                FishVoodooArt.DrawLine(sb, tip - new Vector2(0f, 9f * g), tip + new Vector2(0f, 9f * g), new Color(255, 250, 245, 0) * (0.8f * g), 1.2f);
            }
        }
    }

    /// <summary>
    /// 灵魂缝线，针步节奏缝向目标（2 帧疾送 + 2 帧顿针，方向只在针步起点折转）
    /// 留下针脚虚线轨迹；命中针刺定帧，随后自根部燃蚀、灰烬升腾<br/>
    /// ai[0]=目标 npc，ai[1]=起针延迟帧
    /// </summary>
    internal class FishVoodooThread : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const float DashLen = 7f;
        private const float GapLen = 5f;
        private const int DartCycle = 4;
        private const int MaxFlight = 30;
        private const float ErodeBand = 14f;

        private readonly List<Vector2> pts = new();
        private Vector2 dir;
        private float dartSpeed;
        private int flightTime;
        private int state; //0 待针 1 缝进 2 燃蚀
        private int erodeDelay;
        private float erodedLen;
        private float totalLen;
        private float zigSign = 1f;

        public bool IsFlying => state <= 1;

        private NPC TargetNPC {
            get {
                int id = (int)Projectile.ai[0];
                if (id < 0 || id >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[id];
                return npc.active ? npc : null;
            }
        }

        public override void SetStaticDefaults() {
            //缝线横跨大半屏,头部离屏时轨迹仍需绘制
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;
        }

        public override void SetDefaults() {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.timeLeft = 180;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
            Projectile.hostile = false;
        }

        public override void AI() {
            if (state == 0) {
                if (pts.Count == 0) {
                    pts.Add(Projectile.Center);
                    //起针方向:偏离直连线一个随机侧角,针步中逐渐收束成弧
                    NPC t = TargetNPC;
                    Vector2 toT = t != null ? t.Center - Projectile.Center : Vector2.UnitX;
                    dir = toT.SafeNormalize(Vector2.UnitX).RotatedBy(Main.rand.NextFloat(0.45f, 0.85f) * (Main.rand.NextBool() ? 1f : -1f));
                }
                if (Projectile.ai[1] > 0f) {
                    Projectile.ai[1]--;
                    return;
                }
                state = 1;
            }

            if (state == 1) {
                FlightStep();
                return;
            }

            //====燃蚀:自根部(玩家端)向目标端吞噬针脚====
            if (erodeDelay > 0) {
                erodeDelay--;
                return;
            }
            erodedLen += totalLen / 22f;
            if (flightTime % 3 == 0 && erodedLen < totalLen) {
                Vector2 front = PointAt(erodedLen);
                PRTLoader.NewParticle<PRT_FishVoodooAsh>(front, new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.7f, -0.3f))
                    , Color.White, Main.rand.NextFloat(0.6f, 0.9f))?.Configure(Main.rand.Next(40, 60), 0.9f);
            }
            flightTime++;
            if (erodedLen >= totalLen + 24f) {
                Projectile.Kill();
            }
        }

        private void FlightStep() {
            flightTime++;
            NPC t = TargetNPC;
            Vector2 targetPos = t?.Center ?? (pts[^1] + dir * 60f);

            int phase = flightTime % DartCycle;
            if (phase == 0 || flightTime == 1) {
                //针步起点:折转向目标收束,垂直向交替偏角形成针脚折线
                Vector2 toT = targetPos - Projectile.Center;
                float dist = MathF.Max(toT.Length(), 1f);
                Vector2 dirToT = toT / dist;
                float converge = MathHelper.Clamp(0.4f + flightTime / (float)MaxFlight * 0.55f, 0f, 0.95f);
                dir = Vector2.Lerp(dir, dirToT, converge).SafeNormalize(dirToT);
                zigSign = -zigSign;
                dir = dir.RotatedBy(zigSign * 0.2f);
                dartSpeed = MathHelper.Clamp(dist / 12f, 18f, 58f);
            }
            //疾送-顿针速度包络:2 帧冲,2 帧几乎停(针在布里)
            float sp = phase switch {
                0 => dartSpeed,
                1 => dartSpeed * 0.5f,
                _ => dartSpeed * 0.08f
            };
            Projectile.Center += dir * sp;
            pts.Add(Projectile.Center);

            bool reached = t != null && Projectile.Center.Distance(t.Center) < 30f;
            if (reached || flightTime >= MaxFlight || t == null) {
                Arrive(t);
            }
        }

        private void Arrive(NPC t) {
            state = 2;
            erodeDelay = 8;
            //超时逼近:补一段收针,针脚在目标身上收口而非悬空断线
            if (t != null && Projectile.Center.Distance(t.Center) > 30f) {
                pts.Add(Vector2.Lerp(Projectile.Center, t.Center, 0.55f));
                pts.Add(t.Center);
                Projectile.Center = t.Center;
            }
            totalLen = 0f;
            for (int i = 1; i < pts.Count; i++) {
                totalLen += Vector2.Distance(pts[i - 1], pts[i]);
            }
            if (t != null) {
                TimeFreezeSystem.RefreshNPC<FishVoodooRitual>(t, 4); //针刺定帧
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.6f, MaxInstances = 3 }, t.Center);
                //刺点断纤+一片余温灰
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_FishVoodooFiber>(Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-1f, -0.2f)), Color.White, 0.9f)?.Configure(Main.rand.Next(22, 32), true);
                }
                PRTLoader.NewParticle<PRT_FishVoodooAsh>(Projectile.Center, new Vector2(0f, -0.5f), Color.White, 0.7f)?.Configure(40, 0.8f);
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), t.Center, Vector2.Zero
                        , ModContent.ProjectileType<FishVoodooMark>(), 0, 0f, Projectile.owner, t.whoAmI);
                }
            }
        }

        /// <summary>沿已缝折线取弧长 dist 处的点</summary>
        private Vector2 PointAt(float dist) {
            float acc = 0f;
            for (int i = 1; i < pts.Count; i++) {
                float seg = Vector2.Distance(pts[i - 1], pts[i]);
                if (acc + seg >= dist && seg > 0.01f) {
                    return Vector2.Lerp(pts[i - 1], pts[i], (dist - acc) / seg);
                }
                acc += seg;
            }
            return pts.Count > 0 ? pts[^1] : Projectile.Center;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (pts.Count < 2) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;

            //====根部引线:从娃娃(随玩家移动)放线到首个针脚,燃蚀开始后先行退隐====
            Player owner = Main.player[Projectile.owner];
            if (owner.active && erodedLen < 8f) {
                Vector2 doll = owner.Center + new Vector2(-owner.direction * 16f, -54f);
                float rootFade = state == 1 ? 1f : erodeDelay / 8f;
                if (rootFade > 0.03f) {
                    Vector2 mid = Vector2.Lerp(doll, pts[0], 0.5f) + new Vector2(0f, 3f); //自重微垂
                    FishVoodooArt.DrawLine(sb, doll, mid, FishVoodooArt.ThreadDark * (0.7f * rootFade), 2.4f);
                    FishVoodooArt.DrawLine(sb, mid, pts[0], FishVoodooArt.ThreadDark * (0.7f * rootFade), 2.4f);
                    FishVoodooArt.DrawLine(sb, doll, mid, FishVoodooArt.ThreadCrimson * (0.8f * rootFade), 1.2f);
                    FishVoodooArt.DrawLine(sb, mid, pts[0], FishVoodooArt.ThreadCrimson * (0.8f * rootFade), 1.2f);
                }
            }

            //====针脚虚线:沿折线按 dash+gap 步进,交替上下微偏(over-under 织感)====
            float laid = 0f;
            for (int i = 1; i < pts.Count; i++) {
                laid += Vector2.Distance(pts[i - 1], pts[i]);
            }
            float step = DashLen + GapLen;
            int dashCount = (int)(laid / step) + 1;
            for (int d = 0; d < dashCount; d++) {
                float s0 = d * step;
                float s1 = MathF.Min(s0 + DashLen, laid);
                if (s1 - s0 < 1f || s1 <= erodedLen) {
                    continue;
                }
                Vector2 a = PointAt(MathF.Max(s0, erodedLen));
                Vector2 b = PointAt(s1);
                Vector2 seg = b - a;
                if (seg.LengthSquared() < 0.25f) {
                    continue;
                }
                Vector2 perp = seg.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * ((d % 2 == 0) ? 1.6f : -1.6f);
                a += perp;
                b += perp;

                //最新缝出的 2 段渐显,起针即隐现
                float headFade = MathHelper.Clamp((laid - s0) / (step * 2f), 0.25f, 1f);
                //燃蚀前沿:一小段针脚烧作余烬色,闪两帧后被吞
                float emberT = state == 2 ? MathHelper.Clamp(1f - (s0 - erodedLen) / ErodeBand, 0f, 1f) : 0f;
                Color outline = FishVoodooArt.ThreadDark;
                Color core = FishVoodooArt.ThreadCrimson;
                if (emberT > 0f) {
                    float flick = 0.7f + 0.3f * MathF.Sin(flightTime * 1.3f + d);
                    core = Color.Lerp(core, FishVoodooArt.EmberHot, emberT * flick);
                    outline = Color.Lerp(outline, FishVoodooArt.EmberDim, emberT);
                }
                FishVoodooArt.DrawLine(sb, a, b, outline * (0.78f * headFade), 3.1f);
                FishVoodooArt.DrawLine(sb, a, b, core * (0.95f * headFade), 1.5f);
            }

            //====针头:飞行期一枚顺向钢针细芒,顿针相位收暗====
            if (state == 1) {
                int phase = flightTime % DartCycle;
                float dim = phase <= 1 ? 1f : 0.55f;
                Vector2 head = pts[^1];
                Vector2 tail = head - dir * 15f;
                FishVoodooArt.DrawLine(sb, tail, head, FishVoodooArt.NeedleSteel * (0.9f * dim), 1.7f);
                //针尖一点暗红引线辉,唯一加色元素,极小
                Texture2D glow = FishVoodooAssets.Glow?.Value;
                if (glow != null) {
                    sb.Draw(glow, head - Main.screenPosition, null, new Color(190, 60, 60, 0) * (0.6f * dim)
                        , 0f, glow.Size() / 2f, 0.07f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
