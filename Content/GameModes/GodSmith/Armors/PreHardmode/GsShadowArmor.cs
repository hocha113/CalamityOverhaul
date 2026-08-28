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
    /// 【神赋·盔甲】暗影套「影潮镰舞」（A 档）：材质=暗影紫焰啃噬的月镰。<br/>
    /// ①命中积攒影焰，满 8 层掀起影潮（6 秒窗口态）②潮中每次命中自命中点旋出一道追猎暗影镰刃
    /// ③潮落时以自身为心螺旋放出三枚镰刃新星④非潮期受击崩落 2 层，潮中受击不掉（已释放）。<br/>
    /// 原版套装奖励（移速）保留，神赋是叠加层；层数与窗口态是攻击方端本地量，
    /// 跨端可见的部分是镰刃实体；寄存器复用：影潮期间 EndowCharge 暂存伤害基数（注释详见代码）
    /// </summary>
    internal class GsShadowArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.ShadowHelmet, ItemID.AncientShadowHelmet];

        public override int BodyID => ItemID.ShadowScalemail;

        public override int LegsID => ItemID.ShadowGreaves;

        protected override string EndowLineFallback =>
            "Umbral Tide: strikes build shadowflame; at 8 stacks the tide rises, each strike within 6 seconds reaps a shadow scythe, and three more spiral out as it ebbs";

        //暗影紫焰色板
        internal static readonly Color ShadowDeep = new(46, 20, 66);
        internal static readonly Color ShadowMain = new(122, 54, 189);
        internal static readonly Color ShadowBright = new(200, 140, 255);

        /// <summary>掀潮所需影焰层数</summary>
        private const int FullCharge = 8;

        /// <summary>影潮窗口帧长（6 秒）</summary>
        private const int TideFrames = 360;

        /// <summary>潮中同时在场镰刃上限，防高攻速武器刷屏</summary>
        private const int MaxScythes = 6;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            //窗口态只在攻击方端存在（EndowFlag 仅在 OnEndowHitNPC 里置位）
            bool tide = state.EndowFlag && Main.GameUpdateCount < state.EndowTimer;

            //潮落：螺旋放出三枚镰刃新星，伤害基数取掀潮那一击（暂存在 EndowCharge）
            if (state.EndowFlag && !tide) {
                if (player.whoAmI == Main.myPlayer) {
                    int novaDamage = Math.Clamp((int)(state.EndowCharge * 0.25f), 8, 110);
                    for (int i = 0; i < 3; i++) {
                        float ang = MathHelper.TwoPi * i / 3f + Main.rand.NextFloat(-0.3f, 0.3f);
                        Projectile.NewProjectile(player.GetSource_Misc("GodSmithShadowEndow"),
                            player.Center + ang.ToRotationVector2() * 12f,
                            ang.ToRotationVector2() * 2.2f,
                            ModContent.ProjectileType<GsShadowScytheProj>(), novaDamage, 2f, player.whoAmI,
                            0f, 1f);
                    }
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item104 with { Volume = 0.7f, Pitch = -0.2f }, player.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero, ShadowMain, 0.6f)
                        ?.Configure(0.2f, 1.4f, 18);
                    for (int i = 0; i < 10; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(player.Center,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                            Main.rand.NextBool() ? ShadowBright : ShadowMain, Main.rand.NextFloat(0.3f, 0.55f))
                            ?.Configure(false, Main.rand.Next(14, 24));
                    }
                }
                state.EndowFlag = false;
                state.EndowCharge = 0;
                return;
            }

            //潮中体感：紫焰绕身 + 移动拖出焰痕（个人读数，窗口态只在自己端存在）
            if (tide && !VaultUtils.isServer) {
                Lighting.AddLight(player.Center, ShadowMain.ToVector3() * 0.35f);
                if (Main.rand.NextBool(3)) {
                    Vector2 at = player.Center + Main.rand.NextVector2CircularEdge(18f, 26f);
                    Dust d = Dust.NewDustPerfect(at, DustID.Shadowflame,
                        new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.4f)), 100, default, Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = true;
                }
                //移动时身后洒落影焰残痕
                if (player.velocity.Length() > 2f && Main.rand.NextBool(2)) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(8f, 16f),
                        DustID.Shadowflame, -player.velocity * 0.15f, 120, default, Main.rand.NextFloat(0.7f, 1.1f));
                    d.noGravity = true;
                }
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //镰刃自身命中不喂层不再生镰，防自循环
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsShadowScytheProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            bool tide = state.EndowFlag && Main.GameUpdateCount < state.EndowTimer;
            if (tide) {
                //潮中：每击自命中点旋出一道追猎镰刃（在场上限内）
                if (player.whoAmI == Main.myPlayer
                    && player.ownedProjectileCounts[ModContent.ProjectileType<GsShadowScytheProj>()] < MaxScythes) {
                    int scytheDamage = Math.Clamp((int)(damageDone * 0.25f), 8, 110);
                    float ang = (target.Center - player.Center).ToRotation() + Main.rand.NextFloat(-0.9f, 0.9f);
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithShadowEndow"),
                        target.Center + ang.ToRotationVector2() * 20f,
                        ang.ToRotationVector2() * 9f,
                        ModContent.ProjectileType<GsShadowScytheProj>(), scytheDamage, 2f, player.whoAmI);
                }
                return;
            }

            if (state.EndowCharge < FullCharge - 1) {
                state.EndowCharge++;
                return;
            }

            //满层：这一击掀起影潮；EndowCharge 转为暂存本击伤害基数（潮落新星用）
            state.EndowFlag = true;
            state.EndowTimer = Main.GameUpdateCount + TideFrames;
            state.EndowCharge = Math.Clamp(damageDone, 10, 220);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = -0.1f }, player.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(player.Center, Vector2.Zero, ShadowBright, 0.5f)
                    ?.Configure(0.15f, 1.1f, 14);
                for (int i = 0; i < 12; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center, DustID.Shadowflame,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f), 80, default, Main.rand.NextFloat(1f, 1.6f));
                    d.noGravity = true;
                }
            }
            //掀潮的这一击也算潮中第一击，立即旋出一道镰刃
            if (player.whoAmI == Main.myPlayer) {
                int scytheDamage = Math.Clamp((int)(damageDone * 0.25f), 8, 110);
                float ang = (target.Center - player.Center).ToRotation();
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithShadowEndow"),
                    target.Center + ang.ToRotationVector2() * 20f,
                    ang.ToRotationVector2() * 9f,
                    ModContent.ProjectileType<GsShadowScytheProj>(), scytheDamage, 2f, player.whoAmI);
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //潮中受击不惩罚（力量已释放）；蓄层期受击崩落两层影焰
            bool tide = state.EndowFlag && Main.GameUpdateCount < state.EndowTimer;
            if (tide || state.EndowCharge <= 0) {
                return;
            }
            state.EndowCharge = Math.Max(0, state.EndowCharge - 2);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 16f),
                        DustID.Shadowflame, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f)),
                        100, default, Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// 暗影套「远古变体」：远古暗影鳞甲 + 远古暗影护胫组成的整套同样掀得起影潮；
    /// 机制与本体套完全一致（薄子类只换胸腿 ID）
    /// </summary>
    internal class GsShadowArmorAncient : GsShadowArmor
    {
        public override int BodyID => ItemID.AncientShadowScalemail;

        public override int LegsID => ItemID.AncientShadowGreaves;
    }

    /// <summary>
    /// 暗影镰刃：被紫焰啃噬的月镰，不是发光贴纸。ai[1]=0 追猎态（自命中点旋进、微追踪），
    /// ai[1]=1 新星态（自玩家螺旋外扩加速）；三层月牙叠色（黑鸦紫压边/暗紫主体/亮紫芯）
    /// + 自旋涂抹残影 + 速度拉伸，命中挂暗影焰，亡处紫焰散逸
    /// </summary>
    internal class GsShadowScytheProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "CrescentEdge01";

        private ref float Life => ref Projectile.ai[0];

        /// <summary>1 = 新星态（螺旋外扩不追踪）</summary>
        private bool NovaMode => Projectile.ai[1] == 1f;

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>自旋方向由 identity 定，全生命期稳定</summary>
        private float SpinDir => Projectile.identity % 2 == 0 ? 1f : -1f;

        /// <summary>出生 5 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 5f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 55;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Projectile.rotation += 0.42f * SpinDir;

            if (NovaMode) {
                //新星态：螺旋外扩，越转越快（有加速度，不匀速直飞）
                Projectile.velocity = Projectile.velocity.RotatedBy(0.025f * SpinDir);
                if (Projectile.velocity.Length() < 13f) {
                    Projectile.velocity *= 1.06f;
                }
            }
            else if (Life > 6f) {
                //追猎态：短暂展开后咬向最近目标，转向率随时间收紧
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 10f;
                    float turn = MathHelper.Clamp((Life - 6f) / 24f, 0.04f, 0.13f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
                else {
                    Projectile.velocity *= 0.97f;
                }
            }

            //飞行相：刃缘剥落紫焰
            if (!Main.dedServ && Life % 3 == 0) {
                Vector2 edge = Projectile.Center + Projectile.rotation.ToRotationVector2() * 14f;
                Dust d = Dust.NewDustPerfect(edge, DustID.Shadowflame,
                    Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    100, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, GsShadowArmor.ShadowMain.ToVector3() * (0.32f * VisualFade));
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 420f;
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
            target.AddBuff(BuffID.ShadowFlame, 120);
            if (Main.dedServ) {
                return;
            }
            //命中反馈：紫焰迸溅，与原版命中区分
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                    Main.rand.NextBool() ? GsShadowArmor.ShadowBright : GsShadowArmor.ShadowMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //余痕：紫焰散逸比镰体活得久
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Shadowflame, Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f),
                    100, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = true;
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsShadowArmor.ShadowMain, 0.13f)?.Configure(10, 0.6f);
        }

        //==================== 绘制：三层月牙 + 自旋涂抹残影 + 速度拉伸 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.CrescentEdge01?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 origin = tex.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.02f, 0f, 0.3f);
            //刃体呼吸，确定性相位
            float breathe = 1f + MathF.Sin(Life * 0.5f + Seed * 5f) * 0.06f;
            float baseScale = 0.34f * breathe;

            //自旋涂抹残影：旧位置旧转角的褪色镰影（亮度型贴图一律 A=0 走加色观感）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.28f * fade;
                Vector2 gpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, gpos, null, (GsShadowArmor.ShadowDeep with { A = 0 }) * ghost,
                    Projectile.oldRot[i], origin, baseScale * (1f - i * 0.04f), SpriteEffects.None, 0);
            }

            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 stretchScale = new(baseScale * (1f + stretch), baseScale);
            //黑鸦紫压边
            Main.EntitySpriteDraw(tex, pos, null, (GsShadowArmor.ShadowDeep with { A = 0 }) * (0.9f * fade),
                Projectile.rotation, origin, stretchScale * 1.18f, SpriteEffects.None, 0);
            //暗紫主体
            Main.EntitySpriteDraw(tex, pos, null, (GsShadowArmor.ShadowMain with { A = 0 }) * fade,
                Projectile.rotation, origin, stretchScale, SpriteEffects.None, 0);
            //亮紫芯，窄月牙热线
            Main.EntitySpriteDraw(tex, pos, null, (GsShadowArmor.ShadowBright with { A = 0 }) * (0.75f * fade),
                Projectile.rotation, origin, stretchScale * 0.62f, SpriteEffects.None, 0);
            return false;
        }
    }
}
