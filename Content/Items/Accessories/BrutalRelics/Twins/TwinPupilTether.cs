using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Twins
{
    /// <summary>
    /// 双瞳系绳：双子魔眼残酷遗物。召出视界(激光眼)与焚瞳(火焰眼)双环绕体，
    /// 两眼之间恒拉切割系绳；双击下方向键发动交叉冲锋，交点引爆双色干涉爆纹
    /// </summary>
    internal class TwinPupilTether : BaseBrutalRelic
    {
        //超模数值档：机械Boss档位奖励，刻意压过同期
        public const int LaserDamage = 300;
        public const int LaserFireInterval = 42;
        public const float LaserRange = 1100f;
        public const int FlameDamage = 90;
        public const int FlameFireInterval = 5;
        public const float FlameRange = 330f;
        public const int TetherDamage = 260;
        public const int DashContactDamage = 520;
        public const int BurstDamage = 1250;
        public const float BurstRadius = 250f;
        public const int CrossDashCooldownTime = 360;
        public const float RendBonus = 0.30f;
        public const int RendDuration = 180;

        //系列双色：视界红激光 / 焚瞳青焰
        public static Color LaserColor => new(255, 64, 64);
        public static Color LaserGlow => new(255, 160, 120);
        public static Color FlameColor => new(95, 240, 150);
        public static Color FlameGlow => new(190, 255, 205);

        public override void SetDefaults() {
            base.SetDefaults();
            Item.value = Item.buyPrice(0, 40, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            TwinPupilTetherPlayer mp = player.GetModPlayer<TwinPupilTetherPlayer>();
            mp.TetherEquipped = true;
            mp.SourceItem = Item;
        }
    }

    /// <summary>装备旗标、交叉冲锋指令与冷却、双眼与系绳的常驻补齐</summary>
    internal class TwinPupilTetherPlayer : ModPlayer
    {
        /// <summary>本帧装备生效，物品钩子逐帧点亮；经原版装备同步各端可见</summary>
        public bool TetherEquipped;
        internal Item SourceItem;
        /// <summary>交叉冲锋冷却</summary>
        public int DashCooldown;
        /// <summary>指令时刻戳(GameUpdateCount)，两只眼各自去重消费；0=无指令</summary>
        public long DashOrder;
        /// <summary>指令目标(光标世界坐标)，只在 owner 端有意义</summary>
        public Vector2 DashTarget;

        private int downTapWindow;
        private bool downHeldLast;

        public override void ResetEffects() => TetherEquipped = false;

        //自维护双击窗口，不依赖原版计时器的读取时序；本钩子只在本机玩家输入端跑
        public override void ProcessTriggers(TriggersSet triggersSet) {
            bool fresh = Player.controlDown && !downHeldLast;
            downHeldLast = Player.controlDown;

            if (!TetherEquipped || Player.dead) {
                downTapWindow = 0;
                return;
            }

            if (fresh) {
                if (downTapWindow > 0) {
                    TryOrderCrossDash();
                    downTapWindow = 0;
                }
                else {
                    downTapWindow = 15;//与原版双击判定同宽的窗口
                }
            }
            else if (downTapWindow > 0) {
                downTapWindow--;
            }
        }

        private void TryOrderCrossDash() {
            if (DashCooldown > 0) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.45f, Pitch = -0.4f }, Player.Center);
                return;
            }
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<TwinPupilOrbiter>()] < 2) {
                return;
            }
            //同帧双击下消费闩：被别家抢走则本帧静默放弃(不进冷却)
            if (!Player.CWR().TryConsumeRelicDoubleTap(0)) {
                return;
            }
            DashOrder = Main.GameUpdateCount;
            DashTarget = Main.MouseWorld;
            DashCooldown = TwinPupilTether.CrossDashCooldownTime;
            SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.45f, Volume = 0.8f }, Player.Center);
        }

        public override void PostUpdateEquips() {
            if (DashCooldown > 0) {
                DashCooldown--;
            }
            if (!TetherEquipped || Player.dead || Player.whoAmI != Main.myPlayer) {
                return;
            }

            //缺件补齐：两眼+系绳都是装备期间常驻的弹幕，只在 owner 端生成
            int orbiterType = ModContent.ProjectileType<TwinPupilOrbiter>();
            int beamType = ModContent.ProjectileType<TwinPupilTetherBeam>();
            bool hasLaser = false, hasFlame = false, hasBeam = false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != Player.whoAmI) {
                    continue;
                }
                if (proj.type == orbiterType) {
                    if (proj.ai[0] == 0f) {
                        hasLaser = true;
                    }
                    else {
                        hasFlame = true;
                    }
                }
                else if (proj.type == beamType) {
                    hasBeam = true;
                }
            }

            var source = SourceItem != null
                ? Player.GetSource_Accessory(SourceItem)
                : Player.GetSource_Misc("TwinPupilTether");
            if (!hasLaser) {
                Projectile.NewProjectile(source, Player.Center + new Vector2(46f, -66f), Vector2.Zero,
                    orbiterType, TwinPupilTether.DashContactDamage, 4f, Player.whoAmI, 0f);
            }
            if (!hasFlame) {
                Projectile.NewProjectile(source, Player.Center + new Vector2(-46f, -66f), Vector2.Zero,
                    orbiterType, TwinPupilTether.DashContactDamage, 4f, Player.whoAmI, 1f);
            }
            if (!hasBeam) {
                Projectile.NewProjectile(source, Player.Center, Vector2.Zero,
                    beamType, TwinPupilTether.TetherDamage, 2f, Player.whoAmI);
            }
        }
    }

    /// <summary>
    /// 撕裂创口标记。只在命中判定端(owner 客户端)读写：系绳/撞击/引爆写入，
    /// 本遗物各伤害源结算时消费；伤害广播由 StrikeNPC 承担，无需跨端同步
    /// </summary>
    internal class TwinPupilRendNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>创口过期时刻(GameUpdateCount 刻度)</summary>
        public long RendExpireAt;

        public bool IsRended => (long)Main.GameUpdateCount < RendExpireAt;

        public void ApplyRend() => RendExpireAt = (long)Main.GameUpdateCount + TwinPupilTether.RendDuration;

        /// <summary>本遗物全部伤害源共用的创口增伤入口</summary>
        public static void ApplyRendBonus(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.TryGetGlobalNPC(out TwinPupilRendNPC rend) && rend.IsRended) {
                modifiers.FinalDamage *= 1f + TwinPupilTether.RendBonus;
            }
        }

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            //创口双色余焰；标记只存在于命中端，本地提示足够
            if (!IsRended) {
                return;
            }
            if (Main.rand.NextBool(5)) {
                bool green = Main.rand.NextBool();
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                    green ? DustID.CursedTorch : DustID.RedTorch, 0f, -1.2f, 120, default, 1.1f);
                dust.noGravity = true;
                dust.velocity *= 0.4f;
            }
        }
    }
}
