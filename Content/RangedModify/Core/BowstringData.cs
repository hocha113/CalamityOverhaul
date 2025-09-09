using InnoVault.Trails;

public struct BowstringDataStruct
{
    /// <summary>
    /// 是否裁切弓弦，这个会改变弓的绘制方式
    /// </summary>
    public bool CanDeduct;
    /// <summary>
    /// 是否额外绘制动画弓弦
    /// </summary>
    public bool CanDraw;
    /// <summary>
    /// 如果<see cref="CanDeduct"/>为<see langword="true"/>就需要设置这个矩形，用于决定裁剪的部位
    /// </summary>
    public Rectangle DeductRectangle = default;
    /// <summary>
    /// 设置这个会让整个弓弦位置移动
    /// </summary>
    public Vector2 CoreOffset = default;
    /// <summary>
    /// 上侧的位置矫正
    /// </summary>
    public Vector2 TopBowOffset = default;
    /// <summary>
    /// 下侧的位置矫正
    /// </summary>
    public Vector2 BottomBowOffset = default;
    /// <summary>
    /// 点集，为<see cref="DoEffect"/>所用
    /// </summary>
    public Vector2[] Points = new Vector2[3];
    /// <summary>
    /// 效果实例
    /// </summary>
    public PathEffect DoEffect = null;
    /// <summary>
    /// 是否自动更具<see cref="DeductRectangle"/>的宽度来设置<see cref="thicknessEvaluator"/>，默认为<see langword="true"/>
    /// </summary>
    public bool AutomaticWidthSetting = true;
    /// <summary>
    /// 弓弦宽度
    /// </summary>
    public TrailThicknessCalculator thicknessEvaluator = (_) => 1;
    /// <summary>
    /// 弓弦颜色
    /// </summary>
    public TrailColorEvaluator colorEvaluator = null;
    public BowstringDataStruct() { }
}
