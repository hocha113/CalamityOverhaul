// ============================================================================
//EmpressLanceBeam.fx 光之女皇·光构造三件套
//TelegraphTech 预告折光线 / LanceTech 以太枪骑矛体 / BladeTech 剑雨光剑
//材质=折射的纯光：光谱色散边缘+白热锐芯+干涉细纹；无烟无火
//UV.x 0尾→1头 UV.y 横截面；Additive；无atan2无动态分支
// ============================================================================

float4x4 transformMatrix;
float uTime;
float uHue;       //本体色相 0~1
float uProgress;  //预告充能进度 0~1（矛体/剑体传1）
float uOpacity;   //整体透明度

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

//廉价色相→RGB
float3 hueRGB(float h)
{
    h = frac(h);
    float r = abs(h * 6.0 - 3.0) - 1.0;
    float g = 2.0 - abs(h * 6.0 - 2.0);
    float b = 2.0 - abs(h * 6.0 - 4.0);
    return saturate(float3(r, g, b));
}

//----------------------------------------------------------------------------
//预告折光线：细亮芯+色散侧纹+沿线奔跑的装填光头，进度推白
//----------------------------------------------------------------------------
float4 TelegraphPS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float cross_ = (uv.y - 0.5) * 2.0;

    //穿刺前压缩：临射末拍整条线向芯收拢推白（能量被压进杆体）
    float compress = smoothstep(0.80, 1.0, uProgress);

    //细芯：越接近发射越锐越白，压缩期骤缩
    float coreW = lerp(30.0, 90.0, uProgress) + compress * 170.0;
    float core = exp(-cross_ * cross_ * coreW);

    //色散侧纹：两侧红/紫错开的干涉细线，压缩期向芯并拢
    float fringeOff = lerp(0.34, 0.10, compress);
    float fringeA = exp(-pow((cross_ - fringeOff) * 9.0, 2.0)) * 0.5;
    float fringeB = exp(-pow((cross_ + fringeOff) * 9.0, 2.0)) * 0.5;

    //装填光头沿线奔跑，频率随进度提升
    float runner = frac(along * 1.4 - uTime * (0.7 + uProgress * 1.6));
    float runGlow = exp(-pow((runner - 0.5) * 5.0, 2.0)) * 0.8;

    //亮度呼吸+两端羽化
    float pulse = 0.55 + 0.45 * sin(uTime * (6.0 + uProgress * 16.0));
    float endFade = smoothstep(0.0, 0.05, along) * (1.0 - smoothstep(0.72, 1.0, along));

    float3 prism = hueRGB(uHue);
    float3 white = float3(1.0, 1.0, 1.0);

    float3 color = float3(0.0, 0.0, 0.0);
    color += lerp(prism, white, 0.35 + 0.55 * uProgress) * core * (0.4 + 0.6 * uProgress + compress * 0.7) * pulse;
    color += hueRGB(uHue + 0.06) * fringeA;
    color += hueRGB(uHue - 0.06) * fringeB;
    color += white * runGlow * core * 0.7;

    float alpha = saturate((core * (0.35 + 0.65 * uProgress + compress * 0.5) + (fringeA + fringeB) * 0.5 + runGlow * core * 0.4) * endFade);
    alpha *= uOpacity;
    return float4(color * alpha * endFade, alpha) * input.Color;
}

