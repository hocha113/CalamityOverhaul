using System.Collections.Generic;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes
{
    internal sealed class AuthorityHackUpload
    {
        public uint SessionId;
        public uint RequestId;
        public int SlotIndex;
        public IHackTarget Target;
        public HackNetworkTarget TargetIdentity;
        public float PaidRamCost;
        public int Elapsed;
        public int UploadFrames;
        public HackQueueState State;
        public long ActivationId;
    }

    /// <summary>每玩家服务端上传队列</summary>
    internal sealed class HackTimeAuthorityPlayer : ModPlayer
    {
        internal readonly List<AuthorityHackUpload> Uploads = [];
        internal uint BoundSessionId;

        public override void Initialize() => ClearAuthorityState();

        public override void PlayerDisconnect() => ClearAuthorityState();

        internal void BindSession(uint sessionId) {
            if (BoundSessionId == sessionId) return;
            Uploads.Clear();
            BoundSessionId = sessionId;
        }

        internal void ClearAuthorityState() {
            Uploads.Clear();
            BoundSessionId = 0;
        }
    }
}
