using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.KingSlime
{
    /// <summary>
    /// 王冠坠击状态机。状态全在实例字段(禁static)：<br/>
    /// 快坠移动修饰按同步输入在各端确定性计算；起坠/取消/落地结算只在所有者端运行，
    /// 王冠与震荡波等产物弹幕经原版弹幕同步对全端可见。<br/>
    /// 交互契约：抓钩/坐骑/滑轮绳/入液/反重力/受控即解除坠击并恢复常态，摔伤线由物品钩子免疫
    /// </summary>
    internal class FallenKingsCrownPlayer : ModPlayer
    {
        /// <summary>本帧处于装备状态，物品钩子逐帧点亮</summary>
        public bool Equipped;

        /// <summary>坠击态(王冠已现身)，所有者端权威；远端只通过王冠弹幕感知</summary>
        public bool DiveArmed { get; private set; }

        /// <summary>本次下坠累计像素(所有者端结算用)</summary>
        private float fallPx;

        /// <summary>上一帧垂直速度，落地冲量来源(照抄Boss落地检测)</summary>
        private float prevVelY;

        /// <summary>落地结算内置冷却，期间落地不出波不出池，王冠压暗提示</summary>
        private int slamCooldown;

        /// <summary>按住下键起坠的最小蓄坠(4格)，防平台穿落误触</summary>
        private const float HoldArmPx = 4f * 16f;
        /// <summary>自然坠落自动现冕阈值(25格，恰是原版摔伤线)</summary>
        private const float AutoArmPx = 25f * 16f;
        /// <summary>结算所需最低落地冲速，低于此软着陆不放波</summary>
        private const float SlamImpactVel = 6f;
        /// <summary>快坠落速上限</summary>
        private const float DiveMaxFall = 30f;
        /// <summary>落地结算内置冷却(1.5秒)</summary>
        private const int SlamCooldownFrames = 90;
        /// <summary>计伤坠深封顶(格)：伤害只吃到这里，半径与演出档继续吃真实深度</summary>
        public const float DamageCapTiles = 40f;

        /// <summary>结算冷却剩余帧(仅所有者端计时)，王冠弹幕据此压暗充能演出</summary>
        public int SlamCooldown => slamCooldown;

        /// <summary>当前累计坠深(格)，王冠弹幕与结算共用</summary>
        public float FallTiles => fallPx / 16f;

        /// <summary>
        /// 快坠输入活跃。只依赖原版同步的输入位与玩家状态，各端可独立求值，
        /// 远端模拟该玩家移动时也能得到一致的落速修饰
        /// </summary>
        public bool FastFallInput => Equipped && Player.controlDown
            && Player.velocity.Y > 0f && Player.gravDir > 0f
            && !Player.mount.Active && Player.grapCount <= 0 && !Player.pulley && !AnyWet();

        public override void ResetEffects() => Equipped = false;

        public override void UpdateDead() => ResetDiveState();

        public override void PlayerDisconnect() => ResetDiveState();

        /// <summary>移动修饰各端同跑：快坠抬高落速上限并持续下压，读作"主动坠击"而非自然掉落</summary>
        public override void PostUpdateRunSpeeds() {
            if (!Equipped || Player.dead) {
                return;
            }
            if (FastFallInput) {
                Player.maxFallSpeed = Math.Max(Player.maxFallSpeed, DiveMaxFall);
                Player.velocity.Y = Math.Min(Player.velocity.Y + 1.1f, DiveMaxFall);
            }
        }

        /// <summary>状态机只归所有者端；死亡帧不进此钩子，清理走 UpdateDead</summary>
        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (slamCooldown > 0) {
                slamCooldown--;
                //冷却转好：金鸣一声提示震荡波可再结算(owner本机)
                if (slamCooldown == 0 && Equipped) {
                    KingSlimeGelFX.CrownChime(Player.Center, 0.2f, 0.8f);
                }
            }
            if (!Equipped) {
                ResetDiveState();
                prevVelY = Player.velocity.Y;
                return;
            }

            //环境接管(钩爪/坐骑/绳/入液/反重力/受控)或转入上升：解除坠击并清蓄坠
            bool blocked = Player.mount.Active || Player.grapCount > 0 || Player.pulley
                || Player.CCed || Player.gravDir < 0f || AnyWet();
            if (blocked || Player.velocity.Y < -0.01f) {
                if (DiveArmed) {
                    CancelDive();
                }
                fallPx = 0f;
                prevVelY = Player.velocity.Y;
                return;
            }

            if (prevVelY > 0.5f && Player.velocity.Y == 0f) {
                //落地帧：冷却已转好、够冲量且脚下有真地面才结算，否则软着陆收冠
                if (DiveArmed && slamCooldown == 0 && prevVelY >= SlamImpactVel && GroundBeneath()) {
                    DoSlam();
                }
                else if (DiveArmed) {
                    CancelDive();
                }
                fallPx = 0f;
            }
            else if (Player.velocity.Y > 0f) {
                fallPx += Player.velocity.Y;
                //冷却期间也可现冕坠击(碾压照常)，只是落地不结算，王冠压暗提示充能中
                if (!DiveArmed) {
                    bool holdArm = Player.controlDown && fallPx >= HoldArmPx;
                    bool autoArm = fallPx >= AutoArmPx;
                    if (holdArm || autoArm) {
                        ArmDive();
                    }
                }
            }
            else if (Player.velocity.Y == 0f) {
                fallPx = 0f;
            }

            prevVelY = Player.velocity.Y;
        }

        /// <summary>起坠：王冠实体化(弹幕经原版同步全端可见)；碾压伤害由王冠每帧自刷</summary>
        private void ArmDive() {
            DiveArmed = true;
            Projectile.NewProjectile(Player.GetSource_Misc("CWR_FallenKingsCrown"),
                Player.Top, Vector2.Zero, ModContent.ProjectileType<FallenCrownProj>(),
                (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(30f), 4f, Player.whoAmI);
        }

        /// <summary>
        /// 落地结算：伤害载体+可视金环+凝胶领域三件套，全部所有者端生成。<br/>
        /// 成长曲线(h=累计坠深格数)：伤害吃玩家总伤加成，计伤深度封顶40格
        /// (波 50+3h、领域单跳 6+0.15h 基伤)；波及半径 300+9h(上限1000px)
        /// 与领域宽 280+7h(上限720px)继续吃真实深度，超深坠落换半径与演出档不换伤害
        /// </summary>
        private void DoSlam() {
            float h = FallTiles;
            float hDmg = MathF.Min(h, DamageCapTiles);
            var generic = Player.GetTotalDamage(DamageClass.Generic);
            int dmg = Math.Max((int)generic.ApplyTo(50f + 3f * hDmg), 1);
            float radius = MathF.Min(300f + 9f * h, 1000f);
            float poolW = MathF.Min(280f + 7f * h, 720f);
            int dotDmg = Math.Max((int)generic.ApplyTo(6f + 0.15f * hDmg), 1);
            Vector2 impact = Player.Bottom;
            var source = Player.GetSource_Misc("CWR_FallenKingsCrown");

            //真伤害走ai[0](float整数精确到2^24)，绕开生成包damage字段的short截断
            Projectile.NewProjectile(source, impact, Vector2.Zero,
                ModContent.ProjectileType<KingsVerdictWave>(), 1, 9.5f, Player.whoAmI, dmg, radius);

            //可视金环复用Boss无伤演出弹幕(ai0=档位 ai1=皇冠金配色)
            float sizeClass = h >= 60f ? 2f : h >= 22f ? 1f : 0f;
            Projectile.NewProjectile(source, impact, Vector2.Zero,
                ModContent.ProjectileType<BKSShockwaveProj>(), 0, 0f, Player.whoAmI, sizeClass, 1f);

            //减速凝胶领域(ai0=池宽 ai1=存留帧 ai2=单跳伤害)
            Projectile.NewProjectile(source, impact + new Vector2(0f, -2f), Vector2.Zero,
                ModContent.ProjectileType<RoyalGelField>(), 0, 0f, Player.whoAmI, poolW, 360f, dotDmg);

            //反冲小跳：冲击回馈，也把玩家从凝胶里弹开半步
            Player.velocity.Y = -4.6f;
            slamCooldown = SlamCooldownFrames;
            ResetDiveState();
        }

        /// <summary>解除坠击(不结算)，恢复常态</summary>
        private void CancelDive() {
            DiveArmed = false;
            KillCrown();
        }

        /// <summary>硬重置：死亡/卸装/断线共用</summary>
        private void ResetDiveState() {
            DiveArmed = false;
            fallPx = 0f;
            KillCrown();
        }

        /// <summary>回收王冠弹幕；只有所有者有权Kill，远端等同步</summary>
        private void KillCrown() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            int type = ModContent.ProjectileType<FallenCrownProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == Player.whoAmI && proj.type == type) {
                    proj.Kill();
                }
            }
        }

        /// <summary>任意液体接触(水/岩浆/蜂蜜/微光)</summary>
        private bool AnyWet() {
            return Player.wet || Player.lavaWet || Player.honeyWet || Player.shimmerWet;
        }

        /// <summary>脚下2px内有可站立物(实心或平台)，排除蛛网悬停这类假落地</summary>
        private bool GroundBeneath() {
            int y = (int)((Player.position.Y + Player.height + 2f) / 16f);
            int x0 = (int)(Player.position.X / 16f);
            int x1 = (int)((Player.position.X + Player.width) / 16f);
            for (int x = x0; x <= x1; x++) {
                if (!WorldGen.InWorld(x, y, 10)) {
                    continue;
                }
                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.HasUnactuatedTile
                    && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])) {
                    return true;
                }
            }
            return false;
        }
    }
}
