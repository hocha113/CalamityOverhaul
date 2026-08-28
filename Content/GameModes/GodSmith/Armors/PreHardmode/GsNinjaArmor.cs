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
    /// 【神赋·盔甲】忍者套「替身木椿」（A 档）：材质=玄铁忍镖与烟。<br/>
    /// ①每 8 秒备好一具替身（就绪时轻烟演出，就绪期腰后偶发灰烟缕）
    /// ②受击时替身碎成烟雾木椿闷响，向最近敌人扇形掷出三枚复仇忍镖
    /// ③忍镖高速自旋、5 帧后微追踪，十字冷光自绘 + 旋转涂抹残影
    /// ④替身消耗后进入 8 秒重塑冷却。<br/>
    /// 注意：框架钩子在受伤结算之后，本神赋不闪避不改伤，只做受击反击；
    /// 原版套装奖励保留，神赋是叠加层；就绪态是受击方端本地量，跨端可见的部分是忍镖实体
    /// </summary>
    internal class GsNinjaArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsA";

        public override int[] HeadIDs => [ItemID.NinjaHood];

        public override int BodyID => ItemID.NinjaShirt;

        public override int LegsID => ItemID.NinjaPants;

        protected override string EndowLineFallback =>
            "Substitution Log: readies a decoy every 8 seconds; when you are struck it shatters into smoke and hurls three avenging shuriken";

        //玄铁忍镖色板
        internal static readonly Color IronBlack = new(40, 40, 48);
        internal static readonly Color SteelGray = new(150, 155, 165);
        internal static readonly Color ColdLight = new(220, 228, 235);

        /// <summary>替身重塑冷却帧长（8 秒）</summary>
        private const int CooldownFrames = 480;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            //替身重塑完成：置就绪 + 轻烟就绪演出（寄存器只在佩戴者自己端有值，读数天然个人）
            if (!state.EndowFlag && Main.GameUpdateCount >= state.EndowTimer) {
                state.EndowFlag = true;
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(8f, 14f),
                            DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.4f, 1f)),
                            150, default, Main.rand.NextFloat(0.8f, 1.2f));
                        d.noGravity = true;
                    }
                }
                return;
            }
            //就绪期：腰后偶发灰烟缕，替身藏在身影里
            if (state.EndowFlag && !VaultUtils.isServer && Main.rand.NextBool(14)) {
                Vector2 at = player.Center + new Vector2(-player.direction * Main.rand.NextFloat(6f, 12f), Main.rand.NextFloat(-2f, 8f));
                Dust d = Dust.NewDustPerfect(at, DustID.Smoke, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.7f)),
                    170, default, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //替身未就绪：这次受击无事发生
            if (!state.EndowFlag) {
                return;
            }
            //消耗替身，进入 8 秒重塑
            state.EndowFlag = false;
            state.EndowTimer = Main.GameUpdateCount + CooldownFrames;

            //木椿碎烟：原地烟爆 + 闷响
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.6f, Pitch = -0.2f }, player.Center);
                for (int i = 0; i < 12; i++) {
                    Dust d = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(10f, 18f),
                        DustID.Smoke, Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                        140, default, Main.rand.NextFloat(1f, 1.6f));
                    d.noGravity = true;
                }
                PRTLoader.NewParticle<PRT_Light>(player.Center, Vector2.Zero, SteelGray, 0.16f)?.Configure(12, 0.6f);
            }

            //复仇忍镖：朝最近敌人扇形三连掷；伤害系数 0.8（以彼之道还施彼身）
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            int damage = Math.Clamp((int)(info.Damage * 0.8f), 15, 120);
            NPC nearest = FindNearestEnemy(player.Center);
            //无敌人时朝背离受击方向掷出（HitDirection 即击退推离方向）
            Vector2 dir = nearest != null
                ? (nearest.Center - player.Center).SafeNormalize(Vector2.UnitX)
                : new Vector2(info.HitDirection == 0 ? player.direction : info.HitDirection, 0f);
            float baseAng = dir.ToRotation();
            for (int i = -1; i <= 1; i++) {
                float ang = baseAng + i * 0.5f;
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithNinjaEndow"),
                    player.Center, ang.ToRotationVector2() * 13f,
                    ModContent.ProjectileType<GsNinjaShurikenProj>(), damage, 2f, player.whoAmI);
            }
        }

        private static NPC FindNearestEnemy(Vector2 from) {
            NPC best = null;
            float bestDist = 700f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = from.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// 复仇忍镖：一枚玄铁十字镖，不是发光贴纸。出手 5 帧后微追踪咬向最近目标；
    /// 十字双杆三层叠色（玄黑压边/钢灰主体/冷光刃线）+ 中心冷光星点
    /// + oldRot 旋转涂抹残影 + 速度拉伸，命中金铁脆响迸钢屑
    /// </summary>
    internal class GsNinjaShurikenProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>展开段帧数，之后开始微追踪</summary>
        private const int ScatterFrames = 5;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>确定性抖动相位，绘制路径不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>自旋方向由 identity 奇偶定，全生命期稳定</summary>
        private float SpinDir => Projectile.identity % 2 == 0 ? 1f : -1f;

        /// <summary>出生 4 帧淡入，防第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 50;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Projectile.rotation += 0.6f * SpinDir;

            //展开段之后微追踪：转向率压低，保持忍镖的直线杀气
            if (Life > ScatterFrames) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.06f);
                }
            }

            //飞行相：刃风偶尔削出一缕薄烟
            if (!Main.dedServ && Life % 6 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.4f,
                    DustID.Smoke, Projectile.velocity * 0.06f, 180, default, Main.rand.NextFloat(0.5f, 0.8f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, GsNinjaArmor.ColdLight.ToVector3() * (0.08f * VisualFade));
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
            if (Main.dedServ) {
                return;
            }
            //命中反馈：金铁脆响 + 钢屑迸溅
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.35f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool() ? GsNinjaArmor.ColdLight : GsNinjaArmor.SteelGray,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(false, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //余痕：镖化薄烟散去，比镖体活得久
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    DustID.Smoke, Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.5f, 1.5f),
                    160, default, Main.rand.NextFloat(0.7f, 1f));
                d.noGravity = true;
            }
        }

        //==================== 绘制：十字双杆三层叠色 + 冷光星心 + 旋转涂抹残影 ====================

        /// <summary>在指定位置转角画一次十字（两条细杆，rotation 与 rotation+PiOver2 各一次）</summary>
        private static void DrawCross(Texture2D tex, Vector2 pos, float rot, Color color, Vector2 scale, Vector2 origin) {
            Main.EntitySpriteDraw(tex, pos, null, color, rot, origin, scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, color, rot + MathHelper.PiOver2, origin, scale, SpriteEffects.None, 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 origin = tex.Size() * 0.5f;
            //刃杆随速度轻微拉长，保留投掷势
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.008f, 0f, 0.1f);
            //确定性微呼吸，杆宽轻颤
            float wob = 1f + MathF.Sin(Life * 0.7f + Seed * 5f) * 0.05f;

            //旋转涂抹残影：旧位置旧转角的褪色十字
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.30f * fade;
                Vector2 gpos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                DrawCross(tex, gpos, Projectile.oldRot[i], GsNinjaArmor.IronBlack * ghost,
                    new Vector2(0.42f + stretch, 0.06f) * (1f - i * 0.05f), origin);
            }

            Vector2 posDraw = Projectile.Center - Main.screenPosition;
            //玄黑压边
            DrawCross(tex, posDraw, Projectile.rotation, GsNinjaArmor.IronBlack * (0.9f * fade),
                new Vector2(0.46f + stretch, 0.085f * wob), origin);
            //钢灰主体
            DrawCross(tex, posDraw, Projectile.rotation, GsNinjaArmor.SteelGray * fade,
                new Vector2(0.40f + stretch, 0.06f * wob), origin);
            //冷光刃线
            DrawCross(tex, posDraw, Projectile.rotation, GsNinjaArmor.ColdLight * (0.8f * fade),
                new Vector2(0.30f + stretch, 0.035f * wob), origin);
            //中心冷光星点（黑底贴图走加色观感）
            if (star != null) {
                Main.EntitySpriteDraw(star, posDraw, null, (GsNinjaArmor.ColdLight with { A = 0 }) * (0.7f * fade),
                    Projectile.rotation, star.Size() * 0.5f, 0.16f * wob, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
