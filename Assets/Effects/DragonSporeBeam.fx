// ============================================================================
// DragonSporeBeam.fx —— 螺旋绿藻剑气着色器
// 程序化绘制龙藻巨刃的剑气：双股绿藻螺旋缠绕亮芯，孢子荧光自尾迹剥离
// quad 由 C# 端按速度方向旋转
// uv.x: 1=剑气前端 → 0=尾迹
// uv.y: 0.5=中轴
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uSeed;    //每道剑气的随机种子
float uFade;    //整体透明度 0~1

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
    float x = uv.x;               //1=前端
    float py = (uv.y - 0.5) * 2.0; //-1..1

    //螺旋包络：前端束紧成尖，中后段张开
    float envelope = lerp(0.18, 0.78, smoothstep(1.0, 0.25, x));

    //双股螺旋相位
    float phase = x * 14.0 - uTime * 22.0 + uSeed * 11.0;
    float s1 = sin(phase);
    float s2 = sin(phase + 3.14159);

    //每股的截面亮带，cos>0 视为前层更亮（伪深度）
    float w = 0.16;
    float strand1 = smoothstep(w, 0.0, abs(py - s1 * envelope)) * (0.55 + 0.45 * saturate(cos(phase)));
    float strand2 = smoothstep(w, 0.0, abs(py - s2 * envelope)) * (0.55 + 0.45 * saturate(cos(phase + 3.14159)));

    //中轴亮芯：前端最锐利
    float core = smoothstep(0.45, 0.0, abs(py)) * smoothstep(0.0, 0.30, x);
    core *= 0.55 + 0.45 * smoothstep(0.45, 1.0, x);

    //前端光锥头
    float headDist = length(float2((x - 0.90) * 1.6, py * 0.95));
    float head = smoothstep(0.34, 0.08, headDist);

    //尾迹与边缘渐隐
    float tailFade = smoothstep(0.0, 0.30, x);
    float edgeFade = smoothstep(1.0, 0.55, abs(py));

    //流动藻噪声给条带蒙上有机质感
    float n = tex2D(noiseSamp, float2(x * 2.2 - uTime * 1.4 + uSeed, py * 1.4 + uSeed * 5.0)).r;

    //孢子荧光点：剥离在螺旋外侧
    float spore = tex2D(noiseSamp, float2(x * 6.0 - uTime * 0.8 + uSeed * 2.0, py * 4.0 + uTime * 0.5)).r;
    float sporeDot = smoothstep(0.80, 0.93, spore) * tailFade * edgeFade;

    float strands = (strand1 + strand2) * tailFade * edgeFade * (0.75 + n * 0.4);

    //颜色：深藻绿 → 叶绿 → 鎏金亮白
    float3 cDark = float3(0.05, 0.20, 0.10);
    float3 cMain = float3(0.30, 0.78, 0.38);
    float3 cGlow = float3(0.88, 1.00, 0.64);

    float3 color = cDark * strands * 0.8;
    color += cMain * strands * 0.85;
    color += cMain * core * 0.55;
    color += cGlow * core * 0.45;
    color += cGlow * head * 1.1;
    color += cGlow * sporeDot * 0.9;

    float alpha = saturate(strands * 0.8 + core * 0.55 + head * 0.9 + sporeDot * 0.45);
    alpha *= uFade;

    return float4(color * alpha, alpha) * input.Color;
}

technique Technique1
{
    pass DragonSporeBeamPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
