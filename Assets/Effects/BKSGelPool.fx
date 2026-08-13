// ============================================================================
//BKSGelPool.fx 残酷史莱姆王 地面凝胶池(侧视)
//入场汇聚/迫击落点滞留/死亡终融/撤离渗地 共用
//vs+ps 世界坐标四边形；预乘 alpha 输出 + AlphaBlend
//侧视池体：水平面线+波纹、两端 meniscus 圆肩、体内气泡上浮
//极角审计：全程笛卡尔噪声，无 atan2
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSpread;      //0~1 自中心横向铺开
float uDrain;       //0~1 自表面向下排空(1=空)
float uAlpha;       //整体透明度
float uBoil;        //沸腾强度 0~1(死亡演出/汇聚期冒泡加剧)
float uSeed;
float3 uColorDeep;
float3 uColorMid;
float3 uColorFoam;

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    AddressU = wrap;
    AddressV = wrap;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
};

struct VSInput
{
    float3 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 UV       : TEXCOORD0;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float2 UV       : TEXCOORD0;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = mul(float4(input.Position, 1.0), transformMatrix);
    output.Color = input.Color;
    output.UV = input.UV;
    return output;
}

float4 PSMain(PSInput input) : COLOR0
{
    float2 uv = input.UV;          //x 0左→1右，y 0顶→1贴地
    float envelope = input.Color.a;
    float2 p = float2(uv.x - 0.5, uv.y);

    //---------------- 池形轮廓 ----------------
    //横向铺开：半宽随 uSpread 生长
    float halfW = 0.5 * saturate(uSpread);
    //两端圆肩：接近端点时池面下压(圆润收边，不是矩形切断)
    float xNorm = saturate(abs(p.x) / max(halfW, 0.001));
    float shoulder = xNorm * xNorm * 0.55;

    //表面线：基准高度 + 行波 + 噪声涟漪，排空时整体下沉
    float wave = sin(uv.x * 19.0 + uTime * 2.6 + uSeed * 6.2831) * 0.018
               + sin(uv.x * 7.0 - uTime * 1.7) * 0.012;
    float rippleNoise = (tex2D(noiseSamp, float2(uv.x * 3.1 + uTime * 0.4 + uSeed, 0.5)).r - 0.5) * 0.05;
    float surface = 0.30 + shoulder + (wave + rippleNoise) * (1.0 + uBoil * 1.6) + uDrain * 0.75;

    //池体存在域：表面以下、端点以内
    float inX = step(abs(p.x), halfW);
    float belowSurface = saturate((uv.y - surface) / 0.025);
    float poolMask = inX * belowSurface;

    //---------------- 体色 ----------------
    float depth = saturate((uv.y - surface) / max(1.0 - surface, 0.001));
    float groundBand = saturate((uv.y - 0.88) / 0.12);
    float3 gel = lerp(uColorMid, uColorDeep, depth * 0.7 + groundBand * 0.3);

    //内部缓流
    float inner = tex2D(noiseSamp, float2(uv.x * 3.6 + uTime * 0.24 + uSeed * 4.0, uv.y * 2.2)).g;
    gel += uColorMid * (inner - 0.5) * 0.4;

    //气泡上浮：沸腾时更密更快
    float bubbleSpeed = 0.5 + uBoil * 1.4;
    float bubble = tex2D(noiseSamp, float2(uv.x * 12.0 + uSeed * 9.0, uv.y * 6.0 - uTime * bubbleSpeed)).b;
    float bubbleCut = step(0.84 - uBoil * 0.10, bubble);
    gel += uColorFoam * bubbleCut * (0.35 + uBoil * 0.4);

    //---------------- 表面反光线 ----------------
    float hlDist = uv.y - (surface + 0.03);
    float sheen = exp2(-hlDist * hlDist * 1400.0);
    sheen *= 0.5 + 0.5 * tex2D(noiseSamp, float2(uv.x * 5.0 - uTime * 0.8, 0.2)).r;
    gel += uColorFoam * sheen * 0.8 * inX;

    //表面泡沫线：紧贴表面的一窄条淡色
    float foamLine = exp2(-hlDist * hlDist * 5000.0) * 0.5;
    gel = lerp(gel, uColorFoam, foamLine * inX);

    //---------------- 合成 ----------------
    float alpha = (0.5 + depth * 0.34) * poolMask;
    alpha += sheen * 0.22 * inX * belowSurface;
    alpha = saturate(alpha) * uAlpha * envelope;

    //画布边缘保险
    float guard = saturate(uv.y * 30.0) * saturate((1.0 - uv.y) * 30.0 + 0.4)
                * saturate(uv.x * 20.0) * saturate((1.0 - uv.x) * 20.0);
    alpha *= saturate(guard);

    return float4(gel * alpha, alpha);
}

technique Technique1
{
    pass GelPoolPass
    {
        VertexShader = compile vs_3_0 VSMain();
        PixelShader = compile ps_3_0 PSMain();
    }
}
