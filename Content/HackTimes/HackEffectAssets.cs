using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>骇入效果着色器资源</summary>
    internal class HackEffectAssets
    {
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackSynapseBurn { get; private set; }

        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackShortCircuit { get; private set; }

        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackCyberpsychosis { get; private set; }

        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackSystemReset { get; private set; }

        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackOpticOverload { get; private set; }

        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackMemoryWipe { get; private set; }

        [VaultLoaden(CWRConstant.Effects)]
        public static Effect HackContagion { get; private set; }
    }
}
