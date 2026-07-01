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
        if (data.weaponRights.Length > 0)
        {
            var rightModel = Instantiate(data.weaponRights[arch.rightWeaponIndex].go, weaponPointR);
            rightModel.transform.localPosition = Vector3.zero;
            rightModel.transform.localRotation = Quaternion.identity;
        }
        if (data.weaponLefts.Length > 0)
        {
            var leftModel = Instantiate(data.weaponLefts[arch.leftWeaponIndex].go, weaponPointL);
            leftModel.transform.localPosition = Vector3.zero;
            leftModel.transform.localRotation = Quaternion.identity;
        }
        mpb = renderers.Length>0?new(renderers): new(transform);
        mpb.Set("_BaseMap", data.Diffs[arch.skinIndex].texture)
        .Set("_BlendingScale", arch.blendScale.RawFloat)
        .Set("_BlendingMap", data.Blends[arch.blendIndex].texture).Apply();
    }
}
