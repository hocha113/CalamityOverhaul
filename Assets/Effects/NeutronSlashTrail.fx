// ============================================================================
//NeutronSlashTrail.fx 黑域斩切刀光
//投影运动缎带，premultiplied AlphaBlend（深色本体在亮天空下不会烧穿）
//UV.x 0=起笔(最老，先蒸发) 1=收笔(最新)
//UV.y 0=外刃缘 1=内缘，0.5=刃迹中线
//厚度包络已烘进几何，顶点色携带远近半侧压暗
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uFade;        //整体透明度 0~1
float uHeat;        //引力强度 0~1，终结月牙提升
float uForcePoint;  //受力点位置 0~1，之前干笔碎、之后密实撕裂

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
    float age = uv.x;       //1=最新
    float across = uv.y;    //0=外刃缘 1=内缘
    float fromCenter = abs(across * 2.0 - 1.0);

    //深空尘埃流：缓慢沉稳的双层噪声
    float n1 = tex2D(noiseSamp, float2(age * 1.4 - uTime * 0.8, across * 0.7 + uTime * 0.1)).r;
    float n2 = tex2D(noiseSamp, float2(age * 2.8 + uTime * 0.35, across * 1.6 - uTime * 0.5)).r;
    float dust = n1 * 0.65 + n2 * 0.35;

    //横截面：中线实、两缘薄，噪声只咬边不啃穿
    float edgeBite = (dust - 0.5) * 0.18;
    float band = smoothstep(1.0, 0.12, fromCenter + edgeBite);

    //受力点：入刀侧干笔碎裂，出刀侧密实撕裂，silhouette 里写清力从哪来
    float past = smoothstep(uForcePoint - 0.28, uForcePoint + 0.12, age);
    float feiBai = lerp(0.55, 0.12, past);
    band *= 1.0 - feiBai * smoothstep(0.62, 0.18, dust);

    //尾迹老化：引力残留消散得慢，星河绵长
    float ageMask = smoothstep(0.0, 0.42, age);
    ageMask = ageMask * ageMask * (3.0 - 2.0 * ageMask);

    float intensity = band * ageMask;

    //引力拖曳暗纹：沿弧被拉长弯折的条带
    float lane = 0.80 + 0.20 * sin((across * 9.0 + dust * 2.6 - uTime * 1.2) * 3.14159);
    intensity *= lane;

    //星屑：超高频噪声双阈值产生闪点，随时间闪烁
    float starNoise = tex2D(noiseSamp, float2(age * 9.0 + 31.7, across * 8.0 - 17.3)).r;
    float twinkle = 0.55 + 0.45 * sin(uTime * 6.0 + starNoise * 40.0);
    float star = smoothstep(0.84 - uHeat * 0.06, 0.94, starNoise) * twinkle * ageMask * band;

    //尘埃丝缕
    float filament = smoothstep(0.60, 0.86, dust) * intensity;

    //引力透镜光环：贴外刃缘的一条细亮线。
    //白是结构不是增益——只占极小面积，本体反而压得更暗
    float rimDist = abs(across - 0.075);
    float rim = saturate(1.0 - rimDist / 0.055);
    rim = rim * rim * ageMask * (0.42 + uHeat * 0.58) * (0.35 + 0.65 * past);

    //颜色：深空紫本体，白只留给透镜环与星屑
    float3 cDark = float3(0.07, 0.025, 0.19);
    float3 cMain = float3(0.42, 0.24, 0.92);
    float3 cGlow = float3(0.88, 0.82, 1.00);

    float3 color = cDark * intensity * 1.45;
    color += cMain * intensity * 0.32;
    color = lerp(color, cMain, filament * 0.70);
    color += cGlow * rim;
    color += cGlow * star * (0.85 + uHeat * 0.55);
    color += cGlow * filament * (0.15 + uHeat * 0.28);

    float alpha = saturate(intensity * 0.90 + filament * 0.28 + rim * 0.72 + star * 0.50);
    alpha *= uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass NeutronSlashTrailPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
