using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Undertows
{
    /// <summary>
    /// 渊棘箭。ai0=0 普通:穿透 2,命中坍缩小空化泡;
    /// ai0=1 渊压重箭:贯穿一切,沿途暗流拖拽,终点引爆 <see cref="UndertowBurst"/>
    /// </summary>
    internal class UndertowSpine : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "UndertowSpine";

        private const int TrailLen = 13;
        private const int MaxLife = 270;

        private bool Heavy => Projectile.ai[0] > 0.5f;
        private int Age => MaxLife - Projectile.timeLeft;
        /// <summary>出膛速度,衰减地板的基准</summary>
        private float InitSpeed { get => Projectile.localAI[0]; set => Projectile.localAI[0] = value; }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.arrow = true;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 3;
            Projectile.timeLeft = MaxLife;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            if (Heavy) {
                Projectile.penetrate = -1;
            }
        }

        public override void AI() {
            if (InitSpeed <= 0.01f) {
                InitSpeed = Projectile.velocity.Length();
            }
            Projectile.SetArrowRot();

            //出膛短推,随后水阻减速到地板,不做匀速直线
            float speed = Projectile.velocity.Length();
            if (Age < 8) {
                Projectile.velocity *= 1.012f;
            }
            else if (Heavy) {
                float floor = InitSpeed * 0.75f;
                if (Age > 26 && speed > floor) {
                    Projectile.velocity *= 0.995f;
                }
            }
            else {
                float floor = InitSpeed * 0.55f;
                if (speed > floor) {
                    Projectile.velocity *= 0.988f;
                }
            }

            //重箭沿途暗流,把近旁敌人往弹道上拽,NPC 权威端结算
            if (Heavy && !VaultUtils.isClient) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.boss || npc.friendly || npc.knockBackResist <= 0f || npc.immortal) {
                        continue;
                    }
                    float dist = npc.Distance(Projectile.Center);
                    if (dist > 96f || dist < 10f) {
                        continue;
                    }
                    npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero)
                        * 0.5f * npc.knockBackResist;
                }
            }

            Lighting.AddLight(Projectile.Center, 0.05f, 0.2f, 0.28f);

            if (VaultUtils.isServer) {
                return;
            }
            if (Age % 3 == 0) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center
                    , -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.5f, 0.5f)
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.2f, Heavy ? 0.5f : 0.36f))
                    .Configure(10, 1.4f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 180);
            if (Heavy) {
                target.AddBuff(ModContent.BuffType<AbyssalPressure>(), 180);
            }
            else if (Projectile.IsOwnedByLocalPlayer()) {
                //普通箭:水尾坍缩成小空化泡
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Center, Vector2.Zero
                    , ModContent.ProjectileType<UndertowBurst>()
                    , Math.Max((int)(Projectile.damage * 0.35f), 1), Projectile.knockBack * 0.5f
                    , Projectile.owner, ai0: 0.5f);
            }
            if (!Main.dedServ) {
                AbyssclaspHitFx(target.Center);
            }
        }

        public override void OnKill(int timeLeft) {
            //重箭终点空化内爆
            if (Heavy && Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<UndertowBurst>()
                    , Projectile.damage, Projectile.knockBack, Projectile.owner, ai0: 1.1f);
            }
            if (Main.dedServ) {
                return;
            }
            int count = Heavy ? 8 : 4;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center
                    , Main.rand.NextVector2Circular(3f, 3f)
                    , AbyssrendFX.Deep, Main.rand.NextFloat(0.3f, 0.5f))
                    .Configure(13);
            }
        }

        private static void AbyssclaspHitFx(Vector2 pos) {
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(pos, Main.rand.NextVector2Circular(3f, 3f)
                    , AbyssrendFX.Body, Main.rand.NextFloat(0.28f, 0.5f))
                    .Configure(12);
            }
            PRTLoader.NewParticle<PRT_AbyssSpark>(pos, Main.rand.NextVector2Circular(3.5f, 3.5f)
                , AbyssrendFX.Cyan, Main.rand.NextFloat(0.8f, 1.1f))
                .Configure(10);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            //发光箭头不吃全环境光压暗
            Color col = Color.Lerp(lightColor, Color.White, 0.45f);
            float scale = Projectile.scale * (Heavy ? 1.12f : 1f);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, col
                , Projectile.rotation, tex.Size() * 0.5f, scale, SpriteEffects.None, 0);

            if (Heavy) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Vector2 head = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 34f;
                Main.EntitySpriteDraw(glow, head - Main.screenPosition, null
                    , new Color(AbyssrendFX.Cyan.R, AbyssrendFX.Cyan.G, AbyssrendFX.Cyan.B, 0) * 0.55f
                    , 0f, glow.Size() * 0.5f, 0.2f, SpriteEffects.None, 0);
            }
            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Vector2[] path = new Vector2[TrailLen];
            int count = 0;
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                path[count++] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }
            if (count < 2) {
                return;
            }
            path[count - 1] = Projectile.Center;
            float lifeFade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0.15f, 1f);
            float wMin = Heavy ? 7f : 4f;
            float wMax = Heavy ? 18f : 11f;
            AbyssrendFX.DrawPathStrip(path, count, i => {
                float t = i / (float)Math.Max(count - 1, 1);
                return MathHelper.Lerp(wMin, wMax, t) * lifeFade;
            }, lifeFade);
        }
    }

    /// <summary>
    /// 空化泡。ai0 半径倍率(普通命中 0.5,重箭终点 1.1)。
    /// 伤害窗对准崩开段,大泡在窗内向心拖拽
    /// </summary>
    internal class UndertowBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 30;
        private const float BaseRadius = 118f;

        private float SizeMul => Projectile.ai[0] > 0.05f ? Projectile.ai[0] : 1f;
        private int Age => Lifetime - Projectile.timeLeft;
        private float Progress => MathHelper.Clamp(Age / (float)Lifetime, 0f, 1f);
        private float VisibleRadius => BaseRadius * SizeMul * MathHelper.Lerp(0.35f, 1f, 1f - (1f - Progress) * (1f - Progress));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (Progress > 0.45f && Progress < 0.85f) {
                Lighting.AddLight(Projectile.Center, 0.2f, 0.6f, 0.7f);
            }
            else {
                Lighting.AddLight(Projectile.Center, 0.06f, 0.18f, 0.25f);
            }

            //只有大泡才值得拖拽
            if (SizeMul >= 0.7f && !VaultUtils.isClient && CanDamage() == true) {
                float pullR = BaseRadius * SizeMul * 1.25f;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.boss || npc.friendly || npc.knockBackResist <= 0f || npc.immortal) {
                        continue;
                    }
                    float dist = npc.Distance(Projectile.Center);
                    if (dist > pullR || dist < 12f) {
                        continue;
                    }
                    npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero)
                        * 0.6f * npc.knockBackResist;
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            if (Age == 14) {
                int count = (int)(10 * SizeMul);
                for (int i = 0; i < count; i++) {
                    Vector2 dir = Main.rand.NextVector2Unit();
                    PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center + dir * 6f
                        , dir * Main.rand.NextFloat(2.5f, 6.5f)
                        , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                        , Main.rand.NextFloat(0.4f, 0.7f))
                        .Configure(Main.rand.Next(13, 22), 1.5f);
                }
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_AbyssSpark>(Projectile.Center
                        , Main.rand.NextVector2Circular(4.5f, 4.5f)
                        , AbyssrendFX.Foam, Main.rand.NextFloat(0.7f, 1.1f))
                        .Configure(11);
                }
            }
        }

        public override bool? CanDamage() => Progress >= 0.45f && Progress <= 0.82f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (CanDamage() != true) {
                return false;
            }
            float boomR = BaseRadius * SizeMul * MathHelper.Lerp(0.2f, 1f, (Progress - 0.45f) / 0.37f);
            return targetHitbox.Distance(Projectile.Center) <= boomR;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 200);
            if (SizeMul >= 0.7f) {
                target.AddBuff(ModContent.BuffType<AbyssalPressure>(), 150);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = Progress < 0.12f ? Progress / 0.12f : MathHelper.Clamp((1f - Progress) / 0.18f, 0f, 1f);
            fade = MathF.Max(fade, Progress > 0.35f && Progress < 0.9f ? 1f : fade);
            AbyssrendFX.DrawCanvasTech("TechBurst", Projectile.Center, AbyssrendFX.QuadPx(VisibleRadius)
                , Progress, fade);
            return false;
        }
    }
}
