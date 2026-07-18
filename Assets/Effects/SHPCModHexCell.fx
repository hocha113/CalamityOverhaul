// ============================================================================
//SHPCModHexCell.fx 多格机匣：单个六边形能量单元格
//画布为方形 quad，UV 中心为格心；s0 白色画布 s1 噪声
//纯笛卡尔 SDF 构图，无 atan2/极坐标，无接缝风险
// ============================================================================

sampler baseSamp : register(s0);
sampler noiseSamp : register(s1);

float uTime;
float fadeAlpha;    //整体透明度 0~1（矩阵展开/收起）
float fill;         //0 空格 → 1 充能满（充能过程可平滑过渡）
float flash;        //本格充能瞬间白闪 0~1
float salvoFlash;   //齐射全格爆发 0~1
float hexRot;       //六边形自转弧度
float readyPulse;   //满格待发脉冲 0~1
float cooldown;     //齐射后冷却量 1→0，期间命中不充能，格内滚动扫描纹提示锁定
float3 mainColor;   //主色（矩阵荧绿）
float3 coreColor;   //核心亮色（近白绿）

//正六边形 SDF：仅用 abs/dot/clamp，笛卡尔连续
float hexSDF(float2 p, float r)
{
    const float3 k = float3(-0.866025404, 0.5, 0.577350269);
    p = abs(p);
    p -= 2.0 * min(dot(k.xy, p), 0.0) * k.xy;
    p -= float2(clamp(p.x, -k.z * r, k.z * r), r);
    return length(p) * sign(p.y);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //-1..1 局部坐标 + 自转（刚性旋转，无接缝）
    float2 p = coords * 2.0 - 1.0;
    float cs = cos(hexRot);
    float sn = sin(hexRot);
    p = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);

    float d = hexSDF(p, 0.66);

    //描边：SDF 零等值线两侧的窄带
    float edge = 1.0 - smoothstep(0.0, 0.09, abs(d));
    //内部遮罩
    float inside = 1.0 - smoothstep(-0.02, 0.02, d);

    //内部能量流：噪声沿局部 y 滚动（笛卡尔输入 + frac 仅喂 tex2D）
    float2 nuv = coords * 1.7 + float2(0.0, -uTime * 0.30);
    float energy = tex2D(noiseSamp, frac(nuv)).r;
    //SDF 等距轮廓波纹：d 是连续标量场，sin(d) 无缝
    float ripple = 0.5 + 0.5 * sin(d * 26.0 + uTime * 3.2);
    energy = energy * 0.6 + ripple * 0.4;

    //核心亮点（径向 length 连续）
    float core = 1.0 - smoothstep(0.0, 0.5, length(p));

    //满格待发脉冲：整格呼吸增亮
    float breathe = 1.0 + readyPulse * 0.5 * sin(uTime * 9.0);

    float3 col = float3(0.0, 0.0, 0.0);
    float lum = 0.0;

    //描边：空格暗描边打底，充能后增亮；冷却期整体压暗示意"锁定"
    float edgeGain = lerp(0.30, 1.15, fill) * breathe * (1.0 - cooldown * 0.45);
    col += mainColor * edge * edgeGain;
    lum += edge * edgeGain * 0.55;

    //充能内部：能量流 + 核心辉光
    float fillGlow = inside * fill;
    col += mainColor * fillGlow * energy * 0.8 * breathe;
    col += coreColor * core * fill * (0.5 + 0.2 * sin(uTime * 4.0)) * breathe;
    lum += fillGlow * (0.30 + energy * 0.35) + core * fill * 0.35;

    //空格内部微弱底色，保证"有个格子在这"的可读性
    col += mainColor * inside * (1.0 - fill) * 0.06;
    lum += inside * (1.0 - fill) * 0.05;

    //冷却锁定纹：低饱和横向扫描条纹沿屏幕竖直方向下滚
    //（用未旋转的 coords.y，格子自转时条纹保持稳定的"系统覆盖层"观感）
    float lockBand = smoothstep(0.35, 0.5, frac(coords.y * 3.5 - uTime * 0.55))
                   * smoothstep(0.85, 0.7, frac(coords.y * 3.5 - uTime * 0.55));
    float3 desatColor = lerp(mainColor, dot(mainColor, float3(0.33, 0.34, 0.33)).xxx, 0.7);
    float lockAmt = cooldown * inside * (1.0 - fill);
    col += desatColor * lockBand * lockAmt * 0.5;
    lum += lockBand * lockAmt * 0.35;

    //白闪：充能瞬间/齐射爆发，整格淹白（偏绿白）
    float burst = saturate(flash + salvoFlash);
    col = lerp(col, float3(1.5, 1.9, 1.6), burst * max(inside, edge));
    lum += burst * max(inside, edge);

    float alpha = saturate(lum) * fadeAlpha;
    return float4(col * alpha, alpha) * vertexColor;
}

technique Technique1
{
    pass SHPCModHexCellPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
