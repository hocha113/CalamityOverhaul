// ============================================================================
//MarbleSlash.fx 大理石白芯金边刀光
//近战 TriangleStrip 挥砍弧光；猎刀/巨棍共用签名刀光
//UV.x 1=最新挥砍缘 0=尾  UV.y 0=外缘(刀尖侧) 1=内缘(持握侧)
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

    //细凿痕：沿挥砍方向拉长的各向异性纹理，缓慢流动
    float chisel = tex2D(noiseSamp, float2(uv.x * 1.3 - uTime * 0.32, uv.y * 5.5)).r;
    float chisel2 = tex2D(noiseSamp, float2(uv.x * 2.6 + uTime * 0.18, uv.y * 9.0 + 0.37)).r;
    float carve = chisel * 0.6 + chisel2 * 0.4;

    //弧光主体：外缘锐利，内缘朝持握者渐隐
    float outerMask = smoothstep(0.015, 0.10, uv.y);
    float innerMask = smoothstep(1.0, 0.42, uv.y);
    float ageMask = smoothstep(0.0, 0.5, age);
    ageMask *= ageMask;

    //末端石尘崩解：颗粒阈值随尾部升高，弧光碎成石屑感
    float grain = tex2D(noiseSamp, float2(uv.x * 7.0, uv.y * 8.0) + uTime * 0.05).r;
    float dustCut = smoothstep(0.62 - age * 1.3, 0.80 - age * 1.3, grain);

    float body = outerMask * innerMask * ageMask * dustCut;

    //鎏金外缘：贴着外缘的细金线，凿痕啃出细碎缺口
    float rim = smoothstep(0.16, 0.03, uv.y) * ageMask * dustCut
              * smoothstep(0.30, 0.62, carve + age * 0.25);

    //白亮核心：最新缘外侧
    float core = smoothstep(0.72, 1.0, age) * smoothstep(0.55, 0.10, uv.y)
               * outerMask * (0.6 + uHeat * 0.4);

    //凿痕丝线高光
    float filament = smoothstep(0.60, 0.88, carve) * body;

    //颜色：石白底 → 暖白 → 鎏金边 → 白芯
    float3 cStone = float3(0.80, 0.77, 0.70);
    float3 cWarm  = float3(1.00, 0.95, 0.84);
    float3 cGold  = float3(0.95, 0.76, 0.36);
    float3 cWhite = float3(1.00, 0.99, 0.95);

    float3 color = cStone * body * 0.9;
    color += cWarm * body * carve * 0.5;
    color = lerp(color, cWarm, filament * 0.6);
    color += cGold * rim * (0.9 + uHeat * 0.5);
    color += cWhite * core * (0.85 + uHeat * 0.45);

    float alpha = saturate(body * 0.8 + rim * 0.55 + core * 0.65) * uFade;
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
