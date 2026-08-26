using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors._Exemplars
{
    /// <summary>
    /// 【范例·盔甲神赋】金套装「点石成金」：机制型神赋的标准样板。<br/>
    /// 命中积攒鎏金（proc 弹自身不喂层），满 8 层后下一击引爆：自目标迸出三枚追踪熔金珠
    /// （owner 侧生成，伤害按触发伤害折算），并为目标戴上点金债；受击崩落 2 层。<br/>
    /// 原版套装奖励（+3 防御）保留，神赋是叠加层；层数是攻击方端本地量，
    /// 就绪光环只对佩戴者自己可见（个人读数），跨端可见的部分是熔金珠实体
    /// </summary>
    internal class GsGoldArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "Exemplars";

        public override int[] HeadIDs => [ItemID.GoldHelmet, ItemID.AncientGoldHelmet];

        public override int BodyID => ItemID.GoldChainmail;

        public override int LegsID => ItemID.GoldGreaves;

        protected override string EndowLineFallback =>
            "Midas Verdict: strikes build gilding; at 8 stacks the next strike erupts into three homing molten-gold pearls";

        //熔金色板
        internal static readonly Color GoldBright = new(255, 232, 150);
        internal static readonly Color GoldMain = new(240, 178, 56);
        internal static readonly Color GoldDeep = new(150, 96, 26);

        /// <summary>引爆所需鎏金层数</summary>
        private const int FullCharge = 8;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < FullCharge) {
                return;
            }
            //就绪态：鎏金微光绕身（个人读数，层数只在攻击方端存在）
            Lighting.AddLight(player.Center, GoldMain.ToVector3() * 0.22f);
            if (Main.rand.NextBool(9)) {
                Vector2 at = player.Center + Main.rand.NextVector2CircularEdge(20f, 26f);
                PRTLoader.NewParticle<PRT_Light>(at, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.8f))
                    , GoldBright, Main.rand.NextFloat(0.08f, 0.14f))?.Configure(14, 0.7f);
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //熔金珠自身命中不喂层，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsGoldPearlProj>()) {
                return;
            }
            //目标要有实体感：假人以外的可击杀目标才算数
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            if (state.EndowCharge < FullCharge) {
                state.EndowCharge++;
                return;
            }

            //引爆：满层后这一击迸出三枚熔金珠
            state.EndowCharge = 0;
            target.AddBuff(BuffID.Midas, 300);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.8f, Pitch = 0.1f }, target.Center);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                        Main.rand.NextBool() ? GoldBright : GoldMain, Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(true, Main.rand.Next(16, 26));
                }
            }
            //proc 弹幕 owner 侧生成；伤害按触发伤害两成折算并封顶
            if (player.whoAmI == Main.myPlayer) {
                int pearlDamage = Math.Clamp((int)(damageDone * 0.20f), 8, 200);
                for (int i = 0; i < 3; i++) {
                    float ang = MathHelper.TwoPi * i / 3f + Main.rand.NextFloat(-0.4f, 0.4f);
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithGoldEndow"),
                        target.Center + ang.ToRotationVector2() * 14f,
                        ang.ToRotationVector2() * Main.rand.NextFloat(4.5f, 6f),
                        ModContent.ProjectileType<GsGoldPearlProj>(), pearlDamage, 1.5f, player.whoAmI);
                }
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //受击崩落两层鎏金，攒层有张力
            if (state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.GoldCoin, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(0.5f, 2f)));
                    d.noGravity = false;
                }
            }
        }
    }

    /// <summary>
    /// 熔金珠：一粒有重量的液态金，不是光点。出手短暂散开后咬向最近目标；
    /// 头部三层液态金叠色 + 速度拉伸 + 张力抖动，命中迸金屑，亡处余烬回落
    /// </summary>
    internal class GsGoldPearlProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>散开段帧数，之后开始追踪</summary>
        private const int ScatterFrames = 9;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 75;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //散开段之后咬向最近目标；索敌规则各端确定（最近存活），owner 位置同步兜底
            if (Life > ScatterFrames) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 11f;
                    float turn = MathHelper.Clamp((Life - ScatterFrames) / 22f, 0.05f, 0.16f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
                else {
                    Projectile.velocity *= 0.96f;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行相：液金失稳甩微珠
            if (!Main.dedServ && Life % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center - Projectile.velocity * 0.5f,
                    Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    Main.rand.NextBool(3) ? GsGoldArmor.GoldDeep : GsGoldArmor.GoldMain,
                    Main.rand.NextFloat(0.22f, 0.38f))?.Configure(false, Main.rand.Next(8, 14));
            }
            Lighting.AddLight(Projectile.Center, GsGoldArmor.GoldMain.ToVector3() * (0.3f * VisualFade));
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 500f;
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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Midas, 180);

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //命中/消亡共用：金屑迸溅 + 余烬回落，余痕比珠体活得久
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.35f, Pitch = 0.45f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsGoldArmor.GoldBright, 0.14f)?.Configure(8, 0.7f);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f),
                    Main.rand.NextBool() ? GsGoldArmor.GoldMain : GsGoldArmor.GoldDeep,
                    Main.rand.NextFloat(0.28f, 0.5f))?.Configure(true, Main.rand.Next(16, 28));
            }
        }

        //==================== 绘制：三层液态金 + 速度拉伸 + 张力抖动 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.3f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.035f, 0.1f, 0.6f);

            //表面张力抖动，宽窄反相呼吸
            float wob = MathF.Sin(Life * 0.6f + Seed * 6f) * 0.12f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);

            //焦金压边
            Main.EntitySpriteDraw(tex, pos, null, GsGoldArmor.GoldDeep * (0.8f * fade), rotation, origin,
                new Vector2(0.34f, 0.40f + stretch * 0.7f) * jiggle, SpriteEffects.None, 0);
            //熔金主体
            Main.EntitySpriteDraw(tex, pos, null, GsGoldArmor.GoldMain * fade, rotation, origin,
                new Vector2(0.26f, 0.32f + stretch * 0.6f) * jiggle, SpriteEffects.None, 0);
            //亮芯，加色湿反光
            Color core = GsGoldArmor.GoldBright with { A = 0 };
            Main.EntitySpriteDraw(tex, pos, null, core * (0.6f * fade), rotation, origin,
                new Vector2(0.10f, 0.16f + stretch * 0.25f) * jiggle, SpriteEffects.None, 0);
            return false;
        }
    }
}
