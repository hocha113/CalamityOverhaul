// ============================================================================
//KikasaUmbrellaTrail.fx 悬伞墨痕拖尾(世界空间 TriangleStrip,vs+ps)
//整个伞面在空气里犁过留下的一幅墨幕(头部满宽≈伞盖直径,宽度由 C# 顶点几何给):
//淡幕垫底、横向噪声切出 2~4 股浓墨丝缕骑在幕上(股束沿带向连续、随寿命散开侧漂)、
//宽幕长边蚀得松而毛、尾端阈值抬升大口撕丝、轴心一线血芯尾部先断、新段窄湿光
//
//为什么独立成文件:带 VertexShader 的 technique 一旦在某个 Effect 实例上 Apply 过,
//MojoShader 会把它残留在 effect->current_vert_raw 里永不清除;此后同一实例任何
//ps-only pass 的 CommitChanges 都会按残留顶点着色器的符号表,把 transformMatrix
//写进"当前绑定的顶点着色器"的常量寄存器——SpriteBatch 批下即精灵着色器的投影
//矩阵(c0..c3)被世界矩阵顶掉,整批精灵瞬移出屏。故本效果只许被顶点图元消费,
//绝不进 SpriteBatch;伞体的 ps-only technique 留在 KikasaUmbrella.fx
//
//顶点契约(KikasaRainRender.DrawOneInkTrail 同源):
//uv.x=沿带弧长(32px 一单位,世界锚定——点定下后纹理不追着带跑,只留极慢晕开漂移)
//uv.y=0..1 横跨条带;顶点色 R=剩余寿命 G=速度强度 A=头部整体透明度
//坐标全笛卡尔;直线算术+普通 tex2D,FNA3D 安全;预乘输出进 AlphaBlend 批
//s1=PerlinNoise(实测值域 0.227~0.776,阈值前归一);s0 不采样
//消费入口 KikasaRains/KikasaRainRender.cs DrawInkTrails
// ============================================================================

sampler uImage1 : register(s1);

float4x4 transformMatrix;   //世界→屏幕,世界坐标顶点直入

float uTime;
float uSeed;        //个体相位(伞的 identity)
float3 uColInk;     //墨体近黑
float3 uColDeep;    //暗血缘
float3 uColCore;    //血芯
float3 uColSheen;   //湿反光

float noiseTex(float2 uv) {
    return tex2D(uImage1, uv).r;
}

struct VSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 TexCoords: TEXCOORD0;
};

PSInput VSTrail(VSInput v) {
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PSTrail(PSInput input) : COLOR0 {
    float u = input.TexCoords.x;
    float cx = input.TexCoords.y * 2.0 - 1.0;   //-1..1 横向,0=轴心
    float lifeT = input.Color.r;
    float strength = input.Color.g;
    float headA = input.Color.a;
    float age = 1.0 - lifeT;

    //错频噪声:n1 蚀边揉形(横向频率提到宽幕量级,长边才有起伏),n2 尾端撕丝;
    //漂移压到极慢=墨在空气里晕开
    float n1 = noiseTex(float2(u * 0.55 + uSeed, cx * 0.95 + uSeed * 1.7 + uTime * 0.10));
    float n2 = noiseTex(float2(u * 0.95 - uSeed * 2.3, cx * 0.50 - uTime * 0.06));
    n1 = saturate((n1 - 0.227) / 0.549);
    n2 = saturate((n2 - 0.227) / 0.549);

    //股束:带向低频=丝缕沿带连续,横向中频切出 2~4 股浓墨;
    //寿命走低横向放缓+随 n1 侧漂=股束随幕老化散开
    float sy = cx * (0.55 - 0.20 * age) + (n1 - 0.5) * age * 0.35;
    float n3 = noiseTex(float2(u * 0.10 + uSeed * 2.7, sy + uSeed * 4.1));
    n3 = saturate((n3 - 0.227) / 0.549);
    float strand = smoothstep(0.44, 0.74, n3);

    //墨体:中实边蚀;宽幕长边蚀得松而毛,老段与弱段更碎
    float erode = 0.34 + 0.38 * age + 0.16 * (1.0 - strength);
    float edge = 1.0 - smoothstep(0.26, 1.0, abs(cx) + (n1 - 0.5) * erode);

    //尾端撕丝:撕口以股束场为主驱动——绸先在股间掉幕、股缕存续成丝带,
    //丝再断;寿命走低阈值抬升,细渣项只占小头
    float tearN = n3 * 0.62 + n2 * 0.24 + n1 * 0.14;
    float tearTh = 0.08 + age * 0.46;
    float tear = smoothstep(tearTh, tearTh + 0.24, tearN);
    float body = edge * tear;

    //浓度:淡幕垫底、股束把浓墨压上去;速度强度当墨量,老段沉;
    //末梢整体熄灭,不留悬空墨屑
    float density = body * strength * (0.38 + 0.62 * lifeT) * (0.55 + 0.60 * strand)
        * smoothstep(0.0, 0.12, lifeT);

    //色:墨黑为体,股束更沉;暗血只在蚀缘处透一点
    float3 col = lerp(uColDeep, uColInk,
        saturate(0.52 + 0.34 * strand + 0.16 * (1.0 - abs(cx)) - n1 * 0.18));

    //血芯:轴心一线暗红,尾部先断(同一撕丝场门控,芯不悬空);宽幕下收窄保持一线
    float core = exp2(-cx * cx * 80.0) * smoothstep(0.30, 0.85, lifeT) * tear;

    //新段窄湿光:剥离瞬间的水光,几段内退去
    float dSheen = cx - 0.25;
    float sheen = exp2(-dSheen * dSheen * 34.0) * smoothstep(0.72, 1.0, lifeT) * strength;

    //宽幕摊薄墨量,总透明度较细线版略提;不糊屏由 density 的幕/股分层管着
    float a = saturate(density * 0.76) * headA;
    float3 outCol = col * a
        + uColCore * core * 0.20 * strength * headA
        + uColSheen * sheen * 0.16 * headA;
    return float4(outCol, a);
}

technique TechTrail {
    pass TrailPass {
        VertexShader = compile vs_3_0 VSTrail();
        PixelShader = compile ps_3_0 PSTrail();
    }
}
