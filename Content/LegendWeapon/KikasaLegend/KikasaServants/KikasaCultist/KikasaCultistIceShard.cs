using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaCultist
{
    /// <summary>
    /// 鬼奴邪教徒的血冰簇弹：血湖之水冻成的细长晶刃，激发帧自虚影阵列齐射。
    /// 出膛短暂复利加速（激发的锐气），中段泄劲、尾段微坠；
    /// 命中/贴壁/超时皆碎裂——冰屑四溅里裹着几粒解冻的血珠。落回血湖则被湖收走
    /// </summary>
    internal class KikasaCultistIceShard : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int AccelFrames = 10;
        private const int SinkStart = 34;

        private ref float Life => ref Projectile.localAI[0];

        private bool shattered;
        private bool lakeSwallowed;

        private float Seed => Projectile.identity * 0.7391f % 4.7f;

        /// <summary>出生 3 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 3f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //激发后短暂复利续力，随后泄劲；尾段被寒重拽落
            if (Life <= AccelFrames) {
                Projectile.velocity *= 1.012f;
            }
            else {
                Projectile.velocity *= 0.996f;
            }
            if (Life > SinkStart) {
                Projectile.velocity.Y += 0.06f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //冰尘尾迹：细小的寒芒缓落
            if (!Main.dedServ && Life % 3 == 1) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(4f, 4f),
                    -Projectile.velocity * 0.06f + new Vector2(0f, Main.rand.NextFloat(0.2f, 0.7f)),
                    KikasaCultistServant.IceTint * Main.rand.NextFloat(0.35f, 0.5f),
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(Main.rand.Next(10, 18), 0f);
            }

            float glow = 0.4f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.28f * glow, 0.4f * glow, 0.5f * glow);

            //落回血湖：湖收回自己的血，不碎裂
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.6f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 3);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.35f, Pitch = -0.15f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        //==================== 碎裂 ====================

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Shatter(oldVelocity);
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit5 with { Volume = 0.4f, Pitch = 0.15f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (lakeSwallowed) {
                return;
            }
            Shatter(Projectile.velocity);
        }

        /// <summary>碎裂：冰屑扇 + 解冻血珠 + 一记清脆碎冰声；晶体死后寒雾多活一拍</summary>
        private void Shatter(Vector2 impactVel) {
            if (shattered) {
                return;
            }
            shattered = true;
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            Vector2 back = -impactVel.SafeNormalize(Vector2.UnitY);
            //冰屑：亮片打着旋飞散
            for (int i = 0; i < 7; i++) {
                Vector2 vel = back.RotatedByRandom(1.2f) * Main.rand.NextFloat(1.5f, 4.5f)
                    + impactVel * 0.08f;
                PRTLoader.NewParticle<PRT_Sparkle>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f), vel,
                    KikasaCultistServant.IceTint, Main.rand.NextFloat(0.22f, 0.4f))
                    ?.Configure(KikasaCultistServant.IceTint * 0.5f, Main.rand.Next(14, 26), 0.2f, 0.7f);
            }
            //解冻的血珠：冰里冻着血
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    back.RotatedByRandom(0.9f) * Main.rand.NextFloat(1f, 3f),
                    KikasaCultistServant.BloodMain * Main.rand.NextFloat(0.45f, 0.6f),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 24), Main.rand.NextFloat(-0.3f, 0.3f));
            }
            //寒雾余韵：比弹体活得久的痕迹
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center, back * 0.4f,
                Color.Lerp(KikasaCultistServant.MistBlood, KikasaCultistServant.IceTint, 0.4f) * 0.6f,
                Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(30, 50));
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float fade = VisualFade;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 gOrigin = glow.Size() * 0.5f;
            float rot = Projectile.rotation;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0.5f, 1.3f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //残影拖尾：速度门控
            if (Projectile.velocity.Length() > 10f) {
                for (int k = Projectile.oldPos.Length - 1; k >= 1; k--) {
                    Vector2 oldCenter = Projectile.oldPos[k] + Projectile.Size * 0.5f;
                    if (Projectile.oldPos[k] == Vector2.Zero) {
                        continue;
                    }
                    float fall = 1f - k / (float)Projectile.oldPos.Length;
                    sb.Draw(glow, oldCenter - Main.screenPosition, null,
                        KikasaCultistServant.IceTint * (0.16f * fall * fade), rot, gOrigin,
                        new Vector2(14f * stretch * 2f / glow.Width, 3f * 2f / glow.Height), SpriteEffects.None, 0f);
                }
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            //血冰晶刃三层：深血压边→寒冰主体→白芯
            sb.Draw(glow, pos, null, KikasaCultistServant.BloodDeep * (0.55f * fade), rot, gOrigin,
                new Vector2(19f * stretch * 2f / glow.Width, 6.5f * 2f / glow.Height), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, KikasaCultistServant.IceTint * (0.85f * fade), rot, gOrigin,
                new Vector2(16f * stretch * 2f / glow.Width, 4.6f * 2f / glow.Height), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, KikasaCultistServant.RuneCore * (0.6f * fade), rot, gOrigin,
                new Vector2(10f * stretch * 2f / glow.Width, 2.2f * 2f / glow.Height), SpriteEffects.None, 0f);
            //横向短棱：晶体的十字截面
            sb.Draw(glow, pos, null, KikasaCultistServant.IceTint * (0.4f * fade), rot + MathHelper.PiOver2, gOrigin,
                new Vector2(6f * 2f / glow.Width, 2.4f * 2f / glow.Height), SpriteEffects.None, 0f);
            //晶面闪烁：确定性相位的冷光眨眼
            float glint = MathF.Max(0f, MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Seed * 5f));
            sb.Draw(glow, pos, null, Color.White * (0.3f * glint * glint * fade), rot, gOrigin,
                new Vector2(5f * 2f / glow.Width), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
