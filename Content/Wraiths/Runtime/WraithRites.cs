using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>仪式语义（认主叙事框架，见 WRAITHS-DESIGN.md 第三节）</summary>
    public enum WraithRiteKind : byte
    {
        /// <summary>首次铭刻：Unknown/Discovered → Bound @ FirstBindMastery（井中鸣与未来新鬼的路径）</summary>
        FirstBind,
        /// <summary>重续契约（认主）：Bound → RenewedMastery 跃升 + 来历残页解锁</summary>
        RenewPact,
        /// <summary>重新收伏：反噬挣脱体被按回簿上，驾驭度勉强压回躁动线上</summary>
        Resubdue,
    }

    /// <summary>仪式受理裁定，<see cref="WraithRites.Classify"/> 的产物</summary>
    internal enum WraithRiteDenial : byte
    {
        /// <summary>受理</summary>
        None,
        /// <summary>封印中（记录为 Sealed，或记录缺失而定义生来封印——封不穿）</summary>
        Sealed,
        /// <summary>挣脱体只认把它放走的那只手（非其挣脱源，或簿上无该鬼的契约）</summary>
        EscapedNotYours,
    }

    /// <summary>
    /// 仪式数据路径，服务器确认制事务：<br/>
    /// owner 按键 → 本地预检（同源谓词）→ 请求服务器（单人即权威直办）→
    /// 服务器复核（存活/持刀/判距/死机/挣脱归属）并消耗实体 → 向发起者回执确认 →
    /// 发起者收到确认才写簿 + 推送持有槽同步 + 演出；服务器拒绝则不写不演。<br/>
    /// 语义判定 <see cref="Classify"/> 为 owner 预检与服务器复核共用，绝不两处维护
    /// </summary>
    internal static class WraithRites
    {
        /// <summary>仪式受理半径（像素），死机提示的显示判距与其同源</summary>
        public const float RiteRange = 240f;
        /// <summary>服务器复核判距的宽松系数（请求飞行期间双方都在动）</summary>
        public const float ServerRangeSlack = 1.5f;

        /// <summary>演出呈现缝，载体方在 SetupData 挂接（客户端演出，数据已先行写入）</summary>
        public static Action<WraithDefinition, WraithRiteKind> RitePresenter;

        /// <summary>演出忙判定缝（铭刻仪式播放中不受理新的借力键），载体方挂接</summary>
        public static Func<bool> PresentationBusy;

        /// <summary>
        /// owner 端受理一次仪式；范围内无死机之鬼返回 false（调用方回退到借力施放）。
        /// 预检被拒（封印/挣脱归属）时吞掉按键并播被拒回执，不发请求
        /// </summary>
        public static bool TryPerform(Player player, WraithVesselHandle vessel) {
            if (player.whoAmI != Main.myPlayer || !vessel.IsValid) {
                return false;
            }

            WraithActor target = FindHaltedTarget(player.Center, RiteRange);
            if (target == null) {
                return false;
            }

            WraithRiteDenial denial = Classify(player, vessel.Store, target, out WraithRiteKind kind);
            if (denial != WraithRiteDenial.None) {
                PlayDenial(denial);
                return true;
            }

            if (VaultUtils.isClient) {
                //事务化:先请求,服务器复核+消耗后回执 RiteConfirm,收到才落簿
                WraithNet.SendRiteRequest(target);
            }
            else {
                //单人即权威:消耗与落簿同帧直办
                ConsumeHalted(target);
                ApplyConfirmed(player, target.Definition.Key, kind);
            }
            return true;
        }

        /// <summary>range 内最近的死机之鬼，没有为 null</summary>
        internal static WraithActor FindHaltedTarget(Vector2 center, float range) {
            WraithActor best = null;
            float bestSq = range * range;
            foreach (WraithActor wraith in ActorLoader.GetActiveActors<WraithActor>()) {
                if (!wraith.IsHalted || wraith.Definition == null) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(center, wraith.Center);
                if (distSq < bestSq) {
                    bestSq = distSq;
                    best = wraith;
                }
            }
            return best;
        }

        /// <summary>
        /// 语义判定（owner 预检与服务器复核同源）：封印兜底 → 挣脱契约态 → 常规三语义。<br/>
        /// 封印按"记录为准、缺失回落定义初始态"裁决——记录缺失也封不穿（鬼律兜底）；<br/>
        /// 挣脱体只受理其挣脱源的 Resubdue，FirstBind/RenewPact 对挣脱体一律不受理
        /// </summary>
        internal static WraithRiteDenial Classify(Player player, WraithProgressStore store, WraithActor target, out WraithRiteKind kind) {
            kind = WraithRiteKind.FirstBind;
            WraithDefinition definition = target.Definition;
            bool hasRecord = store.TryGet(definition.Key, out WraithProgressRecord record);

            if (hasRecord ? record.State == WraithBindState.Sealed
                          : definition.InitialBindState == WraithBindState.Sealed) {
                return WraithRiteDenial.Sealed;
            }

            if (target.IsEscaped) {
                bool mine = target.EscapedOwnerPlayer?.whoAmI == player.whoAmI;
                if (!mine || !hasRecord || record.State != WraithBindState.Bound) {
                    return WraithRiteDenial.EscapedNotYours;
                }
                kind = WraithRiteKind.Resubdue;
                return WraithRiteDenial.None;
            }

            kind = hasRecord && record.State == WraithBindState.Bound
                ? WraithRiteKind.RenewPact
                : WraithRiteKind.FirstBind;
            return WraithRiteDenial.None;
        }

        /// <summary>
        /// 服务器复核并执行一次仪式请求（<c>WraithNet.RiteRequest</c> 入口）：
        /// 资格全查（存活/持刀/判距/死机/语义），通过即消耗实体并给出确认语义；
        /// 任何一环不过返回 false，什么也不发生
        /// </summary>
        internal static bool TryServerPerform(Player requester, WraithActor target, out WraithRiteKind kind) {
            kind = WraithRiteKind.FirstBind;
            if (VaultUtils.isClient
                || requester == null || !requester.active || requester.dead
                || target == null || !target.Active || !target.IsHalted || target.Definition == null) {
                return false;
            }
            float range = RiteRange * ServerRangeSlack;
            if (Vector2.DistanceSquared(requester.Center, target.Center) > range * range) {
                return false;
            }
            WraithVesselHandle vessel = WraithVessels.ResolveHeld(requester);
            if (!vessel.IsValid) {
                return false;
            }
            if (Classify(requester, vessel.Store, target, out kind) != WraithRiteDenial.None) {
                return false;
            }
            ConsumeHalted(target);
            return true;
        }

        /// <summary>
        /// 确认落簿（owner 端）：单人直办与多人 RiteConfirm 回执共用。
        /// 写入 → 版本自增 → 显式推送持有槽同步 → 回执文本/音 → 仪式演出。
        /// 确认飞行期间把刀换下手的极端时序走随身兜底；刀已彻底离身则本次确认作废（实体已耗，责任自负）
        /// </summary>
        internal static void ApplyConfirmed(Player player, string key, WraithRiteKind kind) {
            if (player == null || player.whoAmI != Main.myPlayer
                || !WraithRegistry.TryGet(key, out WraithDefinition definition)) {
                return;
            }
            WraithVesselHandle vessel = WraithVessels.ResolveHeld(player);
            if (!vessel.IsValid) {
                vessel = WraithVessels.ResolveCarried(player);
            }
            if (!vessel.IsValid) {
                return;
            }

            WraithProgressRecord record = vessel.Store.GetOrCreate(key);
            //确认在途换刀防降级:kind 是按发起时那把刀的记录裁的,落簿以现记录为准——
            //已 Bound 的记录把 FirstBind 升格为 RenewPact 语义,驾驭度一律 Max,PactRenewed 只升不降
            if (kind == WraithRiteKind.FirstBind && record.State == WraithBindState.Bound) {
                kind = WraithRiteKind.RenewPact;
            }
            switch (kind) {
                case WraithRiteKind.FirstBind:
                    record.State = WraithBindState.Bound;
                    record.Mastery = MathHelper.Clamp(Math.Max(record.Mastery, definition.FirstBindMastery), 0f, 1f);
                    //亲手立契:来历残页即刻解锁
                    record.PactRenewed = true;
                    break;
                case WraithRiteKind.RenewPact:
                    record.State = WraithBindState.Bound;
                    record.Mastery = MathHelper.Clamp(Math.Max(record.Mastery, definition.RenewedMastery), 0f, 1f);
                    record.PactRenewed = true;
                    break;
                case WraithRiteKind.Resubdue:
                    //只压回线上,不解锁残页:按回去不等于它重新认了你
                    record.State = WraithBindState.Bound;
                    record.Mastery = MathHelper.Clamp(Math.Max(record.Mastery, WraithDefinition.RestlessThreshold + 0.05f), 0f, 1f);
                    break;
            }
            vessel.Store.BumpVersion();
            WraithVessels.SyncSlot(player, vessel.Item);

            LocalizedText feedback = kind switch {
                WraithRiteKind.FirstBind => WraithSystemText.RiteFirstBind,
                WraithRiteKind.RenewPact => WraithSystemText.RiteRenewPact,
                _ => WraithSystemText.RiteResubdue,
            };
            VaultUtils.Text(feedback.Format(definition.DisplayName.Value), definition.EyeColor);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.5f, Volume = 0.5f }, player.Center);

            RitePresenter?.Invoke(definition, kind);
        }

        private static void PlayDenial(WraithRiteDenial denial) {
            LocalizedText line = denial == WraithRiteDenial.Sealed
                ? WraithSystemText.RiteDeniedSealed
                : WraithSystemText.RiteDeniedEscaped;
            VaultUtils.Text(line.Value, Color.DarkGray);
            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.8f, Volume = 0.4f });
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
