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
    /// 【神赋·盔甲】血腥套「蛭誓饮血」（A 档）：材质=活的血肉之蛭。<br/>
    /// ①命中积攒血滴，满 6 层后下一击自目标撕出两条血蛭②血蛭分节蠕动、蛇形追咬，
    /// 咬中放血（Bleeding）并把生命虹吸回佩戴者③受击血滴溅落崩 2 层。<br/>
    /// 原版套装奖励（生命再生）保留，神赋是叠加层；吸血走蛭的实体命中（弹幕→治疗联动），
    /// 不是裸数值回复；层数是攻击方端本地量，跨端可见的部分是血蛭实体
    /// </summary>
    internal class GsCrimsonArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.CrimsonHelmet];

        public override int BodyID => ItemID.CrimsonScalemail;

        public override int LegsID => ItemID.CrimsonGreaves;

        protected override string EndowLineFallback =>
            "Leech Pact: strikes build blood; at 6 stacks the next strike tears out two crimson leeches that bite your foe and siphon life back to you";

        //血肉蛭体色板
        internal static readonly Color CrimDeep = new(96, 18, 24);
        internal static readonly Color CrimMain = new(196, 38, 44);
        internal static readonly Color CrimBright = new(255, 120, 120);

        /// <summary>撕蛭所需血滴层数</summary>
        private const int FullCharge = 6;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            //就绪态：心口血珠微光滴坠（个人读数）
            Lighting.AddLight(player.Center, CrimMain.ToVector3() * 0.18f);
            if (Main.rand.NextBool(10)) {
                Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(8f, 12f),
                    DustID.Blood, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)), 60, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = false;
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //血蛭自身命中不喂层，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsCrimsonLeechProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //满层引爆：这一击自目标撕出两条血蛭
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.6f, Pitch = -0.25f }, target.Center);
                for (int i = 0; i < 7; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 40, default, Main.rand.NextFloat(1f, 1.6f));
                    d.noGravity = Main.rand.NextBool();
                }
            }
            //蛭伤按触发伤害 28% 折算并封顶；两条对开撕出
            if (player.whoAmI == Main.myPlayer) {
                int leechDamage = Math.Clamp((int)(damageDone * 0.28f), 8, 100);
                for (int i = 0; i < 2; i++) {
                    float ang = (target.Center - player.Center).ToRotation()
                        + (i == 0 ? 1f : -1f) * Main.rand.NextFloat(1.2f, 1.9f);
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithCrimsonEndow"),
                        target.Center + ang.ToRotationVector2() * 16f,
                        ang.ToRotationVector2() * 6.5f,
                        ModContent.ProjectileType<GsCrimsonLeechProj>(), leechDamage, 1.5f, player.whoAmI);
                }
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击血滴溅落，崩两层
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Blood, new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(0.5f, 2.5f)),
                        40, default, Main.rand.NextFloat(1f, 1.4f));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 血肉之蛭：一段活的血肉，不是红色光点。头节领路、五节身躯沿轨迹蠕动
    /// （行波起伏 + 头亮尾暗 + 湿粉高光），蛇形摆尾追咬最近目标；
    /// 咬中放血并把 2 点生命虹吸回主人，亡处血雾溅开、血珠坠地
    /// </summary>
    internal class GsCrimsonLeechProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>散开段帧数，之后开始追咬</summary>
        private const int ScatterFrames = 10;

        /// <summary>每口虹吸的生命值</summary>
        private const int BiteHeal = 2;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性蠕动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 5 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 5f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 80;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //散开段之后咬向最近目标；追踪之上叠蛇形摆尾（垂直于速度的行波）
            if (Life > ScatterFrames) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 9.5f;
                    float turn = MathHelper.Clamp((Life - ScatterFrames) / 20f, 0.05f, 0.15f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
                else {
                    Projectile.velocity *= 0.97f;
                }
            }
            Vector2 side = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity += side * MathF.Sin(Life * 0.45f + Seed * 4f) * 0.5f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            //蠕行相：偶发滴血，血珠受重力坠落
            if (!Main.dedServ && Life % 6 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.6f,
                    DustID.Blood, new Vector2(0f, Main.rand.NextFloat(0.4f, 1f)), 60, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = false;
            }
            Lighting.AddLight(Projectile.Center, GsCrimsonArmor.CrimMain.ToVector3() * (0.12f * VisualFade));
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 460f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //咬中放血 + 虹吸：治疗走 owner 端（OnHitNPC 只在攻击方端执行）
            target.AddBuff(BuffID.Bleeding, 240);
            Player owner = Main.player[Projectile.owner];
            if (owner.whoAmI == Main.myPlayer && owner.statLife < owner.statLifeMax2) {
                owner.Heal(BiteHeal);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //咬中/消亡共用：血雾溅开 + 血珠坠地，余痕比蛭体活得久
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsCrimsonArmor.CrimMain, 0.12f)?.Configure(9, 0.55f);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f), 40, default, Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = Main.rand.NextBool(3);
            }
        }

        //==================== 绘制：头节 + 五节蠕动身躯（行波起伏、头亮尾暗） ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 side = dir.RotatedBy(MathHelper.PiOver2);

            //五节身躯沿旧轨迹回排，行波自头传向尾（i 越大越靠尾、越小越暗）
            for (int seg = 5; seg >= 1; seg--) {
                int cacheIdx = seg * 2;
                if (cacheIdx >= Projectile.oldPos.Length || Projectile.oldPos[cacheIdx] == Vector2.Zero) {
                    continue;
                }
                float wave = MathF.Sin(Life * 0.45f + Seed * 4f - seg * 0.9f) * 3.5f;
                Vector2 segPos = Projectile.oldPos[cacheIdx] + Projectile.Size * 0.5f
                    + side * wave - Main.screenPosition;
                float t = 1f - seg / 6.5f;
                float segScale = 0.10f + 0.05f * t;
                //节间暗红压边 + 猩红节体，尾节渐暗
                Main.EntitySpriteDraw(tex, segPos, null, GsCrimsonArmor.CrimDeep * (0.85f * t * fade),
                    Projectile.rotation, origin, new Vector2(segScale * 1.25f, segScale * 1.1f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, segPos, null, GsCrimsonArmor.CrimMain * (0.9f * t * fade),
                    Projectile.rotation, origin, new Vector2(segScale, segScale * 0.85f), SpriteEffects.None, 0);
            }

            //头节：略大、带湿粉高光，速度拉伸出「咬向前」的冲势
            Vector2 headPos = Projectile.Center + Projectile.velocity * 0.3f - Main.screenPosition;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0f, 0.4f);
            float headWob = MathF.Sin(Life * 0.6f + Seed * 6f) * 0.1f;
            Vector2 headScale = new Vector2(0.18f * (1f + stretch), 0.15f * (1f - headWob * 0.5f));
            Main.EntitySpriteDraw(tex, headPos, null, GsCrimsonArmor.CrimDeep * (0.9f * fade),
                Projectile.rotation, origin, headScale * 1.3f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, headPos, null, GsCrimsonArmor.CrimMain * fade,
                Projectile.rotation, origin, headScale, SpriteEffects.None, 0);
            //湿亮高光，加色小芯
            Main.EntitySpriteDraw(tex, headPos, null, (GsCrimsonArmor.CrimBright with { A = 0 }) * (0.5f * fade),
                Projectile.rotation, origin, headScale * 0.45f, SpriteEffects.None, 0);
            return false;
        }
    }
}
