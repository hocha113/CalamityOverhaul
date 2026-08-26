using CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon
{
    /// <summary>
    /// 霜月三小Boss签名技层（行为叠加，不接管原版 AI）：
    /// 常绿尖叫怪=饰品炮列（走廊缺口）+松针速射（锁线流）；
    /// 圣诞坦克=礼炮齐放（多落点标记，具名安全间距）+雪橇冲压（直线预告）；
    /// 冰雪女王=冰晶华尔兹（收缩环带具名安全楔，不旋转）+暴风雪航道（扫场预告）。
    /// 三类型走显式名单放行（不放行任何名单外的 boss 旗标个体）；boss 旗标不做离线断言，
    /// 提速补偿在注入时运行时读旗标决定（镜像 GameModeNPC.RageEligible 的口径）。
    /// 每实例同时至多一个签名技进行中；预告一律 ≥40 帧；死亡流程不碰
    /// </summary>
    internal class FrmMiniBossNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private enum BossRole : byte
        {
            None,
            Everscream,
            SantaTank,
            IceQueen,
        }

        //==== 通用节奏 ====
        /// <summary>签名技基准冷却（帧）</summary>
        private const int BaseCooldown = 540;
        private const int CooldownJitter = 70;
        private const int FirstCooldownMin = 240;
        private const int FirstCooldownMax = 420;
        private const int RetryDelay = 40;
        /// <summary>目标最大接战距离</summary>
        private const float MaxEngageRange = 1000f;
        /// <summary>波次风味（只读波次值）：高波次冷却打此折</summary>
        private const int WaveFlavorThreshold = 15;
        private const float WaveFlavorMult = 0.85f;

        //==== 常绿尖叫怪 ====
        private const float OrnamentDamageFrac = 0.5f;
        private const float NeedleDamageFrac = 0.4f;
        private const float NeedleSpeed = 10.5f;
        /// <summary>速射窗（帧）与点射间隔</summary>
        private const int NeedleStreamFrames = 48;
        private const int NeedleFireInterval = 4;
        /// <summary>松针出膛横向散布上限（预告线芯宽画得更宽，可见≥真实散布）</summary>
        private const float NeedleSpawnJitter = 12f;

        //==== 圣诞坦克 ====
        private const float SalvoDamageFrac = 0.65f;
        /// <summary>齐放弹着半径比例</summary>
        private const float SalvoScale = 1f;
        /// <summary>齐放落点间的具名安全间距：相邻弹着环边缘的净空（放点循环直接读取）</summary>
        private const float SalvoSafeGap = 140f;
        /// <summary>齐放飞行帧（=标记环可见时长，小Boss契约 ≥40）</summary>
        private const int SalvoFlightFrames = 78;
        private const int SalvoCountBase = 4;
        private const int SalvoCountHighTier = 5;
        /// <summary>冲压实速（像素/帧，注入按运行时旗标补偿后恰为此值）</summary>
        private const float RamSpeed = 14f;
        /// <summary>冲压帧数 = 冲压线长 ÷ 实速 = 620/14 ≈ 44（承诺距离全覆盖）</summary>
        private const int RamFrames = 44;
        private const int RamRecoverFrames = 26;
        /// <summary>冲压要求目标接近坦克水平面（贴地直线技的适用窗）</summary>
        private const float RamMaxDy = 170f;

        //==== 冰雪女王 ====
        private const float WaltzDamageFrac = 0.6f;
        private const float BlizzardDamageFrac = 0.65f;
        private const float WaltzMaxRange = 820f;

        private const byte PhaseIdle = 0;
        /// <summary>忙相位：签名技实体自治推进，本层只等待（每实例至多一技进行中）</summary>
        private const byte PhaseBusy = 10;
        private const byte PhaseTelegraph = 20;
        private const byte PhaseExecute = 21;
        private const byte PhaseRecover = 22;

        /// <summary>出生绑定档位，0=未绑定</summary>
        private int boundTier;
        private BossRole role;
        private byte phase;
        private int timer;
        private int cooldown;
        /// <summary>交替选技（每实例私产）</summary>
        private bool useAltSig;
        /// <summary>锁定方向（承诺后不再改写）</summary>
        private float lockedAngle;
        /// <summary>速射流冻结出膛点（锁定帧定格）</summary>
        private Vector2 lockedOrigin;
        /// <summary>冲压注入横速（已按运行时旗标补偿）</summary>
        private float ramVX;
        private int omenIndex = -1;

        /// <summary>
        /// 提速补偿系数：GameModeNPC.PostAI 只对「非 Boss 且非体节」个体追加位移
        /// （RageEligible 口径），此处运行时读同一组旗标决定注入是否除回，不做离线断言
        /// </summary>
        private float MoveGain(NPC npc)
            => !npc.boss && npc.realLife < 0 ? 1f + GameModeTuning.SpeedBonus(boundTier) : 1f;

        private static BossRole ResolveRole(int type) {
            //显式类型名单（§2.1）：只放行这三类，名单外的 boss 旗标个体一律不入层
            if (type == NPCID.Everscream) {
                return BossRole.Everscream;
            }
            if (type == NPCID.SantaNK1) {
                return BossRole.SantaTank;
            }
            if (type == NPCID.IceQueen) {
                return BossRole.IceQueen;
            }
            return BossRole.None;
        }

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveRole(entity.type) != BossRole.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            role = BossRole.None;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            role = ResolveRole(npc.type);
            if (role == BossRole.None) {
                return;
            }
            boundTier = tier;
            //冷却是权威端决策私产，Main.rand 播种合法；不读此刻恒为 0 的 whoAmI
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>签名技入口资格：名单已放行 boss 旗标，只排除异常态</summary>
        private static bool Eligible(NPC npc)
            => !npc.friendly && !npc.townNPC && !npc.immortal && !npc.dontTakeDamage && npc.damage > 0;

        /// <summary>签名技冷却：档位缩短 + 高波次风味折扣（波次值只读，权威端决策路径）</summary>
        private int SigCooldown() {
            int cd = (int)(BaseCooldown * (boundTier >= 3 ? 0.7f : boundTier >= 2 ? 0.85f : 1f));
            if (NPC.waveNumber >= WaveFlavorThreshold) {
                cd = (int)(cd * WaveFlavorMult);
            }
            return cd + Main.rand.Next(CooldownJitter + 1);
        }

        /// <summary>校验自己名下的预兆弹幕仍有效（索引+类型双校验，防槽位复用）</summary>
        private bool TryGetOmen(int projType, int npcIndex, out Projectile proj) {
            proj = null;
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            Projectile p = Main.projectile[omenIndex];
            if (!p.active || p.type != projType || (int)p.ai[0] != npcIndex) {
                return false;
            }
            proj = p;
            return true;
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端画面全部来自已同步的签名技实体
                return;
            }
            if (phase == PhaseIdle) {
                if (--cooldown > 0) {
                    return;
                }
                TryStartSignature(npc);
                return;
            }
            if (phase == PhaseBusy) {
                if (--timer <= 0) {
                    phase = PhaseIdle;
                    cooldown = SigCooldown();
                }
                return;
            }
            switch (role) {
                case BossRole.Everscream:
                    NeedleActive(npc);
                    break;
                case BossRole.SantaTank:
                    RamActive(npc);
                    break;
                default:
                    phase = PhaseIdle;
                    break;
            }
        }

        private void TryStartSignature(NPC npc) {
            if (!Eligible(npc)) {
                cooldown = RetryDelay * 2;
                return;
            }
            if (npc.target < 0 || npc.target >= Main.maxPlayers) {
                cooldown = RetryDelay;
                return;
            }
            Player player = Main.player[npc.target];
            if (!player.Alives() || npc.Distance(player.Center) > MaxEngageRange) {
                cooldown = RetryDelay;
                return;
            }

            bool started = false;
            bool preferAlt = useAltSig;
            switch (role) {
                case BossRole.Everscream:
                    //A=饰品炮列 B=松针速射（速射要求视线通畅，不通则回落炮列）
                    started = preferAlt && Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)
                        ? StartNeedle(npc) : StartOrnamentRow(npc, player);
                    break;
                case BossRole.SantaTank:
                    //A=礼炮齐放 B=雪橇冲压（冲压要求目标贴近水平面，不满足则回落齐放）
                    started = preferAlt && Math.Abs(player.Center.Y - npc.Center.Y) <= RamMaxDy
                        && npc.velocity.Y == 0f
                        ? StartRam(npc) : StartSalvo(npc, player);
                    break;
                case BossRole.IceQueen:
                    //A=冰晶华尔兹 B=暴风雪航道（华尔兹要求目标在中程内，不满足则回落航道）
                    started = !preferAlt && npc.Distance(player.Center) <= WaltzMaxRange
                        ? StartWaltz(npc, player) : StartBlizzard(npc, player);
                    break;
            }

            if (started) {
                useAltSig = !useAltSig;
            }
            else {
                cooldown = RetryDelay;
            }
        }

        #region 常绿尖叫怪
        /// <summary>饰品炮列：预兆实体自治（预告亮柱→权威端投放，走廊列恒空）</summary>
        private bool StartOrnamentRow(NPC npc, Player player) {
            int drops = boundTier >= 3 ? 3 : 2;
            int damage = Math.Max(1, (int)(npc.damage * OrnamentDamageFrac));
            //走廊起始列权威掷定（随生成包同步）：保持在内侧，两翼恒有压制列
            int corridor = Main.rand.Next(1, FrmOrnamentRowOmen.RowColumns - FrmOrnamentRowOmen.CorridorClearColumns);
            int index = Projectile.NewProjectile(npc.GetSource_FromAI(), player.Center, Vector2.Zero,
                ModContent.ProjectileType<FrmOrnamentRowOmen>(), 0, 0f, Main.myPlayer,
                drops * 100000 + damage, corridor, npc.whoAmI * 1000 + npc.type);
            if (index < 0 || index >= Main.maxProjectiles) {
                return false;
            }
            //蓄势顿挫
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            //忙窗=预告+投放坠落全程：深井场景（投放顶 452px+地表纵深 736px）落地约 125 帧，取 130 含余量
            phase = PhaseBusy;
            timer = FrmOrnamentRowOmen.TelegraphFrames + 130;
            return true;
        }

        /// <summary>松针速射：瞄准线预告（48 帧 ≥40）→ 冻结出膛点沿承诺线点射</summary>
        private bool StartNeedle(NPC npc) {
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<FrmAimLaneOmen>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, FrmAimLaneOmen.StyleNeedle * 1000 + npc.type, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = FrmAimLaneOmen.NeedleTrackFrames + FrmAimLaneOmen.NeedleLockFrames;
            return true;
        }

        private void NeedleActive(NPC npc) {
            if (phase == PhaseTelegraph) {
                timer--;
                if (timer == FrmAimLaneOmen.NeedleLockFrames + 8) {
                    //中段刹车，压住走位让出膛点贴住预告线
                    npc.velocity.X *= 0.25f;
                    npc.netUpdate = true;
                }
                bool omenValid = TryGetOmen(ModContent.ProjectileType<FrmAimLaneOmen>(), npc.whoAmI, out Projectile omen);
                if (!omenValid) {
                    //预告体缺位：无预告不开火（失败方向=安全方向）
                    phase = PhaseIdle;
                    cooldown = SigCooldown();
                    return;
                }
                if (timer == FrmAimLaneOmen.NeedleLockFrames) {
                    //锁定帧：方向与出膛点双承诺
                    lockedAngle = npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()
                        ? (Main.player[npc.target].Center - npc.Center).ToRotation()
                        : omen.rotation;
                    lockedOrigin = npc.Center;
                    omen.ai[2] = lockedAngle + 10f;
                    omen.netUpdate = true;
                }
                if (timer <= 0) {
                    phase = PhaseExecute;
                    timer = NeedleStreamFrames;
                }
                return;
            }

            if (phase != PhaseExecute) {
                phase = PhaseIdle;
                return;
            }
            //速射窗：自冻结出膛点沿承诺线点射，出膛散布被 NeedleSpawnJitter 钳制、弹向不转向
            if (timer % NeedleFireInterval == 0) {
                float perp = Main.rand.NextFloat(-1f, 1f) * NeedleSpawnJitter;
                Vector2 spawnPos = lockedOrigin + (lockedAngle + MathHelper.PiOver2).ToRotationVector2() * perp;
                int damage = Math.Max(1, (int)(npc.damage * NeedleDamageFrac));
                Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos,
                    lockedAngle.ToRotationVector2() * NeedleSpeed,
                    ModContent.ProjectileType<FrmPineNeedleProj>(), damage, 0.5f, Main.myPlayer,
                    FrmAimLaneOmen.NeedleLaneLength);
            }
            if (--timer <= 0) {
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = SigCooldown();
            }
        }
        #endregion

        #region 圣诞坦克
        /// <summary>礼炮齐放：多落点标记，落点间距 = 弹着直径 + 具名安全净空（放点循环直接读取）</summary>
        private bool StartSalvo(NPC npc, Player player) {
            int count = boundTier >= 2 ? SalvoCountHighTier : SalvoCountBase;
            float spacing = FrmMortarBlastProj.BlastRadius * 2f * SalvoScale + SalvoSafeGap;
            int damage = Math.Max(1, (int)(npc.damage * SalvoDamageFrac));
            int placed = 0;
            for (int i = 0; i < count; i++) {
                float x = player.Center.X + (i - (count - 1) * 0.5f) * spacing;
                //各列独立地表扫描；无地表的列跳过（缺口只会更宽，失败方向=安全方向）
                if (!FrmSiegeUtils.TryFindGroundY(new Vector2(x, player.Bottom.Y - 8f), 46, out float groundY)) {
                    continue;
                }
                FrmSiegeUtils.SpawnMortarShot(npc, new Vector2(x, groundY), SalvoFlightFrames, SalvoScale, damage);
                placed++;
            }
            if (placed <= 0) {
                return false;
            }
            //齐放后座
            npc.velocity.X *= 0.2f;
            npc.netUpdate = true;
            phase = PhaseBusy;
            timer = SalvoFlightFrames + FrmMortarBlastProj.BurstFrames + 12;
            return true;
        }

        /// <summary>雪橇冲压：水平直线预告（48 帧 ≥40）→ 定距直线冲压</summary>
        private bool StartRam(NPC npc) {
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<FrmAimLaneOmen>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, FrmAimLaneOmen.StyleRam * 1000 + npc.type, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                return false;
            }
            //压履带蓄势
            npc.velocity.X *= 0.15f;
            npc.netUpdate = true;
            phase = PhaseTelegraph;
            timer = FrmAimLaneOmen.RamTrackFrames + FrmAimLaneOmen.RamLockFrames;
            return true;
        }

        private void RamActive(NPC npc) {
            if (phase == PhaseTelegraph) {
                timer--;
                if (timer == FrmAimLaneOmen.RamLockFrames + 8) {
                    npc.velocity.X *= 0.2f;
                    npc.netUpdate = true;
                }
                bool omenValid = TryGetOmen(ModContent.ProjectileType<FrmAimLaneOmen>(), npc.whoAmI, out Projectile omen);
                if (!omenValid) {
                    phase = PhaseIdle;
                    cooldown = SigCooldown();
                    return;
                }
                if (timer == FrmAimLaneOmen.RamLockFrames) {
                    //锁定帧：只承诺水平方向
                    float dir = 1f;
                    if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                        dir = Main.player[npc.target].Center.X >= npc.Center.X ? 1f : -1f;
                    }
                    lockedAngle = dir > 0f ? 0f : MathHelper.Pi;
                    omen.ai[2] = lockedAngle + 10f;
                    omen.netUpdate = true;
                }
                if (timer <= 0) {
                    //冲压注入：按运行时旗标决定补偿，实际横速恒为 RamSpeed（预告线长=RamSpeed×RamFrames）
                    float dir = lockedAngle == 0f ? 1f : -1f;
                    ramVX = dir * RamSpeed / MoveGain(npc);
                    npc.velocity.X = ramVX;
                    npc.netUpdate = true;
                    phase = PhaseExecute;
                    timer = RamFrames;
                }
                return;
            }

            if (phase == PhaseExecute) {
                timer--;
                //钉住横速抵抗原版坦克 AI 的转向（纵向留给重力）
                npc.velocity.X = ramVX;
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustPerfect(npc.Bottom + new Vector2(-ramVX * 2f, -4f), DustID.Snow,
                        new Vector2(-ramVX * 0.2f, -Main.rand.NextFloat(1f, 3f)), 100, default, Main.rand.NextFloat(1f, 1.6f));
                    dust.noGravity = true;
                }
                //撞墙即止（collideX 由物块碰撞各端一致判定）
                if (timer <= 0 || npc.collideX) {
                    npc.velocity.X *= 0.2f;
                    npc.netUpdate = true;
                    phase = PhaseRecover;
                    timer = RamRecoverFrames;
                }
                return;
            }

            if (--timer <= 0) {
                phase = PhaseIdle;
                omenIndex = -1;
                cooldown = SigCooldown();
            }
        }
        #endregion

        #region 冰雪女王
        /// <summary>冰晶华尔兹：环心锁定在此刻目标位置（预告即承诺），安全楔权威掷定后随生成包同步</summary>
        private bool StartWaltz(NPC npc, Player player) {
            float wedge = Main.rand.NextFloat(-MathHelper.Pi, MathHelper.Pi);
            int damage = Math.Max(1, (int)(npc.damage * WaltzDamageFrac));
            int index = Projectile.NewProjectile(npc.GetSource_FromAI(), player.Center, Vector2.Zero,
                ModContent.ProjectileType<FrmWaltzRingProj>(), damage, 1f, Main.myPlayer, wedge);
            if (index < 0 || index >= Main.maxProjectiles) {
                return false;
            }
            phase = PhaseBusy;
            timer = FrmWaltzRingProj.FadeInFrames + FrmWaltzRingProj.ContractFrames + 28;
            return true;
        }

        /// <summary>暴风雪航道：航道居中于此刻目标（预告即承诺），扫向权威掷定</summary>
        private bool StartBlizzard(NPC npc, Player player) {
            float dir = Main.rand.NextBool() ? 1f : -1f;
            int damage = Math.Max(1, (int)(npc.damage * BlizzardDamageFrac));
            Vector2 start = new Vector2(player.Center.X - dir * FrmBlizzardFrontProj.LaneLength * 0.5f, player.Center.Y);
            int index = Projectile.NewProjectile(npc.GetSource_FromAI(), start, Vector2.Zero,
                ModContent.ProjectileType<FrmBlizzardFrontProj>(), damage, 1f, Main.myPlayer, dir);
            if (index < 0 || index >= Main.maxProjectiles) {
                return false;
            }
            phase = PhaseBusy;
            timer = FrmBlizzardFrontProj.PreviewFrames + FrmBlizzardFrontProj.SweepFrames + 24;
            return true;
        }
        #endregion
    }
}
