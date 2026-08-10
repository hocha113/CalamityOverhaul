// ============================================================================
// TBUGTerminalPanel.fx  TBUG 终端窗口底层
// 近纯黑绿相底 + 扫描线 + 残留字符缓冲微光 + 偶发横向撕裂行 + 硬边窗框
// 预乘输出 + AlphaBlend；直线算术无动态分支
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float2 uResolution;  //面板像素尺寸
float uEdgePad;      //内框边距 px

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

    //硬边窗框：边距外透明，终端窗口不需要羽化
    float2 edge = min(px, uResolution - px);
    float inFrame = step(uEdgePad, min(edge.x, edge.y));

    //底色：近纯黑带绿相，向边缘微暗
    float2 c = uv - 0.5;
    float vig = 1.0 - dot(c, c) * 0.55;
    float3 col = float3(0.006, 0.018, 0.010) * vig;

    //扫描线：3px 周期
    float scan = 0.90 + 0.10 * step(0.5, frac(px.y / 3.0));
    col *= scan;

    //残留字符缓冲：8x12 格，极稀极暗的绿点阵，缓慢换帧
    float2 cell = floor(px / float2(8.0, 12.0));
    float clock = floor(uTime * 0.8);
    float on = step(0.90, hash11(cell.x * 1.371 + cell.y * 0.291 + clock * 0.417));
    float2 sub = floor(frac(px / float2(8.0, 12.0)) * float2(3.0, 4.0));
    float pix = step(0.45, hash11(sub.x * 0.37 + sub.y * 1.91 + cell.x * 0.11 + cell.y * 0.17 + clock));
    col += float3(0.020, 0.075, 0.034) * on * pix * vig;

    //偶发撕裂行：整行轻微提亮 + 位置每 0.4s 重掷
    float rowIdx = floor(px.y / 2.0);
    float tearPick = floor(uResolution.y * 0.5 * hash11(floor(uTime * 2.5) * 1.31));
    float tear = step(abs(rowIdx - tearPick), 0.5);
    col += float3(0.03, 0.10, 0.05) * tear;

    //上缘 2px 内侧过渡亮线（窗框内的余晖）
    float topGlow = 1.0 - smoothstep(0.0, 14.0, px.y - uEdgePad);
    col += float3(0.010, 0.045, 0.020) * topGlow;

    float a = 0.955;
    return float4(col, a) * inFrame * uAlpha * input.Color;
}

technique Technique1
{
    pass TBUGTerminalPanelPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
