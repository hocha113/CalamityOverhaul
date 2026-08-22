using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>
    /// 质心弹：史莱姆王抛出的六成身体质量。ai[0]=宿主whoAmI ai[1]=落点X ai[2]=落点地表Y<br/>
    /// 抛物线由生成端按常量(飞行帧数/重力)一次性解出，出手即锁定落点
    /// 落点凝胶标记从第一帧起全程可见(契约2实体预告+契约3非追踪承诺)。<br/>
    /// 坠地爆开：两侧凝胶喷泉+滞留池+一道爬回本体的矮回流波。服务端生成
    /// </summary>
    internal class BKSMassGlobProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        //---- 弹道常量(状态端解抛物线用) ----
        /// <summary>飞行帧数：落点标记的可读时长即整段飞行</summary>
        internal const float FlightFrames = 52f;
        /// <summary>自带重力(不走引擎默认)，与状态端解算共用同一数字</summary>
        internal const float Gravity = 0.46f;

        //---- 爆开排布公平阀(契约3) ----
        /// <summary>两侧喷泉离落点的横距：中间留出净空带，贴着落点跳过滞留池即安全</summary>
        internal const float GeyserOffsetPx = 170f;
        /// <summary>喷泉预兆帧：冒泡先行，柱体后至</summary>
        internal const float GeyserWarnFrames = 22f;
        /// <summary>回流波速度；波高由 BKSTideWaveProj.ReturnFlowHeightPx 保证可跳越</summary>
        internal const float ReturnFlowSpeed = 8f;

        private const float MarkerWidthPx = 150f;

        private int HostIndex => (int)Projectile.ai[0];
        private float ImpactX => Projectile.ai[1];
        private float ImpactY => Projectile.ai[2];

        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = (int)FlightFrames + 40;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;

            //自带重力抛物线(与状态端解算同数)
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > 26f) {
                Projectile.velocity.Y = 26f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.012f;

            //飞行洒珠：大团质量一路滴胶
            if (!VaultUtils.isServer) {
                if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_BKSGelBead>(
                        Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                        -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(1f, 1f),
                        Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelDeep, Main.rand.NextFloat()) * 0.75f,
                        Main.rand.NextFloat(0.7f, 1.3f))?.Configure(Main.rand.Next(16, 28));
                }
                //落点标记冒泡：预告是实体，不止贴图
                if (Main.rand.NextBool(3)) {
                    KingSlimeGelFX.BubbleFizz(new Vector2(ImpactX + Main.rand.NextFloat(-40f, 40f), ImpactY - 6f), 12f, 1);
                }
            }

            Lighting.AddLight(Projectile.Center, KingSlimeGelFX.GelMid.ToVector3() * 0.6f);
            Lighting.AddLight(new Vector2(ImpactX, ImpactY - 20f), KingSlimeGelFX.CrownGold.ToVector3() * 0.3f);

            //坠地判定：下落段越过落点地表即爆(各端位置确定性一致)；
            //上升段豁免，落点高于出手点时(玩家站高地)不许半途早爆
            if (Timer > 6f && Projectile.velocity.Y > 0f && Projectile.Center.Y >= ImpactY - 12f) {
                Projectile.Kill();
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slimed, 180);
        }

        public override void OnKill(int timeLeft) {
            Vector2 impact = new Vector2(ImpactX, ImpactY);

            //坠击表现
            KingSlimeGelFX.ThudSound(impact, 24f);
            KingSlimeGelFX.CameraPunch(impact, 8f, 16, "BKSMassImpact", -Vector2.UnitY);
            if (!VaultUtils.isServer) {
                KingSlimeGelFX.LandingBurst(impact, 22f, 1.6f);
                KingSlimeGelFX.GelSplatter(impact - new Vector2(0f, 10f), -Vector2.UnitY, 12, 8f, 1.3f);
            }

            if (VaultUtils.isClient) {
                return;
            }

            //两侧喷泉：预兆先行，中间净空带(公平阀由 GeyserOffsetPx 保证)
            for (int side = -1; side <= 1; side += 2) {
                Vector2 gpos = KingSlimeGelFX.FindGroundBelow(impact + new Vector2(side * GeyserOffsetPx, -30f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), gpos, Vector2.Zero,
                    ModContent.ProjectileType<BKSGeyserProj>(), (int)(Projectile.damage * 0.75f), 0f, Main.myPlayer,
                    0f, GeyserWarnFrames);
            }
            //落点滞留池
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), impact, Vector2.Zero,
                ModContent.ProjectileType<BKSGelPoolProj>(), (int)(Projectile.damage * 0.55f), 0f, Main.myPlayer,
                150f, 220f);
            //冲击环(纯表现)
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), impact, Vector2.Zero,
                ModContent.ProjectileType<BKSShockwaveProj>(), 0, 0f, Main.myPlayer, 1.5f);

            //回流波：质量沿地爬回本体(矮波可跳越，模式3)
            NPC host = HostIndex >= 0 && HostIndex < Main.maxNPCs ? Main.npc[HostIndex] : null;
            if (host != null && host.active && host.type == NPCID.KingSlime) {
                float dirX = host.Center.X >= impact.X ? 1f : -1f;
                float dist = Math.Abs(host.Center.X - impact.X);
                float travel = MathHelper.Clamp(dist / ReturnFlowSpeed, 30f, 110f);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), impact - new Vector2(0f, 16f),
                    new Vector2(dirX * ReturnFlowSpeed, 0f),
                    ModContent.ProjectileType<BKSTideWaveProj>(), (int)(Projectile.damage * 0.7f), 0f, Main.myPlayer,
                    -1f, 3f, travel);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawImpactMarker();
            DrawGlob();
            return false;
        }

        /// <summary>落点标记：渐涨凝胶泊(shader优先，CPU回退)+微光柱，进度=剩余飞行占比</summary>
        private void DrawImpactMarker() {
            float progress = MathHelper.Clamp(Timer / FlightFrames, 0f, 1f);
            Vector2 impact = new Vector2(ImpactX, ImpactY);

            Effect pool = EffectLoader.BKSGelPool?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (pool != null && noise != null) {
                KingSlimeGelFX.SetPoolParams(pool, spread: 0.3f + progress * 0.7f, drain: 0f,
                    alpha: 0.55f + progress * 0.35f, boil: 0.3f + progress * 0.7f,
                    seed: Projectile.whoAmI * 0.211f % 1f);
                KingSlimeGelFX.DrawShaderQuad(pool, noise, impact + new Vector2(0f, -12f),
                    new Vector2(MarkerWidthPx * (0.5f + progress * 0.5f), 46f), 1f);
            }
            else {
                //CPU回退：压扁凝胶渍，绝不许无形落点
                Texture2D blob = CWRAsset.Extra_98?.Value;
                if (blob != null) {
                    Vector2 pos = impact - Main.screenPosition;
                    Color gel = KingSlimeGelFX.GelMid * (0.35f + progress * 0.4f);
                    Main.EntitySpriteDraw(blob, pos, null, gel, 0f, new Vector2(blob.Width * 0.5f, blob.Height),
                        new Vector2(MarkerWidthPx * (0.5f + progress * 0.5f) / blob.Width, 0.35f), SpriteEffects.None, 0);
                }
            }

            //末段收束光柱：坠击临近的最后警示
            Texture2D column = CWRAsset.Extra_98?.Value;
            if (column != null && progress > 0.45f) {
                float late = (progress - 0.45f) / 0.55f;
                Vector2 pos = impact - Main.screenPosition;
                Color gold = KingSlimeGelFX.CrownGold with { A = 0 } * (0.28f * late);
                Main.EntitySpriteDraw(column, pos, null, gold, 0f, new Vector2(column.Width * 0.5f, column.Height),
                    new Vector2(0.35f, 90f / column.Height * (0.5f + late)), SpriteEffects.None, 0);
            }
        }

        /// <summary>质心本体：大号双层凝胶团+速度拉伸+同材质拖尾(契约5)+内嵌王金核心</summary>
        private void DrawGlob() {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color gel = Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelDeep, 0.3f) * 0.9f;

            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0f, 0.7f);
            Vector2 scale = new Vector2(2.2f * (1f - stretch * 0.25f), 2.4f * (1f + stretch * 0.9f));
            float rot = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //同材质拖尾链：尾团≥本体一半(契约5)
            for (int i = 1; i <= 4; i++) {
                Vector2 ghostPos = pos - Projectile.velocity * (i * 0.9f);
                Main.EntitySpriteDraw(tex, ghostPos, null, gel * (0.4f - i * 0.08f), rot, origin,
                    scale * (1f - i * 0.09f), SpriteEffects.None, 0);
            }

            //双层厚度
            Main.EntitySpriteDraw(tex, pos, null, gel, rot, origin, scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, gel * 0.8f, rot, origin, scale * new Vector2(0.6f, 0.92f), SpriteEffects.None, 0);
            //内嵌王金核心：这是王的本体质量，不是普通胶弹
            Main.EntitySpriteDraw(tex, pos, null, KingSlimeGelFX.CrownGold with { A = 0 } * 0.5f, rot, origin,
                scale * 0.34f, SpriteEffects.None, 0);
            //顶部高光
            Main.EntitySpriteDraw(tex, pos - new Vector2(0f, 8f), null, KingSlimeGelFX.GelFoam with { A = 0 } * 0.4f,
                rot, origin, scale * 0.22f, SpriteEffects.None, 0);
        }
    }
}
