using CalamityOverhaul.Common;
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

        //SaveMod 的 DoSave/DoLoad 在 InnoVault 侧不兜底，这里跑在模组加载期，
        //版本戳文件坏了不该让模组加载失败；读不到时 SaveVersion 留空，Version 的 < 对 null 安全（视为新版本）
        public override void SetStaticDefaults() {
            CWRSaveData.Guard(nameof(CheckedVersions) + ".SetStaticDefaults", () => {
                if (!HasSave) {
                    DoSave<CheckedVersions>();
                }
                DoLoad<CheckedVersions>();
            });
        }

        public override void SaveData(TagCompound tag) {
            tag["Versions"] = Mod.Version;
        }

        public override void LoadData(TagCompound tag) {
            if (!tag.TrySafeGet("Versions", out SaveVersion, nameof(CheckedVersions)) || SaveVersion == null) {
                SaveVersion = Mod.Version;
            }
        }
    }
}