//----------------------------------------------------------------------------
//矛体：纺锤白芯+光谱色散鞘+尾部干涉带撕散
//----------------------------------------------------------------------------
float4 LancePS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float cross_ = (uv.y - 0.5) * 2.0;

    //矛形宽度包络：尖端极锐，中段饱满，尾段收细
    float profile = smoothstep(1.0, 0.86, along) * lerp(0.34, 1.0, smoothstep(0.0, 0.42, along));
    float s = cross_ / max(profile, 0.05);
    float d = abs(s);

    float core = exp(-d * d * 34.0);
    float hot = exp(-d * d * 300.0);

    //杆体棱线：芯线外两条对称亮棱，读作实体杆而非光雾
    float rail = exp(-pow((d - 0.52) * 9.0, 2.0)) * 0.55;
    //螺旋槽纹：斜向流动细纹（光在杆体内旋进）
    float flute = 0.84 + 0.16 * sin(along * 64.0 - uTime * 26.0 + s * 3.0);

    //光谱鞘：按横截距离色散（内白→本色→偏移色）
    float3 sheath = hueRGB(uHue + d * 0.16 - 0.05);

    //尾部干涉带：sin细纹让尾段散成光栅
    float grating = 0.75 + 0.25 * sin(along * 90.0 + uTime * 7.0);
    float tailZone = 1.0 - smoothstep(0.30, 0.72, along);
    float body = lerp(1.0, grating, tailZone * 0.85) * flute;

    //尾淡出+尖端聚光
    float tailFade = smoothstep(0.0, 0.24, along);
    float tipFlare = smoothstep(0.78, 0.985, along) * core * 1.5;

    float3 white = float3(1.0, 1.0, 1.0);
    float3 color = float3(0.0, 0.0, 0.0);
    color += sheath * core * 1.0 * body;
    color += white * hot * 1.3;
    color += hueRGB(uHue + 0.08) * rail * body;
    color += lerp(sheath, white, 0.7) * tipFlare;
    //宽晕
    float halo = exp(-d * d * 2.6) * 0.4;
    color += hueRGB(uHue) * halo;

    float alpha = saturate((core * 0.85 * body + hot * 0.95 + rail * 0.32 + halo * 0.4 + tipFlare * 0.8) * tailFade);
    alpha *= uOpacity;
    return float4(color * alpha * tailFade, alpha) * input.Color;
}

//----------------------------------------------------------------------------
//光剑：剑形轮廓（近柄宽腹→锐尖）+护手辉点+棱镜刃缘；悬停期随进度凝实
//----------------------------------------------------------------------------
float4 BladePS(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float cross_ = (uv.y - 0.5) * 2.0;

    //剑形：腹部在33%处最宽，向尖端二次收锐，柄端快收
    float belly = smoothstep(0.0, 0.30, along) * (1.0 - smoothstep(0.33, 0.97, along) * 0.82);
    float profile = max(belly, 0.06);
    float s = cross_ / profile;
    float d = abs(s);

    //剑尖收口：贴图缘之前刃体归零，尖端不被四边形边缘裁出断口
    float tipClose = 1.0 - smoothstep(0.90, 0.995, along);

    float core = exp(-d * d * 40.0);
    float hot = exp(-d * d * 340.0);

    //非对称刃缘：刃侧一条白热锋线，背侧色散逸散——有锋有背才是剑
    float edgeFore = exp(-pow((s + 0.82) * 9.0, 2.0)) * 0.85;
    float edgeBack = exp(-pow((s - 0.72) * 5.0, 2.0)) * 0.45;
    float3 edgeCol = hueRGB(uHue + 0.1);

    //护手辉点：柄部一粒亮星
    float guardD = length(float2((along - 0.10) * 3.2, cross_ * 1.4));
    float guard = exp(-guardD * guardD * 30.0) * 0.9;

    //悬停凝实：未满进度时刃身闪烁未定型
    float forming = lerp(0.55 + 0.45 * sin(uTime * 22.0 + along * 30.0), 1.0, saturate(uProgress));

    float tailFade = smoothstep(0.0, 0.07, along);
    float3 prism = hueRGB(uHue);
    float3 white = float3(1.0, 1.0, 1.0);

    float3 color = float3(0.0, 0.0, 0.0);
    color += prism * core * forming;
    color += white * hot * 1.25 * forming;
    color += white * edgeFore * forming;
    color += edgeCol * edgeBack * forming;
    color += white * guard;

    float alpha = saturate((core * 0.8 + hot * 0.9 + edgeFore * 0.6 + edgeBack * 0.35 + guard * 0.7) * forming * tailFade * tipClose);
    alpha *= uOpacity;
    return float4(color * alpha * tailFade * tipClose, alpha) * input.Color;
}

technique TelegraphTech
{
    pass TelegraphPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 TelegraphPS();
    }
}

technique LanceTech
{
    pass LancePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 LancePS();
    }
}

technique BladeTech
{
    pass BladePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 BladePS();
    }
}
