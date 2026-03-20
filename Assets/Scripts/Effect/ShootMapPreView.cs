using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using GameContract;
using Utils;

public class ShootMapPreView : MonoBehaviour
{
    [SerializeField]
    //private RawImage Image;

    [Header("地形参数")]
    public Terrain targetTerrain;
    /// <summary>等高线高度间隔</summary>
    public float contourInterval = 5f;


    [Header("等高线优化参数")]
    [Range(0.01f, 2f)] public float lineWidth = 0.5f;

    //[Header("噪点过滤参数")]
    //[Tooltip("邻域检测范围（越大过滤越彻底）")]
    //[Range(1, 3)] public int noiseFilterRange = 1;
    //[Tooltip("保留像素所需的最小邻域等高线数量（越大过滤越严格）")]
    //[Range(1, 24)] public int minNeighborCount = 1;

    [Header("输出")]
    [SerializeField]
    RenderTexture contourRenderTexture;


    void Awake()
    {
        GameRoot.OnGameStateChange += OnStartTask;
    }
    private void OnDestroy()
    {
        GameRoot.OnGameStateChange -= OnStartTask;
    }

    private void OnStartTask(GameStateEnum exit, GameStateEnum entry)
    {

        if( entry == GameStateEnum.Game)
        {
           GameRoot.CreateTimer(Shoot,Time.deltaTime*2);
        }
    }

    private void Shoot()
    {
        
        int mapSize = TaskManager.Instance.nowTask.MapSize;
        int cameraSize = TaskManager.Instance.nowTask.CameraSize;
        //Vector3 size = new(cameraSize, 256, cameraSize);
        Vector3 center = new Vector3(mapSize, 0, mapSize)/2;
        
        var CameraObj = new GameObject("TmpCamera");
        Camera cam = CameraObj.AddComponent<Camera>();
        cam.transform.localPosition = new Vector3(center.x, 128, center.z);
        cam.transform.eulerAngles = new Vector3(90, 0, 0);

        cam.clearFlags = CameraClearFlags.Depth;
        cam.orthographic = true;//投射方式：orthographic正交//
        RenderSettings.fog = false;
        cam.orthographicSize = cameraSize / 2; //投射区域大小
        var cameraData = cam.GetUniversalAdditionalCameraData();
        cameraData.renderShadows = false; // 仅对该相机生效
        cameraData.renderPostProcessing = true;
        cam.cullingMask =LayerDefinition.GroundLayers & ~1;


        contourRenderTexture = new RenderTexture(cameraSize, cameraSize, 0, RenderTextureFormat.Default);
        contourRenderTexture.Create();

        cam.targetTexture = contourRenderTexture;
        cam.Render();
        RenderSettings.fog = true;
        cam.enabled = false;
        cam.targetTexture = null;
        Destroy(CameraObj, 01.05f);
        

        AddCountour();
        GetComponent<RawImage>().texture = contourRenderTexture;
    }

