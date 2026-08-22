using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 增益抽取（芯片档，即时）：抽走防守方一个正面 buff，转授攻击方。<br/>
    /// <b>回执载荷的示范用例</b>：msg 50 给远端的 buffTime 全是 60 占位（设计 §0.7），
    /// 全世界只有防守方本机知道真实剩余时长，防守方在 <see cref="OnDefenderApply"/> 里
    /// 挑走 buff 并把 (型号, 真值时长) 写进 <see cref="WriteReceiptPayload"/>；
    /// 服务端 <see cref="HandleReceiptPayload"/> 校验后以 msg 55 直发攻击方
    /// （服务端直发不过 pvpBuff 转播闸，§0.9 的原版先例），攻击方本机 AddBuff 落地
    /// ：攻击方资源归攻击方客户端，服务端只转发（结算落点表 §1.4）。<br/>
    /// 防守方无可抽 buff 时本机终审拒绝（返回 false → 回执 Rejected → 服务端全额退款）
    /// </summary>
    internal class BuffSiphon : PlayerHackDef
    {
        /// <summary>转授时长封顶（帧）：三十秒，长效药不整瓶搬家</summary>
        internal const int TransferCapFrames = 60 * 30;
        /// <summary>剩余低于一秒的不抽：篝火/红心灯这类光环 buff 每帧重挂，抽了等于没抽</summary>
        internal const int MinStealFrames = 60;

        private static readonly Color Siphon = new(150, 255, 190);

        /// <summary>晶粒纹：躯体里的增益箭头被导管吸出，落在管口重新立起，增益换了主人</summary>
        internal const string Die =
            "M -0.72 -0.28 L -0.32 -0.28 M -0.72 0.52 L -0.32 0.52 "
            + "M -0.72 -0.28 Q -0.80 0.12 -0.72 0.52 M -0.32 -0.28 Q -0.24 0.12 -0.32 0.52 "
            + "M -0.52 0.30 L -0.52 0.02 M -0.62 0.14 L -0.52 0.02 L -0.42 0.14 "
            + "M -0.36 -0.10 Q 0.06 -0.52 0.42 -0.34 "
            + "M 0.02 -0.34 L 0.08 -0.26 M 0.20 -0.40 L 0.26 -0.32 "
            + "M 0.54 -0.14 L 0.54 -0.46 M 0.42 -0.32 L 0.54 -0.46 L 0.66 -0.32";

        /// <summary>防守方侧 per-effect 状态：被抽走的 buff 真值，回执载荷从这里读</summary>
        private sealed class SiphonState
        {
            public int BuffType;
            public int BuffTime;
        }

        public override void SetDefaults() {
            UploadTime = 140;
            RamCost = 5;
            Category = QuickHackCategory.Covert;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 0;

        /// <summary>
        /// 可抽判定，一份谓词各端共用：攻击方预检与服务端校验读的是 msg 50 同步来的
        /// buff 型号（时长占位 60 恰好过 <see cref="MinStealFrames"/> 线，预检宽松无害），
        /// 防守方本机终审读真值，最终裁决权在真值端
        /// </summary>
        internal static bool IsSiphonable(int type, int time) {
            if (type <= 0 || type >= BuffLoader.BuffCount) return false;
            if (Main.debuff[type] || Main.vanityPet[type] || Main.lightPet[type]) {
                return false;
            }
            if (BuffID.Sets.BasicMountData[type] != null) return false;
            return time >= MinStealFrames;
        }

        private static bool AnySlotHit(Player player) {
            for (int i = 0; i < Player.MaxBuffs; i++) {
                if (IsSiphonable(player.buffType[i], player.buffTime[i])) return true;
            }
            return false;
        }

        //空手的目标灰显在预检层：buff 型号原版全员同步，攻击方与服务端都看得到
        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (target is not PlayerScannable scan) return false;
            Player defender = scan.ResolvePlayer();
            return defender != null && AnySlotHit(defender);
        }

        #region 防守方通道（真值端挑选与摘除）

        public override bool OnDefenderApply(Player defender, PlayerHackEffect effect) {
            //抽剩余最长的一个：最值钱，且判定确定（不掷骰）
            int bestSlot = -1;
            for (int i = 0; i < Player.MaxBuffs; i++) {
                if (!IsSiphonable(defender.buffType[i], defender.buffTime[i])) continue;
                if (bestSlot == -1 || defender.buffTime[i] > defender.buffTime[bestSlot]) {
                    bestSlot = i;
                }
            }
            //本机真值无可抽（预检吃了占位时长的亏）→ 终审拒绝，服务端全额退攻击方
            if (bestSlot == -1) return false;

            effect.ProtocolState = new SiphonState {
                BuffType = defender.buffType[bestSlot],
                BuffTime = defender.buffTime[bestSlot],
            };
            defender.DelBuff(bestSlot);

            SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.7f, Pitch = 0.25f },
                defender.Center);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    defender.Top + Main.rand.NextVector2Circular(12f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f),
                        Main.rand.NextFloat(-3.2f, -1.4f)),
                    Siphon, Main.rand.NextFloat(0.6f, 1f))?.Configure(false, 20);
            }
            return true;
        }

        public override void WriteReceiptPayload(BinaryWriter writer,
            PlayerHackEffect effect) {
            //防守方回传真值；异常态写零载荷，服务端按无效丢弃（不转授不报错）
            SiphonState state = effect.ProtocolState as SiphonState;
            writer.Write((ushort)(state?.BuffType ?? 0));
            writer.Write(state?.BuffTime ?? 0);
        }

        #endregion

        #region 权威通道（服务端只转发，不落地）

        public override void HandleReceiptPayload(BinaryReader reader, Player caster,
            Player defender, PlayerHackGrant grant) {
            //先吃干净再校验（读包纪律；子流虽隔离，字段仍与写侧一一对应）
            int buffType = reader.ReadUInt16();
            int buffTime = reader.ReadInt32();
            if (Main.netMode != NetmodeID.Server) return;
            if (!IsSiphonable(buffType, buffTime)) {
                //防守方自称抽到了不可抽的东西：不转授，点名记日志（§2.8）
                CWRMod.Instance.Logger.Info(
                    $"[HackPvP] BuffSiphon receipt dropped: defender "
                    + $"{grant.DefenderName} reported invalid buff {buffType}/{buffTime}f");
                return;
            }
            //攻击方已离场则转授蒸发（授予账名字双检防槽位换人）
            if (caster?.active != true || caster.name != grant.CasterName) return;
            int granted = Math.Min(buffTime, TransferCapFrames);
            //msg 55 服务端直发：目标客户端收包后本机 AddBuff（buffImmune 在执行端判）
            NetMessage.TrySendData(MessageID.AddPlayerBuff, grant.CasterIndex, -1, null,
                grant.CasterIndex, buffType, granted);
        }

        #endregion

        //即时协议在镜像里只活到看门狗收账：头两帧打一束"增益离体"流光，其后静默
        public override void OnSpectatorTick(Player defender, int casterIndex,
            int elapsed, int duration) {
            if (Main.dedServ || elapsed > 2) return;
            Player caster = casterIndex >= 0 && casterIndex < Main.maxPlayers
                ? Main.player[casterIndex] : null;
            Vector2 dir = caster?.active == true
                ? (caster.Center - defender.Center).SafeNormalize(-Vector2.UnitY)
                : -Vector2.UnitY;
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(defender.Center,
                    dir.RotatedByRandom(0.3f) * Main.rand.NextFloat(3f, 7f),
                    Siphon, 0.8f)?.Configure(false, 16);
            }
        }

        public override string GlyphDiePath => Die;
    }
}
