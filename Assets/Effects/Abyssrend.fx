// ============================================================================
//Abyssrend.fx 裂渊 深渊高压水
//材质=压实的暗海水被生物荧光切开:主体是能遮挡的靛黑水体,青光只住在刃口/表层/空化环
//TechSlash  刀光带  UV.x 0尾→1刃口  UV.y 0外缘→1内缘  预乘 AlphaBlend
//TechCurrent 暗流管  UV.x 0尾→1头   UV.y 0一侧→1对侧(0.5=轴)
//TechBurst  空化爆  画布半径 0.42,C# quadPx = 可见半径px / 0.42 * 2
//TechClamp  钳压场  同画布契约,笛卡尔无 atan2
//噪声 s1=PerlinNoise LinearWrap;ps_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;
float uProgress;    //Burst/Clamp 生命周期 0~1

sampler noiseSamp : register(s1);

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

//笛卡尔旋转,避开 atan2 缝
float2 rot2(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

//=========================================================
//TechSlash 高压水刃:外缘力点厚,内缘撕成泡沫,刃口一线青荧光
//=========================================================
float4 SlashPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float radial = 1.0 - uv.y;

    float n  = tex2D(noiseSamp, float2(along * 1.8 - uTime * 0.7, radial * 0.9 + uTime * 0.04)).r;
    float n2 = tex2D(noiseSamp, float2(along * 4.4 - uTime * 1.6, radial * 2.2 + 0.37)).r;

    float tail = pow(smoothstep(0.0, 0.28, along + (n2 - 0.5) * 0.16), 1.35);
    float innerTear = smoothstep(0.03, 0.28, radial + (n - 0.5) * 0.2);
    float outerCap = smoothstep(1.0, 0.96, radial);
    float vis = tail * innerTear * outerCap * fadeAlpha;
    if (vis < 0.003)
        return float4(0, 0, 0, 0);

    //厚度不对称:外缘 0.62~0.92 最厚(力点在刃侧,不是居中光带)
    float bodyBand = smoothstep(0.08, 0.32, radial) * smoothstep(0.985, 0.88, radial);
    float bodyMass = bodyBand * (0.78 + 0.22 * smoothstep(0.34, 0.78, radial));
    bodyMass *= 0.84 + 0.16 * n;

    float foam = smoothstep(0.10, 0.28, radial) * smoothstep(0.52, 0.22, radial);
    foam *= step(0.62, n2);

    float edgeDist = abs(radial - 0.93);
    float edgeGlow = exp(-edgeDist * edgeDist * 220.0);
    float edgeCore = exp(-edgeDist * edgeDist * 2600.0);
    edgeGlow *= 0.72 + 0.28 * sin(along * 26.0 - uTime * 18.0);

    float head = smoothstep(0.78, 0.99, along);
    head *= head;

    //水体里的荧光脉,噪声阈值,不是电
    float veinR = 0.38 + 0.40 * tex2D(noiseSamp, float2(along * 1.7 - uTime * 1.4, 0.41)).r;
    float vein = exp(-pow((radial - veinR) * 18.0, 2.0)) * bodyBand * 0.55;

    float3 cDeep  = float3(0.018, 0.028, 0.055);
    float3 cBody  = float3(0.055, 0.10, 0.22);
    float3 cFoam  = float3(0.55, 0.82, 0.92);
    float3 cCyan  = float3(0.22, 0.92, 0.95);
    float3 cCore  = float3(0.78, 0.97, 1.00);

    float radGrad = smoothstep(0.12, 0.90, radial);
    float3 color = lerp(cDeep, cBody, radGrad * 0.9) * bodyMass;
    color += cFoam * foam * 0.55;
    color += cCyan * edgeGlow * 0.95;
    color += cCore * edgeCore * 1.15;
    color += cCyan * head * bodyBand * 0.35;
    color += cCyan * vein * 0.7;

    float alpha = saturate(
          bodyMass * 0.94
        + foam * 0.40
        + edgeGlow * 0.55
        + edgeCore * 0.92
        + vein * 0.35
    );
    alpha *= vis * (0.68 + 0.32 * smoothstep(0.08, 0.92, along));
    return float4(color * alpha, alpha) * input.Color;
}

