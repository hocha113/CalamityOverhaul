using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Endgame
{
    /// <summary>
    /// 【神赋·耀斑套】「日冕环爆」：日冕火舌（从冕层甩出的弧状日珥物质）。
    /// ①日耀冲刺撞中敌人的一瞬，自命中处环状炸出四道日冕火舌；②火舌先散后咬，
    /// 弧线下坠中锁向近敌，命中点燃破晓之炎；③三盾满冕时冕冠常明，冲刺多炸一道。<br/>
    /// 与原版套装技联动而非覆盖：原版日耀护盾与冲刺撞击照常结算，神赋只借
    /// solarDashConsumedFlare 的上升沿在同一瞬叠加环爆；沿检测是佩戴者端本地量
    /// （原版冲刺撞击本就只在 owner 端结算），火舌 owner 侧生成，火舌命中不再触发
    /// </summary>
    internal class GsSolarArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsC";

        public override int[] HeadIDs => [ItemID.SolarFlareHelmet];

        public override int BodyID => ItemID.SolarFlareBreastplate;

        public override int LegsID => ItemID.SolarFlareLeggings;

        protected override string EndowLineFallback =>
            "Corona Burst: the instant a solar dash connects, four coronal fire tongues erupt in a ring and bite nearby enemies; a spare shield adds one more";

        //日冕色板
        internal static readonly Color CoronaBright = new(255, 236, 170);
        internal static readonly Color CoronaMain = new(255, 132, 44);
        internal static readonly Color CoronaDeep = new(122, 34, 14);

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            //冲刺撞击沿检测：solarDashConsumedFlare 只在 owner 端被原版置真，
            //其余端永远读到 false，沿逻辑天然只在佩戴者端走通
            bool consumed = player.solarDashing && player.solarDashConsumedFlare;
            if (consumed && !state.EndowFlag) {
                DetonateCorona(player);
            }
            state.EndowFlag = consumed;

            //满冕驻场：三盾俱在时冕冠两点巡回（确定性角度，不掷 rand）
            if (VaultUtils.isServer || player.solarShields < 3) {
                return;
            }
            Lighting.AddLight(player.Center, CoronaMain.ToVector3() * 0.18f);
            if (Main.GameUpdateCount % 4 == 0) {
                float baseAng = Main.GameUpdateCount * 0.03f;
                for (int i = 0; i < 2; i++) {
                    Vector2 at = player.Center + new Vector2(0f, -6f) + (baseAng + MathHelper.Pi * i).ToRotationVector2() * new Vector2(30f, 14f);
                    PRTLoader.NewParticle<PRT_Light>(at, Vector2.Zero, CoronaBright, 0.07f)?.Configure(6, 0.7f);
                }
            }
        }

        private static void DetonateCorona(Player player) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.9f, Pitch = -0.15f }, player.Center);
                //环爆一瞬：冕层火星环甩
                for (int i = 0; i < 12; i++) {
                    float ang = MathHelper.TwoPi * i / 12f;
                    PRTLoader.NewParticle<PRT_Spark>(player.Center + ang.ToRotationVector2() * 14f,
                        ang.ToRotationVector2() * Main.rand.NextFloat(3f, 6f),
                        Main.rand.NextBool() ? CoronaBright : CoronaMain,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(false, Main.rand.Next(14, 22));
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //火舌伤害走近战面板的固定预算（原版冲刺撞击本体 150 面板同源）；
            //冲刺受护盾充能与冷却双重闸，收益在神赋包络内。盾有富余（撞后仍 >=2）多炸一道
            int tongueDamage = (int)player.GetTotalDamage(DamageClass.Melee).ApplyTo(110f);
            int count = player.solarShields >= 2 ? 5 : 4;
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.3f, 0.3f);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithSolarEndow"),
                    player.Center + ang.ToRotationVector2() * 12f,
                    ang.ToRotationVector2() * Main.rand.NextFloat(6.5f, 8.5f),
                    ModContent.ProjectileType<GsSolarCoronaProj>(), tongueDamage, 3f, player.whoAmI);
            }
        }
    }

    /// <summary>
    /// 日冕火舌：一段甩出去的日珥弧，不是火球。先环状散开、弧线微坠，
    /// 随后咬向最近目标；舌体沿速度拉伸、表面日震抖动，途中甩耀斑碎屑；
    /// 命中点燃破晓之炎，亡处余烬受重力回落，比舌体活得久
    /// </summary>
    internal class GsSolarCoronaProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>散开段帧数，之后开始咬合</summary>
        private const int ScatterFrames = 11;

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.6841f % 3.67f;

        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 80;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;

            if (Life <= ScatterFrames) {
                //散开段：日珥弧的微坠，绝不匀速
                Projectile.velocity *= 0.97f;
                Projectile.velocity.Y += 0.12f;
            }
            else {
                //咬合段：锁向最近可追目标，越咬越急
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                    float turn = MathHelper.Clamp((Life - ScatterFrames) / 20f, 0.06f, 0.18f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
                else {
                    Projectile.velocity *= 0.96f;
                    Projectile.velocity.Y += 0.08f;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行相：舌缘甩耀斑碎屑
            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                    Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Main.rand.NextBool(3) ? GsSolarArmor.CoronaDeep : GsSolarArmor.CoronaMain,
                    Main.rand.NextFloat(0.26f, 0.44f))?.Configure(false, Main.rand.Next(9, 15));
            }
            Lighting.AddLight(Projectile.Center, GsSolarArmor.CoronaMain.ToVector3() * (0.36f * VisualFade));
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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Daybreak, 240);

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //命中/消亡共用：耀斑迸溅 + 余烬回落
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsSolarArmor.CoronaBright, 0.15f)?.Configure(9, 0.75f);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 5f),
                    Main.rand.NextBool() ? GsSolarArmor.CoronaMain : GsSolarArmor.CoronaDeep,
                    Main.rand.NextFloat(0.28f, 0.5f))?.Configure(true, Main.rand.Next(18, 30));
            }
        }

        //==================== 绘制：三层日珥舌体 + 速度拉伸 + 日震抖动 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.3f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.04f, 0.12f, 0.7f);

            //日震抖动：舌面宽窄反相呼吸
            float wob = MathF.Sin(Life * 0.55f + Seed * 6f) * 0.11f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);

            //焦红压边
            Main.EntitySpriteDraw(tex, pos, null, GsSolarArmor.CoronaDeep * (0.85f * fade), rotation, origin,
                new Vector2(0.32f, 0.40f + stretch * 0.8f) * jiggle, SpriteEffects.None, 0);
            //耀橙主体
            Main.EntitySpriteDraw(tex, pos, null, GsSolarArmor.CoronaMain * fade, rotation, origin,
                new Vector2(0.24f, 0.32f + stretch * 0.65f) * jiggle, SpriteEffects.None, 0);
            //白金亮芯：加色
            Color core = GsSolarArmor.CoronaBright with { A = 0 };
            Main.EntitySpriteDraw(tex, pos, null, core * (0.62f * fade), rotation, origin,
                new Vector2(0.10f, 0.17f + stretch * 0.28f) * jiggle, SpriteEffects.None, 0);
            return false;
        }
    }
}
