// ============================================================================
//NeutronPulseBeam.fx 脉冲星磁极灯塔束
//UV.x 0极冠(根)→1远端；UV.y 横截面；TriangleStrip + Additive
//关键是"空心锥"而非实心激光条：等离子体光学薄，锥壁比锥心亮
//直线算术无动态分支
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;
float uFade;        //整体强度，含展开/收束
float uPhase;       //灯塔脉冲包络 0~1，扫过时打峰
float uGlitch;      //星震超频 0~1，加宽加亮
float uSpread;      //锥张开度

float3 uColHot;
float3 uColBeam;
float3 uColMain;

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

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float along = input.TexCoords.x;
    float cross_ = (input.TexCoords.y - 0.5) * 2.0;

    //远端噪声撕口，收成参差锥尖而非平切
    float turb = tex2D(noiseSamp, float2(along * 2.9 - uTime * 1.9, cross_ * 0.55 + uSeed)).r - 0.5;
    float tip = smoothstep(1.0, 0.62, along + turb * 0.22);

    //锥张开：根部收束在极冠上，越远越张
    float coneHalf = lerp(0.10, uSpread, pow(saturate(along), 0.68));
    float d = abs(cross_) / max(coneHalf, 0.02);
    float inside = smoothstep(1.06, 0.92, d);

    //边缘增亮：光学薄的锥壁视线穿得更长，故壁亮心暗
    float wall = smoothstep(0.30, 0.92, d) * inside;
    float core = exp(-d * d * 2.6) * 0.42;
    //极细热轴，只占很小视觉质量
    float axis = exp(-d * d * 90.0) * (0.5 + uGlitch * 0.7);

    //激波结点：沿束向外行进的亮珠，离散而非平滑渐变
    float knotPhase = frac(along * 3.2 - uTime * 1.25 - uSeed);
    float knot = pow(saturate(1.0 - abs(knotPhase - 0.5) * 4.4), 3.0);
    knot *= inside * smoothstep(0.06, 0.35, along);

    //沿束衰减，近似逆平方
    float falloff = 1.0 / (1.0 + along * 2.1);
    float root = smoothstep(0.0, 0.09, along);

    float mask = tip * root * falloff * uFade * (0.35 + uPhase * 0.65);

    float3 col = float3(0, 0, 0);
    col += uColBeam * wall * 1.15;
    col += uColMain * core;
    col += uColHot * axis;
    col += lerp(uColHot, uColBeam, 0.35) * knot * 0.9;

    float alpha = saturate(wall * 0.85 + core * 0.6 + axis + knot * 0.7) * mask;
    col *= mask;

    return float4(col * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass NeutronPulseBeamPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
