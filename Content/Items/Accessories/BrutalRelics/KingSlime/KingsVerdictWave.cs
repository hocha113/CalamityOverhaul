using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.KingSlime
{
    /// <summary>
    /// 王之裁决震荡环：坠击落地的伤害载体。ai[0]=真伤害(float整数) ai[1]=满径px。<br/>
    /// 所有者端生成，波前扫过即命中一次(一次性免疫)；伤害经ai槽携带绕开生成包short截断，
    /// 各端首个AI帧写回damage。可视扩散环由 <see cref="Projectiles.BKSShockwaveProj"/> 承担，
    /// 本体只画落点王冠封印与裁决光柱
    /// </summary>
    internal class KingsVerdictWave : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Life = 26;

        private float Radius => Projectile.ai[1] <= 0f ? 300f : Projectile.ai[1];

        private float Progress => 1f - Projectile.timeLeft / (float)Life;

        /// <summary>波前当前半径，快出慢收</summary>
        private float CurrentR => Radius * VaultUtils.EaseOutCubic(Progress);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //波前扫过每个敌人只结算一次
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //真伤害从ai[0]写回(每端一致；命中在所有者端结算)
                Projectile.damage = Math.Max((int)Projectile.ai[0], 1);
                ImpactBurst();
            }

            Lighting.AddLight(Projectile.Center,
                KingSlimeGelFX.CrownGold.ToVector3() * (1.4f * (1f - Progress)));
        }

        /// <summary>落地拍(各端本地一次)：重响+飞溅+金屑+距离衰减屏震</summary>
        private void ImpactBurst() {
            if (VaultUtils.isServer) {
                return;
            }

            float power = MathHelper.Clamp(10f + Radius * 0.012f, 10f, 22f);
            if (KingSlimeGelFX.OnScreen(Projectile.Center, 900f)) {
                KingSlimeGelFX.ThudSound(Projectile.Center, power);
                KingSlimeGelFX.CrownChime(Projectile.Center, -0.3f, 1f);
                KingSlimeGelFX.LandingBurst(Projectile.Center, power, 1.2f);
                KingSlimeGelFX.GoldGlint(Projectile.Center, 24, 9f);
                KingSlimeGelFX.BubbleFizz(Projectile.Center - new Vector2(0f, 14f), 70f, 8);
            }

            //屏震走CWR接口，距离衰减+克制上限
            float dist = Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center);
            if (dist < 1500f) {
                float strength = MathHelper.Clamp(4f + (Radius - 300f) * 0.007f, 4f, 9f);
                Main.LocalPlayer.CWR().GetScreenShake(strength * (1f - dist / 1500f));
            }
        }

        /// <summary>波前扫过判定：目标最近点进入当前半径即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 closest = new Vector2(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(closest, Projectile.Center) <= CurrentR;
        }

        /// <summary>击退沿波前径向</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = target.Center.X >= Projectile.Center.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            KingSlimeGelFX.GelSplatter(target.Center, new Vector2(0f, -1f), 5, 5f);
        }

        public override bool PreDraw(ref Color lightColor) {
            float t = Progress;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //落点王冠封印：砸入地面的金冠虚影，弹一下再定住淡出
            Main.instance.LoadGore(GoreID.KingSlimeCrown);
            Texture2D crown = TextureAssets.Gore[GoreID.KingSlimeCrown].Value;
            float stampT = MathHelper.Clamp(t / 0.62f, 0f, 1f);
            float stampScale = MathHelper.Lerp(1.4f, 1f, VaultUtils.EaseOutCubic(stampT));
            float stampAlpha = (1f - stampT) * 0.9f;
            Vector2 stampPos = pos + new Vector2(0f, -14f);
            Vector2 origin = crown.Size() * 0.5f;
            Main.EntitySpriteDraw(crown, stampPos, null, Color.White * stampAlpha, 0f,
                origin, stampScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(crown, stampPos, null,
                KingSlimeGelFX.CrownGold with { A = 0 } * stampAlpha, 0f,
                origin, stampScale * 1.06f, SpriteEffects.None, 0);

            //裁决光柱：落点向上的短命金柱，读作"审判落于此地"
            Effect fx = EffectLoader.BKSCrownFX?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx != null && noise != null && t < 0.5f) {
                float columnAlpha = 1f - t / 0.5f;
                fx.CurrentTechnique = fx.Techniques["GuideTech"];
                fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.211f % 1f);
                fx.Parameters["uOpacity"]?.SetValue(0.85f * columnAlpha);
                fx.Parameters["uProg"]?.SetValue(1f);
                fx.Parameters["uLock"]?.SetValue(1f);

                float halfW = 70f;
                Vector2 top = Projectile.Center + new Vector2(0f, -300f);
                Vector2 bottom = Projectile.Center + new Vector2(0f, 16f);
                DrawColumnQuad(fx, noise, top, bottom, halfW);
            }
            return false;
        }

        /// <summary>uv.y 0=顶端 1=地面端的加色quad(设备状态自管)</summary>
        private static void DrawColumnQuad(Effect fx, Texture2D noise, Vector2 top, Vector2 bottom, float halfW) {
            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            DepthStencilState prevDepth = device.DepthStencilState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.None;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture(new Vector3(top.X - halfW, top.Y, 0f), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture(new Vector3(top.X + halfW, top.Y, 0f), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture(new Vector3(bottom.X - halfW, bottom.Y, 0f), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture(new Vector3(bottom.X + halfW, bottom.Y, 0f), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
            device.DepthStencilState = prevDepth;
        }
    }
}
