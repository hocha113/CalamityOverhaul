using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 玉米糖来福枪重铸：糖衣炮弹两吃。<br/>
    /// [糖雨]：原版速射加一点糖粒坠感的微弧，尾上糖霜。<br/>
    /// [粘牙点射]：3 连粘糖点射，命中叠「甜腻」（至多 3 层，每层缓行且受本枪伤 +3%，5 秒）。
    /// 甜腻层挂在跟随目标的糖渍标记弹幕上，弹幕载态天然全端同步
    /// </summary>
    internal class GsCandyCornRifle : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.CandyCornRifle;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch coating\n" +
            "Candy Rain adds a sweet drooping arc to the classic spray\n" +
            "Sticky Burst fires three gummy rounds; hits stack Sugar Coat, slowing the target and sweetening your shots";

        /// <summary>糖橙色</summary>
        internal static readonly Color CandyOrange = new(255, 168, 52);

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeCandyRain", EnName = "Candy Rain",
            },
            new GsFireMode {
                Key = "ModeStickyBurst", EnName = "Sticky Burst",
                UseSpeed = 1.10f, DamageMul = 1.10f, Converge = 0.6f,
                BurstCount = 3, BurstRest = 22,
            },
        ];

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            GsGunsHardPlayer mp = Main.player[proj.owner].GetModPlayer<GsGunsHardPlayer>();
            router.MarkData = PackMark(mp.ModeIndex);
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            //糖雨档：糖粒微坠弧（各端确定性）+ 稀疏糖霜尾
            if (MarkModeOf(router.MarkData) != 0) {
                return;
            }
            proj.velocity.Y += 0.028f;
            if (!VaultUtils.isServer && proj.timeLeft % 5 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.3f,
                    -proj.velocity * 0.03f, CandyOrange,
                    Main.rand.NextFloat(0.32f, 0.5f))?.Configure(CandyOrange, Main.rand.Next(8, 14));
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只在攻击方端执行：粘牙档命中叠甜腻（owner 生成/升层标记弹幕，弹幕载态全端可见）
            if (MarkModeOf(router.MarkData) != 1 || target.friendly || !target.active) {
                return;
            }
            int markType = ModContent.ProjectileType<GsCandySweetMarkProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (other.active && other.type == markType && other.owner == proj.owner
                    && (int)other.ai[0] == target.whoAmI) {
                    //已有标记：升层并刷新时长（owner 权威改动，netUpdate 过线）
                    other.ai[1] = System.Math.Min(3, other.ai[1] + 1);
                    other.timeLeft = GsCandySweetMarkProj.Duration;
                    other.netUpdate = true;
                    return;
                }
            }
            Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                markType, 0, 0f, proj.owner, target.whoAmI, 1f);
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //甜腻增伤：本枪弹命中带层目标时 +3%/层（命中端本地查找，标记弹幕已同步在场）
            int markType = ModContent.ProjectileType<GsCandySweetMarkProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (other.active && other.type == markType && other.owner == proj.owner
                    && (int)other.ai[0] == target.whoAmI) {
                    modifiers.FinalDamage *= 1f + 0.03f * (int)other.ai[1];
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 甜腻糖渍标记：跟随目标的层数载体（ai0 = 目标 NPC 序号，ai1 = 层数 1~3）。
    /// owner 生成随生成包全端可见；缓行在服务器/单机权威端做温和速度阻尼，
    /// 客户端只做糖霜演出。Boss 免疫缓行只吃增伤
    /// </summary>
    internal class GsCandySweetMarkProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>标记时长（5 秒）</summary>
        public const int Duration = 300;

        private NPC Target {
            get {
                int idx = (int)Projectile.ai[0];
                return idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duration;
        }

        public override void AI() {
            NPC target = Target;
            if (target == null || !target.active || target.life <= 0) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = target.Center;
            int layers = (int)Projectile.ai[1];
            //缓行：只在权威端（单机/服务器）做温和阻尼，客户端写了也会被同步覆盖所以不写；
            //阻尼系数与 NPC 自身 AI 加速平衡后约合每层 5% 移速损失，待游戏内标定
            if (Main.netMode != NetmodeID.MultiplayerClient && !target.boss) {
                target.velocity *= 1f - 0.0018f * layers;
            }
            if (VaultUtils.isServer) {
                return;
            }
            //糖霜渗出：频率随层数
            if (Main.rand.NextBool(14 - layers * 3)) {
                PRTLoader.NewParticle<PRT_Sparkle>(
                    target.Center + Main.rand.NextVector2Circular(target.width * 0.4f, target.height * 0.4f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f),
                    GsCandyCornRifle.CandyOrange, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(GsCandyCornRifle.CandyOrange, Main.rand.Next(12, 20));
            }
        }

        public override bool? CanHitNPC(NPC target) => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //糖衣崩解的小铃音收尾
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.3f, Pitch = 0.7f }, Projectile.Center);
        }
    }
}