    private void AddCountour()
    {
        //int textureSize = contourRenderTexture.width;//贴图大小，例如512
        int textureSize = TaskManager.Instance.nowTask.CameraSize;//贴图大小
        // 1. 初始化byte数组
        Texture2D contourTex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        contourTex.filterMode = FilterMode.Bilinear;
        RenderTexture previousActiveRT = RenderTexture.active;
        RenderTexture.active = contourRenderTexture;//更改为激活对象
        contourTex.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
        RenderTexture.active = previousActiveRT;//恢复
        //contourTex.Apply();

        int totalPixels = textureSize * textureSize;
        byte[] pixelBytes = new byte[totalPixels];//实际上byte和bool占用的字节数是一样的

        // 2. 预缓存地形数据（减少重复计算）
        TerrainData terrainData = targetTerrain.terrainData;
        //因为设计需求，地形一定是正方形，所以不需要xz分别处理
        float terrainSize = terrainData.size.x;//例如288
        float heightmapSize = terrainData.heightmapResolution - 1;//获取高度图分辨率并减1，例如1024
        float sizeRatio = terrainSize / textureSize;//计算地形尺寸与输出纹理尺寸的比例关系:288/512=0.4x
        float ratioToSize = heightmapSize / terrainSize;//计算高度图分辨率与地形尺寸的比例关系:1024/288=3.x
        float borderSize = Constants.MapBorder /2;

        byte GetHeight(float X,float Z)//X和Z都是[0,terrainSize]
        {
            //使用插值重新设置为正确的位置
            X = Mathf.Lerp(borderSize, terrainSize - borderSize, X/ terrainSize);
            Z = Mathf.Lerp(borderSize, terrainSize - borderSize, Z/ terrainSize);
            //terrainWidth是512(大小),但是heightmapRes(像素)
            int heightmapX = Mathf.FloorToInt(X * ratioToSize);
            int heightmapZ = Mathf.FloorToInt(Z * ratioToSize);
            float height = terrainData.GetHeight(heightmapX, heightmapZ);
            return (byte)Mathf.Floor(height / contourInterval);
        }

        // 3. 建立高度图+绘制网格
        // 创建临时数组存储过滤结果，避免边遍历边修改导致的错误
        //这里获取的数据直接是[0,512]
        //但是实际上相机获取的是[Constants.MapBorder,512-Constants.MapBorder]
        //所以terrainX实际应该是不一样的
        int gridSize10 = Mathf.FloorToInt(10 * ratioToSize);
        int gridSize50 = gridSize10*5;//这里不能50*ratioToSize
        int gridSize25 = gridSize50 / 2;
        Color[] filteredBytes = contourTex.GetPixels();
        //Debug.LogError("10米等价像素"+gridSize);
        for (int y = 0; y < textureSize; y++)
        {
            int yOffset = y * textureSize;
            float terrainZ = y * sizeRatio;//[0,terrainSize]

            for (int x = 0; x < textureSize; x++)
            {
                int pixelIndex = yOffset + x;
                float terrainX = x * sizeRatio;//[0,terrainSize]
                var color = filteredBytes[pixelIndex];
                //filteredBytes[pixelIndex]= filteredBytes[pixelIndex].MultiplyRGB(0.7f);//降低亮度
                color = color.MultiplyRGB(0.8f);
                color *= color;
                color = color.MultiplyRGB(0.6f);
                // 亮的多降，暗的少降
                //float adjust = 1 - (1 - 0.2f) * color.GetValue();
                //color = color.MultiplyRGB(adjust);
                //filteredBytes[pixelIndex] = color;
                filteredBytes[pixelIndex] = Color.Lerp(color,Color.white.MultiplyRGB(color.grayscale), 0.7f);

                //绘制网格也顺便在这边弄了
                if (x % gridSize10 == 0 || y % gridSize10 == 0)
                {
                    float scale = 0.03f;

                    if (x % gridSize50 == 0 || y % gridSize50 == 0)
                    {
                        scale = 0.05f;
                        
                        if (Mathf.Abs((x % gridSize50) - gridSize25) > gridSize25 - 5 && Mathf.Abs((y % gridSize50) - gridSize25) > gridSize25 - 5)
                        {
                            scale = 0.15f;
                        }
                    }
                    filteredBytes[pixelIndex] += Color.white * scale;
                }


                byte nowHeight = GetHeight(terrainX, terrainZ);
                int contourCount = 0;
                //遍历周围8格
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= textureSize) continue;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= textureSize) continue;

                        float nxWorld = terrainX + dx * sizeRatio;
                        float nzWorld = terrainZ + dy * sizeRatio;
                        if(nowHeight == GetHeight(nxWorld, nzWorld))
                        {
                            contourCount++;
                        }
                    }
                }
                if (contourCount<8)//周围有和自己不一样的
                {
                    filteredBytes[pixelIndex] += Color.white * 0.03f;
                }

   
            }
        }

        

        // 5. 写入纹理（使用过滤后的数组）

        //contourTex.LoadRawTextureData(filteredBytes);
        contourTex.SetPixels(filteredBytes);
        contourTex.Apply();

        // 6. 输出到RenderTexture
        Graphics.Blit(contourTex, contourRenderTexture);

        // 释放资源
        DestroyImmediate(contourTex);
    }

 
    // 编辑器下的测试按钮
    [ContextMenu("生成纹理")]
    private void GenerateContourTextureInEditor()
    {
        Shoot();
    }
}
