// ============================================================================
//HimayoPanorama.fx 主菜单全景：按视线方向采样等距柱状投影图
//全屏 quad，相机基向量在 C# 侧算好传入，本着色器只做方向→UV 映射
//s0=equirect 底图（须 LinearWrap，经度缝由硬件环绕处理） s1=PerlinNoise
//经度 atan2 直接喂 tex2D 属连续性安全路径（VFX.md 极坐标缝规则：仅限直接采样）
//底图 3548x1774（2:1），双线性直采放大后发糊
//改用 Catmull-Rom 双三次（9 次双线性等效 4x4 卷积，负瓣自带锐化）
//水平投影为柱面等角（垂直保持中心透视）：广角下屏幕边缘不再横向发胖，
//且与 C# 侧花瓣投影 HimayoMenuCamera.Project 共用同一几何，转头时流速一致
//底图实为伪全景（左右缝差 19.5 对相邻列基线 3.9，非无缝 360°；中央人物按
//平面构图绘制），uLonScale>1 声明其经度跨度不足 360°，收窄横向防人物拉宽
// ============================================================================

sampler uImage0 : register(s0);
sampler uImage1 : register(s1);

float3 uForward;   //视线前向，单位向量
float3 uRight;     //视平面右向
float3 uUp;        //视平面上向
float uTanHalfFov; //tan(垂直FOV/2)
float uAspect;     //视口宽高比
float uHalfHFov;   //水平半FOV（弧度）= atan(uTanHalfFov*uAspect)，C# 算好传入
float uLonScale;   //经度压缩：>1=底图按不足360°解读，内容横向收窄（校准值 1.30）
float uLatScale;   //纬度压缩，绕赤道对称，默认 1
float uFade;       //0~1 入场淡入
float uTime;       //颗粒缓移
float2 uTexSize;   //equirect 底图像素尺寸

static const float PI = 3.14159265;
static const float TAU = 6.28318531;

//Catmull-Rom 放大采样：越缝取样坐标由采样器 Wrap 兜住，无需手动处理
float3 sampleCatmullRom(float2 uv) {
    float2 samplePos = uv * uTexSize;
    float2 texPos1 = floor(samplePos - 0.5) + 0.5;
    float2 f = samplePos - texPos1;

    float2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
    float2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
    float2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
    float2 w3 = f * f * (-0.5 + 0.5 * f);

    //中间两列/两行合并为一次双线性取样，4x4 卷积降为 9 次取样
    float2 w12 = w1 + w2;
    float2 offset12 = w2 / w12;

    float2 pos0 = (texPos1 - 1.0) / uTexSize;
    float2 pos3 = (texPos1 + 2.0) / uTexSize;
    float2 pos12 = (texPos1 + offset12) / uTexSize;

    float3 result =
          tex2D(uImage0, float2(pos0.x, pos0.y)).rgb * (w0.x * w0.y)
        + tex2D(uImage0, float2(pos12.x, pos0.y)).rgb * (w12.x * w0.y)
        + tex2D(uImage0, float2(pos3.x, pos0.y)).rgb * (w3.x * w0.y)
        + tex2D(uImage0, float2(pos0.x, pos12.y)).rgb * (w0.x * w12.y)
        + tex2D(uImage0, float2(pos12.x, pos12.y)).rgb * (w12.x * w12.y)
        + tex2D(uImage0, float2(pos3.x, pos12.y)).rgb * (w3.x * w12.y)
        + tex2D(uImage0, float2(pos0.x, pos3.y)).rgb * (w0.x * w3.y)
        + tex2D(uImage0, float2(pos12.x, pos3.y)).rgb * (w12.x * w3.y)
        + tex2D(uImage0, float2(pos3.x, pos3.y)).rgb * (w3.x * w3.y);
    //负瓣可能轻微过冲，夹回色域
    return saturate(result);
}

float4 PSPanorama(float2 coords : TEXCOORD0) : COLOR0 {
    //屏幕 y 向下为正，转相机空间取负
    float2 ndc = (coords - 0.5) * 2.0;
    //柱面等角：屏幕横向均匀分角，先在水平面内旋出列方向，再叠垂直 tan 分量
    float theta = ndc.x * uHalfHFov;
    float3 colDir = uForward * cos(theta) + uRight * sin(theta);
    float3 dir = normalize(colDir - uUp * (ndc.y * uTanHalfFov));

    //方向 → 等距柱状 UV；经纬各按声明跨度缩放；v 因俯仰被 C# 夹角限制，永不触及极点
    float u = atan2(dir.x, dir.z) / TAU * uLonScale + 0.5;
    float v = (acos(clamp(dir.y, -1.0, 1.0)) / PI - 0.5) * uLatScale + 0.5;
    float3 col = sampleCatmullRom(float2(u, v));

    //低幅颗粒缓移，柔化放大后残余的平滑面
    float grain = tex2D(uImage1, coords * float2(5.0 * uAspect, 5.0)
        + uTime * float2(0.013, -0.009)).r;
    col += (grain - 0.5) * 0.022;

    //边缘轻压暗，聚焦画面中心
    float vig = 1.0 - dot(ndc, ndc) * 0.10;

    return float4(col * vig * uFade, 1.0);
}

technique TechPanorama {
    pass P0 {
        PixelShader = compile ps_3_0 PSPanorama();
    }
}
