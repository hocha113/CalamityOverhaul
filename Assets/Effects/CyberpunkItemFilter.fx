//============================================================================
//CyberpunkItemFilter.fx 赛博朋克2077风格的物品图标附魔滤镜
//输入参数：
//  uTime       累计时间，用于动画
//  uTint       识别色，用作高光区域调色与边缘霓虹描边主色
//  uTexSize    贴图像素尺寸，用于将uv换算为像素步进
//  uIntensity  整体滤镜强度，0为关闭，1为完整效果
//============================================================================

sampler uImage0 : register(s0);

float uTime;
float3 uTint;
float2 uTexSize;
float uIntensity;

float hash21(float2 p) {
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 px = coords * uTexSize;
    float2 texel = 1.0 / max(uTexSize, float2(1.0, 1.0));

    //完全透明的像素直接返回，阻止透明区域被任何颜色污染
    float aCtr = tex2D(uImage0, coords).a;
    if (aCtr < 0.004) return float4(0, 0, 0, 0);

    //横向毛刺扫描线偏移：偏移幅度从0.30降至0.05，防止大偏移采到透明边界外的白色区域
    float band = floor(px.y * 0.5);
    float bandSeed = hash21(float2(band, floor(uTime * 6.0)));
    float glitchActive = step(0.95, bandSeed);
    float glitchOff = (bandSeed - 0.5) * 0.05 * glitchActive * uIntensity;
    float2 uv = coords + float2(glitchOff, 0.0);

    //RGB色散偏移
    float ca = 1.4 * texel.x * uIntensity;
    float4 cR = tex2D(uImage0, uv + float2(ca, 0.0));
    float4 cG = tex2D(uImage0, uv);
    float4 cB = tex2D(uImage0, uv - float2(ca, 0.0));
    float a = cG.a;

    //色散通道按邻近像素alpha回退到中心采样，防止透明边界外的白色RGB值渗入
    float3 rSafe = lerp(cG.rgb, cR.rgb, saturate(cR.a * 8.0));
    float3 bSafe = lerp(cG.rgb, cB.rgb, saturate(cB.a * 8.0));

    //提取亮度作为重映射输入
    float lum = dot(cG.rgb, float3(0.299, 0.587, 0.114));

    //双调色映射：阴影压向深蓝黑，高光导向识别色
    float3 shadow = float3(0.02, 0.04, 0.08);
    float3 base = lerp(shadow, uTint, smoothstep(0.05, 0.85, lum));

    //叠加色散两端的颜色作为辉光感
    float3 split = float3(rSafe.r, cG.g, bSafe.b);
    float3 col = lerp(base, base + split * 0.38, uIntensity * 0.62);

    //CRT扫描线
    float scan = 0.88 + 0.12 * sin(px.y * 3.1416);
    col *= lerp(1.0, scan, uIntensity);

    //采样四邻域alpha，计算当前像素距透明边界的接近程度
    float aL = tex2D(uImage0, coords - float2(texel.x, 0.0)).a;
    float aR = tex2D(uImage0, coords + float2(texel.x, 0.0)).a;
    float aU = tex2D(uImage0, coords - float2(0.0, texel.y)).a;
    float aD = tex2D(uImage0, coords + float2(0.0, texel.y)).a;
    //minNeighborA越小说明越靠近透明边界，rimProximity越大
    float minNeighborA = min(min(aL, aR), min(aU, aD));
    float rimProximity = 1.0 - minNeighborA;
    //仅对充分不透明的像素激活，避免抗锯齿半透明边缘被影响
    float rimStrength = rimProximity * smoothstep(0.6, 1.0, a) * uIntensity;

    //内侧边缘光：把边缘像素颜色向识别色偏移，用lerp而非叠加，杜绝外来颜色凭空出现
    //缓慢呼吸脉冲只作用于整体亮度，不产生横向扫描波纹
    float rimPulse = sin(uTime * 2.2) * 0.1 + 0.9;
    float3 rimTarget = saturate(uTint * rimPulse * 1.05);
    col = lerp(col, rimTarget, rimStrength * 0.45);

    //像素级闪烁噪点
    float flicker = hash21(float2(floor(px.x * 0.5), floor(uTime * 14.0))) - 0.5;
    col += uTint * flicker * 0.08 * uIntensity;

    //全图脉冲增亮
    float pulse = exp(-frac(uTime * 0.6) * 4.0) * 0.18 * uIntensity;
    col += uTint * pulse;

    //钳制防止颜色叠加超过1.0，避免乘以alpha后出现白色斑块
    col = saturate(col);

    return float4(col * a, a) * vertexColor;
}

technique Technique1
{
    pass CyberpunkItemFilterPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
