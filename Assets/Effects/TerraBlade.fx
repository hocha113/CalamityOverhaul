// ============================================================================
//TerraBlade.fx 泰拉之刃重做 刀光条带(TechSlash) + 泰拉之光彗尾(TechTrail)
//材质=大地翠绿生命能量,由光(白金刃缘)与夜(紫黑内沉)两魂框住:
//  叶脉能流沿弧走、外缘白金锐线+错位第二线、内缘沉入夜紫再撕成暗口、尾部量化成翠色碎屑烧尽
//TechSlash UV.x 0尾→1刃头 UV.y 0外缘光晕垫→1内缘;顶点色 rgb=纵深明暗 a=透明度
//TechTrail UV.x 0弹头→1彗尾 UV.y 0/1两侧 0.5中轴;顶点色 a=透明度
//两条 technique 都自带 VS(同实例混批免疫),输出预乘 alpha 配 AlphaBlend(夜紫内沉是真遮挡)
//噪声按硬规约走 register(s1),消费端 Textures[1]=PerlinNoise(实测值域 0.227~0.776,采样后归一)
// ============================================================================

float4x4 transformMatrix;
float uTime;        //滚动时间(秒)
float uFade;        //0~1 整体存活度,1 完整 0 烧尽(尾侧先蚀)
float uBirth;       //0~1 出鞘成形,0 只剩刃缘线 1 体涌满
float uSoul;        //0 夜魂 0.5 双魂 1 光魂,只染刃缘/光晕
float uHeat;        //刃头热度增益 0.8~2,爆发帧顶到 2
float uBelly;       //0~1 带最厚处的弧向位置(力点)
float uTailWidth;   //0.2~0.6 尾端带宽占比
float uSeed;        //每次挥砍换种子,噪声不复读

sampler noiseSamp : register(s1);

//带外光晕垫占径向比例,C# 网格外扩折算与此锁定
static const float HaloFrac = 0.14;

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

