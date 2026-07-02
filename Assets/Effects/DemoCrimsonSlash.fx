// ============================================================================
//DemoCrimsonSlash.fx 绯红裂空斩月牙主体
//四边形 UV0..1 内做极坐标月牙带：phi=atan2 仅经 u=phi/span+0.5 做单调比较与 clamp 采样，
//无 sin/cos/噪声直接消费原始极角，分支切线(±π)落在弧外 → 极角审计合规
//笔刷贴图提供撕裂质感，噪声贴图负责外缘参差与生命期侵蚀
//预乘 alpha 输出，配 BlendState.AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;         //秒
float uSweep;        //0..1 扫掠揭开进度
float uErode;        //0..1 生命期侵蚀
float uColorShift;   //0..1 亮红→深酒红整体压暗
float uOpacity;      //整体不透明度
float uFlip;         //+1/-1 挥动镜像
float uSeed;         //实例随机相位
float uArcSpan;      //弧总跨度(弧度，须<2π)
float uThick;        //带厚度(p 空间尺度，0..~0.4)
float uFrontGlow;    //扫掠前缘白热强度

float3 uColHot;      //白热核心
float3 uColBright;   //亮绯红
float3 uColDeep;     //深红
float3 uColDark;     //暗酒红描边

texture uBrushTex;
sampler brushSamp = sampler_state
{
    texture = <uBrushTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = clamp;
};

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

#define PI 3.14159265

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

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 p = (input.TexCoords - 0.5) * 2.0;
    p.y *= uFlip;

    float r = length(p);
    float phi = atan2(p.y, p.x);
    //弧向坐标：0=起笔角 → 1=收笔角(冲击端)。弧外像素被厚度包络归零，cut 处两侧同为透明
    float uRaw = phi / uArcSpan + 0.5;
    float uc = saturate(uRaw);

    //厚度包络：峰值偏向收笔端(~0.7)，两端收成尖
    float env = sin(pow(uc, 1.85) * PI);
    float w = uThick * pow(max(env, 0.0), 0.72);
    if (w < 0.004)
        return float4(0, 0, 0, 0);

    //外缘参差：双八度噪声推挤外缘半径，侵蚀期加剧
    float jag1 = tex2D(noiseSamp, float2(uc * 2.6 + uSeed, 0.19 + uSeed * 0.7)).r - 0.5;
    float jag2 = tex2D(noiseSamp, float2(uc * 6.5 - uTime * 0.10 + uSeed, 0.71)).r - 0.5;
    float outerR = 0.90 + (jag1 * 0.055 + jag2 * 0.030) * (1.0 + uErode * 1.6);

    float innerR = outerR - w;
    float v = (r - innerR) / w;   //0=内缘(白热利落) → 1=外缘(暗色撕裂)
    if (v < -0.15 || v > 1.25)
        return float4(0, 0, 0, 0);
    float vc = saturate(v);

    //笔刷主体：两个八度沿弧向反向流动
    float flow = uTime * 0.45 + uSeed;
    float4 b1 = tex2D(brushSamp, float2(uc * 1.30 - flow * 0.22, vc));
    float4 b2 = tex2D(brushSamp, float2(uc * 3.10 + flow * 0.40 + 0.37, vc * 0.62 + 0.20));
    float streak = b1.r * b1.a * 0.85 + b2.r * b2.a * 0.55;

    //扫掠揭开：uRaw < 前缘可见，前缘尾随一条白热光带
    float edge = uSweep * 1.10 - 0.04;
    float reveal = smoothstep(edge + 0.012, edge - 0.055, uRaw);
    float front = exp(-pow((uRaw - edge) / 0.05, 2.0)) * uFrontGlow;

    //生命期侵蚀：外缘先碎(阈值随 v 偏置)，噪声阈值切割 + 燃边
    float eN = tex2D(noiseSamp, float2(uc * 2.3 + uSeed * 3.1, vc * 0.85 + uSeed)).r * 0.65
             + tex2D(noiseSamp, float2(uc * 5.2 - uTime * 0.14, vc * 1.60 + 0.40)).r * 0.35;
    float eTh = uErode * 1.18 - (1.0 - vc) * 0.22;
    float survive = smoothstep(eTh - 0.02, eTh + 0.12, eN);
    float burn = smoothstep(eTh - 0.16, eTh - 0.02, eN) * (1.0 - survive);

    //径向 alpha：内缘利落，外缘被笔刷撕碎；角向两端羽化
    float aIn = smoothstep(-0.045, 0.09, v);
    float aOut = smoothstep(1.06, 0.66, v + (0.5 - streak) * 0.40);
    float tipFeather = smoothstep(0.0, 0.05, uc) * smoothstep(1.0, 0.952, uc);

    float alpha = aIn * aOut * tipFeather * reveal * survive;
    alpha *= saturate(0.42 + streak * 0.95);
    alpha = saturate(alpha) * uOpacity;

    //三段色带：白热内缘 → 亮绯红 → 深红 → 暗酒红外缘，随生命期整体压暗
    float3 col = lerp(uColHot, uColBright, smoothstep(0.03, 0.25, vc));
    col = lerp(col, uColDeep, smoothstep(0.32, 0.70, vc));
    col = lerp(col, uColDark, smoothstep(0.68, 1.02, vc));
    col = lerp(col, lerp(uColDeep * 0.55, uColDark, vc), uColorShift * 0.85);

    //内缘白热刃线 + 笔刷高光 + 侵蚀燃边 + 扫掠前缘
    float rim = pow(saturate(1.0 - vc * 3.4), 2.2);
    col += uColHot * rim * (1.0 - uColorShift * 0.75) * 0.9;
    col += uColBright * streak * (1.0 - vc * 0.6) * 0.55;
    col += float3(1.25, 0.42, 0.18) * burn * 2.3;
    col += uColHot * front * 2.6;

    //前缘/燃边在 alpha 之外再给增益 → 半加法辉光
    float glowA = saturate(alpha + (front * 0.55 + burn * 0.25) * uOpacity * reveal * tipFeather);
    return float4(col * alpha + uColHot * front * 0.35 * uOpacity * tipFeather, glowA);
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
