using System.Collections.Generic;

namespace CalamityOverhaul.Content.GunCustomization
{
    public class FeederGunLoader : ICWRLoader
    {
        public static List<GlobalFeederGun> GlobalFeederGuns { get; private set; } = [];
        void ICWRLoader.LoadData() => GlobalFeederGuns = VaultUtils.GetSubclassInstances<GlobalFeederGun>();
        void ICWRLoader.UnLoadData() => GlobalFeederGuns?.Clear();
    }
}
