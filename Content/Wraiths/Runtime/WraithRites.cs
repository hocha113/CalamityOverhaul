using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>仪式语义（认主叙事框架，见 WRAITHS-DESIGN.md 第三节）</summary>
    public enum WraithRiteKind : byte
    {
        /// <summary>首次铭刻：Unknown/Discovered → Bound @ FirstBindMastery（井中鸣与未来新鬼的路径）</summary>
        FirstBind,
        /// <summary>重续契约（认主）：Bound 低驾驭 → RenewedMastery 跃升 + 来历残页</summary>
        RenewPact,
        /// <summary>重新收伏：反噬挣脱体被按回簿上，驾驭度勉强压回躁动线上</summary>
        Resubdue,
    }

    /// <summary>
    /// 仪式数据路径：死机窗口内持载体按借力键 → 判定语义 → 写入载体进度 → 消耗实体 → 演出。
    /// 数据写在 owner 端（物品归持有人权威，经 LegendData 既有存档/同步链落地）；
    /// 实体消耗走权威端（单人直呼，多人经 <c>WraithNet.SendRiteConsume</c> 请求）。
    /// 演出经 <see cref="RitePresenter"/> 缝隙交给载体方（鬼切=点鬼簿铭刻仪式）
    /// </summary>
    internal static class WraithRites
    {
        /// <summary>仪式受理半径（像素），死机提示的显示判距与其同源</summary>
        public const float RiteRange = 240f;

        /// <summary>演出呈现缝，载体方在 SetupData 挂接（客户端演出，数据已先行写入）</summary>
        public static Action<WraithDefinition, WraithRiteKind> RitePresenter;

        /// <summary>演出忙判定缝（铭刻仪式播放中不受理新的借力键），载体方挂接</summary>
        public static Func<bool> PresentationBusy;

        /// <summary>
        /// owner 端受理一次仪式；范围内无死机之鬼返回 false（调用方回退到借力施放）。
        /// 封印之鬼吞掉这次按键但拒绝写入（鬼律：封印中不可用）
        /// </summary>
        public static bool TryPerform(Player player, WraithVesselHandle vessel) {
            if (player.whoAmI != Main.myPlayer || !vessel.IsValid) {
                return false;
            }

            //最近的死机之鬼
            WraithActor target = null;
            float bestSq = RiteRange * RiteRange;
            foreach (WraithActor wraith in ActorLoader.GetActiveActors<WraithActor>()) {
                if (!wraith.IsHalted || wraith.Definition == null) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(player.Center, wraith.Center);
                if (distSq < bestSq) {
                    bestSq = distSq;
                    target = wraith;
                }
            }
            if (target == null) {
                return false;
            }

            WraithDefinition definition = target.Definition;
            WraithProgressRecord record = vessel.Store.GetOrCreate(definition.Key);

            if (record.State == WraithBindState.Sealed) {
                VaultUtils.Text(WraithSystemText.RiteDeniedSealed.Value, Color.DarkGray);
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.8f, Volume = 0.4f });
                return true;
            }

            WraithRiteKind kind;
            if (record.State == WraithBindState.Bound) {
                kind = target.IsEscaped ? WraithRiteKind.Resubdue : WraithRiteKind.RenewPact;
            }
            else {
                kind = WraithRiteKind.FirstBind;
            }

            switch (kind) {
                case WraithRiteKind.FirstBind:
                    record.State = WraithBindState.Bound;
                    record.Mastery = MathHelper.Clamp(definition.FirstBindMastery, 0f, 1f);
                    break;
                case WraithRiteKind.RenewPact:
                    record.Mastery = MathHelper.Clamp(Math.Max(record.Mastery, definition.RenewedMastery), 0f, 1f);
                    break;
                case WraithRiteKind.Resubdue:
                    record.Mastery = MathHelper.Clamp(Math.Max(record.Mastery, WraithDefinition.RestlessThreshold + 0.05f), 0f, 1f);
                    break;
            }
            vessel.Store.BumpVersion();

            //消耗实体:仪式吃掉这次死机窗口
            if (VaultUtils.isClient) {
                WraithNet.SendRiteConsume(target);
            }
            else {
                ConsumeHalted(target);
            }

            var feedback = kind switch {
                WraithRiteKind.FirstBind => WraithSystemText.RiteFirstBind,
                WraithRiteKind.RenewPact => WraithSystemText.RiteRenewPact,
                _ => WraithSystemText.RiteResubdue,
            };
            VaultUtils.Text(feedback.Format(definition.DisplayName.Value), definition.EyeColor);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.5f, Volume = 0.5f }, player.Center);

            RitePresenter?.Invoke(definition, kind);
            return true;
        }

        /// <summary>权威端消耗死机实体（仪式收尾）；已离开死机窗口则宽容跳过（时序竞态可接受）</summary>
        internal static void ConsumeHalted(WraithActor wraith) {
            if (wraith == null || !wraith.Active || !wraith.IsHalted) {
                return;
            }
            wraith.BeginDematerialize();
        }
    }
}
