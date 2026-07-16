using System;
using UnityEngine;
using Utils;

public class ShowVehicle : MonoBehaviour
{
    public VehicleData_SO data;
    public Transform weaponPointL, weaponPointR;
    public Transform LookPoint;
    //public Material material;
    //[HideInInspector]
    public MpbController mpb;
    [SerializeField]
    private Renderer[] renderers;

    private void Start()
    {
        var arch = ArchiveSvc.Archive.VehicleCustomDic[data.vehicleName];
        // 防止存档 index 越界
        int rightIdx = Mathf.Clamp(arch.rightWeaponIndex, 0, data.weaponRights.Length - 1);
        int leftIdx = Mathf.Clamp(arch.leftWeaponIndex, 0, data.weaponLefts.Length - 1);
        int skinIdx = Mathf.Clamp(arch.skinIndex, 0, data.Diffs.Length - 1);
        int blendIdx = Mathf.Clamp(arch.blendIndex, 0, data.Blends.Length - 1);

        if (data.weaponRights.Length > 0)
        {
            var rightModel = Instantiate(data.weaponRights[rightIdx].go, weaponPointR);
            rightModel.transform.localPosition = Vector3.zero;
            rightModel.transform.localRotation = Quaternion.identity;
        }
        if (data.weaponLefts.Length > 0)
        {
            var leftModel = Instantiate(data.weaponLefts[leftIdx].go, weaponPointL);
            leftModel.transform.localPosition = Vector3.zero;
            leftModel.transform.localRotation = Quaternion.identity;
        }
        mpb = renderers.Length > 0 ? new(renderers) : new(transform);
        mpb.Set("_BaseMap", data.Diffs[skinIdx].texture)
        .Set("_BlendingScale", arch.blendScale.RawFloat)
        .Set("_BlendingMap", data.Blends[blendIdx].texture).Apply();
    }
}
