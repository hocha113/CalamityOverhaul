// ============================================================================
// TBUGTerminalPanel.fx  TBUG 终端玻璃面板底
// 材质："深空黑玻璃后面亮着一层冷蓝数据"——不是发光招牌，也不是纯色填充
// 三档纵深：底黑 → 中层蓝网格 → 上层扫描与噪声；亮度只在顶缘与边缘出现
// 预乘输出 + AlphaBlend；切角在 shader 内切，和 C# 的 Chamfer 常量对齐
// 直线算术无动态分支
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;  //面板像素尺寸
float uChamfer;      //切角边长 px
float uMode;         //0 主窗 1 悬停浮层（网格/噪声更弱，底更实）

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 px = uv * uResolution;
    float2 toEdge = min(px, uResolution - px);

    //切角遮罩：四角按 |x|+|y| 斜切，和 C# 的切角描边同一条斜边
    float corner = toEdge.x + toEdge.y;
    float inside = step(uChamfer, corner) * step(0.0, min(toEdge.x, toEdge.y));

    float overlay = saturate(uMode);
    float edgeDist = min(toEdge.x, toEdge.y);

    //———— 底：中心略亮的深蓝黑，向四周沉下去 ————
    float2 c = uv - 0.5;
    float vig = 1.0 - dot(c, c) * 0.85;
    float3 col = float3(0.012, 0.028, 0.055) * vig;

    //———— 中层：冷蓝网格，横线密竖线疏，读作数据面而不是方格纸 ————
    float gridA = step(0.955, frac(px.y / 4.0));
    float gridB = step(0.982, frac(px.x / 26.0));
    float gridFade = 1.0 - smoothstep(0.0, uResolution.y * 0.85, px.y);
    col += float3(0.020, 0.062, 0.115) * gridA * (0.35 + 0.65 * gridFade) * (1.0 - overlay * 0.6);
    col += float3(0.016, 0.050, 0.098) * gridB * (1.0 - overlay * 0.7);

    //———— 缓慢横向数据流：几条亮度极低的水平带在飘 ————
    float band = frac(px.y / 46.0 - uTime * 0.045);
    float bandLine = smoothstep(0.0, 0.06, band) * (1.0 - smoothstep(0.06, 0.16, band));
    col += float3(0.018, 0.058, 0.110) * bandLine * (1.0 - overlay * 0.5);

    //———— 稀疏字符残影：极暗的蓝点阵，慢速换帧 ————
    float2 cell = floor(px / float2(7.0, 11.0));
    float clock = floor(uTime * 0.7);
    float on = step(0.955, hash11(cell.x * 1.371 + cell.y * 0.291 + clock * 0.417));
    float2 sub = floor(frac(px / float2(7.0, 11.0)) * float2(3.0, 4.0));
    float glyph = step(0.45, hash11(sub.x * 0.37 + sub.y * 1.91 + cell.x * 0.11 + cell.y * 0.17 + clock));
    col += float3(0.055, 0.150, 0.260) * on * glyph * vig * (1.0 - overlay * 0.75);

    //———— 上缘冷光：只有顶部一条内侧余晖，其余靠 C# 描边 ————
    float topGlow = 1.0 - smoothstep(0.0, 26.0, px.y);
    col += float3(0.035, 0.105, 0.200) * topGlow;

    //———— 边缘内侧一圈极窄提亮，给玻璃一点厚度 ————
    float rim = 1.0 - smoothstep(0.0, 3.5, edgeDist - uChamfer * 0.2);
    col += float3(0.030, 0.085, 0.160) * rim;

    //———— 扫描线：2px 周期压暗，保证文字仍然可读 ————
    float scan = 0.93 + 0.07 * step(0.5, frac(px.y * 0.5));
    col *= scan;

    //悬停浮层要压住底下的内容，底更实一点
    float a = lerp(0.955, 0.985, overlay);
    return float4(col, a) * inside * uAlpha * input.Color;
}

technique Technique1
{
    pass TBUGTerminalPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
