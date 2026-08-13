using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles
{
    /// <summary>
    /// 贴地行进的霜脉冲(跺脚震荡波前)。ai[0]=带符号速度 ai[1]=最大行程；
    /// 沿地表爬坡下坎，撞高墙自毁，可跳越
    /// </summary>
    internal class DeerFrostPulseProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float Speed => Projectile.ai[0];
        private float MaxTravel => Projectile.ai[1];

        private ref float Traveled => ref Projectile.localAI[1];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 56;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 170;
            Projectile.coldDamage = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                SnapToGround(force: true);
            }

            //推进+贴地
            float step = Speed;
            Projectile.position.X += step;
            Traveled += Math.Abs(step);
            if (!SnapToGround(force: false) || Traveled >= MaxTravel) {
                Projectile.Kill();
                return;
            }

            //淡入淡出窗口做视觉强度
            float intensity = MathHelper.Clamp(Traveled / 90f, 0f, 1f) * MathHelper.Clamp((MaxTravel - Traveled) / 120f, 0f, 1f);

            if (!Main.dedServ) {
                //冰晶浪头(贴地喷泉)
                int dustCount = 1 + (int)(intensity * 3f);
                for (int i = 0; i < dustCount; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-18f, 18f), -4f),
                        DustID.Frost, new Vector2(Speed * 0.2f + Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(2.5f, 7f) * (0.5f + intensity * 0.7f)),
                        90, default, Main.rand.NextFloat(1f, 1.9f));
                    dust.noGravity = Main.rand.NextBool(3);
                }
                if (Main.rand.NextBool(5)) {
                    PRTLoader.NewParticle<PRT_ATShard>(Projectile.Bottom + new Vector2(0f, -10f),
                        new Vector2(Speed * 0.3f, -Main.rand.NextFloat(2f, 5f)),
                        DeerclopsMotion.IceBlue * 0.8f, Main.rand.NextFloat(0.25f, 0.4f))
                        .Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.25f, 0.25f));
                }
                Lighting.AddLight(Projectile.Center, DeerclopsMotion.IceBlue.ToVector3() * 0.5f * intensity);
            }
        }

        /// <summary>
        /// 吸附地表；返回false表示撞上高墙(应自毁)。
        /// 沿脚下向上让出、向下寻底，落差限8格
        /// </summary>
        private bool SnapToGround(bool force) {
            Point tile = Projectile.Bottom.ToTileCoordinates();
            int x = tile.X;
            int y = tile.Y;

            //从脚位向上找非实心(出土)
            int riseSteps = 0;
            while (riseSteps < 9 && y > 20 && WorldGen.SolidTile(x, y - 1)) {
                y--;
                riseSteps++;
            }
            if (riseSteps >= 9) {
                //高墙，撞毁
                return false;
            }
            //向下找地
            int fallSteps = 0;
            while (fallSteps < 9 && y < Main.maxTilesY - 20 && !WorldGen.SolidTile(x, y)) {
                y++;
                fallSteps++;
            }
            if (fallSteps >= 9 && !force) {
                //深坑，波前散了
                return false;
            }

            float groundY = y * 16f;
            //平滑贴合，小坡不跳变
            float targetY = groundY - Projectile.height;
            Projectile.position.Y = force ? targetY : MathHelper.Lerp(Projectile.position.Y, targetY, 0.5f);
            return true;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.DeerFrostFissure?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }
            float intensity = MathHelper.Clamp(Traveled / 90f, 0f, 1f) * MathHelper.Clamp((MaxTravel - Traveled) / 120f, 0f, 1f);
            if (intensity <= 0.02f) {
                return;
            }

            //拖在浪头后的霜痕
            float halfLen = 64f;
            float halfH = 14f;
            int dir = Math.Sign(Speed);
            Vector2 basePos = Projectile.Bottom + new Vector2(-dir * halfLen * 0.7f, -2f);
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(basePos.X - halfLen, basePos.Y - halfH, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(basePos.X - halfLen, basePos.Y + halfH, 0f), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture(new Vector3(basePos.X + halfLen, basePos.Y - halfH, 0f), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture(new Vector3(basePos.X + halfLen, basePos.Y + halfH, 0f), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uProgress"]?.SetValue(1f);
            effect.Parameters["uFade"]?.SetValue(intensity * 0.8f);
            effect.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.311f % 1f + Traveled * 0.001f);
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        public override bool PreDraw(ref Color lightColor) {
            float intensity = MathHelper.Clamp(Traveled / 90f, 0f, 1f) * MathHelper.Clamp((MaxTravel - Traveled) / 120f, 0f, 1f);
            if (intensity <= 0.02f) {
                return false;
            }
            //浪头冷光核
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color core = DeerclopsMotion.IceBlue with { A = 0 };
            Main.EntitySpriteDraw(glow, drawPos + new Vector2(0f, 8f), null, core * (0.75f * intensity), 0f,
                glow.Size() / 2f, new Vector2(1.3f, 1.05f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos + new Vector2(0f, 2f), null, Color.White with { A = 0 } * (0.4f * intensity), 0f,
                glow.Size() / 2f, new Vector2(0.6f, 0.5f), SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item50 with { Volume = 0.55f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Ice,
                    Main.rand.NextVector2Circular(3f, 2f) - Vector2.UnitY * Main.rand.NextFloat(1f, 4f), 80, default, Main.rand.NextFloat(1f, 1.7f));
                dust.noGravity = Main.rand.NextBool();
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_ATShard>(Projectile.Center, Main.rand.NextVector2Circular(4f, 3f) - Vector2.UnitY * 2f,
                    DeerclopsMotion.IceBlue * 0.85f, Main.rand.NextFloat(0.3f, 0.5f))
                    .Configure(Main.rand.Next(16, 26), Main.rand.NextFloat(-0.25f, 0.25f));
            }
        }
    }
}
