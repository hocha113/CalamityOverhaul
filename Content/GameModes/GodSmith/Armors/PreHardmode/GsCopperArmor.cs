using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【神赋·盔甲】铜套「导雷铜脉」：材质=铜脉里窜行的橙白电弧。<br/>
    /// ①命中积攒电荷，满 5 层后下一击自目标放出一道锯齿电弧②电弧命中后跳线，
    /// 甩向 320px 内最近的下一个敌人，链上伤害逐跳衰减③最多贯穿三个敌人，断线即灭
    /// ④受击漏电崩落 2 层铜屑。<br/>
    /// 原版套装奖励（+2 防御）保留，神赋是叠加层；层数是攻击方端本地量，
    /// 就绪电花只对佩戴者自己可见（个人读数），跨端可见的部分是电弧实体
    /// </summary>
    internal class GsCopperArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.CopperHelmet];

        public override int BodyID => ItemID.CopperChainmail;

        public override int LegsID => ItemID.CopperGreaves;

        protected override string EndowLineFallback =>
            "Conductive Veins: strikes build charge; at 5 stacks the next strike releases a chaining arc that leaps between up to three foes";

        //铜与电弧色板
        internal static readonly Color CopperBrown = new(150, 82, 44);
        internal static readonly Color ArcOrange = new(255, 150, 60);
        internal static readonly Color WhiteHot = new(255, 235, 200);

        /// <summary>放电所需电荷层数</summary>
        private const int FullCharge = 5;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            //就绪态：橙白电花绕身噼跳（个人读数，层数只在攻击方端存在）
            Lighting.AddLight(player.Center, ArcOrange.ToVector3() * 0.2f);
            if (Main.rand.NextBool(8)) {
                Vector2 at = player.Center + Main.rand.NextVector2CircularEdge(18f, 26f);
                PRTLoader.NewParticle<PRT_Spark>(at, Main.rand.NextVector2Circular(1.2f, 1.2f),
                    Main.rand.NextBool() ? WhiteHot : ArcOrange, Main.rand.NextFloat(0.2f, 0.35f))
                    ?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //电弧自身命中不喂层，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsCopperArcProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //放电：满层后这一击自目标放出锯齿电弧
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                        Main.rand.NextBool() ? WhiteHot : ArcOrange, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(false, Main.rand.Next(10, 18));
                }
            }
            //proc 弹幕 owner 侧生成；初伤按触发伤害 20% 折算并封顶
            if (player.whoAmI == Main.myPlayer) {
                int arcDamage = Math.Clamp((int)(damageDone * 0.20f), 6, 90);
                //生成时朝触发目标：从来向偏后一点射入，先咬住它再跳线
                Vector2 dir = (target.Center - player.Center).SafeNormalize(Vector2.UnitX);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithCopperEndow"),
                    target.Center - dir * 40f, dir * 13f,
                    ModContent.ProjectileType<GsCopperArcProj>(), arcDamage, 1f, player.whoAmI);
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击漏电崩落两层电荷
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Copper, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 导雷电弧：铜脉放出的一道锯齿闪电，不是光条。命中后跳线甩向 320px 内下一个敌人
    /// （跳过刚命中者，链上伤害 ×0.85 逐跳衰减），断线即灭；轨迹用相邻 oldPos 连段绘制，
    /// 折点带确定性垂直抖动，三遍叠色（宽铜褐压边/中电橙/窄白热芯）
    /// </summary>
    internal class GsCopperArcProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Line";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>上一个命中的 npc.whoAmI + 1（0 = 尚未命中），跳线时跳过它</summary>
        private ref float LastHitPlusOne => ref Projectile.localAI[0];

        /// <summary>出生 4 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 45;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Projectile.rotation = Projectile.velocity.ToRotation();

            //飞行相：电弧沿途甩电火花
            if (!Main.dedServ && Life % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Main.rand.NextBool(3) ? GsCopperArmor.WhiteHot : GsCopperArmor.ArcOrange,
                    Main.rand.NextFloat(0.2f, 0.35f))?.Configure(false, Main.rand.Next(6, 12));
            }
            Lighting.AddLight(Projectile.Center, GsCopperArmor.ArcOrange.ToVector3() * (0.3f * VisualFade));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            LastHitPlusOne = target.whoAmI + 1;

            //跳线：找 320px 内最近的、非刚命中目标的可追敌
            NPC next = FindNext(target.whoAmI);
            if (next == null) {
                Projectile.Kill();
                return;
            }
            //甩向新目标，链上伤害逐跳衰减（弹幕内机制，不是玩家数值行）
            float speed = Projectile.velocity.Length() * 0.95f;
            Projectile.velocity = (next.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * speed;
            Projectile.damage = (int)(Projectile.damage * 0.85f);
            Projectile.netUpdate = true;

            if (Main.dedServ) {
                return;
            }
            //跳线瞬间：小爆花 + 电噼啪短音
            SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { Volume = 0.45f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsCopperArmor.WhiteHot, 0.12f)?.Configure(7, 0.7f);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? GsCopperArmor.WhiteHot : GsCopperArmor.ArcOrange,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        private NPC FindNext(int skipWhoAmI) {
            NPC best = null;
            float bestDist = 320f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == skipWhoAmI || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //断线：白热余闪 + 电火花散逸
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsCopperArmor.ArcOrange, 0.13f)?.Configure(9, 0.6f);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f),
                    Main.rand.NextBool() ? GsCopperArmor.ArcOrange : GsCopperArmor.CopperBrown,
                    Main.rand.NextFloat(0.22f, 0.4f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        //==================== 绘制：oldPos 连段锯齿闪电，三遍叠色 + 白热头点 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Line?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 half = Projectile.Size * 0.5f;

            //折点表：当前位置 + 轨迹缓存，每点加垂直于段向的确定性抖动
            Span<Vector2> pts = stackalloc Vector2[Projectile.oldPos.Length + 1];
            int count = 0;
            pts[count++] = Projectile.Center;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    break;
                }
                pts[count++] = Projectile.oldPos[i] + half;
            }
            if (count < 2) {
                return false;
            }
            for (int i = 1; i < count - 1; i++) {
                //折点抖动：确定性种子，头尾锚定不抖
                Vector2 seg = pts[i + 1] - pts[i - 1];
                Vector2 perp = new Vector2(-seg.Y, seg.X).SafeNormalize(Vector2.UnitY);
                pts[i] += perp * (MathF.Sin(Projectile.identity * 7.3f + i * 2.1f) * 4f);
            }

            //三遍绘制：宽铜褐压边 / 中电橙 / 窄白热芯（黑底贴图全部 A=0）
            DrawArcPass(tex, pts, count, (GsCopperArmor.CopperBrown with { A = 0 }) * (0.75f * fade), 6f);
            DrawArcPass(tex, pts, count, (GsCopperArmor.ArcOrange with { A = 0 }) * fade, 3.5f);
            DrawArcPass(tex, pts, count, (GsCopperArmor.WhiteHot with { A = 0 }) * (0.85f * fade), 1.6f);

            //白热头点
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null,
                    (GsCopperArmor.WhiteHot with { A = 0 }) * (0.8f * fade), 0f, glow.Size() * 0.5f, 0.24f, SpriteEffects.None, 0);
            }
            return false;
        }

        private static void DrawArcPass(Texture2D tex, Span<Vector2> pts, int count, Color color, float width) {
            for (int i = 0; i < count - 1; i++) {
                Vector2 a = pts[i];
                Vector2 b = pts[i + 1];
                float len = a.Distance(b);
                if (len < 1f) {
                    continue;
                }
                //越靠尾越淡
                float tail = 1f - i / (float)(count - 1);
                Vector2 scale = new(len / tex.Width, width / tex.Height);
                Main.EntitySpriteDraw(tex, a - Main.screenPosition, null, color * tail,
                    (b - a).ToRotation(), new Vector2(0f, tex.Height * 0.5f), scale, SpriteEffects.None, 0);
            }
        }
    }
}
