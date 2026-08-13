using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 剑雨阵光剑：编队悬停瞄准→反向蓄势→齐射；
    /// ai[0]=悬停帧数（错拍齐射）ai[1]=色相 ai[2]=锁定玩家索引
    /// 发射角在各端由同步的玩家位置确定性推得，服务端 netUpdate 校正
    /// </summary>
    internal class EmpressBlade : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const float LaunchSpeed = 34f;
        private const float BladeLength = 132f;
        private const int FlyLife = 70;
        private const int ReelFrames = 4;

        private ref float Timer => ref Projectile.localAI[0];
        private int HoverTime => Math.Max((int)Projectile.ai[0], 20);
        private float Hue => Projectile.ai[1];
        private int TargetIndex => (int)Projectile.ai[2];

        private bool Launched => Timer > HoverTime;
        private Vector2 anchorPos;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 7;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        private Player AimTarget {
            get {
                if (TargetIndex >= 0 && TargetIndex < Main.maxPlayers) {
                    Player p = Main.player[TargetIndex];
                    if (p.active && !p.dead) {
                        return p;
                    }
                }
                return null;
            }
        }

        public override void AI() {
            if (Timer == 0f) {
                Projectile.timeLeft = HoverTime + FlyLife;
                anchorPos = Projectile.Center;
                //初始朝向目标
                Player t0 = AimTarget;
                Projectile.rotation = t0 != null
                    ? (t0.Center - Projectile.Center).ToRotation()
                    : Projectile.velocity.SafeNormalize(Vector2.UnitY).ToRotation();
            }
            Timer++;

            Player target = AimTarget;

            if (!Launched) {
                float hoverT = Timer / (float)HoverTime;
                Projectile.Opacity = MathHelper.Clamp(Timer / 12f, 0f, 1f);

                //悬停瞄准：限转速追角，读得出"正在锁你"
                if (target != null) {
                    float desired = (target.Center - Projectile.Center).ToRotation();
                    Projectile.rotation = Projectile.rotation.AngleTowards(desired, 0.11f);
                }

                //呼吸浮动+发射前反向蓄势（pow迟滞回吸）；identity跨端一致，whoAmI是本地槽位
                Vector2 breathing = EmpressMotion.Breathing(Projectile.identity * 0.61f, 6f);
                float reel = (float)Math.Pow(MathHelper.Clamp(hoverT, 0f, 1f), 8) * 26f;
                Projectile.Center = anchorPos + breathing - Projectile.rotation.ToRotationVector2() * reel;
                Projectile.velocity = Vector2.Zero;

                //发射前quiver：末4帧微颤
                if (Timer > HoverTime - ReelFrames) {
                    Projectile.Center += Main.rand.NextVector2Circular(1.5f, 1.5f);
                }
            }
            else {
                if (Timer == HoverTime + 1) {
                    //一帧点火：直取目标当前位（确定性），服务端补一发同步校正
                    Vector2 aim = target != null
                        ? (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY)
                        : Projectile.rotation.ToRotationVector2();
                    Projectile.velocity = aim * LaunchSpeed;
                    Projectile.rotation = aim.ToRotation();
                    if (!VaultUtils.isClient) {
                        Projectile.netUpdate = true;
                    }
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item163 with { Volume = 0.42f, Pitch = 0.2f + Hue * 0.3f, MaxInstances = 5 }, Projectile.Center);
                        PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero, Color.White, 0.42f)?
                            .Configure(12, Hue);
                    }
                }
                Projectile.rotation = Projectile.velocity.ToRotation();
                Projectile.Opacity = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);

                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_EmpressSpark>(
                        Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(BladeLength),
                        Main.rand.NextVector2Circular(1f, 1f), Main.hslToRgb(Hue, 1f, 0.6f),
                        Main.rand.NextFloat(0.5f, 0.85f))?.Configure(12, Hue);
                }
            }

            Lighting.AddLight(Projectile.Center, Main.hslToRgb(Hue, 1f, 0.55f).ToVector3() * 0.4f * Projectile.Opacity);
        }

        //余韵：剑体碎成沿刃的光屑扇
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer || !Launched) {
                return;
            }
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            //被整场清弹（转阶段/大招/死亡）时降载，防同帧粒子风暴
            if (timeLeft > 4) {
                if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center - dir * BladeLength * 0.4f,
                        VaultUtils.RandVr(1f, 3f), Main.hslToRgb(Hue, 1f, 0.64f),
                        Main.rand.NextFloat(0.45f, 0.75f))?.Configure(12, Hue);
                }
                return;
            }
            for (int i = 0; i < 5; i++) {
                float along = i / 5f;
                PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center - dir * BladeLength * along,
                    dir * 2.5f + Main.rand.NextVector2Circular(1.8f, 1.8f),
                    Main.hslToRgb((Hue + along * 0.1f) % 1f, 1f, 0.64f),
                    Main.rand.NextFloat(0.45f, 0.8f))?.Configure(14, Hue);
            }
        }

        //悬停期不结算伤害
        public override bool? CanDamage() => Launched ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Launched) {
                return false;
            }
            float p = 0f;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center + dir * 20f, Projectile.Center - dir * BladeLength * 0.8f, 18f, ref p);
        }

        public override bool PreDraw(ref Color lightColor) {
            //光剑体走着色器，此处仅剑柄辉点与悬停轨迹备份
            Effect effect = EffectLoader.EmpressLanceBeam?.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color prism = Main.hslToRgb(Hue, 1f, 0.62f) with { A = 0 };

            if (effect == null) {
                //后备：细长星条
                Texture2D star = CWRAsset.StarTexture_White.Value;
                Main.spriteBatch.Draw(star, drawPos, null, prism * Projectile.Opacity, Projectile.rotation,
                    star.Size() / 2f, new Vector2(0.4f, 0.06f), SpriteEffects.None, 0);
                return false;
            }

            //残影，零值轨迹点跳过
            if (Launched) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 old = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float k = 1f - i / (float)Projectile.oldPos.Length;
                    Main.spriteBatch.Draw(glow, old, null, prism * (0.2f * k * Projectile.Opacity), 0f,
                        glow.Size() / 2f, 0.5f * k, SpriteEffects.None, 0);
                }
            }
            Main.spriteBatch.Draw(glow, drawPos, null, prism * (0.55f * Projectile.Opacity), 0f,
                glow.Size() / 2f, 0.5f, SpriteEffects.None, 0);
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Effect effect = EffectLoader.EmpressLanceBeam?.Value;
            if (effect == null) {
                return;
            }
            EffectTechnique tech = effect.Techniques["BladeTech"];
            if (tech == null) {
                return;
            }

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.CurrentTechnique = tech;
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uHue"]?.SetValue(Hue);
            effect.Parameters["uProgress"]?.SetValue(Launched ? 1f : Timer / (float)HoverTime);
            effect.Parameters["uOpacity"]?.SetValue(Projectile.Opacity);

            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float halfW = 30f;

            //挥迹残刃：飞行中沿轨迹补两枚衰减的残刃（挥砍的光谱涂抹）
            if (Launched) {
                for (int g = 2; g <= 4; g += 2) {
                    if (g >= Projectile.oldPos.Length || Projectile.oldPos[g] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 ghostCenter = Projectile.oldPos[g] + Projectile.Size / 2f;
                    float ghostAlpha = (1f - g / 6f) * 0.34f;
                    DrawBladeQuad(device, effect, tech, ghostCenter, dir, perp, halfW * (1f - g * 0.06f),
                        Projectile.Opacity * ghostAlpha);
                }
            }

            DrawBladeQuad(device, effect, tech, Projectile.Center, dir, perp, halfW, Projectile.Opacity);

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        private void DrawBladeQuad(GraphicsDevice device, Effect effect, EffectTechnique tech,
            Vector2 center, Vector2 dir, Vector2 perp, float halfW, float opacity) {
            effect.Parameters["uOpacity"]?.SetValue(opacity);
            Vector2 tip = center + dir * 28f;
            Vector2 tail = center - dir * BladeLength;
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((tail + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((tail - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));
            foreach (EffectPass pass in tech.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }
    }
}
