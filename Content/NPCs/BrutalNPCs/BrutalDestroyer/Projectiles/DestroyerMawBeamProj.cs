using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Projectiles
{
    /// <summary>口吐光柱慢扫；ai[0]头whoAmI ai[1]起始角 ai[2]角速度；展开期无伤</summary>
    internal class DestroyerMawBeamProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal static int ExpandTime => 18;
        internal static int SweepFrames => 156;
        internal static int CollapseTime => 16;
        internal static int TotalLife => ExpandTime + SweepFrames + CollapseTime;

        /// <summary>口器前伸量</summary>
        internal const float MuzzleOffset = 64f;
        private static float MaxBeamLength => 4500f;
        private static float MaxWidth => 126f;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Head => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;

        private float beamWidth;
        private float beamLength;

        private static Color ThemeBlood => new(255, 50, 24);
        private static Color ThemeGlow => new(255, 150, 70);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3200;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife + 30;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>宿主仍在激光弹幕态，否快进收束</summary>
        private bool HostValid {
            get {
                NPC head = Head;
                return head.Alives() && head.type == NPCID.TheDestroyer
                    && (int)head.ai[2] == (int)DestroyerStateIndex.LaserBarrage;
            }
        }

        /// <summary>激怒宿主，EX 更宽更白</summary>
        private bool IsEnragedHost {
            get {
                NPC head = Head;
                return head.Alives() && head.life * 2 < head.lifeMax;
            }
        }

        /// <summary>按头whoAmI找本束，口器跟权威角</summary>
        internal static Projectile FindFor(int headWhoAmI) {
            int type = ModContent.ProjectileType<DestroyerMawBeamProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == type && (int)p.ai[0] == headWhoAmI) {
                    return p;
                }
            }
            return null;
        }

        public override void AI() {
            NPC head = Head;

            //宿主失效快进收束
            if (!HostValid && Timer < TotalLife - CollapseTime) {
                Timer = TotalLife - CollapseTime;
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);
            }

            //展开定格→横扫→收束定格
            float sweepT = MathHelper.Clamp(Timer - ExpandTime, 0f, SweepFrames);
            float beamAngle = Projectile.ai[1] + Projectile.ai[2] * sweepT;
            Projectile.rotation = beamAngle;

            if (head.Alives()) {
                Projectile.Center = head.Center + beamAngle.ToRotationVector2() * MuzzleOffset;
            }

            //宽长缓动
            float collapseStart = TotalLife - CollapseTime;
            if (Timer < ExpandTime) {
                float t = Timer / ExpandTime;
                beamWidth = MathHelper.Lerp(4f, MaxWidth, VaultUtils.EaseOutCubic(t));
                beamLength = MathHelper.Lerp(0f, MaxBeamLength, VaultUtils.EaseOutQuad(t));
            }
            else if (Timer >= collapseStart) {
                float t = (Timer - collapseStart) / CollapseTime;
                beamWidth = MathHelper.Lerp(MaxWidth, 0f, VaultUtils.EaseInQuad(t));
                beamLength = MaxBeamLength;
            }
            else {
                beamWidth = MaxWidth;
                beamLength = MaxBeamLength;
            }
            beamWidth *= 1f + 0.05f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 30f);

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 7; i++) {
                Lighting.AddLight(Projectile.Center + beamDir * (beamLength / 7f * i), ThemeBlood.ToVector3() * 0.85f);
            }

            if (VaultUtils.isServer || beamWidth < MaxWidth * 0.3f) {
                return;
            }

            //低频震屏，同id刷新
            if ((int)Timer % 6 == 0) {
                DestroyerMotionFX.CameraPunch(Projectile.Center, 2.4f, 8, "DestroyerMawBeamRumble", beamDir);
            }

            //沿束熔滴+余烬
            if (Main.rand.NextBool(2)) {
                float along = Main.rand.NextFloat();
                Vector2 sparkPos = Projectile.Center + beamDir * beamLength * along
                    + beamDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.45f, beamWidth * 0.45f);
                PRTLoader.NewParticle<PRT_Spark>(sparkPos,
                    beamDir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(3f, 9f),
                    Color.Lerp(ThemeGlow, Color.White, Main.rand.NextFloat()), Main.rand.NextFloat(1f, 1.7f))
                    ?.Configure(true, Main.rand.Next(14, 22));
            }
            if (Main.rand.NextBool(3)) {
                float along = Main.rand.NextFloat();
                Vector2 emberPos = Projectile.Center + beamDir * beamLength * along;
                PRTLoader.NewParticle<PRT_LavaFire>(emberPos,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(0f, 2.5f)),
                    Color.White, Main.rand.NextFloat(0.8f, 1.4f))?.SetLifetime(20, 40);
            }

            //口器向心聚能
            if (Main.rand.NextBool(2)) {
                Vector2 gatherPos = Projectile.Center + Main.rand.NextVector2CircularEdge(80f, 80f);
                PRTLoader.NewParticle<PRT_Spark>(gatherPos,
                    (Projectile.Center - gatherPos) * 0.12f,
                    ThemeBlood, Main.rand.NextFloat(1.1f, 1.8f))?.Configure(false, 14);
            }
        }

        //展开完才可伤
        public override bool? CanDamage() => Timer > ExpandTime ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            //碰撞比视觉窄
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLength,
                beamWidth * 0.6f, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 120);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>近端 bleed，藏硬切边进头雕</summary>
        private float MuzzleBackBleed => beamWidth * 0.38f + 58f;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (beamWidth <= 1f || beamLength <= 10f) {
                return;
            }

            bool ex = IsEnragedHost;
            float opacity = MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f);

            Effect effect = EffectLoader.DestroyerBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect != null && noise != null) {
                DrawShaderBeam(effect, noise, opacity, ex);
            }
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            DrawAdditiveDressing(MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f), IsEnragedHost);
        }

        /// <summary>DestroyerBeam.fx 主轴+电弧+脉冲</summary>
        private void DrawShaderBeam(Effect effect, Texture2D noise, float opacity, bool ex) {
            Vector2 mouth = Projectile.Center;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            Vector2 tip = mouth + dir * beamLength;
            //近端 bleed 进头
            float backBleed = MuzzleBackBleed;
            Vector2 origin = mouth - dir * backBleed;
            //视觉半宽含电弧/halo 余量
            float halfW = beamWidth * (ex ? 3.4f : 3.0f);

            //uv.x 1口器→0末端；uv.y 横截面；origin 在头后 backBleed
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((origin + perp * halfW).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[1] = new VertexPositionColorTexture((origin - perp * halfW).ToVector3(), Color.White, new Vector2(1f, 1f));
            verts[2] = new VertexPositionColorTexture((tip + perp * halfW).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[3] = new VertexPositionColorTexture((tip - perp * halfW).ToVector3(), Color.White, new Vector2(0f, 1f));

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["fadeAlpha"]?.SetValue(opacity);
            effect.Parameters["exMode"]?.SetValue(ex ? 1f : 0f);
            effect.Parameters["seed"]?.SetValue(Projectile.whoAmI * 0.137f % 1f);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>口器光球/星闪/头桥接，圆点无硬切</summary>
        private void DrawAdditiveDressing(float opacity, bool ex) {
            Texture2D glow = CWRAsset.DiffusionCircle.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            float rot = Projectile.rotation;
            Vector2 dir = rot.ToRotationVector2();
            float flicker = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 40f);

            Color blood = ThemeBlood;
            Color amber = ThemeGlow;
            Color core = Color.White;

            //宽晕已在着色器 halo，这里只补圆点
            //口器→末端推进光球
            Vector2 screenMouth = Projectile.Center - Main.screenPosition;
            const int pulses = 4;
            for (int i = 0; i < pulses; i++) {
                float along = (Main.GlobalTimeWrappedHourly * 0.9f + i / (float)pulses) % 1f;
                Vector2 pPos = screenMouth + dir * beamLength * along;
                float pScale = beamWidth / MaxWidth * (0.5f + 0.5f * (float)Math.Sin(along * MathHelper.Pi));
                Main.EntitySpriteDraw(glow, pPos, null, amber * (0.7f * opacity), 0f, glow.Size() / 2f,
                    pScale * (ex ? 1.5f : 1.1f) * 0.3f, SpriteEffects.None, 0);
            }

            //口器呼吸球+星闪
            float muzzleScale = beamWidth / MaxWidth;
            Main.EntitySpriteDraw(glow, screenMouth, null, blood * (0.95f * opacity), 0f, glow.Size() / 2f,
                muzzleScale * (ex ? 3f : 2.4f) * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenMouth, null, amber * opacity, 0f, glow.Size() / 2f,
                muzzleScale * 1.4f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenMouth, null, core * opacity, 0f, glow.Size() / 2f,
                muzzleScale * 0.85f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, screenMouth, null, amber * (0.9f * opacity), Main.GlobalTimeWrappedHourly * 3.2f,
                star.Size() / 2f, muzzleScale * 0.8f * flicker, SpriteEffects.None, 0);

            //头心桥接，吃近端硬边
            NPC head = Head;
            if (head.Alives()) {
                Vector2 headPos = head.Center - Main.screenPosition;
                float bridge = muzzleScale * (ex ? 2.6f : 2.1f);
                Main.EntitySpriteDraw(glow, headPos, null, blood * (0.55f * opacity), 0f, glow.Size() / 2f,
                    bridge, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, headPos, null, core * (0.35f * opacity), 0f, glow.Size() / 2f,
                    bridge * 0.45f, SpriteEffects.None, 0);
            }
        }
    }
}
