// ============================================================================
//HeadlessShadeBody.fx 无头鬼影本体
//材质是"影"：投影而非实体，所以硬剪影 + 极窄半影，永远没有辉光核心；
//  预乘 alpha 输出配 BlendState.AlphaBlend，是吸光暗体而不是发光叠层。
//配色无彩黑，紫只在毛口留一点；骨白冷青只作结构细线（uRimFlash 的短促撕口）。
//TechBody 画贴 Shutter 剪影的分段躯干条带（也用于地面投影拷贝），
//  自带 UV 边缘护栏（防条带边界直线切口）与断颈口骨白细线（无头是身份）；
//TechLimb 画不吃剪影的程序化锥形条带（双臂、影屑）。
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uOpacity;
float uDissolve;
float uPhase;
float uSeed;
float uRimFlash;    //0..1 骨白撕口短闪，本体与斩痕共用同一套材质语言
float uTipSolid;    //1=末端实/根部散（臂） 0=根部实/末端散（颈口漏影）
float uFray;        //肢体毛口强度

texture uShutterTex;
sampler shutterSamp = sampler_state
{
    texture = <uShutterTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = clamp;
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

//无彩影调；edgeDormant 是全套配色里唯一留紫的地方
static const float3 ShadowCore = float3(0.006, 0.006, 0.009);
static const float3 ShadowGrain = float3(0.026, 0.026, 0.033);
static const float3 EdgeDormant = float3(0.055, 0.049, 0.074);
static const float3 EdgeStriking = float3(0.212, 0.232, 0.262);
static const float3 RimBone = float3(0.72, 0.80, 0.85);

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput input)
{
    PSInput output;
    output.Position = mul(input.Position, transformMatrix);
    output.Color = input.Color;
    output.TexCoords = input.TexCoords;
    return output;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    //UV 边缘护栏：贴图裙摆渐隐一直延伸到贴图底边，若不强制归零，
    //  低侵蚀状态（蓄力/冲刺）会在条带边界露出一条直线切口
    float edgeGuard = smoothstep(1.0, 0.86, uv.y) * smoothstep(0.0, 0.02, uv.y)
        * smoothstep(0.0, 0.04, uv.x) * smoothstep(1.0, 0.96, uv.x);
    float shape = tex2D(shutterSamp, uv).a * edgeGuard;

    float n0 = tex2D(noiseSamp, float2(uv.x * 2.10 + uSeed, uv.y * 1.55 - uTime * 0.10)).r;
    float n1 = tex2D(noiseSamp, float2(uv.x * 4.70 - uTime * 0.16 + uSeed * 2.73,
        uv.y * 3.40 + uTime * 0.07)).r;
    float noise = n0 * 0.67 + n1 * 0.33;

    float lowerFray = smoothstep(0.43, 0.98, uv.y);
    float sideFray = smoothstep(0.18, 0.48, abs(uv.x - 0.5));
    float erosion = uDissolve * (0.10 + lowerFray * 0.52 + sideFray * 0.16)
        * (0.30 + noise * 0.70);
    float field = shape - erosion;
    float body = smoothstep(0.035, 0.33, field);

    float inner = smoothstep(0.34, 0.72, field);
    float tornEdge = saturate(body - inner);
    float grain = saturate((noise - 0.30) * 1.45) * body;
    float phaseBeat = saturate(uPhase);

    float3 edgeColor = lerp(EdgeDormant, EdgeStriking, phaseBeat);
    float3 color = ShadowCore;
    color = lerp(color, ShadowGrain, grain * 0.30);
    color += edgeColor * tornEdge * (0.42 + phaseBeat * 0.58);
    //撕口短闪：白只落在剪影轮廓线上，同时本体压暗
    color *= 1.0 - uRimFlash * 0.30;
    color += RimBone * tornEdge * uRimFlash * 0.85;

    //断颈口骨白细线：只咬剪影上缘中段的撕口线，缓慢明灭、蓄力/扑出时燃亮。
    //  无头是身份，颈口给一线白，比堆几何更能说"这上面没有头"
    float neckBand = smoothstep(0.30, 0.12, uv.y) * exp(-pow((uv.x - 0.5) * 3.2, 2.0));
    float neckStir = 0.55 + 0.45 * sin(uTime * 2.1 + uSeed * 3.7);
    float neckRim = tornEdge * neckBand * (0.14 + 0.22 * neckStir + 0.50 * phaseBeat);
    color += RimBone * neckRim;

    float opacity = saturate(uOpacity * input.Color.a);
    float alpha = saturate(body * opacity * (0.74 + grain * 0.18));
    float glowBoost = (tornEdge * uRimFlash * 0.16 + neckRim * 0.10) * opacity;
    return float4(color * alpha + RimBone * glowBoost * 0.7, saturate(alpha + glowBoost));
}

//锥形条带：u 沿肢体 0=根 1=末，v 横向；顶点色 a 携带逐段衰减
float4 LimbShaderFunction(PSInput input) : COLOR0
{
    float u = input.TexCoords.x;
    float side = input.TexCoords.y * 2.0 - 1.0;

    float n0 = tex2D(noiseSamp, float2(u * 3.30 + uSeed, side * 0.62 - uTime * 0.09)).r;
    float n1 = tex2D(noiseSamp, float2(u * 6.90 - uSeed * 1.9, side * 1.45 + uTime * 0.05)).r;
    float noise = n0 * 0.64 + n1 * 0.36;

    //毛口：一端撕散一端收实，由 uTipSolid 决定朝向
    float frayRamp = lerp(u, 1.0 - u, uTipSolid);
    float edge = 0.84 - uFray * (0.16 + 0.40 * noise) * frayRamp;
    float bodyMask = 1.0 - smoothstep(edge - 0.13, edge, abs(side));

    //两端收口，不留平切的硬头：臂的根塞进剪影底下、末端收实；颈口反过来根实末散
    float rootFade = lerp(1.0, smoothstep(0.0, 0.18, u), uTipSolid);
    float tipFade = lerp(1.0 - smoothstep(0.55, 1.0, u), 1.0 - smoothstep(0.92, 1.0, u), uTipSolid);

    float mask = bodyMask * rootFade * tipFade;
    float tornEdge = saturate(mask - smoothstep(0.0, 0.55, mask * mask));
    float phaseBeat = saturate(uPhase);

    //顶点色 R 携带逐条骨白量，影屑里只有少数几片带新鲜撕口
    float rimAmt = max(uRimFlash, input.Color.r);
    float3 edgeColor = lerp(EdgeDormant, EdgeStriking, phaseBeat);
    float3 color = lerp(ShadowCore, ShadowGrain, saturate((noise - 0.34) * 1.5) * 0.45);
    color += edgeColor * tornEdge * (0.50 + phaseBeat * 0.50);
    color *= 1.0 - rimAmt * 0.30;
    color += RimBone * tornEdge * rimAmt * 0.85;

    float opacity = saturate(uOpacity * input.Color.a);
    float alpha = saturate(mask * opacity * (0.72 + noise * 0.22));
    float glowBoost = tornEdge * rimAmt * 0.14 * opacity;
    return float4(color * alpha + RimBone * glowBoost * 0.7, saturate(alpha + glowBoost));
}

technique TechBody
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}

technique TechLimb
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 LimbShaderFunction();
    }
}
