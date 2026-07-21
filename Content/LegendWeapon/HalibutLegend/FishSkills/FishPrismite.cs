using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishPrismite : FishSkill
    {
        public override int UnlockFishID => ItemID.Prismite;
        public override int DefaultCooldown => 60 - HalibutData.GetDomainLayer() * 3;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (Cooldown > 0) {
                return null;
            }

            Vector2 shootVel = velocity.SafeNormalize(Vector2.UnitX) * 18f;

            //ai[1] 为光谱色相，-1 = 未分光的白光波
            Projectile.NewProjectile(
                source,
                position,
                shootVel,
                ModContent.ProjectileType<PrismiteWaveProjectile>(),
                (int)(damage * (1f + HalibutData.GetDomainLayer() * 0.25f)),
                knockback * 1.2f,
                player.whoAmI,
                0,
                -1f
            );

            SetCooldown();
            SoundEngine.PlaySound(SoundID.Item105 with { Volume = 0.7f, Pitch = 0.3f }, position);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.3f, Pitch = 0.55f, MaxInstances = 3 }, position);

            return false;
        }
    }

    /// <summary>
    /// 棱彩冲击波，一道相干白光波前，飞行中 = 冷白发丝弧线 + 前红后蓝的极窄色散边
    /// 身后脱落波痕残弧；命中或触地分裂时白光展开成光谱扇，子波按出射角序
    /// 继承红→紫色相切片继续前进<br/>
    /// ai[0] = 分裂代数；ai[1] = 光谱色相 0..1（-1 = 白光）
    /// </summary>
    internal class PrismiteWaveProjectile : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private const int MaxLifeTime = 240;

        private float scale = 1f;

        //螺旋运动参数
        private float spiralPhase = 0f;
        private float spiralIntensity = 0f;
        private Vector2 baseVelocity;

        private int generation;
        private float hueT;          //-1 白光，0..1 光谱切片
        private Color colLead;
        private Color colCore;
        private Color colTrail;
        private float pulsePhase;
        private float waveSeed;
        private int waveletShedTimer;

        public override void SetDefaults() {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.penetrate = 3 + (int)(HalibutData.GetLevel() / 4f);
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.extraUpdates = 2;
        }

        public override void Initialize() {
            generation = (int)Projectile.ai[0];
            hueT = Projectile.ai[1];
            FishPrismiteVFX.WaveColors(hueT, out colLead, out colCore, out colTrail);

            baseVelocity = Projectile.velocity;
            Projectile.scale = 1f - generation * 0.12f;
            waveSeed = Main.rand.NextFloat(10f);

            if (Main.dedServ) {
                return;
            }
            //出生玻璃闪，各端都会执行
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            int glintCount = generation == 0 ? 6 : 3;
            for (int i = 0; i < glintCount; i++) {
                Vector2 vel = dir.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 6f);
                PRTLoader.NewParticle<PRT_FishPrismGlint>(Projectile.Center, vel, colCore, Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(10, 16));
            }
            if (generation == 0) {
                //出膛棱镜闪，白光上路
                PRTLoader.NewParticle<PRT_FishPrismGlint>(Projectile.Center, dir * 3f, FishPrismiteVFX.ColdWhite, 1.15f)
                    ?.Configure(10);
            }
        }

        public override void AI() {
            //螺旋运动轨迹（原运动骨架保留）
            spiralPhase += 0.18f;
            float lifeProgress = 1f - Projectile.timeLeft / (float)MaxLifeTime;

            //根据生命周期调整螺旋强度
            if (lifeProgress < 0.2f) {
                spiralIntensity = MathHelper.Lerp(0f, 1f, lifeProgress / 0.2f);
            }
            else if (lifeProgress > 0.7f) {
                spiralIntensity = MathHelper.Lerp(1f, 0.3f, (lifeProgress - 0.7f) / 0.3f);
            }
            else {
                spiralIntensity = 1f;
            }

            if (tileCollideCooltimer > 0) {
                tileCollideCooltimer--;
            }

            //应用螺旋偏移
            Vector2 perpendicular = baseVelocity.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero);
            float spiralOffset = (float)Math.Sin(spiralPhase) * 3f * spiralIntensity * (1f - generation * 0.3f);
            Projectile.velocity = baseVelocity * 0.99f + perpendicular * spiralOffset;
            baseVelocity = Projectile.velocity;

            Projectile.rotation = Projectile.velocity.ToRotation();
            pulsePhase += 0.2f;

            //缩放动画，入场生长
            if (lifeProgress < 0.1f) {
                scale = VaultUtils.EaseOutBack(lifeProgress / 0.1f) * 1f;
            }
            else if (lifeProgress > 0.85f) {
                scale = MathHelper.Lerp(1f, 0.6f, (lifeProgress - 0.85f) / 0.15f);
            }
            else {
                float breathe = (float)Math.Sin(pulsePhase * 0.5f) * 0.15f;
                scale = 1f + breathe + lifeProgress * 0.2f;
            }

            if (!Main.dedServ) {
                //波痕脱落，约 3 帧一道原地衰减的残弧（extraUpdates=2，AI 每帧 3 次）
                if (++waveletShedTimer >= 9) {
                    waveletShedTimer = 0;
                    PRTLoader.NewParticle<PRT_FishPrismWavelet>(Projectile.Center, Projectile.velocity * 0.05f
                        , colCore, scale * Projectile.scale * 0.82f)
                        ?.Configure(Projectile.rotation, colLead, colTrail, 13, 1.014f);
                }
                //相位闪点，沿弧随机位置的稀疏单帧反光
                if (Main.rand.NextBool(11)) {
                    Vector2 perp = Projectile.velocity.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero);
                    Vector2 pos = Projectile.Center + perp * Main.rand.NextFloat(-40f, 40f) * scale * Projectile.scale;
                    Color col = hueT < 0f ? FishPrismiteVFX.ColdWhite : FishPrismiteVFX.Spectrum(hueT);
                    PRTLoader.NewParticle<PRT_FishPrismGlint>(pos, -Projectile.velocity * 0.06f, col, Main.rand.NextFloat(0.32f, 0.55f))
                        ?.Configure(Main.rand.Next(9, 15));
                }
            }

            Color lightCol = hueT < 0f ? FishPrismiteVFX.ColdWhite : FishPrismiteVFX.Spectrum(hueT);
            Lighting.AddLight(Projectile.Center, lightCol.ToVector3() * 0.35f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            bool splitting = generation == 0 && Projectile.numHits == 0;
            if (splitting) {
                //白光在体内展开的一瞬，短顿帧
                target.CWR().TimeFrozenTick = 3;
            }
            SplitOnImpact(Projectile.Center, Projectile.velocity);
            SpawnPierceGlints(!splitting);
        }

        private int tileCollideCount;
        private int tileCollideCooltimer;
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (tileCollideCooltimer > 0) {
                return false;
            }

            //反弹冲量
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.9f;
                baseVelocity.X = Projectile.velocity.X;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.9f;
                baseVelocity.Y = Projectile.velocity.Y;
            }

            SplitOnImpact(Projectile.Center, -oldVelocity);
            SpawnBounceFlash();

            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.3f }, Projectile.Center);

            if (++tileCollideCount > 6) {//允许反弹6次
                Projectile.Kill();//超过反弹次数后消失
                Projectile.netUpdate = true;
            }

            tileCollideCooltimer = 22;
            return false;
        }

        private void SplitOnImpact(Vector2 impactPos, Vector2 impactDirection) {
            if (generation > 0 || Projectile.numHits > 0) {
                return;
            }

            int splitCount = 3 + HalibutData.GetDomainLayer() / 2;
            Vector2 baseDir = impactDirection.SafeNormalize(Vector2.UnitX);
            float spreadAngle = MathHelper.Pi * 0.8f;

            //彩虹时刻
            FishPrismiteVFX.PrismBurst(impactPos, baseDir, spreadAngle, splitCount, scale * Projectile.scale);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            for (int i = 0; i < splitCount; i++) {
                float angle = -spreadAngle / 2f + (spreadAngle * i / (splitCount - 1));
                Vector2 splitVel = baseDir.RotatedBy(angle) * Main.rand.NextFloat(12f, 16f);
                //子波按角序继承光谱切片，红 → 紫
                float hue = i / (float)(splitCount - 1);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    impactPos,
                    splitVel,
                    Projectile.type,
                    (int)(Projectile.damage * 0.7f),
                    Projectile.knockBack * 0.75f,
                    Projectile.owner,
                    generation + 1,
                    hue
                );
            }
        }

        /// <summary>贯穿目标时的折射闪点，光穿过身体的一小簇反光</summary>
        private void SpawnPierceGlints(bool withChime) {
            if (withChime) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.25f, Pitch = 0.55f, MaxInstances = 5 }, Projectile.Center);
            }
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(1.5f, 4.5f);
                Color col = hueT < 0f
                    ? (Main.rand.NextBool() ? FishPrismiteVFX.ColdWhite : FishPrismiteVFX.TrailBlue)
                    : FishPrismiteVFX.Spectrum(hueT + Main.rand.NextFloat(-0.05f, 0.05f));
                PRTLoader.NewParticle<PRT_FishPrismGlint>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , vel, col, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(10, 16));
            }
        }

        /// <summary>触地反弹的反射闪，一道顺新方向的波痕 + 少量接触闪点</summary>
        private void SpawnBounceFlash() {
            if (Main.dedServ) {
                return;
            }
            float rot = Projectile.velocity.SafeNormalize(Vector2.UnitX).ToRotation();
            PRTLoader.NewParticle<PRT_FishPrismWavelet>(Projectile.Center, Vector2.Zero, colCore, scale * Projectile.scale * 0.8f)
                ?.Configure(rot, colLead, colTrail, 10, 1.03f);
            for (int i = 0; i < 3; i++) {
                Vector2 vel = rot.ToRotationVector2().RotatedByRandom(0.7f) * Main.rand.NextFloat(1.5f, 4f);
                PRTLoader.NewParticle<PRT_FishPrismGlint>(Projectile.Center, vel, colCore, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.Next(9, 14));
            }
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.2f, Pitch = 0.7f, MaxInstances = 3 }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            //退相干 aftermath
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            float rot = dir.ToRotation();
            float ws = MathF.Max(scale * Projectile.scale, 0.3f);
            for (int k = 0; k < 3; k++) {
                PRTLoader.NewParticle<PRT_FishPrismWavelet>(Projectile.Center + dir * (k * 10f)
                    , dir * (0.6f - k * 0.2f), colCore, ws * (0.9f - k * 0.14f))
                    ?.Configure(rot, colLead, colTrail, 16 + k * 4, 1.018f);
            }
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1.6f, 1.6f);
                Color col = hueT < 0f ? FishPrismiteVFX.ColdWhite : FishPrismiteVFX.Spectrum(hueT + Main.rand.NextFloat(-0.04f, 0.04f));
                PRTLoader.NewParticle<PRT_FishPrismGlint>(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f)
                    , vel, col, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //shader 缺失时的降级
            if (FishPrismiteAssets.FishPrismWave == null) {
                DrawFallbackArc();
            }
            return false;
        }

        private void DrawFallbackArc() {
            Texture2D tex = FishPrismiteAssets.ArcWaveTex?.Value;
            if (tex == null) {
                return;
            }
            Vector2 origin = new(tex.Width * 0.72f, tex.Height * 0.5f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 dirVec = Projectile.rotation.ToRotationVector2();
            float ws = scale * Projectile.scale;
            Vector2 texScale = new Vector2(0.62f, 0.9f) * ws;
            Main.EntitySpriteDraw(tex, pos + dirVec * 3f, null, colLead with { A = 0 } * 0.45f
                , Projectile.rotation, origin, texScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos - dirVec * 3f, null, colTrail with { A = 0 } * 0.45f
                , Projectile.rotation, origin, texScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, colCore with { A = 0 } * 0.85f
                , Projectile.rotation, origin, texScale, SpriteEffects.None, 0);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect fx = FishPrismiteAssets.FishPrismWave;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null || !Projectile.active) {
                return;
            }
            float ws = scale * Projectile.scale;
            if (ws < 0.05f) {
                return;
            }

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float len = 150f * ws;
            float halfSpan = 64f * ws;
            const float frontFrac = 0.8f;
            Vector2 back = Projectile.Center - dir * (len * frontFrac);
            Vector2 front = Projectile.Center + dir * (len * (1f - frontFrac));

            //入场淡入 + 临终淡出（timeLeft 以 update 计，extraUpdates=2）
            float fade = MathHelper.Clamp((MaxLifeTime - Projectile.timeLeft) / 14f, 0f, 1f);
            if (Projectile.timeLeft < 20) {
                fade *= Projectile.timeLeft / 20f;
            }
            float breathe = 1.12f + 0.22f * MathF.Sin(pulsePhase);

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uSizePx"]?.SetValue(new Vector2(len, halfSpan * 2f));
            fx.Parameters["uFrontFrac"]?.SetValue(frontFrac);
            fx.Parameters["uR"]?.SetValue(66f * ws);
            fx.Parameters["uSpanY"]?.SetValue(52f * ws);
            fx.Parameters["uDisp"]?.SetValue(hueT < 0f ? 3.2f : 2.3f);
            fx.Parameters["uColLead"]?.SetValue(colLead.ToVector3());
            fx.Parameters["uColCore"]?.SetValue(colCore.ToVector3());
            fx.Parameters["uColTrail"]?.SetValue(colTrail.ToVector3());
            fx.Parameters["uCoreGain"]?.SetValue(hueT < 0f ? breathe : breathe * 0.9f);
            fx.Parameters["uFade"]?.SetValue(fade);
            fx.Parameters["uDark"]?.SetValue(hueT < 0f ? 0.45f : 0.32f);
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(waveSeed);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((back - perp * halfSpan).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((back + perp * halfSpan).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture((front - perp * halfSpan).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture((front + perp * halfSpan).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            //预乘输出走 AlphaBlend
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }
    }
}
