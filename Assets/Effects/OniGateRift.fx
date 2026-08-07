// ============================================================================
//OniGateRift.fx 鬼门开缝刀痕(条带几何,band-local UV,无任何极角输入)
//uv.x=uc 沿缝 0起笔..1收笔  uv.y=v 横越 0/1两唇缘 0.5缝心
//顶点色 R=归一化z(0.5=屏面),远近分层与纵深压暗由它驱动,无方向启发式
//
//材质:撕开的世界膜——
//  缝内=异界虚空(近黑暗体,低频冻结纹理,不沿路径流动)
//  缝心=门缝幽光(冷青,像从深处透出,闭合期熄灭)
//  膜缘=绷白细线(结构性白:撕开1~3帧毛刺增亮,常态淡红白)
//  世界侧=灼红燃边(承接鬼切系列绯红色板)
//撕开由几何整形一次出现,本shader不做揭开wipe;闭合=张口收窄(几何侧)+幽光熄灭
//宏观通道吃uSeed(出生冻结),细节通道吃uDetailSeed(S2破碎步进重掷)
//预乘alpha输出,配BlendState.AlphaBlend;直线算术+tex2D,无分支属性/无tex2Dlod
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uSeed;         //宏观噪声相位,出生冻结
float uDetailSeed;   //细节通道相位,S2步进重掷,S3冻结
float uBurr;         //0..1撕开毛刺白包络
float uGlowIn;       //0..1门缝幽光强度
float uGapeT;        //0..1张口保持度(1开0闭)
float uOpacity;      //整体不透明度
float uFarSel;       //远近分层:0=整体 +1=仅近半 -1=仅远半(玩家身后层)
float uFarDim;       //远半侧压暗地板(0=不分层)
float uU0;           //可见窗起(uc,端部捏合)
float uU1;           //可见窗止
float uEmber;        //0..1魂火密度(终结拍)
float uTelegraph;    //应力线强度(TelegraphTech)

float3 uColVoid;     //缝内近黑
float3 uColGlow;     //门缝幽光冷青
float3 uColRim;      //膜缘绷白
float3 uColBurn;     //世界侧灼红
float3 uColDeep;     //深红过渡

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

//可见窗端部羽化
float WindowFeather(float uc)
{
    return smoothstep(uU0, uU0 + 0.045, uc) * (1.0 - smoothstep(uU1 - 0.045, uU1, uc));
}

float4 PSRift(PSInput input) : COLOR0
{
    float uc = input.TexCoords.x;
    float v = input.TexCoords.y;
    float dCtr = abs(v - 0.5) * 2.0;          //0缝心 1唇缘
    float zN = input.Color.r * 2.0 - 1.0;     //-1..1,+朝观者

    //远近半侧分层,身后半侧交玩家背后层;连续纵深压暗(FarDim=0时不压)
    float farW = smoothstep(0.10, -0.10, zN);
    float passMul = 1.0;
    if (uFarSel > 0.5)
        passMul = 1.0 - farW;
    else if (uFarSel < -0.5)
        passMul = farW;
    float dimFloor = lerp(1.0, uFarDim, step(0.01, uFarDim));
    float depthDim = lerp(1.0, dimFloor, saturate(-zN));

    float wf = WindowFeather(uc);
    if (passMul * wf < 0.005)
        return float4(0, 0, 0, 0);

    //唇缘扰动:宏观撕痕形(seed冻结) + 细节毛口(步进通道,撕开期加剧)
    float jagM = tex2D(noiseSamp, float2(uc * 2.8 + uSeed * 5.13, 0.21 + uSeed * 0.37)).r - 0.5;
    float jagD = tex2D(noiseSamp, float2(uc * 8.7 + uDetailSeed * 9.71, 0.63)).r - 0.5;
    float lip = 1.0 + jagM * 0.14 + jagD * (0.05 + uBurr * 0.10);
    float dd = dCtr / max(lip, 0.3);

    //主体裁切 + 世界侧灼边余量
    float body = 1.0 - smoothstep(0.965, 1.03, dd);
    float burnBand = smoothstep(0.92, 1.005, dd) * (1.0 - smoothstep(1.03, 1.16, dd));
    if (body + burnBand < 0.006)
        return float4(0, 0, 0, 0);

    //缝内虚空:暗底+低频异界纹,出生冻结不流动
    float marble = tex2D(noiseSamp, float2(uc * 2.3 + uSeed * 3.71, dd * 1.15 + uSeed * 0.53)).r;
    float marble2 = tex2D(noiseSamp, float2(uc * 5.4 - uSeed * 7.19, dd * 2.6 + 0.31)).r;
    float depthField = marble * 0.62 + marble2 * 0.38;
    float3 col = lerp(uColVoid, uColDeep * 0.40, depthField * depthField * 0.55);

    //门缝幽光:缝心细条,冷,读作从深处透出的光
    float glow = exp(-dd * dd * 16.0);
    col += uColGlow * glow * (0.10 + 0.46 * uGlowIn);

    //魂火:缝内稀疏亮点,细节通道驱动(步进闪烁,不漂移)
    float emberN = tex2D(noiseSamp, float2(uc * 6.5 + uDetailSeed * 13.7, dd * 2.1 + uDetailSeed * 0.77)).r;
    float ember = smoothstep(0.80, 0.94, emberN) * (1.0 - smoothstep(0.0, 0.75, dd)) * uEmber;
    col += uColGlow * ember * 1.7;

    //膜缘绷白:唇缘内侧细线,撕开毛刺期增亮外扩,常态淡红白
    float rim = smoothstep(0.80, 0.955, dd) * (1.0 - smoothstep(0.985, 1.04, dd));
    float burrN = tex2D(noiseSamp, float2(uc * 13.0 + uDetailSeed * 15.3, 0.83)).r;
    float burr = smoothstep(0.46, 0.60, burrN) * uBurr;
    float3 rimCol = lerp(uColRim * 0.34 + uColBurn * 0.28, uColRim * 1.45, saturate(uBurr * 1.2));
    col += rimCol * rim * (0.85 + burr * 1.7);

    //世界侧灼红燃边
    col += uColBurn * burnBand * (0.55 + uBurr * 0.70);

    col *= depthDim;

    //alpha:虚空暗体近实(亮天空下立得住),灼边低alpha羽化;顶点alpha=逐切片衰减通道
    float aVoid = body * (0.80 + depthField * 0.12 + glow * 0.08);
    float alpha = saturate(aVoid + burnBand * 0.42 + rim * 0.28 * uBurr);
    alpha *= wf * passMul * uOpacity * input.Color.a;

    return float4(col * alpha, alpha);
}

float4 PSTelegraph(PSInput input) : COLOR0
{
    float uc = input.TexCoords.x;
    float v = input.TexCoords.y;
    float dCtr = abs(v - 0.5) * 2.0;

    float wf = WindowFeather(uc);
    //绷紧应力线:世界膜被压出的暗红细线,静止微颤(seed驱动,不流动)
    //兼作刀路条带材质,顶点alpha=逐切片年龄衰减
    float stress = tex2D(noiseSamp, float2(uc * 4.1 + uSeed * 6.3, 0.5)).r;
    float lineBand = 1.0 - smoothstep(0.0, 0.9, dCtr);
    float3 col = uColDeep * (0.55 + stress * 0.40) + uColBurn * 0.25 * lineBand;
    float alpha = lineBand * uTelegraph * (0.30 + stress * 0.25) * wf * uOpacity * input.Color.a;

    return float4(col * alpha, alpha);
}

technique RiftTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PSRift();
    }
}

technique TelegraphTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PSTelegraph();
    }
}
