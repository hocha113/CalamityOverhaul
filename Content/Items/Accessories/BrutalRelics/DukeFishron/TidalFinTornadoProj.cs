using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.DukeFishron
{
    /// <summary>
    /// 潮汐之鳍的末段龙卷：末段冲刺落点掀起的滞留水龙卷，原地绞杀，
    /// 复用 FishronTornado 假体积着色器。近地时足底吸附地表，高空时悬滞成水龙吸。
    /// ai[0]=强化(0/1) localAI[0]=寿命计时 localAI[1]=落定标记
    /// </summary>
    internal class TidalFinTornadoProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int RiseTime = 30;
        private const int FadeTime = 40;
        /// <summary>总寿命：起身+滞留+消散 ≈ 7.8 秒</summary>
        private const int TotalLife = 470;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        private bool Empowered => Projectile.ai[0] >= 1f;
        private ref float LifeTimer => ref Projectile.localAI[0];
        private ref float Anchored => ref Projectile.localAI[1];

        private float ColumnWidth => Empowered ? 190f : 160f;
        private float ColumnHeight => Empowered ? 620f : 520f;

        /// <summary>起身/消散包络</summary>
        private float Envelope {
            get {
                float rise = MathHelper.Clamp(LifeTimer / RiseTime, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / (float)FadeTime, 0f, 1f);
                return Math.Min(rise * rise, fade);
            }
        }

        public override void SetStaticDefaults() {
            //绘制 quad 宽出命中盒一倍余，出屏余量不足会整柱瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 620;
        }

        public override void SetDefaults() {
            Projectile.width = 160;
            Projectile.height = 520;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.DamageType = DamageClass.Generic;
            //绞杀节奏：本地免疫表 12 帧一跳
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>起身/消散期判定关闭</summary>
        private bool HitWindowOpen => LifeTimer >= RiseTime * 0.6f && Projectile.timeLeft >= FadeTime;

        public override bool? CanHitNPC(NPC target) => HitWindowOpen ? null : false;

        public override void AI() {
            LifeTimer++;

            //长寿命弹幕：owner 端逐帧按面板刷新单跳伤害（含雨天强化倍率）
            if (Projectile.owner == Main.myPlayer) {
                float mult = Empowered ? TidalFinPlayer.EmpowerMult : 1f;
                Projectile.damage = (int)Main.player[Projectile.owner]
                    .GetTotalDamage(DamageClass.Generic).ApplyTo(TidalFinPlayer.TornadoDamage * mult);
            }

            //首帧落定：向下探地，近地贴地、高空悬滞（同步的生成位置+确定性探地，各端一致）
            if (Anchored == 0f) {
                Anchored = 1f;
                Vector2 ground = FishronMotionFX.FindSurfaceBelow(Projectile.Center, out _);
                float bottomY = Math.Min(ground.Y + 8f, Projectile.Center.Y + 300f);
                Vector2 bottom = new(Projectile.Center.X, bottomY);
                Projectile.width = (int)ColumnWidth;
                Projectile.height = (int)ColumnHeight;
                Projectile.position = new Vector2(bottom.X - Projectile.width / 2f, bottom.Y - Projectile.height);

                FishronMotionFX.SpawnSplashBurst(bottom, Empowered ? 1.5f : 1.25f);
            }

            //绞杀吸力：把非 Boss 且不免击退的敌人往柱身里拽（服务端权威，限频同步；2 帧节流省全表扫）
            if (!VaultUtils.isClient && HitWindowOpen && Main.GameUpdateCount % 2 == 0) {
                PullVictims();
            }

            UpdateVisuals();
        }

        /// <summary>向心拖拽：横向为主的吸力，Boss 与免击退者不受</summary>
        private void PullVictims() {
            Vector2 colCenter = Projectile.Center;
            foreach (var n in Main.ActiveNPCs) {
                if (n.boss || n.knockBackResist <= 0f || !n.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(n.Center, colCenter);
                if (dist > 380f) {
                    continue;
                }
                Vector2 toCol = new(colCenter.X - n.Center.X, (colCenter.Y - n.Center.Y) * 0.35f);
                Vector2 pull = toCol.SafeNormalize(Vector2.Zero) * 5f * n.knockBackResist;
                //2 帧节流下的等效补偿：0.15 ≈ 1-(1-0.08)²，吸力手感不变
                n.velocity = Vector2.Lerp(n.velocity, pull, 0.15f);
                if (Main.GameUpdateCount % 10 == 0) {
                    n.netUpdate = true;
                }
            }
        }

        /// <summary>基座泡沫/卷吸碎浪/柱身水珠/顶冠散逸与风声（纯客户端，镜像本体龙卷）</summary>
        private void UpdateVisuals() {
            if (VaultUtils.isServer) {
                return;
            }
            float env = Envelope;
            Vector2 bottom = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);

            if (Main.rand.NextBool(3)) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(
                    bottom + new Vector2(Main.rand.NextFloat(-ColumnWidth * 0.6f, ColumnWidth * 0.6f), -8f),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f)),
                    FishronMotionFX.FoamWhite * (0.35f * env), Main.rand.NextFloat(0.7f, 1.2f))
                    ?.Configure(Main.rand.Next(24, 40), Main.rand.NextFloat(-0.03f, 0.03f));
            }
            //底部卷吸：柱外碎浪被拖向基座再卷起
            if (Main.rand.NextBool(3)) {
                float sideSign = Main.rand.NextBool() ? 1f : -1f;
                Vector2 pos = bottom + new Vector2(sideSign * Main.rand.NextFloat(0.7f, 1.5f) * ColumnWidth * 0.5f,
                    -Main.rand.NextFloat(4f, 26f));
                Vector2 vel = new(-sideSign * Main.rand.NextFloat(2.5f, 4.5f), -Main.rand.NextFloat(1f, 2.5f));
                FishronMotionFX.SpawnSprayCone(pos, vel.SafeNormalize(-Vector2.UnitY), 1,
                    vel.Length() * 0.7f, vel.Length(), 0.3f, 0.75f * env);
            }
            //柱身甩出的水珠
            if (Main.rand.NextBool(2)) {
                float h = Main.rand.NextFloat(0.1f, 0.95f);
                Vector2 pos = bottom - new Vector2(0, Projectile.height * h)
                    + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * ColumnWidth * (1f - h * 0.45f), 0);
                Vector2 vel = new(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(1f, 4f));
                FishronMotionFX.SpawnSprayCone(pos, vel.SafeNormalize(-Vector2.UnitY), 1,
                    vel.Length() * 0.6f, vel.Length(), 0.4f, 0.8f * env);
            }
            //顶冠散逸
            if (Main.rand.NextBool(4)) {
                Vector2 top = bottom - new Vector2(0, Projectile.height * Main.rand.NextFloat(0.88f, 1.02f));
                float flingSign = Main.rand.NextBool() ? 1f : -1f;
                InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(
                    top + new Vector2(flingSign * ColumnWidth * Main.rand.NextFloat(0.1f, 0.4f), 0),
                    new Vector2(flingSign * Main.rand.NextFloat(1.5f, 3.5f), -Main.rand.NextFloat(0.8f, 2f)),
                    FishronMotionFX.FoamWhite * (0.3f * env), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.05f, 0.05f));
            }
            if (LifeTimer % 40 == 0 && env > 0.5f) {
                SoundEngine.PlaySound(SoundID.DD2_BookStaffTwisterLoop with {
                    Volume = Empowered ? 0.7f : 0.5f,
                    Pitch = -0.25f,
                    MaxInstances = 3
                }, Projectile.Center);
            }
            Lighting.AddLight(Projectile.Center, FishronMotionFX.SeaGreen.ToVector3() * 0.6f * env);
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = Envelope;
            if (env <= 0.01f) {
                return false;
            }

            Effect effect = EffectLoader.FishronTornado?.Value;
            Vector2 bottom = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            //quad 大幅宽于名义柱径：撕裂轮廓与离体飞沫留在画布内侧（合同同本体龙卷）
            float drawW = ColumnWidth * 3.0f;
            float drawH = ColumnHeight * 1.30f;
            Vector2 drawCenter = bottom - new Vector2(0, drawH * 0.5f);

            if (effect == null || noiseTex == null) {
                DrawSpriteFallback(env, bottom, drawW, drawH);
                return false;
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(env);
            effect.Parameters["uGrade"]?.SetValue(Empowered ? 1f : 0f);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI * 0.617f);
            effect.Parameters["uDeepColor"]?.SetValue(FishronMotionFX.DeepSea.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(FishronMotionFX.FoamWhite.ToVector3());
            effect.Parameters["uSeaColor"]?.SetValue(FishronMotionFX.SeaGreen.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图（合同同 ShockRingDraw.Draw）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new(drawW / pixel.Width, drawH / pixel.Height);
            sb.Draw(pixel, drawCenter - Main.screenPosition, null, Color.White,
                0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>着色器缺失兜底：旋筒贴图堆叠</summary>
        private void DrawSpriteFallback(float env, Vector2 bottom, float drawW, float drawH) {
            Texture2D cyclone = CWRUtils.GetT2DAsset(CWRConstant.Masking + "Cyclone")?.Value;
            if (cyclone == null) {
                return;
            }
            int layers = 7;
            for (int i = 0; i < layers; i++) {
                float t = i / (float)(layers - 1);
                Vector2 pos = bottom - new Vector2(0, drawH * t) - Main.screenPosition;
                float w = MathHelper.Lerp(drawW * 0.5f, drawW, t) / cyclone.Width;
                float rotSpin = Main.GlobalTimeWrappedHourly * (4f - t * 1.5f) * (i % 2 == 0 ? 1f : -1f);
                Color c = Color.Lerp(FishronMotionFX.DeepSea, FishronMotionFX.SeaGreen, t);
                c = new Color(c.R, c.G, c.B, 0) * (env * 0.55f);
                Main.EntitySpriteDraw(cyclone, pos, null, c, rotSpin, cyclone.Size() / 2f,
                    new Vector2(w, w * 0.6f), SpriteEffects.None, 0);
            }
        }
    }
}