PSInput VS(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float nrm(float x)
{
    return saturate((x - 0.227) / 0.549);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//噪声等值线抠成脊线=叶脉,thr 越高脉越细
float veinLine(float n, float thr)
{
    float ridge = 1.0 - abs(n * 2.0 - 1.0);
    return smoothstep(thr, thr + 0.08, ridge);
}

//脉周软晕:同一等值线外围一圈更宽的柔光,让脉读作发光的汁液而非划痕
float veinSoft(float n, float thr)
{
    float ridge = 1.0 - abs(n * 2.0 - 1.0);
    return smoothstep(thr - 0.16, thr, ridge);
}

//带宽包络:尾细→力点最厚→刃头微收成鼻
float widthEnv(float along)
{
    float rise = smoothstep(0.0, max(uBelly, 0.05), along);
    float w = lerp(uTailWidth, 1.0, rise);
    float nose = smoothstep(uBelly, 1.0, along) * step(uBelly, 0.98);
    return w * (1.0 - 0.18 * nose);
}

//色板
static const float3 cTerraCore  = float3(0.78, 1.00, 0.80);  //白翠热核
static const float3 cTerraHot   = float3(0.42, 1.00, 0.55);  //亮翠
static const float3 cTerraBody  = float3(0.12, 0.66, 0.30);  //大地翠绿
static const float3 cTerraDeep  = float3(0.04, 0.30, 0.16);  //深翠
static const float3 cNightBody  = float3(0.30, 0.09, 0.55);  //夜紫
static const float3 cNightDark  = float3(0.06, 0.02, 0.12);  //夜黑
static const float3 cLightRazor = float3(1.00, 0.95, 0.74);  //光魂白金
static const float3 cNightRazor = float3(0.86, 0.72, 1.00);  //夜魂白紫
static const float3 cLightHalo  = float3(1.00, 0.80, 0.36);  //光晕金
static const float3 cNightHalo  = float3(0.62, 0.34, 1.00);  //光晕紫

float4 PSSlash(PSInput input) : COLOR0
{
    float along = saturate(input.TexCoords.x);      //0 尾 → 1 刃头
    float vRaw = saturate(input.TexCoords.y);       //0 外缘垫 → 1 内缘
    //刃缘以内的体坐标,负值落在光晕垫
    float vBody = (vRaw - HaloFrac) / (1.0 - HaloFrac);
    float w = widthEnv(along);
    //带内归一 0 刃缘 → 1 内边界(>1 为带外空区)
    float r = vBody / max(w, 1e-3);

    float3 razorCol = lerp(cNightRazor, cLightRazor, uSoul);
    float3 haloCol = lerp(cNightHalo, cLightHalo, uSoul);

    //噪声:能流(顺弧拉长)/主脉(低频粗)/细脉/消散块
    float2 flowUv = float2(along * 2.1 - uTime * 1.5 + uSeed, r * 0.45 + uSeed * 0.37);
    float flow = nrm(tex2D(noiseSamp, flowUv).r);
    float2 veinUv = float2(along * 1.3 - uTime * 0.7 + uSeed * 1.7, r * 0.8 + uSeed);
    float veinN = nrm(tex2D(noiseSamp, veinUv).r);
    float2 fineUv = float2(along * 3.0 - uTime * 1.4 + uSeed * 0.4, r * 1.6 + 0.6 + uSeed * 2.3);
    float fineN = nrm(tex2D(noiseSamp, fineUv).r);
    float2 dissUv = float2(along * 1.8 + uSeed * 3.1, r * 0.9 + uTime * 0.15);
    float diss = nrm(tex2D(noiseSamp, dissUv).r);

    //量化碎屑单元:烧尽前沿按小格子崭开,与连续噪声对半混,读作碎裂不读作贴砖
    float cellU = floor(along * 40.0);
    float cellR = floor(saturate(r) * 6.0);
    float h = hash21(float2(cellU * 1.37 + uSeed * 19.0, cellR * 7.3 + floor(uTime * 7.0)));

    //====================== 骨架:可见域 ======================
    float head = smoothstep(0.78, 0.985, along);
    float headHeat = head * head;
    //内缘撕口:夜紫沿噪声咬成舌状暗口
    float innerTear = smoothstep(1.02, 0.80, r + (veinN - 0.5) * 0.22 + (fineN - 0.5) * 0.10);
    //尾端撕丝:尾部先散成沿弧丝再没
    float filament = smoothstep(0.30, 0.60, flow);
    float tailSolid = smoothstep(0.03, 0.30, along);
    float tailShape = lerp(filament, 1.0, tailSolid) * smoothstep(0.0, 0.05, along);
    float inBand = step(-0.0001, vBody) * step(r, 1.0);

    //====================== 出鞘成形 ======================
    float revealFront = lerp(0.92, 0.0, uBirth);
    float reveal = smoothstep(revealFront - 0.16, revealFront + 0.02, 1.0 - r + (flow - 0.5) * 0.08);
    reveal *= smoothstep(0.0, 0.18, uBirth);

    //====================== 烧尽:尾侧先蚀,碎裂前沿一线翠色燃边 ======================
    float cut = (1.0 - uFade) * 1.30;
    float blockNoise = diss * 0.65 + h * 0.35;
    float edgeCoord = along + (blockNoise - 0.5) * 0.26;
    float alive = smoothstep(cut - 0.02, cut + 0.10, edgeCoord);
    float burn = smoothstep(cut - 0.02, cut + 0.03, edgeCoord) * (1.0 - smoothstep(cut + 0.03, cut + 0.10, edgeCoord));
    burn *= step(0.001, 1.0 - uFade);

    //====================== 体层 ======================
    //径向三段:白翠热核(外) → 翠绿体(中) → 夜紫内沉
    float coreBand = smoothstep(0.30, 0.02, r);
    float greenBand = smoothstep(0.02, 0.22, r) * smoothstep(0.90, 0.66, r);
    float nightBand = smoothstep(0.60, 0.90, r);

    //叶脉:粗主脉+细支脉,各带一圈软晕;靠近刃头更亮
    float vein = veinLine(veinN, 0.80) * (0.6 + 0.4 * headHeat);
    float veinFine = veinLine(fineN, 0.88) * 0.35;
    float veins = saturate(vein + veinFine);
    float sap = veinSoft(veinN, 0.80) * 0.30 + veinSoft(fineN, 0.88) * 0.15;

    //能流明暗:顺弧拉丝
    float streak = 0.70 + 0.45 * flow;
    //弧向热记忆:越靠刃头越亮,尾段冷却
    float heat = 0.45 + 0.55 * smoothstep(0.10, 0.95, along);

    float3 col = float3(0, 0, 0);
    float3 body = lerp(cTerraDeep, cTerraBody, streak) * heat;
    col += body * greenBand;
    col += cTerraHot * greenBand * headHeat * 0.22;
    col += lerp(cTerraHot, cTerraCore, headHeat * 0.7) * coreBand * (0.85 + 0.45 * headHeat * uHeat);
    //夜紫内沉:暗体,靠内越黑
    float3 night = lerp(cNightBody, cNightDark, smoothstep(0.70, 1.0, r));
    col += night * nightBand;
    //叶脉:翠区亮脉+汁液软晕,夜区换成夜紫发光脉
    col += cTerraHot * sap * greenBand * heat;
    col += lerp(cTerraHot, cTerraCore, 0.6) * veins * greenBand * 0.7 * heat;
    col += cNightHalo * veins * nightBand * 0.55;
    col += cTerraHot * veins * coreBand * 0.5;

    //====================== 刃缘:白金/白紫主线 + 错位第二线(px 级细,按 vBody 定宽) ======================
    float razor = exp(-pow(vBody / 0.030, 2.0));
    float razor2 = exp(-pow((vBody - 0.075) / 0.016, 2.0)) * 0.55;
    float razorGain = (1.0 + 0.9 * headHeat) * uHeat;
    //刃缘沿弧串珠,读出高速
    float beads = 0.75 + 0.25 * step(0.55, nrm(tex2D(noiseSamp, float2(along * 14.0 - uTime * 6.0 + uSeed, 0.5)).r));
    col += razorCol * razor * razorGain * beads;
    col += lerp(razorCol, cTerraCore, 0.5) * razor2 * razorGain;

    //内缘暗口边线:撕口处一圈更黑的硬边
    float innerRim = smoothstep(0.86, 0.94, r) * innerTear;
    col = lerp(col, cNightDark, innerRim * 0.7);

    //燃边:烧尽前沿一线亮翠
    col += cTerraHot * burn * 1.1;

    //====================== alpha ======================
    float alpha = greenBand * 0.90 + coreBand * 0.97 + nightBand * 0.84;
    alpha = max(alpha, razor * 0.95 + razor2 * 0.6);
    alpha *= innerTear;
    alpha *= inBand;
    alpha *= tailShape;
    alpha *= reveal;
    alpha *= alive;
    alpha = saturate(alpha + burn * 0.35 * inBand * reveal * innerTear);

    //====================== 带外光晕垫:刃缘向外软衰减,魂色 ======================
    float haloIn = saturate(1.0 + vBody / HaloFrac * (1.0 - HaloFrac));   //1 贴刃缘 → 0 垫外缘
    float halo = pow(haloIn, 2.6) * step(vBody, 0.0) * tailShape * alive * smoothstep(0.0, 0.3, uBirth);
    halo *= 0.32 + 0.38 * headHeat;
    float3 haloRgb = haloCol * halo * (0.8 + 0.4 * uHeat);

    //顶点色:rgb 纵深压暗 a 透明度
    float3 rgbOut = (col * alpha + haloRgb) * input.Color.rgb;
    float aOut = saturate(alpha + halo * 0.55) * input.Color.a;
    return float4(rgbOut, aOut);
}

//====================== 泰拉之光彗尾 ======================
float4 PSTrail(PSInput input) : COLOR0
{
    float u = saturate(input.TexCoords.x);   //0 弹头 → 1 尾
    float v = input.TexCoords.y;
    float d = abs(v - 0.5) * 2.0;            //0 中轴 → 1 边

    float3 edgeCol = lerp(cNightHalo, cLightHalo, uSoul);
    float3 razorCol = lerp(cNightRazor, cLightRazor, uSoul);

    float flow = nrm(tex2D(noiseSamp, float2(u * 1.6 - uTime * 2.4 + uSeed, d * 0.35 + uSeed * 0.7)).r);
    float veinN = nrm(tex2D(noiseSamp, float2(u * 1.4 - uTime * 1.1 + uSeed * 2.1, v * 0.9 + uSeed)).r);
    float tearN = nrm(tex2D(noiseSamp, float2(u * 3.2 + uSeed * 4.0, v * 2.0 + uTime * 0.6)).r);

    //尾部随 u 收窄并撕成丝
    float widthShrink = lerp(1.0, 0.30, smoothstep(0.30, 1.0, u));
    float dn = d / max(widthShrink, 1e-3);
    float edgeMask = smoothstep(1.0, 0.70, dn + (tearN - 0.5) * 0.46 * smoothstep(0.25, 1.0, u));
    float tailFade = 1.0 - smoothstep(0.55, 1.0, u + (flow - 0.5) * 0.25);

    float core = exp(-pow(dn / 0.18, 2.0));
    float body = smoothstep(0.95, 0.20, dn);
    float edge = smoothstep(0.50, 0.90, dn) * edgeMask;
    float veins = veinLine(veinN, 0.82) * body;
    float sap = veinSoft(veinN, 0.82) * body * 0.4;
    float headGlow = smoothstep(0.35, 0.0, u);

    float3 col = lerp(cTerraDeep, cTerraBody, 0.55 + 0.45 * flow) * body;
    col += cTerraHot * sap;
    col += cTerraHot * core * (0.8 + 1.4 * headGlow);
    col += cTerraCore * core * headGlow * 0.9;
    col += edgeCol * edge * 1.5;
    col += razorCol * veins * 0.7;

    float alpha = saturate(body * 0.85 + core * 0.95 + edge * 0.75);
    alpha *= edgeMask * tailFade * uFade;

    float3 rgbOut = col * alpha * input.Color.rgb;
    return float4(rgbOut, alpha * input.Color.a);
}

technique TechSlash
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PSSlash();
    }
}

technique TechTrail
{
    pass P0
    {
        VertexShader = compile vs_3_0 VS();
        PixelShader = compile ps_3_0 PSTrail();
    }
}
