using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EyeOfCthulhu
{
    /// <summary>
    /// 血雾之瞳：克苏鲁之眼残酷遗物。把克眼的血雾伏击反转成玩家能力：<br/>
    /// 致命伤化雾免死并向光标重凝(重凝后短暂虚弱)、双击方向键血雾突进、
    /// 突进/重凝后短窗口伏击必暴
    /// </summary>
    internal class BloodfogIris : BaseBrutalRelic, ICWRLoader
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //同期克眼档掉落物约 1~2 金，取 4 倍档
            Item.value = Item.buyPrice(0, 8, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<BloodfogIrisPlayer>().Equipped = true;
        }

        void ICWRLoader.UnLoadData() {
            BloodfogVeilProj.UnloadTrailResources();
            BloodfogScreenFX.Clear();
        }
    }

    /// <summary>
    /// 血雾之瞳逐玩家状态：免死冷却、突进状态机、伏击窗口全在实例字段。<br/>
    /// 免死与突进只在本机(owner)触发；跨端演出全部由随身雾裹弹幕承载，
    /// 拖尾采样与雾态视觉计时由各客户端本地推进，保持多人一致
    /// </summary>
    internal class BloodfogIrisPlayer : ModPlayer
    {
        #region 数值
        /// <summary>免死冷却(帧)，60 秒</summary>
        public const int DodgeCooldownFrames = 3600;
        /// <summary>免死后无敌帧</summary>
        public const int DodgeImmuneFrames = 45;
        /// <summary>雾体虚弱窗口(帧)：重凝后受到的伤害提高，免死的代价</summary>
        public const int MistWeaknessFrames = 180;
        /// <summary>雾体虚弱受伤倍率</summary>
        public const float MistWeaknessMul = 1.15f;
        /// <summary>重凝最近距离(px)</summary>
        public const float DodgeMinDist = 150f;
        /// <summary>重凝最远距离(px)</summary>
        public const float DodgeMaxDist = 430f;
        /// <summary>突进反向预备帧</summary>
        public const int DashWindupFrames = 3;
        /// <summary>突进满速帧</summary>
        public const int DashTravelFrames = 10;
        /// <summary>突进急刹帧</summary>
        public const int DashBrakeFrames = 3;
        public const int DashTotalFrames = DashWindupFrames + DashTravelFrames + DashBrakeFrames;
        /// <summary>突进速度(px/帧)</summary>
        public const float DashSpeed = 26f;
        /// <summary>突进起手无敌帧(只够穿模)</summary>
        public const int DashImmuneFrames = 8;
        /// <summary>突进冷却(帧)，收招起算</summary>
        public const int DashCooldownFrames = 75;
        /// <summary>伏击窗口(帧)，1.5 秒</summary>
        public const int AmbushWindowFrames = 90;
        /// <summary>常驻暴击率加成(%)</summary>
        public const int CritChanceBonus = 8;
        /// <summary>拖尾采样点寿命(帧)</summary>
        public const int TrailPointLife = 22;
        private const int MaxTrailPoints = 40;
        #endregion

        #region 状态
        /// <summary>本帧是否装备，物品钩子逐帧点亮</summary>
        public bool Equipped;
        /// <summary>免死冷却剩余</summary>
        public int DodgeCooldown;
        /// <summary>突进冷却剩余</summary>
        public int DashCooldown;
        /// <summary>伏击窗口剩余，>0 时下一击必暴</summary>
        public int AmbushWindow;
        /// <summary>雾体虚弱剩余(仅 owner 端置位)，期间受到的伤害提高15%</summary>
        public int MistWeaknessTimer;
        /// <summary>雾态视觉计时，由随身雾裹弹幕在各端每帧点亮</summary>
        public int VeilVisualTimer;
        /// <summary>突进剩余帧，>0 为突进中(仅 owner 有效)</summary>
        private int dashTimer;
        private Vector2 dashDir;
        /// <summary>拖尾热度 0~1，由位移速度推导，各端一致</summary>
        public float TrailHeat;

        public struct TrailPoint
        {
            public Vector2 Pos;
            /// <summary>过期时刻(GameUpdateCount)</summary>
            public long DeathAt;
        }

        /// <summary>血带拖尾采样，旧点在前；各客户端本地自采</summary>
        public readonly System.Collections.Generic.List<TrailPoint> TrailPoints = new(MaxTrailPoints + 4);
        #endregion

        #region 计时
        public override void ResetEffects() {
            //先走计时再清旗：就绪提示要读上一帧的装备状态
            TickTimers();
            Equipped = false;
        }

        //死亡期间 ResetEffects 不跑，冷却在这里照常流逝；死亡同时打断突进与伏击
        public override void UpdateDead() {
            TickTimers();
            dashTimer = 0;
            AmbushWindow = 0;
            TrailPoints.Clear();
            TrailHeat = 0f;
        }

        private void TickTimers() {
            if (DodgeCooldown > 0) {
                DodgeCooldown--;
                if (DodgeCooldown == 0) {
                    PlayReadyCue();
                }
            }
            if (DashCooldown > 0) {
                DashCooldown--;
            }
            if (AmbushWindow > 0) {
                AmbushWindow--;
            }
            if (MistWeaknessTimer > 0) {
                MistWeaknessTimer--;
            }
            if (VeilVisualTimer > 0) {
                VeilVisualTimer--;
            }
        }

        /// <summary>免死就绪提示：瞳孔微光一闪，仅本机</summary>
        private void PlayReadyCue() {
            if (Player.whoAmI != Main.myPlayer || VaultUtils.isServer || !Equipped) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.4f, Volume = 0.55f }, Player.Center);
            EocMotion.MistPuff(Player.Center, 2, 0.9f, 0.35f);
        }
        #endregion

        public override void PostUpdateEquips() {
            if (Equipped) {
                Player.GetCritChance(DamageClass.Generic) += CritChanceBonus;
            }
            //帧戳：装备或雾态存续时盖戳，全屏合成层凭此免于空扫弹幕表
            if (Equipped || VeilVisualTimer > 0) {
                BloodfogIrisRender.ActiveStamp.Stamp();
            }
            //雾态仇恨压制：VeilVisualTimer 由雾裹弹幕在各端(含服务端)点亮，
            //写在玩家更新阶段，NPC 索敌同帧读到
            if (VeilVisualTimer > 0) {
                Player.aggro -= 400;
            }
            //双击检测必须在这里跑：原版在 Update 中段把 releaseLeft/Right 改写为"按住即 false"，
            //到 PreUpdateMovement 时按键沿已不可见，检测恒假（NinjaCthulsparkBoots 同款既证，反馈 #29）
            if (Player.whoAmI == Main.myPlayer && Equipped && !Player.dead) {
                TryStartDash();
            }
        }

        #region 突进
        public override void PreUpdateMovement() {
            if (Player.whoAmI != Main.myPlayer || !Equipped || Player.dead) {
                return;
            }

            if (dashTimer <= 0) {
                return;
            }

            int elapsed = DashTotalFrames - dashTimer;
            if (elapsed < DashWindupFrames) {
                //反向预备：pow8 末帧猛缩
                float tw = (elapsed + 1f) / DashWindupFrames;
                Player.velocity = -dashDir * 4.6f * MathF.Pow(tw, 8f);
            }
            else if (elapsed == DashWindupFrames) {
                //一帧满速，起手演出交给随身雾裹弹幕首帧(各端可见)；无敌只够穿模
                Player.velocity = dashDir * DashSpeed;
                Player.GivePlayerImmuneState(DashImmuneFrames, false);
                SpawnShroudVeil(0, DashTravelFrames + DashBrakeFrames + AmbushWindowFrames + 26);
            }
            else if (elapsed < DashWindupFrames + DashTravelFrames) {
                //满速保持，微衰减避免匀速僵直
                Player.velocity = dashDir * DashSpeed * (1f - (elapsed - DashWindupFrames) * 0.012f);
            }
            else {
                //急刹
                Player.velocity *= 0.52f;
            }

            //突进全程免坠落伤害，并当帧压掉原版冲刺处理
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.dashDelay = Math.Max(Player.dashDelay, 2);

            dashTimer--;
            if (dashTimer == 0) {
                DashCooldown = DashCooldownFrames;
                AmbushWindow = AmbushWindowFrames;
            }
        }

        /// <summary>双击窗口：首按当帧被置 15，二按时必然 &lt;15 且 &gt;0</summary>
        private static bool TapWindow(Player player, int index)
            => player.doubleTapCardinalTimer[index] > 0 && player.doubleTapCardinalTimer[index] < 15;

        private void TryStartDash() {
            if (dashTimer > 0 || DashCooldown > 0
                || Player.mount.Active || Player.grapCount > 0 || Player.pulley || Player.CCed) {
                return;
            }

            //双击左/右/上触发；下双击留给平台下落与其他模组套装，避免误触
            Vector2 dir = Vector2.Zero;
            int tapDir = -1;
            if (Player.controlRight && Player.releaseRight && TapWindow(Player, 2)) {
                dir = Vector2.UnitX;
                tapDir = 2;
            }
            else if (Player.controlLeft && Player.releaseLeft && TapWindow(Player, 3)) {
                dir = -Vector2.UnitX;
                tapDir = 3;
            }
            else if (Player.controlUp && Player.releaseUp && TapWindow(Player, 1)) {
                dir = -Vector2.UnitY;
                tapDir = 1;
            }
            if (dir == Vector2.Zero) {
                return;
            }
            //同帧同方向双击位移技消费闩：被别家抢走则本帧静默放弃
            if (!Player.CWR().TryConsumeRelicDoubleTap(tapDir)) {
                return;
            }

            dashTimer = DashTotalFrames;
            dashDir = dir;
            //压制原版克苏鲁护盾类冲刺在本窗口内起步
            Player.dashDelay = Math.Max(Player.dashDelay, DashTotalFrames + 4);

            //预备吸气：雾丝向身体收拢，仅本机小演出
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.5f, Pitch = -0.5f }, Player.Center);
                EocMotion.ConvergeStreaks(Player.Center, 0.3f, 90f);
                EocMotion.MistPuff(Player.Center - dir * 26f, 2, 0.8f, 0.35f);
            }
        }
        #endregion

        #region 免死重凝
        //排在原版黑腰带/混乱之脑/圣骑士闪避与 FreeDodge 之后，别人闪掉的伤害不耗本效果
        public override bool ConsumableDodge(Player.HurtInfo info) {
            if (!Equipped || DodgeCooldown > 0) {
                return false;
            }
            //只截致命伤
            if (info.Damage < Player.statLife) {
                return false;
            }
            ExecuteBloodfogRebirth();
            return true;
        }

        /// <summary>化雾免死：消隐点雾爆 + 向光标方向找落点重凝 + 无敌 + 伏击窗口 + 3秒雾体虚弱</summary>
        private void ExecuteBloodfogRebirth() {
            Vector2 oldCenter = Player.Center;
            Vector2 aim = Main.MouseWorld - oldCenter;
            Vector2 dir = aim.SafeNormalize(-Vector2.UnitY);
            float wantDist = MathHelper.Clamp(aim.Length(), DodgeMinDist, DodgeMaxDist);

            //由远及近找不嵌固体的落点，找不到就原地凝形
            Vector2 target = oldCenter;
            bool found = false;
            for (float dist = wantDist; dist >= 60f; dist -= 24f) {
                Vector2 candidate = oldCenter + dir * dist;
                if (candidate.X < 336f || candidate.X > Main.maxTilesX * 16f - 336f
                    || candidate.Y < 336f || candidate.Y > Main.maxTilesY * 16f - 336f) {
                    continue;
                }
                if (Collision.SolidCollision(candidate - Player.Size * 0.5f, Player.width, Player.height)) {
                    continue;
                }
                target = candidate;
                found = true;
                break;
            }

            DodgeCooldown = DodgeCooldownFrames;
            Player.GivePlayerImmuneState(DodgeImmuneFrames, false);
            AmbushWindow = AmbushWindowFrames;
            MistWeaknessTimer = MistWeaknessFrames;   //雾体虚弱：免死的代价
            TrailPoints.Clear();   //传送切断血带

            //消隐点雾爆(弹幕承载，各端可见)
            SpawnBurstVeil(oldCenter);

            if (found) {
                Vector2 topLeft = target - Player.Size * 0.5f;
                Player.Teleport(topLeft, TeleportationStyleID.RodOfDiscord);
                if (Main.netMode == NetmodeID.MultiplayerClient) {
                    NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0,
                        Player.whoAmI, topLeft.X, topLeft.Y, TeleportationStyleID.RodOfDiscord);
                }
                //凝形后顺光标方向缓移，读作雾体漂到位
                Player.velocity = dir * 5f;
            }

            //重凝落点随身雾裹
            SpawnShroudVeil(2, DodgeImmuneFrames + AmbushWindowFrames + 20);

            BloodfogScreenFX.PushFlash(0.5f);
            EocMotion.Shake(Player.Center, 6f, 12, dir);
        }

        //雾体虚弱：重凝后短窗内受到的伤害加深(受伤结算天然在 owner 端，无同步面)
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (MistWeaknessTimer > 0) {
                modifiers.FinalDamage *= MistWeaknessMul;
            }
        }
        #endregion

        #region 伏击
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (!Equipped || AmbushWindow <= 0) {
                return;
            }
            //伏击必暴，无额外倍率(×2单发消费位归饕餮之喉独占)
            modifiers.SetCrit();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!Equipped || AmbushWindow <= 0) {
                return;
            }
            //一击即耗，印记弹幕承载跨端演出
            AmbushWindow = 0;
            if (Player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(Player.GetSource_Misc("BloodfogIris"), target.Center,
                    Vector2.Zero, ModContent.ProjectileType<BloodfogAmbushMark>(), 0, 0f, Player.whoAmI);
            }
        }
        #endregion

        #region 雾裹弹幕生成(仅 owner 调用)
        /// <summary>随身雾裹：mode 0=突进 2=重凝；已有随身雾裹先杀旧再生新，保持同步干净</summary>
        private void SpawnShroudVeil(int mode, int life) {
            int veilType = ModContent.ProjectileType<BloodfogVeilProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == veilType && proj.owner == Player.whoAmI && proj.ai[0] != 1f) {
                    proj.Kill();
                }
            }
            Projectile.NewProjectile(Player.GetSource_Misc("BloodfogIris"), Player.Center,
                Vector2.Zero, veilType, 0, 0f, Player.whoAmI, mode, life);
        }

        /// <summary>消隐点原地雾爆</summary>
        private void SpawnBurstVeil(Vector2 pos) {
            Projectile.NewProjectile(Player.GetSource_Misc("BloodfogIris"), pos,
                Vector2.Zero, ModContent.ProjectileType<BloodfogVeilProj>(), 0, 0f, Player.whoAmI, 1f, 46f);
        }
        #endregion

        #region 视觉
        //雾态时本体褪成半透酒红，各端一致(VeilVisualTimer 由弹幕在各端点亮)；
        //虚弱期再加深一成(计时仅 owner 端非零，信息属自己)
        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright) {
            bool weakened = MistWeaknessTimer > 0;
            if (VeilVisualTimer <= 0 && !weakened) {
                return;
            }
            r *= 0.78f;
            g *= 0.36f;
            b *= 0.42f;
            a *= 0.55f;
            if (weakened) {
                r *= 0.9f;
                g *= 0.9f;
                b *= 0.9f;
                a *= 0.9f;
            }
        }

        //拖尾各端本地自采：位移推导热度，无需网络包
        public override void PostUpdate() {
            if (Main.dedServ) {
                return;
            }

            long now = Main.GameUpdateCount;
            while (TrailPoints.Count > 0 && TrailPoints[0].DeathAt <= now) {
                TrailPoints.RemoveAt(0);
            }

            if (TrailPoints.Count > 0) {
                float move = Vector2.Distance(TrailPoints[^1].Pos, Player.Center);
                //传送级位移直接斩断
                if (move > 210f) {
                    TrailPoints.Clear();
                    TrailHeat = 0f;
                }
                else {
                    TrailHeat = Math.Max(TrailHeat * 0.93f, MathHelper.Clamp((move - 13f) / 20f, 0f, 1f));
                }
            }
            else {
                TrailHeat *= 0.93f;
            }

            if (VeilVisualTimer <= 0 && TrailHeat < 0.05f) {
                return;
            }

            if (TrailPoints.Count == 0
                || Vector2.DistanceSquared(TrailPoints[^1].Pos, Player.Center) > 36f) {
                TrailPoints.Add(new TrailPoint { Pos = Player.Center, DeathAt = now + TrailPointLife });
                if (TrailPoints.Count > MaxTrailPoints) {
                    TrailPoints.RemoveAt(0);
                }
            }
        }
        #endregion
    }
}
