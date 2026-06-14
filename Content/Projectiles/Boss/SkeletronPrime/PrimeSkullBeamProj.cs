using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    /// <summary>
    /// 颅骨主炮巨型扫射光束：锚定头部，固定角速度横扫
    /// <br/>ai[0] = 头部 NPC 的 whoAmI
    /// <br/>ai[1] = 起始角（弧度）
    /// <br/>ai[2] = 每帧扫射角速度（含方向）
    /// <br/>展开/收束缓动，未完全展开无伤害；头部失效或脱离主炮状态时快速收束
    /// </summary>
    internal class PrimeSkullBeamProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder2;

        internal static int ExpandTime => 16;
        internal static int SweepFrames => 160;
        internal static int CollapseTime => 14;
        internal static int TotalLife => ExpandTime + SweepFrames + CollapseTime;

        private static float MaxBeamLength => 2600f;
        private static float MaxWidth => 64f;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Head => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;

        private float beamWidth;
        private float beamLength;

        private static Color ThemeColor => new(255, 86, 22);
        private static Color ThemeGlow => new(255, 212, 120);

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

        public override void AI() {
            NPC head = Head;

            //头部失效或已不在主炮状态：快进到收束段
            bool hostValid = head.Alives() && head.type == NPCID.SkeletronPrime
                && (int)head.ai[PrimeAiSlots.HeadStateSlot] == (int)PrimeStateIndex.SkullCannon;
            if (!hostValid && Timer < TotalLife - CollapseTime) {
                Timer = TotalLife - CollapseTime;
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1f, Pitch = -0.35f, MaxInstances = 3 }, Projectile.Center);
            }

            //扫射角：展开期定格起始角 → 匀速横扫 → 收束期定格末角
            float sweepT = MathHelper.Clamp(Timer - ExpandTime, 0f, SweepFrames);
            Projectile.rotation = Projectile.ai[1] + Projectile.ai[2] * sweepT;

            if (head.Alives()) {
                Projectile.Center = head.Center + Projectile.rotation.ToRotationVector2() * 44f;
            }

            //宽度展开/收束缓动
            float collapseStart = TotalLife - CollapseTime;
            if (Timer < ExpandTime) {
                float t = Timer / ExpandTime;
                beamWidth = MathHelper.Lerp(3f, MaxWidth, VaultUtils.EaseOutCubic(t));
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
            beamWidth *= 1f + 0.06f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 36f);

            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            //沿束光照
            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            for (int i = 0; i < 6; i++) {
                Lighting.AddLight(Projectile.Center + beamDir * (beamLength / 6f * i), ThemeColor.ToVector3() * 0.8f);
            }

            if (VaultUtils.isServer || beamWidth < MaxWidth * 0.3f) {
                return;
            }

            //全功率期间的低频震屏
            if ((int)Timer % 7 == 0) {
                PrimeDeathPerformancePlayer.RequestShake(3.2f, 6);
            }

            //沿线飞溅火花
            if (Main.rand.NextBool(2)) {
                float along = Main.rand.NextFloat();
                Vector2 sparkPos = Projectile.Center + beamDir * beamLength * along
                    + beamDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-beamWidth * 0.4f, beamWidth * 0.4f);
                PRTLoader.NewParticle<PRT_Spark>(sparkPos,
                    beamDir.RotatedBy(Main.rand.NextFloat(-0.4f, 0.4f)) * Main.rand.NextFloat(3f, 8f),
                    Color.Gold, Main.rand.NextFloat(1f, 1.6f))?.Configure(false, 16);
            }

            //枪口聚能（向心汇聚）
            if (Main.rand.NextBool(3)) {
                Vector2 gatherPos = Projectile.Center + Main.rand.NextVector2CircularEdge(70f, 70f);
                PRTLoader.NewParticle<PRT_Spark>(gatherPos,
                    (Projectile.Center - gatherPos) * 0.11f,
                    Color.OrangeRed, Main.rand.NextFloat(1.1f, 1.7f))?.Configure(false, 14);
            }
        }

        //未完全展开时不造成伤害，给玩家反应窗口
        public override bool? CanDamage() => Timer > ExpandTime ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + Projectile.rotation.ToRotationVector2() * beamLength,
                beamWidth * 0.7f, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 90);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (beamWidth <= 0.5f || beamLength <= 10f) {
                return false;
            }

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation;
            float flicker = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 43f);

            Color outer = ThemeColor with { A = 0 };
            Color mid = ThemeGlow with { A = 0 };
            Color core = Color.White with { A = 0 };

            if (EffectLoader.PrimeSkullBeam?.Value != null) {
                DrawShaderBeam(rot);
            }
            else {
                DrawFallbackBeam(drawPos, rot, outer, mid, core, flicker);
            }

            //枪口辉光：多层呼吸光球 + 十字星闪
            float muzzleScale = beamWidth / MaxWidth;
            Main.EntitySpriteDraw(glow, drawPos, null, outer * 0.95f, 0f, glow.Size() / 2f,
                muzzleScale * 2.2f * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, core, 0f, glow.Size() / 2f,
                muzzleScale * 1.05f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, mid * 0.85f, Main.GlobalTimeWrappedHourly * 3.4f,
                star.Size() / 2f, muzzleScale * 0.6f * flicker, SpriteEffects.None, 0);

            return false;
        }

        private void DrawShaderBeam(float rot) {
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Effect shader = EffectLoader.PrimeSkullBeam.Value;
            float expandProgress = MathHelper.Clamp(Timer / ExpandTime, 0f, 1f);
            shader.Parameters["uColor"]?.SetValue(ThemeColor.ToVector3());
            shader.Parameters["uSecondaryColor"]?.SetValue(ThemeGlow.ToVector3());
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly * 1.7f);
            shader.Parameters["uOpacity"]?.SetValue(MathHelper.Clamp(beamWidth / MaxWidth, 0f, 1f));
            shader.Parameters["uIntensity"]?.SetValue(1.1f);
            shader.Parameters["uExpandProgress"]?.SetValue(expandProgress);
            shader.Parameters["uImage1"]?.SetValue(CWRAsset.Extra_193.Value);
            shader.Parameters["uImage2"]?.SetValue(CWRAsset.PerlinNoise.Value);
            shader.CurrentTechnique.Passes[0].Apply();

            Texture2D quad = CWRAsset.Placeholder_White.Value;
            //视觉宽度大于碰撞宽度，撕裂边缘需要余量
            float visualWidth = beamWidth * 3.6f;
            sb.Draw(quad, Projectile.Center - Main.screenPosition, null, Color.White, rot,
                new Vector2(0, quad.Height / 2f),
                new Vector2(beamLength / quad.Width, visualWidth / quad.Height),
                SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawFallbackBeam(Vector2 drawPos, float rot, Color outer, Color mid, Color core, float flicker) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            Vector2 lineOrigin = new(0, line.Height / 2f);
            float lenScale = beamLength / line.Width;

            Main.EntitySpriteDraw(line, drawPos, null, outer * 0.45f, rot, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 3.2f * flicker), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, Color.Lerp(outer, mid, 0.5f) * 0.85f, rot, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 1.7f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, core * 0.95f, rot, lineOrigin,
                new Vector2(lenScale, beamWidth / line.Height * 0.8f * flicker), SpriteEffects.None, 0);
        }
    }
}
