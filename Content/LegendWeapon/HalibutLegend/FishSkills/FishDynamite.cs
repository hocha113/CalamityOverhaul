using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishDynamite : FishSkill
    {
        //使用反射加载灰度纹理，这个是一个光束纹理，大小高1024宽256，
        //光束朝正上方，适合用来复合一些光束一类的特效或者爆炸的旋转光束演出
        [VaultLoaden(CWRConstant.Masking)]
        public static Texture2D LightBeam;
        public override int UnlockFishID => ItemID.DynamiteFish;
        public override int DefaultCooldown => 60 * (12 - HalibutData.GetDomainLayer());
    }
}
