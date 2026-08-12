// ============================================================================
//KikasaBloodJet.fx 毁灭者鬼奴的血液喷柱（静态四边形条带，uv.x 1=口器根→0=末端）
//浓血三律：不对称截面（重力先撕下缘，锯齿蚀边随远端加剧）、
//高光只做偏离中线的各向异性窄湿反光条（圆形高光=塑料）、
//远端 Plateau-Rayleigh 颈缩断裂成滴串（阈值随 along 收紧，绝非平滑收口）。
//禁「尾暗→白热」能量拖尾语法——血是暗的：色程 暗血缘→深血→饱和血红核，无白热。
//uDrain 泄压收束自根部向上啃食而非整体淡出；根部湍流领口藏进头雕。
//预乘 alpha 输出，配 BlendState.AlphaBlend：暗缘真正压暗背景，读作有体积的液柱。
//全笛卡尔条带坐标，无 atan2/theta，无极角接缝风险。ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //流动相位秒
float uSeed;        //实例相位
float uFade;        //展开/整体包络 0..1
float uDrain;       //泄压 0..1：自根部（along=1）向末端啃食
float uGravSide;    //把 uv 横坐标映到世界重力向：downSide = y * uGravSide
float3 uColDark;    //暗血缘
float3 uColDeep;    //深血
float3 uColMain;    //饱和血红核
float3 uColBright;  //血沫湿反光，仅窄条与领口

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

float noiseTex(float2 uv)
{
    return tex2D(noiseSamp, uv).r;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float along = input.TexCoords.x;      //1=根 0=末端
    float y = input.TexCoords.y - 0.5;
    float loosen = 1.0 - along;           //压力衰减：越远越松

    //液柱摆动：双频噪声推挤横向，远端振幅增大（失稳前兆）
    float wob1 = noiseTex(float2(along * 1.8 + uTime * 1.3 + uSeed, uSeed * 0.7)) - 0.5;
    float wob2 = noiseTex(float2(along * 4.6 - uTime * 2.1 + uSeed * 3.0, 0.37)) - 0.5;
    y += (wob1 * 0.40 + wob2 * 0.22) * loosen * 0.5;

    //重力坠沉：整柱下缘随远端下垂
    float sag = noiseTex(float2(along * 3.2 + uTime * 0.9 + uSeed * 5.0, 0.61));
    y -= uGravSide * sag * 0.10 * loosen;

    //不对称截面：下缘被撕出锯齿蚀边，上缘保持水膜张力
    float downSide = y * uGravSide;
    float tear = noiseTex(float2(along * 6.0 - uTime * 2.6 + uSeed * 7.0, 0.23)) * loosen;
    float halfDn = 0.5 - tear * 0.22;
    float across = downSide > 0.0
        ? saturate(downSide / max(halfDn, 0.08))
        : saturate(-downSide / 0.5);
    float body = 1.0 - across;

    //轴向流动：冲向末端的粘稠条纹，比水快、比光慢
    float flow = noiseTex(float2(along * 2.2 + uTime * 3.4 + uSeed * 9.0, y * 1.6 + 0.5));

    //远端珠化断裂：阈值随 along 收紧，末端 1/3 被撕成滴串
    float beadGate = smoothstep(0.28 - along * 0.50, 0.62 - along * 0.42,
        body * (0.50 + flow * 0.70));
    if (beadGate < 0.004)
        return float4(0, 0, 0, 0);

    //泄压啃食：自根部向上吃，被吃缘泛血沫
    float drainEdge = 1.0 - along;
    float drainMask = smoothstep(uDrain * 1.06, uDrain * 1.06 + 0.10, drainEdge);
    float drainRim = exp(-abs(drainEdge - uDrain * 1.06 - 0.05) * 22.0) * saturate(uDrain * 8.0);

    //色程：暗血缘 → 深血 → 饱和血红核（血是暗的，核不发白）
    float3 col = lerp(uColDark, uColDeep, saturate(body * 1.6));
    col = lerp(col, uColMain, saturate(pow(body, 2.2) * (0.50 + 0.50 * flow)));

    //各向异性窄湿反光条：偏向上缘（远离撕裂侧），随摆动起伏
    float spec = saturate(1.0 - abs(y + uGravSide * 0.16) * 8.0);
    col += uColBright * pow(spec, 3.0) * 0.22 * (0.50 + 0.50 * along);

    //根部湍流领口：出口涡流的血沫，藏接缝进头雕
    float collar = smoothstep(0.86, 0.98, along)
        * noiseTex(float2(y * 3.0 + uTime * 2.8, uSeed + along * 2.0));
    col += uColBright * collar * 0.30;

    //蚀缘血沫
    col += uColBright * drainRim * 0.55;

    float alpha = beadGate * drainMask * (0.35 + 0.65 * body) * uFade;
    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
