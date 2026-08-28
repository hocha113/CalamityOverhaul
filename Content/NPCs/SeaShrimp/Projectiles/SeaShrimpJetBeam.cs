using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Core;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 渊喉水炮弹幕：口吐巨型高压水柱。判定角自持在同步 ai 上（不读本地骨架，
    /// 各端几何一致），锚点随宿主头位每帧刷新。宽度生命周期:展开12f→满宽持续→塌缩14f;
    /// 展开 ≥60% 才开伤害窗，判定芯 0.62× 可见宽且直线判定恒藏在垂坠可见体内(uSag≤18px)。
    /// ai[0]=宿主 npc.whoAmI，ai[1]=锁定角，ai[2]=扫速(带符号 rad/f)
    /// </summary>
    internal class SeaShrimpJetBeam : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        private const int ExpandFrames = 12;
        private const int CollapseFrames = 14;
        private static int TotalLife => ExpandFrames + SeaShrimpDirector.JetFireFrames + CollapseFrames;

        private int OwnerIndex => (int)Projectile.ai[0];
        private float LockAngle => Projectile.ai[1];
        private float SweepRate => Projectile.ai[2];

        /// <summary>本地帧龄：逐端计数，各端偏差 ≤2 帧（既有口径）</summary>
        private int Age => (int)Projectile.localAI[0];

        /// <summary>当前柱向角：锁定角+匀速扫掠，纯同步量的确定函数</summary>
        private float Angle => LockAngle + SweepRate * Age;

        /// <summary>宽度生命周期 0..1</summary>
        private float Width01 {
            get {
                int age = Age;
                if (age < ExpandFrames) {
                    float t = age / (float)ExpandFrames;
                    return 1f - (1f - t) * (1f - t);
                }
                int fromEnd = TotalLife - age;
                if (fromEnd < CollapseFrames) {
                    float t = fromEnd / (float)CollapseFrames;
                    return t * t;
                }
                return 1f;
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1700;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 130;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>口部锚点：宿主头前沿柱向探出（宿主没了退化为自身位置）</summary>
        private Vector2 Mouth() {
            int owner = OwnerIndex;
            if (owner >= 0 && owner < Main.maxNPCs && Main.npc[owner].active
                && Main.npc[owner].ModNPC is SeaShrimpBoss) {
                return Main.npc[owner].Center + Angle.ToRotationVector2() * 52f;
            }
            return Projectile.Center;
        }

        /// <summary>当前柱长与落点：射线打地形，打空给最大射程</summary>
        private float BeamLength(Vector2 mouth, Vector2 dir, out bool hit, out Vector2 hitPoint) {
            hit = ShrimpTerrain.RaycastSurface(mouth, dir, SeaShrimpDirector.JetMaxLength, out hitPoint);
            return Vector2.Distance(mouth, hitPoint);
        }

        public override void AI() {
            Projectile.localAI[0]++;
            if (Age >= TotalLife) {
                Projectile.Kill();
                return;
            }
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 mouth = Mouth();
            Projectile.Center = mouth;
            float len = BeamLength(mouth, dir, out bool hit, out Vector2 hitPoint);

            float w01 = Width01;
            Lighting.AddLight(mouth + dir * len * 0.5f, 0.12f * w01, 0.26f * w01, 0.46f * w01);

            if (Main.dedServ || w01 < 0.25f) {
                return;
            }
            //口部回溅：高压出流的反向水雾
            if (Main.GameUpdateCount % 3 == 0) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(mouth + dir * 14f,
                    -dir * Main.rand.NextFloat(1.5f, 3.5f) + Main.rand.NextVector2Circular(1.2f, 1.2f),
                    Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(12, 1.6f);
            }
            //落点溅泉：柱打在地形上的持续冲刷（活得比柱久=余痕）
            if (hit && Main.GameUpdateCount % 4 == 0) {
                EverdeepVFX.SplashBurst(hitPoint, dir * 9f, 0.85f);
            }
            //沿柱飞沫
            if (Main.GameUpdateCount % 5 == 0) {
                float at = Main.rand.NextFloat(0.2f, 0.9f);
                EverdeepVFX.ShedDroplet(mouth + dir * (len * at)
                    + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-40f, 40f),
                    dir * 3f + Main.rand.NextVector2Circular(1f, 1f), 0.8f);
            }
            //持续低鸣：水压轰响
            if (Age % 24 == 6) {
                SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.45f, Pitch = -0.6f, MaxInstances = 2 }, mouth);
            }
        }

        /// <summary>伤害窗=可见窗：展开 ≥60% 才咬人</summary>
        public override bool? CanDamage() => Width01 >= 0.6f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 mouth = Mouth();
            float len = BeamLength(mouth, dir, out _, out _);
            //判定芯 0.62× 可见满宽：直线判定,垂坠 ≤18px < (可见半宽-判定半宽),恒藏在可见体内
            float coreWidth = SeaShrimpDirector.JetWidth * SeaShrimpDirector.JetCoreFrac * Width01;
            float _ignore = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                mouth, mouth + dir * len, coreWidth, ref _ignore);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //断流：柱身化滴（余痕活过弹体）
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 mouth = Mouth();
            for (int i = 0; i < 8; i++) {
                float at = i / 8f;
                EverdeepVFX.ShedDroplet(mouth + dir * (SeaShrimpDirector.JetMaxLength * 0.4f * at),
                    dir * Main.rand.NextFloat(2f, 5f) + new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)), 0.9f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float w01 = Width01;
            if (w01 <= 0.02f) {
                return false;
            }
            Vector2 dir = Angle.ToRotationVector2();
            Vector2 mouth = Mouth();
            float len = BeamLength(mouth, dir, out bool hit, out _);

            //画布契约：quad 长 = 柱长 + 末端余量（打空散逸 130px / 打中回溅 44px），
            //quad 高 = 满宽×2.6 + 2×垂坠；1 uv = 1 quad px，由 uQuadLenPx/uQuadHPx 告知 shader
            float quadLen = len + (hit ? 44f : 130f);
            float sag = 18f * MathHelper.Clamp(len / SeaShrimpDirector.JetMaxLength, 0f, 1f);
            //quad 局部 +y 在旋转后指向世界下方的分量随 cos(angle) 变号，垂坠符号随之折算
            float sagLocal = sag * (MathF.Cos(Angle) >= 0f ? 1f : -1f);
            float quadH = SeaShrimpDirector.JetWidth * 2.6f + MathF.Abs(sagLocal) * 2f;

            Effect fx = EffectLoader.SeaShrimpJet?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return false;
            }
            if (fx == null || noiseTex == null) {
                DrawFallback(mouth, dir, len, w01);
                return false;
            }

            fx.CurrentTechnique = fx.Techniques["TechJet"];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.47f);
            fx.Parameters["fadeAlpha"]?.SetValue(1f);
            fx.Parameters["uQuadLenPx"]?.SetValue(quadLen);
            fx.Parameters["uQuadHPx"]?.SetValue(quadH);
            fx.Parameters["uLenPx"]?.SetValue(len);
            fx.Parameters["uWidthPx"]?.SetValue(SeaShrimpDirector.JetWidth * w01);
            fx.Parameters["uSagPx"]?.SetValue(sagLocal);
            fx.Parameters["uImpact"]?.SetValue(hit ? 1f : 0f);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();

            //quad 起点埋进口壳 8px，源头淡起被头部剪影盖住
            Vector2 origin = new(0f, 0.5f);
            Rectangle src = new(0, 0, 1, 1);
            sb.Draw(pixel, mouth - dir * 8f - Main.screenPosition, src, Color.White,
                Angle, origin, new Vector2(quadLen, quadH), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>着色器缺失回退：分段暗鞘+亮芯，两端正弦包络收口（禁整条平切）</summary>
        private void DrawFallback(Vector2 mouth, Vector2 dir, float len, float w01) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            const int Segs = 12;
            for (int i = 0; i < Segs; i++) {
                float t0 = i / (float)Segs;
                float segLen = len / Segs;
                float endEnv = MathF.Sin(MathF.Min(t0 * 3f, MathF.Min((1f - t0) * 3f, 1f)) * MathHelper.PiOver2);
                Vector2 pos = mouth + dir * (len * t0) - Main.screenPosition;
                float w = SeaShrimpDirector.JetWidth * w01 * endEnv;
                Main.spriteBatch.Draw(pixel, pos, src, SeaShrimpVFX.Deep * (0.75f * endEnv), Angle,
                    new Vector2(0f, 0.5f), new Vector2(segLen + 1f, w), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(pixel, pos, src, SeaShrimpVFX.Glow * (0.5f * endEnv), Angle,
                    new Vector2(0f, 0.5f), new Vector2(segLen + 1f, w * 0.34f), SpriteEffects.None, 0f);
            }
        }
    }
}
