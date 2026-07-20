// ============================================================================
//FishFrostMinnowFern.fx 凝霜宣言命中冰凌花纹 decal（贴宿主表面的椭圆域）
//冰晶质感:Voronoi 脊线晶脉沿表面自命中点向外爬开,主脉沿切向,细脉羽状偏轴
//uGrow 0..1 生长前沿(错相参差,前沿一圈冰白亮带,长成后前沿熄灭)
//uFade 0..1 融解(外梢先融,末段整体淡出);uGlint 门控高频晶点瞬闪
//色彩:深蓝薄霜垫底压暗(带 alpha)+淡青晶脉+冰白仅前沿与瞬闪,无彩虹无亮蓝糊屏
//全部输入为 quad uv 与 length(p),无极角无缝;预乘 alpha 输出,配 AlphaBlend
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;    //秒
float uSeed;    //实例随机相位
float uGrow;    //0..1 结晶生长进度
float uFade;    //0..1 融解进度
float uGlint;   //0..1 晶点瞬闪门控

texture uVoroTex;
sampler voroSamp = sampler_state
{
    texture = <uVoroTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
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

static const float3 ColFrost = float3(0.086, 0.149, 0.267);  //深蓝薄霜垫底
static const float3 ColVein = float3(0.588, 0.839, 0.925);   //淡青晶脉
static const float3 ColIce = float3(0.886, 0.953, 1.0);      //冰白(仅前沿/瞬闪)

//Voronoi 脊线场:值穿 0.5 处为晶脉线,thin 控线宽
float VeinField(float2 uv, float thin)
{
    float v1 = tex2D(voroSamp, uv * 1.15 + uSeed).r;
    float v2 = tex2D(voroSamp, uv * 2.60 + uSeed * 2.3 + 0.41).r;
    float ridge1 = 1.0 - smoothstep(0.0, thin, abs(v1 - 0.5));
    float ridge2 = 1.0 - smoothstep(0.0, thin * 0.7, abs(v2 - 0.5));
    return saturate(ridge1 + ridge2 * 0.65);
}

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
    float2 p = (uv - 0.5) * 2.0;      //x 沿表面切向长轴,y 横向
    float r = length(p);
    if (r > 1.0)
        return float4(0, 0, 0, 0);
    float domeFade = smoothstep(1.0, 0.72, r);

    //错相生长:噪声抖动生长半径,结晶前沿参差
    float stagger = tex2D(noiseSamp, uv * 2.7 + uSeed * 5.1).r - 0.5;
    float rr = r + stagger * 0.26;

    //生长前沿:R 自 0 扫向 1.15,长成域 = rr < R
    float R = uGrow * 1.15;
    float grown = smoothstep(R, R - 0.10, rr);
    //前沿冰白亮带:生长途中一圈结晶热线,长成后熄灭
    float rim = smoothstep(R - 0.09, R, rr) * smoothstep(R + 0.045, R, rr);
    rim *= 1.0 - smoothstep(0.80, 1.0, uGrow);

    //融解:外梢先融,融线内缩
    float meltR = (1.0 - uFade) * 1.15;
    float melt = smoothstep(meltR, meltR - 0.12, rr);
    float endFade = 1.0 - smoothstep(0.70, 1.0, uFade);

    //羽状各向异性:晶脉沿 ±x 主轴更密,横向稀疏
    float axial = abs(p.x) / max(r, 0.002);
    float feather = 0.42 + 0.58 * pow(axial, 1.35);

    //主脉:沿切向的窄亮线,走向被噪声扰动
    float wander = tex2D(noiseSamp, float2(uv.x * 1.5 + uSeed, uSeed * 3.3)).r;
    float spine = 1.0 - smoothstep(0.0, 0.085, abs(p.y + (wander - 0.5) * 0.42));
    spine *= smoothstep(1.0, 0.15, abs(p.x));

    //细脉:双八度 Voronoi 脊线,中心粗外梢细
    float thin = lerp(0.115, 0.05, r);
    float vein = VeinField(uv * float2(1.35, 1.0), thin) * feather;

    float crystal = saturate(spine * 0.95 + vein * 0.85) * domeFade * grown;

    //晶点瞬闪:高频 Voronoi 阈值点 × 时间闪烁门,小而锐、不驻留
    float spN = tex2D(voroSamp, uv * 5.3 + uSeed * 9.7).r;
    float twinkle = 0.5 + 0.5 * sin(uTime * 8.0 + rr * 22.0 + uSeed * 40.0);
    float glint = smoothstep(0.965, 0.995, spN * (0.62 + 0.38 * twinkle)) * grown * uGlint;

    //深蓝薄霜垫底:整片被霜面积的暗压,晶脉亮部才立得住
    float fill = domeFade * grown * 0.55;

    float aFill = fill * 0.30;
    float aVein = crystal * 0.42;
    float aRim = rim * domeFade * 0.55;

    float3 col = ColFrost * aFill
        + ColVein * (crystal * 0.85 + aVein * 0.4)
        + ColIce * (rim * domeFade * 0.9 + glint * 0.8);

    float alpha = saturate(aFill + aVein + aRim + glint * 0.5);
    float env = melt * endFade;
    return float4(col * env, alpha * env) * input.Color;
}

technique FernTech
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
