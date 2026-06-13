// ============================================================================
// DestroyerSlash.fx 毁灭者之刃挥砍轨迹
// UV.x 0尾→1刃口 UV.y 0外缘→1内缘；AlphaBlend 预乘 alpha
// ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;        //滚动时间
float fadeAlpha;    //整体透明度 0~1（挥砍结束后的余像衰减）
float heatBoost;    //重击充能 0~1：提升白热程度与余烬密度
float exMode;       //0=普通版 1=EX版（更炽白的等离子色板）
float segCount;     //沿轨迹分布的机械段甲数量

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

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;          //0 旧 → 1 新
    float radial = 1.0 - uv.y;   //0 内缘 → 1 外缘

    //=========================================================
    //可见度骨架：尾部淡出 + 内缘淡出
    //=========================================================
    float tailFade = smoothstep(0.0, 0.5, along);
    tailFade = pow(tailFade, 1.5);
    float innerFade = smoothstep(0.0, 0.20, radial);
    if (tailFade * innerFade * fadeAlpha < 0.002)
        return float4(0, 0, 0, 0);

    //滚动噪声：低频热浪 + 高频余烬
    float n  = tex2D(noiseSamp, float2(along * 1.8 - uTime * 1.1, radial * 0.9 + uTime * 0.07)).r;
    float n2 = tex2D(noiseSamp, float2(along * 3.7 - uTime * 2.4, radial * 2.3 + 0.37)).r;

    //=========================================================
    //外缘激光：刀尖扫过的弧线（红激光 + 白热芯）
    //=========================================================
    float edgeDist = abs(radial - 0.92);
    float edgeGlow = exp(-edgeDist * edgeDist * 320.0);
    float edgeCore = exp(-edgeDist * edgeDist * 2400.0);
    //沿弧线的高频能量颤动
    edgeGlow *= 0.74 + 0.26 * sin(along * 34.0 - uTime * 26.0);

    //=========================================================
    //机械段甲：毁灭者体节，段间留缝、中央亮脊
    //=========================================================
    float plateBand = smoothstep(0.14, 0.30, radial) * smoothstep(0.86, 0.70, radial);
    float segPos = along * segCount;
    float segIdx = floor(segPos);
    float segFrac = segPos - segIdx;
    float gapDist = min(segFrac, 1.0 - segFrac);
    //段与段之间的切割缝隙
    float plate = smoothstep(0.030, 0.090, gapDist);
    //每节装甲的金属高光脊线
    float ridge = exp(-pow((radial - 0.50) * 4.2, 2.0));
    float plateShade = lerp(0.30, 1.0, ridge) * plate * plateBand;
    //装甲表面被噪声轻微腐蚀，避免过于干净的几何感
    plateShade *= 0.72 + 0.28 * n;

    //=========================================================
    //探测红灯：每节体甲中央一颗，随机明灭（毁灭者的探针眼）
    //=========================================================
    float2 lampVec = float2((segFrac - 0.5) * 2.6, (radial - 0.5) * 5.0);
    float lampMask = exp(-dot(lampVec, lampVec) * 14.0);
    float lampOn = 0.30 + 0.70 * step(0.40, hash21(float2(segIdx * 1.37 + 5.0, floor(uTime * 7.0))));
    float lamp = lampMask * lampOn * plate * plateBand;

    //=========================================================
    //刀锋头部闪光：当前刀刃位置的高亮压迫感
    //=========================================================
    float head = smoothstep(0.78, 0.985, along);
    head *= head;
    float headGlow = head * (0.40 + 0.60 * radial);

    //=========================================================
    //热浪与余烬碎屑
    //=========================================================
    float heat = n * (0.45 + heatBoost * 0.55);
    float ember = step(0.93 - heatBoost * 0.05, n2);

    //=========================================================
    //调色板：暗红装甲 / 血红激光 / 灼橙 / 白热芯
    //=========================================================
    float3 cDeep  = lerp(float3(0.30, 0.015, 0.020), float3(0.34, 0.030, 0.060), exMode);
    float3 cBlood = lerp(float3(0.85, 0.090, 0.050), float3(0.95, 0.120, 0.100), exMode);
    float3 cHot   = lerp(float3(1.00, 0.420, 0.120), float3(1.00, 0.520, 0.240), exMode);
    float3 cCore  = lerp(float3(1.00, 0.800, 0.550), float3(1.00, 0.930, 0.820), exMode);
    float3 cLamp  = float3(1.00, 0.16, 0.10);

    float3 color = float3(0, 0, 0);
    color += cDeep  * plateShade * 1.10;
    color += cBlood * plateShade * ridge * 0.55;
    color += cLamp  * lamp * 1.70;
    color += cBlood * edgeGlow * 1.15;
    color += cCore  * edgeCore * (1.0 + heatBoost * 0.9);
    color += cHot   * headGlow * (0.9 + heatBoost * 0.6);
    color += cDeep  * heat * 0.75;
    color += cHot   * ember * 1.25;

    float alpha = saturate(
          plateShade * 0.55
        + lamp * 0.80
        + edgeGlow * 0.85
        + edgeCore * 0.90
        + headGlow * 0.70
        + heat * 0.28
        + ember * 0.75
    );

    alpha *= tailFade * innerFade * fadeAlpha;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass DestroyerSlashPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
