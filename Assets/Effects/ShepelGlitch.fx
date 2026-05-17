// ShepelGlitch.fx - CP2077风格故障扭曲，用于Shepel全身立绘
// 横向切片位移 + RGB色差 + 块状损坏 + 亮度闪烁

float uTime;
float uIntensity; // 0~1

sampler2D uImage : register(s0);

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float4 PixelShaderFunction(float2 uv : TEXCOORD0, float4 color : COLOR0) : COLOR0
{
    if (uIntensity <= 0.001)
    {
        return tex2D(uImage, uv) * color;
    }

    float time = uTime * 10.0;
    float it = uIntensity;

    //横向切片位移
    float sliceH = 0.015 + hash11(floor(time * 2.7)) * 0.06;
    float sliceIdx = floor(uv.y / sliceH);
    float sliceRand = hash11(sliceIdx + floor(time * 6.0) * 137.0);
    float displace = 0.0;
    if (sliceRand > 1.0 - it * 0.55)
    {
        displace = (hash11(sliceIdx * 7.13 + time * 1.3) - 0.5) * it * 0.18;
    }

    //块状损坏
    float2 bSizeA = float2(0.12 + hash11(time + 0.1) * 0.18, 0.025 + hash11(time + 0.2) * 0.055);
    float2 bPosA = float2(hash11(floor(time * 3.5) + 10.0), hash11(floor(time * 3.5) + 20.0));
    float2 bUvA = (uv - bPosA) / bSizeA;
    float inA = step(0.0, bUvA.x) * step(bUvA.x, 1.0) * step(0.0, bUvA.y) * step(bUvA.y, 1.0);
    float blockD = inA * (hash11(time * 2.7 + 5.0) - 0.5) * it * 0.22;

    float2 bSizeB = float2(0.08 + hash11(time + 3.0) * 0.14, 0.02 + hash11(time + 4.0) * 0.04);
    float2 bPosB = float2(hash11(floor(time * 2.8) + 30.0), hash11(floor(time * 2.8) + 40.0));
    float2 bUvB = (uv - bPosB) / bSizeB;
    float inB = step(0.0, bUvB.x) * step(bUvB.x, 1.0) * step(0.0, bUvB.y) * step(bUvB.y, 1.0);
    blockD += inB * (hash11(time * 1.9 + 8.0) - 0.5) * it * 0.16;

    float2 bSizeC = float2(0.05 + hash11(time + 6.0) * 0.1, 0.015 + hash11(time + 7.0) * 0.03);
    float2 bPosC = float2(hash11(floor(time * 5.0) + 50.0), hash11(floor(time * 5.0) + 60.0));
    float2 bUvC = (uv - bPosC) / bSizeC;
    float inC = step(0.0, bUvC.x) * step(bUvC.x, 1.0) * step(0.0, bUvC.y) * step(bUvC.y, 1.0);
    blockD += inC * (hash11(time * 3.3 + 12.0) - 0.5) * it * 0.12;

    float totalDX = displace + blockD;

    //RGB色差
    float chromaOff = it * 0.025;
    float2 uvR = float2(uv.x + totalDX + chromaOff, uv.y);
    float2 uvG = float2(uv.x + totalDX, uv.y);
    float2 uvB = float2(uv.x + totalDX - chromaOff, uv.y);

    float4 colR = (uvR.x >= 0.0 && uvR.x <= 1.0 && uvR.y >= 0.0 && uvR.y <= 1.0)
        ? tex2D(uImage, uvR) : float4(0, 0, 0, 0);
    float4 colG = (uvG.x >= 0.0 && uvG.x <= 1.0 && uvG.y >= 0.0 && uvG.y <= 1.0)
        ? tex2D(uImage, uvG) : float4(0, 0, 0, 0);
    float4 colB = (uvB.x >= 0.0 && uvB.x <= 1.0 && uvB.y >= 0.0 && uvB.y <= 1.0)
        ? tex2D(uImage, uvB) : float4(0, 0, 0, 0);

    //越界返回透明，以中心通道alpha定轮廓，R/B通道反预乘后重新预乘，保证透明区域RGB为0避免白色矩形
    float baseAlpha = colG.a;
    float4 result;
    result.r = (colR.a > 0.01) ? (colR.r / max(colR.a, 0.001) * baseAlpha) : colG.r;
    result.g = colG.g;
    result.b = (colB.a > 0.01) ? (colB.b / max(colB.a, 0.001) * baseAlpha) : colG.b;
    result.a = baseAlpha;

    //扫描线干扰，乘以alpha保持预乘状态，透明区域贡献为0
    float scanline = sin(uv.y * 600.0 + time * 18.0) * 0.05 * it;
    result.rgb -= scanline * baseAlpha;

    //亮度闪烁
    float flicker = 1.0 + (hash11(floor(time * 9.0)) - 0.5) * it * 0.35;
    result.rgb *= flicker;

    //偶发色调闪变，乘以alpha保持预乘状态，透明区域贡献为0
    float tintChance = hash11(floor(time * 4.0) + 99.0);
    if (tintChance > 0.85 && it > 0.3)
    {
        float tintType = hash11(floor(time * 4.0) + 77.0);
        float3 tintColor = tintType > 0.5 ? float3(0.1, 0.3, 0.4) : float3(0.3, 0.05, 0.2);
        result.rgb += tintColor * it * 0.25 * baseAlpha; //乘以alpha保持预乘状态，透明区域贡献为0
    }

    return result * color;
}

technique Technique1
{
    pass GlitchPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
