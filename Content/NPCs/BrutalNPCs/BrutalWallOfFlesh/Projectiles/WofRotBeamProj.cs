using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles
{
    /// <summary>
    /// 腐眼断头闸斩束：锁定高度上的水平血光铡刀，贴着跑道把整条车道封死一拍。
    /// ai[0]=墙whoAmI ai[1]=锁定高度(生成后永不再瞄，预告即承诺)。
    /// 判定半厚固定30px(低于跳跃高度)，可见束体更宽，判定窄于视觉
    /// </summary>
    internal class WofRotBeamProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int ExpandTime = 6;
        private static int SustainTime => WofDirector.GuillotineSustain;
        private static int DecayTime => WofDirector.GuillotineDecay;
        private static int TotalLife => ExpandTime + SustainTime + DecayTime;
        private const float MaxBeamLength = 3400f;
        private const float MaxWidth = 74f;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Wall => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;

        private float beamWidth;
        private float beamLength;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3600;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            NPC wall = Wall;
            //墙没了或已被死亡/换态打断→快进衰减，不硬切
            bool hostValid = wall.Alives() && wall.type == NPCID.WallofFlesh
                && WallOfFleshAI.GetStateIndex(wall) == WofStateIndex.RotGuillotine;
            if (!hostValid && Timer < TotalLife - DecayTime) {
                Timer = TotalLife - DecayTime;
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie103 with { Pitch = 0.15f, Volume = 1f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item12 with { Pitch = -0.6f, Volume = 0.8f, MaxInstances = 3 }, Projectile.Center);
            }

            //锚定：口随墙面推进，高度死锁在ai[1]
            int dir = 1;
            if (wall.Alives()) {
                dir = wall.direction != 0 ? wall.direction : 1;
                Projectile.Center = new Vector2(
                    WofWallField.WallFaceX(wall) - dir * 6f, Projectile.ai[1]);
            }
            Projectile.direction = dir;
            Projectile.rotation = dir > 0 ? 0f : MathHelper.Pi;

            //宽度包络：急张→持续搏动→衰减
            float collapseStart = TotalLife - DecayTime;
            if (Timer < ExpandTime) {
                beamWidth = MathHelper.Lerp(6f, MaxWidth, VaultUtils.EaseOutCubic(Timer / ExpandTime));
                beamLength = MaxBeamLength;
            }
            else if (Timer >= collapseStart) {
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad((Timer - collapseStart) / DecayTime));
                beamLength = MaxBeamLength;
            }
            else {
                beamWidth = MaxWidth;
                beamLength = MaxBeamLength;
            }
            beamWidth *= 1f + 0.06f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 24f);

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 2;

            Vector2 beamDir = new(dir, 0f);
            for (int i = 0; i < 6; i++) {
                Lighting.AddLight(Projectile.Center + beamDir * (beamLength / 6f * i),
                    WofMotionFX.BloodHot.ToVector3() * 0.7f);
            }

            if (VaultUtils.isServer) {
                return;
            }
            //沿束余波：灼落的血珠雨(衰减期更密，车道上留下的湿痕)
            int dropChance = Timer >= collapseStart ? 2 : 4;
            if (Main.rand.NextBool(dropChance)) {
                Vector2 dropPos = Projectile.Center + beamDir * beamLength * Main.rand.NextFloat(0.05f, 0.9f);
                if (WofMotionFX.OnScreen(dropPos, 60f)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(dropPos,
                        new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(1.5f, 4f)),
                        Color.Lerp(WofMotionFX.BloodMid, WofRotEyeBudProj.RotFlesh, Main.rand.NextFloat(0.4f)),
                        Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(18, 30), 0.32f);
                }
            }
        }

        /// <summary>张开完成才有伤害，衰减期无伤(伤害窗=满宽束体)</summary>
        public override bool? CanDamage() {
            return Timer > ExpandTime && Timer <= ExpandTime + SustainTime ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLength,
                WofDirector.GuillotineHalfHeight, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Bleeding, 300);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (beamWidth <= 1f || beamLength <= 10f) {
                return;
            }
            Effect effect = EffectLoader.WofRetinaBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 tip = Projectile.Center + dir * beamLength;
            //起始边后撤埋进墙体：着色器根部生长段藏住平切
            float backBleed = beamWidth * 0.4f + 30f;
            Vector2 origin = Projectile.Center - dir * backBleed;
            float halfW = beamWidth * 2.6f;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((origin + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[1] = new VertexPositionColorTexture((origin - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            //预乘AlphaBlend：着色器输出暗血鞘遮挡背景，亮芯嵌在暗体内(契约4)
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(opacity);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.137f % 1f);
            effect.Parameters["uScanTurn"]?.SetValue(0.35f);
            effect.Parameters["uQuadLen"]?.SetValue(beamLength + backBleed);
            //斩束不扫掠：显式归零，Effect 实例与扫描束共享，参数会跨绘制残留
            effect.Parameters["uBend"]?.SetValue(0f);
            //噪声显式绑到 s1（shader 内 register(s1)）
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (beamWidth <= 1f) {
                return;
            }
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float flicker = 1f + 0.08f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 30f);

            //口部聚光：真加色批，A随强度走；束体喉口辉在 shader 内，此处贴腐眼尺寸
            Main.EntitySpriteDraw(glow, screenPos, null, WofMotionFX.BloodHot * (0.85f * opacity),
                0f, glow.Size() / 2f, 1.5f * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenPos, null, new Color(255, 200, 160) * (0.65f * opacity),
                0f, glow.Size() / 2f, 0.8f, SpriteEffects.None, 0);
        }
    }
}