//=========================================================
//TechCurrent 暗流管:轴心更实,两侧泡沫收口,头是圆钝水团不是平切
//=========================================================
float4 CurrentPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float across = uv.y;
    float axis = abs(across - 0.5) * 2.0;

    float n  = tex2D(noiseSamp, float2(along * 2.2 - uTime * 1.8, across * 1.1 + 0.2)).r;
    float n2 = tex2D(noiseSamp, float2(along * 5.0 - uTime * 3.1, across * 2.4 + 0.51)).g;

    float tail = pow(smoothstep(0.0, 0.22, along + (n - 0.5) * 0.12), 1.25);
    float headCap = smoothstep(1.0, 0.86, along); //头端收成水团,禁平切
    float tube = 1.0 - smoothstep(0.42, 0.98, axis + (n2 - 0.5) * 0.18);
    float vis = tail * headCap * tube * fadeAlpha;
    if (vis < 0.003)
        return float4(0, 0, 0, 0);

    float core = 1.0 - smoothstep(0.0, 0.38, axis);
    float skin = smoothstep(0.28, 0.55, axis) * smoothstep(0.95, 0.62, axis);
    float foam = step(0.78, n2) * skin;
    float caustic = pow(saturate(n * n2), 3.5) * core * 2.2;
    caustic *= 0.55 + 0.45 * sin(along * 18.0 - uTime * 10.0);

    float3 cDeep = float3(0.015, 0.03, 0.06);
    float3 cMid  = float3(0.04, 0.14, 0.28);
    float3 cCyan = float3(0.18, 0.85, 0.95);
    float3 cFoam = float3(0.62, 0.90, 0.98);

    float3 color = lerp(cDeep, cMid, core);
    color += cCyan * skin * 0.85;
    color += cCyan * caustic * 0.55;
    color += cFoam * foam * 0.7;
    color += cCyan * smoothstep(0.72, 0.94, along) * core * 0.45;

    float alpha = saturate(tube * 0.92 + skin * 0.5 + caustic * 0.25 + foam * 0.4);
    alpha *= vis * (0.55 + 0.45 * along);
    return float4(color * alpha, alpha) * input.Color;
}

//=========================================================
//TechBurst 空化:先内收压实,再环面崩开。环是结构,不是实心光球
//=========================================================
float4 BurstPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 p = uv - 0.5;
    float r = length(p);
    float canvas = 1.0 - smoothstep(0.42, 0.495, r);
    if (canvas < 0.01)
        return float4(0, 0, 0, 0);

    float t = saturate(uProgress);
    float2 pn = p / max(r, 1e-4);
    float2 nUV = rot2(pn * (0.35 + r * 1.8), uTime * 0.6) * 0.5 + 0.5;
    float n  = tex2D(noiseSamp, nUV + float2(uTime * 0.07, 0.13)).r;
    float n2 = tex2D(noiseSamp, nUV * 2.4 + float2(0.41, -uTime * 0.11)).g;

    //0~0.40 内收:暗核从 0.30 收到 0.08
    float implode = saturate(t / 0.40);
    float coreR = lerp(0.30, 0.08, implode);
    //0.40~0.52 塌缩闪:只在环上,不是满盘增益
    float flash = saturate((t - 0.40) / 0.12) * (1.0 - saturate((t - 0.52) / 0.08));
    //0.48~1 冲击环外扩
    float boom = saturate((t - 0.48) / 0.52);
    float ringR = lerp(0.09, 0.38, boom);

    float core = 1.0 - smoothstep(coreR * 0.55, coreR, r);
    core *= (1.0 - boom * 0.85) * (0.75 + 0.25 * n);

    float ring = exp(-pow((r - ringR) / 0.018, 2.0));
    ring *= 0.55 + 0.45 * boom;
    float ringCore = exp(-pow((r - ringR) / 0.007, 2.0)) * boom;

    //环外碎水:噪声撕,不是实心盘
    float sprayBand = smoothstep(ringR - 0.02, ringR + 0.01, r)
                    * smoothstep(ringR + 0.16, ringR + 0.04, r);
    float spray = sprayBand * step(0.42 + boom * 0.18, n2) * boom;

    float flashRing = ring * flash;

    float3 cDeep  = float3(0.02, 0.04, 0.08);
    float3 cBody  = float3(0.05, 0.16, 0.30);
    float3 cCyan  = float3(0.20, 0.90, 0.98);
    float3 cWhite = float3(0.86, 0.97, 1.00);

    float3 color = cDeep * core * 1.4;
    color += cBody * spray * 0.9;
    color += cCyan * ring * 1.1;
    color += cWhite * ringCore * 1.35;
    color += cWhite * flashRing * 1.6;

    float alpha = saturate(core * 0.88 + spray * 0.55 + ring * 0.85 + ringCore * 0.95 + flashRing * 0.7);
    alpha *= canvas * fadeAlpha;
    return float4(color * alpha, alpha) * input.Color;
}

