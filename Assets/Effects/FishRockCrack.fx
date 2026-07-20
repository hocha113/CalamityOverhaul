// ============================================================================
//FishRockCrack.fx 岩鱼锤嵌地裂纹 decal（横宽压扁椭圆域）
//哑光石质:暗色裂缝压暗地面+微亮岩粉尘边,全程无热色无发光
//主缝 = 沿地面的噪声扰动水平窄带;支缝 = 双八度脊线噪声,中心粗边缘细
//uLife 0..1:出生~3帧粉尘过曝(灰白非纯白)→裂纹定形→裂缝收窄+尘边先蚀+整体淡出
//全部输入为 quad uv,无极角无缝;预乘 alpha 输出,配 AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;   //秒
float uLife;   //0..1 decal生命进度
float uSeed;   //实例随机

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

static const float3 ColCrack = float3(0.085, 0.078, 0.070);  //裂缝暗芯
static const float3 ColDust = float3(0.560, 0.525, 0.470);   //岩粉尘边
static const float3 ColFlash = float3(0.760, 0.730, 0.660);  //出生粉尘闪(暖灰非纯白)

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

//支缝脊线场:两八度 1-|n-0.5| 阈值,返回 0..1
float CrackField(float2 uv, float thin)
{
    float n1 = tex2D(noiseSamp, uv * 1.8 + uSeed).r;
    float n2 = tex2D(noiseSamp, uv * 4.1 + uSeed * 2.3 + 0.37).r;
    float ridge1 = 1.0 - smoothstep(0.0, thin, abs(n1 - 0.5));
    float ridge2 = 1.0 - smoothstep(0.0, thin * 1.5, abs(n2 - 0.5));
    return saturate(ridge1 + ridge2 * 0.5);
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 p = (uv - 0.5) * 2.0;

    //横宽椭圆域:x 沿地面展开,y 压扁
    float dome = length(p * float2(1.0, 1.9));
    if (dome > 1.0)
        return float4(0, 0, 0, 0);
    float domeFade = smoothstep(1.0, 0.5, dome);

    //出生粉尘闪(前 6%,约 3 帧)与后段冷却
    float birth = 1.0 - smoothstep(0.0, 0.06, uLife);
    float cool = smoothstep(0.35, 1.0, uLife);

    //主缝:沿地面的水平窄带,走向被噪声扰动
    float wander = tex2D(noiseSamp, float2(uv.x * 1.4 + uSeed, uSeed * 3.1)).r;
    float seam = 1.0 - smoothstep(0.0, lerp(0.17, 0.07, cool), abs(p.y + (wander - 0.5) * 0.6));

    //支缝:中心粗边缘细,冷却期收窄
    float thin = lerp(0.13, 0.05, dome) * (1.0 - cool * 0.5);
    float branch = CrackField(uv * float2(2.4, 1.2), thin);

    float crack = saturate(seam * 0.9 + branch * 0.8) * domeFade;

    //岩粉尘边:砸点中心的浮尘,缓慢流动,随冷却先于裂缝蚀去
    float dustNoise = tex2D(noiseSamp, uv * 1.1 + float2(uTime * 0.03, uSeed)).r;
    float dustHalo = smoothstep(0.6, 0.0, dome) * (1.0 - cool) * (0.5 + 0.5 * dustNoise);

    float aCrack = crack * (1.0 - cool * 0.75);
    float aDust = dustHalo * 0.38;
    float aBirth = birth * domeFade * 0.85;

    float3 col = ColCrack * aCrack + ColDust * aDust + ColFlash * aBirth;
    float alpha = saturate(aCrack * 0.85 + aDust + aBirth);
    float endFade = smoothstep(1.0, 0.78, uLife);   //末段整体淡出
    return float4(col * endFade, alpha * endFade);
}

technique GroundTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
