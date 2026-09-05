using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Yoyos
{
    //21 把原版悠悠球的方案条目：右键环绕指令与简述由族基类统一提供，这里只留认领与伤害倍率

    /// <summary>Y1 木悠悠球</summary>
    internal sealed class GsWoodYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.WoodYoyo;
        internal override float DamageMul => 1.15f;
    }

    /// <summary>Y2 集结</summary>
    internal sealed class GsRallyYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Rally;
        internal override float DamageMul => 1.12f;
    }

    /// <summary>Y3 萎靡</summary>
    internal sealed class GsMalaiseYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.CorruptYoyo;
        internal override float DamageMul => 1.10f;
    }

    /// <summary>Y4 动脉</summary>
    internal sealed class GsArteryYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.CrimsonYoyo;
        internal override float DamageMul => 1.10f;
    }

    /// <summary>Y5 亚马逊</summary>
    internal sealed class GsAmazonYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.JungleYoyo;
        internal override float DamageMul => 1.10f;
    }

    /// <summary>Y6 代码1</summary>
    internal sealed class GsCode1Yoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Code1;
        internal override float DamageMul => 1.08f;
    }

    /// <summary>Y7 勇气</summary>
    internal sealed class GsValorYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Valor;
        internal override float DamageMul => 1.08f;
    }

    /// <summary>Y8 小瀑布</summary>
    internal sealed class GsCascadeYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Cascade;
        internal override float DamageMul => 1.08f;
    }

    /// <summary>Y9 蜂巢</summary>
    internal sealed class GsHiveFiveYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.HiveFive;
        internal override float DamageMul => 1.06f;
    }

    /// <summary>Y10 格式化:C</summary>
    internal sealed class GsFormatCYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.FormatC;
        internal override float DamageMul => 1.06f;
    }

    /// <summary>Y11 梯度</summary>
    internal sealed class GsGradientYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Gradient;
        internal override float DamageMul => 1.06f;
    }

    /// <summary>Y12 奇克</summary>
    internal sealed class GsChikYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Chik;
        internal override float DamageMul => 1.06f;
    }

    /// <summary>Y13 冥火</summary>
    internal sealed class GsHelFireYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.HelFire;
        internal override float DamageMul => 1.05f;
    }

    /// <summary>Y14 阿马洛克</summary>
    internal sealed class GsAmarokYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Amarok;
        internal override float DamageMul => 1.05f;
    }

    /// <summary>Y15 代码2</summary>
    internal sealed class GsCode2Yoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Code2;
        internal override float DamageMul => 1.05f;
    }

    /// <summary>Y16 叶列茨</summary>
    internal sealed class GsYeletsYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Yelets;
        internal override float DamageMul => 1.05f;
    }

    /// <summary>Y17 红的投掷</summary>
    internal sealed class GsRedsThrowYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.RedsYoyo;
        internal override float DamageMul => 1.04f;
    }

    /// <summary>Y18 女武神悠悠球</summary>
    internal sealed class GsValkyrieYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.ValkyrieYoyo;
        internal override float DamageMul => 1.04f;
    }

    /// <summary>Y19 挪威海妖</summary>
    internal sealed class GsKrakenYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Kraken;
        internal override float DamageMul => 1.03f;
    }

    /// <summary>Y20 克苏鲁之眼</summary>
    internal sealed class GsEyeOfCthulhuYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.TheEyeOfCthulhu;
        internal override float DamageMul => 1.03f;
    }

    /// <summary>Y21 泰拉悠悠球</summary>
    internal sealed class GsTerrarianYoyo : GsYoyoScheme
    {
        public override int TargetItemID => ItemID.Terrarian;
        internal override float DamageMul => 1.0f;
    }
}
