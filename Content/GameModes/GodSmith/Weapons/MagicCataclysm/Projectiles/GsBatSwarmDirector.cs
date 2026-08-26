using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 蝙蝠权杖灾变「万蝠临渊」：跟随玩家。蓄势 40t 头顶月轮虚影渐显、蝠鸣渐密；
    /// 爆发 120t 共 8 波、每波 5 只原版蝙蝠自玩家外环扑向场内敌（×0.8）；
    /// 余韵 90t 蝠群绕体护环（触敌 ×0.3）
    /// </summary>
    internal class GsBatSwarmDirector : GsCataclysmDirectorProj
    {
        public override int OmenTicks => 40;
        public override int MainTicks => 120;
        public override int AftermathTicks => 90;

        protected override bool FollowOwner => true;

        protected override int HitTickRate => 20;

        protected override float TickDamageMul => 0.3f;

        /// <summary>余韵护环半径</summary>
        private const float GuardRadius = 88f;
        /// <summary>月轮悬于头顶的偏移</summary>
        private static readonly Vector2 MoonOffset = new(0f, -150f);

        [VaultLoaden(CWRConstant.Masking + "Extra_98")]
        internal static Asset<Texture2D> DarkTex = null;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        internal static readonly Color DuskViolet = new(120, 90, 165);
        internal static readonly Color MoonPale = new(235, 215, 170);

        private static int BatType => ContentSamples.ItemsByType[ItemID.BatScepter].shoot;

        private float MoonEnvelope() {
            if (Phase == 0) {
                return VaultUtils.EaseOutQuad(Elapsed / (float)OmenTicks);
            }
            if (Phase == 1) {
                return 1f;
            }
            return MathHelper.Clamp(1f - (Elapsed - OmenTicks - MainTicks) / (float)AftermathTicks, 0f, 1f);
        }

        protected override void OmenUpdate(int t) {
            //蝠鸣渐密：间隔从 14t 收到 7t，音调渐升
            int interval = 14 - t / 8;
            if (!VaultUtils.isServer && t % Math.Max(interval, 7) == 0) {
                SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.3f, Pitch = -0.3f + t * 0.012f }, Projectile.Center);
            }
            if (!VaultUtils.isServer && t % 4 == 0) {
                Vector2 moonPos = Projectile.Center + MoonOffset;
                PRTLoader.NewParticle<PRT_Smoke>(moonPos + Main.rand.NextVector2Circular(40f, 40f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.2f, 0.7f)),
                    DuskViolet * 0.8f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(30, 0.5f, 0.02f);
            }
            Lighting.AddLight(Projectile.Center + MoonOffset, MoonPale.ToVector3() * 0.4f * MoonEnvelope());
        }

        protected override void MainUpdate(int t) {
            Lighting.AddLight(Projectile.Center + MoonOffset, MoonPale.ToVector3() * 0.5f);
            if (t % 15 != 0) {
                return;
            }
            //每 15t 一波蝠群（共 8 波），全端听声、owner 端生成
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.42f, Pitch = 0.1f + (t / 15) * 0.03f }, Projectile.Center);
            }
            if (!OwnerSide) {
                return;
            }
            Vector2 targetPos = FindSwarmTarget();
            for (int i = 0; i < 5; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawn = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(460f, 580f);
                Vector2 vel = (targetPos + Main.rand.NextVector2Circular(50f, 50f) - spawn)
                    .SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(9.5f, 11.5f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawn, vel,
                    BatType, ScaledDamage(0.8f), Projectile.knockBack, Projectile.owner);
            }
        }

        /// <summary>owner 端选目标：750px 内随机可追踪敌，无则朝光标</summary>
        private Vector2 FindSwarmTarget() {
            int count = 0;
            int picked = -1;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || !npc.CanBeChasedBy() || npc.type == NPCID.TargetDummy) {
                    continue;
                }
                if (Vector2.Distance(npc.Center, Projectile.Center) > 750f) {
                    continue;
                }
                count++;
                //蓄水池抽样：等概率取一只
                if (Main.rand.Next(count) == 0) {
                    picked = i;
                }
            }
            return picked >= 0 ? Main.npc[picked].Center : Main.MouseWorld;
        }

        protected override void AftermathUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.4f, Pitch = -0.2f }, Projectile.Center);
            }
            if (!VaultUtils.isServer && t % 6 == 0) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + angle.ToRotationVector2() * GuardRadius,
                    angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.2f,
                    DuskViolet * 0.7f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(24, 0.45f, 0.03f);
            }
        }

        /// <summary>只有余韵护环有自身判定（爆发伤害全在子蝠）</summary>
        public override bool? CanDamage() => Phase == 2 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != 2) {
                return false;
            }
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return Math.Abs(dist - GuardRadius) < 26f + Math.Min(targetHitbox.Width, targetHitbox.Height) * 0.5f;
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = MoonEnvelope();
            Texture2D dark = DarkTex?.Value;
            Texture2D glow = GlowTex?.Value;
            if (env > 0.02f && dark != null && glow != null) {
                Vector2 moonPos = Projectile.Center + MoonOffset - Main.screenPosition;
                //月晕（加色）与暗月轮（真 alpha 压暗）
                Main.EntitySpriteDraw(glow, moonPos, null, MoonPale with { A = 0 } * (0.4f * env), 0f,
                    glow.Size() * 0.5f, 150f / glow.Width, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(dark, moonPos, null, new Color(26, 16, 44) * (0.88f * env),
                    Projectile.identity * 0.4f, dark.Size() * 0.5f, 96f / dark.Width, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, moonPos, null, MoonPale with { A = 0 } * (0.55f * env), 0f,
                    glow.Size() * 0.5f, 62f / glow.Width, SpriteEffects.None, 0);
            }

            //余韵：五只蝠影绕体护环
            if (Phase == 2) {
                int batType = BatType;
                Main.instance.LoadProjectile(batType);
                Texture2D batTex = TextureAssets.Projectile[batType].Value;
                int frames = Math.Max(1, Main.projFrames[batType]);
                int frameH = batTex.Height / frames;
                float fade = MathHelper.Clamp(1f - (Elapsed - OmenTicks - MainTicks) / (float)AftermathTicks, 0f, 1f);
                for (int i = 0; i < 5; i++) {
                    float angle = MathHelper.TwoPi / 5f * i + Timer * 0.055f;
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * GuardRadius - Main.screenPosition;
                    int frame = (Elapsed / 5 + i) % frames;
                    Rectangle src = new(0, frameH * frame, batTex.Width, frameH);
                    bool flip = Math.Cos(angle + MathHelper.PiOver2) < 0;
                    Main.EntitySpriteDraw(batTex, pos, src, DuskViolet * (0.85f * fade), 0f,
                        new Vector2(batTex.Width, frameH) * 0.5f, 1f,
                        flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