//=========================================================
//TechClamp 钳压:两瓣钳口压在目标两侧,中间暗水 wrap,环是血压不是光圈
//=========================================================
float4 ClampPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float2 p = uv - 0.5;
    float r = length(p);
    float canvas = 1.0 - smoothstep(0.42, 0.495, r);
    if (canvas < 0.01)
        return float4(0, 0, 0, 0);

    float t = saturate(uProgress);
    //闭合度驱动亮度，禁止 uTime 正弦空转（读成待机呼吸）
    float pulse = 0.2 + 0.8 * t;
    float2 nUV = p * 2.4 + float2(uTime * 0.08, -uTime * 0.05);
    float n = tex2D(noiseSamp, nUV * 0.5 + 0.5).r;

    //两瓣钳口:沿 X 轴两侧的厚荚,闭合度随 t
    float close = lerp(0.22, 0.10, t);
    float jawL = exp(-pow((p.x + close) / 0.055, 2.0)) * (1.0 - smoothstep(0.28, 0.40, abs(p.y)));
    float jawR = exp(-pow((p.x - close) / 0.055, 2.0)) * (1.0 - smoothstep(0.28, 0.40, abs(p.y)));
    float jaws = saturate(jawL + jawR);

    float wrap = (1.0 - smoothstep(0.08, 0.34, r)) * (0.7 + 0.3 * n);
    wrap *= 0.45 + 0.55 * t;

    float ringR = lerp(0.30, 0.22, t);
    float ring = exp(-pow((r - ringR) / 0.016, 2.0));
    ring *= 0.6 + 0.4 * pulse;

    float3 cDeep = float3(0.02, 0.035, 0.07);
    float3 cJaw  = float3(0.08, 0.22, 0.38);
    float3 cCyan = float3(0.25, 0.92, 0.96);

    float3 color = cDeep * wrap * 1.3;
    color += cJaw * jaws * 0.9;
    color += cCyan * jaws * (0.25 + 0.35 * t);
    color += cCyan * ring * 0.85;

    float alpha = saturate(wrap * 0.8 + jaws * 0.75 + ring * 0.55);
    alpha *= canvas * fadeAlpha;
    return float4(color * alpha, alpha) * input.Color;
}

technique TechSlash
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 SlashPS();
    }
}

technique TechCurrent
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 CurrentPS();
    }
}

technique TechBurst
{
    pass P0
    {
        PixelShader = compile ps_3_0 BurstPS();
    }
}

technique TechClamp
{
    pass P0
    {
        PixelShader = compile ps_3_0 ClampPS();
    }
}
