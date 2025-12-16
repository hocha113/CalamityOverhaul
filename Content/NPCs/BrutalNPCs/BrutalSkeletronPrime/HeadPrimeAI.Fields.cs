using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime
{
    /// <summary>
    /// 存储AI相关字段和属性
    /// </summary>
    internal partial class HeadPrimeAI
    {
        #region Data
        public override int TargetID => NPCID.SkeletronPrime;
        private const int maxfindModes = 6000;
        private Player player;
        private int frame = 0;
        private int frameCount;
        private int primeCannon;
        private int primeSaw;
        private int primeVice;
        private int primeLaser;
        private int oneToTwoPrsAddNumBloodValue;
        private bool cannonAlive;
        private bool viceAlive;
        private bool sawAlive;
        private bool laserAlive;
        private bool bossRush;
        private bool death;
        private bool noArm => !cannonAlive && !laserAlive && !sawAlive && !viceAlive;
        private bool noEye;
        internal static int setPosingStarmCount;
        internal ref float ai0 => ref ai[0];
        internal ref float ai1 => ref ai[1];
        internal ref float ai2 => ref ai[2];
        internal ref float ai3 => ref ai[3];
        internal ref float ai4 => ref ai[4];
        internal ref float ai5 => ref ai[5];
        internal ref float ai6 => ref ai[6];
        internal ref float ai7 => ref ai[7];
        internal ref float ai8 => ref ai[8];
        internal ref float ai9 => ref ai[9];
        internal ref float ai10 => ref ai[10];
        internal ref float ai11 => ref ai[11];
        [VaultLoaden(CWRConstant.NPC + "BSP/MachineRebellion")]
        internal static Asset<Texture2D> MachineRebellionAsset = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BrutalSkeletron")]
        internal static Asset<Texture2D> HandAsset = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPCannon")]
        internal static Asset<Texture2D> BSPCannon = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPlaser")]
        internal static Asset<Texture2D> BSPlaser = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPPliers")]
        internal static Asset<Texture2D> BSPPliers = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPSAW")]
        internal static Asset<Texture2D> BSPSAW = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPRAM")]
        internal static Asset<Texture2D> BSPRAM = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPRAM_Forearm")]
        internal static Asset<Texture2D> BSPRAM_Forearm = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BrutalSkeletronGlow")]
        internal static Asset<Texture2D> HandAssetGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPCannonGlow")]
        internal static Asset<Texture2D> BSPCannonGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPlaserGlow")]
        internal static Asset<Texture2D> BSPlaserGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPPliersGlow")]
        internal static Asset<Texture2D> BSPPliersGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPSAWGlow")]
        internal static Asset<Texture2D> BSPSAWGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPRAMGlow")]
        internal static Asset<Texture2D> BSPRAMGlow = null;
        [VaultLoaden(CWRConstant.NPC + "BSP/BSPRAM_ForearmGlow")]
        internal static Asset<Texture2D> BSPRAM_ForearmGlow = null;
        //下面是用于缓存原版纹理的字段
        internal static Asset<Texture2D> Vanilla_TwinsBossBag;
        internal static Asset<Texture2D> Vanilla_DestroyerBossBag;
        internal static Asset<Texture2D> Vanilla_SkeletronPrimeBossBag;
        private static int iconIndex;
        #endregion
    }
}
