namespace CalamityOverhaul.Content.NPCs.Victors
{
    internal enum VictorRequestKind : byte
    {
        Install = 1,
        Uninstall = 2,
        Purchase = 3,
    }

    internal enum VictorResultCode : byte
    {
        Success,
        InvalidSession,
        ConflictingRequest,
        ExpiredRequest,
        InvalidPlayer,
        InvalidVictor,
        InvalidPayload,
        StaleLoadout,
        InvalidInventoryItem,
        CapacityExceeded,
        InventoryFull,
        InsufficientFunds,
        RateLimited,
    }

    internal enum VictorRequestDisposition : byte
    {
        New,
        Replay,
        Invalid,
        Conflict,
        Expired,
    }

    internal readonly record struct VictorRequestToken(
        uint SessionGeneration,
        uint RequestId,
        uint LoadoutRevision)
    {
        internal bool IsValid => SessionGeneration != 0 && RequestId != 0
            && LoadoutRevision != 0;
    }

    /// <summary>
    /// 权威端裁决结果；<see cref="AuthorityPrice"/> 仅购买有效，请求方本机据此扣款
    /// </summary>
    internal readonly record struct VictorRequestResult(
        uint RequestSessionGeneration,
        uint RequestId,
        VictorRequestKind Kind,
        VictorResultCode Code,
        uint AuthorityLoadoutRevision,
        long AuthorityPrice = 0L)
    {
        internal bool IsValid => RequestSessionGeneration != 0 && RequestId != 0
            && AuthorityLoadoutRevision != 0 && AuthorityPrice >= 0L
            && VictorProtocol.IsValidKind(Kind)
            && VictorProtocol.IsValidResultCode(Code);

        internal bool IsSuccess => IsValid && Code == VictorResultCode.Success;
    }

    internal static class VictorProtocol
    {
        internal static bool IsValidKind(VictorRequestKind kind)
            => kind is VictorRequestKind.Install
                or VictorRequestKind.Uninstall
                or VictorRequestKind.Purchase;

        internal static bool IsValidResultCode(VictorResultCode code)
            => code is >= VictorResultCode.Success
                and <= VictorResultCode.RateLimited;
    }
}
