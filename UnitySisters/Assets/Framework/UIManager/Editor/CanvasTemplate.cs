using System.Collections;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

using UnityFramework.UI;


[CreateAssetMenu(fileName = "CanvasTemplate", menuName = "UnityFramework/CreateCanvasTemplate")]
public class CanvasTemplate : ScriptableObject
{
    [SerializeField] CanvasScaler.ScaleMode scaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    [SerializeField] Vector2 resolution = new Vector2(1920.0f, 1080.0f);
    [SerializeField][Range(0.0f, 1.0f)] float match = 0.5f;
    [SerializeField] bool useGraphicRaycaster = true;


    public CanvasScaler.ScaleMode ScaleMode => scaleMode;
    public Vector2 Resolution => resolution;
    public float Match => match;
    public bool UseGraphicRaycaster => useGraphicRaycaster;


    
}


public class CanvasTemplateCreater
{
    [MenuItem("GameObject/UnityFramework/CreateCanvasTemplate",false,0)]
    public static void CreaterCanvs()
    {
        string[] guids = AssetDatabase.FindAssets("t:CanvasTemplate");

        if (guids.Length == 0)
        {
            Debug.LogWarning("CanvasTemplate媛 ?놁뒿?덈떎");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        CanvasTemplate canvasTemplate = AssetDatabase.LoadAssetAtPath<CanvasTemplate>(path);


        GameObject canvasObject = new GameObject("Canvas");
        canvasObject.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = canvasTemplate.ScaleMode;
        canvasScaler.referenceResolution = canvasTemplate.Resolution;
        canvasScaler.matchWidthOrHeight = canvasTemplate.Match;
        if (canvasTemplate.UseGraphicRaycaster)
            canvasObject.AddComponent<GraphicRaycaster>();
    }

    [MenuItem("GameObject/UI/SubCanvas", false,2059)]
    public static void CreaterSubCanvs()
    {
        GameObject canvasObject = null;
        canvasObject = Selection.activeGameObject;
        if (Selection.activeGameObject == null)
            canvasObject = CreateCanvas();
               
        GameObject subCanvasObject = new GameObject("SubCanvas");
        subCanvasObject.layer = LayerMask.NameToLayer("UI");
        subCanvasObject.transform.parent = canvasObject.transform;  
        Canvas canvas = subCanvasObject.AddComponent<Canvas>();
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        subCanvasObject.AddComponent<GraphicRaycaster>();
        subCanvasObject.AddComponent<SubUI>();
    }

    private static GameObject CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas");
        canvasObject.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasObject.AddComponent<GraphicRaycaster>();
        
        return canvasObject;
    }
}