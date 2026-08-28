using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.PreHardmode
{
    /// <summary>
    /// 【神赋·盔甲】死灵套「白骨升矛」（A 档）：材质=白骨，死物不发光。<br/>
    /// ①命中 +1 层骸骨，击杀额外 +2 层（上限 6），杀得越快升矛越快
    /// ②满 6 层后下一击自目标脚下唤起三根骨矛：前 12 帧蛰伏预兆（碎骨聚拢无判定），
    /// 后 28 帧升矛上刺（减速穿刺，每敌一次）③骨矛全程正常 alpha 自绘，无任何发光层
    /// ④受击掉 1 层（骸骨已收，惩罚轻）。<br/>
    /// 本套是全包唯一禁 Lighting.AddLight、禁加色亮芯的套：白骨是死物，全部正常 alpha 绘制。
    /// 原版套装奖励（弹药减耗）保留，神赋是叠加层；骸骨层数是攻击方端本地量，
    /// 跨端可见的部分是骨矛实体
    /// </summary>
    internal class GsNecroArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.NecroHelmet, ItemID.AncientNecroHelmet];

        public override int BodyID => ItemID.NecroBreastplate;

        public override int LegsID => ItemID.NecroGreaves;

        protected override string EndowLineFallback =>
            "Bone Uprising: strikes build ossuary and kills feed it faster; at 6 stacks the next strike raises three bone lances from the earth";

        //白骨色板（第三色是普通 alpha 小高光，不是发光芯）
        internal static readonly Color BoneEdge = new(120, 110, 92);
        internal static readonly Color BoneWhite = new(226, 218, 196);
        internal static readonly Color BoneSheen = new(240, 236, 224);

        /// <summary>升矛所需骸骨层数</summary>
        private const int FullCharge = 6;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            //满骨态：脚边偶发碎骨浮动（个人读数；死物不发光，不加光源）
            if (Main.rand.NextBool(12)) {
                Dust d = Dust.NewDustPerfect(player.Bottom + new Vector2(Main.rand.NextFloat(-14f, 14f), -Main.rand.NextFloat(0f, 6f)),
                    DustID.Bone, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f)),
                    80, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = true;
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //骨矛自身命中不喂层，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsNecroBoneLanceProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //升矛：满骨后这一击自目标脚下唤起三根骨矛
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustPerfect(target.Bottom + new Vector2(Main.rand.NextFloat(-60f, 60f), 0f),
                        DustID.Bone, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)),
                        80, default, Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = true;
                }
            }
            //proc 弹幕 owner 侧生成；每根伤害按触发伤害 25% 折算并封顶
            if (player.whoAmI == Main.myPlayer) {
                int lanceDamage = Math.Clamp((int)(damageDone * 0.25f), 8, 100);
                for (int i = -1; i <= 1; i++) {
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithNecroEndow"),
                        new Vector2(target.Center.X + i * 60f, target.Bottom.Y), Vector2.Zero,
                        ModContent.ProjectileType<GsNecroBoneLanceProj>(), lanceDamage, 2.5f, player.whoAmI);
                }
            }
        }

        public override void OnEndowKillNPC(Player player, GodSmithArmorPlayer state, NPC target) {
            //击杀喂骨：额外 +2 层封顶（骨矛击杀也会进来，白骨自食是死灵的本分）
            state.EndowCharge = Math.Min(FullCharge, state.EndowCharge + 2);
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击掉 1 层（骸骨已收，惩罚轻），骨屑洒落
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 1);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Bone, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2f)));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 白骨升矛：从地里刺出的一根长骨，死物不发光——全程正常 alpha 绘制，无任何加色层与光源。<br/>
    /// 两相：前 12 帧蛰伏预兆（无判定，地面碎骨聚拢上飘 + 确定性碎骨点自绘）；
    /// 后 28 帧升矛（-11 初速逐帧 ×0.93 减速上刺，每敌一次），末 8 帧 alpha 消散。
    /// 长骨三层叠色（灰褐边/骨白体/骨节高光）+ 微偏 3° 的骨节短段
    /// </summary>
    internal class GsNecroBoneLanceProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>蛰伏预兆帧数，之后升矛</summary>
        private const int TelegraphFrames = 12;

        /// <summary>全生命帧长：12 蛰伏 + 28 升矛</summary>
        private const int TotalFrames = 40;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 60;
            //蛰伏相无判定，升矛瞬间才置 friendly
            Projectile.friendly = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //蛰伏相：地面碎骨聚拢上飘，升矛的预兆
            if (Life < TelegraphFrames) {
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 at = Projectile.Center + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(0f, 8f));
                    Dust d = Dust.NewDustPerfect(at, DustID.Bone,
                        (Projectile.Center - at) * 0.06f + new Vector2(0f, -Main.rand.NextFloat(0.4f, 1f)),
                        80, default, Main.rand.NextFloat(0.7f, 1.1f));
                    d.noGravity = true;
                }
                return;
            }

            //升矛瞬间：开判定 + 上刺初速 + 骨响
            if (Life == TelegraphFrames) {
                Projectile.friendly = true;
                Projectile.velocity = new Vector2(0f, -11f);
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DD2_SkeletonSummoned with { Volume = 0.5f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-10f, 10f), 0f),
                            DustID.Bone, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f)),
                            80, default, Main.rand.NextFloat(0.9f, 1.3f));
                        d.noGravity = false;
                    }
                }
            }

            //上刺减速：破土迅猛，出土渐滞
            Projectile.velocity *= 0.93f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //命中反馈：骨响 + 骨屑迸溅（无光，白骨是死物）
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.4f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Top + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.Bone, Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f));
                d.noGravity = false;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //余痕：骨矛碎成骨屑回落，比矛体活得久
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 20f),
                    DustID.Bone, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(0.5f, 2f)));
                d.noGravity = false;
            }
        }

        //==================== 绘制：全程正常 alpha——预兆碎骨点 + 长骨三层 + 骨节短段 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 origin = tex.Size() * 0.5f;

            //蛰伏相：地面聚拢的暗色碎骨点，确定性位置随进度向中心收
            if (Life < TelegraphFrames) {
                float progress = Life / TelegraphFrames;
                for (int i = 0; i < 6; i++) {
                    float ang = Seed + i * 1.047f;
                    float radius = (1f - progress) * 26f + 4f;
                    Vector2 at = Projectile.Center + new Vector2(MathF.Cos(ang) * radius,
                        MathF.Sin(ang * 1.3f + i) * 5f - progress * 10f);
                    Main.EntitySpriteDraw(tex, at - Main.screenPosition, null,
                        GsNecroArmor.BoneEdge * (0.7f * progress), ang, origin,
                        new Vector2(0.05f, 0.035f), SpriteEffects.None, 0);
                }
                return false;
            }

            //升矛相：破土 4 帧淡入，末 8 帧 alpha 消散
            float riseFade = MathHelper.Clamp((Life - TelegraphFrames) / 4f, 0f, 1f);
            float endFade = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
            float fade = riseFade * endFade;
            Vector2 posDraw = Projectile.Center - Main.screenPosition;
            //出土的确定性微颤，骨杆不是激光
            float sway = MathF.Sin(Life * 0.4f + Seed * 4f) * 0.02f;

            //灰褐压边
            Main.EntitySpriteDraw(tex, posDraw, null, GsNecroArmor.BoneEdge * (0.85f * fade),
                sway, origin, new Vector2(0.105f, 0.52f), SpriteEffects.None, 0);
            //骨白主体
            Main.EntitySpriteDraw(tex, posDraw, null, GsNecroArmor.BoneWhite * fade,
                sway, origin, new Vector2(0.09f, 0.5f), SpriteEffects.None, 0);
            //骨节短段：上端微偏 3°，长骨不是均匀圆杆
            Main.EntitySpriteDraw(tex, posDraw + new Vector2(0f, -18f), null, GsNecroArmor.BoneWhite * (0.9f * fade),
                sway + 0.052f, origin, new Vector2(0.075f, 0.16f), SpriteEffects.None, 0);
            //骨节高光：普通 alpha 小亮斑，不是发光芯
            Main.EntitySpriteDraw(tex, posDraw + new Vector2(2f, -12f), null, GsNecroArmor.BoneSheen * (0.8f * fade),
                sway, origin, new Vector2(0.035f, 0.14f), SpriteEffects.None, 0);
            return false;
        }
    }
}
