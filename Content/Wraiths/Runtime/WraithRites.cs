using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>仪式语义，见 WRAITHS-DESIGN.md 第三节</summary>
    public enum WraithRiteKind : byte
    {
        /// <summary>首次铭刻 → Bound @ FirstBindMastery</summary>
        FirstBind,
        /// <summary>认主，驾驭跃升+残页解锁</summary>
        RenewPact,
        /// <summary>收伏挣脱体，驾驭压回躁动线</summary>
        Resubdue,
    }

    /// <summary>仪式受理裁定，Classify 产物</summary>
    internal enum WraithRiteDenial : byte
    {
        /// <summary>受理</summary>
        None,
        /// <summary>封印中，封不穿</summary>
        Sealed,
        /// <summary>挣脱体非己源</summary>
        EscapedNotYours,
    }

    /// <summary>
    /// 仪式确认制。owner 预检 → 服复核消耗 → 回执才落簿。<br/>
    /// <see cref="Classify"/> 预检与复核共用
    /// </summary>
    internal static class WraithRites
    {
        /// <summary>受理半径 px，死机提示同源</summary>
        public const float RiteRange = 240f;
        /// <summary>服复核判距宽松系数</summary>
        public const float ServerRangeSlack = 1.5f;

        /// <summary>演出呈现缝，载体 SetupData 挂接</summary>
        public static Action<WraithDefinition, WraithRiteKind> RitePresenter;

        /// <summary>owner 端受理；无死机目标返回 false；预检拒则吞键播回执</summary>
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
                //先请求，回执才落簿
                WraithNet.SendRiteRequest(target);
            }
            else {
                //单人直办
                ConsumeHalted(target);
                ApplyConfirmed(player, target.Definition.Key, kind);
            }
            return true;
        }

        /// <summary>range 内最近死机之鬼</summary>
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

        /// <summary>语义判定，预检与复核同源；封印缺记录也封；挣脱体只受理其源的 Resubdue</summary>
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

        /// <summary>服复核仪式请求，资格不过返回 false</summary>
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

        /// <summary>确认落簿，单人/多人回执共用；在途换刀走随身兜底</summary>
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
            //在途换刀，已 Bound 则 FirstBind 升格 RenewPact，驾驭只升不降
            if (kind == WraithRiteKind.FirstBind && record.State == WraithBindState.Bound) {
                kind = WraithRiteKind.RenewPact;
            }
            switch (kind) {
                case WraithRiteKind.FirstBind:
                    record.State = WraithBindState.Bound;
                    record.Mastery = MathHelper.Clamp(Math.Max(record.Mastery, definition.FirstBindMastery), 0f, 1f);
                    //亲手立契即解锁残页
                    record.PactRenewed = true;
                    break;
                case WraithRiteKind.RenewPact:
                    record.State = WraithBindState.Bound;
                    record.Mastery = MathHelper.Clamp(Math.Max(record.Mastery, definition.RenewedMastery), 0f, 1f);
                    record.PactRenewed = true;
                    break;
                case WraithRiteKind.Resubdue:
                    //只压回线上，不解锁残页
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

        /// <summary>权威端消耗死机实体；已离窗则跳过</summary>
        internal static void ConsumeHalted(WraithActor wraith) {
            if (wraith == null || !wraith.Active || !wraith.IsHalted) {
                return;
            }
            wraith.BeginDematerialize();
        }
    }
}
