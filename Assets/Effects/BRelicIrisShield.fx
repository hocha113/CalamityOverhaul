// ============================================================================
//BRelicIrisShield.fx 血雾之瞳·冲击血盾（冲刺前方的弓形血膜 / 重凝时的血茧环）
//弧形三角带：uv.x=t 沿弧 0..1（0.5=弧顶=冲刺正前），uv.y=0 内缘(贴身)→1 外缘(迎风)
//顶点色 R=本处厚度/uMaxThickPx  G=外法线朝上度(0..1)
//
//材质=被冲压的血：
//  1) 迎风外缘 2px 近黑描边 + 内侧 3px 压亮带（滞止点的血被压得最密最亮），
//     体为四阶平涂色阶，内缘毛口向身体撕开（血从身体被抽出来喂给盾）；
//  2) 流纹自弧顶向两翼回掠（uCrescent=1），环形时沿弧单向流（uCrescent=0）；
//  3) 成形自弧顶向两翼长出、厚度由 C# 几何随成形膨胀；
//  4) 碎裂自两翼向弧顶按噪声收颈成珠，C# 同拍甩血珠；
//  5) 噪声按弧长/径向 px 取样并量化到 uPixel 网格，颗粒对齐泰拉像素
//
//噪声全走 (弧长px, 径向px) 笛卡尔坐标，无极角；全程直线代码+纯 tex2D
//预乘 alpha 输出，配 BlendState.AlphaBlend；vs_3_0/ps_3_0，仅供顶点图元消费
// ============================================================================

float4x4 transformMatrix;
float uTime;         //秒
float uOpacity;      //整体不透明度 0..1
float uPixel;        //像素量化格(px)，0=不量化
float uMaxThickPx;   //顶点色 R 的折算基准 px
float uArcPx;        //弧总长 px
float uForm;         //0..1 成形进度（弧顶→两翼）
float uBreak;        //0..1 碎裂进度（两翼→弧顶收颈成珠）
float uFlash;        //0..1 起步过曝帧
float uFlowMul;      //回掠流速倍率
float uSeed;         //实例相位
float uCrescent;     //1=弓形（弧顶权重） 0=环形（均匀）

//噪声固定 s1：C# 侧在 Draw 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler2D noiseTex : register(s1);

//克眼血色板(与 EocMotion 同源)，无白热
static const float3 RimDark    = float3(0.11, 0.008, 0.02);
static const float3 DeepVein   = float3(0.165, 0.014, 0.03);
static const float3 VenousDark = float3(0.239, 0.024, 0.043);
static const float3 Arterial   = float3(0.557, 0.059, 0.102);
static const float3 Bright     = float3(0.831, 0.129, 0.180);
static const float3 Highlight  = float3(0.95, 0.36, 0.34);

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

//四停色阶：0 深静脉 → 1/3 静脉 → 2/3 动脉 → 1 鲜血
float3 Palette(float x)
{
    float3 c = lerp(DeepVein, VenousDark, saturate(x * 3.0));
    c = lerp(c, Arterial, saturate(x * 3.0 - 1.0));
    c = lerp(c, Bright, saturate(x * 3.0 - 2.0));
    return c;
}

//带抗锯齿的阶梯量化：台阶间 16% 过渡
float Quantize(float x, float steps)
{
    float t = x * steps;
    float q = floor(t);
    float f = frac(t);
    return (q + smoothstep(0.42, 0.58, f)) / steps;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float t = input.TexCoords.x;
    float apex = 1.0 - abs(t * 2.0 - 1.0);             //1 弧顶 → 0 翼尖
    float apexW = lerp(0.5, apex, uCrescent);
    float thickPx = input.Color.r * uMaxThickPx;
    float upSign = input.Color.g * 2.0 - 1.0;
    float radPx = input.TexCoords.y * thickPx;          //自内缘起 px
    float arcPx = t * uArcPx;

    //---- 局部像素量化 ----
    float cell = max(uPixel, 0.001);
    float useQ = step(0.5, uPixel);
    float2 lp = float2(arcPx, radPx);
    lp = lerp(lp, floor(lp / cell) * cell + cell * 0.5, useQ);

    //---- 回掠流纹：弓形自弧顶向两翼，环形沿弧单向 ----
    float dApex = abs(lp.x - uArcPx * 0.5);
    float flowCoord = lerp(lp.x, dApex, uCrescent) - uTime * 95.0 * uFlowMul;
    float streak = tex2D(noiseTex, float2(flowCoord / 64.0, lp.y / 9.0 + uSeed)).r;
    float nB = tex2D(noiseTex, float2(lp.x / 34.0 + uSeed * 3.0, lp.y / 34.0 - uTime * 0.25)).r;
    float nE = tex2D(noiseTex, float2(lp.x / 22.0 + 0.5, lp.y / 22.0 + uTime * 0.4)).r;
    float streakC = saturate((streak - 0.5) * 2.4 + 0.5);

    //---- 成形：弧顶先出、两翼后到；环形靠噪声错拍 ----
    float formEdge = (1.0 - apexW) + (nB - 0.5) * 0.30;
    float formGate = smoothstep(uForm * 1.15 + 0.03, uForm * 1.15 - 0.12, formEdge);

    //---- 碎裂：两翼先断、弧顶最后；噪声高处最后断 ----
    float bTh = uBreak * 1.5;
    float survive = smoothstep(bTh - 0.15, bTh + 0.15, nB * 0.8 + 0.1 + apexW * 0.25);

    //---- 有效厚度与双缘（外缘湿撕、内缘毛口） ----
    float thickEff = thickPx * survive * formGate;
    float outerTear = (nE - 0.5) * 3.0;
    float innerTear = (nB - 0.5) * 8.0 + (nE - 0.5) * 4.0;
    float dOuter = thickEff + outerTear - radPx;
    float dInner = radPx - innerTear;
    float edgePx = min(dOuter, dInner);
    float mask = smoothstep(0.0, 1.2, edgePx);
    float rimOuter = 1.0 - smoothstep(1.4, 2.8, dOuter);
    float rimInner = 1.0 - smoothstep(0.6, 1.8, dInner);
    //迎风压亮带：外描边内侧 4px，滞止点最密
    float comp = (1.0 - smoothstep(2.6, 6.4, dOuter)) * (1.0 - rimOuter);

    //---- 色阶：体偏静脉暗，流纹推到动脉，压亮带推到鲜血 ----
    float light = upSign * 0.5;
    float tone = 0.20 + streakC * 0.50 + comp * 0.70 + light * 0.16 + apexW * 0.12;
    float3 col = Palette(Quantize(saturate(tone), 3.0));
    //压亮带上的湿光短划
    col = lerp(col, Highlight, comp * smoothstep(0.55, 0.72, streakC) * 0.9);
    col = lerp(col, RimDark, max(rimOuter, rimInner * 0.85));
    //起步过曝一拍
    col = lerp(col, Highlight, saturate(uFlash) * 0.75);

    float alpha = mask * uOpacity * 0.94;
    float live = step(0.004, alpha);
    return float4(col * alpha, alpha) * live;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
