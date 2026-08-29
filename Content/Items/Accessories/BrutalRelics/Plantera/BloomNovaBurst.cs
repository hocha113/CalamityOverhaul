using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Plantera
{
    /// <summary>
    /// 绽放新星：低血救急的演出与清场判定。
    /// 四相：花苞鼓胀(起手)→炸开花瓣冲击波(爆发)→孢子雾遮蔽(余韵)→消散(收尾)。
    /// 位置每帧钉在主人中心，各端从同步的玩家位置自算；伤害只在爆发窗按扩张环带判定，
    /// 每个敌人只吃一次。回血/再生已在 ModPlayer 触发时 owner 端结算，本弹幕零治疗逻辑。
    /// ai[0]=出手时生长值(演出规模微调)
    /// </summary>
    internal class BloomNovaBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>花瓣冲击波基数(生成时吃 Generic 加成，临爆前 owner 逐帧校准)</summary>
        public const int WaveDamage = 1200;
        /// <summary>花苞鼓胀(帧)</summary>
        public const int SwellTime = 26;
        /// <summary>冲击波扩张(帧)</summary>
        public const int BurstTime = 30;
        /// <summary>孢子雾滞留(帧)</summary>
        public const int FogHoldTime = 360;
        /// <summary>消散(帧)</summary>
        public const int DecayTime = 26;
        private const int TotalTime = SwellTime + BurstTime + FogHoldTime + DecayTime;
        /// <summary>冲击波最大半径(世界px)，与屏幕花环演出同源</summary>
        public const float WaveMaxRadius = 860f;
        /// <summary>孢子雾半径(世界px)</summary>
        private const float FogRadius = 250f;

        private float Growth => Projectile.ai[0];
        private int Age => TotalTime - Projectile.timeLeft;

        /// <summary>爆发演出闩：只前进不重放(快照回拨/迟到端都不补演)</summary>
        private bool burstFxDone;
        private float seed;

        private Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalTime;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>只有冲击波窗口有伤害</summary>
        public override bool? CanDamage() {
            int age = Age;
            return age >= SwellTime && age < SwellTime + BurstTime ? null : false;
        }

        /// <summary>扩张环带判定：波前扫过即命中(带宽>单帧波速，不漏帧)</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            int age = Age;
            if (age < SwellTime || age >= SwellTime + BurstTime) {
                return false;
            }
            float burstT = (age - SwellTime) / (float)BurstTime;
            float waveR = WaveMaxRadius * VaultUtils.EaseOutCubic(burstT);
            Vector2 closest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            float dist = Vector2.Distance(Projectile.Center, closest);
            return dist <= waveR + 30f && dist >= waveR - 150f;
        }

        public override void AI() {
            if (seed == 0f) {
                seed = 0.19f + Projectile.identity * 0.061f % 0.7f;
            }
            if (!Owner.active) {
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.Kill();
                }
                return;
            }

            //钉在主人身上：雾随人走，救急演出不离身
            Projectile.Center = Owner.Center;
            Projectile.velocity = Vector2.Zero;

            int age = Age;
            //临爆前 owner 逐帧按面板校准冲击波伤害(鼓胀期吃到的增益不落空)
            if (Projectile.owner == Main.myPlayer && age < SwellTime + BurstTime) {
                Projectile.damage = (int)Owner.GetTotalDamage(DamageClass.Generic).ApplyTo(WaveDamage);
            }
            if (age < SwellTime) {
                UpdateSwell(age);
            }
            else if (!burstFxDone && age <= SwellTime + BurstTime) {
                burstFxDone = true;
                DoBurstPresentation();
            }
            else if (age < SwellTime + BurstTime + FogHoldTime) {
                UpdateFog(age);
            }
        }

        /// <summary>起手：花苞急速鼓胀，荧光尘向心汇聚，蓄势声阶梯上行</summary>
        private void UpdateSwell(int age) {
            float swellT = age / (float)SwellTime;
            Lighting.AddLight(Projectile.Center, PlanteraRenderHelper.GlowGreen.ToVector3() * (0.5f + swellT * 0.9f));

            if (VaultUtils.isServer) {
                return;
            }
            if (age == 1) {
                SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.4f, Volume = 0.9f }, Projectile.Center);
            }
            if (age % 7 == 0) {
                SoundEngine.PlaySound(SoundID.Grass with {
                    Pitch = -0.5f + swellT * 0.9f,
                    Volume = 0.55f + swellT * 0.35f,
                    MaxInstances = 5
                }, Projectile.Center);
            }
            //向心汇聚的荧光孢子
            if (Main.rand.NextBool(2)) {
                Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2CircularEdge(150f, 140f) * Main.rand.NextFloat(0.75f, 1.25f);
                PRTLoader.NewParticle<PRT_PlanteraSporeMote>(spawnPos,
                    (Projectile.Center - spawnPos) * 0.06f,
                    PlanteraRenderHelper.GlowGreen, Main.rand.NextFloat(0.9f, 1.6f))
                    ?.Converge(Projectile.Center).SetLife(40);
            }
        }

        /// <summary>爆发拍：花瓣风暴+屏幕花环+闪光+震屏+三层声。闩锁保证只演一次，迟到端不补演</summary>
        private void DoBurstPresentation() {
            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = 0.25f, Volume = 0.95f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.6f, Volume = 0.55f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.4f, Volume = 1f }, Projectile.Center);

            //花瓣风暴+孢子拍
            PlanteraRenderHelper.SpawnPetalBurst(Projectile.Center, 42 + (int)(Growth * 14f), 13f, false);
            PlanteraRenderHelper.SpawnSporePuff(Projectile.Center, 2.2f);
            for (int i = 0; i < 26; i++) {
                float angle = MathHelper.TwoPi * i / 26f;
                PRTLoader.NewParticle<PRT_PlanteraSporeMote>(Projectile.Center,
                    angle.ToRotationVector2() * Main.rand.NextFloat(6f, 15f),
                    PlanteraRenderHelper.GlowGreen, Main.rand.NextFloat(0.9f, 1.7f))?.SetLife(Main.rand.Next(30, 55));
            }

            //屏幕花环三连+爆心闪光(权重1.83渲染层消费)
            BloomNovaFX.PushRing(Projectile.Center, WaveMaxRadius, BurstTime + 6, 0, seed);
            BloomNovaFX.PushRing(Projectile.Center, WaveMaxRadius * 0.72f, BurstTime + 2, 5, seed + 0.31f);
            BloomNovaFX.PushRing(Projectile.Center, WaveMaxRadius * 0.45f, BurstTime, 10, seed + 0.57f);
            BloomNovaFX.PushFlash(Projectile.Center, 1f);

            //震屏只给爆心附近的本地玩家(距离衰减)
            float dist = Main.LocalPlayer.Distance(Projectile.Center);
            if (dist < 1500f) {
                Main.LocalPlayer.CWR().GetScreenShake(9f * (1f - dist / 1500f));
            }
        }

        /// <summary>余韵：粉孢子雾跟随，零星荧光</summary>
        private void UpdateFog(int age) {
            Lighting.AddLight(Projectile.Center, PlanteraRenderHelper.PetalPink.ToVector3() * 0.35f);
            if (VaultUtils.isServer) {
                return;
            }
            if (Main.rand.NextBool(7)) {
                PRTLoader.NewParticle<PRT_PlanteraSporeMote>(
                    Projectile.Center + Main.rand.NextVector2Circular(FogRadius * 0.75f, FogRadius * 0.7f),
                    new Vector2(0f, -0.35f),
                    Color.Lerp(PlanteraRenderHelper.PetalPink, PlanteraRenderHelper.GlowGreen, Main.rand.NextFloat(0.5f)) * 0.85f,
                    Main.rand.NextFloat(0.5f, 1.1f))?.SetLife(45);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //冲击波扫中即缠(短缚)，清场兼控场
            target.AddBuff(ModContent.BuffType<BloomSnaredDebuff>(), 180);
            PlanteraRenderHelper.SpawnPetalBurst(target.Center, 5, 3.5f, false);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!Owner.active) {
                return false;
            }
            int age = Age;
            if (age < SwellTime) {
                DrawSwellBud(age);
            }
            else if (age < SwellTime + BurstTime) {
                DrawBurstGhost(age);
            }
            else {
                DrawFog(age);
            }
            return false;
        }

        /// <summary>花苞：藤蔓自两侧卷上来，瓣片闭合鼓胀，临爆颤抖</summary>
        private void DrawSwellBud(int age) {
            float swellT = age / (float)SwellTime;
            //慢起步猛收尾的鼓胀曲线
            float swell = MathF.Pow(swellT, 2.1f);
            //末段颤抖(确定性，不掷骰)
            float tremble = MathF.Sin(age * 2.7f + seed * 20f) * 2.4f * swell * swell;
            Vector2 center = Projectile.Center + new Vector2(tremble, MathF.Cos(age * 3.1f) * tremble * 0.6f);

            //两根藤从脚边卷向花苞(生长前沿跟随鼓胀进度)
            VineParams vine = VineParams.Default;
            vine.HalfWidth = 6f;
            vine.Taut = 0.45f;
            vine.Pulse = 0.55f;
            vine.PulseDir = 1f;
            vine.Grow = MathHelper.Clamp(swellT * 1.4f, 0f, 1f);
            vine.Seed = seed;
            Vector2 footL = Owner.Bottom + new Vector2(-34f, 2f);
            Vector2 footR = Owner.Bottom + new Vector2(34f, 2f);
            vine.RestLength = Vector2.Distance(footL, center) * 1.25f;
            PlanteraVineRenderer.DrawVine(Main.spriteBatch, footL, center, vine);
            vine.Seed = seed + 0.29f;
            vine.RestLength = Vector2.Distance(footR, center) * 1.25f;
            PlanteraVineRenderer.DrawVine(Main.spriteBatch, footR, center, vine);

            Texture2D petal = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = center - Main.screenPosition;

            //底衬辉光(A=0加色技法，预乘AlphaBlend批)
            Main.EntitySpriteDraw(glow, drawPos, null,
                PlanteraRenderHelper.GlowGreen with { A = 0 } * (0.55f * swell),
                0f, glow.Size() / 2f, 1.1f + swell * 1.7f, SpriteEffects.None, 0);

            //闭合瓣片：六瓣向心收拢，随鼓胀撑大
            float spin = Main.GlobalTimeWrappedHourly * 0.6f + seed * 9f;
            for (int i = 0; i < 6; i++) {
                float rot = MathHelper.TwoPi * i / 6f + spin;
                Color petalCol = Color.Lerp(PlanteraRenderHelper.PetalPink, Color.White, 0.12f) * (0.35f + 0.6f * swell);
                Main.EntitySpriteDraw(petal, drawPos, null, petalCol, rot + MathHelper.PiOver2,
                    new Vector2(petal.Width / 2f, petal.Height * 0.92f),
                    new Vector2(0.13f + 0.08f * swell, 0.16f + 0.34f * swell), SpriteEffects.None, 0);
            }

            //白热芯：临爆才亮
            Main.EntitySpriteDraw(glow, drawPos, null,
                Color.White with { A = 0 } * (0.7f * swell * swell),
                0f, glow.Size() / 2f, 0.35f + swell * 0.4f, SpriteEffects.None, 0);
        }

        /// <summary>爆开残像：瓣片向外甩开的几帧定格，风暴主体交给PRT与屏幕花环</summary>
        private void DrawBurstGhost(int age) {
            float t = (age - SwellTime) / 9f;
            if (t >= 1f) {
                return;
            }
            Texture2D petal = CWRAsset.Extra_98.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float fling = 26f + t * 120f;
            float spin = seed * 9f + t * 0.8f;
            Color col = Color.Lerp(PlanteraRenderHelper.PetalPink, Color.White, 0.3f) * (1f - t);
            for (int i = 0; i < 6; i++) {
                float rot = MathHelper.TwoPi * i / 6f + spin;
                Vector2 off = rot.ToRotationVector2() * fling;
                Main.EntitySpriteDraw(petal, drawPos + off, null, col, rot + MathHelper.PiOver2,
                    new Vector2(petal.Width / 2f, petal.Height * 0.5f),
                    new Vector2(0.2f, 0.5f + t * 0.3f), SpriteEffects.None, 0);
            }
        }

        /// <summary>孢子雾：暗压层(真alpha雾图)遮蔽打底+着色器孢子云，粉色余韵</summary>
        private void DrawFog(int age) {
            int fogAge = age - SwellTime - BurstTime;
            float fogIn = MathHelper.Clamp(fogAge / 24f, 0f, 1f);
            float decay = MathHelper.Clamp((age - (TotalTime - DecayTime)) / (float)DecayTime, 0f, 1f);
            float fade = fogIn * (1f - decay);
            if (fade <= 0.02f) {
                return;
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //暗压层：加色画不出遮蔽，必须真alpha贴图(Fog)压暗
            Texture2D fogTex = CWRAsset.Fog.Value;
            for (int i = 0; i < 3; i++) {
                float rot = Main.GlobalTimeWrappedHourly * (0.08f + i * 0.05f) * (i == 1 ? -1f : 1f) + i * 2.3f + seed * 11f;
                SpriteEffects flip = i == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Color dark = new Color(26, 9, 22) * (0.5f * fade);
                Main.EntitySpriteDraw(fogTex, drawPos, null, dark, rot, fogTex.Size() / 2f,
                    FogRadius / 96f * (0.85f + i * 0.16f), flip, 0);
            }

            //孢子云着色器(品红配色=uPhase2)，缺编走贴图回退
            Effect shader = EffectLoader.PlanteraSporeFog?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || noise == null) {
                Color tint = PlanteraRenderHelper.PetalPink * (0.4f * fade);
                for (int i = 0; i < 3; i++) {
                    float rot = Main.GlobalTimeWrappedHourly * (0.1f + i * 0.06f) + i * 2.1f;
                    SpriteEffects flip = i == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                    Main.EntitySpriteDraw(fogTex, drawPos, null, tint, rot, fogTex.Size() / 2f,
                        FogRadius / 110f * (0.8f + i * 0.18f), flip, 0);
                }
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uBirth"]?.SetValue(fogIn);
            shader.Parameters["uDecay"]?.SetValue(decay);
            shader.Parameters["uPhase2"]?.SetValue(1f);
            shader.Parameters["seed"]?.SetValue(seed);
            //噪声显式绑s1：SpriteBatch.Draw会把s0覆写成画布贴图，参数式绑定实机失效
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = InnoVault.VaultAsset.placeholder2.Value;
            float size = FogRadius * 2.6f;
            sb.Draw(quad, drawPos, null, Color.White, 0f, quad.Size() / 2f, size / quad.Width, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            return;
        }
    }
}
