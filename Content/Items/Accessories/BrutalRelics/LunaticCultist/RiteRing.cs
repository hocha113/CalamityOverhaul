using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.LunaticCultist
{
    /// <summary>
    /// 仪轨集环：拜月教邪教徒残酷遗物。攻击/施法逐枚点亮身周符文环，
    /// 集满自动发动当前仪式并轮换（镜像→月焰→星辰）；双击左右方向键消耗符文纱幕步瞬移
    /// </summary>
    internal class RiteRing : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //平衡框架 §9：T4 遗物统一 75 金
            Item.value = Item.buyPrice(0, 75, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            RiteRingPlayer mp = player.GetModPlayer<RiteRingPlayer>();
            mp.MarkEquipped(Item, hideVisual);
            //常驻全伤+集环随符文数追加（8%~16% 波动，峰值靠维护符文数背书）
            player.GetDamage(DamageClass.Generic) += 0.08f + mp.RuneCount * 0.01f;
            player.statManaMax2 += 40;
        }
    }

    /// <summary>
    /// 集环状态机：符文计数与仪式轮换全部 owner 端权威，
    /// 远端只经 <see cref="RiteRingNet"/> 收状态供环形绘制；仪式弹幕由 owner 生成走原版弹幕同步
    /// </summary>
    internal class RiteRingPlayer : ModPlayer
    {
        #region 常量与状态
        /// <summary>一环符文位数</summary>
        public const int RuneMax = 8;
        /// <summary>纱幕步符文消耗</summary>
        public const int VeilStepCost = 3;
        /// <summary>纱幕步冷却(帧)，与符文消耗共同构成次数节流</summary>
        private const int VeilStepCooldownTicks = 40;
        /// <summary>纱幕步最大位移(px)</summary>
        private const float VeilStepDistance = 190f;
        /// <summary>符文获取最小间隔(帧)，防高攻速瞬满</summary>
        private const int RuneGainSpacing = 10;
        /// <summary>符文环基础半径(px)</summary>
        public const float RingBaseRadius = 76f;

        /// <summary>在场帧戳：任一玩家环形显形时盖戳，绘制层据此跳过空场全玩家表扫描</summary>
        internal static ActivityStamp PresenceStamp;

        /// <summary>本帧装备中（ResetEffects 清）</summary>
        public bool Equipped;
        /// <summary>功能栏隐藏可见性时只藏环，机制照常</summary>
        public bool HideRing;
        /// <summary>已点亮符文数 0..<see cref="RuneMax"/>（owner 权威，远端收包）</summary>
        public int RuneCount;
        /// <summary>当前仪式 0镜像 1月焰 2星辰（owner 权威，远端收包）</summary>
        public int RitualIndex;

        //---- 各端本地演出量 ----
        /// <summary>环显形 0~1</summary>
        public float RingReveal;
        /// <summary>仪式定形脉冲，快衰减</summary>
        public float CommitPulse;
        /// <summary>新符文点亮闪光，快衰减</summary>
        public float LitFlash;

        private Item sourceItem;
        private int runeGainCooldown;
        private int veilStepCooldown;
        private bool wasUsingItem;
        //演出侧变化检测（远端由收包驱动同一套）
        private int displayedCount;
        private int displayedIndex;
        //网络：脏标+最小间隔+慢速保活
        private bool syncDirty;
        private int syncSpacing;
        private int keepaliveTimer;
        #endregion

        #region 环几何（绘制与演出共用）
        /// <summary>环当前半径：基础+呼吸</summary>
        public static float RingRadius(Player player) {
            return RingBaseRadius + (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 1.6f + player.whoAmI * 0.9f) * 4f;
        }

        /// <summary>环整体旋转相位</summary>
        public static float RingRotation(Player player) {
            return Main.GlobalTimeWrappedHourly * 0.5f + player.whoAmI * 0.7f;
        }

        /// <summary>第 slot 枚符文的世界坐标（内圈 0.66 倍半径处）</summary>
        public static Vector2 SlotPos(Player player, int slot) {
            float angle = -MathHelper.PiOver2 + RingRotation(player) + MathHelper.TwoPi * slot / RuneMax;
            return player.Center + angle.ToRotationVector2() * (RingRadius(player) * 0.66f);
        }

        /// <summary>仪式主题色 0镜像苍白 1月焰蚀青 2星辰晶青</summary>
        public static Color RitualColor(int index) => index switch {
            1 => CultistMotion.MoonCore,
            2 => CultistMotion.StardustCore,
            _ => CultistMotion.PaleClone,
        };
        #endregion

        #region 生命周期
        public override void ResetEffects() {
            Equipped = false;
            HideRing = false;
        }

        public void MarkEquipped(Item item, bool hidden) {
            sourceItem = item;
            HideRing = hidden;
            Equipped = true;
        }

        public override void UpdateDead() {
            CommitPulse *= 0.9f;
            LitFlash *= 0.85f;
            //死亡清环（owner 端裁决并广播）
            if (Player.whoAmI == Main.myPlayer && RuneCount != 0) {
                RuneCount = 0;
                MarkDirty();
                FlushNet(true);
            }
        }

        public override void PreUpdateMovement() {
            TickPresentation();

            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            //以下 owner 端玩法权威
            if (runeGainCooldown > 0) {
                runeGainCooldown--;
            }
            if (veilStepCooldown > 0) {
                veilStepCooldown--;
            }

            if (!Equipped) {
                if (RuneCount != 0) {
                    RuneCount = 0;
                    MarkDirty();
                }
                wasUsingItem = false;
                FlushNet(false);
                return;
            }

            DetectRuneGain();

            //慢速保活：覆盖晚入场端与丢包
            if (++keepaliveTimer >= 150) {
                keepaliveTimer = 0;
                MarkDirty();
            }
            FlushNet(false);
        }

        //纱幕步双击检测必须在这里跑：原版在 Update 中段把 releaseLeft/Right 改写为
        //"按住即 false"，到 PreUpdateMovement 时按键沿已不可见，检测恒假（与血雾之瞳同病同修，反馈 #29）
        public override void PostUpdateEquips() {
            if (Player.whoAmI == Main.myPlayer && Equipped) {
                DetectVeilStep();
            }
        }
        #endregion

        #region 演出（各端本地，由同步字段的变化沿驱动）
        private void TickPresentation() {
            RingReveal = MathHelper.Lerp(RingReveal, Equipped && !HideRing ? 1f : 0f, 0.1f);
            CommitPulse *= 0.88f;
            LitFlash *= 0.86f;
            if (RingReveal > 0.02f) {
                PresenceStamp.Stamp();
            }

            //仪式轮换沿=定形拍（远端由收包触发同一处）
            if (displayedIndex != RitualIndex) {
                int committed = (RitualIndex + 2) % 3;
                displayedIndex = RitualIndex;
                if (RingReveal > 0.3f) {
                    OnRitualCommitFX(committed);
                }
            }
            if (displayedCount != RuneCount) {
                int prev = displayedCount;
                displayedCount = RuneCount;
                if (RuneCount > prev && RingReveal > 0.3f) {
                    OnRuneLitFX();
                }
            }
        }

        /// <summary>符文点亮：刻位星闪+随集满度爬升的咏唱短音</summary>
        private void OnRuneLitFX() {
            if (VaultUtils.isServer || !CultistMotion.OnScreen(Player.Center, 220f)) {
                return;
            }
            LitFlash = 1f;
            int slot = Utils.Clamp(RuneCount, 1, RuneMax) - 1;
            Vector2 pos = SlotPos(Player, slot);
            PRTLoader.NewParticle<PRT_CultistGlyphFlash>(
                pos, Vector2.Zero, RitualColor(RitualIndex), 0.42f)?.Configure(10);
            float fill = RuneCount / (float)RuneMax;
            SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.26f, Pitch = -0.2f + fill * 0.7f }, pos);
        }

        /// <summary>仪式定形拍：收拢环+符文散射+轻震，重头戏交给仪式弹幕自身</summary>
        private void OnRitualCommitFX(int committedIndex) {
            if (VaultUtils.isServer || !CultistMotion.OnScreen(Player.Center, 300f)) {
                return;
            }
            CommitPulse = 1f;
            Color color = RitualColor(committedIndex);
            CultistMotion.SigilCommitFX(Player.Center, color, 1.1f);
            CultistMotion.RuneBurst(Player.Center, color, 12, 7f);
            CultistMotion.Shake(Player.Center, 3.5f, 8);
        }
        #endregion

        #region 集环（owner 端）
        /// <summary>攻击/施法沿检测：每次武器使用点亮一枚，带最小间隔</summary>
        private void DetectRuneGain() {
            bool usingNow = Player.itemAnimation > 0;
            if (usingNow && !wasUsingItem && runeGainCooldown <= 0 && RuneCount < RuneMax) {
                Item held = Player.HeldItem;
                bool isWeapon = held != null && !held.IsAir && held.damage > 0
                    && held.pick == 0 && held.axe == 0 && held.hammer == 0;
                if (isWeapon) {
                    RuneCount++;
                    runeGainCooldown = RuneGainSpacing;
                    MarkDirty();
                    if (RuneCount >= RuneMax) {
                        FireRitual();
                    }
                }
            }
            wasUsingItem = usingNow;
        }

        /// <summary>集满发动当前仪式并轮换（弹幕全部 owner 生成，走原版同步）</summary>
        private void FireRitual() {
            IEntitySource src = sourceItem != null
                ? Player.GetSource_Accessory(sourceItem) : Player.GetSource_Misc("RiteRing");
            switch (RitualIndex) {
                case 1:
                    FireMoonFlare(src);
                    break;
                case 2:
                    FireStarRite(src);
                    break;
                default:
                    FireMirrorRite(src);
                    break;
            }
            RuneCount = 0;
            RitualIndex = (RitualIndex + 1) % 3;
            MarkDirty();
            FlushNet(true);
        }

        /// <summary>镜像仪式：三座苍白镜身绕身列阵，各齐射三轮当前武器弹幕</summary>
        private void FireMirrorRite(IEntitySource src) {
            for (int i = 0; i < 3; i++) {
                Projectile.NewProjectile(src, Player.Center, Vector2.Zero,
                    ModContent.ProjectileType<RiteMirrorPhantom>(), 0, 0f, Player.whoAmI, i);
            }
        }

        /// <summary>月焰激光：头顶月眼睁开，横扫死光；扫向朝最近敌人所在侧</summary>
        private void FireMoonFlare(IEntitySource src) {
            Vector2 anchor = Player.Center + new Vector2(0f, -176f);
            NPC target = FindNearestTarget(1500f);
            float aim = target != null
                ? (target.Center - anchor).ToRotation()
                : (Player.direction >= 0 ? 0.35f : MathHelper.Pi - 0.35f);
            //扫向：从瞄准角一侧扫到另一侧
            float sweepDir = Main.rand.NextBool() ? 1f : -1f;
            float start = aim - RiteMoonFlare.SweepHalfArc * sweepDir;
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(250f);
            Projectile.NewProjectile(src, anchor, Vector2.Zero,
                ModContent.ProjectileType<RiteMoonFlare>(), damage, 6f, Player.whoAmI, start, sweepDir);
        }

        /// <summary>星辰仪式：头顶星核结晶成形，行星环坍缩后唤落坠星</summary>
        private void FireStarRite(IEntitySource src) {
            int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(195f);
            Projectile.NewProjectile(src, Player.Center + new Vector2(0f, -168f), Vector2.Zero,
                ModContent.ProjectileType<RiteStarCore>(), damage, 5f, Player.whoAmI);
        }

        /// <summary>找最近可追猎目标</summary>
        internal NPC FindNearestTarget(float range) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = Player.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }
        #endregion

        #region 纱幕步（owner 端）
        /// <summary>
        /// 双击左/右方向键触发短距瞬移。与克眼遗物血雾冲刺同用双击语义，
        /// 同帧并存由 CWRPlayer 消费闩裁决；触发后吃掉双击计时防止后续钩子连发
        /// </summary>
        private void DetectVeilStep() {
            if (veilStepCooldown > 0 || Player.mount.Active || Player.grapCount > 0
                || Player.pulley || Player.CCed || Player.dead) {
                return;
            }

            int dir = 0;
            if (Player.controlRight && Player.releaseRight && TapWindow(2)) {
                dir = 1;
            }
            else if (Player.controlLeft && Player.releaseLeft && TapWindow(3)) {
                dir = -1;
            }
            if (dir == 0) {
                return;
            }
            //同帧同方向双击位移技消费闩：被别家抢走则本帧静默放弃(不进冷却不响提示音)
            if (!Player.CWR().TryConsumeRelicDoubleTap(dir == 1 ? 2 : 3)) {
                return;
            }

            if (RuneCount < VeilStepCost) {
                //符文不足：闷响提示，短冷却防音效连响
                veilStepCooldown = 12;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.8f }, Player.Center);
                return;
            }

            //落点扫描：自最远处向回找第一处不卡墙的位置
            Vector2 unit = new(dir, 0f);
            Vector2 dest = Player.position;
            bool found = false;
            for (float d = VeilStepDistance; d >= 48f; d -= 8f) {
                Vector2 tryPos = Player.position + unit * d;
                tryPos.X = MathHelper.Clamp(tryPos.X, 660f, Main.maxTilesX * 16f - 660f - Player.width);
                if (!Collision.SolidCollision(tryPos, Player.width, Player.height)) {
                    dest = tryPos;
                    found = true;
                    break;
                }
            }
            if (!found) {
                veilStepCooldown = 12;
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.8f }, Player.Center);
                return;
            }

            ExecuteVeilStep(dest, dir);
        }

        /// <summary>双击窗口：首按当帧被置 15，二按时必然 &lt;15 且 &gt;0（同 BloodfogIris 判式）</summary>
        private bool TapWindow(int index)
            => Player.doubleTapCardinalTimer[index] > 0 && Player.doubleTapCardinalTimer[index] < 15;

        private void ExecuteVeilStep(Vector2 dest, int dir) {
            Vector2 origin = Player.Center;
            RuneCount -= VeilStepCost;
            veilStepCooldown = VeilStepCooldownTicks;
            MarkDirty();

            //吃掉本次双击，避免同帧再触发其他双击位移
            Player.doubleTapCardinalTimer[2] = 0;
            Player.doubleTapCardinalTimer[3] = 0;

            Player.Teleport(dest, 1);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0,
                    Player.whoAmI, dest.X, dest.Y, 1);
            }
            Player.GivePlayerImmuneState(15, true);

            //出入口裂幕（owner 生成，各端经原版弹幕同步看到）
            IEntitySource src = sourceItem != null
                ? Player.GetSource_Accessory(sourceItem) : Player.GetSource_Misc("RiteRing");
            Projectile.NewProjectile(src, origin, Vector2.Zero,
                ModContent.ProjectileType<RiteVeilRift>(), 0, 0f, Player.whoAmI, 0f, dir);
            Projectile.NewProjectile(src, Player.Center, Vector2.Zero,
                ModContent.ProjectileType<RiteVeilRift>(), 0, 0f, Player.whoAmI, 1f, dir);

            FlushNet(true);
        }
        #endregion

        #region 网络
        private void MarkDirty() => syncDirty = true;

        /// <summary>owner 端节流发送 3 字节状态包</summary>
        private void FlushNet(bool force) {
            if (VaultUtils.isSinglePlayer || Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (syncSpacing > 0) {
                syncSpacing--;
            }
            if (!syncDirty || (!force && syncSpacing > 0)) {
                return;
            }
            syncDirty = false;
            syncSpacing = 5;
            ModPacket packet = CWRNetWork.GetPacket<RiteRingNet>();
            packet.Write((byte)Player.whoAmI);
            packet.Write((byte)RuneCount);
            packet.Write((byte)RitualIndex);
            packet.Send();
        }

        /// <summary>远端/服务端应用收到的状态，演出由变化沿检测驱动</summary>
        internal void ApplyNetState(int count, int index) {
            RuneCount = Utils.Clamp(count, 0, RuneMax);
            RitualIndex = Utils.Clamp(index, 0, 2);
            //远端没有 UpdateAccessory 时机差导致的环隐藏问题：有符文即视作显形
            if (RuneCount > 0) {
                Equipped = true;
            }
        }
        #endregion
    }

    /// <summary>集环状态同步信道：{玩家, 符文数, 仪式序}，服务端中继给其余客户端</summary>
    internal sealed class RiteRingNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) {
            //先读净负载再校验（流对齐纪律）
            int who = reader.ReadByte();
            int count = reader.ReadByte();
            int index = reader.ReadByte();
            //服务端防伪：所有者以连接号为准，包内声明不符即弃（对齐SolarCoreFistNet）
            if (VaultUtils.isServer && who != whoAmI) {
                return;
            }
            if (who >= Main.maxPlayers) {
                return;
            }
            Player player = Main.player[who];
            if (player != null && (VaultUtils.isServer || who != Main.myPlayer)
                && player.TryGetModPlayer(out RiteRingPlayer mp)) {
                mp.ApplyNetState(count, index);
            }
            if (VaultUtils.isServer) {
                ModPacket packet = CWRNetWork.GetPacket<RiteRingNet>();
                packet.Write((byte)who);
                packet.Write((byte)count);
                packet.Write((byte)index);
                packet.Send(-1, whoAmI);
            }
        }
    }
}
