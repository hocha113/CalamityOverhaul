// ============================================================================
//MarbleSlash.fx 大理石白芯金边刀光
//近战 TriangleStrip 挥砍弧光；猎刀/巨棍共用签名刀光
//UV.x 1=最新挥砍缘 0=尾  UV.y 0=外缘(刀尖侧) 1=内缘(持握侧)
//读感=凿刻石光：沿弧向的地层凿纹条纹为主体，尾部大半崩解成石屑，
//领刃白芯只贴最新缘外侧，鎏金细线锁外缘；严禁读成均匀实心色带
//颜色内置（石白底/暖白/鎏金边/白芯），顶点色作整体调制
//vs_3_0 / ps_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;    //流动相位（GlobalTimeWrappedHourly）
float uFade;    //整体透明度 0~1
float uHeat;    //强击度 0~1，重击/终结时提升金边与白芯

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
    float2 uv = input.TexCoords;
    float age = uv.x; //1=最新 越大越亮

    //凿纹地层：沿弧向拉长的强对比条纹，大理石纹理主体
    float strata = tex2D(noiseSamp, float2(uv.x * 1.1 - uTime * 0.22, uv.y * 3.2)).r;
    float strata2 = tex2D(noiseSamp, float2(uv.x * 2.4 + uTime * 0.13, uv.y * 6.5 + 0.4)).r;
    float carve = strata * 0.62 + strata2 * 0.38;

    //外缘贴刀尖且被凿纹轻咬；内缘早收，不糊到持握者身上
    float edgeBite = (carve - 0.5) * 0.12;
    float outerMask = smoothstep(0.02 + edgeBite, 0.14 + edgeBite, uv.y);
    float innerMask = smoothstep(0.95, 0.30, uv.y);

    //年龄衰减 + 石屑崩解：崩解从尾端一路啃进带身大半，只留最新缘完整
    float ageMask = smoothstep(0.0, 0.45, age);
    ageMask *= ageMask;
    float grain = tex2D(noiseSamp, float2(uv.x * 6.0, uv.y * 7.0)).r;
    float dissolve = smoothstep(0.9 - age * 1.05, 1.05 - age * 1.05, grain);

    float body = outerMask * innerMask * ageMask * dissolve;

    //凿纹丝线高光：主要纹理读感
    float filament = smoothstep(0.55, 0.85, carve) * body;

    //鎏金外缘线：凿纹啃出细碎缺口
    float rim = smoothstep(0.17, 0.02, uv.y) * ageMask * dissolve
              * smoothstep(0.35, 0.60, carve + age * 0.2);

    //白亮领刃：仅最新缘、外半侧
    float core = smoothstep(0.78, 1.0, age) * smoothstep(0.50, 0.06, uv.y)
               * dissolve * (0.55 + uHeat * 0.45);

    //颜色：石白底 → 暖白丝线 → 鎏金边 → 白芯
    float3 cStone = float3(0.72, 0.68, 0.60);
    float3 cWarm  = float3(1.00, 0.94, 0.80);
    float3 cGold  = float3(0.98, 0.78, 0.34);
    float3 cWhite = float3(1.00, 0.99, 0.95);

    float3 color = cStone * body * 0.55;
    color += cWarm * filament * 0.85;
    color += cGold * rim * (0.85 + uHeat * 0.5);
    color += cWhite * core * (0.8 + uHeat * 0.5);

    float alpha = saturate(body * 0.42 + filament * 0.35 + rim * 0.6 + core * 0.75) * uFade;
    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass MarbleSlashPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
