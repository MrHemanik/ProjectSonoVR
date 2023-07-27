using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SonoGame;
using mKit;

// uses Assets/DemoApp/Scripts/DemoApp1.cs as base
public class VolumeGenerationManager : MonoBehaviour
{
    #region Variables
    private EVisualization visualization = EVisualization.Colored;
    private UltrasoundScannerTypeEnum scannerType = UltrasoundScannerTypeEnum.CURVED;

    /// <summary>
    /// Position anchor for volume placement
    /// </summary>
    [Header("Scene")] public Transform[] volumeAnchors;

    /// <summary>
    /// Probe-attached visual placeholder for the mKit slice
    /// </summary>
    public Transform sliceCopyTransform;

    /// <summary>
    /// MaterialConfig for different visualizations
    /// </summary>
    public MaterialConfig materialConfig;

    /// <summary>
    /// 2D slice view (on Canvas with RawImage)
    /// </summary>
    public SliceView sliceViewRawImage;

    /// <summary>
    /// 2D slice view (on Unity quad mesh)
    /// </summary>
    public SliceView sliceViewQuad;

    /// <summary>
    /// still 2D slice view (on Canvas with RawImage)
    /// </summary>
    public GameObject stillSliceViewRawImage;

    private List<Level> levelList;

    private int winningAnswerVolumeId;
    #endregion
    private void Awake()
    {
        levelList = new LevelList(materialConfig).levelList;
    }

    IEnumerator Start()
    {
        Debug.Log(Application.dataPath);
        enabled = false; // will be re-enabled after generating artificials

        VolumeManager.Instance.SetMaterialConfig(materialConfig);

        yield return GenerateVolumeWithVolumeManager();
        SetWinningAnswerVolume();
        yield return GetStillDefaultSlice(winningAnswerVolumeId, stillSliceViewRawImage.GetComponent<RawImage>());
    }

    void Update()
    {
        // make mKit slice follow sliceTransform
        Volume.Volumes[0].ToolTransform.SetPositionAndRotation(sliceCopyTransform.position, sliceCopyTransform.rotation * Quaternion.Euler(-90, 0, 0));
        //Volume.Volumes[0].SetToolSize(new Vector2(sliceCopyTransform.transform.localScale.x, sliceCopyTransform.transform.localScale.y));
    }

    #region GenerateVolumeWithVolumeManager

    IEnumerator GenerateVolumeWithVolumeManager()
    {
        yield return VolumeManager.Instance.GenerateArtificialVolume(levelList[0].volumeList[0], volumeSlot: 0,
            addObjectModels: true);
        yield return VolumeManager.Instance.GenerateArtificialVolume(levelList[0].volumeList[1], volumeSlot: 1,
            addObjectModels: true);
        Debug.Log("GenerateArtificialVolume finished");

        /*
         * position of volume needs to be set before configuration
         * Without:         https://i.imgur.com/4Su6Wyp.png
         * With:            https://i.imgur.com/iIdQK1p.png
         * Problem as GIF:  https://i.imgur.com/RKlqztO.gif
         */
        Volume.Volumes[0].ToolTransform.SetPositionAndRotation(sliceCopyTransform.position,
            sliceCopyTransform.rotation * Quaternion.Euler(-90, 0, 0));
        Volume.Volumes[0].SetToolSize(new Vector2(sliceCopyTransform.transform.localScale.x, sliceCopyTransform.localScale.y));

        for (int i = 0; i < Volume.Volumes.Count; i++)
        {
            ConfigureVolume(Volume.Volumes[i], scannerType, visualization, i);
            ConfigureSliceViews(Volume.Volumes[i], scannerType, visualization);
            
            //Bugfix: problem where render is flickering, Gets temporarily fixed when clicking on OsCamera, even when it is inactive at the time. Changing the CameraType also works
            Volume.Volumes[i].GetToolgroupCamera(0).cameraType = CameraType.Preview;
        }
        /*foreach (var volume in Volume.Volumes) //Initializes Visualization
        {
            ConfigureVolume(volume, scannerType, visualization, 0);
            ConfigureSliceViews(volume, scannerType, visualization);
        }*/

        enabled = true; // enable Update()
    }

    #endregion

    #region SliceViewConfiguration

    void ConfigureSliceViews(Volume v, UltrasoundScannerTypeEnum scannerType, EVisualization visualization)
    {
        MultiVolumeSlice mvs = GetComponent<MultiVolumeSlice>();
        if (mvs == null)
        {
            sliceViewQuad.InitSliceView(visualization, scannerType,
                v.GetSliceRenderTexture()); // assign mkit texture to slice display
            sliceViewRawImage.InitSliceView(visualization, scannerType,
                v.GetSliceRenderTexture()); // assign mkit texture to slice display
        }
        else
        {
            // multi-volume-slice Material
            Material mvsMat = mvs.SetupMultiVolumeSlice(0, visualization, Volume.Volumes[0].GetToolSize(0));
            sliceViewRawImage.SetMaterial(mvsMat);
            sliceViewQuad.SetMaterial(mvsMat);
        }

        //sliceCopyTransform.SetSliceMask(scannerType);
    }

    #endregion

    #region VolumeConfiguration

    void ConfigureVolume(Volume v, UltrasoundScannerTypeEnum scannerType, EVisualization visualization, int index)
    {
        v.SliceMaskingTexture = AppConfig.assets.GetScannerMask(scannerType);
        v.UseSliceMasking = scannerType != UltrasoundScannerTypeEnum.LINEAR;
        v.UltrasoundScannerType = scannerType;
        VolumeManager.Instance.UseMaterialConfigVisualization(v, visualization);
        UltrasoundSimulation.Instance.Init(v);

        v.VolumeProxy.position = volumeAnchors[index].position; // set volume position
        GameObject.Find("mKitVolume #" + index + " (ArtificialVolume.vm2)").transform
            .SetParent(volumeAnchors[index]); //set volumeAnchor as parent of volume
        v.Threshold = 0.001f;
        Debug.Log(GameObject.Find("mKitVolume #0 (ArtificialVolume.vm2)").name);
    }

    #endregion

    #region GetStillDefaultSlice

    /// <summary>
    /// Generates a texture from volume with id of volumeId in default position (centered, straight from the top directed at the bottom) and assigns it to the RawImage image
    /// </summary>
    IEnumerator GetStillDefaultSlice(int volumeId, RawImage image)
    {
        //The image will flash on the normal imageslice, for now it shouldn't be a problem as it will only be called right after object generation
        Transform sliceAnchorTransform = sliceCopyTransform.parent.GetChild(3).transform;
        Vector3 defaultPosition = sliceAnchorTransform.position;
        Quaternion defaultRotation = sliceAnchorTransform.rotation;
        yield return sliceAnchorTransform.position =
            GameObject.Find("VolumeAnchor (Volume" + (volumeId + 1) + ")").transform.position;
        yield return sliceAnchorTransform.rotation = Quaternion.identity;
        image.texture =
            VolumeManager.Instance
                .GetSliceCamCapture(Volume.Volumes[volumeId]); //Adds still shot of volume 0 to stillView
        sliceAnchorTransform.position = defaultPosition;
        sliceAnchorTransform.rotation = defaultRotation;
        //return null;
    }
    #endregion
    
    #region winningVolume

    private void SetWinningAnswerVolume()
    {
        winningAnswerVolumeId = UnityEngine.Random.Range(0, Volume.Volumes.Count);
        Debug.Log(winningAnswerVolumeId);
    }
    #endregion
}