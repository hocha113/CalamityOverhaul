// ============================================================================
//KikasaDreamFog.fx 鬼梦贴地雾（TechGroundFog）
//世界锚定地形跟随三角带：C# 逐列探地建带（KikasaDreams/KikasaDreamFogRender.cs），
//顶点契约：POSITION=世界坐标（VS 过 transformMatrix，并把世界 xy 转发 TEXCOORD1），
//TEXCOORD0.y=带内高度01（0=裙边底 1=带顶），COLOR0.r=断崖渐隐
//密度=底重剖面 × 噪声侵蚀顶缘 × 断崖渐隐；风相滚动=雾贴着地走；
//uRepulse[6] 驱散孔（玩家/光标/恶犬），孔内让净、孔缘微堆亮=雾被拨开挤在边上
//直线算术+平 tex2D 三采样，无 atan2 无深层 fbm，FNA3D 安全；
//绑定噪声实测值域 0.227~0.776，阈值一律过 nrm() 归一
//预乘输出，进 AlphaBlend；连续场无生命周期，替换粒子堆叠地雾的闪烁
// ============================================================================

sampler uNoiseTex : register(s1); //PerlinNoise，消费端上 s1 + LinearWrap

float4x4 transformMatrix;

float uTime;         //秒（域 EffectTime）
float uWind;         //风速 px/s，正=向右爬
float uAlpha;        //0~1 总透明度（DreamBlend，归返随闪退场）
float4 uRepulse[6];  //驱散源 xy=世界源心 z=半径px w=孔强01；空槽 z=0

//====== 梦雾色板（承接鬼梦沉红） ======
static const float3 FOG_DEEP = float3(0.361, 0.165, 0.157);  //深红灰雾体
static const float3 FOG_LIT  = float3(0.596, 0.290, 0.235);  //吃到红天光的暖亮缘

float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

struct VSInput {
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

struct PSInput {
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
    float2 World : TEXCOORD1;
};

PSInput VSFog(VSInput v) {
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    //世界坐标转发给 PS：噪声锚定与驱散孔都按世界像素算，相机平移不带着雾走
    o.World = v.Position.xy;
    return o;
}

float4 PSGroundFog(PSInput i) : COLOR0 {
    float h = i.TexCoords.y;              //0=裙边底 1=带顶
    float xw = i.World.x - uTime * uWind; //风相：整场雾横向缓行

    //顶缘冠线（低频）与雾体纹理（中频），速度错开成两层视差，雾在翻涌而非贴图平移
    float nCrest = nrm(tex2D(uNoiseTex, float2(xw * 0.0021, 0.23 + uTime * 0.008)).r);
    float nBody = nrm(tex2D(uNoiseTex, float2(xw * 0.0058 + uTime * 0.006, h * 0.34 - uTime * 0.011)).r);

    //雾顶在 0.42~0.86 间起伏，顶缘被噪声撕出软边
    float crest = 0.42 + 0.44 * nCrest;
    float edge = saturate((crest - h) * 4.5);
    //底重剖面：地线一带最实、裙边底轻收、向上渐薄
    float profile = saturate(1.15 - h * 1.05) * saturate(h * 5.0 + 0.30);
    float dens = profile * edge * (0.62 + 0.38 * nBody);

    //驱散孔：孔内密度让净，孔缘细带微堆。空槽 z=0 时两式除数被 max 兜住、贡献归零
    float rim = 0.0;
    for (int s = 0; s < 6; s++) {
        float4 r = uRepulse[s];
        float d = distance(i.World, r.xy);
        float inside = saturate((r.z - d) / max(r.z * 0.55, 1.0));
        inside = inside * inside * (3.0 - 2.0 * inside);
        dens *= 1.0 - r.w * inside;
        float band = saturate((r.z - d) / max(r.z * 0.22, 1.0))
            * saturate((d - r.z * 0.66) / max(r.z * 0.18, 1.0));
        rim += band * r.w;
    }
    rim = saturate(rim);

    //色带：体色深红灰，顶冠与孔缘吃暖亮；亮缘随第三层噪声闪动，读作天光在雾脊上流
    float litW = saturate((h - (crest - 0.24)) * 3.2) * edge;
    float sparkle = nrm(tex2D(uNoiseTex, float2(xw * 0.0124, 0.71 - uTime * 0.017)).r);
    float3 col = lerp(FOG_DEEP, FOG_LIT, saturate(litW * (0.45 + 0.55 * sparkle) + rim * 0.5));

    float alpha = saturate(dens * 0.62 * (1.0 + rim * 0.35)) * i.Color.r * uAlpha;
    return float4(col * alpha, alpha);
}

technique TechGroundFog {
    pass P0 {
        VertexShader = compile vs_3_0 VSFog();
        PixelShader = compile ps_3_0 PSGroundFog();
    }
}
