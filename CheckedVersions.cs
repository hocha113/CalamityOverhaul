using InnoVault.GameSystem;
using System;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul
{
    internal class CheckedVersions : SaveMod
    {
        internal static Version SaveVersion;
        internal static bool IsNewVersion {
            get {
                if (CWRMod.Instance == null || CWRMod.Instance.Version == null) {
                    return false;
                }
                return SaveVersion < CWRMod.Instance.Version;
            }
        }
        public override void SetStaticDefaults() {
            if (!HasSave) {
                DoSave<CheckedVersions>();
            }
            DoLoad<CheckedVersions>();
        }

        public override void SaveData(TagCompound tag) {
            tag["Versions"] = Mod.Version;
        }

        public override void LoadData(TagCompound tag) {
            if (!tag.TryGet("Versions", out SaveVersion)) {
                SaveVersion = Mod.Version;
            }
        }
    }
}
