// ============================================================================
//KikasaDreamFog.fx 鬼梦贴地雾（TechGroundFog）
//密度源=带符号离地距离场（KikasaDreamGroundField 逐 tick 由瓦片重建，s2 采样）：
//空气为正=离下方最近地面高度，岩内为负=沉入地表深度，R 通道 128=地表、4px/单位。
//任意地形（陡坡/多层洞穴/悬空岛）逐像素贴合；旧管线是 C# 逐列探地三角带，
//复杂地形会把雾带钉在玩家视线高度横贯岩面，已废除。
//顶点契约（KikasaDreamFogRender.cs 对齐）：POSITION=世界坐标窗口 quad
//（VS 过 transformMatrix，世界 xy 转发 TEXCOORD1），COLOR0/TEXCOORD0 不承载数据
//密度=底重剖面 × 噪声侵蚀顶缘 × 跳变闸（真实地面接触处场连续、梯度恒 32px/样距；
//竖直岩壁两侧、薄板底面、崖口无地侧的场值跳变数百 px，双线性过渡会扫出假雾膜，
//按水平+竖直梯度闸掉，顺带免费重现断崖羽化）；
//风相滚动=雾贴着地走；uRepulse[6] 驱散孔（玩家/光标/恶犬），孔内让净、孔缘微堆亮
//直线算术+平 tex2D 八采样（场5+噪声3），无 atan2 无动态分支，FNA3D 安全；
//绑定噪声实测值域 0.227~0.776，阈值一律过 nrm() 归一
//预乘输出，进 AlphaBlend；连续场无生命周期，替换粒子堆叠地雾的闪烁
// ============================================================================

sampler uNoiseTex : register(s1); //PerlinNoise，消费端上 s1 + LinearWrap
sampler uFieldTex : register(s2); //离地距离场，消费端上 s2 + LinearClamp

float4x4 transformMatrix;

float uTime;          //秒（域 EffectTime）
float uWind;          //风速 px/s，正=向右爬
float uAlpha;         //0~1 总透明度（DreamBlend，归返随闪退场）
float4 uRepulse[6];   //驱散源 xy=世界源心 z=半径px w=孔强01；空槽 z=0
float2 uFieldOrigin;  //距离场窗口原点（世界px，整tile对齐）
float2 uFieldUvMul;   //1/(容量tile数×16px)
float4 uFieldUvClamp; //xy=min uv, zw=max uv（半 texel 内缩到实际窗口子矩形）

//====== 梦雾色板（承接鬼梦沉红） ======
static const float3 FOG_DEEP = float3(0.361, 0.165, 0.157);  //深红灰雾体
static const float3 FOG_LIT  = float3(0.596, 0.290, 0.235);  //吃到红天光的暖亮缘

//====== 带几何（与旧三角带同值：地上带高 / 地下裙边） ======
static const float BAND_H = 96.0;
static const float SKIRT = 26.0;
//R 通道解码：(r - 128/255) × 1020 → ±508px
static const float DIST_BIAS = 128.0 / 255.0;
static const float DIST_SPAN = 1020.0;

float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

//带符号离地距离（世界px入参）：正=空气离地高，负=岩内沉深
float FieldDist(float2 world) {
    float2 uv = clamp((world - uFieldOrigin) * uFieldUvMul, uFieldUvClamp.xy, uFieldUvClamp.zw);
    return (tex2D(uFieldTex, uv).r - DIST_BIAS) * DIST_SPAN;
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
    //世界坐标转发给 PS：距离场/噪声锚定与驱散孔都按世界像素算，相机平移不带着雾走
    o.World = v.Position.xy;
    return o;
}

float4 PSGroundFog(PSInput i) : COLOR0 {
    //离地距离→带内高度01：0=裙底（地下26px） 1=带顶（地上96px），旧三角带纵坐标同语义；
    //带外值不钳（h>1 被顶缘/剖面自然归零，深岩 h<<0 同理）
    float dist = FieldDist(i.World);
    float h = (dist + SKIRT) / (BAND_H + SKIRT);

    //跳变闸：±1tile 场值差读连续性（32px 样距，平滑场恒 32）。
    //水平向读坡度（60°内全保、74°以上闸零，竖直岩壁/崖口在此收没）；
    //竖直向抓薄板底面跳变（板下空气离地极远，与板内沉深断档，压掉底面假雾线），
    //真实地面接触两侧场连续（气+8 岩−8 同斜率），裙边不受累
    float gL = FieldDist(i.World - float2(16.0, 0.0));
    float gR = FieldDist(i.World + float2(16.0, 0.0));
    float gU = FieldDist(i.World - float2(0.0, 16.0));
    float gD = FieldDist(i.World + float2(0.0, 16.0));
    float jump = max(abs(gR - gL), abs(gD - gU));
    float wallGate = saturate((110.0 - jump) / 55.0);

    float xw = i.World.x - uTime * uWind; //风相：整场雾横向缓行

    //顶缘冠线（低频）与雾体纹理（中频），速度错开成两层视差，雾在翻涌而非贴图平移
    float nCrest = nrm(tex2D(uNoiseTex, float2(xw * 0.0021, 0.23 + uTime * 0.008)).r);
    float nBody = nrm(tex2D(uNoiseTex, float2(xw * 0.0058 + uTime * 0.006, h * 0.34 - uTime * 0.011)).r);

    //雾顶在 0.42~0.86 间起伏，顶缘被噪声撕出软边
    float crest = 0.42 + 0.44 * nCrest;
    float edge = saturate((crest - h) * 4.5);
    //底重剖面：地线一带最实、裙边底轻收、向上渐薄
    float profile = saturate(1.15 - h * 1.05) * saturate(h * 5.0 + 0.30);
    float dens = profile * edge * (0.62 + 0.38 * nBody) * wallGate;

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

    float alpha = saturate(dens * 0.62 * (1.0 + rim * 0.35)) * uAlpha;
    return float4(col * alpha, alpha);
}

technique TechGroundFog {
    pass P0 {
        VertexShader = compile vs_3_0 VSFog();
        PixelShader = compile ps_3_0 PSGroundFog();
    }
}
